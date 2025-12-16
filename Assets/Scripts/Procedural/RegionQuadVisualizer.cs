using UnityEngine;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LandOfTheConsumers.Procedural
{
    [ExecuteInEditMode]
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

        [Header("LOD Settings")]
        [Tooltip("Quad size for LOD 0 (highest detail) - 1/4 quads per level")]
        private float lod0QuadSize = 0.5f;

        [Tooltip("Quad size for LOD 1 (medium detail) - 1/4 quads of LOD 0")]
        private float lod1QuadSize = 1.0f;

        [Tooltip("Quad size for LOD 2 (lowest detail) - 1/4 quads of LOD 1")]
        private float lod2QuadSize = 2.0f;

        [Tooltip("Screen relative transition height for LOD 0 to LOD 1")]
        [SerializeField] private float lod0Transition = 0.6f;

        [Tooltip("Screen relative transition height for LOD 1 to LOD 2")]
        [SerializeField] private float lod1Transition = 0.3f;

        [Tooltip("Screen relative transition height for LOD 2 to culled")]
        [SerializeField] private float lod2Transition = 0.15f;

        // Queue system for sequential region spawning
        private class RegionGenerationData
        {
            public CellularRegion region;
            public int regionIndex;
            public float regionHeight;
            public Color regionColor;
            public TerrainNoisePreset selectedPreset;
            public string presetName;
            public GameObject regionObject;
            public GameObject lod0Object;
            public GameObject lod1Object;
            public GameObject lod2Object;
            public RegionTerrainGenerator terrainGeneratorLOD0;
            public RegionTerrainGenerator terrainGeneratorLOD1;
            public RegionTerrainGenerator terrainGeneratorLOD2;
            public LODGroup lodGroup;
        }

        private class LODGenerationTask
        {
            public RegionGenerationData regionData;
            public int lodLevel; // 0, 1, or 2
            public RegionTerrainGenerator generator;
            public GameObject targetObject;
        }

        private Queue<RegionGenerationData> regionQueue = new Queue<RegionGenerationData>();
        private Queue<LODGenerationTask> lodQueue = new Queue<LODGenerationTask>();
        private bool isProcessingQueue = false;
        private bool cancelRequested = false;

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

                // Random color for this region
                Color regionRandomColor;
                if (useRandomColors)
                {
                    regionRandomColor = new Color(
                        Random.Range(0f, 1f),
                        Random.Range(0f, 1f),
                        Random.Range(0f, 1f),
                        1f
                    );
                }
                else
                {
                    // Use a deterministic color based on region index
                    float hue = (float)regionIdx / regions.Count;
                    regionRandomColor = Color.HSVToRGB(hue, 0.8f, 0.9f);
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
                    regionColor = regionRandomColor,
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

                // Add LODGroup
                data.lodGroup = data.regionObject.AddComponent<LODGroup>();

                // Create LOD GameObjects
                data.lod0Object = new GameObject("LOD0");
                data.lod1Object = new GameObject("LOD1");
                data.lod2Object = new GameObject("LOD2");

                data.lod0Object.transform.SetParent(data.regionObject.transform, false);
                data.lod1Object.transform.SetParent(data.regionObject.transform, false);
                data.lod2Object.transform.SetParent(data.regionObject.transform, false);

                // Create terrain generators if preset is assigned
                if (data.selectedPreset != null)
                {
                    // LOD0: 2 pixels per unit
                    data.terrainGeneratorLOD0 = data.regionObject.AddComponent<RegionTerrainGenerator>();
                    data.terrainGeneratorLOD0.regionIndex = data.regionIndex;
                    data.terrainGeneratorLOD0.assignedPreset = data.selectedPreset;
                    data.terrainGeneratorLOD0.pixelsPerUnit = 2;
                    data.terrainGeneratorLOD0.pixelSamplingStep = 1;

                    // LOD1: 1 pixel per unit
                    data.terrainGeneratorLOD1 = data.regionObject.AddComponent<RegionTerrainGenerator>();
                    data.terrainGeneratorLOD1.regionIndex = data.regionIndex;
                    data.terrainGeneratorLOD1.assignedPreset = data.selectedPreset;
                    data.terrainGeneratorLOD1.pixelsPerUnit = 1;
                    data.terrainGeneratorLOD1.pixelSamplingStep = 1;

                    // LOD2: 0.5 pixels per unit (sample every 2nd pixel)
                    data.terrainGeneratorLOD2 = data.regionObject.AddComponent<RegionTerrainGenerator>();
                    data.terrainGeneratorLOD2.regionIndex = data.regionIndex;
                    data.terrainGeneratorLOD2.assignedPreset = data.selectedPreset;
                    data.terrainGeneratorLOD2.pixelsPerUnit = 1;
                    data.terrainGeneratorLOD2.pixelSamplingStep = 2;
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

            // Phase 2: Build LOD generation queue with priority
            // Priority: All LOD0s first, then all LOD1s, then all LOD2s
            lodQueue.Clear();

            // Add all LOD0 tasks
            foreach (var data in allRegionData)
            {
                if (data.terrainGeneratorLOD0 != null)
                {
                    lodQueue.Enqueue(new LODGenerationTask
                    {
                        regionData = data,
                        lodLevel = 0,
                        generator = data.terrainGeneratorLOD0,
                        targetObject = data.lod0Object
                    });
                }
            }

            // Add all LOD1 tasks
            foreach (var data in allRegionData)
            {
                if (data.terrainGeneratorLOD1 != null)
                {
                    lodQueue.Enqueue(new LODGenerationTask
                    {
                        regionData = data,
                        lodLevel = 1,
                        generator = data.terrainGeneratorLOD1,
                        targetObject = data.lod1Object
                    });
                }
            }

            // Add all LOD2 tasks
            foreach (var data in allRegionData)
            {
                if (data.terrainGeneratorLOD2 != null)
                {
                    lodQueue.Enqueue(new LODGenerationTask
                    {
                        regionData = data,
                        lodLevel = 2,
                        generator = data.terrainGeneratorLOD2,
                        targetObject = data.lod2Object
                    });
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

                Debug.Log($"[RegionQuadVisualizer] Generating LOD{task.lodLevel} for region {task.regionData.regionIndex} ({tasksProcessed}/{totalTasks})");

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

                // After each LOD completes, update the LOD group for that region
                UpdateLODGroupImmediately(task.regionData);

                Debug.Log($"[RegionQuadVisualizer] LOD{task.lodLevel} for region {task.regionData.regionIndex} completed");
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

        private void UpdateLODGroupImmediately(RegionGenerationData data)
        {
            // Collect current renderers from each LOD level
            Renderer[] lod0Renderers = data.lod0Object.GetComponentsInChildren<MeshRenderer>();
            Renderer[] lod1Renderers = data.lod1Object.GetComponentsInChildren<MeshRenderer>();
            Renderer[] lod2Renderers = data.lod2Object.GetComponentsInChildren<MeshRenderer>();

            // Setup LOD levels
            LOD[] lods = new LOD[3];
            lods[0] = new LOD(lod0Transition, lod0Renderers.Length > 0 ? lod0Renderers : new Renderer[0]);
            lods[1] = new LOD(lod1Transition, lod1Renderers.Length > 0 ? lod1Renderers : new Renderer[0]);
            lods[2] = new LOD(lod2Transition, lod2Renderers.Length > 0 ? lod2Renderers : new Renderer[0]);

            data.lodGroup.SetLODs(lods);
            data.lodGroup.RecalculateBounds();
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
