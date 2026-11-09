using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using GTA2Clone.Player;
using GTA2Clone.Vehicles;

namespace GTA2Clone.UI
{
    /// <summary>
    /// Exibe o nome do veículo no canto inferior direito da tela quando o jogador entra em um carro.
    /// Escuta os eventos OnVehicleEntered e OnVehicleExited do PlayerController de forma desacoplada.
    /// </summary>
    public class VehicleHudUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerController player;
        [SerializeField] private Text vehicleNameText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Animation & Timing")]
        [SerializeField] private bool autoHideAfterDelay = true;
        [SerializeField] private float displayDuration = 3.5f;
        [SerializeField] private float fadeInDuration = 0.2f;
        [SerializeField] private float fadeOutDuration = 0.5f;

        private Coroutine displayCoroutine;

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
                player.OnVehicleEntered += HandleVehicleEntered;
                player.OnVehicleExited += HandleVehicleExited;
            }
        }

        private void OnDisable()
        {
            if (player != null)
            {
                player.OnVehicleEntered -= HandleVehicleEntered;
                player.OnVehicleExited -= HandleVehicleExited;
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
            if (vehicle == null || vehicleNameText == null) return;

            vehicleNameText.text = vehicle.VehicleName.ToUpperInvariant();

            if (displayCoroutine != null)
            {
                StopCoroutine(displayCoroutine);
            }

            displayCoroutine = StartCoroutine(ShowVehicleNameRoutine());
        }

        private void HandleVehicleExited(VehicleController vehicle)
        {
            if (displayCoroutine != null)
            {
                StopCoroutine(displayCoroutine);
            }

            displayCoroutine = StartCoroutine(FadeCanvasGroup(0f, fadeOutDuration));
        }

        private IEnumerator ShowVehicleNameRoutine()
        {
            yield return FadeCanvasGroup(1f, fadeInDuration);

            if (autoHideAfterDelay)
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

