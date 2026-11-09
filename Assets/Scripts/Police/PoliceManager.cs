using System;
using System.Collections.Generic;
using UnityEngine;
using GTA2Clone.Audio;
using GTA2Clone.Missions;
using GTA2Clone.Player;
using GTA2Clone.Traffic;
using GTA2Clone.Vehicles;

namespace GTA2Clone.Police
{
    /// <summary>
    /// Gerencia o Nível de Procurado (Wanted Level de 0 a 6 estrelas), coordena as viaturas de patrulha/perseguição
    /// e aplica a condição de derrota (Busted / Preso):
    /// - A cada 3 entregas de veículos roubados, a notoriedade aumenta o nível de procurado (escalando até 6 estrelas).
    /// - Nas demais entregas, o Desmanche (DeliveryZone) esconde o jogador, dispersa viaturas próximas e reduz -1 estrela.
    /// - Se a polícia estiver perto (e com linha de visão) quando o jogador roubar um carro em movimento, aumenta +1 estrela.
    /// - Prisão por Captura (Busted): o jogador pode sair do carro livremente mesmo sob perseguição;
    ///   a prisão ocorre se uma viatura policial alcançar e encostar nele enquanto estiver a pé.
    /// </summary>
    public class PoliceManager : MonoBehaviour
    {
        public const int MaxWantedStars = 6;

        [Header("Scene References")]
        [SerializeField] private PlayerController player;
        [SerializeField] private MissionManager missionManager;
        [SerializeField] private TrafficRoadGraph roadGraph;
        [SerializeField] private VehicleController standardPolicePrefab;
        [SerializeField] private VehicleController heavyPolicePrefab;
        [SerializeField] private List<VehicleController> initialScenePoliceVehicles = new List<VehicleController>();

        [Header("Wanted Level & Busted Rules")]
        [SerializeField] private int deliveriesPerStarIncrease = 3;
        [SerializeField] private float policeWitnessRadius = 24f;
        [SerializeField] private float bustedOnFootContactRadius = 3.1f;
        [SerializeField] private float postDeliverySafeGraceSeconds = 3.5f;

        [Header("Spawn & Patrol Settings")]
        [SerializeField] private int patrolUnitsAtZeroStars = 2;
        [SerializeField] private float minSpawnDistance = 36f;
        [SerializeField] private float maxSpawnDistance = 82f;
        [SerializeField] private float relocateDistance = 110f;
        [SerializeField] private float spawnClearanceRadius = 4.0f;
        [SerializeField] private float maintenanceInterval = 0.4f;
        [SerializeField] private LayerMask obstacleLayerMask = ~0;
        [SerializeField] private AudioClip policeSirenClip;

        private readonly List<PoliceVehicleAI> activePoliceUnits = new List<PoliceVehicleAI>(12);
        private readonly Collider[] witnessHits = new Collider[24];
        private readonly RaycastHit[] losHits = new RaycastHit[12];

        private int wantedStars;
        private float maintenanceTimer;
        private float postDeliveryGraceTimer;
        private bool isBustedTriggered;

        public int WantedStars => wantedStars;
        public bool IsPursuitActive => wantedStars > 0;
        public int ActivePoliceCount => activePoliceUnits.Count;
        public float PoliceWitnessRadius => policeWitnessRadius;

        /// <summary>
        /// Disparado quando o nível de estrelas de procurado muda (oldStars, newStars).
        /// </summary>
        public event Action<int, int> OnWantedStarsChanged;

        /// <summary>
        /// Disparado com mensagem descritiva quando o nível de procurado muda ou há alerta policial (message, stars).
        /// </summary>
        public event Action<string, int> OnPoliceAlertTriggered;

        /// <summary>
        /// Disparado quando o jogador é preso pela polícia (Busted).
        /// </summary>
        public event Action<string> OnPlayerBusted;

        private void Awake()
        {
            if (GetComponent<PoliceRadioAudio>() == null)
            {
                gameObject.AddComponent<PoliceRadioAudio>();
            }
        }

        private void OnEnable()
        {
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void Start()
        {
            wantedStars = 0;
            isBustedTriggered = false;
            postDeliveryGraceTimer = 0f;
            AdoptInitialScenePoliceVehicles();
            MaintainPoliceUnits();
            OnWantedStarsChanged?.Invoke(0, wantedStars);
        }

        private void Update()
        {
            if (isBustedTriggered || (missionManager != null && missionManager.IsGameOver))
                return;

            if (postDeliveryGraceTimer > 0f)
            {
                postDeliveryGraceTimer -= Time.deltaTime;
            }

            // Se o jogador estiver a pé com nível de procurado ativo, verifica se alguma viatura o alcançou/capturou
            if (wantedStars > 0 && postDeliveryGraceTimer <= 0f && player != null && player.CurrentVehicle == null)
            {
                if (IsPoliceTouchingPlayerOnFoot(player.transform.position))
                {
                    TriggerPlayerBusted("CAPTURADO PELA VIATURA POLICIAL ENQUANTO CORRIA A PÉ!");
                    return;
                }
            }

            maintenanceTimer += Time.deltaTime;
            if (maintenanceTimer >= maintenanceInterval)
            {
                maintenanceTimer = 0f;
                MaintainPoliceUnits();
            }
        }

        /// <summary>
        /// Configura todas as referências do PoliceManager (usado pelas ferramentas de Editor).
        /// </summary>
        public void Configure(
            PlayerController targetPlayer,
            MissionManager targetMissionManager,
            TrafficRoadGraph targetGraph,
            VehicleController policePrefab,
            VehicleController heavyPrefab,
            List<VehicleController> scenePoliceVehicles = null,
            AudioClip sirenClip = null)
        {
            UnsubscribeEvents();
            player = targetPlayer;
            missionManager = targetMissionManager;
            roadGraph = targetGraph;
            standardPolicePrefab = policePrefab;
            heavyPolicePrefab = heavyPrefab;
            initialScenePoliceVehicles = scenePoliceVehicles ?? new List<VehicleController>();
            if (sirenClip != null)
            {
                policeSirenClip = sirenClip;
            }

            if (isActiveAndEnabled)
            {
                SubscribeEvents();
            }
        }

        /// <summary>
        /// Aumenta o nível de procurado (Wanted Level) em `starsDelta` estrelas, até o limite máximo de 6 estrelas.
        /// </summary>
        public void IncreaseWantedLevel(int starsDelta, string alertReason)
        {
            if (starsDelta <= 0 || isBustedTriggered)
                return;

            if (missionManager != null && missionManager.IsGameOver)
                return;

            SetWantedLevelInternal(Mathf.Clamp(wantedStars + starsDelta, 0, MaxWantedStars), alertReason);
        }

        /// <summary>
        /// Reduz o nível de procurado (Wanted Level) em `starsDelta` estrelas (ex: ao se esconder no Desmanche).
        /// </summary>
        public void DecreaseWantedLevel(int starsDelta, string alertReason)
        {
            if (starsDelta <= 0 || isBustedTriggered || wantedStars <= 0)
                return;

            if (missionManager != null && missionManager.IsGameOver)
                return;

            SetWantedLevelInternal(Mathf.Clamp(wantedStars - starsDelta, 0, MaxWantedStars), alertReason);
        }

        private void SetWantedLevelInternal(int newStars, string alertReason)
        {
            int previous = wantedStars;
            wantedStars = Mathf.Clamp(newStars, 0, MaxWantedStars);

            UpdateAllUnitsState();
            MaintainPoliceUnits();

            if (wantedStars != previous)
            {
                OnWantedStarsChanged?.Invoke(previous, wantedStars);
            }

            if (!string.IsNullOrEmpty(alertReason))
            {
                OnPoliceAlertTriggered?.Invoke(alertReason, wantedStars);
            }
        }

        /// <summary>
        /// Verifica se há alguma viatura policial próxima e com linha de visão livre da posição informada.
        /// </summary>
        public bool IsPoliceNearby(Vector3 worldPos, float radius, VehicleController ignoreVehicle = null, bool requireLineOfSight = true)
        {
            float radiusSq = radius * radius;

            // 1. Checa todas as unidades policiais gerenciadas
            for (int i = 0; i < activePoliceUnits.Count; i++)
            {
                PoliceVehicleAI unit = activePoliceUnits[i];
                if (unit == null || unit.Vehicle == null || unit.Vehicle == ignoreVehicle || unit.Vehicle.IsDestroyed)
                    continue;

                Vector3 diff = unit.transform.position - worldPos;
                diff.y = 0f;
                if (diff.sqrMagnitude <= radiusSq)
                {
                    if (!requireLineOfSight || HasClearLineOfSight(unit.transform.position, worldPos, unit.transform, ignoreVehicle))
                    {
                        return true;
                    }
                }
            }

            // 2. Checa também qualquer viatura policial presente no raio físico
            int hitCount = Physics.OverlapSphereNonAlloc(
                worldPos,
                radius,
                witnessHits,
                obstacleLayerMask,
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = witnessHits[i];
                if (col == null) continue;

                VehicleController vc = col.GetComponentInParent<VehicleController>();
                if (vc == null || vc == ignoreVehicle || vc.IsOccupied || vc.IsDestroyed)
                    continue;

                if (IsPoliceVehicleModel(vc))
                {
                    if (!requireLineOfSight || HasClearLineOfSight(vc.transform.position, worldPos, vc.transform, ignoreVehicle))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsPoliceTouchingPlayerOnFoot(Vector3 playerPos)
        {
            float radiusSq = bustedOnFootContactRadius * bustedOnFootContactRadius;

            for (int i = 0; i < activePoliceUnits.Count; i++)
            {
                PoliceVehicleAI unit = activePoliceUnits[i];
                if (unit == null || !unit.IsPursuing || unit.Vehicle == null)
                    continue;

                Vector3 diff = unit.transform.position - playerPos;
                diff.y = 0f;
                if (diff.sqrMagnitude <= radiusSq)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasClearLineOfSight(Vector3 fromPos, Vector3 toPos, Transform policeTransform, VehicleController ignoreVehicle)
        {
            Vector3 origin = fromPos + Vector3.up * 0.9f;
            Vector3 dest = toPos + Vector3.up * 0.9f;
            Vector3 diff = dest - origin;
            float dist = diff.magnitude;
            if (dist < 2.5f) return true;

            Vector3 dir = diff / dist;
            int hits = Physics.SphereCastNonAlloc(origin, 0.45f, dir, losHits, dist - 1.0f, obstacleLayerMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hits; i++)
            {
                Collider col = losHits[i].collider;
                if (col == null) continue;
                if (policeTransform != null && col.transform.IsChildOf(policeTransform)) continue;
                if (ignoreVehicle != null && col.transform.IsChildOf(ignoreVehicle.transform)) continue;
                if (player != null && col.transform.IsChildOf(player.transform)) continue;
                if (col.GetComponentInParent<VehicleController>() != null) continue;

                string n = col.gameObject.name;
                if (n == "Ground" || n.StartsWith("Road_", StringComparison.Ordinal) || n.StartsWith("SidewalkLot_", StringComparison.Ordinal))
                    continue;

                return false;
            }

            return true;
        }

        private void SubscribeEvents()
        {
            if (player != null)
            {
                player.OnVehicleEntered -= HandlePlayerVehicleEntered;
                player.OnVehicleEntered += HandlePlayerVehicleEntered;
                player.OnVehicleExited -= HandlePlayerVehicleExited;
                player.OnVehicleExited += HandlePlayerVehicleExited;
            }

            if (missionManager != null)
            {
                missionManager.OnContractCompleted -= HandleContractCompleted;
                missionManager.OnContractCompleted += HandleContractCompleted;
            }
        }

        private void UnsubscribeEvents()
        {
            if (player != null)
            {
                player.OnVehicleEntered -= HandlePlayerVehicleEntered;
                player.OnVehicleExited -= HandlePlayerVehicleExited;
            }

            if (missionManager != null)
            {
                missionManager.OnContractCompleted -= HandleContractCompleted;
            }
        }

        private void HandleContractCompleted(MissionCompletionResult result)
        {
            if (isBustedTriggered) return;

            // Concede janela de proteção pós-entrega e dispersa viaturas coladas na DeliveryZone
            postDeliveryGraceTimer = postDeliverySafeGraceSeconds;
            if (result.DeliveryZone != null)
            {
                DispersePoliceNearPosition(result.DeliveryZone.transform.position, 46f);
            }

            int deliveries = result.CompletedMissionsCount;
            bool isMilestoneDelivery = deliveries > 0 && deliveriesPerStarIncrease > 0 && (deliveries % deliveriesPerStarIncrease == 0);

            if (isMilestoneDelivery)
            {
                // A cada 3 entregas (3, 6, 9, 12, 15, 18), o nível de procurado sobe (alcançando até 6 estrelas)
                int milestoneStars = Mathf.Clamp(deliveries / deliveriesPerStarIncrease, 1, MaxWantedStars);
                int targetStars = Mathf.Clamp(Mathf.Max(wantedStars + 1, milestoneStars), 1, MaxWantedStars);

                SetWantedLevelInternal(
                    targetStars,
                    $"★ {deliveries} ENTREGAS CONCLUÍDAS! NOTORIEDADE SUBIU PARA {targetStars} ★"
                );
            }
            else if (wantedStars > 0)
            {
                // Nas demais entregas, o Desmanche esconde o jogador e reduz -1 estrela de procurado!
                int newStars = Mathf.Max(0, wantedStars - 1);
                string msg = newStars == 0
                    ? "★ DESMANCHE DESPISTOU A POLÍCIA! FICHA LIMPA (0 ★) ★"
                    : $"★ DESMANCHE ESCONDEU VOCÊ! PROCURADO REDUZIDO PARA {newStars} ★";

                DecreaseWantedLevel(1, msg);
            }
        }

        private void DispersePoliceNearPosition(Vector3 zonePos, float clearRadius)
        {
            float clearRadiusSq = clearRadius * clearRadius;

            for (int i = activePoliceUnits.Count - 1; i >= 0; i--)
            {
                PoliceVehicleAI unit = activePoliceUnits[i];
                if (unit == null || unit.Vehicle == null || unit.Vehicle.IsOccupied)
                    continue;

                if (missionManager != null && missionManager.CurrentTargetVehicle == unit.Vehicle)
                    continue;

                Vector3 diff = unit.transform.position - zonePos;
                diff.y = 0f;
                if (diff.sqrMagnitude <= clearRadiusSq)
                {
                    UnregisterPoliceUnitAt(i);
                    Destroy(unit.gameObject);
                }
            }
        }

        private void HandlePlayerVehicleEntered(VehicleController enteredVehicle)
        {
            if (isBustedTriggered || enteredVehicle == null)
                return;

            bool wasMovingCarjack = enteredVehicle.WasMovingOrAICarjackedOnLastEntry;
            if (!wasMovingCarjack)
                return;

            bool stolePoliceCarItself = IsPoliceVehicleModel(enteredVehicle);
            bool witnessedByNearbyPolice = IsPoliceNearby(
                enteredVehicle.transform.position,
                policeWitnessRadius,
                enteredVehicle,
                requireLineOfSight: true
            );

            if (stolePoliceCarItself || witnessedByNearbyPolice)
            {
                int nextStar = Mathf.Min(MaxWantedStars, wantedStars + 1);
                string alertMsg = stolePoliceCarItself
                    ? $"★ VIATURA POLICIAL ROUBADA! PROCURADO NÍVEL {nextStar} ★"
                    : $"★ POLÍCIA VIU O ROUBO EM MOVIMENTO! PROCURADO NÍVEL {nextStar} ★";

                IncreaseWantedLevel(1, alertMsg);
            }
        }

        private void HandlePlayerVehicleExited(VehicleController exitedVehicle)
        {
            if (isBustedTriggered || exitedVehicle == null)
                return;

            if (missionManager != null && missionManager.IsGameOver)
                return;

            // Ignora ejeção automática na DeliveryZone
            if (exitedVehicle.LastExitWasForced)
                return;

            // O jogador pode sair livremente do carro sem ser preso no ato da saída
            if (wantedStars > 0)
            {
                OnPoliceAlertTriggered?.Invoke("A PÉ E PROCURADO! ENTRE RÁPIDO EM UM VEÍCULO!", wantedStars);
            }
        }

        private void TriggerPlayerBusted(string reasonMessage)
        {
            if (isBustedTriggered) return;
            isBustedTriggered = true;

            OnPlayerBusted?.Invoke(reasonMessage);

            if (missionManager != null)
            {
                missionManager.TriggerCustomGameOver(GameOverReason.Busted, reasonMessage);
            }
        }

        private void AdoptInitialScenePoliceVehicles()
        {
            if (initialScenePoliceVehicles == null) return;

            for (int i = 0; i < initialScenePoliceVehicles.Count; i++)
            {
                VehicleController vc = initialScenePoliceVehicles[i];
                if (vc == null || vc.IsOccupied || vc.IsDestroyed)
                    continue;

                PoliceVehicleAI policeAI = vc.GetComponent<PoliceVehicleAI>();
                if (policeAI == null)
                {
                    policeAI = vc.gameObject.AddComponent<PoliceVehicleAI>();
                }

                if (activePoliceUnits.Contains(policeAI))
                    continue;

                TrafficWaypoint startWp = roadGraph != null
                    ? roadGraph.FindClosestAlignedWaypoint(vc.transform.position, vc.transform.forward, 20f)
                    : null;

                RegisterPoliceUnit(policeAI, startWp);
            }
        }

        private void MaintainPoliceUnits()
        {
            if (roadGraph == null || standardPolicePrefab == null)
                return;

            Vector3 refPos = GetPlayerReferencePosition();
            float relocateDistSq = relocateDistance * relocateDistance;

            // 1. Remove unidades destruídas/roubadas ou reposiciona viaturas que ficaram muito para trás
            for (int i = activePoliceUnits.Count - 1; i >= 0; i--)
            {
                PoliceVehicleAI unit = activePoliceUnits[i];
                if (unit == null || unit.Vehicle == null || unit.Vehicle.IsDestroyed || unit.Vehicle.IsOccupied)
                {
                    UnregisterPoliceUnitAt(i);
                    continue;
                }

                if (missionManager != null && missionManager.CurrentTargetVehicle == unit.Vehicle)
                {
                    continue;
                }

                Vector3 diff = unit.transform.position - refPos;
                diff.y = 0f;
                if (diff.sqrMagnitude > relocateDistSq)
                {
                    UnregisterPoliceUnitAt(i);
                    Destroy(unit.gameObject);
                }
            }

            // 2. Define quantas viaturas devem estar ativas:
            int targetUnitCount = wantedStars == 0
                ? patrolUnitsAtZeroStars
                : Mathf.Clamp(wantedStars, 1, MaxWantedStars);

            // Se caiu de nível (ex: reduziu para 1 estrela no Desmanche) e há mais viaturas perseguindo que o necessário,
            // converte as excedentes mais distantes para Patrulha ou remove
            if (wantedStars > 0 && activePoliceUnits.Count > targetUnitCount)
            {
                for (int i = activePoliceUnits.Count - 1; i >= targetUnitCount; i--)
                {
                    PoliceVehicleAI extraUnit = activePoliceUnits[i];
                    if (extraUnit != null && (missionManager == null || missionManager.CurrentTargetVehicle != extraUnit.Vehicle))
                    {
                        UnregisterPoliceUnitAt(i);
                        Destroy(extraUnit.gameObject);
                    }
                }
            }

            int maxSpawnsThisTick = 1;
            int spawned = 0;

            while (activePoliceUnits.Count < targetUnitCount && spawned < maxSpawnsThisTick)
            {
                if (!TrySpawnPoliceUnit(refPos))
                    break;

                spawned++;
            }
        }

        private bool TrySpawnPoliceUnit(Vector3 refPos)
        {
            if (!roadGraph.TryGetSpawnWaypoint(refPos, minSpawnDistance, maxSpawnDistance, spawnClearanceRadius, obstacleLayerMask, out TrafficWaypoint spawnWp))
            {
                if (!roadGraph.TryGetSpawnWaypoint(refPos, 24f, relocateDistance * 0.9f, spawnClearanceRadius * 0.8f, obstacleLayerMask, out spawnWp))
                    return false;
            }

            VehicleController prefabToSpawn = ChoosePolicePrefabForCurrentStars();
            if (prefabToSpawn == null)
                return false;

            Vector3 spawnPos = new Vector3(spawnWp.Position.x, 0.08f, spawnWp.Position.z);
            Quaternion spawnRot = Quaternion.LookRotation(spawnWp.ForwardDirection, Vector3.up);

            VehicleController instance = Instantiate(prefabToSpawn, spawnPos, spawnRot, transform);
            instance.name = wantedStars > 0
                ? $"{prefabToSpawn.name}_Pursuit_{wantedStars}Star"
                : $"{prefabToSpawn.name}_Patrol";

            PoliceVehicleAI policeAI = instance.GetComponent<PoliceVehicleAI>();
            if (policeAI == null)
            {
                policeAI = instance.gameObject.AddComponent<PoliceVehicleAI>();
            }

            RegisterPoliceUnit(policeAI, spawnWp);
            return true;
        }

        private VehicleController ChoosePolicePrefabForCurrentStars()
        {
            if (wantedStars >= 4 && heavyPolicePrefab != null && UnityEngine.Random.value < 0.38f)
            {
                return heavyPolicePrefab;
            }
            return standardPolicePrefab;
        }

        private void RegisterPoliceUnit(PoliceVehicleAI policeAI, TrafficWaypoint startWp)
        {
            if (policeAI == null) return;

            if (policeSirenClip != null)
            {
                policeAI.SetSirenClip(policeSirenClip);
            }

            policeAI.OnStolenByPlayer -= HandlePoliceUnitStolen;
            policeAI.OnStolenByPlayer += HandlePoliceUnitStolen;
            policeAI.OnDestroyed -= HandlePoliceUnitDestroyed;
            policeAI.OnDestroyed += HandlePoliceUnitDestroyed;

            PoliceAIState desiredState = wantedStars > 0 ? PoliceAIState.Pursuit : PoliceAIState.Patrol;
            policeAI.Initialize(player, roadGraph, startWp, desiredState, wantedStars);

            if (!activePoliceUnits.Contains(policeAI))
            {
                activePoliceUnits.Add(policeAI);
            }
        }

        private void UnregisterPoliceUnitAt(int index)
        {
            if (index < 0 || index >= activePoliceUnits.Count) return;

            PoliceVehicleAI unit = activePoliceUnits[index];
            if (unit != null)
            {
                unit.OnStolenByPlayer -= HandlePoliceUnitStolen;
                unit.OnDestroyed -= HandlePoliceUnitDestroyed;
            }
            activePoliceUnits.RemoveAt(index);
        }

        private void UpdateAllUnitsState()
        {
            PoliceAIState desiredState = wantedStars > 0 ? PoliceAIState.Pursuit : PoliceAIState.Patrol;
            for (int i = 0; i < activePoliceUnits.Count; i++)
            {
                PoliceVehicleAI unit = activePoliceUnits[i];
                if (unit != null && unit.Vehicle != null && !unit.Vehicle.IsOccupied && !unit.Vehicle.IsDestroyed)
                {
                    unit.SetState(desiredState, wantedStars);
                }
            }
        }

        private void HandlePoliceUnitStolen(PoliceVehicleAI stolenUnit)
        {
            if (stolenUnit == null) return;
            int idx = activePoliceUnits.IndexOf(stolenUnit);
            if (idx >= 0)
            {
                UnregisterPoliceUnitAt(idx);
            }
            stolenUnit.transform.SetParent(null, worldPositionStays: true);
        }

        private void HandlePoliceUnitDestroyed(PoliceVehicleAI destroyedUnit)
        {
            if (destroyedUnit == null) return;
            int idx = activePoliceUnits.IndexOf(destroyedUnit);
            if (idx >= 0)
            {
                UnregisterPoliceUnitAt(idx);
            }
        }

        private Vector3 GetPlayerReferencePosition()
        {
            if (player != null)
            {
                if (player.CurrentVehicle != null)
                    return player.CurrentVehicle.transform.position;
                return player.transform.position;
            }
            return transform.position;
        }

        private static bool IsPoliceVehicleModel(VehicleController vc)
        {
            if (vc == null) return false;
            string nameToCheck = vc.VehicleName;
            return !string.IsNullOrEmpty(nameToCheck) &&
                   nameToCheck.IndexOf("Police", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
