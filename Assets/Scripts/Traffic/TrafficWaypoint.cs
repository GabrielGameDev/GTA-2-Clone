using System.Collections.Generic;
using UnityEngine;

namespace GTA2Clone.Traffic
{
    /// <summary>
    /// Representa um nó direcional de faixa de rolamento na malha viária do tráfego urbano.
    /// </summary>
    public class TrafficWaypoint : MonoBehaviour
    {
        [Header("Lane Configuration")]
        [SerializeField] private Vector3 forwardDirection = Vector3.forward;
        [SerializeField] private bool isIntersectionEntry;
        [SerializeField] private TrafficIntersection controlledIntersection;
        [SerializeField] private List<TrafficWaypoint> nextWaypoints = new List<TrafficWaypoint>();

        public Vector3 Position => transform.position;
        public Vector3 ForwardDirection => forwardDirection.sqrMagnitude > 0.001f ? forwardDirection.normalized : transform.forward;
        public bool IsIntersectionEntry => isIntersectionEntry;
        public TrafficIntersection ControlledIntersection => controlledIntersection;
        public IReadOnlyList<TrafficWaypoint> NextWaypoints => nextWaypoints;

        /// <summary>
        /// Inicializa os dados do waypoint (utilizado pelo gerador de mapa em Editor).
        /// </summary>
        public void Configure(Vector3 direction, bool intersectionEntry, TrafficIntersection intersection = null)
        {
            forwardDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
            isIntersectionEntry = intersectionEntry;
            controlledIntersection = intersection;
            transform.rotation = Quaternion.LookRotation(forwardDirection, Vector3.up);
        }

        /// <summary>
        /// Verifica se o veículo tem sinal verde e área livre caso este waypoint seja uma parada de semáforo.
        /// </summary>
        public bool CanProceed(TrafficVehicleAI vehicle)
        {
            if (controlledIntersection == null)
                return true;

            return controlledIntersection.CanVehicleEnter(vehicle, ForwardDirection);
        }

        /// <summary>
        /// Retorna verdadeiro caso o semáforo controlado esteja fechado (Vermelho ou Amarelo) para esta faixa.
        /// </summary>
        public bool IsSignalRedOrYellow()
        {
            if (controlledIntersection == null)
                return false;

            return controlledIntersection.GetSignalForDirection(ForwardDirection) != TrafficIntersection.SignalState.Green;
        }

        /// <summary>
        /// Adiciona uma conexão direcionada para um waypoint seguinte caso ainda não exista.
        /// </summary>
        public void AddConnection(TrafficWaypoint target)
        {
            if (target == null || target == this) return;
            if (!nextWaypoints.Contains(target))
            {
                nextWaypoints.Add(target);
            }
        }

        /// <summary>
        /// Sorteia um próximo waypoint conectado para o veículo seguir.
        /// </summary>
        public TrafficWaypoint GetNextWaypoint()
        {
            if (nextWaypoints == null || nextWaypoints.Count == 0)
                return null;

            if (nextWaypoints.Count == 1)
                return nextWaypoints[0];

            int index = Random.Range(0, nextWaypoints.Count);
            return nextWaypoints[index];
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = isIntersectionEntry ? new Color(1f, 0.65f, 0.1f, 0.9f) : new Color(0.15f, 0.85f, 1f, 0.9f);
            Gizmos.DrawSphere(transform.position, 0.35f);

            if (nextWaypoints == null) return;

            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.85f);
            foreach (var next in nextWaypoints)
            {
                if (next == null) continue;
                Vector3 from = transform.position + Vector3.up * 0.15f;
                Vector3 to = next.transform.position + Vector3.up * 0.15f;
                Gizmos.DrawLine(from, to);

                Vector3 dir = (to - from).normalized;
                if (dir.sqrMagnitude > 0.01f)
                {
                    Vector3 mid = Vector3.Lerp(from, to, 0.65f);
                    Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
                    Gizmos.DrawLine(mid, mid - dir * 0.6f + right * 0.3f);
                    Gizmos.DrawLine(mid, mid - dir * 0.6f - right * 0.3f);
                }
            }
        }
    }
}

