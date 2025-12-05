using UnityEngine;
using System.Collections.Generic;

namespace LandOfTheConsumers.Procedural
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class RegionQuadVisualizer : MonoBehaviour
    {
        [Header("Region Selection")]
        [Tooltip("The cellular noise visualizer to get region data from")]
        [SerializeField] private CellularNoiseVisualizer cellularVisualizer;

        [Tooltip("Which region to visualize (region ID) - Used for single region mode")]
        [SerializeField] private int regionIndex = 0;

        [Header("Quad Settings")]
        [Tooltip("Size of each quad representing a pixel")]
        [SerializeField] private float quadSize = 0.5f;

        [Tooltip("Color for the region quads - Used for single region mode")]
        [SerializeField] private Color regionColor = Color.cyan;

        [Header("All Regions Settings")]
        [Tooltip("Use random colors for each region in all regions mode")]
        [SerializeField] private bool useRandomColors = true;

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

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;

        private void OnEnable()
        {
            SetupComponents();
        }

        private void SetupComponents()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();

            if (meshRenderer.sharedMaterial == null)
            {
                Material mat = new Material(Shader.Find("Standard"));
                mat.color = regionColor;
                meshRenderer.sharedMaterial = mat;
            }
        }

        [ContextMenu("Generate Region Quads")]
        public void GenerateRegionQuads()
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

            if (regionIndex < 0 || regionIndex >= regions.Count)
            {
                Debug.LogError($"[RegionQuadVisualizer] Region index {regionIndex} is out of range (0-{regions.Count - 1})");
                return;
            }

            CellularRegion region = regions[regionIndex];
            if (region.PixelCount == 0)
            {
                Debug.LogWarning($"[RegionQuadVisualizer] Region {regionIndex} has no pixels!");
                return;
            }

            Debug.Log($"[RegionQuadVisualizer] Generating {region.PixelCount} quads for region {regionIndex}");

            GenerateMesh(region);
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

        private void GenerateMesh(CellularRegion region)
        {
            int pixelCount = region.PixelCount;
            int pixelsPerUnit = cellularVisualizer.pixelsPerUnit;

            // Calculate world size to center the mesh
            Vector2Int worldSizeInBiomes = cellularVisualizer.worldSizeInBiomes;
            const int biomeSize = 128;
            float worldWidth = worldSizeInBiomes.x * biomeSize;
            float worldHeight = worldSizeInBiomes.y * biomeSize;
            float halfWidth = worldWidth * 0.5f;
            float halfHeight = worldHeight * 0.5f;

            // Each quad needs 4 vertices and 6 indices (2 triangles)
            Vector3[] vertices = new Vector3[pixelCount * 4];
            int[] triangles = new int[pixelCount * 6];
            Vector2[] uvs = new Vector2[pixelCount * 4];
            Color[] colors = new Color[pixelCount * 4];

            float halfQuad = quadSize * 0.5f;

            for (int i = 0; i < pixelCount; i++)
            {
                Vector2Int pixel = region.pixels[i];

                // Convert pixel coordinates to world coordinates
                float worldX = (float)pixel.x / pixelsPerUnit;
                float worldZ = (float)pixel.y / pixelsPerUnit;

                // Center the coordinates
                worldX -= halfWidth;
                worldZ -= halfHeight;

                // Create quad vertices (centered on the pixel position)
                int vertexIndex = i * 4;
                vertices[vertexIndex + 0] = new Vector3(worldX - halfQuad, 0f, worldZ - halfQuad);
                vertices[vertexIndex + 1] = new Vector3(worldX + halfQuad, 0f, worldZ - halfQuad);
                vertices[vertexIndex + 2] = new Vector3(worldX - halfQuad, 0f, worldZ + halfQuad);
                vertices[vertexIndex + 3] = new Vector3(worldX + halfQuad, 0f, worldZ + halfQuad);

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

            // Create mesh
            Mesh mesh = new Mesh();
            mesh.name = $"Region {regionIndex} Quads";

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

            meshFilter.mesh = mesh;

            // Update material color
            if (meshRenderer.sharedMaterial != null)
            {
                meshRenderer.sharedMaterial.color = regionColor;
            }

            Debug.Log($"[RegionQuadVisualizer] Generated mesh with {vertices.Length} vertices, {triangles.Length / 3} triangles for region {regionIndex}");
        }

        private void GenerateAllRegionsMesh(List<CellularRegion> regions)
        {
            // Clean up existing child objects
            ClearChildRegions();

            int regionsGenerated = 0;

            // Generate a separate GameObject for each region
            for (int regionIdx = 0; regionIdx < regions.Count; regionIdx++)
            {
                CellularRegion region = regions[regionIdx];
                if (region.PixelCount == 0) continue;

                // All regions at height 0
                float regionHeight = 0f;

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

                // Create GameObject for this region
                GameObject regionObject = new GameObject($"Region_{regionIdx}");
                regionObject.transform.SetParent(transform);
                regionObject.transform.localPosition = Vector3.zero;
                regionObject.transform.localRotation = Quaternion.identity;
                regionObject.transform.localScale = Vector3.one;

                // Always generate with LOD levels
                GenerateRegionWithLODs(regionObject, region, regionIdx, regionHeight, regionRandomColor);

                regionsGenerated++;
            }

            Debug.Log($"[RegionQuadVisualizer] Generated {regionsGenerated} region objects with 3 LOD levels at height 0");
        }

        private void GenerateRegionWithLODs(GameObject parentObject, CellularRegion region, int regionIdx, float regionHeight, Color regionColor)
        {
            // Add LODGroup component to parent
            LODGroup lodGroup = parentObject.AddComponent<LODGroup>();

            // Create 3 LOD levels
            GameObject lod0Object = new GameObject("LOD0");
            GameObject lod1Object = new GameObject("LOD1");
            GameObject lod2Object = new GameObject("LOD2");

            lod0Object.transform.SetParent(parentObject.transform, false);
            lod1Object.transform.SetParent(parentObject.transform, false);
            lod2Object.transform.SetParent(parentObject.transform, false);

            // Generate meshes for each LOD level
            Renderer lod0Renderer = GenerateSingleRegionMesh(lod0Object, region, regionIdx, regionHeight, regionColor, lod0QuadSize);
            Renderer lod1Renderer = GenerateSingleRegionMesh(lod1Object, region, regionIdx, regionHeight, regionColor, lod1QuadSize);
            Renderer lod2Renderer = GenerateSingleRegionMesh(lod2Object, region, regionIdx, regionHeight, regionColor, lod2QuadSize);

            // Setup LOD levels
            LOD[] lods = new LOD[3];
            lods[0] = new LOD(lod0Transition, new Renderer[] { lod0Renderer });
            lods[1] = new LOD(lod1Transition, new Renderer[] { lod1Renderer });
            lods[2] = new LOD(lod2Transition, new Renderer[] { lod2Renderer });

            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();
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

        private void OnDrawGizmosSelected()
        {
            if (cellularVisualizer == null) return;

            var regions = cellularVisualizer.Regions;
            if (regions == null || regionIndex < 0 || regionIndex >= regions.Count) return;

            CellularRegion region = regions[regionIndex];

            // Draw the points that belong to this region
            Gizmos.color = Color.yellow;
            foreach (var point in region.points)
            {
                Vector2Int worldSizeInBiomes = cellularVisualizer.worldSizeInBiomes;
                const int biomeSize = 128;
                float worldWidth = worldSizeInBiomes.x * biomeSize;
                float worldHeight = worldSizeInBiomes.y * biomeSize;
                float halfWidth = worldWidth * 0.5f;
                float halfHeight = worldHeight * 0.5f;

                Vector3 worldPos = transform.position + new Vector3(point.x - halfWidth, 0.5f, point.y - halfHeight);
                Gizmos.DrawSphere(worldPos, 1f);
            }
        }
    }
}
