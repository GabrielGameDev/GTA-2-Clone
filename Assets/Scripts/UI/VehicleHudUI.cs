using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using GTA2Clone.Player;
using GTA2Clone.Vehicles;

namespace GTA2Clone.UI
{
    /// <summary>
    /// Exibe o nome do veículo e a barra de integridade (vida/dano) no canto inferior direito da tela
    /// enquanto o jogador está dirigindo um veículo.
    /// Escuta OnVehicleEntered / OnVehicleExited do PlayerController e OnHealthChanged do VehicleHealth.
    /// </summary>
    public class VehicleHudUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerController player;
        [SerializeField] private Text vehicleNameText;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform healthFillRect;
        [SerializeField] private Image healthFillImage;
        [SerializeField] private Text healthStatusText;

        [Header("Animation & Timing")]
        [SerializeField] private bool autoHideAfterDelay = false;
        [SerializeField] private float displayDuration = 3.5f;
        [SerializeField] private float fadeInDuration = 0.18f;
        [SerializeField] private float fadeOutDuration = 0.35f;

        [Header("Health Bar Colors")]
        [SerializeField] private Color healthyColor = new Color(0.20f, 0.94f, 0.42f, 1f);
        [SerializeField] private Color damagedColor = new Color(1.00f, 0.78f, 0.16f, 1f);
        [SerializeField] private Color criticalColor = new Color(1.00f, 0.20f, 0.20f, 1f);

        private VehicleController subscribedVehicle;
        private VehicleHealth subscribedHealth;
        private Coroutine displayCoroutine;
        private bool isCriticalHealth;

        private void Awake()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            if (vehicleNameText != null)
            {
                vehicleNameText.text = string.Empty;
            }
        }

        private void OnEnable()
        {
            if (player != null)
            {
                player.OnVehicleEntered -= HandleVehicleEntered;
                player.OnVehicleEntered += HandleVehicleEntered;
                player.OnVehicleExited -= HandleVehicleExited;
                player.OnVehicleExited += HandleVehicleExited;

                if (player.CurrentVehicle != null)
                {
                    HandleVehicleEntered(player.CurrentVehicle);
                }
            }
        }

        private void OnDisable()
        {
            if (player != null)
            {
                player.OnVehicleEntered -= HandleVehicleEntered;
                player.OnVehicleExited -= HandleVehicleExited;
            }
            UnsubscribeFromVehicleHealth();
        }

        private void Update()
        {
            if (isCriticalHealth && healthFillImage != null && subscribedVehicle != null)
            {
                float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 10f));
                healthFillImage.color = Color.Lerp(criticalColor, Color.white, pulse * 0.35f);
            }
        }

        /// <summary>
        /// Configura todas as referências do HUD de veículo (usado pelo SceneSetupUtility).
        /// </summary>
        public void Configure(
            PlayerController targetPlayer,
            Text nameText,
            CanvasGroup group,
            RectTransform fillRect,
            Image fillImg,
            Text statusText)
        {
            if (player != null)
            {
                player.OnVehicleEntered -= HandleVehicleEntered;
                player.OnVehicleExited -= HandleVehicleExited;
            }

            player = targetPlayer;
            vehicleNameText = nameText;
            canvasGroup = group;
            healthFillRect = fillRect;
            healthFillImage = fillImg;
            healthStatusText = statusText;
            autoHideAfterDelay = false;

            if (isActiveAndEnabled && player != null)
            {
                player.OnVehicleEntered += HandleVehicleEntered;
                player.OnVehicleExited += HandleVehicleExited;
            }
        }

        /// <summary>
        /// Define a referência do jogador e atualiza a inscrição nos eventos.
        /// </summary>
        public void SetPlayer(PlayerController targetPlayer)
        {
            if (player != null)
            {
                player.OnVehicleEntered -= HandleVehicleEntered;
                player.OnVehicleExited -= HandleVehicleExited;
            }

            player = targetPlayer;

            if (isActiveAndEnabled && player != null)
            {
                player.OnVehicleEntered += HandleVehicleEntered;
                player.OnVehicleExited += HandleVehicleExited;
            }
        }

        private void HandleVehicleEntered(VehicleController vehicle)
        {
            if (vehicle == null) return;

            if (vehicleNameText != null)
            {
                vehicleNameText.text = vehicle.VehicleName.ToUpperInvariant();
            }

            SubscribeToVehicleHealth(vehicle);

            if (displayCoroutine != null)
            {
                StopCoroutine(displayCoroutine);
            }

            displayCoroutine = StartCoroutine(ShowVehiclePanelRoutine());
        }

        private void HandleVehicleExited(VehicleController vehicle)
        {
            UnsubscribeFromVehicleHealth();

            if (displayCoroutine != null)
            {
                StopCoroutine(displayCoroutine);
            }

            displayCoroutine = StartCoroutine(FadeCanvasGroup(0f, fadeOutDuration));
        }

        private void SubscribeToVehicleHealth(VehicleController vehicle)
        {
            UnsubscribeFromVehicleHealth();
            subscribedVehicle = vehicle;
            subscribedHealth = vehicle != null ? vehicle.Health : null;

            if (subscribedHealth != null)
            {
                subscribedHealth.OnHealthChanged += HandleVehicleHealthChanged;
                HandleVehicleHealthChanged(subscribedHealth.CurrentHealth, subscribedHealth.MaxHealth);
            }
            else
            {
                HandleVehicleHealthChanged(100f, 100f);
            }
        }

        private void UnsubscribeFromVehicleHealth()
        {
            if (subscribedHealth != null)
            {
                subscribedHealth.OnHealthChanged -= HandleVehicleHealthChanged;
                subscribedHealth = null;
            }
            subscribedVehicle = null;
            isCriticalHealth = false;
        }

        private void HandleVehicleHealthChanged(float current, float max)
        {
            float normalized = max > 0.01f ? Mathf.Clamp01(current / max) : 0f;
            int pct = Mathf.CeilToInt(normalized * 100f);

            if (healthFillRect != null)
            {
                healthFillRect.anchorMax = new Vector2(normalized, 1f);
            }

            isCriticalHealth = normalized <= 0.28f;

            if (healthFillImage != null)
            {
                if (normalized > 0.55f)
                {
                    healthFillImage.color = healthyColor;
                }
                else if (normalized > 0.28f)
                {
                    healthFillImage.color = damagedColor;
                }
                else
                {
                    healthFillImage.color = criticalColor;
                }
            }

            if (healthStatusText != null)
            {
                if (normalized <= 0f)
                {
                    healthStatusText.text = "DESTRUÍDO (0%)";
                    healthStatusText.color = criticalColor;
                }
                else if (isCriticalHealth)
                {
                    healthStatusText.text = $"PERIGO! MOTOR EM CHAMAS ({pct}%)";
                    healthStatusText.color = criticalColor;
                }
                else
                {
                    healthStatusText.text = $"LATARIA / MOTOR: {pct}%";
                    healthStatusText.color = normalized > 0.55f
                        ? new Color(0.86f, 0.94f, 0.88f, 0.95f)
                        : damagedColor;
                }
            }
        }

        private IEnumerator ShowVehiclePanelRoutine()
        {
            yield return FadeCanvasGroup(1f, fadeInDuration);

            // Mantém o painel visível continuamente enquanto houver barra de vida ou autoHideAfterDelay == false
            if (autoHideAfterDelay && healthFillRect == null)
            {
                yield return new WaitForSeconds(displayDuration);
                yield return FadeCanvasGroup(0f, fadeOutDuration);
            }

            displayCoroutine = null;
        }

        private IEnumerator FadeCanvasGroup(float targetAlpha, float duration)
        {
            if (canvasGroup == null) yield break;

            float startAlpha = canvasGroup.alpha;
            if (duration <= 0.001f)
            {
                canvasGroup.alpha = targetAlpha;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
        }
    }
}
