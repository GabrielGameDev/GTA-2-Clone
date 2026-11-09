using UnityEngine;
using GTA2Clone.Vehicles;

namespace GTA2Clone.Missions
{
    /// <summary>
    /// Fase atual da missão no loop de roubo e entrega.
    /// </summary>
    public enum MissionPhase
    {
        None = 0,
        StealVehicle = 1,
        DeliverVehicle = 2,
        GameOver = 3
    }

    /// <summary>
    /// Motivo pelo qual a partida foi encerrada (Game Over).
    /// </summary>
    public enum GameOverReason
    {
        TimeExpired = 0,
        Busted = 1,
        VehicleDestroyed = 2
    }

    /// <summary>
    /// Origem do evento de nível de procurado (heat) para integração com o sistema de polícia.
    /// </summary>
    public enum HeatSourceType
    {
        MissionVehicleStolen = 0,
        MissionVehicleDelivered = 1,
        FreeRoamCarjacking = 2,
        PoliceWitnessedCarjacking = 3,
        EveryThreeDeliveriesMilestone = 4
    }

    /// <summary>
    /// Dados da missão ativa atual emitidos quando um contrato inicia ou muda de fase.
    /// </summary>
    public readonly struct ActiveMissionInfo
    {
        public readonly int MissionNumber;
        public readonly VehicleContractData Contract;
        public readonly VehicleController TargetVehicle;
        public readonly DeliveryZone TargetDeliveryZone;
        public readonly MissionPhase Phase;
        public readonly bool SpawnedInTraffic;

        public ActiveMissionInfo(
            int missionNumber,
            VehicleContractData contract,
            VehicleController targetVehicle,
            DeliveryZone targetDeliveryZone,
            MissionPhase phase,
            bool spawnedInTraffic)
        {
            MissionNumber = missionNumber;
            Contract = contract;
            TargetVehicle = targetVehicle;
            TargetDeliveryZone = targetDeliveryZone;
            Phase = phase;
            SpawnedInTraffic = spawnedInTraffic;
        }
    }

    /// <summary>
    /// Resultado econômico, de tempo e estatístico de uma entrega concluída.
    /// </summary>
    public readonly struct MissionCompletionResult
    {
        public readonly int MissionNumber;
        public readonly VehicleContractData Contract;
        public readonly DeliveryZone DeliveryZone;
        public readonly int BaseReward;
        public readonly int TimeBonus;
        public readonly int TotalReward;
        public readonly int TotalPlayerMoney;
        public readonly int CompletedMissionsCount;
        public readonly float ElapsedDeliverySeconds;
        public readonly float AddedTimeSeconds;
        public readonly float RemainingTimeBankSeconds;

        public MissionCompletionResult(
            int missionNumber,
            VehicleContractData contract,
            DeliveryZone deliveryZone,
            int baseReward,
            int timeBonus,
            int totalReward,
            int totalPlayerMoney,
            int completedMissionsCount,
            float elapsedDeliverySeconds,
            float addedTimeSeconds,
            float remainingTimeBankSeconds)
        {
            MissionNumber = missionNumber;
            Contract = contract;
            DeliveryZone = deliveryZone;
            BaseReward = baseReward;
            TimeBonus = timeBonus;
            TotalReward = totalReward;
            TotalPlayerMoney = totalPlayerMoney;
            CompletedMissionsCount = completedMissionsCount;
            ElapsedDeliverySeconds = elapsedDeliverySeconds;
            AddedTimeSeconds = addedTimeSeconds;
            RemainingTimeBankSeconds = remainingTimeBankSeconds;
        }
    }

    /// <summary>
    /// Dados finais da partida quando ocorre Game Over (por tempo esgotado, prisão ou explosão do veículo).
    /// </summary>
    public readonly struct GameOverResult
    {
        public readonly int TotalPlayerMoney;
        public readonly int CompletedMissionsCount;
        public readonly float TotalSurvivedSeconds;
        public readonly int FinalHeatLevel;
        public readonly GameOverReason Reason;
        public readonly string CustomMessage;

        public GameOverResult(
            int totalPlayerMoney,
            int completedMissionsCount,
            float totalSurvivedSeconds,
            int finalHeatLevel,
            GameOverReason reason = GameOverReason.TimeExpired,
            string customMessage = null)
        {
            TotalPlayerMoney = totalPlayerMoney;
            CompletedMissionsCount = completedMissionsCount;
            TotalSurvivedSeconds = totalSurvivedSeconds;
            FinalHeatLevel = finalHeatLevel;
            Reason = reason;
            CustomMessage = customMessage ?? string.Empty;
        }
    }

    /// <summary>
    /// Payload de evento emitido para integração com o sistema de polícia/procurado (Heat).
    /// </summary>
    public readonly struct HeatEventPayload
    {
        public readonly int HeatDelta;
        public readonly int AccumulatedMissionHeat;
        public readonly HeatSourceType Source;
        public readonly Vector3 WorldPosition;
        public readonly VehicleController Vehicle;
        public readonly VehicleContractData Contract;

        public HeatEventPayload(
            int heatDelta,
            int accumulatedMissionHeat,
            HeatSourceType source,
            Vector3 worldPosition,
            VehicleController vehicle,
            VehicleContractData contract)
        {
            HeatDelta = heatDelta;
            AccumulatedMissionHeat = accumulatedMissionHeat;
            Source = source;
            WorldPosition = worldPosition;
            Vehicle = vehicle;
            Contract = contract;
        }
    }
}
