using UnityEngine;
using GTA2Clone.Vehicles;

namespace GTA2Clone.CameraSystem
{
    public class TopDownCameraFollow : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Camera Settings")]
        [SerializeField] private float height = 22f;
        [SerializeField] private float smoothTime = 0.12f;
        [SerializeField] private Vector2 planarOffset = Vector2.zero;

        [Header("Dynamic Speed Zoom (GTA 2)")]
        [SerializeField] private float maxSpeedExtraHeight = 14f;
        [SerializeField] private float zoomOutSmoothTime = 0.35f;
        [SerializeField] private float zoomInSmoothTime = 0.5f;

        private VehicleController targetVehicle;
        private Vector3 planarVelocity;
        private float currentHeight;
        private float heightVelocity;

        private void Awake()
        {
            currentHeight = height;
            CacheTargetVehicle();
        }

        private void LateUpdate()
        {
            if (target == null) return;

            float speedFactor = targetVehicle != null ? targetVehicle.NormalizedSpeed : 0f;
            float desiredHeight = height + (maxSpeedExtraHeight * speedFactor);
            float activeZoomSmoothTime = desiredHeight > currentHeight ? zoomOutSmoothTime : zoomInSmoothTime;

            currentHeight = Mathf.SmoothDamp(
                currentHeight,
                desiredHeight,
                ref heightVelocity,
                activeZoomSmoothTime
            );

            Vector3 currentPos = transform.position;
            Vector3 desiredPlanarPos = new Vector3(
                target.position.x + planarOffset.x,
                0f,
                target.position.z + planarOffset.y
            );
            Vector3 currentPlanarPos = new Vector3(currentPos.x, 0f, currentPos.z);

            Vector3 smoothedPlanar = Vector3.SmoothDamp(
                currentPlanarPos,
                desiredPlanarPos,
                ref planarVelocity,
                smoothTime
            );

            transform.position = new Vector3(
                smoothedPlanar.x,
                target.position.y + currentHeight,
                smoothedPlanar.z
            );

            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        /// <summary>
        /// Define o Transform seguido pela câmera e detecta automaticamente se é um veículo para aplicar zoom dinâmico.
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            CacheTargetVehicle();
        }

        private void CacheTargetVehicle()
        {
            targetVehicle = target != null ? target.GetComponent<VehicleController>() : null;
        }
    }
}
