using System.Collections.Generic;
using UnityEngine;

namespace GTA2Clone.Traffic
{
    /// <summary>
    /// Armazena o grafo de waypoints das faixas de rolamento do mapa e fornece pontos livres para spawn de tráfego.
    /// </summary>
    public class TrafficRoadGraph : MonoBehaviour
    {
        [SerializeField] private List<TrafficWaypoint> waypoints = new List<TrafficWaypoint>();

        private readonly Collider[] clearanceHits = new Collider[12];
        private readonly List<TrafficWaypoint> candidateBuffer = new List<TrafficWaypoint>(256);

        public IReadOnlyList<TrafficWaypoint> Waypoints => waypoints;

        /// <summary>
        /// Define a lista completa de waypoints do grafo (usado pelo gerador de mapa em Editor).
        /// </summary>
        public void SetWaypoints(List<TrafficWaypoint> generatedWaypoints)
        {
            waypoints = generatedWaypoints ?? new List<TrafficWaypoint>();
        }

        /// <summary>
        /// Procura um waypoint desobstruído dentro de um anel de distância ao redor da posição de referência.
        /// </summary>
        public bool TryGetSpawnWaypoint(
            Vector3 referencePos,
            float minDistance,
            float maxDistance,
            float clearanceRadius,
            LayerMask obstacleMask,
            out TrafficWaypoint selectedWaypoint)
        {
            selectedWaypoint = null;
            if (waypoints == null || waypoints.Count == 0)
                return false;

            float minDistSq = minDistance * minDistance;
            float maxDistSq = maxDistance * maxDistance;

            candidateBuffer.Clear();
            for (int i = 0; i < waypoints.Count; i++)
            {
                TrafficWaypoint wp = waypoints[i];
                if (wp == null || wp.NextWaypoints.Count == 0) continue;

                // Evita spawnar exatamente em cima da linha de retenção de um semáforo ou entrada de curva
                if (wp.ControlledIntersection != null || wp.IsIntersectionEntry)
                    continue;

                Vector3 diff = wp.Position - referencePos;
                diff.y = 0f;
                float distSq = diff.sqrMagnitude;

                if (distSq >= minDistSq && distSq <= maxDistSq)
                {
                    candidateBuffer.Add(wp);
                }
            }

            // Fallback caso nenhum waypoint fora de cruzamento seja encontrado no anel
            if (candidateBuffer.Count == 0)
            {
                for (int i = 0; i < waypoints.Count; i++)
                {
                    TrafficWaypoint wp = waypoints[i];
                    if (wp == null || wp.NextWaypoints.Count == 0) continue;

                    Vector3 diff = wp.Position - referencePos;
                    diff.y = 0f;
                    float distSq = diff.sqrMagnitude;

                    if (distSq >= minDistSq && distSq <= maxDistSq)
                    {
                        candidateBuffer.Add(wp);
                    }
                }
            }

            if (candidateBuffer.Count == 0)
                return false;

            int attempts = Mathf.Min(16, candidateBuffer.Count);
            int startIndex = Random.Range(0, candidateBuffer.Count);

            for (int i = 0; i < attempts; i++)
            {
                int idx = (startIndex + i * 7) % candidateBuffer.Count;
                TrafficWaypoint candidate = candidateBuffer[idx];

                if (IsSpawnPositionClear(candidate.Position, clearanceRadius, obstacleMask))
                {
                    selectedWaypoint = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Encontra o waypoint mais próximo alinhado com a direção fornecida (útil para conectar veículos já presentes na cena).
        /// </summary>
        public TrafficWaypoint FindClosestAlignedWaypoint(Vector3 position, Vector3 forward, float maxDistance = 8f)
        {
            if (waypoints == null || waypoints.Count == 0)
                return null;

            float bestDistSq = maxDistance * maxDistance;
            TrafficWaypoint bestWp = null;

            for (int i = 0; i < waypoints.Count; i++)
            {
                TrafficWaypoint wp = waypoints[i];
                if (wp == null || wp.NextWaypoints.Count == 0) continue;

                if (Vector3.Dot(wp.ForwardDirection, forward) < 0.45f)
                    continue;

                Vector3 diff = wp.Position - position;
                diff.y = 0f;
                float distSq = diff.sqrMagnitude;

                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestWp = wp;
                }
            }

            return bestWp;
        }

        private bool IsSpawnPositionClear(Vector3 position, float radius, LayerMask mask)
        {
            Vector3 checkPos = position + Vector3.up * 0.75f;
            int hitCount = Physics.OverlapSphereNonAlloc(
                checkPos,
                radius,
                clearanceHits,
                mask,
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = clearanceHits[i];
                if (col == null) continue;
                if (col.gameObject.name == "Ground" || col.gameObject.name.StartsWith("Road_") || col.gameObject.name.StartsWith("SidewalkLot_"))
                    continue;

                return false;
            }

            return true;
        }
    }
}

