using System;
using System.Collections.Generic;
using UnityEngine;
using GTA2Clone.Player;
using GTA2Clone.Vehicles;

namespace GTA2Clone.Traffic
{
    /// <summary>
    /// Controlador autônomo de IA para veículos do tráfego urbano.
    /// Segue faixas de rolamento (TrafficWaypoint) com curvas suaves por Bezier/Pure-Pursuit,
    /// respeita semáforos (TrafficIntersection), evita colisões sem ponto cego frontal e possui recuperação anti-deadlock.
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    public class TrafficVehicleAI : MonoBehaviour
    {
        [Header("Navigation")]
        [SerializeField] private TrafficWaypoint currentWaypoint;
        [SerializeField] private float waypointReachRadius = 1.35f;
        [SerializeField] private float stopLineOffsetDistance = 2.0f;
        [SerializeField] private float maxSteerAngleRef = 20f;
        [SerializeField] private float straightLookaheadDistance = 3.2f;

        [Header("Urban Speed Control")]
        [SerializeField] private float cruiseSpeedRatio = 0.56f;
        [SerializeField] private float maxUrbanSpeed = 11.0f;
        [SerializeField] private float corneringSpeedLimit = 4.6f;

        [Header("Obstacle & Traffic Sensor")]
        [SerializeField] private float frontSensorOffset = 1.35f;
        [SerializeField] private float sensorRadius = 0.66f;
        [SerializeField] private float slowDownDistance = 7.5f;
        [SerializeField] private float stopDistance = 2.2f;
        [SerializeField] private LayerMask obstacleLayerMask = ~0;

        [Header("Unstuck & Anti-Deadlock Recovery")]
        [SerializeField] private float stuckTimeThreshold = 1.4f;
        [SerializeField] private float trafficDeadlockThreshold = 2.2f;
        [SerializeField] private float reverseRecoveryDuration = 1.05f;
        [SerializeField] private float passThroughRecoveryThreshold = 3.6f;
        [SerializeField] private float passThroughDuration = 2.0f;
        [SerializeField] private float maxQueueWaitTime = 11.5f;

        private VehicleController vehicleController;
        private Rigidbody rb;
        private BoxCollider ownCollider;
        private TrafficWaypoint previousWaypoint;
        private TrafficIntersection activeIntersection;

        private float vehicleHalfLength = 1.35f;
        private float vehicleHalfWidth = 0.70f;

        private Vector3 stuckAnchorPosition;
        private float stuckTimer;
        private float persistentBlockTimer;
        private float queueWaitTimer;
        private float reverseRecoveryTimer;
        private float reverseSteerDirection = -0.85f;
        private float passThroughTimer;

        private readonly List<Collider> ignoredColliders = new List<Collider>(8);
        private readonly RaycastHit[] sensorHits = new RaycastHit[12];
        private readonly Collider[] overlapHits = new Collider[12];

        public VehicleController Vehicle => vehicleController;
        public TrafficWaypoint CurrentWaypoint => currentWaypoint;
        public bool IsWaitingAtRedLight { get; private set; }

        /// <summary>
        /// Disparado quando o jogador entra neste veículo e assume a direção.
        /// </summary>
        public event Action<TrafficVehicleAI> OnStolenByPlayer;

        private void Awake()
        {
            CacheComponentsAndDimensions();
        }

        private void OnEnable()
        {
            CacheComponentsAndDimensions();
            stuckAnchorPosition = transform.position;

            if (vehicleController != null)
            {
                vehicleController.OnDriverEntered -= HandleDriverEntered;
                vehicleController.OnDriverEntered += HandleDriverEntered;
            }
        }

        private void OnDisable()
        {
            RestoreIgnoredCollisions();
            ReleaseActiveIntersection();
            IsWaitingAtRedLight = false;

            if (vehicleController != null)
            {
                vehicleController.OnDriverEntered -= HandleDriverEntered;
                vehicleController.ClearAIInput();
            }
        }

        /// <summary>
        /// Inicializa o veículo de tráfego em um waypoint inicial e ativa a condução autônoma.
        /// </summary>
        public void InitializeRoute(TrafficWaypoint spawnWaypoint, bool snapToWaypoint = true)
        {
            CacheComponentsAndDimensions();

            RestoreIgnoredCollisions();
            ReleaseActiveIntersection();
            IsWaitingAtRedLight = false;
            stuckTimer = 0f;
            persistentBlockTimer = 0f;
            queueWaitTimer = 0f;
            reverseRecoveryTimer = 0f;
            passThroughTimer = 0f;

            if (spawnWaypoint != null)
            {
                if (snapToWaypoint)
                {
                    transform.SetPositionAndRotation(
                        new Vector3(spawnWaypoint.Position.x, 0.08f, spawnWaypoint.Position.z),
                        Quaternion.LookRotation(spawnWaypoint.ForwardDirection, Vector3.up)
                    );

                    if (rb != null)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                }

                previousWaypoint = spawnWaypoint;
                currentWaypoint = spawnWaypoint.GetNextWaypoint() ?? spawnWaypoint;
            }

            stuckAnchorPosition = transform.position;
            enabled = true;
        }

        private void CacheComponentsAndDimensions()
        {
            if (vehicleController == null)
            {
                vehicleController = GetComponent<VehicleController>();
            }
            if (rb == null)
            {
                rb = GetComponent<Rigidbody>();
            }
            if (ownCollider == null)
            {
                ownCollider = GetComponent<BoxCollider>();
            }

            if (ownCollider != null)
            {
                Vector3 lossy = transform.lossyScale;
                vehicleHalfWidth = Mathf.Max(0.55f, ownCollider.size.x * Mathf.Abs(lossy.x) * 0.5f);
                vehicleHalfLength = Mathf.Max(1.10f, ownCollider.size.z * Mathf.Abs(lossy.z) * 0.5f);
                sensorRadius = Mathf.Max(0.62f, vehicleHalfWidth * 0.94f);
                frontSensorOffset = vehicleHalfLength;
            }
        }

        private void FixedUpdate()
        {
            if (vehicleController == null || vehicleController.IsOccupied)
                return;

            if (currentWaypoint == null)
            {
                vehicleController.SetAIInput(Vector2.zero);
                return;
            }

            if (passThroughTimer > 0f)
            {
                passThroughTimer -= Time.fixedDeltaTime;
                if (passThroughTimer <= 0f)
                {
                    RestoreIgnoredCollisions();
                }
            }

            AdvanceWaypointIfNeeded();

            if (currentWaypoint == null)
            {
                vehicleController.SetAIInput(Vector2.zero);
                return;
            }

            Vector3 steerTargetWorld = ComputeSteeringTargetPosition();
            Vector3 localTarget = transform.InverseTransformPoint(steerTargetWorld);
            float angleToTarget = Mathf.Atan2(localTarget.x, Mathf.Max(0.15f, localTarget.z)) * Mathf.Rad2Deg;
            float steer = Mathf.Clamp(angleToTarget / maxSteerAngleRef, -1f, 1f);

            float currentForwardSpeed = vehicleController.ForwardSpeed;

            // Se estiver executando manobra de ré para sair de atolamento
            if (reverseRecoveryTimer > 0f)
            {
                reverseRecoveryTimer -= Time.fixedDeltaTime;
                UpdateDisplacementTracking();
                persistentBlockTimer += Time.fixedDeltaTime;

                if (IsRearBlocked() || persistentBlockTimer >= passThroughRecoveryThreshold)
                {
                    reverseRecoveryTimer = 0f;
                    stuckTimer = 0f;
                    ActivatePassThrough(null);
                }
                else
                {
                    vehicleController.SetAIInput(new Vector2(reverseSteerDirection, -0.9f));
                    return;
                }
            }

            // Calcula velocidade desejada considerando curvas, cruzamentos e semáforos
            float baseCruiseSpeed = Mathf.Min(vehicleController.MaxForwardSpeed * cruiseSpeedRatio, maxUrbanSpeed);
            bool isTurnSegment = previousWaypoint != null &&
                                 Vector3.Dot(previousWaypoint.ForwardDirection, currentWaypoint.ForwardDirection) < 0.75f;
            bool isTurning = isTurnSegment || Mathf.Abs(angleToTarget) > 16f || currentWaypoint.IsIntersectionEntry;
            float desiredSpeed = isTurning ? Mathf.Min(baseCruiseSpeed, corneringSpeedLimit) : baseCruiseSpeed;

            IsWaitingAtRedLight = false;
            bool isWaitingForIntersectionClearance = false;

            if (currentWaypoint.ControlledIntersection != null && !currentWaypoint.CanProceed(this))
            {
                Vector3 toStopLine = currentWaypoint.Position - transform.position;
                toStopLine.y = 0f;
                float alongApproach = Vector3.Dot(toStopLine, currentWaypoint.ForwardDirection);

                // Só para na linha de retenção se ainda estiver antes dela (alongApproach > -0.35m)
                if (alongApproach > -0.35f)
                {
                    float distToStopLine = toStopLine.magnitude;
                    float stopThreshold = Mathf.Max(waypointReachRadius, stopLineOffsetDistance);

                    if (distToStopLine <= stopThreshold || alongApproach <= stopThreshold)
                    {
                        desiredSpeed = 0f;
                        steer = 0f;
                        if (currentWaypoint.IsSignalRedOrYellow())
                        {
                            IsWaitingAtRedLight = true;
                        }
                        else
                        {
                            isWaitingForIntersectionClearance = true;
                        }
                    }
                    else if (distToStopLine < slowDownDistance)
                    {
                        float stopLineFactor = Mathf.InverseLerp(stopThreshold, slowDownDistance, distToStopLine);
                        desiredSpeed = Mathf.Min(desiredSpeed, baseCruiseSpeed * stopLineFactor);
                        if (distToStopLine <= stopThreshold + 1.2f)
                        {
                            if (currentWaypoint.IsSignalRedOrYellow())
                            {
                                IsWaitingAtRedLight = true;
                            }
                            else
                            {
                                isWaitingForIntersectionClearance = true;
                            }
                        }
                    }
                }
            }

            bool hasObstacle = false;
            float obstacleBumperDistance = slowDownDistance;
            bool isWaitingBehindQueue = false;
            bool isBlockedByPlayer = false;
            bool isBlockedByParkedVehicle = false;
            Collider blockerCollider = null;

            if (passThroughTimer <= 0f)
            {
                hasObstacle = DetectFrontObstacle(
                    steer,
                    out obstacleBumperDistance,
                    out isWaitingBehindQueue,
                    out isBlockedByPlayer,
                    out isBlockedByParkedVehicle,
                    out blockerCollider
                );
            }

            if (hasObstacle)
            {
                float effectiveStopDist = activeIntersection != null ? stopDistance * 0.65f : stopDistance;
                if (obstacleBumperDistance <= effectiveStopDist)
                {
                    desiredSpeed = 0f;
                }
                else
                {
                    float distFactor = Mathf.InverseLerp(effectiveStopDist, slowDownDistance, obstacleBumperDistance);
                    desiredSpeed *= distFactor;
                }

                if (isWaitingBehindQueue && obstacleBumperDistance <= effectiveStopDist + 1.2f)
                {
                    IsWaitingAtRedLight = true;
                }
            }

            // Evita velocidades residuais minúsculas que fazem o carro pulsar acelerador e freio sem sair do lugar
            if (desiredSpeed < 0.65f)
            {
                desiredSpeed = 0f;
            }

            float throttle;
            if (desiredSpeed <= 0.05f)
            {
                // Freia até parar completamente sem engatar a marcha à ré
                throttle = currentForwardSpeed > 0.25f ? -1f : 0f;
            }
            else if (currentForwardSpeed > desiredSpeed + 1.0f)
            {
                throttle = -0.65f;
            }
            else
            {
                // Em VehicleController, maxForwardSpeed * throttle define a velocidade-alvo exata do MoveTowards
                throttle = Mathf.Clamp(desiredSpeed / Mathf.Max(1f, vehicleController.MaxForwardSpeed), 0.12f, 1f);
            }

            HandleAntiDeadlock(
                currentForwardSpeed,
                steer,
                hasObstacle,
                isWaitingBehindQueue || isWaitingForIntersectionClearance,
                isBlockedByPlayer,
                isBlockedByParkedVehicle,
                blockerCollider
            );

            vehicleController.SetAIInput(new Vector2(steer, throttle));
        }

        private Vector3 ComputeSteeringTargetPosition()
        {
            if (previousWaypoint == null || previousWaypoint == currentWaypoint)
            {
                return currentWaypoint.Position;
            }

            Vector3 p0 = previousWaypoint.Position;
            Vector3 p2 = currentWaypoint.Position;
            p0.y = transform.position.y;
            p2.y = transform.position.y;

            Vector3 d0 = previousWaypoint.ForwardDirection;
            Vector3 d2 = currentWaypoint.ForwardDirection;
            d0.y = 0f;
            d2.y = 0f;
            if (d0.sqrMagnitude > 0.001f) d0.Normalize();
            if (d2.sqrMagnitude > 0.001f) d2.Normalize();

            float dirDot = Vector3.Dot(d0, d2);

            // 1. Trecho reto: Pure-Pursuit ao longo da linha central da faixa para manter o carro sempre centralizado
            if (dirDot > 0.85f)
            {
                Vector3 seg = p2 - p0;
                float segLen = seg.magnitude;
                if (segLen < 0.1f)
                    return p2;

                Vector3 segDir = seg / segLen;
                float proj = Vector3.Dot(transform.position - p0, segDir);
                float lookaheadDist = Mathf.Clamp(proj + straightLookaheadDistance, 0f, segLen);
                return p0 + segDir * lookaheadDist;
            }

            // 2. Curva de 90° (Direita ou Esquerda) ou Retorno de 180°: Curva de Bézier quadrática pelo vértice das faixas
            Vector3 p1;
            if (dirDot < -0.5f)
            {
                // Retorno em fim de rua (U-Turn)
                p1 = (p0 + p2) * 0.5f + d0 * 2.8f;
            }
            else
            {
                // Interseção geométrica das duas faixas ortogonais
                float alongIn = Mathf.Clamp(Vector3.Dot(p2 - p0, d0), 1.0f, 6.8f);
                p1 = p0 + d0 * alongIn;
            }

            float chordLen = Vector3.Distance(p0, p2);
            float distFromStart = Vector3.Distance(
                new Vector3(transform.position.x, p0.y, transform.position.z),
                p0
            );
            float u = chordLen > 0.1f ? Mathf.Clamp01(distFromStart / chordLen) : 0.5f;
            float lookaheadU = Mathf.Clamp01(u + 0.38f);

            float oneMinusU = 1f - lookaheadU;
            return (oneMinusU * oneMinusU * p0) + (2f * oneMinusU * lookaheadU * p1) + (lookaheadU * lookaheadU * p2);
        }

        private void UpdateDisplacementTracking()
        {
            Vector3 flatDelta = transform.position - stuckAnchorPosition;
            flatDelta.y = 0f;

            if (flatDelta.sqrMagnitude >= 7.84f) // Deslocou pelo menos 2.8m de fato
            {
                stuckAnchorPosition = transform.position;
                stuckTimer = 0f;
                persistentBlockTimer = 0f;
                queueWaitTimer = 0f;
            }
        }

        private void HandleAntiDeadlock(
            float currentForwardSpeed,
            float currentSteer,
            bool hasObstacle,
            bool isLegitimatelyWaiting,
            bool isBlockedByPlayer,
            bool isBlockedByParkedVehicle,
            Collider blockerCollider)
        {
            UpdateDisplacementTracking();

            // Se está bloqueado diretamente pelo jogador a pé, aguarda pacientemente sem atropelar
            if (isBlockedByPlayer)
            {
                stuckTimer = 0f;
                persistentBlockTimer = 0f;
                queueWaitTimer = 0f;
                return;
            }

            // Se há um carro estacionado/abandonado sem motorista bloqueando a faixa, desvia/atravessa rápido
            if (isBlockedByParkedVehicle && Mathf.Abs(currentForwardSpeed) < 0.6f)
            {
                stuckTimer += Time.fixedDeltaTime;
                if (stuckTimer >= 1.1f)
                {
                    stuckTimer = 0f;
                    persistentBlockTimer = 0f;
                    ActivatePassThrough(blockerCollider);
                }
                return;
            }

            // Se está aguardando no sinal vermelho ou fila, permite esperar até o limite máximo de um ciclo de semáforo
            if (IsWaitingAtRedLight || isLegitimatelyWaiting)
            {
                if (Mathf.Abs(currentForwardSpeed) < 0.45f)
                {
                    queueWaitTimer += Time.fixedDeltaTime;
                    if (queueWaitTimer < maxQueueWaitTime)
                    {
                        stuckTimer = 0f;
                        persistentBlockTimer = 0f;
                        return;
                    }
                    // Se passou de maxQueueWaitTime parado, há um deadlock real no cruzamento/fila
                }
                else
                {
                    queueWaitTimer = 0f;
                    return;
                }
            }
            else
            {
                queueWaitTimer = 0f;
            }

            if (Mathf.Abs(currentForwardSpeed) < 0.85f)
            {
                stuckTimer += Time.fixedDeltaTime;
                persistentBlockTimer += Time.fixedDeltaTime;

                // Estágio 3: Caso extremo (> 6.8s sem conseguir avançar mesmo após pass-through) -> Realinha na faixa
                if (persistentBlockTimer >= passThroughRecoveryThreshold + 3.2f)
                {
                    SnapToCurrentLane();
                    return;
                }

                // Estágio 2: Impasse prolongado (> 3.6s) -> Ignora colisão com todos os obstáculos ao redor e avança
                if (persistentBlockTimer >= passThroughRecoveryThreshold)
                {
                    stuckTimer = 0f;
                    reverseRecoveryTimer = 0f;
                    ActivatePassThrough(blockerCollider);
                    return;
                }

                // Estágio 1: Preso contra obstáculo ou bloqueio mútuo -> Manobra de ré (se houver espaço atrás) ou Pass-Through imediato
                float threshold = hasObstacle ? trafficDeadlockThreshold : stuckTimeThreshold;
                if (stuckTimer >= threshold)
                {
                    stuckTimer = 0f;
                    if (IsRearBlocked())
                    {
                        ActivatePassThrough(blockerCollider);
                    }
                    else
                    {
                        reverseSteerDirection = Mathf.Abs(currentSteer) > 0.08f ? -Mathf.Sign(currentSteer) : -0.85f;
                        reverseRecoveryTimer = reverseRecoveryDuration;
                    }
                }
            }
        }

        private bool IsRearBlocked()
        {
            Vector3 rearCheckPos = transform.position + Vector3.up * 0.65f - transform.forward * (vehicleHalfLength + 0.45f);
            int count = Physics.OverlapSphereNonAlloc(
                rearCheckPos,
                Mathf.Max(0.5f, vehicleHalfWidth * 0.85f),
                overlapHits,
                obstacleLayerMask,
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < count; i++)
            {
                Collider col = overlapHits[i];
                if (col == null || col.transform.IsChildOf(transform)) continue;
                if (IsIgnoredRoadSurface(col.gameObject.name)) continue;
                return true;
            }

            return false;
        }

        private void ActivatePassThrough(Collider primaryBlocker)
        {
            RestoreIgnoredCollisions();
            passThroughTimer = passThroughDuration;

            if (ownCollider == null) return;

            if (primaryBlocker != null && primaryBlocker != ownCollider && !primaryBlocker.transform.IsChildOf(transform))
            {
                Physics.IgnoreCollision(ownCollider, primaryBlocker, true);
                ignoredColliders.Add(primaryBlocker);
            }

            // Ignora também qualquer outro veículo, poste ou quina imediatamente colado ao chassi
            Vector3 center = transform.position + Vector3.up * 0.7f;
            float radius = vehicleHalfLength + 1.6f;
            int count = Physics.OverlapSphereNonAlloc(
                center,
                radius,
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

                if (!ignoredColliders.Contains(col))
                {
                    Physics.IgnoreCollision(ownCollider, col, true);
                    ignoredColliders.Add(col);
                }
            }
        }

        private void RestoreIgnoredCollisions()
        {
            if (ownCollider != null)
            {
                for (int i = 0; i < ignoredColliders.Count; i++)
                {
                    Collider col = ignoredColliders[i];
                    if (col != null)
                    {
                        Physics.IgnoreCollision(ownCollider, col, false);
                    }
                }
            }
            ignoredColliders.Clear();
        }

        private void SnapToCurrentLane()
        {
            persistentBlockTimer = 0f;
            stuckTimer = 0f;
            queueWaitTimer = 0f;
            reverseRecoveryTimer = 0f;

            if (currentWaypoint == null) return;

            ActivatePassThrough(null);
            transform.SetPositionAndRotation(
                new Vector3(currentWaypoint.Position.x, 0.08f, currentWaypoint.Position.z),
                Quaternion.LookRotation(currentWaypoint.ForwardDirection, Vector3.up)
            );

            if (rb != null)
            {
                rb.linearVelocity = currentWaypoint.ForwardDirection * 3.0f;
                rb.angularVelocity = Vector3.zero;
            }

            stuckAnchorPosition = transform.position;
            previousWaypoint = currentWaypoint;
            currentWaypoint = currentWaypoint.GetNextWaypoint() ?? currentWaypoint;
        }

        private void AdvanceWaypointIfNeeded()
        {
            Vector3 toWaypoint = currentWaypoint.Position - transform.position;
            toWaypoint.y = 0f;
            float distSq = toWaypoint.sqrMagnitude;

            Vector3 wpForward = currentWaypoint.ForwardDirection;
            float alongWpDir = Vector3.Dot(toWaypoint, wpForward);

            // Lateral offset em relação à linha da faixa do waypoint atual
            Vector3 lateralVec = toWaypoint - (wpForward * alongWpDir);
            float lateralDistSq = lateralVec.sqrMagnitude;

            bool reachedRadius = distSq <= waypointReachRadius * waypointReachRadius;

            // Considera que cruzou a linha do waypoint se já passou do plano dele (alongWpDir <= 0.15f)
            // estando alinhado à faixa (distância lateral < 2.4m)
            bool crossedWaypointPlane = alongWpDir <= 0.15f &&
                                        alongWpDir > -4.5f &&
                                        lateralDistSq <= 5.76f &&
                                        Vector3.Dot(transform.forward, wpForward) > 0.25f;

            // Para linha de retenção de semáforo, permite avaliar a parada um pouco antes (stopLineOffsetDistance)
            bool atStopLine = currentWaypoint.ControlledIntersection != null &&
                              alongWpDir <= stopLineOffsetDistance &&
                              alongWpDir > -3.5f &&
                              lateralDistSq <= 5.76f;

            if (!reachedRadius && !crossedWaypointPlane && !atStopLine)
                return;

            // Se o waypoint atual é linha de retenção de um semáforo:
            if (currentWaypoint.ControlledIntersection != null)
            {
                // Se o carro ainda está antes da linha de retenção (alongWpDir > -0.35f), respeita o semáforo/ocupação
                if (alongWpDir > -0.35f && !currentWaypoint.CanProceed(this))
                {
                    return;
                }

                // Só avança do WP_In para o WP_Out quando realmente chega perto da linha (reachedRadius ou crossedWaypointPlane)
                if (!reachedRadius && !crossedWaypointPlane)
                {
                    return;
                }

                if (activeIntersection != null && activeIntersection != currentWaypoint.ControlledIntersection)
                {
                    activeIntersection.NotifyVehicleExited(this);
                }

                activeIntersection = currentWaypoint.ControlledIntersection;
                activeIntersection.NotifyVehicleEntered(this, wpForward);
            }
            else if (activeIntersection != null)
            {
                // Ao atingir o waypoint de saída do cruzamento, libera a reserva da interseção
                activeIntersection.NotifyVehicleExited(this);
                activeIntersection = null;
            }

            previousWaypoint = currentWaypoint;
            currentWaypoint = currentWaypoint.GetNextWaypoint();
            stuckAnchorPosition = transform.position;
            stuckTimer = 0f;
            persistentBlockTimer = 0f;
            queueWaitTimer = 0f;
        }

        private void ReleaseActiveIntersection()
        {
            if (activeIntersection != null)
            {
                activeIntersection.NotifyVehicleExited(this);
                activeIntersection = null;
            }
        }

        private bool DetectFrontObstacle(
            float currentSteer,
            out float closestBumperDistance,
            out bool isWaitingBehindQueue,
            out bool isBlockedByPlayer,
            out bool isBlockedByParkedVehicle,
            out Collider closestBlockerCollider)
        {
            closestBumperDistance = slowDownDistance;
            isWaitingBehindQueue = false;
            isBlockedByPlayer = false;
            isBlockedByParkedVehicle = false;
            closestBlockerCollider = null;

            bool found = false;

            // 1. Verificação de contato imediato no para-choque frontal (elimina o ponto cego do SphereCast quando a distância é ~0m)
            Vector3 bumperCenter = transform.position + Vector3.up * 0.65f + transform.forward * (vehicleHalfLength + 0.25f);
            float bumperRadius = Mathf.Max(0.58f, vehicleHalfWidth * 0.90f);
            int overlapCount = Physics.OverlapSphereNonAlloc(
                bumperCenter,
                bumperRadius,
                overlapHits,
                obstacleLayerMask,
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < overlapCount; i++)
            {
                Collider col = overlapHits[i];
                if (!IsValidObstacleCollider(col, isAngledCast: false, out VehicleController otherVehicle, out TrafficVehicleAI otherAI, out PlayerController hitPlayer))
                    continue;

                closestBumperDistance = 0f;
                found = true;
                closestBlockerCollider = col;
                ClassifyBlocker(otherVehicle, otherAI, hitPlayer, out isWaitingBehindQueue, out isBlockedByPlayer, out isBlockedByParkedVehicle);
                return true;
            }

            // 2. SphereCast frontal partindo de dentro do chassi (+0.20m) para nunca perder colisores próximos ao para-choque
            const float castStartLocalZ = 0.20f;
            float bumperOffsetFromOrigin = Mathf.Max(0.5f, vehicleHalfLength - castStartLocalZ);
            Vector3 castOrigin = transform.position + Vector3.up * 0.65f + transform.forward * castStartLocalZ;
            float castMaxDist = slowDownDistance + bumperOffsetFromOrigin;

            int hitCount = Physics.SphereCastNonAlloc(
                castOrigin,
                sensorRadius,
                transform.forward,
                sensorHits,
                castMaxDist,
                obstacleLayerMask,
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = sensorHits[i];
                Collider col = hit.collider;
                if (!IsValidObstacleCollider(col, isAngledCast: false, out VehicleController otherVehicle, out TrafficVehicleAI otherAI, out PlayerController hitPlayer))
                    continue;

                float bumperDist = Mathf.Max(0f, hit.distance - bumperOffsetFromOrigin);
                if (bumperDist < closestBumperDistance)
                {
                    closestBumperDistance = bumperDist;
                    found = true;
                    closestBlockerCollider = col;
                    ClassifyBlocker(otherVehicle, otherAI, hitPlayer, out isWaitingBehindQueue, out isBlockedByPlayer, out isBlockedByParkedVehicle);
                }
            }

            // 3. Sensor suplementar direcionado para a curva quando o veículo está esterçando
            if (Mathf.Abs(currentSteer) > 0.18f)
            {
                Vector3 steeredDir = Quaternion.Euler(0f, currentSteer * 24f, 0f) * transform.forward;
                float turnCastDist = (stopDistance + 2.2f) + bumperOffsetFromOrigin;
                int turnHits = Physics.SphereCastNonAlloc(
                    castOrigin,
                    sensorRadius * 0.85f,
                    steeredDir,
                    sensorHits,
                    turnCastDist,
                    obstacleLayerMask,
                    QueryTriggerInteraction.Ignore
                );

                for (int i = 0; i < turnHits; i++)
                {
                    RaycastHit hit = sensorHits[i];
                    Collider col = hit.collider;
                    if (!IsValidObstacleCollider(col, isAngledCast: true, out VehicleController otherVehicle, out TrafficVehicleAI otherAI, out PlayerController hitPlayer))
                        continue;

                    float bumperDist = Mathf.Max(0f, hit.distance - bumperOffsetFromOrigin);
                    if (bumperDist < closestBumperDistance)
                    {
                        closestBumperDistance = bumperDist;
                        found = true;
                        closestBlockerCollider = col;
                        ClassifyBlocker(otherVehicle, otherAI, hitPlayer, out isWaitingBehindQueue, out isBlockedByPlayer, out isBlockedByParkedVehicle);
                    }
                }
            }

            return found;
        }

        private bool IsValidObstacleCollider(
            Collider col,
            bool isAngledCast,
            out VehicleController otherVehicle,
            out TrafficVehicleAI otherAI,
            out PlayerController hitPlayer)
        {
            otherVehicle = null;
            otherAI = null;
            hitPlayer = null;

            if (col == null || col.transform.IsChildOf(transform))
                return false;

            if (IsIgnoredRoadSurface(col.gameObject.name))
                return false;

            hitPlayer = col.GetComponentInParent<PlayerController>();
            if (hitPlayer != null)
                return true;

            otherVehicle = col.GetComponentInParent<VehicleController>();
            otherAI = otherVehicle != null ? otherVehicle.GetComponent<TrafficVehicleAI>() : null;

            if (otherVehicle != null)
            {
                float headingDot = Vector3.Dot(transform.forward, otherVehicle.transform.forward);
                Vector3 localOtherPos = transform.InverseTransformPoint(otherVehicle.transform.position);

                // Ignora carros que já ficaram atrás do nosso centro
                if (localOtherPos.z < 0.2f)
                    return false;

                // 1. Ignora carros na mão contrária se estiverem na própria faixa oposta (deslocamento lateral > 1.30m)
                if (headingDot < -0.25f && Mathf.Abs(localOtherPos.x) > 1.30f)
                    return false;

                // 2. Se este carro está atravessando ou virando em cruzamento, ignora carros parados no sinal vermelho de vias transversais/opostas
                if (otherAI != null && otherAI.IsWaitingAtRedLight && headingDot < 0.55f && Mathf.Abs(localOtherPos.x) > 1.15f)
                    return false;

                return true;
            }

            // No sensor angulado de curva, ignora prédios/calçadas laterais para não frear sem necessidade ao contornar esquinas
            if (isAngledCast)
                return false;

            // Para obstáculos estáticos no sensor frontal reto, ignora objetos que estão na calçada fora da largura do carro
            Vector3 closestPoint = col.ClosestPoint(transform.position + Vector3.up * 0.65f + transform.forward * vehicleHalfLength);
            Vector3 localPoint = transform.InverseTransformPoint(closestPoint);
            if (Mathf.Abs(localPoint.x) > vehicleHalfWidth + 0.18f)
                return false;

            return true;
        }

        private void ClassifyBlocker(
            VehicleController otherVehicle,
            TrafficVehicleAI otherAI,
            PlayerController hitPlayer,
            out bool isWaitingBehindQueue,
            out bool isBlockedByPlayer,
            out bool isBlockedByParkedVehicle)
        {
            isWaitingBehindQueue = false;
            isBlockedByPlayer = false;
            isBlockedByParkedVehicle = false;

            if (hitPlayer != null)
            {
                isBlockedByPlayer = true;
                isWaitingBehindQueue = true;
                return;
            }

            if (otherVehicle != null)
            {
                if (!otherVehicle.IsOccupied && (otherAI == null || !otherAI.isActiveAndEnabled))
                {
                    isBlockedByParkedVehicle = true;
                    return;
                }

                if (otherAI != null && otherAI.IsWaitingAtRedLight &&
                    Vector3.Dot(transform.forward, otherAI.transform.forward) > 0.55f)
                {
                    isWaitingBehindQueue = true;
                }
            }
        }

        private static bool IsIgnoredRoadSurface(string objName)
        {
            return objName == "Ground" ||
                   objName.StartsWith("Road_") ||
                   objName.StartsWith("SidewalkLot_");
        }

        private void HandleDriverEntered(PlayerController driver)
        {
            RestoreIgnoredCollisions();
            ReleaseActiveIntersection();
            IsWaitingAtRedLight = false;

            if (vehicleController != null)
            {
                vehicleController.ClearAIInput();
            }

            OnStolenByPlayer?.Invoke(this);
            enabled = false;
        }
    }
}
