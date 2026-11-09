using UnityEngine;
using GTA2Clone.Vehicles;

namespace GTA2Clone.Missions
{
    /// <summary>
    /// ScriptableObject que define um contrato de roubo e entrega de veículo no loop arcade.
    /// </summary>
    [CreateAssetMenu(fileName = "Contract_", menuName = "GTA2/Vehicle Contract")]
    public class VehicleContractData : ScriptableObject
    {
        [Header("Identification")]
        [SerializeField] private string contractId = "contract_default";
        [SerializeField] private string contractTitle = "Encomenda de Veículo";

        [Header("Target Vehicle")]
        [SerializeField] private VehicleData targetVehicleData;
        [SerializeField] private VehicleController targetVehiclePrefab;
        [SerializeField] private bool allowTrafficSpawn = true;

        [Header("Economy & Time Bank (Crazy Taxi Arcade)")]
        [SerializeField] private int baseReward = 2000;
        [SerializeField] private float targetDeliveryTimeSeconds = 45f;
        [SerializeField] private int maxSpeedBonus = 1000;
        [SerializeField] private float baseTimeAwardSeconds = 25f;

        [Header("Wanted / Heat Integration")]
        [SerializeField] private int heatOnSteal = 1;
        [SerializeField] private int heatOnDelivery = 0;

        public string ContractId => contractId;
        public string ContractTitle => contractTitle;
        public VehicleData TargetVehicleData => targetVehicleData;
        public VehicleController TargetVehiclePrefab => targetVehiclePrefab;
        public bool AllowTrafficSpawn => allowTrafficSpawn;
        public int BaseReward => baseReward;
        public float TargetDeliveryTimeSeconds => targetDeliveryTimeSeconds;
        public int MaxSpeedBonus => maxSpeedBonus;
        public float BaseTimeAwardSeconds => baseTimeAwardSeconds;
        public int HeatOnSteal => heatOnSteal;
        public int HeatOnDelivery => heatOnDelivery;

        public string TargetVehicleDisplayName => targetVehicleData != null
            ? targetVehicleData.VehicleName
            : (targetVehiclePrefab != null ? targetVehiclePrefab.VehicleName : contractTitle);

        /// <summary>
        /// Inicializa os dados do contrato (usado pelas ferramentas de Editor na geração automática de assets).
        /// </summary>
        public void Initialize(
            string id,
            string title,
            VehicleData vehicleData,
            VehicleController vehiclePrefab,
            int reward,
            float targetTime,
            int speedBonus,
            int stealHeat,
            int deliveryHeat,
            bool canSpawnInTraffic = true,
            float timeAwardSeconds = 25f)
        {
            contractId = id;
            contractTitle = title;
            targetVehicleData = vehicleData;
            targetVehiclePrefab = vehiclePrefab;
            baseReward = reward;
            targetDeliveryTimeSeconds = targetTime;
            maxSpeedBonus = speedBonus;
            heatOnSteal = stealHeat;
            heatOnDelivery = deliveryHeat;
            allowTrafficSpawn = canSpawnInTraffic;
            baseTimeAwardSeconds = timeAwardSeconds;
        }
    }
}
