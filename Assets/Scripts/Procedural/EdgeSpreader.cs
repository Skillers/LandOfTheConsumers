using UnityEngine;
using System.Collections.Generic;

namespace LandOfTheConsumers.Procedural
{
    /// <summary>
    /// Component for managing edge spreading functionality
    /// </summary>
    public class EdgeSpreader : MonoBehaviour
    {
        [HideInInspector] public EdgeData edgeData;
        private List<Vector2Int> edgePoints;
        private Vector2 perpendicular;

        public void SetupEdgeSpread()
        {
            // Get edgeData from the EdgePairGenerator on the same GameObject
            EdgePairGenerator edgePairGenerator = GetComponent<EdgePairGenerator>();
            if (edgePairGenerator == null)
            {
                Debug.LogError("[EdgeSpreader] EdgePairGenerator component not found on this GameObject!");
                return;
            }

            edgeData = edgePairGenerator.edgeData;

            // Get the perpendicular vector from edge data
            perpendicular = edgeData.perpendicularDirection;

            // Copy all edge points
            edgePoints = new List<Vector2Int>(edgeData.edgePixels);

            SpreadWings();
        }

        private void SpreadWings()
        {
            for (int i = 0; i < edgePoints.Count; i++)
            {

            }
        }
    }
}
