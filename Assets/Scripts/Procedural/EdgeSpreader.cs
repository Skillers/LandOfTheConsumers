using UnityEngine;
using System.Collections.Generic;

namespace LandOfTheConsumers.Procedural
{
    /// <summary>
    /// Component for managing edge spreading functionality
    /// </summary>
    public class EdgeSpreader : MonoBehaviour
    {
        private EdgeSpineData edgeData;
        private List<Vector3> edgePoints3D;
        private List<Vector3> FullEdgePoints;
        private Vector3 perpendicular;
        private float pixelToWorldScale;

        public void SetupEdgeSpread()
        {
            // Get edgeData from the EdgePairGenerator on the same GameObject
            EdgePairGenerator edgePairGenerator = GetComponent<EdgePairGenerator>();
            if (edgePairGenerator == null)
            {
                Debug.LogError("[EdgeSpreader] EdgePairGenerator component not found on this GameObject!");
                return;
            }

            edgeData = edgePairGenerator.edgeData;

            // Get cellularVisualizer for coordinate conversion
            CellularNoiseVisualizer cellularVisualizer = edgePairGenerator.cellularVisualizer;
            if (cellularVisualizer == null)
            {
                Debug.LogError("[EdgeSpreader] CellularNoiseVisualizer not found!");
                return;
            }

            // Get world dimensions for coordinate conversion
            Vector2Int worldSizeInBiomes = cellularVisualizer.worldSizeInBiomes;
            const int biomeSize = 128;
            float worldWidth = worldSizeInBiomes.x * biomeSize;
            float worldHeight = worldSizeInBiomes.y * biomeSize;
            float halfWidth = worldWidth * 0.5f;
            float halfHeight = worldHeight * 0.5f;
            int cellularPixelsPerUnit = cellularVisualizer.pixelsPerUnit;

            // Store the pixel to world scale for use in SpreadWings
            pixelToWorldScale = 1.0f / cellularPixelsPerUnit;

            // Get the perpendicular vector from edge data and convert to Vector3
            Vector2 perp2D = edgeData.perpendicularDirection;
            perpendicular = new Vector3(perp2D.x, 0f, perp2D.y);

            // Create Vector3 copy with heights, converting from pixel coordinates to world coordinates
            edgePoints3D = new List<Vector3>();
            foreach (Vector2Int pixel in edgeData.centerPixels)
            {
                float height = edgeData.pixelHeights.ContainsKey(pixel) ? edgeData.pixelHeights[pixel] : 0f;

                // Convert pixel coordinates to world coordinates
                float worldX = (float)pixel.x / cellularPixelsPerUnit - halfWidth;
                float worldZ = (float)pixel.y / cellularPixelsPerUnit - halfHeight;

                edgePoints3D.Add(new Vector3(worldX, height, worldZ));
            }

            // Initialize full edge points list
            FullEdgePoints = new List<Vector3>();

            SpreadWings();
            GenerateEdgeMesh();
        }

        private void SpreadWings()
        {
            // Target: 20 world units on each side
            float wingSizeInUnits = 20f;

            // Calculate pixels per unit (should be 2 for LOD0)
            int pixelsPerUnit = (int)(1.0f / pixelToWorldScale);

            // Calculate wing size in pixels to maintain LOD0 sampling density
            // 20 units * 2 pixels/unit = 40 pixels
            int wingSizeInPixels = (int)(wingSizeInUnits * pixelsPerUnit);

            for (int i = 0; i < edgePoints3D.Count; i++)
            {
                Vector3 spinePoint = edgePoints3D[i];

                for (int j = -wingSizeInPixels; j <= wingSizeInPixels; j++)
                {
                    // Scale the pixel offset to world space
                    Vector3 perpOffset = perpendicular * (j * pixelToWorldScale);
                    FullEdgePoints.Add(spinePoint + perpOffset);
                }
            }
        }

        private void GenerateEdgeMesh()
        {
            if (FullEdgePoints.Count == 0)
            {
                Debug.LogWarning("[EdgeSpreader] No edge points to generate mesh from!");
                return;
            }

            // Create mesh
            Mesh edgeMesh = new Mesh();
            edgeMesh.name = "EdgeSpreadMesh";

            // Set vertices
            edgeMesh.vertices = FullEdgePoints.ToArray();

            // Generate triangles
            List<int> triangles = new List<int>();
            // Calculate points per row based on actual vertex count
            // pointsPerRow = total vertices / number of spine points
            int pointsPerRow = FullEdgePoints.Count / edgePoints3D.Count;

            // Iterate through all rows except the last one
            for (int row = 0; row < edgePoints3D.Count - 1; row++)
            {
                int rowStart = row * pointsPerRow;

                // Create quads along this row
                for (int col = 0; col < pointsPerRow - 1; col++)
                {
                    int i = rowStart + col;

                    // First triangle: (i, i+1, i+21)
                    triangles.Add(i);
                    triangles.Add(i + 1);
                    triangles.Add(i + pointsPerRow);

                    // Second triangle: (i+1, i+22, i+21)
                    triangles.Add(i + 1);
                    triangles.Add(i + pointsPerRow + 1);
                    triangles.Add(i + pointsPerRow);
                }
            }

            edgeMesh.triangles = triangles.ToArray();

            // Recalculate normals for proper lighting
            edgeMesh.RecalculateNormals();

            // Get or create MeshFilter and MeshRenderer
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
                meshFilter = gameObject.AddComponent<MeshFilter>();

            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                meshRenderer = gameObject.AddComponent<MeshRenderer>();

            // Assign mesh
            meshFilter.mesh = edgeMesh;

            // Create and assign orange material
            Material orangeMaterial = new Material(Shader.Find("Standard"));
            orangeMaterial.color = new Color(1f, 0.5f, 0f); // Orange color
            meshRenderer.material = orangeMaterial;

            Debug.Log($"[EdgeSpreader] Generated edge mesh with {FullEdgePoints.Count} vertices and {triangles.Count / 3} triangles");
        }
    }
}
