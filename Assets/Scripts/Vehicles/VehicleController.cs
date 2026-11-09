using System;
using UnityEngine;
using UnityEngine.InputSystem;
using GTA2Clone.Player;

namespace GTA2Clone.Vehicles
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BoxCollider))]
    public class VehicleController : MonoBehaviour
    {
        [Header("Vehicle Data")]
        [SerializeField] private VehicleData vehicleData;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;

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

        [Header("Wheel Visuals")]
        [SerializeField] private Transform frontLeftWheel;
        [SerializeField] private Transform frontRightWheel;
        [SerializeField] private Transform backLeftWheel;
        [SerializeField] private Transform backRightWheel;
        [SerializeField] private float maxVisualSteerAngle = 32f;
        [SerializeField] private float wheelSteerLerpSpeed = 12f;
        [SerializeField] private float wheelRadius = 0.32f;

        [Header("Entry / Exit")]
        [SerializeField] private Transform exitPointLeft;
        [SerializeField] private Transform exitPointRight;
        [SerializeField] private Vector3 fallbackExitOffset = new Vector3(-1.45f, 0.1f, 0f);
        [SerializeField] private float exitCheckRadius = 0.4f;
        [SerializeField] private LayerMask obstacleLayerMask = ~0;

        private Rigidbody rb;
        private BoxCollider boxCollider;
        private InputAction moveAction;
        private InputAction exitVehicleAction;

        private static PhysicsMaterial sharedFrictionlessMaterial;

        private PlayerController currentDriver;
        private Vector2 driveInput;
        private Vector2 aiDriveInput;
        private bool hasAIDriver;
        private float currentVisualSteerAngle;
        private float wheelSpinAngle;
        private float enterTimestamp;

        private Quaternion flInitialRot = Quaternion.identity;
        private Quaternion frInitialRot = Quaternion.identity;
        private Quaternion blInitialRot = Quaternion.identity;
        private Quaternion brInitialRot = Quaternion.identity;

        public VehicleData Data => vehicleData;
        public string VehicleName => vehicleData != null ? vehicleData.VehicleName : gameObject.name;
        public bool IsOccupied => currentDriver != null;
        public bool HasAIDriver => hasAIDriver && !IsOccupied;
        public bool IsControlled => IsOccupied || HasAIDriver;
        public float ForwardSpeed { get; private set; }
        public float MaxForwardSpeed => maxForwardSpeed;
        public float CurrentSpeed => rb != null
            ? new Vector2(rb.linearVelocity.x, rb.linearVelocity.z).magnitude
            : Mathf.Abs(ForwardSpeed);
        public float NormalizedSpeed => maxForwardSpeed > 0.01f
            ? Mathf.Clamp01(CurrentSpeed / maxForwardSpeed)
            : 0f;

        public event Action<PlayerController> OnDriverEntered;
        public event Action<PlayerController> OnDriverExited;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            boxCollider = GetComponent<BoxCollider>();
            ApplyVehicleData();
            ConfigureRigidbody();
            ConfigureColliderMaterial();
            AutoFindWheels();
            EnsureWheelPivotsCentered();
            CacheWheelRotations();

            if (inputActions != null)
            {
                var playerMap = inputActions.FindActionMap("Player", throwIfNotFound: true);
                moveAction = playerMap.FindAction("Move", throwIfNotFound: true);
                exitVehicleAction = playerMap.FindAction("StealVehicle", throwIfNotFound: true);
            }
        }

        private void OnValidate()
        {
            ApplyVehicleData();
        }

        /// <summary>
        /// Sincroniza os parâmetros de condução e direção a partir do ScriptableObject VehicleData atribuído.
        /// </summary>
        public void ApplyVehicleData()
        {
            if (vehicleData == null) return;

            acceleration = vehicleData.Acceleration;
            reverseAcceleration = vehicleData.ReverseAcceleration;
            maxForwardSpeed = vehicleData.MaxForwardSpeed;
            maxReverseSpeed = vehicleData.MaxReverseSpeed;
            brakeStrength = vehicleData.BrakeStrength;
            naturalDrag = vehicleData.NaturalDrag;

            turnSpeed = vehicleData.TurnSpeed;
            minSpeedForFullTurn = vehicleData.MinSpeedForFullTurn;
            lateralFriction = vehicleData.LateralFriction;
        }

        private void OnDisable()
        {
            UnbindExitInput();
        }

        private void Update()
        {
            if (IsOccupied)
            {
                driveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
            }
            else if (hasAIDriver)
            {
                driveInput = aiDriveInput;
            }
            else
            {
                driveInput = Vector2.zero;
            }

            UpdateWheelVisuals();
        }

        private void FixedUpdate()
        {
            HandleArcadePhysics();
        }

        /// <summary>
        /// Define os comandos de direção (x = steer, y = throttle) fornecidos por um motorista de IA.
        /// Ignorado caso o jogador esteja ocupando o veículo.
        /// </summary>
        public void SetAIInput(Vector2 input)
        {
            if (IsOccupied) return;
            hasAIDriver = true;
            aiDriveInput = new Vector2(Mathf.Clamp(input.x, -1f, 1f), Mathf.Clamp(input.y, -1f, 1f));
        }

        /// <summary>
        /// Remove o controle da IA sobre o veículo e zera os comandos de direção.
        /// </summary>
        public void ClearAIInput()
        {
            hasAIDriver = false;
            aiDriveInput = Vector2.zero;
            if (!IsOccupied)
            {
                driveInput = Vector2.zero;
            }
        }

        /// <summary>
        /// Faz o jogador entrar no veículo e assumir a direção.
        /// </summary>
        public bool EnterVehicle(PlayerController driver)
        {
            if (IsOccupied || driver == null) return false;

            ClearAIInput();
            currentDriver = driver;
            enterTimestamp = Time.time;

            driver.OnEnteredVehicle(this);

            BindDriverInput();
            OnDriverEntered?.Invoke(driver);
            return true;
        }

        /// <summary>
        /// Faz o motorista atual sair do veículo pela porta lateral.
        /// </summary>
        public void ExitVehicle()
        {
            if (!IsOccupied) return;

            // Evita sair no mesmo frame em que entrou
            if (Time.time - enterTimestamp < 0.2f) return;

            PlayerController exitingDriver = currentDriver;
            currentDriver = null;
            driveInput = Vector2.zero;

            UnbindExitInput();

            Vector3 exitPos = CalculateExitPosition();
            Quaternion exitRot = Quaternion.LookRotation(-transform.right, Vector3.up);

            exitingDriver.OnExitedVehicle(exitPos, exitRot);
            OnDriverExited?.Invoke(exitingDriver);
        }

        /// <summary>
        /// Detecta automaticamente os Transforms das rodas na hierarquia do modelo 3D.
        /// </summary>
        public void AutoFindWheels()
        {
            if (frontLeftWheel != null && frontRightWheel != null && backLeftWheel != null && backRightWheel != null)
                return;

            Transform[] children = GetComponentsInChildren<Transform>(true);
            foreach (var child in children)
            {
                string lowerName = child.name.ToLowerInvariant();
                if (frontLeftWheel == null && lowerName.Contains("wheel-front-left"))
                    frontLeftWheel = child;
                else if (frontRightWheel == null && lowerName.Contains("wheel-front-right"))
                    frontRightWheel = child;
                else if (backLeftWheel == null && lowerName.Contains("wheel-back-left"))
                    backLeftWheel = child;
                else if (backRightWheel == null && lowerName.Contains("wheel-back-right"))
                    backRightWheel = child;
            }
        }

        private void EnsureWheelPivotsCentered()
        {
            EnsureSingleWheelPivot(ref frontLeftWheel);
            EnsureSingleWheelPivot(ref frontRightWheel);
            EnsureSingleWheelPivot(ref backLeftWheel);
            EnsureSingleWheelPivot(ref backRightWheel);
        }

        private void EnsureSingleWheelPivot(ref Transform wheelTransform)
        {
            if (wheelTransform == null) return;

            Renderer wheelRenderer = wheelTransform.GetComponentInChildren<Renderer>();
            if (wheelRenderer == null) return;

            Bounds worldBounds = wheelRenderer.bounds;
            if (worldBounds.extents.y > 0.05f)
            {
                wheelRadius = worldBounds.extents.y;
            }

            // Cria um pivot no centro geométrico da roda e alinhado com os eixos do veículo (+Z frente, +X direita, +Y cima).
            // Isso garante que rodas espelhadas no FBX girem e estercem na mesma direção sem inverter os eixos.
            GameObject pivotObj = new GameObject(wheelTransform.name + "_Pivot");
            pivotObj.transform.SetParent(transform, worldPositionStays: false);
            pivotObj.transform.position = worldBounds.center;
            pivotObj.transform.rotation = transform.rotation;
            pivotObj.transform.localScale = Vector3.one;

            wheelTransform.SetParent(pivotObj.transform, worldPositionStays: true);
            wheelTransform = pivotObj.transform;
        }

        private void CacheWheelRotations()
        {
            if (frontLeftWheel != null) flInitialRot = frontLeftWheel.localRotation;
            if (frontRightWheel != null) frInitialRot = frontRightWheel.localRotation;
            if (backLeftWheel != null) blInitialRot = backLeftWheel.localRotation;
            if (backRightWheel != null) brInitialRot = backRightWheel.localRotation;
        }

        private void ConfigureRigidbody()
        {
            rb.mass = 1200f;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 3f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.centerOfMass = new Vector3(0f, 0.25f, 0f);
        }

        private void ConfigureColliderMaterial()
        {
            if (boxCollider == null)
            {
                boxCollider = GetComponent<BoxCollider>();
            }
            if (boxCollider == null) return;

            if (sharedFrictionlessMaterial == null)
            {
                sharedFrictionlessMaterial = new PhysicsMaterial("VehicleArcadeFrictionless")
                {
                    dynamicFriction = 0f,
                    staticFriction = 0f,
                    bounciness = 0f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
            }

            boxCollider.sharedMaterial = sharedFrictionlessMaterial;
        }

        private void BindDriverInput()
        {
            if (moveAction != null && !moveAction.enabled)
            {
                moveAction.Enable();
            }

            if (exitVehicleAction != null)
            {
                exitVehicleAction.performed -= OnExitVehiclePerformed;
                exitVehicleAction.performed += OnExitVehiclePerformed;
                if (!exitVehicleAction.enabled)
                {
                    exitVehicleAction.Enable();
                }
            }
        }

        private void UnbindExitInput()
        {
            if (exitVehicleAction != null)
            {
                exitVehicleAction.performed -= OnExitVehiclePerformed;
            }
        }

        private void OnExitVehiclePerformed(InputAction.CallbackContext context)
        {
            ExitVehicle();
        }

        private void HandleArcadePhysics()
        {
            Vector3 velocity = rb.linearVelocity;
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;

            ForwardSpeed = Vector3.Dot(velocity, forward);
            float lateralSpeed = Vector3.Dot(velocity, right);

            float throttle = IsControlled ? driveInput.y : 0f;
            float steer = IsControlled ? driveInput.x : 0f;

            float newForwardSpeed = ForwardSpeed;

            if (throttle > 0.01f)
            {
                // Se estava indo de ré, freia primeiro antes de avançar
                if (ForwardSpeed < -0.1f)
                {
                    newForwardSpeed = Mathf.MoveTowards(ForwardSpeed, 0f, brakeStrength * Time.fixedDeltaTime);
                }
                else
                {
                    newForwardSpeed = Mathf.MoveTowards(ForwardSpeed, maxForwardSpeed * throttle, acceleration * Time.fixedDeltaTime);
                }
            }
            else if (throttle < -0.01f)
            {
                // Se estava indo para frente, S / Seta para baixo freia
                if (ForwardSpeed > 0.15f)
                {
                    newForwardSpeed = Mathf.MoveTowards(ForwardSpeed, 0f, brakeStrength * Time.fixedDeltaTime);
                }
                else
                {
                    // Quando para, engata a marcha à ré
                    newForwardSpeed = Mathf.MoveTowards(ForwardSpeed, maxReverseSpeed * throttle, reverseAcceleration * Time.fixedDeltaTime);
                }
            }
            else
            {
                // Desaceleração natural quando nenhum pedal é pressionado
                float stopDrag = IsControlled ? naturalDrag : naturalDrag * 1.5f;
                newForwardSpeed = Mathf.MoveTowards(ForwardSpeed, 0f, stopDrag * Time.fixedDeltaTime);
            }

            // Aderência lateral (reduz derrapagem infinita mantendo leve drift)
            float effectiveLateralFriction = HasAIDriver ? Mathf.Max(lateralFriction, 14f) : lateralFriction;
            float newLateralSpeed = Mathf.MoveTowards(lateralSpeed, 0f, effectiveLateralFriction * Time.fixedDeltaTime);

            ForwardSpeed = newForwardSpeed;
            Vector3 newVelocity = (forward * newForwardSpeed) + (right * newLateralSpeed);
            newVelocity.y = velocity.y;
            rb.linearVelocity = newVelocity;

            // Direção (só faz curva se o carro estiver em movimento)
            float absSpeed = Mathf.Abs(newForwardSpeed);
            if (absSpeed > 0.08f && Mathf.Abs(steer) > 0.01f)
            {
                float effectiveMinSpeed = HasAIDriver ? Mathf.Min(minSpeedForFullTurn, 1.8f) : minSpeedForFullTurn;
                float effectiveTurnSpeed = HasAIDriver ? Mathf.Max(turnSpeed, 168f) : turnSpeed;
                float speedFactor = Mathf.Clamp01(absSpeed / Mathf.Max(0.25f, effectiveMinSpeed));
                float reverseMultiplier = newForwardSpeed >= -0.05f ? 1f : -1f;
                float turnAmount = steer * effectiveTurnSpeed * speedFactor * reverseMultiplier * Time.fixedDeltaTime;

                Quaternion turnRotation = Quaternion.Euler(0f, turnAmount, 0f);
                rb.MoveRotation(rb.rotation * turnRotation);
            }
            else
            {
                rb.angularVelocity = Vector3.zero;
            }
        }

        private void UpdateWheelVisuals()
        {
            float targetSteerAngle = (IsControlled ? driveInput.x : 0f) * maxVisualSteerAngle;
            currentVisualSteerAngle = Mathf.Lerp(currentVisualSteerAngle, targetSteerAngle, wheelSteerLerpSpeed * Time.deltaTime);

            if (Mathf.Abs(ForwardSpeed) > 0.02f && wheelRadius > 0.01f)
            {
                float angularDisplacement = (ForwardSpeed / wheelRadius) * Mathf.Rad2Deg * Time.deltaTime;
                wheelSpinAngle = Mathf.Repeat(wheelSpinAngle + angularDisplacement, 360f);
            }

            Quaternion steerRot = Quaternion.Euler(0f, currentVisualSteerAngle, 0f);
            Quaternion spinRot = Quaternion.Euler(wheelSpinAngle, 0f, 0f);

            if (frontLeftWheel != null)
                frontLeftWheel.localRotation = flInitialRot * steerRot * spinRot;
            if (frontRightWheel != null)
                frontRightWheel.localRotation = frInitialRot * steerRot * spinRot;
            if (backLeftWheel != null)
                backLeftWheel.localRotation = blInitialRot * spinRot;
            if (backRightWheel != null)
                backRightWheel.localRotation = brInitialRot * spinRot;
        }

        private Vector3 CalculateExitPosition()
        {
            Vector3 leftCandidate = exitPointLeft != null
                ? exitPointLeft.position
                : transform.TransformPoint(fallbackExitOffset);

            if (!IsPositionBlocked(leftCandidate))
            {
                return leftCandidate;
            }

            Vector3 rightCandidate = exitPointRight != null
                ? exitPointRight.position
                : transform.TransformPoint(new Vector3(-fallbackExitOffset.x, fallbackExitOffset.y, fallbackExitOffset.z));

            if (!IsPositionBlocked(rightCandidate))
            {
                return rightCandidate;
            }

            return transform.position + Vector3.up * 1.6f;
        }

        private bool IsPositionBlocked(Vector3 position)
        {
            Vector3 checkCenter = position + Vector3.up * 0.6f;
            Collider[] hits = Physics.OverlapSphere(checkCenter, exitCheckRadius, obstacleLayerMask, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(transform)) continue;
                if (currentDriver != null && hit.transform.IsChildOf(currentDriver.transform)) continue;
                if (hit.gameObject.name == "Ground") continue;
                return true;
            }
            return false;
        }
    }
}
