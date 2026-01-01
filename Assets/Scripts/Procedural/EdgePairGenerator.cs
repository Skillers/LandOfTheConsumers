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
        [HideInInspector] public EdgeData edgeData;
        [HideInInspector] public CellularNoiseVisualizer cellularVisualizer;
        [HideInInspector] public RegionQuadVisualizer regionQuadVisualizer;
        [HideInInspector] public Material edgeMaterial;

        private const int pixelsPerUnit = 2;  // LOD0 only

        private LineRenderer lineRenderer;
        private bool isGenerating = false;
        private Dictionary<Vector2Int, float> centerPixelHeights; // Height for each center pixel

        // Temporary PerlinSettings for height calculation
        private PerlinSettings tempSettingsA;
        private PerlinSettings tempSettingsB;

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
            int cellularPixelsPerUnit = cellularVisualizer.pixelsPerUnit;

            // Note: We don't need bounds for center pixels since we're just drawing a line
            // The bounds calculation is kept for potential future use

            // Get presets for both regions
            if (!regionQuadVisualizer.regionPresetMapping.ContainsKey(edgeData.regionIdA) ||
                !regionQuadVisualizer.regionPresetMapping.ContainsKey(edgeData.regionIdB))
            {
                Debug.LogError($"[EdgePairGenerator] Region presets not found for {edgeData.GetPairName()}");
                isGenerating = false;
                OnGenerationComplete?.Invoke();
                yield break;
            }

            TerrainNoisePreset presetA = regionQuadVisualizer.regionPresetMapping[edgeData.regionIdA];
            TerrainNoisePreset presetB = regionQuadVisualizer.regionPresetMapping[edgeData.regionIdB];

            // Create temporary PerlinSettings for height calculation
            tempSettingsA = gameObject.AddComponent<PerlinSettings>();
            tempSettingsA.ApplyPreset(presetA);
            tempSettingsB = gameObject.AddComponent<PerlinSettings>();
            tempSettingsB.ApplyPreset(presetB);

            // Get the actual regions to find their bounds
            var regions = cellularVisualizer.Regions;
            if (regions == null || edgeData.regionIdA >= regions.Count || edgeData.regionIdB >= regions.Count)
            {
                Debug.LogError($"[EdgePairGenerator] Cannot access regions {edgeData.regionIdA} or {edgeData.regionIdB}");
                isGenerating = false;
                OnGenerationComplete?.Invoke();
                yield break;
            }

            CellularRegion regionA = regions[edgeData.regionIdA];
            CellularRegion regionB = regions[edgeData.regionIdB];

            // Calculate bounds for each region (same logic as RegionTerrainGenerator)
            int regionA_minX = int.MaxValue, regionA_minY = int.MaxValue;
            int regionA_maxX = int.MinValue, regionA_maxY = int.MinValue;
            foreach (var pixel in regionA.pixels)
            {
                regionA_minX = Mathf.Min(regionA_minX, pixel.x);
                regionA_minY = Mathf.Min(regionA_minY, pixel.y);
                regionA_maxX = Mathf.Max(regionA_maxX, pixel.x);
                regionA_maxY = Mathf.Max(regionA_maxY, pixel.y);
            }

            int regionB_minX = int.MaxValue, regionB_minY = int.MaxValue;
            int regionB_maxX = int.MinValue, regionB_maxY = int.MinValue;
            foreach (var pixel in regionB.pixels)
            {
                regionB_minX = Mathf.Min(regionB_minX, pixel.x);
                regionB_minY = Mathf.Min(regionB_minY, pixel.y);
                regionB_maxX = Mathf.Max(regionB_maxX, pixel.x);
                regionB_maxY = Mathf.Max(regionB_maxY, pixel.y);
            }

            int regionA_terrainWidth = (regionA_maxX - regionA_minX + 1) * pixelsPerUnit;
            int regionA_terrainHeight = (regionA_maxY - regionA_minY + 1) * pixelsPerUnit;
            int regionB_terrainWidth = (regionB_maxX - regionB_minX + 1) * pixelsPerUnit;
            int regionB_terrainHeight = (regionB_maxY - regionB_minY + 1) * pixelsPerUnit;

            // Calculate heights for center pixels - average of both regions' heights
            centerPixelHeights = new Dictionary<Vector2Int, float>();

            Debug.Log($"[EdgePairGenerator] Calculating heights for {edgeData.centerPixels.Count} center pixels");

            for (int i = 0; i < edgeData.centerPixels.Count; i++)
            {
                Vector2Int centerPixel = edgeData.centerPixels[i];
                bool isInFirstFive = i < 5;
                bool isInLastFive = i >= edgeData.centerPixels.Count - 5;

                // Calculate height from region A's perspective
                float heightA = 0f;
                bool hasValidHeightA = false;
                int terrainA_X = (centerPixel.x - regionA_minX) * pixelsPerUnit + pixelsPerUnit / 2;
                int terrainA_Y = (centerPixel.y - regionA_minY) * pixelsPerUnit + pixelsPerUnit / 2;
                if (terrainA_X >= 0 && terrainA_X < regionA_terrainWidth &&
                    terrainA_Y >= 0 && terrainA_Y < regionA_terrainHeight)
                {
                    heightA = CalculateHeightAtTerrain(terrainA_X, terrainA_Y, regionA_terrainWidth, regionA_terrainHeight, tempSettingsA);
                    hasValidHeightA = true;
                }

                // If heightA is invalid and in first 5, search forward
                if (!hasValidHeightA && isInFirstFive)
                {
                    heightA = FindValidHeightForward(i, regionA_minX, regionA_minY, regionA_terrainWidth, regionA_terrainHeight, tempSettingsA, out hasValidHeightA);
                    if (hasValidHeightA)
                    {
                        Debug.LogWarning($"[EdgePairGenerator] {edgeData.GetPairName()} - Pixel {i} at {centerPixel} - Using forward heightA");
                    }
                }
                // If heightA is invalid and in last 5, search backward
                else if (!hasValidHeightA && isInLastFive)
                {
                    heightA = FindValidHeightBackward(i, regionA_minX, regionA_minY, regionA_terrainWidth, regionA_terrainHeight, tempSettingsA, out hasValidHeightA);
                    if (hasValidHeightA)
                    {
                        Debug.LogWarning($"[EdgePairGenerator] {edgeData.GetPairName()} - Pixel {i} at {centerPixel} - Using backward heightA");
                    }
                }

                // Calculate height from region B's perspective
                float heightB = 0f;
                bool hasValidHeightB = false;
                int terrainB_X = (centerPixel.x - regionB_minX) * pixelsPerUnit + pixelsPerUnit / 2;
                int terrainB_Y = (centerPixel.y - regionB_minY) * pixelsPerUnit + pixelsPerUnit / 2;
                if (terrainB_X >= 0 && terrainB_X < regionB_terrainWidth &&
                    terrainB_Y >= 0 && terrainB_Y < regionB_terrainHeight)
                {
                    heightB = CalculateHeightAtTerrain(terrainB_X, terrainB_Y, regionB_terrainWidth, regionB_terrainHeight, tempSettingsB);
                    hasValidHeightB = true;
                }

                // If heightB is invalid and in first 5, search forward
                if (!hasValidHeightB && isInFirstFive)
                {
                    heightB = FindValidHeightForward(i, regionB_minX, regionB_minY, regionB_terrainWidth, regionB_terrainHeight, tempSettingsB, out hasValidHeightB);
                    if (hasValidHeightB)
                    {
                        Debug.LogWarning($"[EdgePairGenerator] {edgeData.GetPairName()} - Pixel {i} at {centerPixel} - Using forward heightB");
                    }
                }
                // If heightB is invalid and in last 5, search backward
                else if (!hasValidHeightB && isInLastFive)
                {
                    heightB = FindValidHeightBackward(i, regionB_minX, regionB_minY, regionB_terrainWidth, regionB_terrainHeight, tempSettingsB, out hasValidHeightB);
                    if (hasValidHeightB)
                    {
                        Debug.LogWarning($"[EdgePairGenerator] {edgeData.GetPairName()} - Pixel {i} at {centerPixel} - Using backward heightB");
                    }
                }

                // Average the two heights and store for this center pixel
                // If one or both heights are still invalid (0), the average will reflect that
                float averagedHeight = (heightA + heightB) * 0.5f;
                centerPixelHeights[centerPixel] = averagedHeight;
            }

            // Validate critical pixels (first 3 and last 3) have proper height data
            ValidateCriticalPixelHeights();

            // Apply smoothing pass to reduce spikes, steps, and flats
            SmoothEdgeHeights();

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
                float height = centerPixelHeights.ContainsKey(centerPixel) ? centerPixelHeights[centerPixel] : 0f;

                // Convert pixel coordinates to world coordinates
                float worldX = (float)centerPixel.x / cellularPixelsPerUnit - halfWidth;
                float worldZ = (float)centerPixel.y / cellularPixelsPerUnit - halfHeight;

                // Add small offset to prevent z-fighting
                height += 0.02f;

                Vector3 worldPos = new Vector3(worldX, height, worldZ);
                linePoints.Add(worldPos);

                // Add interpolated point between this pixel and the next (except for last pixel)
                if (i < edgeData.centerPixels.Count - 1)
                {
                    Vector2Int nextPixel = edgeData.centerPixels[i + 1];

                    // Get smoothed height for next pixel
                    float nextHeight = centerPixelHeights.ContainsKey(nextPixel) ? centerPixelHeights[nextPixel] : 0f;
                    nextHeight += 0.02f;

                    // Interpolate position and height (50% between neighbors)
                    float interpWorldX = (float)nextPixel.x / cellularPixelsPerUnit - halfWidth;
                    float interpWorldZ = (float)nextPixel.y / cellularPixelsPerUnit - halfHeight;

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

            // Create 2 child objects with exact copies of the smoothed line data
            Vector3[] leftLinePoints;
            Vector3[] rightLinePoints;
            CreateChildEdgeLines(smoothedLinePoints, out leftLinePoints, out rightLinePoints);

            // Create mesh from the 3 lines
            CreateEdgeMesh(smoothedLinePoints, leftLinePoints, rightLinePoints);

            isGenerating = false;
            Debug.Log($"[EdgePairGenerator] Completed {edgeData.GetPairName()} with {smoothedLinePoints.Length} line points (from {edgeData.centerPixels.Count} center pixels)");
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
        /// Creates 2 child objects with exact copies of the parent line, offset perpendicular
        /// Left (red) at -0.5 units, Right (blue) at +0.5 units
        /// </summary>
        private void CreateChildEdgeLines(Vector3[] linePoints, out Vector3[] leftLinePoints, out Vector3[] rightLinePoints)
        {
            // Calculate perpendicular offsets
            leftLinePoints = CalculatePerpendicularOffset(linePoints, -0.5f);
            rightLinePoints = CalculatePerpendicularOffset(linePoints, 0.5f);

            // Create left child (red) - offset to left
            GameObject leftChild = new GameObject("EdgeLine_Left");
            leftChild.transform.SetParent(transform);
            leftChild.transform.localPosition = Vector3.zero;
            leftChild.transform.localRotation = Quaternion.identity;
            leftChild.transform.localScale = Vector3.one;

            LineRenderer leftLineRenderer = leftChild.AddComponent<LineRenderer>();
            leftLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            leftLineRenderer.startColor = Color.red;
            leftLineRenderer.endColor = Color.red;
            leftLineRenderer.startWidth = 0.1f;
            leftLineRenderer.endWidth = 0.1f;
            leftLineRenderer.positionCount = leftLinePoints.Length;
            leftLineRenderer.SetPositions(leftLinePoints);

            // Create right child (blue) - offset to right
            GameObject rightChild = new GameObject("EdgeLine_Right");
            rightChild.transform.SetParent(transform);
            rightChild.transform.localPosition = Vector3.zero;
            rightChild.transform.localRotation = Quaternion.identity;
            rightChild.transform.localScale = Vector3.one;

            LineRenderer rightLineRenderer = rightChild.AddComponent<LineRenderer>();
            rightLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            rightLineRenderer.startColor = Color.blue;
            rightLineRenderer.endColor = Color.blue;
            rightLineRenderer.startWidth = 0.1f;
            rightLineRenderer.endWidth = 0.1f;
            rightLineRenderer.positionCount = rightLinePoints.Length;
            rightLineRenderer.SetPositions(rightLinePoints);

            Debug.Log($"[EdgePairGenerator] Created 2 offset child edge lines for {edgeData.GetPairName()}");
        }

        /// <summary>
        /// Creates a mesh from the 3 lines (center, left, right)
        /// </summary>
        private void CreateEdgeMesh(Vector3[] centerPoints, Vector3[] leftPoints, Vector3[] rightPoints)
        {
            if (centerPoints.Length < 2 || leftPoints.Length != centerPoints.Length || rightPoints.Length != centerPoints.Length)
            {
                Debug.LogWarning($"[EdgePairGenerator] Cannot create mesh - line lengths don't match or too short");
                return;
            }

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            // Add all vertices from all three lines (convert from world to local space)
            // We'll interleave them: for each point index, add left, center, right
            for (int i = 0; i < centerPoints.Length; i++)
            {
                // Convert world space positions to local space relative to this GameObject
                vertices.Add(transform.InverseTransformPoint(leftPoints[i]));   // Index: i * 3 + 0
                vertices.Add(transform.InverseTransformPoint(centerPoints[i])); // Index: i * 3 + 1
                vertices.Add(transform.InverseTransformPoint(rightPoints[i]));  // Index: i * 3 + 2
            }

            // Create triangles connecting the strips
            for (int i = 0; i < centerPoints.Length - 1; i++)
            {
                int baseIndex = i * 3;
                int nextBaseIndex = (i + 1) * 3;

                // Left strip (between left and center lines)
                // Triangle 1: left[i], center[i], left[i+1]
                triangles.Add(baseIndex + 0);      // left[i]
                triangles.Add(baseIndex + 1);      // center[i]
                triangles.Add(nextBaseIndex + 0);  // left[i+1]

                // Triangle 2: center[i], center[i+1], left[i+1]
                triangles.Add(baseIndex + 1);      // center[i]
                triangles.Add(nextBaseIndex + 1);  // center[i+1]
                triangles.Add(nextBaseIndex + 0);  // left[i+1]

                // Right strip (between center and right lines)
                // Triangle 1: center[i], right[i], center[i+1]
                triangles.Add(baseIndex + 1);      // center[i]
                triangles.Add(baseIndex + 2);      // right[i]
                triangles.Add(nextBaseIndex + 1);  // center[i+1]

                // Triangle 2: right[i], right[i+1], center[i+1]
                triangles.Add(baseIndex + 2);      // right[i]
                triangles.Add(nextBaseIndex + 2);  // right[i+1]
                triangles.Add(nextBaseIndex + 1);  // center[i+1]
            }

            // Create mesh
            Mesh edgeMesh = new Mesh();
            edgeMesh.name = $"{edgeData.GetPairName()}_Mesh";
            edgeMesh.vertices = vertices.ToArray();
            edgeMesh.triangles = triangles.ToArray();
            edgeMesh.RecalculateNormals();
            edgeMesh.RecalculateBounds();

            // Create GameObject for the mesh
            GameObject meshObject = new GameObject("EdgeMesh");
            meshObject.transform.SetParent(transform);
            meshObject.transform.localPosition = Vector3.zero;
            meshObject.transform.localRotation = Quaternion.identity;
            meshObject.transform.localScale = Vector3.one;

            MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
            meshFilter.mesh = edgeMesh;

            MeshRenderer meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshRenderer.material = edgeMaterial != null ? edgeMaterial : new Material(Shader.Find("Standard"));

            Debug.Log($"[EdgePairGenerator] Created mesh with {vertices.Count} vertices and {triangles.Count / 3} triangles for {edgeData.GetPairName()}");
        }

        /// <summary>
        /// Calculates perpendicular offset positions for a line
        /// </summary>
        private Vector3[] CalculatePerpendicularOffset(Vector3[] linePoints, float offsetDistance)
        {
            Vector3[] offsetPoints = new Vector3[linePoints.Length];

            for (int i = 0; i < linePoints.Length; i++)
            {
                Vector3 perpendicular = Vector3.zero;

                if (i == 0 && linePoints.Length > 1)
                {
                    // First point: use direction to next point
                    Vector3 forward = linePoints[i + 1] - linePoints[i];
                    perpendicular = GetPerpendicularXZ(forward);
                }
                else if (i == linePoints.Length - 1 && linePoints.Length > 1)
                {
                    // Last point: use direction from previous point
                    Vector3 forward = linePoints[i] - linePoints[i - 1];
                    perpendicular = GetPerpendicularXZ(forward);
                }
                else if (linePoints.Length > 1)
                {
                    // Middle points: average perpendicular of both segments
                    Vector3 forwardPrev = linePoints[i] - linePoints[i - 1];
                    Vector3 forwardNext = linePoints[i + 1] - linePoints[i];
                    Vector3 perp1 = GetPerpendicularXZ(forwardPrev);
                    Vector3 perp2 = GetPerpendicularXZ(forwardNext);
                    perpendicular = (perp1 + perp2).normalized;
                }

                offsetPoints[i] = linePoints[i] + perpendicular * offsetDistance;
            }

            return offsetPoints;
        }

        /// <summary>
        /// Gets the perpendicular vector in the XZ plane (rotated 90 degrees left)
        /// </summary>
        private Vector3 GetPerpendicularXZ(Vector3 direction)
        {
            // Perpendicular in XZ plane (rotate 90 degrees counter-clockwise around Y axis)
            // For direction (x, y, z), perpendicular is (-z, y, x)
            Vector3 perpendicular = new Vector3(-direction.z, 0, direction.x);

            // Normalize only the XZ components
            float length = Mathf.Sqrt(perpendicular.x * perpendicular.x + perpendicular.z * perpendicular.z);
            if (length > 0.0001f)
            {
                perpendicular.x /= length;
                perpendicular.z /= length;
            }

            return perpendicular;
        }

        private float CalculateHeightAtTerrain(int x, int y, int width, int height, PerlinSettings settings)
        {
            float xCoord = (float)x / width * settings.scale * settings.xScale
                           + settings.offSetX / (settings.scale * settings.xScale) / 2f;
            float yCoord = (float)y / height * settings.scale * settings.zScale
                           + settings.offSetY / (settings.scale * settings.zScale) / 2f;

            float sample = Mathf.PerlinNoise(xCoord, yCoord);
            return sample * settings.heightMultiplier;
        }

        /// <summary>
        /// Searches forward through pixels to find valid height data for a region
        /// </summary>
        private float FindValidHeightForward(int startIndex, int regionMinX, int regionMinY, int terrainWidth, int terrainHeight, PerlinSettings settings, out bool foundValid)
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
                    return CalculateHeightAtTerrain(terrainX, terrainY, terrainWidth, terrainHeight, settings);
                }
            }

            // If no valid height found forward, return 0
            return 0f;
        }

        /// <summary>
        /// Searches backward through pixels to find valid height data for a region
        /// </summary>
        private float FindValidHeightBackward(int startIndex, int regionMinX, int regionMinY, int terrainWidth, int terrainHeight, PerlinSettings settings, out bool foundValid)
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
                    return CalculateHeightAtTerrain(terrainX, terrainY, terrainWidth, terrainHeight, settings);
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
                if (!centerPixelHeights.ContainsKey(pixel))
                {
                    Debug.LogWarning($"[EdgePairGenerator] {edgeData.GetPairName()} - First pixel {i} at {pixel} is missing height data!");
                }
                else if (Mathf.Approximately(centerPixelHeights[pixel], 0f))
                {
                    Debug.LogWarning($"[EdgePairGenerator] {edgeData.GetPairName()} - First pixel {i} at {pixel} has zero height (may indicate sampling issue)");
                }
            }

            // Check last 5 pixels
            int count = edgeData.centerPixels.Count;
            for (int i = count - 5; i < count; i++)
            {
                Vector2Int pixel = edgeData.centerPixels[i];
                if (!centerPixelHeights.ContainsKey(pixel))
                {
                    Debug.LogWarning($"[EdgePairGenerator] {edgeData.GetPairName()} - Last pixel {count - 1 - i} at {pixel} is missing height data!");
                }
                else if (Mathf.Approximately(centerPixelHeights[pixel], 0f))
                {
                    Debug.LogWarning($"[EdgePairGenerator] {edgeData.GetPairName()} - Last pixel {count - 1 - i} at {pixel} has zero height (may indicate sampling issue)");
                }
            }

            Debug.Log($"[EdgePairGenerator] {edgeData.GetPairName()} - Validated critical pixels (first 5 and last 5)");
        }

        /// <summary>
        /// Smooths edge heights to reduce spikes, steps, and flats.
        /// Middle pixels take 40% from each side (prev and next).
        /// First and last pixels take 60% from their single neighbor.
        /// </summary>
        private void SmoothEdgeHeights()
        {
            if (edgeData.centerPixels.Count < 2)
                return; // No smoothing needed for single pixel

            Dictionary<Vector2Int, float> smoothedHeights = new Dictionary<Vector2Int, float>();

            for (int i = 0; i < edgeData.centerPixels.Count; i++)
            {
                Vector2Int currentPixel = edgeData.centerPixels[i];
                float currentHeight = centerPixelHeights.ContainsKey(currentPixel) ? centerPixelHeights[currentPixel] : 0f;

                float smoothedHeight = currentHeight;

                bool isFirstPixel = (i == 0);
                bool isLastPixel = (i == edgeData.centerPixels.Count - 1);

                // Determine smoothing amount based on position
                float smoothingAmount = (isFirstPixel || isLastPixel) ? 0.6f : 0.4f;

                // Add smoothing from previous pixel
                if (i > 0)
                {
                    Vector2Int prevPixel = edgeData.centerPixels[i - 1];
                    float prevHeight = centerPixelHeights.ContainsKey(prevPixel) ? centerPixelHeights[prevPixel] : 0f;
                    smoothedHeight += smoothingAmount * (prevHeight - currentHeight);
                }

                // Add smoothing from next pixel
                if (i < edgeData.centerPixels.Count - 1)
                {
                    Vector2Int nextPixel = edgeData.centerPixels[i + 1];
                    float nextHeight = centerPixelHeights.ContainsKey(nextPixel) ? centerPixelHeights[nextPixel] : 0f;
                    smoothedHeight += smoothingAmount * (nextHeight - currentHeight);
                }

                smoothedHeights[currentPixel] = smoothedHeight;
            }

            // Replace original heights with smoothed heights
            centerPixelHeights = smoothedHeights;

            Debug.Log($"[EdgePairGenerator] Applied smoothing to {smoothedHeights.Count} edge heights");
        }
    }
}
