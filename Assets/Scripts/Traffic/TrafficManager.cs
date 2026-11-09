using System.Collections.Generic;
using UnityEngine;
using GTA2Clone.Player;
using GTA2Clone.Vehicles;

namespace GTA2Clone.Traffic
{
    /// <summary>
    /// Gerencia o spawn, despawn e densidade de veículos autônomos de tráfego ao redor do jogador.
    /// </summary>
    public class TrafficManager : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private PlayerController player;
        [SerializeField] private TrafficRoadGraph roadGraph;
        [SerializeField] private List<VehicleController> vehiclePrefabs = new List<VehicleController>();
        [SerializeField] private List<TrafficVehicleAI> initialSceneVehicles = new List<TrafficVehicleAI>();

        [Header("Population & Distances")]
        [SerializeField] private int maxActiveVehicles = 16;
        [SerializeField] private float minInitialSpawnDistance = 18f;
        [SerializeField] private float minSpawnDistance = 36f;
        [SerializeField] private float maxSpawnDistance = 95f;
        [SerializeField] private float despawnDistance = 115f;
        [SerializeField] private float spawnClearanceRadius = 4.5f;
        [SerializeField] private float maintenanceInterval = 0.35f;
        [SerializeField] private LayerMask obstacleLayerMask = ~0;

        private readonly List<TrafficVehicleAI> activeTrafficVehicles = new List<TrafficVehicleAI>(32);
        private readonly List<VehicleController> stolenVehicles = new List<VehicleController>(8);
        private float maintenanceTimer;

        public int ActiveTrafficCount => activeTrafficVehicles.Count;

        private void Start()
        {
            AdoptInitialSceneVehicles();
            PopulateInitialTraffic();
        }

        private void Update()
        {
            maintenanceTimer += Time.deltaTime;
            if (maintenanceTimer >= maintenanceInterval)
            {
                maintenanceTimer = 0f;
                MaintainTrafficPopulation();
            }
        }

        /// <summary>
        /// Configura as referências do TrafficManager (utilizado pelas ferramentas de Editor).
        /// </summary>
        public void Configure(
            PlayerController targetPlayer,
            TrafficRoadGraph targetGraph,
            List<VehicleController> prefabs,
            List<TrafficVehicleAI> sceneVehicles = null)
        {
            player = targetPlayer;
            roadGraph = targetGraph;
            vehiclePrefabs = prefabs ?? new List<VehicleController>();
            initialSceneVehicles = sceneVehicles ?? new List<TrafficVehicleAI>();
        }

        private void AdoptInitialSceneVehicles()
        {
            if (roadGraph == null || initialSceneVehicles == null)
                return;

            for (int i = 0; i < initialSceneVehicles.Count; i++)
            {
                TrafficVehicleAI sceneAI = initialSceneVehicles[i];
                if (sceneAI == null || activeTrafficVehicles.Contains(sceneAI))
                    continue;

                if (sceneAI.Vehicle != null && sceneAI.Vehicle.IsOccupied)
                    continue;

                TrafficWaypoint wp = roadGraph.FindClosestAlignedWaypoint(
                    sceneAI.transform.position,
                    sceneAI.transform.forward,
                    maxDistance: 12f
                );

                if (wp != null)
                {
                    sceneAI.OnStolenByPlayer -= HandleTrafficVehicleStolen;
                    sceneAI.OnStolenByPlayer += HandleTrafficVehicleStolen;
                    sceneAI.InitializeRoute(wp, snapToWaypoint: false);
                    activeTrafficVehicles.Add(sceneAI);
                }
            }
        }

        private void PopulateInitialTraffic()
        {
            if (roadGraph == null || vehiclePrefabs == null || vehiclePrefabs.Count == 0)
                return;

            Vector3 refPos = GetReferencePosition();
            int attempts = maxActiveVehicles * 3;

            for (int i = 0; i < attempts && activeTrafficVehicles.Count < maxActiveVehicles; i++)
            {
                TrySpawnTrafficVehicle(refPos, minInitialSpawnDistance, maxSpawnDistance);
            }
        }

        private void MaintainTrafficPopulation()
        {
            if (roadGraph == null || vehiclePrefabs == null || vehiclePrefabs.Count == 0)
                return;

            Vector3 refPos = GetReferencePosition();
            float despawnDistSq = despawnDistance * despawnDistance;

            // 1. Remove veículos de IA que ficaram muito longe da câmera/jogador
            for (int i = activeTrafficVehicles.Count - 1; i >= 0; i--)
            {
                TrafficVehicleAI aiVehicle = activeTrafficVehicles[i];
                if (aiVehicle == null)
                {
                    activeTrafficVehicles.RemoveAt(i);
                    continue;
                }

                Vector3 diff = aiVehicle.transform.position - refPos;
                diff.y = 0f;
                if (diff.sqrMagnitude > despawnDistSq)
                {
                    aiVehicle.OnStolenByPlayer -= HandleTrafficVehicleStolen;
                    activeTrafficVehicles.RemoveAt(i);
                    Destroy(aiVehicle.gameObject);
                }
            }

            // 2. Limpa veículos roubados que foram abandonados muito longe do jogador
            for (int i = stolenVehicles.Count - 1; i >= 0; i--)
            {
                VehicleController stolen = stolenVehicles[i];
                if (stolen == null)
                {
                    stolenVehicles.RemoveAt(i);
                    continue;
                }

                if (stolen.IsOccupied)
                    continue;

                Vector3 diff = stolen.transform.position - refPos;
                diff.y = 0f;
                if (diff.sqrMagnitude > despawnDistSq)
                {
                    stolenVehicles.RemoveAt(i);
                    Destroy(stolen.gameObject);
                }
            }

            // 3. Spawna novos veículos no anel externo ao redor do jogador
            int maxSpawnsPerTick = 2;
            int spawnedThisTick = 0;

            while (activeTrafficVehicles.Count < maxActiveVehicles && spawnedThisTick < maxSpawnsPerTick)
            {
                if (!TrySpawnTrafficVehicle(refPos, minSpawnDistance, maxSpawnDistance))
                    break;

                spawnedThisTick++;
            }
        }

        private bool TrySpawnTrafficVehicle(Vector3 refPos, float minDist, float maxDist)
        {
            if (!roadGraph.TryGetSpawnWaypoint(refPos, minDist, maxDist, spawnClearanceRadius, obstacleLayerMask, out TrafficWaypoint spawnWp))
                return false;

            VehicleController prefab = ChooseRandomVehiclePrefab();
            if (prefab == null)
                return false;

            Quaternion spawnRot = Quaternion.LookRotation(spawnWp.ForwardDirection, Vector3.up);
            Vector3 spawnPos = new Vector3(spawnWp.Position.x, 0.08f, spawnWp.Position.z);

            VehicleController instance = Instantiate(prefab, spawnPos, spawnRot, transform);
            instance.name = $"{prefab.name}_Traffic";

            TrafficVehicleAI ai = instance.GetComponent<TrafficVehicleAI>();
            if (ai == null)
            {
                ai = instance.gameObject.AddComponent<TrafficVehicleAI>();
            }

            ai.OnStolenByPlayer += HandleTrafficVehicleStolen;
            ai.InitializeRoute(spawnWp);

            activeTrafficVehicles.Add(ai);
            return true;
        }

        private VehicleController ChooseRandomVehiclePrefab()
        {
            if (vehiclePrefabs == null || vehiclePrefabs.Count == 0)
                return null;

            int index = Random.Range(0, vehiclePrefabs.Count);
            return vehiclePrefabs[index];
        }

        private void HandleTrafficVehicleStolen(TrafficVehicleAI stolenAI)
        {
            if (stolenAI == null) return;

            stolenAI.OnStolenByPlayer -= HandleTrafficVehicleStolen;
            activeTrafficVehicles.Remove(stolenAI);

            if (stolenAI.Vehicle != null && !stolenVehicles.Contains(stolenAI.Vehicle))
            {
                stolenVehicles.Add(stolenAI.Vehicle);
            }

            // Desanexa da hierarquia do TrafficManager para organização clara na cena
            stolenAI.transform.SetParent(null, worldPositionStays: true);
        }

        private Vector3 GetReferencePosition()
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
    }
}

