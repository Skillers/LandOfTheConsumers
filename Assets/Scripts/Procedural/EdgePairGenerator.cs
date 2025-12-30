using UnityEngine;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LandOfTheConsumers.Procedural
{
    /// <summary>
    /// Generates mesh for a single edge pair between two regions.
    /// Creates chunked terrain following the same pattern as regions, with averaged heights.
    /// </summary>
    public class EdgePairGenerator : MonoBehaviour
    {
        [HideInInspector] public EdgeData edgeData;
        [HideInInspector] public CellularNoiseVisualizer cellularVisualizer;
        [HideInInspector] public RegionQuadVisualizer regionQuadVisualizer;
        [HideInInspector] public Material edgeMaterial;

        private const int chunkSize = 32;  // Match region chunk size
        private const int pixelsPerUnit = 2;  // LOD0 only
        private const int pixelSamplingStep = 1;  // LOD0 only

        private List<GameObject> chunks = new List<GameObject>();
        private bool isGenerating = false;
        private Dictionary<Vector2Int, float> centerPixelHeights; // Height for each center pixel

        // Temporary PerlinSettings for height calculation
        private PerlinSettings tempSettingsA;
        private PerlinSettings tempSettingsB;

        public System.Action OnGenerationComplete;
        public bool IsGenerating => isGenerating;

        public void GenerateEdgeMesh()
        {
            if (edgeData == null || edgeData.edgePixels.Count == 0)
            {
                Debug.LogWarning($"[EdgePairGenerator] No edge pixels for {edgeData?.GetPairName()}");
                OnGenerationComplete?.Invoke();
                return;
            }

            if (isGenerating)
            {
                Debug.LogWarning($"[EdgePairGenerator] Already generating {edgeData.GetPairName()}");
                return;
            }

            StartCoroutine(GenerateEdgeMeshCoroutine());
        }

        private IEnumerator GenerateEdgeMeshCoroutine()
        {
            isGenerating = true;

            // Clear existing chunks
            foreach (var chunk in chunks)
            {
                if (chunk != null)
                {
                    if (Application.isPlaying)
                        Destroy(chunk);
                    else
                        DestroyImmediate(chunk);
                }
            }
            chunks.Clear();

            // Get world dimensions for coordinate conversion
            Vector2Int worldSizeInBiomes = cellularVisualizer.worldSizeInBiomes;
            const int biomeSize = 128;
            float worldWidth = worldSizeInBiomes.x * biomeSize;
            float worldHeight = worldSizeInBiomes.y * biomeSize;
            float halfWidth = worldWidth * 0.5f;
            float halfHeight = worldHeight * 0.5f;
            int cellularPixelsPerUnit = cellularVisualizer.pixelsPerUnit;

            // Find bounds of edge pixels
            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;

            foreach (var pixel in edgeData.edgePixels)
            {
                minX = Mathf.Min(minX, pixel.x);
                minY = Mathf.Min(minY, pixel.y);
                maxX = Mathf.Max(maxX, pixel.x);
                maxY = Mathf.Max(maxY, pixel.y);
            }

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

            // Calculate heights for center pixels only (the ribbon's centerline)
            // These heights will be used for the entire 3-pixel width at each point
            centerPixelHeights = new Dictionary<Vector2Int, float>();

            Debug.Log($"[EdgePairGenerator] Calculating heights for {edgeData.centerPixels.Count} center pixels");

            foreach (var centerPixel in edgeData.centerPixels)
            {
                // Calculate height from region A's perspective
                float heightA = 0f;
                // Use the center of the pixel for height sampling
                int terrainA_X = (centerPixel.x - regionA_minX) * pixelsPerUnit + pixelsPerUnit / 2;
                int terrainA_Y = (centerPixel.y - regionA_minY) * pixelsPerUnit + pixelsPerUnit / 2;
                if (terrainA_X >= 0 && terrainA_X < regionA_terrainWidth &&
                    terrainA_Y >= 0 && terrainA_Y < regionA_terrainHeight)
                {
                    heightA = CalculateHeightAtTerrain(terrainA_X, terrainA_Y, regionA_terrainWidth, regionA_terrainHeight, tempSettingsA);
                }

                // Calculate height from region B's perspective
                float heightB = 0f;
                int terrainB_X = (centerPixel.x - regionB_minX) * pixelsPerUnit + pixelsPerUnit / 2;
                int terrainB_Y = (centerPixel.y - regionB_minY) * pixelsPerUnit + pixelsPerUnit / 2;
                if (terrainB_X >= 0 && terrainB_X < regionB_terrainWidth &&
                    terrainB_Y >= 0 && terrainB_Y < regionB_terrainHeight)
                {
                    heightB = CalculateHeightAtTerrain(terrainB_X, terrainB_Y, regionB_terrainWidth, regionB_terrainHeight, tempSettingsB);
                }

                // Average the two heights and store for this center pixel
                float averagedHeight = (heightA + heightB) * 0.5f;
                centerPixelHeights[centerPixel] = averagedHeight;
            }

            // Calculate chunks
            int chunkMinX = minX / chunkSize;
            int chunkMinY = minY / chunkSize;
            int chunkMaxX = maxX / chunkSize;
            int chunkMaxY = maxY / chunkSize;

            int chunksCreated = 0;
            int totalChunksToCreate = 0;

            // Count chunks
            for (int chunkX = chunkMinX; chunkX <= chunkMaxX; chunkX++)
            {
                for (int chunkY = chunkMinY; chunkY <= chunkMaxY; chunkY++)
                {
                    if (ChunkContainsEdgePixels(chunkX, chunkY))
                    {
                        totalChunksToCreate++;
                    }
                }
            }

            Debug.Log($"[EdgePairGenerator] {edgeData.GetPairName()} will generate {totalChunksToCreate} chunks");

            // Create chunks
            for (int chunkX = chunkMinX; chunkX <= chunkMaxX; chunkX++)
            {
                for (int chunkY = chunkMinY; chunkY <= chunkMaxY; chunkY++)
                {
                    if (ChunkContainsEdgePixels(chunkX, chunkY))
                    {
                        CreateChunk(chunkX, chunkY, minX, minY, cellularPixelsPerUnit, halfWidth, halfHeight);
                        chunksCreated++;

                        // Yield every 5 chunks to prevent freezing
                        if (chunksCreated % 5 == 0)
                        {
                            #if UNITY_EDITOR
                            if (!Application.isPlaying)
                            {
                                EditorApplication.QueuePlayerLoopUpdate();
                            }
                            #endif
                            yield return null;
                        }
                    }
                }
            }

            isGenerating = false;
            Debug.Log($"[EdgePairGenerator] Completed {edgeData.GetPairName()} with {chunksCreated} chunks");
            OnGenerationComplete?.Invoke();
        }

        private bool ChunkContainsEdgePixels(int chunkX, int chunkY)
        {
            int startX = chunkX * chunkSize;
            int startY = chunkY * chunkSize;
            int endX = startX + chunkSize;
            int endY = startY + chunkSize;

            for (int x = startX; x <= endX; x++)
            {
                for (int y = startY; y <= endY; y++)
                {
                    if (edgeData.edgePixelSet.Contains(new Vector2Int(x, y)))
                    {
                        return true;
                    }
                }
            }
            return false;
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
        /// Find the nearest center pixel to a given edge pixel.
        /// The edge is like a ribbon - all pixels across the width should use the same center pixel's height.
        /// </summary>
        private Vector2Int FindNearestCenterPixel(Vector2Int edgePixel)
        {
            // If this pixel IS a center pixel, return it
            if (edgeData.centerPixelSet.Contains(edgePixel))
            {
                return edgePixel;
            }

            // Otherwise, find the closest center pixel (Manhattan distance)
            Vector2Int nearest = edgeData.centerPixels[0];
            int minDistance = int.MaxValue;

            foreach (var centerPixel in edgeData.centerPixels)
            {
                int distance = Mathf.Abs(centerPixel.x - edgePixel.x) + Mathf.Abs(centerPixel.y - edgePixel.y);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = centerPixel;
                }
            }

            return nearest;
        }

        private void CreateChunk(int chunkX, int chunkY, int minPixelX, int minPixelY,
                                int cellularPixelsPerUnit, float halfWidth, float halfHeight)
        {
            GameObject chunkObj = new GameObject($"Chunk_{chunkX}_{chunkY}");
            chunkObj.transform.parent = this.transform;
            chunks.Add(chunkObj);

            // Calculate pixel range
            int pixelStartX = chunkX * chunkSize;
            int pixelStartY = chunkY * chunkSize;
            int pixelEndX = pixelStartX + chunkSize;
            int pixelEndY = pixelStartY + chunkSize;

            // Position chunk in world space
            float chunkWorldX = (pixelStartX + pixelEndX) / 2.0f / cellularPixelsPerUnit - halfWidth;
            float chunkWorldZ = (pixelStartY + pixelEndY) / 2.0f / cellularPixelsPerUnit - halfHeight;
            Vector3 chunkPosition = new Vector3(chunkWorldX, 0, chunkWorldZ);
            chunkObj.transform.position = chunkPosition;

            MeshFilter meshFilter = chunkObj.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = chunkObj.AddComponent<MeshRenderer>();

            // Use provided material or create default gray material for edges
            if (edgeMaterial != null)
            {
                meshRenderer.material = edgeMaterial;
            }
            else
            {
                meshRenderer.material = new Material(Shader.Find("Standard"));
                meshRenderer.material.color = new Color(0.5f, 0.5f, 0.5f); // Gray
            }

            // Create mesh
            Mesh mesh = CreateChunkMeshForEdge(pixelStartX, pixelStartY, pixelEndX, pixelEndY,
                                              minPixelX, minPixelY, cellularPixelsPerUnit,
                                              halfWidth, halfHeight);
            meshFilter.mesh = mesh;
        }

        private Mesh CreateChunkMeshForEdge(int pixelStartX, int pixelStartY, int pixelEndX, int pixelEndY,
                                           int minPixelX, int minPixelY, int cellularPixelsPerUnit,
                                           float halfWidth, float halfHeight)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Color> colors = new List<Color>();

            Dictionary<Vector2Int, int> vertexIndexMap = new Dictionary<Vector2Int, int>();

            // Generate vertices for edge pixels
            for (int pixelX = pixelStartX; pixelX <= pixelEndX; pixelX++)
            {
                for (int pixelY = pixelStartY; pixelY <= pixelEndY; pixelY++)
                {
                    Vector2Int pixel = new Vector2Int(pixelX, pixelY);

                    // Only process pixels that belong to this edge
                    if (edgeData.edgePixelSet.Contains(pixel) &&
                        pixelX % pixelSamplingStep == 0 && pixelY % pixelSamplingStep == 0)
                    {
                        // Find the nearest center pixel to get the ribbon's height at this point
                        Vector2Int nearestCenter = FindNearestCenterPixel(pixel);
                        float ribbonHeight = 0f;

                        if (centerPixelHeights.ContainsKey(nearestCenter))
                        {
                            ribbonHeight = centerPixelHeights[nearestCenter];
                        }

                        // Each pixel generates (pixelsPerUnit + 1)² vertices (3x3 for LOD0)
                        for (int subX = 0; subX <= pixelsPerUnit; subX++)
                        {
                            for (int subY = 0; subY <= pixelsPerUnit; subY++)
                            {
                                int terrainX = (pixelX - minPixelX) * pixelsPerUnit + subX;
                                int terrainY = (pixelY - minPixelY) * pixelsPerUnit + subY;
                                Vector2Int terrainCoord = new Vector2Int(terrainX, terrainY);

                                if (vertexIndexMap.ContainsKey(terrainCoord))
                                    continue;

                                // Use the ribbon height (same for entire width)
                                float height = ribbonHeight;

                                // Add small Y offset to prevent z-fighting with regions
                                height += 0.01f;

                                // Calculate world position (local to chunk)
                                float worldX = pixelX + (subX / (float)pixelsPerUnit);
                                float worldZ = pixelY + (subY / (float)pixelsPerUnit);
                                float localX = worldX / cellularPixelsPerUnit - halfWidth
                                             - (pixelStartX + pixelEndX) / 2.0f / cellularPixelsPerUnit + halfWidth;
                                float localZ = worldZ / cellularPixelsPerUnit - halfHeight
                                             - (pixelStartY + pixelEndY) / 2.0f / cellularPixelsPerUnit + halfHeight;

                                Vector3 vertexPos = new Vector3(localX, height, localZ);

                                int vertexIndex = vertices.Count;
                                vertices.Add(vertexPos);
                                colors.Add(Color.white);
                                vertexIndexMap[terrainCoord] = vertexIndex;
                            }
                        }
                    }
                }
            }

            // Generate triangles (same logic as RegionTerrainGenerator)
            for (int pixelX = pixelStartX; pixelX < pixelEndX; pixelX++)
            {
                for (int pixelY = pixelStartY; pixelY < pixelEndY; pixelY++)
                {
                    Vector2Int pixel = new Vector2Int(pixelX, pixelY);

                    if (edgeData.edgePixelSet.Contains(pixel) &&
                        pixelX % pixelSamplingStep == 0 && pixelY % pixelSamplingStep == 0)
                    {
                        for (int subX = 0; subX < pixelsPerUnit; subX++)
                        {
                            for (int subY = 0; subY < pixelsPerUnit; subY++)
                            {
                                int terrainX = (pixelX - minPixelX) * pixelsPerUnit + subX;
                                int terrainY = (pixelY - minPixelY) * pixelsPerUnit + subY;

                                Vector2Int tl = new Vector2Int(terrainX, terrainY);
                                Vector2Int tr = new Vector2Int(terrainX + 1, terrainY);
                                Vector2Int bl = new Vector2Int(terrainX, terrainY + 1);
                                Vector2Int br = new Vector2Int(terrainX + 1, terrainY + 1);

                                if (vertexIndexMap.ContainsKey(tl) && vertexIndexMap.ContainsKey(tr) &&
                                    vertexIndexMap.ContainsKey(bl) && vertexIndexMap.ContainsKey(br))
                                {
                                    int tlIdx = vertexIndexMap[tl];
                                    int trIdx = vertexIndexMap[tr];
                                    int blIdx = vertexIndexMap[bl];
                                    int brIdx = vertexIndexMap[br];

                                    // First triangle
                                    triangles.Add(tlIdx);
                                    triangles.Add(blIdx);
                                    triangles.Add(trIdx);

                                    // Second triangle
                                    triangles.Add(trIdx);
                                    triangles.Add(blIdx);
                                    triangles.Add(brIdx);
                                }
                            }
                        }
                    }
                }
            }

            Mesh mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.colors = colors.ToArray();
            mesh.RecalculateNormals();

            return mesh;
        }
    }
}
