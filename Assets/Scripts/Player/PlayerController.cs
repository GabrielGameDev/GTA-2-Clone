using System;
using UnityEngine;
using UnityEngine.InputSystem;
using GTA2Clone.CameraSystem;
using GTA2Clone.Vehicles;

namespace GTA2Clone.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;

        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float rotationSpeed = 720f;
        [SerializeField] private float gravity = -20f;

        [Header("Vehicle Interaction")]
        [SerializeField] private float enterVehicleRadius = 2.8f;
        [SerializeField] private LayerMask vehicleLayerMask = ~0;
        [SerializeField] private TopDownCameraFollow cameraFollow;

        [Header("Animation & Visuals")]
        [SerializeField] private Animator animator;
        [SerializeField] private GameObject visualRoot;

        private CharacterController characterController;
        private InputAction moveAction;
        private InputAction stealVehicleAction;

        private Vector2 moveInput;
        private float verticalVelocity;
        private float lastExitTime = -10f;
        private VehicleController currentVehicle;

        private readonly Collider[] overlapResults = new Collider[16];
        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        public Vector3 Velocity => characterController != null ? characterController.velocity : Vector3.zero;
        public VehicleController CurrentVehicle => currentVehicle;

        public event Action<VehicleController> OnVehicleEntered;
        public event Action<VehicleController> OnVehicleExited;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (visualRoot == null && animator != null)
            {
                visualRoot = animator.gameObject;
            }

            if (inputActions != null)
            {
                var playerMap = inputActions.FindActionMap("Player", throwIfNotFound: true);
                moveAction = playerMap.FindAction("Move", throwIfNotFound: true);
                stealVehicleAction = playerMap.FindAction("StealVehicle", throwIfNotFound: true);
            }
        }

        private void OnEnable()
        {
            if (moveAction != null)
            {
                moveAction.Enable();
            }

            if (stealVehicleAction != null)
            {
                stealVehicleAction.performed -= OnStealVehiclePerformed;
                stealVehicleAction.performed += OnStealVehiclePerformed;
                stealVehicleAction.Enable();
            }
        }

        private void OnDisable()
        {
            if (stealVehicleAction != null)
            {
                stealVehicleAction.performed -= OnStealVehiclePerformed;
            }
        }

        private void Update()
        {
            ReadInput();
            HandleMovement();
            UpdateAnimator();
        }

        private void ReadInput()
        {
            moveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        }

        private void HandleMovement()
        {
            Vector3 moveDirection = new Vector3(moveInput.x, 0f, moveInput.y);
            if (moveDirection.sqrMagnitude > 1f)
            {
                moveDirection.Normalize();
            }

            if (moveDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
            }

            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }
            verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = moveDirection * moveSpeed;
            velocity.y = verticalVelocity;

            characterController.Move(velocity * Time.deltaTime);
        }

        private void UpdateAnimator()
        {
            if (animator == null) return;
            float normalizedSpeed = Mathf.Clamp01(moveInput.magnitude);
            animator.SetFloat(SpeedHash, normalizedSpeed);
        }

        private void OnStealVehiclePerformed(InputAction.CallbackContext context)
        {
            if (Time.time - lastExitTime < 0.2f) return;
            TryStealVehicle();
        }

        /// <summary>
        /// Procura o veículo disponível mais próximo dentro do raio de alcance e entra nele.
        /// </summary>
        public void TryStealVehicle()
        {
            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                enterVehicleRadius,
                overlapResults,
                vehicleLayerMask,
                QueryTriggerInteraction.Ignore
            );

            VehicleController closestVehicle = null;
            float closestDistSq = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = overlapResults[i];
                if (col == null) continue;

                VehicleController vehicle = col.GetComponentInParent<VehicleController>();
                if (vehicle == null || vehicle.IsOccupied) continue;

                float distSq = (vehicle.transform.position - transform.position).sqrMagnitude;
                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    closestVehicle = vehicle;
                }
            }

            if (closestVehicle != null)
            {
                closestVehicle.EnterVehicle(this);
            }
        }

        /// <summary>
        /// Chamado pelo VehicleController quando o jogador entra no carro.
        /// Desativa o CharacterController e o visual, tornando o jogador filho do veículo.
        /// </summary>
        public void OnEnteredVehicle(VehicleController vehicle)
        {
            currentVehicle = vehicle;
            moveInput = Vector2.zero;
            verticalVelocity = 0f;

            if (animator != null)
            {
                animator.SetFloat(SpeedHash, 0f);
            }

            if (characterController != null)
            {
                characterController.enabled = false;
            }

            if (visualRoot != null)
            {
                visualRoot.SetActive(false);
            }

            transform.SetParent(vehicle.transform, worldPositionStays: false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            if (cameraFollow != null)
            {
                cameraFollow.SetTarget(vehicle.transform);
            }

            enabled = false;
            OnVehicleEntered?.Invoke(vehicle);
        }

        /// <summary>
        /// Chamado pelo VehicleController quando o jogador sai do carro.
        /// Reposiciona o jogador na porta lateral e reativa seus componentes.
        /// </summary>
        public void OnExitedVehicle(Vector3 exitPosition, Quaternion exitRotation)
        {
            VehicleController exitedVehicle = currentVehicle;
            currentVehicle = null;
            lastExitTime = Time.time;

            transform.SetParent(null, worldPositionStays: true);
            transform.SetPositionAndRotation(exitPosition, exitRotation);

            if (visualRoot != null)
            {
                visualRoot.SetActive(true);
            }

            if (characterController != null)
            {
                characterController.enabled = true;
            }

            if (cameraFollow != null)
            {
                cameraFollow.SetTarget(transform);
            }

            enabled = true;
            OnVehicleExited?.Invoke(exitedVehicle);
        }
    }
}
