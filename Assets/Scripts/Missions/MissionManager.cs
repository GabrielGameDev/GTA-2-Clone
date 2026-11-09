using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using GTA2Clone.Player;
using GTA2Clone.Traffic;
using GTA2Clone.Vehicles;

namespace GTA2Clone.Missions
{
    /// <summary>
    /// Gerencia o Loop Infinito de Missões Arcade (roubo e entrega de veículos estilo GTA 2 / Crazy Taxi).
    /// - Banco de Tempo Global: inicia com initialTimeBankSeconds (60s), ganha tempo extra a cada entrega
    ///   (com escalada de dificuldade progressiva) e dispara Game Over quando o relógio chega a 00:00.
    /// - 1ª missão: sempre spawna um veículo parado próximo ao jogador.
    /// - Missões seguintes: sorteia entre veículo parado em vehicleSpawnPoints ou veículo rodando no tráfego.
    /// - Controla a seta direcional (Verde -> Carro Alvo, Amarela -> DeliveryZone).
    /// - Processa a entrega na DeliveryZone, computa recompensas, ejeta o jogador em segurança e inicia novo contrato.
    /// - Emite eventos desacoplados para UI e integração futura com o sistema de polícia/procurado (heat).
    /// </summary>
    public class MissionManager : MonoBehaviour
    {
        [Header("Core References")]
        [SerializeField] private PlayerController player;
        [SerializeField] private TrafficManager trafficManager;
        [SerializeField] private MissionTargetIndicator targetIndicator;

        [Header("Contracts & Locations")]
        [SerializeField] private List<VehicleContractData> availableContracts = new List<VehicleContractData>();
        [SerializeField] private Transform firstMissionSpawnPoint;
        [SerializeField] private List<Transform> vehicleSpawnPoints = new List<Transform>();
        [SerializeField] private List<DeliveryZone> deliveryZones = new List<DeliveryZone>();

        [Header("Global Time Bank (Crazy Taxi Arcade)")]
        [SerializeField] private float initialTimeBankSeconds = 60f;
        [SerializeField] private float maxTimeBankSeconds = 120f;
        [SerializeField] private float defaultTimeAwardPerDelivery = 25f;
        [SerializeField] private float timeAwardDecayPerDelivery = 1.5f;
        [SerializeField] private float minTimeAwardPerDelivery = 11f;

        [Header("Arcade Loop Settings")]
        [SerializeField, Range(0f, 1f)] private float trafficTargetChance = 0.5f;
        [SerializeField] private float minParkedSpawnDistanceFromPlayer = 22f;
        [SerializeField] private float minDeliveryDistanceFromTarget = 45f;
        [SerializeField] private float spawnClearanceRadius = 3.2f;
        [SerializeField] private LayerMask obstacleLayerMask = ~0;

        private VehicleContractData currentContract;
        private VehicleController currentTargetVehicle;
        private DeliveryZone activeDeliveryZone;
        private MissionPhase currentPhase = MissionPhase.None;

        private int currentMissionNumber;
        private int completedMissionsCount;
        private int totalMoney;
        private int accumulatedHeat;
        private float remainingTimeSeconds;
        private float totalSurvivedSeconds;
        private bool isGameOver;
        private bool currentMissionSpawnedInTraffic;
        private bool hasTriggeredStealHeatForCurrentMission;
        private float contractStealTimestamp;
        private bool isProcessingDelivery;

        private readonly Collider[] clearanceHits = new Collider[12];
        private readonly List<Transform> candidateSpawnPoints = new List<Transform>(16);
        private readonly List<DeliveryZone> candidateDeliveryZones = new List<DeliveryZone>(8);

        public VehicleContractData CurrentContract => currentContract;
        public VehicleController CurrentTargetVehicle => currentTargetVehicle;
        public DeliveryZone ActiveDeliveryZone => activeDeliveryZone;
        public MissionPhase CurrentPhase => currentPhase;
        public int CurrentMissionNumber => currentMissionNumber;
        public int CompletedMissionsCount => completedMissionsCount;
        public int TotalMoney => totalMoney;
        public int AccumulatedHeat => accumulatedHeat;
        public float RemainingTimeSeconds => remainingTimeSeconds;
        public float TotalSurvivedSeconds => totalSurvivedSeconds;
        public bool IsGameOver => isGameOver;

        /// <summary>
        /// Disparado sempre que um novo contrato é sorteado e seu veículo alvo está disponível no mapa.
        /// </summary>
        public event Action<ActiveMissionInfo> OnContractStarted;

        /// <summary>
        /// Disparado quando a missão muda de fase (ex: de StealVehicle para DeliverVehicle ou vice-versa).
        /// </summary>
        public event Action<ActiveMissionInfo> OnMissionPhaseChanged;

        /// <summary>
        /// Disparado quando o jogador entra no veículo alvo da missão.
        /// </summary>
        public event Action<ActiveMissionInfo, VehicleController> OnTargetVehicleStolen;

        /// <summary>
        /// Disparado quando o jogador sai do veículo alvo antes de entregá-lo na DeliveryZone.
        /// </summary>
        public event Action<ActiveMissionInfo, VehicleController> OnTargetVehicleAbandoned;

        /// <summary>
        /// Disparado quando o veículo alvo é entregue com sucesso na DeliveryZone.
        /// </summary>
        public event Action<MissionCompletionResult> OnContractCompleted;

        /// <summary>
        /// Disparado a cada frame com o tempo restante no Banco de Tempo Global.
        /// </summary>
        public event Action<float> OnTimerUpdated;

        /// <summary>
        /// Disparado quando o Banco de Tempo Global chega a zero (Game Over).
        /// </summary>
        public event Action<GameOverResult> OnGameOver;

        /// <summary>
        /// Disparado para integração futura com o sistema de polícia/procurado (heat).
        /// </summary>
        public event Action<HeatEventPayload> OnHeatGenerated;

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        private void Start()
        {
            isGameOver = false;
            remainingTimeSeconds = initialTimeBankSeconds;
            totalSurvivedSeconds = 0f;
            DeactivateAllDeliveryZones();
            OnTimerUpdated?.Invoke(remainingTimeSeconds);
            StartNextContract();
        }

        private void Update()
        {
            if (isGameOver)
                return;

            if (currentPhase != MissionPhase.None && currentPhase != MissionPhase.GameOver)
            {
                float dt = Time.deltaTime;
                totalSurvivedSeconds += dt;
                remainingTimeSeconds -= dt;

                if (remainingTimeSeconds <= 0f)
                {
                    remainingTimeSeconds = 0f;
                    OnTimerUpdated?.Invoke(0f);
                    TriggerGameOver();
                    return;
                }

                OnTimerUpdated?.Invoke(remainingTimeSeconds);
            }

            // Recuperação defensiva caso o veículo alvo tenha sido destruído externamente (sem o jogador dentro)
            if (!isProcessingDelivery && currentPhase != MissionPhase.None && currentPhase != MissionPhase.GameOver &&
                (currentTargetVehicle == null || (currentTargetVehicle.IsDestroyed && !currentTargetVehicle.IsOccupied)))
            {
                StartNextContract();
            }
        }

        /// <summary>
        /// Configura todas as referências do MissionManager (utilizado pelas ferramentas de Editor).
        /// </summary>
        public void Configure(
            PlayerController targetPlayer,
            TrafficManager targetTrafficManager,
            MissionTargetIndicator indicator,
            List<VehicleContractData> contracts,
            Transform firstSpawnPoint,
            List<Transform> spawnPoints,
            List<DeliveryZone> zones)
        {
            player = targetPlayer;
            trafficManager = targetTrafficManager;
            targetIndicator = indicator;
            availableContracts = contracts ?? new List<VehicleContractData>();
            firstMissionSpawnPoint = firstSpawnPoint;
            vehicleSpawnPoints = spawnPoints ?? new List<Transform>();
            deliveryZones = zones ?? new List<DeliveryZone>();
        }

        /// <summary>
        /// Reinicia a partida arcade recarregando a cena atual.
        /// </summary>
        public void RestartGame()
        {
            Time.timeScale = 1f;
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.buildIndex);
        }

        /// <summary>
        /// Sorteia e inicia imediatamente o próximo contrato do loop infinito.
        /// A primeira missão (currentMissionNumber == 1) sempre spawna um veículo parado próximo ao jogador.
        /// </summary>
        public void StartNextContract()
        {
            if (isGameOver)
                return;

            if (availableContracts == null || availableContracts.Count == 0)
            {
                Debug.LogWarning("[MissionManager] Nenhum VehicleContractData configurado em availableContracts.");
                return;
            }

            DeactivateAllDeliveryZones();

            currentMissionNumber++;
            bool isFirstMission = currentMissionNumber == 1;

            currentContract = ChooseRandomContract(currentContract);
            if (currentContract == null || currentContract.TargetVehiclePrefab == null)
            {
                Debug.LogWarning("[MissionManager] Contrato sorteado inválido ou sem TargetVehiclePrefab.");
                return;
            }

            currentTargetVehicle = SpawnOrSelectTargetVehicle(currentContract, isFirstMission, out currentMissionSpawnedInTraffic);
            if (currentTargetVehicle == null)
            {
                Debug.LogWarning("[MissionManager] Não foi possível criar ou localizar o veículo alvo da missão.");
                return;
            }

            if (trafficManager != null)
            {
                trafficManager.SetProtectedMissionVehicle(currentTargetVehicle);
            }

            activeDeliveryZone = ChooseDeliveryZoneForTarget(currentTargetVehicle.transform.position);
            hasTriggeredStealHeatForCurrentMission = false;
            contractStealTimestamp = Time.time;

            // Se por acaso o jogador já estiver dentro do veículo alvo, vai direto para a fase de entrega
            if (player != null && player.CurrentVehicle == currentTargetVehicle)
            {
                AdvanceToDeliveryPhase();
            }
            else
            {
                currentPhase = MissionPhase.StealVehicle;
                if (targetIndicator != null)
                {
                    targetIndicator.SetTarget(currentTargetVehicle.transform, MissionPhase.StealVehicle);
                }
            }

            ActiveMissionInfo info = BuildActiveMissionInfo();
            OnContractStarted?.Invoke(info);
            OnMissionPhaseChanged?.Invoke(info);
        }

        /// <summary>
        /// Encerra a partida com um motivo específico (Tempo Esgotado, Preso pela Polícia ou Veículo Destruído).
        /// </summary>
        public void TriggerCustomGameOver(GameOverReason reason, string customMessage = null)
        {
            if (isGameOver) return;

            isGameOver = true;
            currentPhase = MissionPhase.GameOver;

            DeactivateAllDeliveryZones();

            if (targetIndicator != null)
            {
                targetIndicator.ClearTarget();
            }

            // Para e bloqueia os controles do jogador/veículo ao encerrar a partida
            if (player != null)
            {
                if (player.CurrentVehicle != null)
                {
                    player.CurrentVehicle.ForceExitVehicle();
                }
                player.enabled = false;
            }

            GameOverResult result = new GameOverResult(
                totalMoney,
                completedMissionsCount,
                totalSurvivedSeconds,
                accumulatedHeat,
                reason,
                customMessage
            );

            OnGameOver?.Invoke(result);
        }

        private void TriggerGameOver()
        {
            TriggerCustomGameOver(GameOverReason.TimeExpired, "O TEMPO DO RELÓGIO ESGOTOU!");
        }

        private void SubscribeToEvents()
        {
            VehicleHealth.OnAnyVehicleExploded -= HandleAnyVehicleExploded;
            VehicleHealth.OnAnyVehicleExploded += HandleAnyVehicleExploded;

            if (player != null)
            {
                player.OnVehicleEntered -= HandlePlayerVehicleEntered;
                player.OnVehicleEntered += HandlePlayerVehicleEntered;
                player.OnVehicleExited -= HandlePlayerVehicleExited;
                player.OnVehicleExited += HandlePlayerVehicleExited;
            }

            if (deliveryZones != null)
            {
                for (int i = 0; i < deliveryZones.Count; i++)
                {
                    DeliveryZone zone = deliveryZones[i];
                    if (zone == null) continue;
                    zone.OnVehicleDelivered -= HandleVehicleDelivered;
                    zone.OnVehicleDelivered += HandleVehicleDelivered;
                }
            }
        }

        private void UnsubscribeFromEvents()
        {
            VehicleHealth.OnAnyVehicleExploded -= HandleAnyVehicleExploded;

            if (player != null)
            {
                player.OnVehicleEntered -= HandlePlayerVehicleEntered;
                player.OnVehicleExited -= HandlePlayerVehicleExited;
            }

            if (deliveryZones != null)
            {
                for (int i = 0; i < deliveryZones.Count; i++)
                {
                    DeliveryZone zone = deliveryZones[i];
                    if (zone == null) continue;
                    zone.OnVehicleDelivered -= HandleVehicleDelivered;
                }
            }
        }

        private void HandleAnyVehicleExploded(VehicleController explodedVehicle, bool wasOccupiedByPlayer)
        {
            if (isGameOver || explodedVehicle == null)
                return;

            if (wasOccupiedByPlayer || (player != null && player.CurrentVehicle == explodedVehicle))
            {
                TriggerCustomGameOver(
                    GameOverReason.VehicleDestroyed,
                    $"SEU VEÍCULO ({explodedVehicle.VehicleName.ToUpperInvariant()}) PERDEU TODA A VIDA E EXPLODIU!"
                );
            }
        }

        private void HandlePlayerVehicleEntered(VehicleController enteredVehicle)
        {
            if (isGameOver || enteredVehicle == null || currentPhase == MissionPhase.None)
                return;

            if (enteredVehicle == currentTargetVehicle)
            {
                AdvanceToDeliveryPhase();
            }
        }

        private void HandlePlayerVehicleExited(VehicleController exitedVehicle)
        {
            if (isGameOver || isProcessingDelivery || currentPhase == MissionPhase.None)
                return;

            // Se o jogador saiu do carro alvo antes de entregá-lo, volta para a fase StealVehicle (seta verde no carro)
            if (exitedVehicle != null && exitedVehicle == currentTargetVehicle && currentPhase == MissionPhase.DeliverVehicle)
            {
                currentPhase = MissionPhase.StealVehicle;

                if (activeDeliveryZone != null)
                {
                    activeDeliveryZone.DeactivateZone();
                }

                if (targetIndicator != null && currentTargetVehicle != null)
                {
                    targetIndicator.SetTarget(currentTargetVehicle.transform, MissionPhase.StealVehicle);
                }

                ActiveMissionInfo info = BuildActiveMissionInfo();
                OnTargetVehicleAbandoned?.Invoke(info, currentTargetVehicle);
                OnMissionPhaseChanged?.Invoke(info);
            }
        }

        private void AdvanceToDeliveryPhase()
        {
            if (isGameOver || currentTargetVehicle == null)
                return;

            if (activeDeliveryZone == null)
            {
                activeDeliveryZone = ChooseDeliveryZoneForTarget(currentTargetVehicle.transform.position);
            }

            currentPhase = MissionPhase.DeliverVehicle;

            if (activeDeliveryZone != null)
            {
                activeDeliveryZone.ActivateForVehicle(currentTargetVehicle);
            }

            if (targetIndicator != null && activeDeliveryZone != null)
            {
                targetIndicator.SetTarget(activeDeliveryZone.transform, MissionPhase.DeliverVehicle);
            }

            ActiveMissionInfo info = BuildActiveMissionInfo();
            OnTargetVehicleStolen?.Invoke(info, currentTargetVehicle);
            OnMissionPhaseChanged?.Invoke(info);

            // Emite evento de Heat apenas na primeira vez que o veículo alvo deste contrato é roubado
            if (!hasTriggeredStealHeatForCurrentMission)
            {
                hasTriggeredStealHeatForCurrentMission = true;
                contractStealTimestamp = Time.time;

                int stealHeat = currentContract != null ? currentContract.HeatOnSteal : 1;
                if (stealHeat != 0)
                {
                    EmitHeatEvent(
                        stealHeat,
                        HeatSourceType.MissionVehicleStolen,
                        currentTargetVehicle.transform.position,
                        currentTargetVehicle,
                        currentContract
                    );
                }
            }
        }

        private void HandleVehicleDelivered(DeliveryZone zone, VehicleController deliveredVehicle)
        {
            if (isGameOver || isProcessingDelivery || deliveredVehicle == null || deliveredVehicle != currentTargetVehicle)
                return;

            isProcessingDelivery = true;

            // 1. Computa a pontuação / recompensa financeira do contrato
            int baseReward = currentContract != null ? currentContract.BaseReward : 2000;
            float elapsedSeconds = Mathf.Max(0.1f, Time.time - contractStealTimestamp);
            int timeBonus = CalculateTimeBonus(currentContract, elapsedSeconds);
            int totalReward = baseReward + timeBonus;

            // 2. Computa o tempo extra adicionado ao Banco de Tempo Global (com escalada de dificuldade)
            float addedTimeSeconds = CalculateDeliveryTimeAward(currentContract, completedMissionsCount);
            remainingTimeSeconds = Mathf.Min(maxTimeBankSeconds, remainingTimeSeconds + addedTimeSeconds);
            OnTimerUpdated?.Invoke(remainingTimeSeconds);

            totalMoney += totalReward;
            completedMissionsCount++;

            // 3. Ejeta o jogador com segurança no ponto de saída da DeliveryZone
            if (deliveredVehicle.IsOccupied)
            {
                zone.GetSafeEjectPose(deliveredVehicle, out Vector3 safePos, out Quaternion safeRot);
                deliveredVehicle.ForceExitVehicle(safePos, safeRot);
            }

            // 4. Emite evento de Heat na entrega caso configurado no contrato
            if (currentContract != null && currentContract.HeatOnDelivery != 0)
            {
                EmitHeatEvent(
                    currentContract.HeatOnDelivery,
                    HeatSourceType.MissionVehicleDelivered,
                    zone.transform.position,
                    deliveredVehicle,
                    currentContract
                );
            }

            // 5. Desregistra e remove o veículo entregue da cena
            if (trafficManager != null)
            {
                trafficManager.UnregisterVehicle(deliveredVehicle);
            }

            currentTargetVehicle = null;
            currentPhase = MissionPhase.None;

            Destroy(deliveredVehicle.gameObject);

            // 6. Notifica os ouvintes (UI, áudio, estatísticas) sobre a conclusão da missão
            MissionCompletionResult result = new MissionCompletionResult(
                currentMissionNumber,
                currentContract,
                zone,
                baseReward,
                timeBonus,
                totalReward,
                totalMoney,
                completedMissionsCount,
                elapsedSeconds,
                addedTimeSeconds,
                remainingTimeSeconds
            );
            OnContractCompleted?.Invoke(result);

            isProcessingDelivery = false;

            // 7. Inicia um novo contrato imediatamente (Arcade Loop infinito)
            StartNextContract();
        }

        private float CalculateDeliveryTimeAward(VehicleContractData contract, int deliveriesCompletedBeforeThis)
        {
            float baseAward = contract != null && contract.BaseTimeAwardSeconds > 1f
                ? contract.BaseTimeAwardSeconds
                : defaultTimeAwardPerDelivery;

            float decayedAward = baseAward - (deliveriesCompletedBeforeThis * timeAwardDecayPerDelivery);
            return Mathf.Max(minTimeAwardPerDelivery, Mathf.Round(decayedAward));
        }

        private int CalculateTimeBonus(VehicleContractData contract, float elapsedSeconds)
        {
            if (contract == null || contract.MaxSpeedBonus <= 0 || contract.TargetDeliveryTimeSeconds <= 1f)
                return 0;

            if (elapsedSeconds >= contract.TargetDeliveryTimeSeconds)
                return 0;

            float factor = Mathf.Clamp01(1f - (elapsedSeconds / contract.TargetDeliveryTimeSeconds));
            int rawBonus = Mathf.RoundToInt(contract.MaxSpeedBonus * factor);
            // Arredonda para múltiplos de 50 para estética arcade clássica
            return (rawBonus / 50) * 50;
        }

        private void EmitHeatEvent(
            int heatDelta,
            HeatSourceType source,
            Vector3 worldPos,
            VehicleController vehicle,
            VehicleContractData contract)
        {
            accumulatedHeat = Mathf.Max(0, accumulatedHeat + heatDelta);
            HeatEventPayload payload = new HeatEventPayload(
                heatDelta,
                accumulatedHeat,
                source,
                worldPos,
                vehicle,
                contract
            );
            OnHeatGenerated?.Invoke(payload);
        }

        private VehicleContractData ChooseRandomContract(VehicleContractData previous)
        {
            if (availableContracts == null || availableContracts.Count == 0)
                return null;

            if (availableContracts.Count == 1)
                return availableContracts[0];

            int attempts = 8;
            for (int i = 0; i < attempts; i++)
            {
                VehicleContractData candidate = availableContracts[UnityEngine.Random.Range(0, availableContracts.Count)];
                if (candidate != null && candidate != previous)
                {
                    return candidate;
                }
            }

            return availableContracts[UnityEngine.Random.Range(0, availableContracts.Count)];
        }

        private VehicleController SpawnOrSelectTargetVehicle(
            VehicleContractData contract,
            bool isFirstMission,
            out bool spawnedInTraffic)
        {
            spawnedInTraffic = false;
            Vector3 playerRefPos = GetPlayerReferencePosition();

            // Regra 1: A primeira missão sempre é um veículo parado próximo ao jogador
            if (isFirstMission)
            {
                Transform nearPoint = GetFirstMissionSpawnTransform(playerRefPos);
                Vector3 spawnPos = nearPoint != null
                    ? new Vector3(nearPoint.position.x, 0.08f, nearPoint.position.z)
                    : playerRefPos + new Vector3(-5.0f, 0f, -1.65f);
                Quaternion spawnRot = nearPoint != null
                    ? nearPoint.rotation
                    : Quaternion.Euler(0f, 90f, 0f);

                ClearBlockingUnoccupiedVehiclesAt(spawnPos, 2.8f);
                return InstantiateParkedTargetVehicle(contract.TargetVehiclePrefab, spawnPos, spawnRot);
            }

            // Regra 2: Nas missões seguintes, sorteia entre veículo rodando no tráfego ou ponto fixo do mapa (vehicleSpawnPoints)
            bool tryTraffic = contract.AllowTrafficSpawn &&
                              trafficManager != null &&
                              UnityEngine.Random.value <= trafficTargetChance;

            if (tryTraffic)
            {
                if (trafficManager.TryGetOrSpawnTrafficTarget(
                        contract.TargetVehicleData,
                        contract.TargetVehiclePrefab,
                        playerRefPos,
                        out VehicleController trafficTarget))
                {
                    spawnedInTraffic = true;
                    return trafficTarget;
                }
            }

            // Spawn estacionado em um dos vehicleSpawnPoints do mapa
            Transform chosenSpawnPoint = ChooseParkedSpawnPoint(playerRefPos);
            if (chosenSpawnPoint != null)
            {
                Vector3 pos = new Vector3(chosenSpawnPoint.position.x, 0.08f, chosenSpawnPoint.position.z);
                ClearBlockingUnoccupiedVehiclesAt(pos, 2.5f);
                return InstantiateParkedTargetVehicle(contract.TargetVehiclePrefab, pos, chosenSpawnPoint.rotation);
            }

            // Fallback: se todos os pontos fixos estiverem ocupados, tenta usar o TrafficManager
            if (trafficManager != null &&
                trafficManager.TryGetOrSpawnTrafficTarget(
                    contract.TargetVehicleData,
                    contract.TargetVehiclePrefab,
                    playerRefPos,
                    out VehicleController fallbackTrafficTarget))
            {
                spawnedInTraffic = true;
                return fallbackTrafficTarget;
            }

            Vector3 fallbackPos = playerRefPos + new Vector3(20f, 0f, 0f);
            fallbackPos.y = 0.08f;
            return InstantiateParkedTargetVehicle(contract.TargetVehiclePrefab, fallbackPos, Quaternion.identity);
        }

        private VehicleController InstantiateParkedTargetVehicle(
            VehicleController prefab,
            Vector3 position,
            Quaternion rotation)
        {
            if (prefab == null) return null;

            VehicleController instance = Instantiate(prefab, position, rotation);
            instance.name = $"{prefab.name}_MissionTarget";

            // Garante que o veículo fique estacionado/parado aguardando o roubo pelo jogador
            TrafficVehicleAI ai = instance.GetComponent<TrafficVehicleAI>();
            if (ai != null)
            {
                ai.enabled = false;
            }
            instance.ClearAIInput();

            Rigidbody rb = instance.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            return instance;
        }

        private Transform GetFirstMissionSpawnTransform(Vector3 playerRefPos)
        {
            if (firstMissionSpawnPoint != null)
            {
                return firstMissionSpawnPoint;
            }

            if (vehicleSpawnPoints == null || vehicleSpawnPoints.Count == 0)
                return null;

            Transform closest = null;
            float closestDistSq = float.MaxValue;

            for (int i = 0; i < vehicleSpawnPoints.Count; i++)
            {
                Transform pt = vehicleSpawnPoints[i];
                if (pt == null) continue;

                Vector3 diff = pt.position - playerRefPos;
                diff.y = 0f;
                float distSq = diff.sqrMagnitude;
                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    closest = pt;
                }
            }

            return closest;
        }

        private Transform ChooseParkedSpawnPoint(Vector3 playerRefPos)
        {
            if (vehicleSpawnPoints == null || vehicleSpawnPoints.Count == 0)
                return firstMissionSpawnPoint;

            float minDistSq = minParkedSpawnDistanceFromPlayer * minParkedSpawnDistanceFromPlayer;
            candidateSpawnPoints.Clear();

            for (int i = 0; i < vehicleSpawnPoints.Count; i++)
            {
                Transform pt = vehicleSpawnPoints[i];
                if (pt == null) continue;

                Vector3 diff = pt.position - playerRefPos;
                diff.y = 0f;
                if (diff.sqrMagnitude >= minDistSq && IsSpawnAreaClear(pt.position))
                {
                    candidateSpawnPoints.Add(pt);
                }
            }

            if (candidateSpawnPoints.Count == 0)
            {
                for (int i = 0; i < vehicleSpawnPoints.Count; i++)
                {
                    Transform pt = vehicleSpawnPoints[i];
                    if (pt == null) continue;

                    Vector3 diff = pt.position - playerRefPos;
                    diff.y = 0f;
                    if (diff.sqrMagnitude >= 100f)
                    {
                        candidateSpawnPoints.Add(pt);
                    }
                }
            }

            if (candidateSpawnPoints.Count > 0)
            {
                return candidateSpawnPoints[UnityEngine.Random.Range(0, candidateSpawnPoints.Count)];
            }

            return vehicleSpawnPoints[UnityEngine.Random.Range(0, vehicleSpawnPoints.Count)];
        }

        private DeliveryZone ChooseDeliveryZoneForTarget(Vector3 targetVehiclePos)
        {
            if (deliveryZones == null || deliveryZones.Count == 0)
                return null;

            float minDistSq = minDeliveryDistanceFromTarget * minDeliveryDistanceFromTarget;
            candidateDeliveryZones.Clear();

            for (int i = 0; i < deliveryZones.Count; i++)
            {
                DeliveryZone zone = deliveryZones[i];
                if (zone == null) continue;

                Vector3 diff = zone.transform.position - targetVehiclePos;
                diff.y = 0f;
                if (diff.sqrMagnitude >= minDistSq)
                {
                    candidateDeliveryZones.Add(zone);
                }
            }

            if (candidateDeliveryZones.Count > 0)
            {
                return candidateDeliveryZones[UnityEngine.Random.Range(0, candidateDeliveryZones.Count)];
            }

            // Fallback: escolhe a DeliveryZone mais distante da posição atual do veículo alvo
            DeliveryZone farthestZone = deliveryZones[0];
            float maxDistSq = -1f;

            for (int i = 0; i < deliveryZones.Count; i++)
            {
                DeliveryZone zone = deliveryZones[i];
                if (zone == null) continue;

                Vector3 diff = zone.transform.position - targetVehiclePos;
                diff.y = 0f;
                float distSq = diff.sqrMagnitude;
                if (distSq > maxDistSq)
                {
                    maxDistSq = distSq;
                    farthestZone = zone;
                }
            }

            return farthestZone;
        }

        private bool IsSpawnAreaClear(Vector3 position)
        {
            Vector3 checkPos = position + Vector3.up * 0.75f;
            int count = Physics.OverlapSphereNonAlloc(
                checkPos,
                spawnClearanceRadius,
                clearanceHits,
                obstacleLayerMask,
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < count; i++)
            {
                Collider col = clearanceHits[i];
                if (col == null) continue;
                if (col.gameObject.name == "Ground" || col.gameObject.name.StartsWith("Road_") || col.gameObject.name.StartsWith("SidewalkLot_"))
                    continue;

                return false;
            }

            return true;
        }

        private void ClearBlockingUnoccupiedVehiclesAt(Vector3 position, float radius)
        {
            Vector3 checkPos = position + Vector3.up * 0.75f;
            int count = Physics.OverlapSphereNonAlloc(
                checkPos,
                radius,
                clearanceHits,
                obstacleLayerMask,
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < count; i++)
            {
                Collider col = clearanceHits[i];
                if (col == null) continue;

                VehicleController existingVehicle = col.GetComponentInParent<VehicleController>();
                if (existingVehicle != null && !existingVehicle.IsOccupied && existingVehicle != currentTargetVehicle)
                {
                    if (trafficManager != null)
                    {
                        trafficManager.UnregisterVehicle(existingVehicle);
                    }
                    Destroy(existingVehicle.gameObject);
                }
            }
        }

        private void DeactivateAllDeliveryZones()
        {
            if (deliveryZones == null) return;

            for (int i = 0; i < deliveryZones.Count; i++)
            {
                if (deliveryZones[i] != null)
                {
                    deliveryZones[i].DeactivateZone();
                }
            }
        }

        private Vector3 GetPlayerReferencePosition()
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

        private ActiveMissionInfo BuildActiveMissionInfo()
        {
            return new ActiveMissionInfo(
                currentMissionNumber,
                currentContract,
                currentTargetVehicle,
                activeDeliveryZone,
                currentPhase,
                currentMissionSpawnedInTraffic
            );
        }
    }
}
