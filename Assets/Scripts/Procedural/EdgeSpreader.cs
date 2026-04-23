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
        private Vector2 perpendicular2D;
        private float pixelToWorldScale;
        private RegionTerrainGenerator terrainGenA;
        private RegionTerrainGenerator terrainGenB;
        private int cellularPixelsPerUnit;

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
            cellularPixelsPerUnit = cellularVisualizer.pixelsPerUnit;

            // Store the pixel to world scale for use in SpreadWings
            pixelToWorldScale = 1.0f / cellularPixelsPerUnit;

            // Get the perpendicular vector from edge data and convert to Vector3
            perpendicular2D = edgeData.perpendicularDirection;
            perpendicular = new Vector3(perpendicular2D.x, 0f, perpendicular2D.y);

            // Grab the two region terrain generators so we can sample heightmaps during wing spread
            RegionQuadVisualizer rqv = edgePairGenerator.regionQuadVisualizer;
            terrainGenA = null;
            terrainGenB = null;
            if (rqv != null)
            {
                if (rqv.regionTerrainGenerators.ContainsKey(edgeData.regionIdA))
                    terrainGenA = rqv.regionTerrainGenerators[edgeData.regionIdA];
                if (rqv.regionTerrainGenerators.ContainsKey(edgeData.regionIdB))
                    terrainGenB = rqv.regionTerrainGenerators[edgeData.regionIdB];
            }

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

        /// <summary>
        /// Determines which region is on the positive-j side of the perpendicular by probing
        /// a few spine pixels and seeing which heightmap the positive-offset pixel lands in.
        /// Returns true if region A is on the positive side, false if region B is.
        /// </summary>
        private bool DetermineRegionAIsPositiveSide(int pixelsPerUnit)
        {
            int probeOffset = Mathf.Max(1, (int)(wingSizeInUnits * pixelsPerUnit / 4));
            int votesForA = 0, votesForB = 0;

            int step = Mathf.Max(1, edgeData.centerPixels.Count / 5);
            for (int i = 0; i < edgeData.centerPixels.Count; i += step)
            {
                Vector2Int centerPixel = edgeData.centerPixels[i];
                float px = centerPixel.x + perpendicular2D.x * probeOffset;
                float py = centerPixel.y + perpendicular2D.y * probeOffset;

                float dummy;
                if (TrySampleSingleRegion(terrainGenA, px, py, pixelsPerUnit, out dummy))
                    votesForA++;
                else if (TrySampleSingleRegion(terrainGenB, px, py, pixelsPerUnit, out dummy))
                    votesForB++;
            }

            return votesForA >= votesForB;
        }

        private float wingSizeInUnits = 20f;

        private void SpreadWings()
        {
            // Calculate pixels per unit (should be 2 for LOD0)
            int pixelsPerUnit = cellularPixelsPerUnit;

            // Calculate wing size in pixels to maintain LOD0 sampling density
            // 20 units * 2 pixels/unit = 40 pixels
            int wingSizeInPixels = (int)(wingSizeInUnits * pixelsPerUnit);

            // Determine which region is on the positive-j side of the perpendicular
            bool regionAIsPositiveSide = DetermineRegionAIsPositiveSide(pixelsPerUnit);
            RegionTerrainGenerator positiveGen = regionAIsPositiveSide ? terrainGenA : terrainGenB;
            RegionTerrainGenerator negativeGen = regionAIsPositiveSide ? terrainGenB : terrainGenA;

            for (int i = 0; i < edgePoints3D.Count; i++)
            {
                Vector3 spinePoint = edgePoints3D[i];
                float spineHeight = spinePoint.y;
                Vector2Int centerPixel = edgeData.centerPixels[i];

                for (int j = -wingSizeInPixels; j <= wingSizeInPixels; j++)
                {
                    // Scale the pixel offset to world space
                    Vector3 perpOffset = perpendicular * (j * pixelToWorldScale);

                    // Compute the cellular pixel at this perpendicular offset
                    float samplePixelX = centerPixel.x + perpendicular2D.x * j;
                    float samplePixelY = centerPixel.y + perpendicular2D.y * j;

                    // Sample the region whose territory this side of the spine belongs to
                    float regionHeight;
                    bool hasRegionHeight = j >= 0
                        ? TrySampleSingleRegion(positiveGen, samplePixelX, samplePixelY, pixelsPerUnit, out regionHeight)
                        : TrySampleSingleRegion(negativeGen, samplePixelX, samplePixelY, pixelsPerUnit, out regionHeight);

                    float blendedHeight;
                    if (hasRegionHeight)
                    {
                        // t = 0 at spine (100% spine height), t = 1 at wing tip (100% region height)
                        float t = Mathf.Abs(j) / (float)wingSizeInPixels;
                        blendedHeight = (1f - t) * spineHeight + t * regionHeight;
                    }
                    else
                    {
                        blendedHeight = spineHeight;
                    }

                    FullEdgePoints.Add(new Vector3(spinePoint.x + perpOffset.x, blendedHeight, spinePoint.z + perpOffset.z));
                }
            }
        }

        private bool TrySampleSingleRegion(RegionTerrainGenerator gen, float pixelX, float pixelY, int pixelsPerUnit, out float height)
        {
            height = 0f;
            if (gen == null || gen.HeightMap == null)
                return false;

            int terrainX = Mathf.RoundToInt((pixelX - gen.RegionMinX) * pixelsPerUnit + pixelsPerUnit / 2);
            int terrainY = Mathf.RoundToInt((pixelY - gen.RegionMinY) * pixelsPerUnit + pixelsPerUnit / 2);

            int w = gen.HeightMap.GetLength(0);
            int h = gen.HeightMap.GetLength(1);
            if (terrainX < 0 || terrainX >= w || terrainY < 0 || terrainY >= h)
                return false;

            height = gen.HeightMap[terrainX, terrainY];
            return true;
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
