using UnityEngine;

namespace GTA2Clone.Vehicles
{
    /// <summary>
    /// Armazena os parâmetros de identificação, condução arcade e direção/aderência de um veículo.
    /// </summary>
    [CreateAssetMenu(fileName = "VehicleData_", menuName = "GTA2/Vehicle Data")]
    public class VehicleData : ScriptableObject
    {
        [Header("Identification")]
        [SerializeField] private string vehicleName = "Vehicle";

        [Header("Drive Settings (GTA 2 Arcade)")]
        [SerializeField] private float acceleration = 20f;
        [SerializeField] private float reverseAcceleration = 12f;
        [SerializeField] private float maxForwardSpeed = 18f;
        [SerializeField] private float maxReverseSpeed = 8f;
        [SerializeField] private float brakeStrength = 32f;
        [SerializeField] private float naturalDrag = 5f;

        [Header("Steering & Grip")]
        [SerializeField] private float turnSpeed = 165f;
        [SerializeField] private float minSpeedForFullTurn = 3.5f;
        [SerializeField] private float lateralFriction = 9f;

        public string VehicleName => vehicleName;
        public float Acceleration => acceleration;
        public float ReverseAcceleration => reverseAcceleration;
        public float MaxForwardSpeed => maxForwardSpeed;
        public float MaxReverseSpeed => maxReverseSpeed;
        public float BrakeStrength => brakeStrength;
        public float NaturalDrag => naturalDrag;
        public float TurnSpeed => turnSpeed;
        public float MinSpeedForFullTurn => minSpeedForFullTurn;
        public float LateralFriction => lateralFriction;

        /// <summary>
        /// Configura todos os dados do veículo (usado por ferramentas de Editor na geração de assets).
        /// </summary>
        public void Initialize(
            string name,
            float accel,
            float reverseAccel,
            float maxForward,
            float maxReverse,
            float brake,
            float drag,
            float turn,
            float minSpeedTurn,
            float grip)
        {
            vehicleName = name;
            acceleration = accel;
            reverseAcceleration = reverseAccel;
            maxForwardSpeed = maxForward;
            maxReverseSpeed = maxReverse;
            brakeStrength = brake;
            naturalDrag = drag;
            turnSpeed = turn;
            minSpeedForFullTurn = minSpeedTurn;
            lateralFriction = grip;
        }
    }
}

