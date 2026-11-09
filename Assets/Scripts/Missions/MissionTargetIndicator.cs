using UnityEngine;
using UnityEngine.Rendering;
using GTA2Clone.Player;

namespace GTA2Clone.Missions
{
    /// <summary>
    /// Seta direcional 3D que orbita ao redor do jogador (ou do veículo atual)
    /// e aponta para o objetivo da missão:
    /// - Cor Verde: aponta para o veículo alvo que deve ser roubado.
    /// - Cor Amarela: aponta para a DeliveryZone onde o veículo deve ser entregue.
    /// </summary>
    public class MissionTargetIndicator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerController player;
        [SerializeField] private Transform arrowVisualRoot;
        [SerializeField] private MeshRenderer arrowRenderer;

        [Header("Orbit & Animation Settings")]
        [SerializeField] private float orbitRadius = 2.75f;
        [SerializeField] private float heightOffset = 1.35f;
        [SerializeField] private float bobAmplitude = 0.22f;
        [SerializeField] private float bobFrequency = 6.5f;
        [SerializeField] private float minHideDistance = 2.1f;

        [Header("Phase Colors (GTA 2 Arcade)")]
        [SerializeField] private Color stealVehicleColor = new Color(0.12f, 0.95f, 0.32f, 1f);
        [SerializeField] private Color deliverVehicleColor = new Color(1.0f, 0.85f, 0.10f, 1f);

        private Transform currentTarget;
        private Color currentColor;
        private MaterialPropertyBlock propBlock;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        public Transform CurrentTarget => currentTarget;

        private void Awake()
        {
            propBlock = new MaterialPropertyBlock();
            EnsureArrowVisualExists();
            ApplyColor(stealVehicleColor);
            UpdateVisibility(false);
        }

        private void LateUpdate()
        {
            if (currentTarget == null || player == null)
            {
                UpdateVisibility(false);
                return;
            }

            Vector3 originPos = GetOrbitCenterPosition();
            Vector3 toTarget = currentTarget.position - originPos;
            toTarget.y = 0f;

            float planarDistSq = toTarget.sqrMagnitude;
            if (planarDistSq < minHideDistance * minHideDistance)
            {
                UpdateVisibility(false);
                return;
            }

            UpdateVisibility(true);

            Vector3 dir = toTarget.normalized;
            float bobOffset = Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
            float effectiveRadius = orbitRadius + bobOffset;

            Vector3 orbitWorldPos = new Vector3(
                originPos.x + dir.x * effectiveRadius,
                heightOffset,
                originPos.z + dir.z * effectiveRadius
            );

            transform.SetPositionAndRotation(
                orbitWorldPos,
                Quaternion.LookRotation(dir, Vector3.up)
            );
        }

        /// <summary>
        /// Configura a referência do jogador que a seta deve orbitar.
        /// </summary>
        public void SetPlayer(PlayerController targetPlayer)
        {
            player = targetPlayer;
        }

        /// <summary>
        /// Define o alvo atual da seta e a fase da missão (Verde para roubar carro, Amarela para entregar na DeliveryZone).
        /// </summary>
        public void SetTarget(Transform target, MissionPhase phase)
        {
            currentTarget = target;
            EnsureArrowVisualExists();

            if (target == null || phase == MissionPhase.None)
            {
                UpdateVisibility(false);
                return;
            }

            Color phaseColor = phase == MissionPhase.DeliverVehicle
                ? deliverVehicleColor
                : stealVehicleColor;

            ApplyColor(phaseColor);
            UpdateVisibility(true);
        }

        /// <summary>
        /// Oculta imediatamente o indicador direcional.
        /// </summary>
        public void ClearTarget()
        {
            currentTarget = null;
            UpdateVisibility(false);
        }

        /// <summary>
        /// Garante que a malha 3D da seta direcional exista para visualização clara na câmera zenital.
        /// </summary>
        public void EnsureArrowVisualExists()
        {
            if (arrowVisualRoot == null)
            {
                Transform existing = transform.Find("ArrowMesh");
                if (existing != null)
                {
                    arrowVisualRoot = existing;
                }
                else
                {
                    GameObject visualObj = new GameObject("ArrowMesh");
                    visualObj.transform.SetParent(transform, false);
                    visualObj.transform.localPosition = Vector3.zero;
                    visualObj.transform.localRotation = Quaternion.identity;
                    arrowVisualRoot = visualObj.transform;
                }
            }

            MeshFilter mf = arrowVisualRoot.GetComponent<MeshFilter>();
            if (mf == null)
            {
                mf = arrowVisualRoot.gameObject.AddComponent<MeshFilter>();
            }
            if (mf.sharedMesh == null)
            {
                mf.sharedMesh = BuildProceduralArrowMesh();
            }

            if (arrowRenderer == null)
            {
                arrowRenderer = arrowVisualRoot.GetComponent<MeshRenderer>();
                if (arrowRenderer == null)
                {
                    arrowRenderer = arrowVisualRoot.gameObject.AddComponent<MeshRenderer>();
                }
            }

            arrowRenderer.shadowCastingMode = ShadowCastingMode.Off;
            arrowRenderer.receiveShadows = false;

            if (arrowRenderer.sharedMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                Material mat = new Material(shader)
                {
                    name = "MissionArrowRuntimeMat"
                };
                mat.EnableKeyword("_EMISSION");
                arrowRenderer.sharedMaterial = mat;
            }
        }

        private void ApplyColor(Color color)
        {
            currentColor = color;
            if (arrowRenderer == null) return;

            if (propBlock == null)
            {
                propBlock = new MaterialPropertyBlock();
            }

            Color emission = color * 0.85f;
            arrowRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor(BaseColorId, color);
            propBlock.SetColor(ColorId, color);
            propBlock.SetColor(EmissionColorId, emission);
            arrowRenderer.SetPropertyBlock(propBlock);
        }

        private void UpdateVisibility(bool visible)
        {
            if (arrowVisualRoot != null && arrowVisualRoot.gameObject.activeSelf != visible)
            {
                arrowVisualRoot.gameObject.SetActive(visible);
            }
        }

        private Vector3 GetOrbitCenterPosition()
        {
            if (player != null)
            {
                if (player.CurrentVehicle != null)
                {
                    return player.CurrentVehicle.transform.position;
                }
                return player.transform.position;
            }
            return transform.position;
        }

        /// <summary>
        /// Constrói uma malha 3D de seta estilizada visível de cima (câmera zenital 90°).
        /// Aponta para +Z local.
        /// </summary>
        public static Mesh BuildProceduralArrowMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "GTA2_DirectionalArrowMesh"
            };

            const float halfThickness = 0.08f;

            // Contorno 2D da seta no plano XZ (apontando para +Z):
            // 0: Ponta frontal (0, +0.75)
            // 1: Asa direita (+0.52, +0.05)
            // 2: Encaixe direito da haste (+0.22, +0.05)
            // 3: Base traseira direita (+0.22, -0.60)
            // 4: Base traseira esquerda (-0.22, -0.60)
            // 5: Encaixe esquerdo da haste (-0.22, +0.05)
            // 6: Asa esquerda (-0.52, +0.05)
            Vector2[] profile =
            {
                new Vector2(0.00f, 0.78f),
                new Vector2(0.54f, 0.05f),
                new Vector2(0.22f, 0.05f),
                new Vector2(0.22f, -0.58f),
                new Vector2(-0.22f, -0.58f),
                new Vector2(-0.22f, 0.05f),
                new Vector2(-0.54f, 0.05f)
            };

            Vector3[] vertices = new Vector3[14];
            for (int i = 0; i < 7; i++)
            {
                vertices[i] = new Vector3(profile[i].x, halfThickness, profile[i].y);
                vertices[i + 7] = new Vector3(profile[i].x, -halfThickness, profile[i].y);
            }

            int[] triangles =
            {
                // Face superior (+Y)
                0, 1, 2,
                0, 2, 5,
                0, 5, 6,
                2, 3, 4,
                2, 4, 5,

                // Face inferior (-Y)
                7, 9, 8,
                7, 12, 9,
                7, 13, 12,
                9, 11, 10,
                9, 12, 11,

                // Laterais
                0, 7, 1,  1, 7, 8,
                1, 8, 2,  2, 8, 9,
                2, 9, 3,  3, 9, 10,
                3, 10, 4, 4, 10, 11,
                4, 11, 5, 5, 11, 12,
                5, 12, 6, 6, 12, 13,
                6, 13, 0, 0, 13, 7
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

