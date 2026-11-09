using System;
using UnityEngine;
using GTA2Clone.Vehicles;

namespace GTA2Clone.Missions
{
    /// <summary>
    /// Zona de desmanche/entrega (Drop-off Zone).
    /// Componente de trigger isolado que valida a chegada do veículo alvo conduzido pelo jogador,
    /// fornece posição segura de ejeção e dispara OnVehicleDelivered.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class DeliveryZone : MonoBehaviour
    {
        [Header("Zone Settings")]
        [SerializeField] private string zoneName = "Desmanche da Lapa";
        [SerializeField] private Transform safeEjectPoint;
        [SerializeField] private GameObject visualHighlightRoot;
        [SerializeField] private float ejectCheckRadius = 0.45f;
        [SerializeField] private LayerMask obstacleLayerMask = ~0;

        [Header("Pulse Visuals")]
        [SerializeField] private Renderer[] highlightRenderers;
        [SerializeField] private Color activeZoneColor = new Color(1.0f, 0.84f, 0.08f, 1f);
        [SerializeField] private float pulseSpeed = 4.5f;

        private BoxCollider triggerCollider;
        private VehicleController expectedVehicle;
        private bool isActiveZone;
        private MaterialPropertyBlock propBlock;
        private readonly Collider[] overlapHits = new Collider[8];

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        public string ZoneName => zoneName;
        public bool IsActiveZone => isActiveZone;
        public VehicleController ExpectedVehicle => expectedVehicle;
        public Transform SafeEjectPoint => safeEjectPoint;

        /// <summary>
        /// Disparado quando o veículo alvo da missão entra na zona de entrega conduzido pelo jogador.
        /// </summary>
        public event Action<DeliveryZone, VehicleController> OnVehicleDelivered;

        private void Awake()
        {
            triggerCollider = GetComponent<BoxCollider>();
            triggerCollider.isTrigger = true;
            propBlock = new MaterialPropertyBlock();

            if (highlightRenderers == null || highlightRenderers.Length == 0)
            {
                if (visualHighlightRoot != null)
                {
                    highlightRenderers = visualHighlightRoot.GetComponentsInChildren<Renderer>(true);
                }
            }

            SetVisualState(isActiveZone);
        }

        private void Update()
        {
            if (!isActiveZone || highlightRenderers == null || highlightRenderers.Length == 0)
                return;

            float pulse = 0.78f + 0.22f * Mathf.Sin(Time.time * pulseSpeed);
            Color pulsedColor = new Color(
                activeZoneColor.r * pulse,
                activeZoneColor.g * pulse,
                activeZoneColor.b * pulse,
                activeZoneColor.a
            );
            Color emission = activeZoneColor * (0.45f * pulse);

            for (int i = 0; i < highlightRenderers.Length; i++)
            {
                Renderer r = highlightRenderers[i];
                if (r == null) continue;

                r.GetPropertyBlock(propBlock);
                propBlock.SetColor(BaseColorId, pulsedColor);
                propBlock.SetColor(ColorId, pulsedColor);
                propBlock.SetColor(EmissionColorId, emission);
                r.SetPropertyBlock(propBlock);
            }
        }

        /// <summary>
        /// Configura os dados da zona de entrega (usado pelas ferramentas de Editor).
        /// </summary>
        public void Configure(string name, Transform ejectPoint, GameObject highlightRoot)
        {
            zoneName = name;
            safeEjectPoint = ejectPoint;
            visualHighlightRoot = highlightRoot;
            if (visualHighlightRoot != null)
            {
                highlightRenderers = visualHighlightRoot.GetComponentsInChildren<Renderer>(true);
            }
        }

        /// <summary>
        /// Ativa esta zona de desmanche aguardando especificamente o veículo alvo informado.
        /// </summary>
        public void ActivateForVehicle(VehicleController targetVehicle)
        {
            expectedVehicle = targetVehicle;
            isActiveZone = targetVehicle != null;
            SetVisualState(isActiveZone);
        }

        /// <summary>
        /// Desativa esta zona de desmanche e oculta seu destaque visual.
        /// </summary>
        public void DeactivateZone()
        {
            expectedVehicle = null;
            isActiveZone = false;
            SetVisualState(false);
        }

        /// <summary>
        /// Calcula uma posição e rotação seguras para ejetar o jogador ao entregar o veículo.
        /// </summary>
        public void GetSafeEjectPose(VehicleController deliveredVehicle, out Vector3 ejectPosition, out Quaternion ejectRotation)
        {
            if (safeEjectPoint != null && !IsPointBlocked(safeEjectPoint.position, deliveredVehicle))
            {
                ejectPosition = new Vector3(safeEjectPoint.position.x, 0.08f, safeEjectPoint.position.z);
                ejectRotation = safeEjectPoint.rotation;
                return;
            }

            // Fallback: tenta laterais da zona de entrega
            Vector3[] fallbackOffsets =
            {
                transform.right * 3.2f,
                -transform.right * 3.2f,
                -transform.forward * 4.0f,
                transform.forward * 4.0f
            };

            for (int i = 0; i < fallbackOffsets.Length; i++)
            {
                Vector3 candidate = transform.position + fallbackOffsets[i];
                candidate.y = 0.08f;
                if (!IsPointBlocked(candidate, deliveredVehicle))
                {
                    ejectPosition = candidate;
                    Vector3 lookDir = (transform.position - candidate);
                    lookDir.y = 0f;
                    ejectRotation = lookDir.sqrMagnitude > 0.01f
                        ? Quaternion.LookRotation(lookDir.normalized, Vector3.up)
                        : transform.rotation;
                    return;
                }
            }

            ejectPosition = new Vector3(transform.position.x, 0.08f, transform.position.z);
            ejectRotation = transform.rotation;
        }

        private void OnTriggerEnter(Collider other)
        {
            TryValidateDelivery(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryValidateDelivery(other);
        }

        private void TryValidateDelivery(Collider other)
        {
            if (!isActiveZone || expectedVehicle == null || other == null)
                return;

            VehicleController vehicle = other.GetComponentInParent<VehicleController>();
            if (vehicle == null || vehicle != expectedVehicle)
                return;

            // Garante que o jogador está dirigindo o veículo no momento da entrega
            if (!vehicle.IsOccupied)
                return;

            VehicleController delivered = expectedVehicle;
            DeactivateZone();
            OnVehicleDelivered?.Invoke(this, delivered);
        }

        private bool IsPointBlocked(Vector3 point, VehicleController ignoredVehicle)
        {
            Vector3 checkPos = point + Vector3.up * 0.7f;
            int count = Physics.OverlapSphereNonAlloc(
                checkPos,
                ejectCheckRadius,
                overlapHits,
                obstacleLayerMask,
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < count; i++)
            {
                Collider col = overlapHits[i];
                if (col == null) continue;
                if (col.transform.IsChildOf(transform)) continue;
                if (ignoredVehicle != null && col.transform.IsChildOf(ignoredVehicle.transform)) continue;
                if (col.gameObject.name == "Ground" || col.gameObject.name.StartsWith("Road_") || col.gameObject.name.StartsWith("SidewalkLot_"))
                    continue;

                return true;
            }

            return false;
        }

        private void SetVisualState(bool active)
        {
            if (visualHighlightRoot != null)
            {
                visualHighlightRoot.SetActive(active);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = isActiveZone ? new Color(1f, 0.85f, 0.1f, 0.65f) : new Color(1f, 0.6f, 0.1f, 0.25f);
            BoxCollider col = GetComponent<BoxCollider>();
            Vector3 size = col != null ? col.size : new Vector3(4.5f, 2.5f, 7.5f);
            Vector3 center = col != null ? col.center : new Vector3(0f, 1.25f, 0f);

            Matrix4x4 prevMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(center, size);
            Gizmos.matrix = prevMatrix;

            if (safeEjectPoint != null)
            {
                Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
                Gizmos.DrawWireSphere(safeEjectPoint.position + Vector3.up * 0.5f, 0.45f);
            }
        }
    }
}

