using UnityEngine;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LandOfTheConsumers.Procedural
{
    /// <summary>
    /// Generates marching cubes terrain for a single region based on its assigned Perlin preset
    /// </summary>
    public class RegionTerrainGenerator : MonoBehaviour
    {
        [Header("Region Info")]
        [Tooltip("The region this generator is for")]
        public int regionIndex;

        [Tooltip("The Perlin preset assigned to this region")]
        public TerrainNoisePreset assignedPreset;

        [Header("Generation Settings")]
        [Tooltip("Pixels per unit for terrain generation")]
        public int pixelsPerUnit = 2;

        [Tooltip("Sample every Nth pixel (1 = all pixels, 2 = every other pixel, etc.)")]
        public int pixelSamplingStep = 1;

        private const int chunkSize = 32;
        private float[,] heightMap;
        private List<GameObject> chunks = new List<GameObject>();
        private bool isGenerating = false;

        // Temporary PerlinSettings to hold our preset values
        private PerlinSettings tempSettings;

        // Target GameObject to generate terrain chunks into (typically LOD0)
        private GameObject targetContainer;

        // Bounds of the region (min pixel coordinates)
        private int regionMinX;
        private int regionMinY;

        // Event called when terrain generation is complete
        public System.Action OnGenerationComplete;

        // Public property to check if currently generating
        public bool IsGenerating => isGenerating;

        // Public accessor for heightMap
        public float[,] HeightMap => heightMap;

        // Public accessor for region bounds (needed to convert pixel coords to heightmap coords)
        public int RegionMinX => regionMinX;
        public int RegionMinY => regionMinY;

        public void GenerateTerrain(CellularRegion region, CellularNoiseVisualizer cellularVisualizer, GameObject targetParent)
        {
            if (assignedPreset == null)
            {
                Debug.LogWarning($"[RegionTerrainGenerator] No preset assigned for region {regionIndex}");
                return;
            }

            if (isGenerating)
            {
                Debug.LogWarning($"[RegionTerrainGenerator] Already generating terrain for region {regionIndex}");
                return;
            }

            targetContainer = targetParent;
            StartCoroutine(GenerateTerrainCoroutine(region, cellularVisualizer));
        }

        private IEnumerator GenerateTerrainCoroutine(CellularRegion region, CellularNoiseVisualizer cellularVisualizer)
        {
            isGenerating = true;

            // Create temporary PerlinSettings from the preset
            tempSettings = gameObject.AddComponent<PerlinSettings>();
            tempSettings.ApplyPreset(assignedPreset);

            // Clear existing chunks
            foreach (var chunk in chunks)
            {
                if (chunk != null)
                    DestroyImmediate(chunk);
            }
            chunks.Clear();

            // Calculate the bounds of this region
            Vector2Int worldSizeInBiomes = cellularVisualizer.worldSizeInBiomes;
            const int biomeSize = 128;
            float worldWidth = worldSizeInBiomes.x * biomeSize;
            float worldHeight = worldSizeInBiomes.y * biomeSize;
            float halfWidth = worldWidth * 0.5f;
            float halfHeight = worldHeight * 0.5f;

            // Find min/max bounds of the region pixels
            if (region.pixels.Count == 0)
            {
                Debug.LogWarning($"[RegionTerrainGenerator] Region {regionIndex} has no pixels!");
                isGenerating = false;
                yield break;
            }

            // Create a HashSet of region pixels for fast lookup
            HashSet<Vector2Int> regionPixelSet = new HashSet<Vector2Int>(region.pixels);

            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;

            foreach (var pixel in region.pixels)
            {
                minX = Mathf.Min(minX, pixel.x);
                minY = Mathf.Min(minY, pixel.y);
                maxX = Mathf.Max(maxX, pixel.x);
                maxY = Mathf.Max(maxY, pixel.y);
            }

            Debug.Log($"[RegionTerrainGenerator] Region {regionIndex} - Pixel bounds: ({minX}, {minY}) to ({maxX}, {maxY})");

            // Store region bounds for later height sampling
            regionMinX = minX;
            regionMinY = minY;

            // Generate height map for the entire region bounds
            // Add 1 to account for the extra vertex at the end (vertices go from 0 to pixelsPerUnit inclusive)
            int terrainWidth = (maxX - minX + 1) * pixelsPerUnit + 1;
            int terrainHeight = (maxY - minY + 1) * pixelsPerUnit + 1;
            heightMap = new float[terrainWidth, terrainHeight];

            for (int x = 0; x < terrainWidth; x++)
            {
                for (int y = 0; y < terrainHeight; y++)
                {
                    heightMap[x, y] = CalculateHeight(x, y, terrainWidth, terrainHeight);
                }
            }

            // Calculate chunks based on pixel coordinates (not terrain coordinates)
            int chunkMinX = minX / chunkSize;
            int chunkMinY = minY / chunkSize;
            int chunkMaxX = maxX / chunkSize;
            int chunkMaxY = maxY / chunkSize;

            int chunksCreated = 0;
            int totalChunksToCreate = 0;

            // Count total chunks first for progress tracking
            for (int chunkX = chunkMinX; chunkX <= chunkMaxX; chunkX++)
            {
                for (int chunkY = chunkMinY; chunkY <= chunkMaxY; chunkY++)
                {
                    if (ChunkContainsRegionPixels(chunkX, chunkY, regionPixelSet))
                    {
                        totalChunksToCreate++;
                    }
                }
            }

            Debug.Log($"[RegionTerrainGenerator] Region {regionIndex} will generate {totalChunksToCreate} chunks");

            // Create chunks only where region pixels exist
            for (int chunkX = chunkMinX; chunkX <= chunkMaxX; chunkX++)
            {
                for (int chunkY = chunkMinY; chunkY <= chunkMaxY; chunkY++)
                {
                    // Check if this chunk contains any pixels from the region
                    if (ChunkContainsRegionPixels(chunkX, chunkY, regionPixelSet))
                    {
                        CreateChunk(chunkX, chunkY, minX, minY, regionPixelSet, cellularVisualizer.pixelsPerUnit, halfWidth, halfHeight);
                        chunksCreated++;

                        // Yield every few chunks to prevent freezing, but no delay
                        if (chunksCreated % 5 == 0)
                        {
                            #if UNITY_EDITOR
                            if (!Application.isPlaying)
                            {
                                // Force editor update in Edit Mode
                                EditorApplication.QueuePlayerLoopUpdate();
                            }
                            #endif
                            yield return null;
                        }
                    }
                }
            }

            isGenerating = false;
            Debug.Log($"[RegionTerrainGenerator] Completed terrain generation for region {regionIndex} with {chunksCreated} chunks");

            // Notify listeners that generation is complete
            OnGenerationComplete?.Invoke();
        }

        private bool ChunkContainsRegionPixels(int chunkX, int chunkY, HashSet<Vector2Int> regionPixelSet)
        {
            int startX = chunkX * chunkSize;
            int startY = chunkY * chunkSize;
            int endX = startX + chunkSize;
            int endY = startY + chunkSize;

            // Check if any pixel in this chunk area belongs to the region
            for (int x = startX; x <= endX; x++)
            {
                for (int y = startY; y <= endY; y++)
                {
                    if (regionPixelSet.Contains(new Vector2Int(x, y)))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private float CalculateHeight(int x, int y, int width, int height)
        {
            float xCoord = (float)x / width * tempSettings.scale * tempSettings.xScale + tempSettings.offSetX / (tempSettings.scale * tempSettings.xScale) / 2f;
            float yCoord = (float)y / height * tempSettings.scale * tempSettings.zScale + tempSettings.offSetY / (tempSettings.scale * tempSettings.zScale) / 2f;

            float sample = Mathf.PerlinNoise(xCoord, yCoord);
            return sample * tempSettings.heightMultiplier;
        }

        private void CreateChunk(int chunkX, int chunkY, int minPixelX, int minPixelY, HashSet<Vector2Int> regionPixelSet, int cellularPixelsPerUnit, float halfWidth, float halfHeight)
        {
            GameObject chunkObj = new GameObject($"Chunk_{chunkX}_{chunkY}");
            chunkObj.transform.parent = targetContainer != null ? targetContainer.transform : this.transform;
            chunks.Add(chunkObj);

            // Calculate pixel range for this chunk
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
            meshRenderer.material = new Material(Shader.Find("Standard"));

            // Create mesh only for pixels that belong to the region
            Mesh mesh = CreateChunkMeshForRegion(pixelStartX, pixelStartY, pixelEndX, pixelEndY, minPixelX, minPixelY, regionPixelSet, cellularPixelsPerUnit, halfWidth, halfHeight);
            meshFilter.mesh = mesh;
        }

        private Mesh CreateChunkMeshForRegion(int pixelStartX, int pixelStartY, int pixelEndX, int pixelEndY,
            int minPixelX, int minPixelY, HashSet<Vector2Int> regionPixelSet, int cellularPixelsPerUnit,
            float halfWidth, float halfHeight)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Color> colors = new List<Color>();

            // Dictionary to map terrain coordinate to vertex index
            Dictionary<Vector2Int, int> vertexIndexMap = new Dictionary<Vector2Int, int>();

            // For each pixel in this chunk, generate terrain vertices if the pixel belongs to the region
            for (int pixelX = pixelStartX; pixelX <= pixelEndX; pixelX++)
            {
                for (int pixelY = pixelStartY; pixelY <= pixelEndY; pixelY++)
                {
                    Vector2Int pixel = new Vector2Int(pixelX, pixelY);

                    // Only process pixels that belong to this region and match sampling pattern
                    if (regionPixelSet.Contains(pixel) && pixelX % pixelSamplingStep == 0 && pixelY % pixelSamplingStep == 0)
                    {
                        // Each pixel generates pixelsPerUnit x pixelsPerUnit vertices
                        for (int subX = 0; subX <= pixelsPerUnit; subX++)
                        {
                            for (int subY = 0; subY <= pixelsPerUnit; subY++)
                            {
                                // Terrain coordinate (in heightmap space)
                                int terrainX = (pixelX - minPixelX) * pixelsPerUnit + subX;
                                int terrainY = (pixelY - minPixelY) * pixelsPerUnit + subY;
                                Vector2Int terrainCoord = new Vector2Int(terrainX, terrainY);

                                // Skip if we already added this vertex
                                if (vertexIndexMap.ContainsKey(terrainCoord))
                                    continue;

                                // Get height from heightmap
                                float height = 0f;
                                if (terrainX >= 0 && terrainX < heightMap.GetLength(0) &&
                                    terrainY >= 0 && terrainY < heightMap.GetLength(1))
                                {
                                    height = heightMap[terrainX, terrainY];
                                }

                                // Calculate world position (local to chunk)
                                float worldX = pixelX + (subX / (float)pixelsPerUnit);
                                float worldZ = pixelY + (subY / (float)pixelsPerUnit);
                                float localX = worldX / cellularPixelsPerUnit - halfWidth - (pixelStartX + pixelEndX) / 2.0f / cellularPixelsPerUnit + halfWidth;
                                float localZ = worldZ / cellularPixelsPerUnit - halfHeight - (pixelStartY + pixelEndY) / 2.0f / cellularPixelsPerUnit + halfHeight;

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

            // Generate triangles for pixels in the region
            for (int pixelX = pixelStartX; pixelX < pixelEndX; pixelX++)
            {
                for (int pixelY = pixelStartY; pixelY < pixelEndY; pixelY++)
                {
                    Vector2Int pixel = new Vector2Int(pixelX, pixelY);

                    if (regionPixelSet.Contains(pixel) && pixelX % pixelSamplingStep == 0 && pixelY % pixelSamplingStep == 0)
                    {
                        // Create triangles for each sub-quad within the pixel
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

                                // Only create triangles if all four vertices exist
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
