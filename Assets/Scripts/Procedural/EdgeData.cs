using System.Collections.Generic;
using UnityEngine;

namespace LandOfTheConsumers.Procedural
{
    /// <summary>
    /// Data structure for storing information about an edge between two regions.
    /// Contains pixel coordinates for the 3-pixel-wide edge and region pair IDs.
    /// </summary>
    [System.Serializable]
    public class EdgeData
    {
        /// <summary>
        /// First region ID (always the lower ID)
        /// </summary>
        public int regionIdA;

        /// <summary>
        /// Second region ID (always the higher ID)
        /// </summary>
        public int regionIdB;

        /// <summary>
        /// List of center line pixels (1-pixel wide, before expansion)
        /// These are used to calculate heights for the ribbon
        /// </summary>
        public List<Vector2Int> centerPixels;

        /// <summary>
        /// HashSet for O(1) lookup of center pixel membership
        /// </summary>
        public HashSet<Vector2Int> centerPixelSet;

        /// <summary>
        /// List of all pixel coordinates belonging to this edge (3-pixel width)
        /// </summary>
        public List<Vector2Int> edgePixels;

        /// <summary>
        /// HashSet for O(1) lookup of pixel membership
        /// </summary>
        public HashSet<Vector2Int> edgePixelSet;

        public EdgeData(int regionA, int regionB)
        {
            // Always store smaller ID first for consistent naming
            if (regionA < regionB)
            {
                regionIdA = regionA;
                regionIdB = regionB;
            }
            else
            {
                regionIdA = regionB;
                regionIdB = regionA;
            }

            centerPixels = new List<Vector2Int>();
            centerPixelSet = new HashSet<Vector2Int>();
            edgePixels = new List<Vector2Int>();
            edgePixelSet = new HashSet<Vector2Int>();
        }

        /// <summary>
        /// Add a center pixel (1-pixel wide line)
        /// </summary>
        public void AddCenterPixel(Vector2Int pixel)
        {
            if (!centerPixelSet.Contains(pixel))
            {
                centerPixels.Add(pixel);
                centerPixelSet.Add(pixel);
            }
        }

        /// <summary>
        /// Add a pixel to this edge (3-pixel width, avoids duplicates)
        /// </summary>
        public void AddPixel(Vector2Int pixel)
        {
            if (!edgePixelSet.Contains(pixel))
            {
                edgePixels.Add(pixel);
                edgePixelSet.Add(pixel);
            }
        }

        /// <summary>
        /// Get the formatted name for this edge pair GameObject
        /// </summary>
        public string GetPairName()
        {
            return $"EdgePair_{regionIdA}_{regionIdB}";
        }
    }
}
