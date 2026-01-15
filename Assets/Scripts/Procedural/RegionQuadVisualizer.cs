using UnityEngine;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LandOfTheConsumers.Procedural
{
    public class RegionQuadVisualizer : MonoBehaviour
    {
        [Header("Region Selection")]
        [Tooltip("The cellular noise visualizer to get region data from")]
        [SerializeField] private CellularNoiseVisualizer cellularVisualizer;

        [Header("All Regions Settings")]
        [Tooltip("Use random colors for each region in all regions mode")]
        [SerializeField] private bool useRandomColors = true;

        [Tooltip("Reference to PerlinSettings containing the list of presets to randomly assign")]
        [SerializeField] private PerlinSettings perlinSettingsReference;

        [Header("Region Preset Mapping")]
        [Tooltip("Stores which preset is assigned to each region (Region Index -> Preset)")]
        public Dictionary<int, TerrainNoisePreset> regionPresetMapping = new Dictionary<int, TerrainNoisePreset>();

        [Tooltip("Stores terrain generators for each region (Region Index -> LOD0 TerrainGenerator)")]
        public Dictionary<int, RegionTerrainGenerator> regionTerrainGenerators = new Dictionary<int, RegionTerrainGenerator>();

        [Header("LOD Settings")]
        [Tooltip("Quad size for LOD 0 (highest detail) - 1/4 quads per level")]
        private float lod0QuadSize = 0.5f;
        
        // Queue system for sequential region spawning
        private class RegionGenerationData
        {
            public CellularRegion region;
            public int regionIndex;
            public float regionHeight;
            public TerrainNoisePreset selectedPreset;
            public string presetName;
            public GameObject regionObject;
            public GameObject lod0Object;
            public RegionTerrainGenerator terrainGeneratorLOD0;
        }

        private class LODGenerationTask
        {
            public RegionGenerationData regionData;
            public RegionTerrainGenerator generator;
            public GameObject targetObject;
        }

        private Queue<RegionGenerationData> regionQueue = new Queue<RegionGenerationData>();
        private Queue<LODGenerationTask> lodQueue = new Queue<LODGenerationTask>();
        private bool isProcessingQueue = false;
        private bool cancelRequested = false;

        // Event fired when all generation is complete
        public System.Action OnGenerationComplete;

        private void Start()
        {
            // Automatically generate regions when Play mode starts
            GenerateAllRegionsWithRandomHeights();
        }

        [ContextMenu("Generate All Regions With Random Heights")]
        public void GenerateAllRegionsWithRandomHeights()
        {
            if (cellularVisualizer == null)
            {
                Debug.LogError("[RegionQuadVisualizer] CellularNoiseVisualizer is not assigned!");
                return;
            }

            // Access the regions list
            var regions = cellularVisualizer.Regions;
            if (regions == null || regions.Count == 0)
            {
                Debug.LogError("[RegionQuadVisualizer] No regions found! Generate noise first.");
                return;
            }

            Debug.Log($"[RegionQuadVisualizer] Generating all {regions.Count} regions with random heights");

            GenerateAllRegionsMesh(regions);
        }

        private void GenerateAllRegionsMesh(List<CellularRegion> regions)
        {
            // Clean up existing child objects
            ClearChildRegions();

            // Clear existing preset mapping and queue
            regionPresetMapping.Clear();
            regionTerrainGenerators.Clear();
            regionQueue.Clear();

            // Get preset list from referenced PerlinSettings
            List<TerrainNoisePreset> availablePresets = null;
            if (perlinSettingsReference != null && perlinSettingsReference.terrainNoisePresets != null && perlinSettingsReference.terrainNoisePresets.Count > 0)
            {
                availablePresets = perlinSettingsReference.terrainNoisePresets;
                Debug.Log($"[RegionQuadVisualizer] Found {availablePresets.Count} Perlin Presets from PerlinSettings reference");
            }
            else
            {
                Debug.LogWarning("[RegionQuadVisualizer] No PerlinSettings reference assigned or it has no presets! All regions will use 'Default' naming.");
            }

            // Build queue of regions to generate
            for (int regionIdx = 0; regionIdx < regions.Count; regionIdx++)
            {
                CellularRegion region = regions[regionIdx];
                if (region.PixelCount == 0) continue;

                // All regions at height 0
                float regionHeight = 0f;

                // Randomly select a Perlin Settings Preset
                TerrainNoisePreset selectedPreset = null;
                string presetName = "Default";
                if (availablePresets != null && availablePresets.Count > 0)
                {
                    selectedPreset = availablePresets[Random.Range(0, availablePresets.Count)];
                    if (selectedPreset != null)
                    {
                        if (!string.IsNullOrEmpty(selectedPreset.presetName))
                        {
                            presetName = selectedPreset.presetName;
                            Debug.Log($"[RegionQuadVisualizer] Region {regionIdx} assigned preset: {presetName}");
                        }
                        else
                        {
                            Debug.LogWarning($"[RegionQuadVisualizer] Region {regionIdx}: Selected preset has empty presetName field!");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[RegionQuadVisualizer] Region {regionIdx}: Selected preset is null!");
                    }
                }
                

                // Store the preset mapping in the main visualizer
                if (selectedPreset != null)
                {
                    regionPresetMapping[regionIdx] = selectedPreset;
                    Debug.Log($"[RegionQuadVisualizer] Stored preset '{presetName}' for region {regionIdx}");
                }

                // Add to queue instead of generating immediately
                RegionGenerationData data = new RegionGenerationData
                {
                    region = region,
                    regionIndex = regionIdx,
                    regionHeight = regionHeight,
                    selectedPreset = selectedPreset,
                    presetName = presetName
                };
                regionQueue.Enqueue(data);
            }

            Debug.Log($"[RegionQuadVisualizer] Queued {regionQueue.Count} regions for LOD-based generation");

            // Start processing the queue
            if (!isProcessingQueue && regionQueue.Count > 0)
            {
                StartCoroutine(ProcessRegionQueueWithLODPriority());
            }
        }

        private IEnumerator ProcessRegionQueueWithLODPriority()
        {
            isProcessingQueue = true;
            cancelRequested = false;
            int totalRegions = regionQueue.Count;
            List<RegionGenerationData> allRegionData = new List<RegionGenerationData>();

            Debug.Log($"[RegionQuadVisualizer] Creating {totalRegions} region hierarchies");

            // Phase 1: Create all region GameObjects and setup their hierarchy (no terrain generation yet)
            int regionCount = 0;
            while (regionQueue.Count > 0 && !cancelRequested)
            {
                RegionGenerationData data = regionQueue.Dequeue();
                regionCount++;

                Debug.Log($"[RegionQuadVisualizer] Setting up region {regionCount}/{totalRegions} (Index: {data.regionIndex})");

                // Create GameObject for this region
                data.regionObject = new GameObject($"R_{data.presetName}_{data.regionIndex}");
                data.regionObject.transform.SetParent(transform);
                data.regionObject.transform.localPosition = Vector3.zero;
                data.regionObject.transform.localRotation = Quaternion.identity;
                data.regionObject.transform.localScale = Vector3.one;



                // Create LOD GameObjects
                data.lod0Object = new GameObject("LOD0");
                data.lod0Object.transform.SetParent(data.regionObject.transform, false);

                // Create terrain generators if preset is assigned
                if (data.selectedPreset != null)
                {
                    // LOD0: 2 pixels per unit
                    data.terrainGeneratorLOD0 = data.regionObject.AddComponent<RegionTerrainGenerator>();
                    data.terrainGeneratorLOD0.regionIndex = data.regionIndex;
                    data.terrainGeneratorLOD0.assignedPreset = data.selectedPreset;
                    data.terrainGeneratorLOD0.pixelsPerUnit = 2;
                    data.terrainGeneratorLOD0.pixelSamplingStep = 1;

                    // Store LOD0 terrain generator for public access
                    regionTerrainGenerators[data.regionIndex] = data.terrainGeneratorLOD0;
                }

                allRegionData.Add(data);

                yield return null; // Yield after creating each region's hierarchy
            }

            // Check if cancelled during Phase 1
            if (cancelRequested)
            {
                Debug.Log("[RegionQuadVisualizer] Generation cancelled during region hierarchy creation");
                isProcessingQueue = false;
                cancelRequested = false;
                yield break;
            }

            Debug.Log($"[RegionQuadVisualizer] Region hierarchies created. Building LOD generation queue...");

            // Phase 2: Build LOD generation queue
            lodQueue.Clear();

            // Add all LOD0 tasks
            int lod0TaskCount = 0;
            foreach (var data in allRegionData)
            {
                if (data.terrainGeneratorLOD0 != null)
                {
                    lodQueue.Enqueue(new LODGenerationTask
                    {
                        regionData = data,
                        generator = data.terrainGeneratorLOD0,
                        targetObject = data.lod0Object
                    });
                    lod0TaskCount++;
                }
            }

            Debug.Log($"[RegionQuadVisualizer] LOD generation queue built with {lodQueue.Count} tasks");

            // Phase 3: Process LOD generation queue
            int tasksProcessed = 0;
            int totalTasks = lodQueue.Count;

            while (lodQueue.Count > 0 && !cancelRequested)
            {
                LODGenerationTask task = lodQueue.Dequeue();
                tasksProcessed++;

                Debug.Log($"[RegionQuadVisualizer] Generating region {task.regionData.regionIndex} ({tasksProcessed}/{totalTasks})");

                // Flag to track completion
                bool generationComplete = false;
                task.generator.OnGenerationComplete = () => { generationComplete = true; };

                // Start terrain generation
                task.generator.GenerateTerrain(task.regionData.region, cellularVisualizer, task.targetObject);

                // Wait for this LOD to complete
                while (!generationComplete)
                {
                    #if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        EditorApplication.QueuePlayerLoopUpdate();
                    }
                    #endif
                    yield return null;
                }

                Debug.Log($"[RegionQuadVisualizer] Region {task.regionData.regionIndex} completed");

                // Check if all LOD0 tasks are complete
                if (tasksProcessed == lod0TaskCount && lod0TaskCount > 0)
                {
                    Debug.Log($"[RegionQuadVisualizer] All LOD0 generation complete! Firing OnLOD0GenerationComplete event.");
                    OnGenerationComplete?.Invoke();
                }
            }

            // Final cleanup
            if (cancelRequested)
            {
                Debug.Log($"[RegionQuadVisualizer] Generation cancelled. {tasksProcessed}/{totalTasks} LOD tasks completed before cancellation.");
            }
            else
            {
                Debug.Log($"[RegionQuadVisualizer] Completed all LOD generation for {totalRegions} regions");
            }

            isProcessingQueue = false;
            cancelRequested = false;
        }

        private Renderer GenerateSingleRegionMesh(GameObject targetObject, CellularRegion region, int regionIdx, float regionHeight, Color regionColor, float meshQuadSize)
        {
            int pixelsPerUnit = cellularVisualizer.pixelsPerUnit;

            // Calculate world size to center the mesh
            Vector2Int worldSizeInBiomes = cellularVisualizer.worldSizeInBiomes;
            const int biomeSize = 128;
            float worldWidth = worldSizeInBiomes.x * biomeSize;
            float worldHeight = worldSizeInBiomes.y * biomeSize;
            float halfWidth = worldWidth * 0.5f;
            float halfHeight = worldHeight * 0.5f;

            float halfQuad = meshQuadSize * 0.5f;

            // Calculate pixel skip rate based on quad size
            // quadSize 0.5 = skip 1 (every pixel), quadSize 1.0 = skip 2 (every 2nd pixel), quadSize 2.0 = skip 4 (every 4th pixel)
            int pixelSkip = Mathf.RoundToInt(meshQuadSize / lod0QuadSize);
            if (pixelSkip < 1) pixelSkip = 1;

            // First pass: determine which pixels to include based on skip rate
            List<Vector2Int> sampledPixels = new List<Vector2Int>();
            HashSet<Vector2Int> pixelSet = new HashSet<Vector2Int>(region.pixels);

            foreach (Vector2Int pixel in region.pixels)
            {
                // Sample pixels at regular intervals
                if (pixel.x % pixelSkip == 0 && pixel.y % pixelSkip == 0)
                {
                    sampledPixels.Add(pixel);
                }
            }

            int sampledPixelCount = sampledPixels.Count;
            if (sampledPixelCount == 0)
            {
                // Fallback: if no pixels match the sampling pattern, use at least one
                sampledPixels.Add(region.pixels[0]);
                sampledPixelCount = 1;
            }

            // Create arrays for this region's mesh
            Vector3[] vertices = new Vector3[sampledPixelCount * 4];
            int[] triangles = new int[sampledPixelCount * 6];
            Vector2[] uvs = new Vector2[sampledPixelCount * 4];
            Color[] colors = new Color[sampledPixelCount * 4];

            // Generate quads for sampled pixels
            for (int i = 0; i < sampledPixelCount; i++)
            {
                Vector2Int pixel = sampledPixels[i];

                // Convert pixel coordinates to world coordinates
                float worldX = (float)pixel.x / pixelsPerUnit;
                float worldZ = (float)pixel.y / pixelsPerUnit;

                // Center the coordinates
                worldX -= halfWidth;
                worldZ -= halfHeight;

                // Create quad vertices (centered on the pixel position)
                int vertexIndex = i * 4;
                vertices[vertexIndex + 0] = new Vector3(worldX - halfQuad, regionHeight, worldZ - halfQuad);
                vertices[vertexIndex + 1] = new Vector3(worldX + halfQuad, regionHeight, worldZ - halfQuad);
                vertices[vertexIndex + 2] = new Vector3(worldX - halfQuad, regionHeight, worldZ + halfQuad);
                vertices[vertexIndex + 3] = new Vector3(worldX + halfQuad, regionHeight, worldZ + halfQuad);

                // Set UVs
                uvs[vertexIndex + 0] = new Vector2(0, 0);
                uvs[vertexIndex + 1] = new Vector2(1, 0);
                uvs[vertexIndex + 2] = new Vector2(0, 1);
                uvs[vertexIndex + 3] = new Vector2(1, 1);

                // Set colors
                colors[vertexIndex + 0] = regionColor;
                colors[vertexIndex + 1] = regionColor;
                colors[vertexIndex + 2] = regionColor;
                colors[vertexIndex + 3] = regionColor;

                // Create triangles
                int triangleIndex = i * 6;
                triangles[triangleIndex + 0] = vertexIndex + 0;
                triangles[triangleIndex + 1] = vertexIndex + 2;
                triangles[triangleIndex + 2] = vertexIndex + 1;
                triangles[triangleIndex + 3] = vertexIndex + 2;
                triangles[triangleIndex + 4] = vertexIndex + 3;
                triangles[triangleIndex + 5] = vertexIndex + 1;
            }

            // Add MeshFilter and MeshRenderer
            MeshFilter regionMeshFilter = targetObject.AddComponent<MeshFilter>();
            MeshRenderer regionMeshRenderer = targetObject.AddComponent<MeshRenderer>();

            // Create mesh
            Mesh mesh = new Mesh();
            mesh.name = $"Region {regionIdx} Mesh (QuadSize {meshQuadSize})";

            // Check if we need to use 32-bit indices
            if (vertices.Length > 65535)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            regionMeshFilter.mesh = mesh;

            // Create and assign material
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = regionColor;
            regionMeshRenderer.sharedMaterial = mat;

            Debug.Log($"[RegionQuadVisualizer] Region {regionIdx} LOD: {sampledPixelCount} quads (skip rate: {pixelSkip}, original: {region.PixelCount})");

            return regionMeshRenderer;
        }

        public void CancelGeneration()
        {
            if (isProcessingQueue)
            {
                Debug.Log("[RegionQuadVisualizer] Cancelling generation...");
                cancelRequested = true;
            }
            else
            {
                Debug.Log("[RegionQuadVisualizer] No generation in progress to cancel.");
            }
        }

        public void ClearAndCancel()
        {
            Debug.Log("[RegionQuadVisualizer] Clear and cancel requested");

            // Cancel any ongoing generation
            if (isProcessingQueue)
            {
                cancelRequested = true;
            }

            // Clear the queues
            regionQueue.Clear();
            lodQueue.Clear();

            // Clear all child regions
            ClearChildRegions();

            Debug.Log("[RegionQuadVisualizer] Cleared all regions and cancelled generation");
        }

        private void ClearChildRegions()
        {
            // Clear existing child region objects
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }
    }
}
