using UnityEngine;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LandOfTheConsumers.Procedural
{
    /// <summary>
    /// Generates line visualization for edge between two regions.
    /// Creates a yellow LineRenderer along the center line showing averaged heights.
    /// </summary>
    public class EdgePairGenerator : MonoBehaviour
    {
        [HideInInspector] public EdgeSpineData edgeData;
        [HideInInspector] public CellularNoiseVisualizer cellularVisualizer;
        [HideInInspector] public RegionQuadVisualizer regionQuadVisualizer;
        [HideInInspector] public Material edgeMaterial;

        private LineRenderer lineRenderer;
        private bool isGenerating = false;

        public System.Action OnGenerationComplete;
        public bool IsGenerating => isGenerating;

        public void GenerateEdgeMesh()
        {
            if (edgeData == null || edgeData.centerPixels.Count == 0)
            {
                Debug.LogWarning($"[EdgePairGenerator] No center pixels for {edgeData?.GetPairName()}");
                OnGenerationComplete?.Invoke();
                return;
            }

            if (isGenerating)
            {
                Debug.LogWarning($"[EdgePairGenerator] Already generating {edgeData.GetPairName()}");
                return;
            }

            StartCoroutine(GenerateEdgeLineCoroutine());
        }

        private IEnumerator GenerateEdgeLineCoroutine()
        {
            isGenerating = true;

            // Clear existing LineRenderer if any
            if (lineRenderer != null)
            {
                if (Application.isPlaying)
                    Destroy(lineRenderer);
                else
                    DestroyImmediate(lineRenderer);
            }

            // Get world dimensions for coordinate conversion
            Vector2Int worldSizeInBiomes = cellularVisualizer.worldSizeInBiomes;
            const int biomeSize = 128;
            float worldWidth = worldSizeInBiomes.x * biomeSize;
            float worldHeight = worldSizeInBiomes.y * biomeSize;
            float halfWidth = worldWidth * 0.5f;
            float halfHeight = worldHeight * 0.5f;
            int pixelsPerUnit = cellularVisualizer.pixelsPerUnit;

            // Note: We don't need bounds for center pixels since we're just drawing a line
            // The bounds calculation is kept for potential future use

            // Get terrain generators for both regions
            if (!regionQuadVisualizer.regionTerrainGenerators.ContainsKey(edgeData.regionIdA) ||
                !regionQuadVisualizer.regionTerrainGenerators.ContainsKey(edgeData.regionIdB))
            {
                Debug.LogError($"[EdgePairGenerator] Terrain generators not found for {edgeData.GetPairName()}");
                isGenerating = false;
                OnGenerationComplete?.Invoke();
                yield break;
            }

            RegionTerrainGenerator terrainGenA = regionQuadVisualizer.regionTerrainGenerators[edgeData.regionIdA];
            RegionTerrainGenerator terrainGenB = regionQuadVisualizer.regionTerrainGenerators[edgeData.regionIdB];

            // Verify terrain generators have height maps
            if (terrainGenA.HeightMap == null || terrainGenB.HeightMap == null)
            {
                Debug.LogError($"[EdgePairGenerator] Terrain generators for {edgeData.GetPairName()} don't have height maps yet!");
                isGenerating = false;
                OnGenerationComplete?.Invoke();
                yield break;
            }

            // Get region bounds from terrain generators (they were calculated during terrain generation)
            int regionA_minX = terrainGenA.RegionMinX;
            int regionA_minY = terrainGenA.RegionMinY;
            int regionB_minX = terrainGenB.RegionMinX;
            int regionB_minY = terrainGenB.RegionMinY;

            // Get heightMap dimensions
            int regionA_terrainWidth = terrainGenA.HeightMap.GetLength(0);
            int regionA_terrainHeight = terrainGenA.HeightMap.GetLength(1);
            int regionB_terrainWidth = terrainGenB.HeightMap.GetLength(0);
            int regionB_terrainHeight = terrainGenB.HeightMap.GetLength(1);

            // Calculate and store perpendicular directions for all center pixels
            CalculatePerpendicularDirections();

            // Calculate heights for center pixels - average of both regions' heights
            edgeData.pixelHeights.Clear();

            Debug.Log($"[EdgePairGenerator] Calculating heights for {edgeData.centerPixels.Count} center pixels");

            for (int i = 0; i < edgeData.centerPixels.Count; i++)
            {
                Vector2Int centerPixel = edgeData.centerPixels[i];
                bool isInFirstFive = i < 5;
                bool isInLastFive = i >= edgeData.centerPixels.Count - 5;

                // Sample height from region A's heightMap
                float heightA = 0f;
                bool hasValidHeightA = false;
                int terrainA_X = (centerPixel.x - regionA_minX) * pixelsPerUnit + pixelsPerUnit / 2;
                int terrainA_Y = (centerPixel.y - regionA_minY) * pixelsPerUnit + pixelsPerUnit / 2;
                if (terrainA_X >= 0 && terrainA_X < regionA_terrainWidth &&
                    terrainA_Y >= 0 && terrainA_Y < regionA_terrainHeight)
                {
                    heightA = terrainGenA.HeightMap[terrainA_X, terrainA_Y];
                    hasValidHeightA = true;
                }

                // If heightA is invalid and in first 5, search forward
                if (!hasValidHeightA && isInFirstFive)
                {
                    heightA = FindValidHeightForward(i, regionA_minX, regionA_minY, regionA_terrainWidth, regionA_terrainHeight, terrainGenA.HeightMap, pixelsPerUnit, out hasValidHeightA);
                    if (hasValidHeightA)
                    {
                        Debug.LogWarning($"[EdgePairGenerator] {edgeData.GetPairName()} - Pixel {i} at {centerPixel} - Using forward heightA");
                    }
                }
                // If heightA is invalid and in last 5, search backward
                else if (!hasValidHeightA && isInLastFive)
                {
                    heightA = FindValidHeightBackward(i, regionA_minX, regionA_minY, regionA_terrainWidth, regionA_terrainHeight, terrainGenA.HeightMap, pixelsPerUnit, out hasValidHeightA);
                    if (hasValidHeightA)
                    {
                        Debug.LogWarning($"[EdgePairGenerator] {edgeData.GetPairName()} - Pixel {i} at {centerPixel} - Using backward heightA");
                    }
                }

                // Sample height from region B's heightMap
                float heightB = 0f;
                bool hasValidHeightB = false;
                int terrainB_X = (centerPixel.x - regionB_minX) * pixelsPerUnit + pixelsPerUnit / 2;
                int terrainB_Y = (centerPixel.y - regionB_minY) * pixelsPerUnit + pixelsPerUnit / 2;
                if (terrainB_X >= 0 && terrainB_X < regionB_terrainWidth &&
                    terrainB_Y >= 0 && terrainB_Y < regionB_terrainHeight)
                {
                    heightB = terrainGenB.HeightMap[terrainB_X, terrainB_Y];
                    hasValidHeightB = true;
                }

                // If heightB is invalid and in first 5, search forward
                if (!hasValidHeightB && isInFirstFive)
                {
                    heightB = FindValidHeightForward(i, regionB_minX, regionB_minY, regionB_terrainWidth, regionB_terrainHeight, terrainGenB.HeightMap, pixelsPerUnit, out hasValidHeightB);
                    if (hasValidHeightB)
                    {
                        Debug.LogWarning($"[EdgePairGenerator] {edgeData.GetPairName()} - Pixel {i} at {centerPixel} - Using forward heightB");
                    }
                }
                // If heightB is invalid and in last 5, search backward
                else if (!hasValidHeightB && isInLastFive)
                {
                    heightB = FindValidHeightBackward(i, regionB_minX, regionB_minY, regionB_terrainWidth, regionB_terrainHeight, terrainGenB.HeightMap, pixelsPerUnit, out hasValidHeightB);
                    if (hasValidHeightB)
                    {
                        Debug.LogWarning($"[EdgePairGenerator] {edgeData.GetPairName()} - Pixel {i} at {centerPixel} - Using backward heightB");
                    }
                }

                // Average the two heights and store for this center pixel
                // If one or both heights are still invalid (0), the average will reflect that
                float averagedHeight = (heightA + heightB) * 0.5f;
                edgeData.pixelHeights[centerPixel] = averagedHeight;
            }

            // Validate critical pixels (first 3 and last 3) have proper height data
            ValidateCriticalPixelHeights();

            // Apply smoothing pass to reduce spikes, steps, and flats
            SmoothEdgeHeights();

            // Apply final neighbor smoothing (35% from each neighbor)
            ApplyFinalNeighborSmoothing();

            // Create LineRenderer for the center line
            lineRenderer = gameObject.AddComponent<LineRenderer>();

            // Configure LineRenderer appearance
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = Color.yellow;
            lineRenderer.endColor = Color.yellow;
            lineRenderer.startWidth = 0.1f;
            lineRenderer.endWidth = 0.1f;

            // Build smoothed line with interpolated points (2x density)
            List<Vector3> linePoints = new List<Vector3>();

            for (int i = 0; i < edgeData.centerPixels.Count; i++)
            {
                Vector2Int centerPixel = edgeData.centerPixels[i];

                // Get the smoothed height for this pixel
                float height = edgeData.pixelHeights.ContainsKey(centerPixel) ? edgeData.pixelHeights[centerPixel] : 0f;

                // Convert pixel coordinates to world coordinates
                float worldX = (float)centerPixel.x / pixelsPerUnit - halfWidth;
                float worldZ = (float)centerPixel.y / pixelsPerUnit - halfHeight;

                // Add small offset to prevent z-fighting
                height += 0.02f;

                Vector3 worldPos = new Vector3(worldX, height, worldZ);
                linePoints.Add(worldPos);

                // Add interpolated point between this pixel and the next (except for last pixel)
                if (i < edgeData.centerPixels.Count - 1)
                {
                    Vector2Int nextPixel = edgeData.centerPixels[i + 1];

                    // Get smoothed height for next pixel
                    float nextHeight = edgeData.pixelHeights.ContainsKey(nextPixel) ? edgeData.pixelHeights[nextPixel] : 0f;
                    nextHeight += 0.02f;

                    // Interpolate position and height (50% between neighbors)
                    float interpWorldX = (float)nextPixel.x / pixelsPerUnit - halfWidth;
                    float interpWorldZ = (float)nextPixel.y / pixelsPerUnit - halfHeight;

                    Vector3 interpPos = new Vector3(
                        (worldX + interpWorldX) * 0.5f,
                        (height + nextHeight) * 0.5f,
                        (worldZ + interpWorldZ) * 0.5f
                    );

                    linePoints.Add(interpPos);
                }
            }

            // Apply extra smoothing pass to the line points
            Vector3[] smoothedLinePoints = ApplyLineSmoothing(linePoints.ToArray());

            // Set smoothed positions to LineRenderer
            lineRenderer.positionCount = smoothedLinePoints.Length;
            lineRenderer.SetPositions(smoothedLinePoints);

            isGenerating = false;
            Debug.Log($"[EdgePairGenerator] Completed {edgeData.GetPairName()} with {smoothedLinePoints.Length} line points (from {edgeData.centerPixels.Count} center pixels)");

            // Generate the edge spread mesh
            EdgeSpreader edgeSpreader = GetComponent<EdgeSpreader>();
            if (edgeSpreader != null)
            {
                edgeSpreader.SetupEdgeSpread();
            }

            OnGenerationComplete?.Invoke();
        }

        /// <summary>
        /// Applies an additional smoothing pass to the line points
        /// </summary>
        private Vector3[] ApplyLineSmoothing(Vector3[] linePoints)
        {
            if (linePoints.Length < 3)
                return linePoints; // No smoothing needed for very short lines

            Vector3[] smoothedPoints = new Vector3[linePoints.Length];

            for (int i = 0; i < linePoints.Length; i++)
            {
                if (i == 0)
                {
                    // First point: blend with next point
                    smoothedPoints[i] = linePoints[i] * 0.4f + linePoints[i + 1] * 0.6f;
                }
                else if (i == linePoints.Length - 1)
                {
                    // Last point: blend with previous point
                    smoothedPoints[i] = linePoints[i] * 0.4f + linePoints[i - 1] * 0.6f;
                }
                else
                {
                    // Middle points: blend with neighbors
                    smoothedPoints[i] = linePoints[i] * 0.2f + linePoints[i - 1] * 0.4f + linePoints[i + 1] * 0.4f;
                }
            }

            return smoothedPoints;
        }

        /// <summary>
        /// Searches forward through pixels to find valid height data for a region
        /// </summary>
        private float FindValidHeightForward(int startIndex, int regionMinX, int regionMinY, int terrainWidth, int terrainHeight, float[,] heightMap, int pixelsPerUnit, out bool foundValid)
        {
            foundValid = false;

            // Search forward through remaining pixels
            for (int j = startIndex + 1; j < edgeData.centerPixels.Count; j++)
            {
                Vector2Int searchPixel = edgeData.centerPixels[j];
                int terrainX = (searchPixel.x - regionMinX) * pixelsPerUnit + pixelsPerUnit / 2;
                int terrainY = (searchPixel.y - regionMinY) * pixelsPerUnit + pixelsPerUnit / 2;

                if (terrainX >= 0 && terrainX < terrainWidth &&
                    terrainY >= 0 && terrainY < terrainHeight)
                {
                    foundValid = true;
                    return heightMap[terrainX, terrainY];
                }
            }

            // If no valid height found forward, return 0
            return 0f;
        }

        /// <summary>
        /// Searches backward through pixels to find valid height data for a region
        /// </summary>
        private float FindValidHeightBackward(int startIndex, int regionMinX, int regionMinY, int terrainWidth, int terrainHeight, float[,] heightMap, int pixelsPerUnit, out bool foundValid)
        {
            foundValid = false;

            // Search backward through previous pixels
            for (int j = startIndex - 1; j >= 0; j--)
            {
                Vector2Int searchPixel = edgeData.centerPixels[j];
                int terrainX = (searchPixel.x - regionMinX) * pixelsPerUnit + pixelsPerUnit / 2;
                int terrainY = (searchPixel.y - regionMinY) * pixelsPerUnit + pixelsPerUnit / 2;

                if (terrainX >= 0 && terrainX < terrainWidth &&
                    terrainY >= 0 && terrainY < terrainHeight)
                {
                    foundValid = true;
                    return heightMap[terrainX, terrainY];
                }
            }

            // If no valid height found backward, return 0
            return 0f;
        }

        /// <summary>
        /// Validates that critical pixels (first 5 and last 5) have proper height data
        /// </summary>
        private void ValidateCriticalPixelHeights()
        {
            if (edgeData.centerPixels.Count < 10)
            {
                Debug.LogWarning($"[EdgePairGenerator] {edgeData.GetPairName()} has only {edgeData.centerPixels.Count} pixels - cannot validate first/last 5");
                return;
            }

            // Check first 5 pixels
            for (int i = 0; i < 5; i++)
            {
                Vector2Int pixel = edgeData.centerPixels[i];
                if (!edgeData.pixelHeights.ContainsKey(pixel))
                {
                    Debug.LogWarning($"[EdgePairGenerator] {edgeData.GetPairName()} - First pixel {i} at {pixel} is missing height data!");
                }
                else if (Mathf.Approximately(edgeData.pixelHeights[pixel], 0f))
                {
                    Debug.LogWarning($"[EdgePairGenerator] {edgeData.GetPairName()} - First pixel {i} at {pixel} has zero height (may indicate sampling issue)");
                }
            }

            // Check last 5 pixels
            int count = edgeData.centerPixels.Count;
            for (int i = count - 5; i < count; i++)
            {
                Vector2Int pixel = edgeData.centerPixels[i];
                if (!edgeData.pixelHeights.ContainsKey(pixel))
                {
                    Debug.LogWarning($"[EdgePairGenerator] {edgeData.GetPairName()} - Last pixel {count - 1 - i} at {pixel} is missing height data!");
                }
                else if (Mathf.Approximately(edgeData.pixelHeights[pixel], 0f))
                {
                    Debug.LogWarning($"[EdgePairGenerator] {edgeData.GetPairName()} - Last pixel {count - 1 - i} at {pixel} has zero height (may indicate sampling issue)");
                }
            }

            Debug.Log($"[EdgePairGenerator] {edgeData.GetPairName()} - Validated critical pixels (first 5 and last 5)");
        }

        /// <summary>
        /// Smooths edge heights with two passes:
        /// First pass (start to end): 50% from each neighbor
        /// Second pass (end to start): 50% from each neighbor
        /// First and last pixels only have one neighbor.
        /// </summary>
        private void SmoothEdgeHeights()
        {
            if (edgeData.centerPixels.Count < 2)
                return; // No smoothing needed for single pixel

            // FIRST PASS: Start to end with 40% smoothing
            Dictionary<Vector2Int, float> firstPassHeights = new Dictionary<Vector2Int, float>();

            for (int i = 0; i < edgeData.centerPixels.Count; i++)
            {
                Vector2Int currentPixel = edgeData.centerPixels[i];
                float currentHeight = edgeData.pixelHeights.ContainsKey(currentPixel) ? edgeData.pixelHeights[currentPixel] : 0f;

                float smoothedHeight = currentHeight;

                bool isFirstPixel = (i == 0);
                bool isLastPixel = (i == edgeData.centerPixels.Count - 1);

                // First pass uses 50% smoothing from neighbors
                float smoothingAmount = 0.5f;

                // Add smoothing from previous pixel (if exists)
                if (i > 0)
                {
                    Vector2Int prevPixel = edgeData.centerPixels[i - 1];
                    float prevHeight = edgeData.pixelHeights.ContainsKey(prevPixel) ? edgeData.pixelHeights[prevPixel] : 0f;
                    smoothedHeight += smoothingAmount * (prevHeight - currentHeight);
                }

                // Add smoothing from next pixel (if exists)
                if (i < edgeData.centerPixels.Count - 1)
                {
                    Vector2Int nextPixel = edgeData.centerPixels[i + 1];
                    float nextHeight = edgeData.pixelHeights.ContainsKey(nextPixel) ? edgeData.pixelHeights[nextPixel] : 0f;
                    smoothedHeight += smoothingAmount * (nextHeight - currentHeight);
                }

                firstPassHeights[currentPixel] = smoothedHeight;
            }

            // SECOND PASS: End to start with 30% smoothing
            Dictionary<Vector2Int, float> secondPassHeights = new Dictionary<Vector2Int, float>();

            for (int i = edgeData.centerPixels.Count - 1; i >= 0; i--)
            {
                Vector2Int currentPixel = edgeData.centerPixels[i];
                float currentHeight = firstPassHeights.ContainsKey(currentPixel) ? firstPassHeights[currentPixel] : 0f;

                float smoothedHeight = currentHeight;

                bool isFirstPixel = (i == 0);
                bool isLastPixel = (i == edgeData.centerPixels.Count - 1);

                // Second pass uses 50% smoothing from neighbors
                float smoothingAmount = 0.5f;

                // Add smoothing from previous pixel (if exists)
                if (i > 0)
                {
                    Vector2Int prevPixel = edgeData.centerPixels[i - 1];
                    float prevHeight = firstPassHeights.ContainsKey(prevPixel) ? firstPassHeights[prevPixel] : 0f;
                    smoothedHeight += smoothingAmount * (prevHeight - currentHeight);
                }

                // Add smoothing from next pixel (if exists)
                if (i < edgeData.centerPixels.Count - 1)
                {
                    Vector2Int nextPixel = edgeData.centerPixels[i + 1];
                    float nextHeight = firstPassHeights.ContainsKey(nextPixel) ? firstPassHeights[nextPixel] : 0f;
                    smoothedHeight += smoothingAmount * (nextHeight - currentHeight);
                }

                secondPassHeights[currentPixel] = smoothedHeight;
            }

            // Replace original heights with final smoothed heights
            edgeData.pixelHeights = secondPassHeights;

            Debug.Log($"[EdgePairGenerator] Applied two-pass smoothing (50% forward, 50% backward) to {secondPassHeights.Count} edge heights");
        }

        /// <summary>
        /// Final smoothing pass: for each point, check both neighbors and apply 70% of height difference.
        /// Uses a copy of heights to read from, then applies changes to the original.
        /// </summary>
        private void ApplyFinalNeighborSmoothing()
        {
            if (edgeData.centerPixels.Count < 3)
                return; // Need at least 3 points for neighbor smoothing

            // Make a copy of current heights to read from
            Dictionary<Vector2Int, float> heightsCopy = new Dictionary<Vector2Int, float>(edgeData.pixelHeights);

            float smoothingFactor = 0.7f;

            for (int i = 0; i < edgeData.centerPixels.Count; i++)
            {
                Vector2Int currentPixel = edgeData.centerPixels[i];
                float currentHeight = heightsCopy.ContainsKey(currentPixel) ? heightsCopy[currentPixel] : 0f;

                float adjustment = 0f;

                // Check previous neighbor
                if (i > 0)
                {
                    Vector2Int prevPixel = edgeData.centerPixels[i - 1];
                    float prevHeight = heightsCopy.ContainsKey(prevPixel) ? heightsCopy[prevPixel] : 0f;
                    adjustment += smoothingFactor * (prevHeight - currentHeight);
                }

                // Check next neighbor
                if (i < edgeData.centerPixels.Count - 1)
                {
                    Vector2Int nextPixel = edgeData.centerPixels[i + 1];
                    float nextHeight = heightsCopy.ContainsKey(nextPixel) ? heightsCopy[nextPixel] : 0f;
                    adjustment += smoothingFactor * (nextHeight - currentHeight);
                }

                // Apply adjustment to original heights
                edgeData.pixelHeights[currentPixel] = currentHeight + adjustment;
            }

            Debug.Log($"[EdgePairGenerator] Applied final neighbor smoothing (70%) to {edgeData.pixelHeights.Count} edge heights");
        }

        /// <summary>
        /// Calculates a single perpendicular direction vector (1 unit length) for this edge
        /// Uses the overall direction from start to end of the edge
        /// </summary>
        private void CalculatePerpendicularDirections()
        {
            if (edgeData.centerPixels.Count == 0)
            {
                Debug.LogWarning($"[EdgePairGenerator] No center pixels to calculate perpendicular for {edgeData.GetPairName()}");
                edgeData.perpendicularDirection = Vector2.zero;
                return;
            }

            if (edgeData.centerPixels.Count == 1)
            {
                // Only one pixel - no meaningful direction
                edgeData.perpendicularDirection = Vector2.zero;
                return;
            }

            // Calculate overall edge direction from start to end
            Vector2Int startPixel = edgeData.centerPixels[0];
            Vector2Int endPixel = edgeData.centerPixels[edgeData.centerPixels.Count - 1];

            Vector2 edgeDirection = new Vector2(endPixel.x - startPixel.x, endPixel.y - startPixel.y);

            // Get perpendicular (90 degrees counter-clockwise)
            edgeData.perpendicularDirection = GetPerpendicularXZ2D(edgeDirection);

            Debug.Log($"[EdgePairGenerator] Calculated perpendicular direction {edgeData.perpendicularDirection} for {edgeData.GetPairName()}");
        }

        /// <summary>
        /// Gets the perpendicular vector in 2D (rotated 90 degrees left)
        /// For direction (x, y), perpendicular is (-y, x)
        /// </summary>
        private Vector2 GetPerpendicularXZ2D(Vector2 direction)
        {
            // Perpendicular in 2D (rotate 90 degrees counter-clockwise)
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);

            // Normalize
            float length = perpendicular.magnitude;
            if (length > 0.0001f)
            {
                perpendicular /= length;
            }

            return perpendicular;
        }
    }
}
