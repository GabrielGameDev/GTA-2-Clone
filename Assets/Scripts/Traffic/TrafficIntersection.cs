using System.Collections.Generic;
using UnityEngine;

namespace GTA2Clone.Traffic
{
    /// <summary>
    /// Controla os semáforos (fases Verde, Amarelo e Vermelho), iluminação visual e reserva de travessia de um cruzamento viário.
    /// Impede colisões laterais e bloqueios (deadlocks) entre veículos que se cruzam.
    /// </summary>
    public class TrafficIntersection : MonoBehaviour
    {
        public enum SignalState
        {
            Red,
            Yellow,
            Green
        }

        private enum Phase
        {
            HorizontalGreen,
            HorizontalYellow,
            VerticalGreen,
            VerticalYellow
        }

        [Header("Timing Settings")]
        [SerializeField] private float greenDuration = 6.5f;
        [SerializeField] private float yellowClearanceDuration = 2.2f;
        [SerializeField] private float maxReservationSeconds = 3.5f;
        [SerializeField] private float initialTimeOffset;

        [Header("Visual Signal Lights & Lenses")]
        [SerializeField] private List<Light> horizontalLights = new List<Light>();
        [SerializeField] private List<Light> verticalLights = new List<Light>();
        [SerializeField] private List<MeshRenderer> horizontalBulbRenderers = new List<MeshRenderer>();
        [SerializeField] private List<MeshRenderer> verticalBulbRenderers = new List<MeshRenderer>();

        private Phase currentPhase = Phase.HorizontalGreen;
        private float phaseTimer;

        private readonly List<TrafficVehicleAI> vehiclesInIntersection = new List<TrafficVehicleAI>(4);
        private readonly Dictionary<TrafficVehicleAI, float> entryTimestamps = new Dictionary<TrafficVehicleAI, float>(4);
        private readonly Dictionary<TrafficVehicleAI, Vector3> entryDirections = new Dictionary<TrafficVehicleAI, Vector3>(4);
        private MaterialPropertyBlock propBlock;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private static readonly Color GreenLightColor = new Color(0.12f, 1.0f, 0.28f, 1f);
        private static readonly Color YellowLightColor = new Color(1.0f, 0.78f, 0.08f, 1f);
        private static readonly Color RedLightColor = new Color(1.0f, 0.14f, 0.14f, 1f);

        private void Awake()
        {
            propBlock = new MaterialPropertyBlock();
            phaseTimer = initialTimeOffset % (greenDuration + yellowClearanceDuration);
            if ((int)(initialTimeOffset / (greenDuration + yellowClearanceDuration)) % 2 == 1)
            {
                currentPhase = Phase.VerticalGreen;
            }
            UpdateVisualSignals();
        }

        private void Update()
        {
            CleanupInvalidVehicles();

            phaseTimer += Time.deltaTime;
            float currentLimit = (currentPhase == Phase.HorizontalGreen || currentPhase == Phase.VerticalGreen)
                ? greenDuration
                : yellowClearanceDuration;

            if (phaseTimer >= currentLimit)
            {
                phaseTimer = 0f;
                AdvancePhase();
                UpdateVisualSignals();
            }
        }

        /// <summary>
        /// Configura o deslocamento de tempo inicial e os componentes de luz/lente dos semáforos (usado em Editor).
        /// </summary>
        public void Configure(
            float timeOffset,
            List<Light> hLights,
            List<Light> vLights,
            List<MeshRenderer> horizontalBulbs,
            List<MeshRenderer> verticalBulbs)
        {
            initialTimeOffset = timeOffset;
            horizontalLights = hLights ?? new List<Light>();
            verticalLights = vLights ?? new List<Light>();
            horizontalBulbRenderers = horizontalBulbs ?? new List<MeshRenderer>();
            verticalBulbRenderers = verticalBulbs ?? new List<MeshRenderer>();
        }

        /// <summary>
        /// Retorna o estado atual do semáforo para uma direção de aproximação (eixo X = Horizontal, eixo Z = Vertical).
        /// </summary>
        public SignalState GetSignalForDirection(Vector3 approachDirection)
        {
            bool isHorizontal = Mathf.Abs(approachDirection.x) >= Mathf.Abs(approachDirection.z);

            if (isHorizontal)
            {
                return currentPhase switch
                {
                    Phase.HorizontalGreen => SignalState.Green,
                    Phase.HorizontalYellow => SignalState.Yellow,
                    _ => SignalState.Red
                };
            }
            else
            {
                return currentPhase switch
                {
                    Phase.VerticalGreen => SignalState.Green,
                    Phase.VerticalYellow => SignalState.Yellow,
                    _ => SignalState.Red
                };
            }
        }

        /// <summary>
        /// Verifica se um veículo pode cruzar a linha de retenção e entrar no cruzamento agora.
        /// </summary>
        public bool CanVehicleEnter(TrafficVehicleAI vehicle, Vector3 approachDirection)
        {
            if (vehicle == null) return false;

            // Se o veículo já está registrado atravessando este cruzamento, tem permissão para concluir
            if (vehiclesInIntersection.Contains(vehicle))
                return true;

            // Só permite novas entradas no sinal Verde
            if (GetSignalForDirection(approachDirection) != SignalState.Green)
                return false;

            CleanupInvalidVehicles();

            Vector3 normApproach = approachDirection.sqrMagnitude > 0.001f ? approachDirection.normalized : vehicle.transform.forward;

            // Evita que carros em direções conflitantes (opostas ou transversais) entrem ao mesmo tempo e colidam ao convergir
            for (int i = 0; i < vehiclesInIntersection.Count; i++)
            {
                TrafficVehicleAI other = vehiclesInIntersection[i];
                if (other == null || other == vehicle) continue;

                Vector3 otherApproach = entryDirections.TryGetValue(other, out Vector3 savedDir)
                    ? savedDir
                    : other.transform.forward;

                // Se entrou exatamente pela mesma faixa/direção, exige espaçamento mínimo antes de liberar o próximo
                if (Vector3.Dot(otherApproach, normApproach) > 0.85f)
                {
                    Vector3 vehicleDiff = other.transform.position - vehicle.transform.position;
                    vehicleDiff.y = 0f;
                    if (vehicleDiff.sqrMagnitude < 16.0f) // < 4.0m de distância para o carro da frente
                    {
                        return false;
                    }
                    continue;
                }

                // Direções opostas ou transversais: aguarda o veículo anterior sair do miolo do cruzamento (~5.8m do centro)
                Vector3 diff = other.transform.position - transform.position;
                diff.y = 0f;
                if (diff.sqrMagnitude < 33.64f)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Registra que um veículo cruzou a linha de retenção e está atravessando o cruzamento.
        /// </summary>
        public void NotifyVehicleEntered(TrafficVehicleAI vehicle, Vector3 approachDirection = default)
        {
            if (vehicle == null) return;
            if (!vehiclesInIntersection.Contains(vehicle))
            {
                vehiclesInIntersection.Add(vehicle);
            }
            entryTimestamps[vehicle] = Time.time;
            entryDirections[vehicle] = approachDirection.sqrMagnitude > 0.001f
                ? approachDirection.normalized
                : vehicle.transform.forward;
        }

        /// <summary>
        /// Remove o veículo da lista de ocupação ao sair do cruzamento.
        /// </summary>
        public void NotifyVehicleExited(TrafficVehicleAI vehicle)
        {
            if (vehicle == null) return;
            vehiclesInIntersection.Remove(vehicle);
            entryTimestamps.Remove(vehicle);
            entryDirections.Remove(vehicle);
        }

        private void AdvancePhase()
        {
            currentPhase = currentPhase switch
            {
                Phase.HorizontalGreen => Phase.HorizontalYellow,
                Phase.HorizontalYellow => Phase.VerticalGreen,
                Phase.VerticalGreen => Phase.VerticalYellow,
                _ => Phase.HorizontalGreen
            };
        }

        private void CleanupInvalidVehicles()
        {
            float now = Time.time;
            for (int i = vehiclesInIntersection.Count - 1; i >= 0; i--)
            {
                TrafficVehicleAI v = vehiclesInIntersection[i];
                if (v == null || !v.isActiveAndEnabled || (v.Vehicle != null && v.Vehicle.IsOccupied))
                {
                    if (v != null)
                    {
                        entryTimestamps.Remove(v);
                        entryDirections.Remove(v);
                    }
                    vehiclesInIntersection.RemoveAt(i);
                    continue;
                }

                // Expiração por tempo máximo de reserva (evita que um carro lento bloqueie o cruzamento indefinidamente)
                if (entryTimestamps.TryGetValue(v, out float enterTime))
                {
                    float elapsed = now - enterTime;
                    bool isStalled = elapsed > 1.6f && v.Vehicle != null && v.Vehicle.CurrentSpeed < 0.35f;
                    if (elapsed > maxReservationSeconds || isStalled)
                    {
                        entryTimestamps.Remove(v);
                        entryDirections.Remove(v);
                        vehiclesInIntersection.RemoveAt(i);
                        continue;
                    }
                }

                // Se já se afastou mais de 6.0m do centro do cruzamento, libera automaticamente
                Vector3 diff = v.transform.position - transform.position;
                diff.y = 0f;
                if (diff.sqrMagnitude > 36.0f)
                {
                    entryTimestamps.Remove(v);
                    entryDirections.Remove(v);
                    vehiclesInIntersection.RemoveAt(i);
                }
            }
        }

        private void UpdateVisualSignals()
        {
            if (propBlock == null)
            {
                propBlock = new MaterialPropertyBlock();
            }

            Color hColor = GetColorForState(GetSignalForDirection(Vector3.right));
            Color vColor = GetColorForState(GetSignalForDirection(Vector3.forward));

            ApplyLightColor(horizontalLights, hColor);
            ApplyLightColor(verticalLights, vColor);

            ApplyBulbColor(horizontalBulbRenderers, hColor);
            ApplyBulbColor(verticalBulbRenderers, vColor);
        }

        private Color GetColorForState(SignalState state)
        {
            return state switch
            {
                SignalState.Green => GreenLightColor,
                SignalState.Yellow => YellowLightColor,
                _ => RedLightColor
            };
        }

        private void ApplyLightColor(List<Light> lights, Color color)
        {
            if (lights == null || lights.Count == 0) return;

            for (int i = 0; i < lights.Count; i++)
            {
                Light l = lights[i];
                if (l != null)
                {
                    l.color = color;
                }
            }
        }

        private void ApplyBulbColor(List<MeshRenderer> renderers, Color color)
        {
            if (renderers == null || renderers.Count == 0) return;

            propBlock.SetColor(BaseColorId, color);
            propBlock.SetColor(EmissionColorId, color * 2.2f);

            for (int i = 0; i < renderers.Count; i++)
            {
                MeshRenderer mr = renderers[i];
                if (mr != null)
                {
                    mr.SetPropertyBlock(propBlock);
                }
            }
        }
    }
}
