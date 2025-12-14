using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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

            // Clear existing preset mapping
            regionPresetMapping.Clear();

            int regionsGenerated = 0;

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

            // Generate a separate GameObject for each region
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

                // Create GameObject for this region with preset name
                GameObject regionObject = new GameObject($"R_{presetName}_{regionIdx}");
                regionObject.transform.SetParent(transform);
                regionObject.transform.localPosition = Vector3.zero;
                regionObject.transform.localRotation = Quaternion.identity;
                regionObject.transform.localScale = Vector3.one;

                // Store the preset mapping in the main visualizer
                if (selectedPreset != null)
                {
                    regionPresetMapping[regionIdx] = selectedPreset;
                    Debug.Log($"[RegionQuadVisualizer] Stored preset '{presetName}' for region {regionIdx}");
                }

                // Add RegionTerrainGenerator to the region parent if preset is assigned
                RegionTerrainGenerator terrainGenerator = null;
                if (selectedPreset != null)
                {
                    terrainGenerator = regionObject.AddComponent<RegionTerrainGenerator>();
                    terrainGenerator.regionIndex = regionIdx;
                    terrainGenerator.assignedPreset = selectedPreset;
                    terrainGenerator.pixelsPerUnit = 2;
                }

                // Always generate with LOD levels
                GenerateRegionWithLODs(regionObject, region, regionIdx, regionHeight, regionRandomColor, terrainGenerator);

                regionsGenerated++;
            }

            Debug.Log($"[RegionQuadVisualizer] Generated {regionsGenerated} region objects with 3 LOD levels at height 0");
        }

        private void GenerateRegionWithLODs(GameObject parentObject, CellularRegion region, int regionIdx, float regionHeight, Color regionColor, RegionTerrainGenerator terrainGenerator)
        {
            // Add LODGroup component to parent
            LODGroup lodGroup = parentObject.AddComponent<LODGroup>();

            // Create 3 LOD levels - LOD0 is just an empty shell
            GameObject lod0Object = new GameObject("LOD0");
            GameObject lod1Object = new GameObject("LOD1");
            GameObject lod2Object = new GameObject("LOD2");

            lod0Object.transform.SetParent(parentObject.transform, false);
            lod1Object.transform.SetParent(parentObject.transform, false);
            lod2Object.transform.SetParent(parentObject.transform, false);

            // LOD0: Empty shell - terrain chunks will be generated into it
            Renderer lod0Renderer = null;
            if (terrainGenerator != null)
            {
                // Tell the terrain generator to generate terrain into LOD0
                terrainGenerator.GenerateTerrain(region, cellularVisualizer, lod0Object);

                // Create a dummy renderer for LOD system (terrain generator creates mesh renderers as children)
                MeshRenderer dummyRenderer = lod0Object.AddComponent<MeshRenderer>();
                dummyRenderer.enabled = false; // Disabled - chunks will have their own renderers
                lod0Renderer = dummyRenderer;

                Debug.Log($"[RegionQuadVisualizer] LOD0 for region {regionIdx}: Terrain will be generated as children");
            }
            else
            {
                // Fallback to quad mesh if no terrain generator
                lod0Renderer = GenerateSingleRegionMesh(lod0Object, region, regionIdx, regionHeight, regionColor, lod0QuadSize);
                Debug.LogWarning($"[RegionQuadVisualizer] LOD0 for region {regionIdx}: No preset assigned, using quad mesh");
            }

            // LOD1 and LOD2: Generate quad meshes (lower detail)
            Renderer lod1Renderer = GenerateSingleRegionMesh(lod1Object, region, regionIdx, regionHeight, regionColor, lod1QuadSize);
            Renderer lod2Renderer = GenerateSingleRegionMesh(lod2Object, region, regionIdx, regionHeight, regionColor, lod2QuadSize);

            // Setup LOD levels
            if (terrainGenerator != null)
            {
                // For terrain generator LOD0, we need to wait for chunks to be created
                // Start a coroutine to update LOD group after terrain generation
                StartCoroutine(UpdateLODGroupAfterTerrainGeneration(lodGroup, lod0Object, lod1Renderer, lod2Renderer));
            }
            else
            {
                // Standard LOD setup for quad-based rendering
                LOD[] lods = new LOD[3];
                lods[0] = new LOD(lod0Transition, new Renderer[] { lod0Renderer });
                lods[1] = new LOD(lod1Transition, new Renderer[] { lod1Renderer });
                lods[2] = new LOD(lod2Transition, new Renderer[] { lod2Renderer });

                lodGroup.SetLODs(lods);
                lodGroup.RecalculateBounds();
            }
        }

        private IEnumerator UpdateLODGroupAfterTerrainGeneration(LODGroup lodGroup, GameObject lod0Object, Renderer lod1Renderer, Renderer lod2Renderer)
        {
            // Wait a frame for the terrain generation to start
            yield return null;

            // Wait for terrain generation to complete (check if chunks exist in LOD0)
            // Chunks are generated as children of LOD0
            while (lod0Object.transform.childCount == 0)
            {
                yield return new WaitForSeconds(0.5f);
            }

            // Wait a bit more to ensure all chunks are created
            yield return new WaitForSeconds(1f);

            // Collect all child renderers from LOD0 (the terrain chunks)
            Renderer[] lod0Renderers = lod0Object.GetComponentsInChildren<MeshRenderer>();

            // Setup LOD levels with collected renderers
            LOD[] lods = new LOD[3];
            lods[0] = new LOD(lod0Transition, lod0Renderers);
            lods[1] = new LOD(lod1Transition, new Renderer[] { lod1Renderer });
            lods[2] = new LOD(lod2Transition, new Renderer[] { lod2Renderer });

            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();

            Debug.Log($"[RegionQuadVisualizer] Updated LOD group with {lod0Renderers.Length} terrain chunk renderers for LOD0");
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
