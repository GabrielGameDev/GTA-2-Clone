using System;
using System.Collections.Generic;
using UnityEngine;
using GTA2Clone.Player;
using GTA2Clone.Traffic;
using GTA2Clone.Vehicles;

namespace GTA2Clone.Police
{
    public enum PoliceAIState
    {
        Patrol = 0,
        Pursuit = 1
    }

    /// <summary>
    /// Controlador de IA para viaturas policiais.
    /// - Em modo Patrol (0 estrelas): patrulha as vias usando o TrafficVehicleAI com giroflex desligado.
    /// - Em modo Pursuit (1 a 6 estrelas): ativa o giroflex luminoso (vermelho/azul), ignora semáforos,
    ///   escala sua velocidade proporcionalmente ao carro do jogador (mais lenta em 1★-2★, parelha em 3★-4★,
    ///   rápida e agressiva em 5★-6★) e navega pelo grafo viário ou por interceptação visual.
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    public class PoliceVehicleAI : MonoBehaviour
    {
        [Header("State & References")]
        [SerializeField] private PoliceAIState currentState = PoliceAIState.Patrol;
        [SerializeField] private PlayerController targetPlayer;
        [SerializeField] private TrafficRoadGraph roadGraph;

        [Header("Pursuit Navigation")]
        [SerializeField] private float directInterceptMaxDistance = 26f;
        [SerializeField] private float waypointReachRadius = 2.4f;
        [SerializeField] private float maxSteerAngleRef = 24f;
        [SerializeField] private float leadPursuitTime = 0.35f;
        [SerializeField] private LayerMask obstacleLayerMask = ~0;

        [Header("Unstuck Recovery")]
        [SerializeField] private float stuckTimeThreshold = 1.15f;
        [SerializeField] private float reverseDuration = 0.85f;
        [SerializeField] private float passThroughDuration = 1.6f;

        [Header("Siren Visuals")]
        [SerializeField] private float sirenFlashFrequency = 7.5f;

        // Escala de velocidade da viatura relativa à velocidade máxima do veículo do jogador por estrela (1★ a 6★)
        private static readonly float[] RelativeSpeedRatioByStar =
        {
            0.70f, // 0★ (Patrol)
            0.76f, // 1★ (Mais lenta que o jogador: ~76% da velocidade dele)
            0.84f, // 2★ (Moderada: ~84% da velocidade do jogador)
            0.92f, // 3★ (Parelha: ~92% da velocidade do jogador)
            0.98f, // 4★ (Forte: ~98% da velocidade do jogador)
            1.04f, // 5★ (Rápida: 104% da velocidade do jogador)
            1.10f  // 6★ (Máxima: 110% da velocidade do jogador)
        };

        private VehicleController vehicleController;
        private TrafficVehicleAI trafficAI;
        private Rigidbody rb;
        private BoxCollider ownCollider;

        private TrafficWaypoint currentWaypoint;
        private TrafficWaypoint previousWaypoint;
        private int currentWantedStars = 1;

        private Vector3 stuckAnchorPos;
        private float stuckTimer;
        private float reverseTimer;
        private float reverseSteerDir = -1f;
        private float passThroughTimer;
        private float waypointReanchorTimer;

        private GameObject sirenRoot;
        private MeshRenderer redLensRenderer;
        private MeshRenderer blueLensRenderer;
        private Light sirenPointLight;
        private float sirenPhaseOffset;

        private static Material sirenRedOnMat;
        private static Material sirenBlueOnMat;
        private static Material sirenOffMat;

        private readonly List<Collider> ignoredColliders = new List<Collider>(8);
        private readonly RaycastHit[] rayHits = new RaycastHit[12];
        private readonly Collider[] overlapHits = new Collider[12];

        public VehicleController Vehicle => vehicleController;
        public PoliceAIState CurrentState => currentState;
        public bool IsPursuing => currentState == PoliceAIState.Pursuit && enabled && vehicleController != null && !vehicleController.IsOccupied && !vehicleController.IsDestroyed;

        /// <summary>
        /// Disparado quando o jogador rouba esta viatura policial.
        /// </summary>
        public event Action<PoliceVehicleAI> OnStolenByPlayer;

        /// <summary>
        /// Disparado quando esta viatura explode.
        /// </summary>
        public event Action<PoliceVehicleAI> OnDestroyed;

        private void Awake()
        {
            CacheComponents();
            sirenPhaseOffset = UnityEngine.Random.Range(0f, 10f);
            EnsureSirenVisuals();
            UpdateSirenState(false);
        }

        private void OnEnable()
        {
            CacheComponents();
            stuckAnchorPos = transform.position;

            if (vehicleController != null)
            {
                vehicleController.OnDriverEntered -= HandleDriverEntered;
                vehicleController.OnDriverEntered += HandleDriverEntered;

                if (vehicleController.Health != null)
                {
                    vehicleController.Health.OnVehicleExploded -= HandleVehicleExploded;
                    vehicleController.Health.OnVehicleExploded += HandleVehicleExploded;
                }
            }
        }

        private void OnDisable()
        {
            RestoreIgnoredCollisions();
            UpdateSirenState(false);

            if (vehicleController != null)
            {
                vehicleController.OnDriverEntered -= HandleDriverEntered;
                vehicleController.SetAISpeedMultiplier(1f);

                if (vehicleController.Health != null)
                {
                    vehicleController.Health.OnVehicleExploded -= HandleVehicleExploded;
                }
            }
        }

        /// <summary>
        /// Inicializa a viatura policial com referências ao jogador, grafo de tráfego e waypoint inicial.
        /// </summary>
        public void Initialize(
            PlayerController player,
            TrafficRoadGraph graph,
            TrafficWaypoint startWaypoint,
            PoliceAIState initialState,
            int wantedStars)
        {
            CacheComponents();
            targetPlayer = player;
            roadGraph = graph;
            currentWaypoint = startWaypoint;
            previousWaypoint = startWaypoint;
            stuckAnchorPos = transform.position;
            stuckTimer = 0f;
            reverseTimer = 0f;

            EnsureSirenVisuals();
            SetState(initialState, wantedStars);
        }

        /// <summary>
        /// Altera o estado da viatura entre Patrulha (0 estrelas) e Perseguição (1 a 6 estrelas).
        /// </summary>
        public void SetState(PoliceAIState newState, int wantedStars)
        {
            CacheComponents();
            currentState = newState;
            currentWantedStars = Mathf.Clamp(wantedStars, 0, 6);

            if (vehicleController == null || vehicleController.IsOccupied || vehicleController.IsDestroyed)
                return;

            if (currentState == PoliceAIState.Patrol)
            {
                RestoreIgnoredCollisions();
                vehicleController.SetAISpeedMultiplier(1f);
                UpdateSirenState(false);

                if (trafficAI != null)
                {
                    TrafficWaypoint wp = currentWaypoint;
                    if (wp == null && roadGraph != null)
                    {
                        wp = roadGraph.FindClosestAlignedWaypoint(transform.position, transform.forward, 25f);
                    }
                    trafficAI.enabled = true;
                    if (wp != null)
                    {
                        trafficAI.InitializeRoute(wp, snapToWaypoint: false);
                    }
                }
            }
            else
            {
                if (trafficAI != null)
                {
                    if (trafficAI.CurrentWaypoint != null)
                    {
                        currentWaypoint = trafficAI.CurrentWaypoint;
                    }
                    trafficAI.enabled = false;
                }

                if (currentWaypoint == null && roadGraph != null)
                {
                    currentWaypoint = roadGraph.FindClosestAlignedWaypoint(transform.position, transform.forward, 30f);
                }

                UpdatePursuitSpeedMultiplier();
                UpdateSirenState(true);
            }
        }

        private void Update()
        {
            if (currentState == PoliceAIState.Pursuit && !vehicleController.IsDestroyed && !vehicleController.IsOccupied)
            {
                AnimateSirenLights();
            }
        }

        private void FixedUpdate()
        {
            if (vehicleController == null || vehicleController.IsOccupied || vehicleController.IsDestroyed)
                return;

            if (currentState != PoliceAIState.Pursuit)
                return;

            if (targetPlayer == null)
            {
                vehicleController.SetAIInput(Vector2.zero);
                return;
            }

            // Atualiza dinamicamente a velocidade máxima da viatura proporcionalmente ao carro atual do jogador
            UpdatePursuitSpeedMultiplier();

            if (passThroughTimer > 0f)
            {
                passThroughTimer -= Time.fixedDeltaTime;
                if (passThroughTimer <= 0f)
                {
                    RestoreIgnoredCollisions();
                }
            }

            Vector3 playerPos = GetPlayerPosition();
            Vector3 playerVel = GetPlayerVelocity();
            float distToPlayer = Vector3.Distance(
                new Vector3(transform.position.x, 0f, transform.position.z),
                new Vector3(playerPos.x, 0f, playerPos.z)
            );

            // Manobra de ré rápida caso tenha batido de frente em um prédio/obstáculo
            if (reverseTimer > 0f)
            {
                reverseTimer -= Time.fixedDeltaTime;
                vehicleController.SetAIInput(new Vector2(reverseSteerDir, -0.85f));
                return;
            }

            Vector3 targetSteerWorld = ComputePursuitSteerTarget(playerPos, playerVel, distToPlayer, out bool isDirectIntercept);
            Vector3 localTarget = transform.InverseTransformPoint(targetSteerWorld);

            float angleToTarget = Mathf.Atan2(localTarget.x, Mathf.Max(0.2f, localTarget.z)) * Mathf.Rad2Deg;
            float steer = Mathf.Clamp(angleToTarget / maxSteerAngleRef, -1f, 1f);

            // Desvio lateral de obstáculos estáticos ou tráfego civil no caminho
            float avoidanceSteer = ComputeWhiskerAvoidance(out bool immediateFrontWall);
            if (Mathf.Abs(avoidanceSteer) > 0.05f)
            {
                steer = Mathf.Clamp(steer + avoidanceSteer, -1f, 1f);
            }

            // Em níveis baixos (1★ e 2★), a direção é um pouco menos fechada para permitir que o jogador despiste em curvas
            float steerResponsiveness = currentWantedStars <= 2 ? 0.82f : 1.0f;
            steer *= steerResponsiveness;

            if (isDirectIntercept && localTarget.z < -1.0f)
            {
                steer = (localTarget.x >= 0f ? 1f : -1f) * steerResponsiveness;
            }

            float throttle = 1.0f;

            // Em 1★ e 2★, a polícia não dá ramming brutal em alta velocidade: quando chega perto (< 7.5m), modera o acelerador
            if (currentWantedStars <= 2 && distToPlayer < 7.5f && targetPlayer.CurrentVehicle != null)
            {
                throttle = distToPlayer < 4.2f ? 0.25f : 0.60f;
            }
            else if (!isDirectIntercept && Mathf.Abs(angleToTarget) > 25f && vehicleController.ForwardSpeed > 8.5f)
            {
                throttle = 0.50f;
            }

            if (immediateFrontWall && vehicleController.ForwardSpeed > 10f)
            {
                throttle = -0.55f;
            }

            HandlePursuitUnstuck(steer);

            vehicleController.SetAIInput(new Vector2(steer, throttle));
        }

        private void UpdatePursuitSpeedMultiplier()
        {
            if (vehicleController == null) return;

            float ownBaseMaxSpeed = Mathf.Max(10f, vehicleController.MaxForwardSpeed);

            // Referência: velocidade máxima do veículo atual do jogador (ou 16.5 m/s caso o jogador esteja a pé)
            float playerRefMaxSpeed = 16.5f;
            if (targetPlayer != null && targetPlayer.CurrentVehicle != null)
            {
                playerRefMaxSpeed = Mathf.Max(12f, targetPlayer.CurrentVehicle.MaxForwardSpeed);
            }

            int starIdx = Mathf.Clamp(currentWantedStars, 0, RelativeSpeedRatioByStar.Length - 1);
            float desiredMaxSpeed = playerRefMaxSpeed * RelativeSpeedRatioByStar[starIdx];

            // Piso mínimo para não parar completamente em carros muito lentos e teto justo
            desiredMaxSpeed = Mathf.Clamp(desiredMaxSpeed, 10.5f, 24.5f);

            float multiplier = desiredMaxSpeed / ownBaseMaxSpeed;
            vehicleController.SetAISpeedMultiplier(Mathf.Clamp(multiplier, 0.45f, 1.35f));
        }

        private Vector3 ComputePursuitSteerTarget(Vector3 playerPos, Vector3 playerVel, float distToPlayer, out bool isDirectIntercept)
        {
            // Em 1★ e 2★, só tenta interceptar diretamente quando está bem perto (< 18m) e sem predizer muito à frente
            float effectiveDirectDist = currentWantedStars <= 2
                ? 17f
                : directInterceptMaxDistance;

            if (distToPlayer <= effectiveDirectDist && HasLineOfSightToPlayer(playerPos))
            {
                isDirectIntercept = true;

                // Em 1★-2★ mira na posição atual sem antecipar a curva do jogador; em 3★+ usa Lead Pursuit
                if (currentWantedStars <= 2)
                {
                    return playerPos;
                }

                float dynamicLead = Mathf.Clamp(distToPlayer / Mathf.Max(8f, vehicleController.MaxForwardSpeed), 0.08f, leadPursuitTime);
                return playerPos + new Vector3(playerVel.x, 0f, playerVel.z) * dynamicLead;
            }

            isDirectIntercept = false;

            // Navegação pelo grafo de vias (TrafficRoadGraph) escolhendo a saída mais próxima do jogador
            waypointReanchorTimer += Time.fixedDeltaTime;
            if (currentWaypoint == null || waypointReanchorTimer >= 1.8f)
            {
                waypointReanchorTimer = 0f;
                if (roadGraph != null)
                {
                    TrafficWaypoint aligned = roadGraph.FindClosestAlignedWaypoint(transform.position, transform.forward, 22f);
                    if (aligned != null)
                    {
                        currentWaypoint = aligned;
                    }
                }
            }

            if (currentWaypoint == null)
            {
                isDirectIntercept = true;
                return playerPos;
            }

            Vector3 toWp = currentWaypoint.Position - transform.position;
            toWp.y = 0f;
            float alongWp = Vector3.Dot(toWp, currentWaypoint.ForwardDirection);

            if (toWp.sqrMagnitude <= waypointReachRadius * waypointReachRadius || alongWp <= 0.35f)
            {
                previousWaypoint = currentWaypoint;
                currentWaypoint = ChooseBestNextWaypointTowards(currentWaypoint, playerPos);
            }

            return currentWaypoint != null ? currentWaypoint.Position : playerPos;
        }

        private TrafficWaypoint ChooseBestNextWaypointTowards(TrafficWaypoint fromWp, Vector3 targetWorldPos)
        {
            if (fromWp == null || fromWp.NextWaypoints == null || fromWp.NextWaypoints.Count == 0)
                return null;

            if (fromWp.NextWaypoints.Count == 1)
                return fromWp.NextWaypoints[0];

            TrafficWaypoint bestNext = fromWp.NextWaypoints[0];
            float bestScore = float.MaxValue;

            Vector3 dirToTarget = targetWorldPos - fromWp.Position;
            dirToTarget.y = 0f;
            if (dirToTarget.sqrMagnitude > 0.01f)
            {
                dirToTarget.Normalize();
            }

            for (int i = 0; i < fromWp.NextWaypoints.Count; i++)
            {
                TrafficWaypoint candidate = fromWp.NextWaypoints[i];
                if (candidate == null) continue;

                Vector3 evalPos = candidate.Position;
                if (candidate.NextWaypoints != null && candidate.NextWaypoints.Count > 0 && candidate.NextWaypoints[0] != null)
                {
                    evalPos = candidate.NextWaypoints[0].Position;
                }

                float distSq = (evalPos - targetWorldPos).sqrMagnitude;
                float alignmentBonus = Vector3.Dot(candidate.ForwardDirection, dirToTarget) * 180f;
                float score = distSq - alignmentBonus;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestNext = candidate;
                }
            }

            return bestNext;
        }

        /// <summary>
        /// Verifica se há linha de visão livre (sem prédios/muros) entre a viatura e a posição alvo.
        /// </summary>
        public bool HasLineOfSightToPosition(Vector3 worldPos)
        {
            return HasLineOfSightToPlayer(worldPos);
        }

        private bool HasLineOfSightToPlayer(Vector3 playerPos)
        {
            Vector3 origin = transform.position + Vector3.up * 0.85f;
            Vector3 dest = playerPos + Vector3.up * 0.85f;
            Vector3 diff = dest - origin;
            float dist = diff.magnitude;
            if (dist < 2.5f) return true;

            Vector3 dir = diff / dist;
            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                0.55f,
                dir,
                rayHits,
                dist - 1.2f,
                obstacleLayerMask,
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = rayHits[i].collider;
                if (col == null || col.transform.IsChildOf(transform))
                    continue;

                if (targetPlayer != null)
                {
                    if (col.transform.IsChildOf(targetPlayer.transform))
                        continue;
                    if (targetPlayer.CurrentVehicle != null && col.transform.IsChildOf(targetPlayer.CurrentVehicle.transform))
                        continue;
                }

                if (col.GetComponentInParent<VehicleController>() != null)
                    continue;

                string n = col.gameObject.name;
                if (n == "Ground" || n.StartsWith("Road_", StringComparison.Ordinal) || n.StartsWith("SidewalkLot_", StringComparison.Ordinal))
                    continue;

                return false;
            }

            return true;
        }

        private float ComputeWhiskerAvoidance(out bool immediateFrontWall)
        {
            immediateFrontWall = false;
            Vector3 origin = transform.position + Vector3.up * 0.65f + transform.forward * 1.2f;
            float checkDist = 5.5f;

            bool blockedCenter = CastObstacleRay(origin, transform.forward, checkDist, out float centerDist);
            bool blockedLeft = CastObstacleRay(origin, Quaternion.Euler(0f, -24f, 0f) * transform.forward, checkDist * 0.85f, out float leftDist);
            bool blockedRight = CastObstacleRay(origin, Quaternion.Euler(0f, 24f, 0f) * transform.forward, checkDist * 0.85f, out float rightDist);

            if (blockedCenter && centerDist < 2.2f)
            {
                immediateFrontWall = true;
            }

            if (!blockedCenter && !blockedLeft && !blockedRight)
                return 0f;

            if (blockedLeft && !blockedRight)
                return 0.65f;
            if (blockedRight && !blockedLeft)
                return -0.65f;
            if (blockedCenter)
            {
                return leftDist > rightDist ? -0.75f : 0.75f;
            }

            return 0f;
        }

        private bool CastObstacleRay(Vector3 origin, Vector3 dir, float maxDist, out float closestDist)
        {
            closestDist = maxDist;
            int hits = Physics.SphereCastNonAlloc(origin, 0.48f, dir, rayHits, maxDist, obstacleLayerMask, QueryTriggerInteraction.Ignore);
            bool found = false;

            for (int i = 0; i < hits; i++)
            {
                Collider col = rayHits[i].collider;
                if (col == null || col.transform.IsChildOf(transform))
                    continue;

                if (targetPlayer != null)
                {
                    if (col.transform.IsChildOf(targetPlayer.transform))
                        continue;
                    if (targetPlayer.CurrentVehicle != null && col.transform.IsChildOf(targetPlayer.CurrentVehicle.transform))
                        continue;
                }

                string n = col.gameObject.name;
                if (n == "Ground" || n.StartsWith("Road_", StringComparison.Ordinal) || n.StartsWith("SidewalkLot_", StringComparison.Ordinal))
                    continue;

                if (rayHits[i].distance < closestDist)
                {
                    closestDist = rayHits[i].distance;
                    found = true;
                }
            }

            return found;
        }

        private void HandlePursuitUnstuck(float currentSteer)
        {
            Vector3 delta = transform.position - stuckAnchorPos;
            delta.y = 0f;

            if (delta.sqrMagnitude >= 5.5f)
            {
                stuckAnchorPos = transform.position;
                stuckTimer = 0f;
                return;
            }

            if (Mathf.Abs(vehicleController.ForwardSpeed) < 1.1f)
            {
                stuckTimer += Time.fixedDeltaTime;
                if (stuckTimer >= stuckTimeThreshold)
                {
                    stuckTimer = 0f;
                    reverseSteerDir = Mathf.Abs(currentSteer) > 0.1f ? -Mathf.Sign(currentSteer) : -0.9f;
                    reverseTimer = reverseDuration;
                    ActivateTemporaryPassThrough();
                }
            }
        }

        private void ActivateTemporaryPassThrough()
        {
            RestoreIgnoredCollisions();
            passThroughTimer = passThroughDuration;

            if (ownCollider == null) return;

            int count = Physics.OverlapSphereNonAlloc(
                transform.position + Vector3.up * 0.7f,
                2.8f,
                overlapHits,
                obstacleLayerMask,
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < count; i++)
            {
                Collider col = overlapHits[i];
                if (col == null || col == ownCollider || col.transform.IsChildOf(transform))
                    continue;

                if (col.gameObject.name == "Ground")
                    continue;

                if (targetPlayer != null && targetPlayer.CurrentVehicle != null && col.transform.IsChildOf(targetPlayer.CurrentVehicle.transform))
                    continue;

                Physics.IgnoreCollision(ownCollider, col, true);
                ignoredColliders.Add(col);
            }
        }

        private void RestoreIgnoredCollisions()
        {
            if (ownCollider != null)
            {
                for (int i = 0; i < ignoredColliders.Count; i++)
                {
                    if (ignoredColliders[i] != null)
                    {
                        Physics.IgnoreCollision(ownCollider, ignoredColliders[i], false);
                    }
                }
            }
            ignoredColliders.Clear();
        }

        private void HandleDriverEntered(PlayerController driver)
        {
            RestoreIgnoredCollisions();
            UpdateSirenState(false);
            vehicleController.SetAISpeedMultiplier(1f);
            OnStolenByPlayer?.Invoke(this);
            enabled = false;
        }

        private void HandleVehicleExploded(VehicleController explodedVehicle)
        {
            RestoreIgnoredCollisions();
            UpdateSirenState(false);
            OnDestroyed?.Invoke(this);
            enabled = false;
        }

        private Vector3 GetPlayerPosition()
        {
            if (targetPlayer == null) return transform.position;
            if (targetPlayer.CurrentVehicle != null)
                return targetPlayer.CurrentVehicle.transform.position;
            return targetPlayer.transform.position;
        }

        private Vector3 GetPlayerVelocity()
        {
            if (targetPlayer == null) return Vector3.zero;
            if (targetPlayer.CurrentVehicle != null)
            {
                Rigidbody playerRb = targetPlayer.CurrentVehicle.GetComponent<Rigidbody>();
                if (playerRb != null) return playerRb.linearVelocity;
            }
            return targetPlayer.Velocity;
        }

        private void CacheComponents()
        {
            if (vehicleController == null)
                vehicleController = GetComponent<VehicleController>();
            if (trafficAI == null)
                trafficAI = GetComponent<TrafficVehicleAI>();
            if (rb == null)
                rb = GetComponent<Rigidbody>();
            if (ownCollider == null)
                ownCollider = GetComponent<BoxCollider>();
        }

        private void EnsureSirenVisuals()
        {
            if (sirenRoot != null) return;

            EnsureSirenMaterials();

            sirenRoot = new GameObject("PoliceSirenLightbar");
            sirenRoot.transform.SetParent(transform, false);
            sirenRoot.transform.localPosition = new Vector3(0f, 1.32f, -0.05f);

            GameObject redLens = GameObject.CreatePrimitive(PrimitiveType.Cube);
            redLens.name = "SirenLens_Red";
            redLens.transform.SetParent(sirenRoot.transform, false);
            redLens.transform.localPosition = new Vector3(-0.24f, 0f, 0f);
            redLens.transform.localScale = new Vector3(0.32f, 0.12f, 0.18f);
            Collider c1 = redLens.GetComponent<Collider>();
            if (c1 != null) Destroy(c1);
            redLensRenderer = redLens.GetComponent<MeshRenderer>();

            GameObject blueLens = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blueLens.name = "SirenLens_Blue";
            blueLens.transform.SetParent(sirenRoot.transform, false);
            blueLens.transform.localPosition = new Vector3(0.24f, 0f, 0f);
            blueLens.transform.localScale = new Vector3(0.32f, 0.12f, 0.18f);
            Collider c2 = blueLens.GetComponent<Collider>();
            if (c2 != null) Destroy(c2);
            blueLensRenderer = blueLens.GetComponent<MeshRenderer>();

            GameObject lightObj = new GameObject("SirenPointLight");
            lightObj.transform.SetParent(sirenRoot.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            sirenPointLight = lightObj.AddComponent<Light>();
            sirenPointLight.type = LightType.Point;
            sirenPointLight.range = 11f;
            sirenPointLight.intensity = 4.2f;
            sirenPointLight.shadows = LightShadows.None;
            sirenPointLight.enabled = false;
        }

        private void UpdateSirenState(bool active)
        {
            if (sirenRoot == null) return;

            if (!active)
            {
                if (redLensRenderer != null && sirenOffMat != null)
                    redLensRenderer.sharedMaterial = sirenOffMat;
                if (blueLensRenderer != null && sirenOffMat != null)
                    blueLensRenderer.sharedMaterial = sirenOffMat;
                if (sirenPointLight != null)
                    sirenPointLight.enabled = false;
            }
            else if (sirenPointLight != null)
            {
                sirenPointLight.enabled = true;
            }
        }

        private void AnimateSirenLights()
        {
            if (sirenRoot == null) return;

            bool redTurn = Mathf.Sin((Time.time + sirenPhaseOffset) * sirenFlashFrequency * Mathf.PI * 2f) >= 0f;

            if (redLensRenderer != null)
                redLensRenderer.sharedMaterial = redTurn ? sirenRedOnMat : sirenOffMat;
            if (blueLensRenderer != null)
                blueLensRenderer.sharedMaterial = redTurn ? sirenOffMat : sirenBlueOnMat;

            if (sirenPointLight != null)
            {
                sirenPointLight.enabled = true;
                sirenPointLight.color = redTurn
                    ? new Color(1.0f, 0.12f, 0.12f, 1f)
                    : new Color(0.12f, 0.45f, 1.0f, 1f);
            }
        }

        private static void EnsureSirenMaterials()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");

            if (sirenOffMat == null)
            {
                sirenOffMat = new Material(urpLit) { name = "SirenOffMat" };
                sirenOffMat.SetColor("_BaseColor", new Color(0.18f, 0.18f, 0.22f, 1f));
            }

            if (sirenRedOnMat == null)
            {
                sirenRedOnMat = new Material(urpLit) { name = "SirenRedOnMat" };
                Color c = new Color(1.0f, 0.1f, 0.1f, 1f);
                sirenRedOnMat.SetColor("_BaseColor", c);
                sirenRedOnMat.EnableKeyword("_EMISSION");
                sirenRedOnMat.SetColor("_EmissionColor", c * 3.8f);
            }

            if (sirenBlueOnMat == null)
            {
                sirenBlueOnMat = new Material(urpLit) { name = "SirenBlueOnMat" };
                Color c = new Color(0.1f, 0.45f, 1.0f, 1f);
                sirenBlueOnMat.SetColor("_BaseColor", c);
                sirenBlueOnMat.EnableKeyword("_EMISSION");
                sirenBlueOnMat.SetColor("_EmissionColor", c * 3.8f);
            }
        }
    }
}
