using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using GTA2Clone.Missions;

namespace GTA2Clone.UI
{
    /// <summary>
    /// HUD desacoplado do Sistema de Missões Infinito (Arcade Loop).
    /// Escuta os eventos do MissionManager para atualizar:
    /// - O objetivo atual (Verde/Amarelo)
    /// - O cronômetro do Banco de Tempo Global (pulsando em vermelho abaixo de 10s)
    /// - O dinheiro acumulado ($) e contador de entregas
    /// - O banner de recompensa ($ + tempo extra)
    /// - A tela de Game Over com botão e atalho de reinício (Enter / R)
    /// </summary>
    public class MissionHudUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MissionManager missionManager;
        [SerializeField] private Text objectiveText;
        [SerializeField] private Image objectiveAccentBar;
        [SerializeField] private Text timerText;
        [SerializeField] private Text moneyText;
        [SerializeField] private Text deliveriesCountText;
        [SerializeField] private CanvasGroup rewardPopupGroup;
        [SerializeField] private Text rewardPopupText;

        [Header("Game Over UI")]
        [SerializeField] private CanvasGroup gameOverGroup;
        [SerializeField] private Text gameOverStatsText;
        [SerializeField] private Button restartButton;

        [Header("Theme Colors")]
        [SerializeField] private Color stealPhaseColor = new Color(0.18f, 0.96f, 0.38f, 1f);
        [SerializeField] private Color deliverPhaseColor = new Color(1.0f, 0.86f, 0.14f, 1f);
        [SerializeField] private Color normalTimerColor = new Color(1.0f, 0.92f, 0.28f, 1f);
        [SerializeField] private Color criticalTimerColor = new Color(1.0f, 0.20f, 0.20f, 1f);
        [SerializeField] private float criticalTimeThreshold = 10f;

        [Header("Popup Timing")]
        [SerializeField] private float popupDisplayDuration = 2.6f;
        [SerializeField] private float popupFadeDuration = 0.35f;

        private Coroutine popupCoroutine;
        private bool isShowingGameOver;

        private void Awake()
        {
            if (rewardPopupGroup != null)
            {
                rewardPopupGroup.alpha = 0f;
            }

            if (gameOverGroup != null)
            {
                gameOverGroup.alpha = 0f;
                gameOverGroup.interactable = false;
                gameOverGroup.blocksRaycasts = false;
            }

            UpdateScoreDisplay(0, 0);
            HandleTimerUpdated(60f);
        }

        private void OnEnable()
        {
            BindEvents();
        }

        private void OnDisable()
        {
            UnbindEvents();
        }

        private void Update()
        {
            if (!isShowingGameOver || missionManager == null)
                return;

            // Permite reiniciar rapidamente pelo teclado (R ou Enter) ou Gamepad
            Keyboard kb = Keyboard.current;
            if (kb != null && (kb.rKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
            {
                OnRestartClicked();
                return;
            }

            Gamepad pad = Gamepad.current;
            if (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame))
            {
                OnRestartClicked();
            }
        }

        /// <summary>
        /// Configura as referências de UI do MissionHudUI (usado pelas ferramentas de Editor).
        /// </summary>
        public void Configure(
            MissionManager manager,
            Text objective,
            Image accentBar,
            Text timer,
            Text money,
            Text deliveries,
            CanvasGroup popupGroup,
            Text popupText,
            CanvasGroup gameOverCanvasGroup,
            Text gameOverStats,
            Button restartBtn)
        {
            UnbindEvents();
            missionManager = manager;
            objectiveText = objective;
            objectiveAccentBar = accentBar;
            timerText = timer;
            moneyText = money;
            deliveriesCountText = deliveries;
            rewardPopupGroup = popupGroup;
            rewardPopupText = popupText;
            gameOverGroup = gameOverCanvasGroup;
            gameOverStatsText = gameOverStats;
            restartButton = restartBtn;

            if (isActiveAndEnabled)
            {
                BindEvents();
            }
        }

        private void BindEvents()
        {
            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
                restartButton.onClick.AddListener(OnRestartClicked);
            }

            if (missionManager == null) return;

            missionManager.OnContractStarted -= HandleMissionUpdated;
            missionManager.OnContractStarted += HandleMissionUpdated;

            missionManager.OnMissionPhaseChanged -= HandleMissionUpdated;
            missionManager.OnMissionPhaseChanged += HandleMissionUpdated;

            missionManager.OnContractCompleted -= HandleContractCompleted;
            missionManager.OnContractCompleted += HandleContractCompleted;

            missionManager.OnTimerUpdated -= HandleTimerUpdated;
            missionManager.OnTimerUpdated += HandleTimerUpdated;

            missionManager.OnGameOver -= HandleGameOver;
            missionManager.OnGameOver += HandleGameOver;

            UpdateScoreDisplay(missionManager.TotalMoney, missionManager.CompletedMissionsCount);
            if (missionManager.RemainingTimeSeconds > 0f)
            {
                HandleTimerUpdated(missionManager.RemainingTimeSeconds);
            }

            if (missionManager.CurrentContract != null)
            {
                HandleMissionUpdated(new ActiveMissionInfo(
                    missionManager.CurrentMissionNumber,
                    missionManager.CurrentContract,
                    missionManager.CurrentTargetVehicle,
                    missionManager.ActiveDeliveryZone,
                    missionManager.CurrentPhase,
                    false
                ));
            }
        }

        private void UnbindEvents()
        {
            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
            }

            if (missionManager == null) return;

            missionManager.OnContractStarted -= HandleMissionUpdated;
            missionManager.OnMissionPhaseChanged -= HandleMissionUpdated;
            missionManager.OnContractCompleted -= HandleContractCompleted;
            missionManager.OnTimerUpdated -= HandleTimerUpdated;
            missionManager.OnGameOver -= HandleGameOver;
        }

        private void HandleMissionUpdated(ActiveMissionInfo info)
        {
            if (objectiveText == null || info.Contract == null)
                return;

            if (info.Phase == MissionPhase.DeliverVehicle)
            {
                string zoneLabel = info.TargetDeliveryZone != null
                    ? info.TargetDeliveryZone.ZoneName.ToUpperInvariant()
                    : "DESMANCHE";

                objectiveText.text = $"ENTREGUE EM: {zoneLabel}";
                objectiveText.color = deliverPhaseColor;
                if (objectiveAccentBar != null)
                {
                    objectiveAccentBar.color = deliverPhaseColor;
                }
            }
            else if (info.Phase == MissionPhase.StealVehicle)
            {
                string vehicleName = info.Contract.TargetVehicleDisplayName.ToUpperInvariant();
                objectiveText.text = $"ROUBE: {vehicleName}";
                objectiveText.color = stealPhaseColor;
                if (objectiveAccentBar != null)
                {
                    objectiveAccentBar.color = stealPhaseColor;
                }
            }
        }

        private void HandleTimerUpdated(float remainingSeconds)
        {
            if (timerText == null) return;

            float clamped = Mathf.Max(0f, remainingSeconds);
            int minutes = Mathf.FloorToInt(clamped / 60f);
            float seconds = clamped - (minutes * 60f);

            timerText.text = minutes > 0
                ? $"{minutes:00}:{seconds:00.0}"
                : $"{seconds:00.0}s";

            if (clamped <= criticalTimeThreshold && clamped > 0f)
            {
                timerText.color = criticalTimerColor;
                float pulse = 1f + 0.14f * Mathf.Abs(Mathf.Sin(Time.time * 8f));
                timerText.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
            }
            else
            {
                timerText.color = clamped <= 0f ? criticalTimerColor : normalTimerColor;
                timerText.rectTransform.localScale = Vector3.one;
            }
        }

        private void HandleContractCompleted(MissionCompletionResult result)
        {
            UpdateScoreDisplay(result.TotalPlayerMoney, result.CompletedMissionsCount);

            if (rewardPopupGroup != null && rewardPopupText != null)
            {
                string bonusStr = result.TimeBonus > 0 ? $" (BÔNUS +${result.TimeBonus:N0})" : string.Empty;
                int addedSecInt = Mathf.RoundToInt(result.AddedTimeSeconds);
                rewardPopupText.text = $"ENTREGUE! +${result.TotalReward:N0}{bonusStr}  |  +{addedSecInt}s NO RELÓGIO";

                if (popupCoroutine != null)
                {
                    StopCoroutine(popupCoroutine);
                }
                popupCoroutine = StartCoroutine(ShowRewardPopupRoutine());
            }
        }

        private void HandleGameOver(GameOverResult result)
        {
            isShowingGameOver = true;

            if (objectiveText != null)
            {
                objectiveText.text = "TEMPO ESGOTADO!";
                objectiveText.color = criticalTimerColor;
            }
            if (objectiveAccentBar != null)
            {
                objectiveAccentBar.color = criticalTimerColor;
            }

            if (gameOverStatsText != null)
            {
                int survMin = Mathf.FloorToInt(result.TotalSurvivedSeconds / 60f);
                int survSec = Mathf.FloorToInt(result.TotalSurvivedSeconds % 60f);
                gameOverStatsText.text =
                    $"DINHEIRO ACUMULADO:  ${result.TotalPlayerMoney:N0}\n" +
                    $"VEÍCULOS ENTREGUES:  {result.CompletedMissionsCount}\n" +
                    $"TEMPO DE CORRIDA:    {survMin:00}:{survSec:00}";
            }

            if (gameOverGroup != null)
            {
                gameOverGroup.interactable = true;
                gameOverGroup.blocksRaycasts = true;
                StartCoroutine(FadeCanvasGroup(gameOverGroup, 1f, 0.35f));
            }
        }

        private void OnRestartClicked()
        {
            if (missionManager != null)
            {
                missionManager.RestartGame();
            }
        }

        private void UpdateScoreDisplay(int totalMoney, int deliveriesCount)
        {
            if (moneyText != null)
            {
                moneyText.text = $"${totalMoney:000000}";
            }

            if (deliveriesCountText != null)
            {
                deliveriesCountText.text = $"ENTREGAS: {deliveriesCount}";
            }
        }

        private IEnumerator ShowRewardPopupRoutine()
        {
            yield return FadeCanvasGroup(rewardPopupGroup, 1f, 0.15f);
            yield return new WaitForSeconds(popupDisplayDuration);
            yield return FadeCanvasGroup(rewardPopupGroup, 0f, popupFadeDuration);
            popupCoroutine = null;
        }

        private static IEnumerator FadeCanvasGroup(CanvasGroup group, float targetAlpha, float duration)
        {
            if (group == null) yield break;

            float startAlpha = group.alpha;
            if (duration <= 0.001f)
            {
                group.alpha = targetAlpha;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                group.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            group.alpha = targetAlpha;
        }
    }
}
