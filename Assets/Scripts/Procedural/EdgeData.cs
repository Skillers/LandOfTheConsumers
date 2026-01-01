using System.Collections.Generic;
using UnityEngine;

namespace LandOfTheConsumers.Procedural
{
    /// <summary>
    /// Data structure for storing information about an edge between two regions.
    /// Contains pixel coordinates for the edge and region pair IDs.
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
        /// List of line segments that make up this edge boundary
        /// Each segment is a list of pixels from one corner to another
        /// </summary>
        public List<List<Vector2Int>> centerSegments;

        /// <summary>
        /// List of center line pixels (1-pixel wide, ordered to form continuous path)
        /// These are used to calculate heights for the line
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

            centerSegments = new List<List<Vector2Int>>();
            centerPixels = new List<Vector2Int>();
            centerPixelSet = new HashSet<Vector2Int>();
            edgePixels = new List<Vector2Int>();
            edgePixelSet = new HashSet<Vector2Int>();
        }

        /// <summary>
        /// Add a segment (list of pixels forming a line from one corner to another)
        /// </summary>
        public void AddCenterSegment(List<Vector2Int> segment)
        {
            if (segment != null && segment.Count > 0)
            {
                centerSegments.Add(segment);
            }
        }

        /// <summary>
        /// Build ordered centerPixels list from segments by connecting them end-to-end
        /// </summary>
        public void BuildOrderedCenterLine()
        {
            centerPixels.Clear();
            centerPixelSet.Clear();

            if (centerSegments.Count == 0)
                return;

            if (centerSegments.Count == 1)
            {
                // Simple case: only one segment
                foreach (var pixel in centerSegments[0])
                {
                    if (!centerPixelSet.Contains(pixel))
                    {
                        centerPixels.Add(pixel);
                        centerPixelSet.Add(pixel);
                    }
                }
                return;
            }

            // Multiple segments: need to connect them in order
            List<List<Vector2Int>> remainingSegments = new List<List<Vector2Int>>(centerSegments);
            List<Vector2Int> currentPath = remainingSegments[0];
            remainingSegments.RemoveAt(0);

            // Keep connecting segments until all are used
            while (remainingSegments.Count > 0)
            {
                bool foundConnection = false;
                Vector2Int pathEnd = currentPath[currentPath.Count - 1];
                Vector2Int pathStart = currentPath[0];

                // Try to find a segment that connects to the current path
                for (int i = 0; i < remainingSegments.Count; i++)
                {
                    List<Vector2Int> segment = remainingSegments[i];
                    Vector2Int segStart = segment[0];
                    Vector2Int segEnd = segment[segment.Count - 1];

                    // Check if this segment connects to the end of current path
                    if (segStart == pathEnd || (segStart - pathEnd).sqrMagnitude <= 2)
                    {
                        // Add segment to end (skip first pixel if it's duplicate)
                        int startIdx = (segStart == pathEnd) ? 1 : 0;
                        for (int j = startIdx; j < segment.Count; j++)
                        {
                            currentPath.Add(segment[j]);
                        }
                        remainingSegments.RemoveAt(i);
                        foundConnection = true;
                        break;
                    }
                    else if (segEnd == pathEnd || (segEnd - pathEnd).sqrMagnitude <= 2)
                    {
                        // Add reversed segment to end
                        int startIdx = (segEnd == pathEnd) ? segment.Count - 2 : segment.Count - 1;
                        for (int j = startIdx; j >= 0; j--)
                        {
                            currentPath.Add(segment[j]);
                        }
                        remainingSegments.RemoveAt(i);
                        foundConnection = true;
                        break;
                    }
                    // Check if this segment connects to the start of current path
                    else if (segEnd == pathStart || (segEnd - pathStart).sqrMagnitude <= 2)
                    {
                        // Add segment to start (skip last pixel if it's duplicate)
                        int endIdx = (segEnd == pathStart) ? segment.Count - 2 : segment.Count - 1;
                        for (int j = endIdx; j >= 0; j--)
                        {
                            currentPath.Insert(0, segment[j]);
                        }
                        remainingSegments.RemoveAt(i);
                        foundConnection = true;
                        break;
                    }
                    else if (segStart == pathStart || (segStart - pathStart).sqrMagnitude <= 2)
                    {
                        // Add reversed segment to start
                        int startIdx = (segStart == pathStart) ? 1 : 0;
                        for (int j = startIdx; j < segment.Count; j++)
                        {
                            currentPath.Insert(0, segment[j]);
                        }
                        remainingSegments.RemoveAt(i);
                        foundConnection = true;
                        break;
                    }
                }

                // If no connection found, this might be a separate component - just append it
                if (!foundConnection)
                {
                    foreach (var pixel in remainingSegments[0])
                    {
                        currentPath.Add(pixel);
                    }
                    remainingSegments.RemoveAt(0);
                }
            }

            // Copy final path to centerPixels
            foreach (var pixel in currentPath)
            {
                if (!centerPixelSet.Contains(pixel))
                {
                    centerPixels.Add(pixel);
                    centerPixelSet.Add(pixel);
                }
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
