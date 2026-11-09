using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using GTA2Clone.Missions;
using GTA2Clone.Police;

namespace GTA2Clone.UI
{
    /// <summary>
    /// HUD desacoplado do Sistema de Missões Infinito (Arcade Loop) e do Sistema de Polícia (Wanted Level).
    /// Escuta os eventos do MissionManager e PoliceManager para atualizar:
    /// - O objetivo atual (Verde/Amarelo)
    /// - O cronômetro do Banco de Tempo Global (pulsando em vermelho abaixo de 10s)
    /// - O dinheiro acumulado ($), contador de entregas e as 6 estrelas de Nível de Procurado
    /// - O banner de recompensa ($ + tempo extra) e alertas de perseguição policial
    /// - A tela de Game Over (Tempo Esgotado, Preso pela Polícia / Busted, ou Veículo Destruído / Wasted)
    /// </summary>
    public class MissionHudUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MissionManager missionManager;
        [SerializeField] private PoliceManager policeManager;
        [SerializeField] private Text objectiveText;
        [SerializeField] private Image objectiveAccentBar;
        [SerializeField] private Text timerText;
        [SerializeField] private Text moneyText;
        [SerializeField] private Text deliveriesCountText;
        [SerializeField] private CanvasGroup rewardPopupGroup;
        [SerializeField] private Image rewardPopupBackground;
        [SerializeField] private Text rewardPopupText;

        [Header("Wanted Level HUD (6 Stars)")]
        [SerializeField] private Image wantedPanelBackground;
        [SerializeField] private Text wantedStarsText;
        [SerializeField] private Text wantedStatusText;

        [Header("Game Over UI")]
        [SerializeField] private CanvasGroup gameOverGroup;
        [SerializeField] private Text gameOverTitleText;
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
        private int currentDisplayedStars;

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
            UpdateWantedStarsDisplay(0);
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
            if (currentDisplayedStars > 0 && !isShowingGameOver)
            {
                AnimateWantedSirenBorder();
            }

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
            Text popupTxt,
            CanvasGroup gameOverCanvasGroup,
            Text gameOverStats,
            Button restartBtn,
            PoliceManager policeMgr = null,
            Image wantedBg = null,
            Text wantedStars = null,
            Text wantedStatus = null,
            Text gameOverTitle = null,
            Image popupBg = null)
        {
            UnbindEvents();
            missionManager = manager;
            policeManager = policeMgr;
            objectiveText = objective;
            objectiveAccentBar = accentBar;
            timerText = timer;
            moneyText = money;
            deliveriesCountText = deliveries;
            rewardPopupGroup = popupGroup;
            rewardPopupBackground = popupBg != null ? popupBg : (popupGroup != null ? popupGroup.GetComponent<Image>() : null);
            rewardPopupText = popupTxt;
            gameOverGroup = gameOverCanvasGroup;
            gameOverTitleText = gameOverTitle;
            gameOverStatsText = gameOverStats;
            restartButton = restartBtn;
            wantedPanelBackground = wantedBg;
            wantedStarsText = wantedStars;
            wantedStatusText = wantedStatus;

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

            if (missionManager != null)
            {
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

            if (policeManager != null)
            {
                policeManager.OnWantedStarsChanged -= HandleWantedStarsChanged;
                policeManager.OnWantedStarsChanged += HandleWantedStarsChanged;

                policeManager.OnPoliceAlertTriggered -= HandlePoliceAlertTriggered;
                policeManager.OnPoliceAlertTriggered += HandlePoliceAlertTriggered;

                UpdateWantedStarsDisplay(policeManager.WantedStars);
            }
        }

        private void UnbindEvents()
        {
            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
            }

            if (missionManager != null)
            {
                missionManager.OnContractStarted -= HandleMissionUpdated;
                missionManager.OnMissionPhaseChanged -= HandleMissionUpdated;
                missionManager.OnContractCompleted -= HandleContractCompleted;
                missionManager.OnTimerUpdated -= HandleTimerUpdated;
                missionManager.OnGameOver -= HandleGameOver;
            }

            if (policeManager != null)
            {
                policeManager.OnWantedStarsChanged -= HandleWantedStarsChanged;
                policeManager.OnPoliceAlertTriggered -= HandlePoliceAlertTriggered;
            }
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

                // Se esta entrega também foi múltiplo de 3, destaca o aumento de estrela
                bool triggeredPoliceStar = result.CompletedMissionsCount > 0 && (result.CompletedMissionsCount % 3 == 0);
                string starNotice = triggeredPoliceStar ? "  |  ★ +1 PROCURADO!" : string.Empty;

                if (rewardPopupBackground != null)
                {
                    rewardPopupBackground.color = new Color(0.08f, 0.22f, 0.10f, 0.90f);
                }

                rewardPopupText.color = new Color(1.0f, 0.90f, 0.22f, 1f);
                rewardPopupText.text = $"ENTREGUE! +${result.TotalReward:N0}{bonusStr}  |  +{addedSecInt}s{starNotice}";

                ShowPopupBanner();
            }
        }

        private void HandleWantedStarsChanged(int oldStars, int newStars)
        {
            UpdateWantedStarsDisplay(newStars);
        }

        private void HandlePoliceAlertTriggered(string alertMessage, int stars)
        {
            UpdateWantedStarsDisplay(stars);

            if (rewardPopupGroup != null && rewardPopupText != null)
            {
                bool isEscapeAlert = alertMessage != null && alertMessage.IndexOf("DESMANCHE", System.StringComparison.OrdinalIgnoreCase) >= 0;

                if (rewardPopupBackground != null)
                {
                    rewardPopupBackground.color = isEscapeAlert
                        ? new Color(0.06f, 0.24f, 0.16f, 0.92f)
                        : new Color(0.28f, 0.06f, 0.08f, 0.92f);
                }
                rewardPopupText.color = isEscapeAlert
                    ? new Color(0.32f, 0.98f, 0.62f, 1f)
                    : new Color(1.0f, 0.35f, 0.35f, 1f);
                rewardPopupText.text = alertMessage;
                ShowPopupBanner();
            }
        }

        private void UpdateWantedStarsDisplay(int activeStars)
        {
            currentDisplayedStars = Mathf.Clamp(activeStars, 0, PoliceManager.MaxWantedStars);

            if (wantedStarsText != null)
            {
                StringBuilder sb = new StringBuilder(64);
                for (int i = 1; i <= PoliceManager.MaxWantedStars; i++)
                {
                    if (i <= currentDisplayedStars)
                    {
                        sb.Append("<color=#FFD828>★</color>");
                    }
                    else
                    {
                        sb.Append("<color=#48505E>☆</color>");
                    }

                    if (i < PoliceManager.MaxWantedStars)
                    {
                        sb.Append(' ');
                    }
                }
                wantedStarsText.supportRichText = true;
                wantedStarsText.text = sb.ToString();
            }

            if (wantedStatusText != null)
            {
                if (currentDisplayedStars == 0)
                {
                    wantedStatusText.text = "FICHA LIMPA (PATRULHA)";
                    wantedStatusText.color = new Color(0.72f, 0.78f, 0.86f, 0.85f);
                }
                else
                {
                    wantedStatusText.text = $"PERSEGUIÇÃO ({currentDisplayedStars} VIATURA{(currentDisplayedStars > 1 ? "S" : "")}) — DESPISTE NO DESMANCHE!";
                    wantedStatusText.color = new Color(1.0f, 0.38f, 0.38f, 1f);
                }
            }

            if (wantedPanelBackground != null && currentDisplayedStars == 0)
            {
                wantedPanelBackground.color = new Color(0.05f, 0.06f, 0.08f, 0.78f);
            }
        }

        private void AnimateWantedSirenBorder()
        {
            if (wantedPanelBackground == null) return;

            float wave = Mathf.Sin(Time.time * 10f);
            Color policeRed = new Color(0.36f, 0.06f, 0.08f, 0.88f);
            Color policeBlue = new Color(0.06f, 0.14f, 0.38f, 0.88f);
            wantedPanelBackground.color = Color.Lerp(policeRed, policeBlue, (wave + 1f) * 0.5f);
        }

        private void HandleGameOver(GameOverResult result)
        {
            isShowingGameOver = true;

            string headerLabel;
            string overlayTitle;
            Color themeColor;

            switch (result.Reason)
            {
                case GameOverReason.Busted:
                    headerLabel = "PRESO PELA POLÍCIA! (BUSTED)";
                    overlayTitle = "PRESO! (BUSTED)";
                    themeColor = new Color(0.28f, 0.62f, 1.0f, 1f);
                    break;

                case GameOverReason.VehicleDestroyed:
                    headerLabel = "VEÍCULO EXPLODIU! (WASTED)";
                    overlayTitle = "VEÍCULO DESTRUÍDO! (WASTED)";
                    themeColor = new Color(1.0f, 0.38f, 0.12f, 1f);
                    break;

                default:
                    headerLabel = "TEMPO ESGOTADO!";
                    overlayTitle = "FIM DE TEMPO! GAME OVER";
                    themeColor = criticalTimerColor;
                    break;
            }

            if (objectiveText != null)
            {
                objectiveText.text = headerLabel;
                objectiveText.color = themeColor;
            }
            if (objectiveAccentBar != null)
            {
                objectiveAccentBar.color = themeColor;
            }

            if (gameOverTitleText != null)
            {
                gameOverTitleText.text = overlayTitle;
                gameOverTitleText.color = themeColor;
            }

            if (gameOverStatsText != null)
            {
                int survMin = Mathf.FloorToInt(result.TotalSurvivedSeconds / 60f);
                int survSec = Mathf.FloorToInt(result.TotalSurvivedSeconds % 60f);
                int finalStars = policeManager != null ? policeManager.WantedStars : currentDisplayedStars;

                string reasonDetail = !string.IsNullOrEmpty(result.CustomMessage)
                    ? $"{result.CustomMessage}\n\n"
                    : string.Empty;

                gameOverStatsText.text =
                    $"{reasonDetail}" +
                    $"DINHEIRO ACUMULADO:  ${result.TotalPlayerMoney:N0}\n" +
                    $"VEÍCULOS ENTREGUES:  {result.CompletedMissionsCount}\n" +
                    $"NÍVEL DE PROCURADO:  {finalStars} / {PoliceManager.MaxWantedStars} ESTRELAS\n" +
                    $"TEMPO DE CORRIDA:    {survMin:00}:{survSec:00}";
            }

            if (gameOverGroup != null)
            {
                gameOverGroup.interactable = true;
                gameOverGroup.blocksRaycasts = true;
                StartCoroutine(FadeCanvasGroup(gameOverGroup, 1f, 0.35f));
            }
        }

        private void ShowPopupBanner()
        {
            if (popupCoroutine != null)
            {
                StopCoroutine(popupCoroutine);
            }
            popupCoroutine = StartCoroutine(ShowRewardPopupRoutine());
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
