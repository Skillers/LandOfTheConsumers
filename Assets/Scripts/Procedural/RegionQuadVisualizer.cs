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

        [Tooltip("Which region to visualize (region ID)")]
        [SerializeField] private int regionIndex = 0;

        [Header("Quad Settings")]
        [Tooltip("Size of each quad representing a pixel")]
        [SerializeField] private float quadSize = 0.5f;

        [Tooltip("Height offset for the quads (Y position)")]
        [SerializeField] private float heightOffset = 1f;

        [Tooltip("Color for the region quads")]
        [SerializeField] private Color regionColor = Color.cyan;

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
                vertices[vertexIndex + 0] = new Vector3(worldX - halfQuad, heightOffset, worldZ - halfQuad);
                vertices[vertexIndex + 1] = new Vector3(worldX + halfQuad, heightOffset, worldZ - halfQuad);
                vertices[vertexIndex + 2] = new Vector3(worldX - halfQuad, heightOffset, worldZ + halfQuad);
                vertices[vertexIndex + 3] = new Vector3(worldX + halfQuad, heightOffset, worldZ + halfQuad);

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

                Vector3 worldPos = transform.position + new Vector3(point.x - halfWidth, heightOffset + 0.5f, point.y - halfHeight);
                Gizmos.DrawSphere(worldPos, 1f);
            }
        }
    }
}
