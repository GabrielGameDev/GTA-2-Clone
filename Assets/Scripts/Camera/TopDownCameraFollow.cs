using UnityEngine;

namespace GTA2Clone.CameraSystem
{
    public class TopDownCameraFollow : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Camera Settings")]
        [SerializeField] private float height = 18f;
        [SerializeField] private float smoothTime = 0.12f;
        [SerializeField] private Vector2 planarOffset = Vector2.zero;

        private Vector3 currentVelocity;

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desiredPosition = new Vector3(
                target.position.x + planarOffset.x,
                target.position.y + height,
                target.position.z + planarOffset.y
            );

            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref currentVelocity,
                smoothTime
            );

            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }
    }
}

