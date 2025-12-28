using System;
using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LandOfTheConsumers.Procedural
{
    [System.Serializable]
    public class CellCornerData
    {
        public Vector2 position;
        public List<int> connectedRegions = new List<int>();

        public CellCornerData(Vector2 pos)
        {
            position = pos;
        }
    }

    [ExecuteInEditMode]
    public class CellularNoiseVisualizer : MonoBehaviour
    {
        [Header("World Size")]
        [Tooltip("World size in biomes (X, Z). Example: (10, 10) = 10x10 biomes")]
        [SerializeField] public Vector2Int worldSizeInBiomes = new Vector2Int(10, 10);

        private const int biomeSize = 128;

        [Tooltip("Pixels per world unit (controls texture detail)\n\n" +
                 "• 1 = 1 pixel per unit (low detail, fast)\n" +
                 "• 2-4 = Medium detail (recommended)\n" +
                 "• 8-16 = High detail (slower)")]
        [SerializeField] [Range(1, 16)] public int pixelsPerUnit = 2;

        [Header("Noise Settings")]
        [Tooltip("SEED - Random seed for noise generation\n\n" +
                 "Same seed = identical pattern every time\n" +
                 "Change seed = completely different pattern\n\n" +
                 "Each biome has a 90% chance of having ONE random point\n" +
                 "10% of biomes will have no point (more organic!)")]
        [SerializeField] public int seed = 12345;

        [Tooltip("CONNECTION DISTANCE - Points within this distance connect into one larger region\n\n" +
                 "• 0 = No connections (default Voronoi)\n" +
                 "• 20-40 = Small connections\n" +
                 "• 50-80 = Medium connections\n" +
                 "• 100+ = Large connected regions\n\n" +
                 "Connected points shown with RED LINES")]
        [SerializeField] [Range(0f, 150f)] public float connectionDistance = 50f;

        [Header("Display")]
        [Tooltip("Auto-regenerate on parameter changes (disable for better performance)")]
        [SerializeField] private bool autoRegenerate = false;

        [Tooltip("Log empty biomes to console")]
        [SerializeField] private bool logEmptyBiomes = true;

        [Tooltip("Show region numbers in scene view")]
        [SerializeField] private bool showRegionNumbers = true;

        private Texture2D noiseTexture;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;

        // Store biome points and connections for visualization
        private List<Vector2> biomePoints = new List<Vector2>();
        private List<(int, int)> connections = new List<(int, int)>(); // Pairs of point indices that are connected
        private Dictionary<int, int> pointToRegionID = new Dictionary<int, int>(); // Maps point index to region ID
        private List<CellularRegion> regions = new List<CellularRegion>(); // All regions
        private List<CellCornerData> cellCorners = new List<CellCornerData>(); // All cell corners with connected regions

        // Public accessor for regions
        public List<CellularRegion> Regions => regions;

        // Public accessor for cell corners
        public List<CellCornerData> CellCorners => cellCorners;

        private void OnEnable()
        {
            SetupComponents();
            GenerateNoiseTexture();
        }

        private void Start()
        {
            // Print corner data when in play mode
            if (Application.isPlaying)
            {
                PrintCellCornerData();
            }
        }

        private void PrintCellCornerData()
        {
            if (cellCorners == null || cellCorners.Count == 0)
            {
                Debug.LogWarning("[CellularNoiseVisualizer] No cell corner data available. Make sure to generate noise first.");
                return;
            }

            // Calculate half dimensions for centered coordinates
            float worldWidth = worldSizeInBiomes.x * biomeSize;
            float worldHeight = worldSizeInBiomes.y * biomeSize;
            float halfWidth = worldWidth * 0.5f;
            float halfHeight = worldHeight * 0.5f;

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"========== CELL CORNER DATA ({cellCorners.Count} corners) ==========");
            sb.AppendLine();

            for (int i = 0; i < cellCorners.Count; i++)
            {
                CellCornerData corner = cellCorners[i];
                float centeredX = corner.position.x - halfWidth;
                float centeredY = corner.position.y - halfHeight;

                sb.AppendLine($"Corner {i}:");
                sb.AppendLine($"  Position (Raw): ({corner.position.x:F2}, {corner.position.y:F2})");
                sb.AppendLine($"  Position (Centered): ({centeredX:F2}, {centeredY:F2})");
                sb.Append($"  Connected Regions: [");
                for (int j = 0; j < corner.connectedRegions.Count; j++)
                {
                    sb.Append(corner.connectedRegions[j]);
                    if (j < corner.connectedRegions.Count - 1)
                        sb.Append(", ");
                }
                sb.AppendLine("]");
                sb.AppendLine();
            }

            sb.AppendLine("==================================================");
            Debug.Log(sb.ToString());
        }

        private void SetupComponents()
        {
            // Get or create MeshFilter
            meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }

            // Get or create MeshRenderer
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();


                Material mat = new Material(Shader.Find("Unlit/Texture"));
                meshRenderer.sharedMaterial = mat;
            }
        }

        [ContextMenu("Generate Noise")]
        public void GenerateNoiseTexture()
        {
            // Validate settings
            if (worldSizeInBiomes.x <= 0 || worldSizeInBiomes.y <= 0)
            {
                Debug.LogWarning("[CellularNoiseVisualizer] World size must be greater than 0");
                return;
            }

            SetupComponents();

            // Clear previous data
            biomePoints.Clear();
            connections.Clear();
            pointToRegionID.Clear();
            regions.Clear();
            cellCorners.Clear();

            // Calculate world size in units
            int worldWidth = worldSizeInBiomes.x * biomeSize;
            int worldHeight = worldSizeInBiomes.y * biomeSize;

            // Generate all biome points and log empty biomes
            GenerateBiomePoints();

            // Build region groups from connections
            BuildRegionGroups();

            // Calculate cell corners after regions are built
            CalculateAndStoreCellCorners();

            // Calculate texture resolution based on world size and pixels per unit
            int textureWidth = worldWidth * pixelsPerUnit;
            int textureHeight = worldHeight * pixelsPerUnit;

            // Create or recreate texture
            if (noiseTexture == null || noiseTexture.width != textureWidth || noiseTexture.height != textureHeight)
            {
                if (noiseTexture != null)
                {
                    DestroyImmediate(noiseTexture);
                }

                noiseTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGB24, false);
                noiseTexture.filterMode = FilterMode.Bilinear;
                noiseTexture.wrapMode = TextureWrapMode.Clamp;
            }

            // Generate noise based on world coordinates
            Color[] pixels = new Color[textureWidth * textureHeight];

            for (int y = 0; y < textureHeight; y++)
            {
                for (int x = 0; x < textureWidth; x++)
                {
                    // Convert pixel coordinates to world coordinates
                    float worldX = (float)x / pixelsPerUnit;
                    float worldY = (float)y / pixelsPerUnit;
                    Vector2 pixelPos = new Vector2(worldX, worldY);

                    // Find which region this pixel belongs to (based on nearest point)
                    float minDistanceToAnyPoint = float.MaxValue;
                    int nearestPointIndex = -1;

                    for (int i = 0; i < biomePoints.Count; i++)
                    {
                        float dist = Vector2.Distance(pixelPos, biomePoints[i]);
                        if (dist < minDistanceToAnyPoint)
                        {
                            minDistanceToAnyPoint = dist;
                            nearestPointIndex = i;
                        }
                    }

                    if (nearestPointIndex >= 0)
                    {
                        // Use the nearest point directly for distance calculation
                        Vector2 nearestPoint = biomePoints[nearestPointIndex];
                        float distanceToNearestPoint = Vector2.Distance(pixelPos, nearestPoint);

                        // Normalize the distance for grayscale
                        float maxDist = biomeSize * 1.5f;
                        float noise = Mathf.Clamp01(distanceToNearestPoint / maxDist);

                        // Convert to grayscale color
                        pixels[y * textureWidth + x] = new Color(noise, noise, noise);

                        // Add this pixel to the region it belongs to
                        int regionID = pointToRegionID[nearestPointIndex];
                        regions[regionID].AddPixel(new Vector2Int(x, y));
                    }
                    else
                    {
                        // No points found - use white
                        pixels[y * textureWidth + x] = Color.white;
                    }
                }
            }

            // Log pixel counts for each region
            foreach (var region in regions)
            {
                Debug.Log($"[CellularNoiseVisualizer] Region {region.id} has {region.PixelCount} pixels");
            }

            // Apply to texture
            noiseTexture.SetPixels(pixels);

            // Highlight the actual random points in yellow and draw red connection lines
            DrawBiomePoints(pixels, textureWidth, textureHeight, worldWidth, worldHeight);
            DrawConnections(pixels, textureWidth, textureHeight);

            noiseTexture.SetPixels(pixels);
            noiseTexture.Apply();

            // Apply texture to material
            if (meshRenderer != null && meshRenderer.sharedMaterial != null)
            {
                meshRenderer.sharedMaterial.mainTexture = noiseTexture;
            }

            // Create or update plane mesh
            CreatePlaneMesh(worldWidth, worldHeight);
        }

        private void GenerateBiomePoints()
        {
            int emptyBiomeCount = 0;
            List<Vector2Int> emptyBiomes = new List<Vector2Int>();

            // Calculate half dimensions for centered coordinates
            const int biomeSize = 128;
            float halfWidth = (worldSizeInBiomes.x * biomeSize) * 0.5f;
            float halfHeight = (worldSizeInBiomes.y * biomeSize) * 0.5f;

            // Generate all biome points
            for (int biomeY = 0; biomeY < worldSizeInBiomes.y; biomeY++)
            {
                for (int biomeX = 0; biomeX < worldSizeInBiomes.x; biomeX++)
                {
                    Vector2? point = GetBiomePointForVisualization(biomeX, biomeY);

                    if (point.HasValue)
                    {
                        biomePoints.Add(point.Value);
                        float centeredX = point.Value.x - halfWidth;
                        float centeredY = point.Value.y - halfHeight;
                        Debug.Log($"[CellularNoiseVisualizer] Biome ({biomeX}, {biomeY}) -> Raw: ({point.Value.x:F2}, {point.Value.y:F2}) | Centered: ({centeredX:F2}, {centeredY:F2})");
                    }
                    else
                    {
                        emptyBiomeCount++;
                        emptyBiomes.Add(new Vector2Int(biomeX, biomeY));
                    }
                }
            }

            // Log empty biomes if enabled
            if (logEmptyBiomes && emptyBiomeCount > 0)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine($"[CellularNoiseVisualizer] {emptyBiomeCount} empty biomes (10% chance per biome):");
                foreach (var biome in emptyBiomes)
                {
                    sb.AppendLine($"  - Biome ({biome.x}, {biome.y}) has no point");
                }
                Debug.Log(sb.ToString());
            }

            // Find connections between nearby points
            if (connectionDistance > 0)
            {
                for (int i = 0; i < biomePoints.Count; i++)
                {
                    for (int j = i + 1; j < biomePoints.Count; j++)
                    {
                        float distance = Vector2.Distance(biomePoints[i], biomePoints[j]);
                        if (distance <= connectionDistance)
                        {
                            connections.Add((i, j));
                        }
                    }
                }
            }
        }

        private void BuildRegionGroups()
        {
            // Initialize: each point is its own region
            for (int i = 0; i < biomePoints.Count; i++)
            {
                pointToRegionID[i] = i;
            }

            // Union-find: merge connected points into same region
            foreach (var connection in connections)
            {
                int regionA = FindRegion(connection.Item1);
                int regionB = FindRegion(connection.Item2);

                // Merge regions by setting all points in regionB to regionA
                if (regionA != regionB)
                {
                    for (int i = 0; i < biomePoints.Count; i++)
                    {
                        if (pointToRegionID[i] == regionB)
                        {
                            pointToRegionID[i] = regionA;
                        }
                    }
                }
            }

            // Renumber regions to be sequential (0, 1, 2, 3...)
            HashSet<int> uniqueRegionIDs = new HashSet<int>();
            foreach (var regionID in pointToRegionID.Values)
            {
                uniqueRegionIDs.Add(regionID);
            }

            // Create mapping from old IDs to new sequential IDs
            Dictionary<int, int> oldToNewID = new Dictionary<int, int>();
            int newID = 0;
            foreach (var oldID in uniqueRegionIDs)
            {
                oldToNewID[oldID] = newID;
                newID++;
            }

            // Update all points to use new sequential IDs
            List<int> keys = new List<int>(pointToRegionID.Keys);
            foreach (int pointIndex in keys)
            {
                pointToRegionID[pointIndex] = oldToNewID[pointToRegionID[pointIndex]];
            }

            // Create CellularRegion objects
            int numRegions = uniqueRegionIDs.Count;
            for (int i = 0; i < numRegions; i++)
            {
                regions.Add(new CellularRegion(i));
            }

            // Populate regions with their points
            for (int i = 0; i < biomePoints.Count; i++)
            {
                int regionID = pointToRegionID[i];
                regions[regionID].AddPoint(biomePoints[i]);
            }

            // Debug statistics
            int totalBiomes = worldSizeInBiomes.x * worldSizeInBiomes.y;
            int biomesWithPoints = biomePoints.Count;
            int biomesWithoutPoints = totalBiomes - biomesWithPoints;
            int totalConnections = connections.Count;
            int totalRegions = uniqueRegionIDs.Count;

            // Count points per region
            Dictionary<int, int> regionPointCounts = new Dictionary<int, int>();
            foreach (var regionID in pointToRegionID.Values)
            {
                if (!regionPointCounts.ContainsKey(regionID))
                {
                    regionPointCounts[regionID] = 0;
                }
                regionPointCounts[regionID]++;
            }

            // Count how many regions have multiple points (are connected)
            int connectedRegions = 0;
            int singlePointRegions = 0;
            foreach (var count in regionPointCounts.Values)
            {
                if (count > 1)
                {
                    connectedRegions++;
                }
                else
                {
                    singlePointRegions++;
                }
            }

            // Log comprehensive statistics (single line)
            Debug.Log($"[CellularNoiseVisualizer] Total Biomes: {totalBiomes} | With Points: {biomesWithPoints} | Without Points: {biomesWithoutPoints} | Connections: {totalConnections} | Total Regions: {totalRegions} (Connected: {connectedRegions}, Single: {singlePointRegions})");

            // Log each region and its points (before pixels are assigned)
            const int biomeSize = 128;
            float halfWidth = (worldSizeInBiomes.x * biomeSize) * 0.5f;
            float halfHeight = (worldSizeInBiomes.y * biomeSize) * 0.5f;

            foreach (var region in regions)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.Append($"[CellularNoiseVisualizer] Region {region.id} ({region.PointCount} points) - Centered: ");
                for (int i = 0; i < region.points.Count; i++)
                {
                    float centeredX = region.points[i].x - halfWidth;
                    float centeredY = region.points[i].y - halfHeight;
                    sb.Append($"({centeredX:F2}, {centeredY:F2})");
                    if (i < region.points.Count - 1)
                    {
                        sb.Append(", ");
                    }
                }
                Debug.Log(sb.ToString());
            }
        }

        private int FindRegion(int pointIndex)
        {
            return pointToRegionID[pointIndex];
        }

        private void DrawBiomePoints(Color[] pixels, int textureWidth, int textureHeight, int worldWidth, int worldHeight)
        {
            // Draw yellow markers at all point locations
            foreach (var point in biomePoints)
            {
                // Convert world position to pixel position
                int pixelX = Mathf.RoundToInt(point.x * pixelsPerUnit);
                int pixelY = Mathf.RoundToInt(point.y * pixelsPerUnit);

                // Draw a yellow cross/marker (7x7 pixels)
                int markerSize = 3;
                for (int dy = -markerSize; dy <= markerSize; dy++)
                {
                    for (int dx = -markerSize; dx <= markerSize; dx++)
                    {
                        int px = pixelX + dx;
                        int py = pixelY + dy;

                        // Check bounds
                        if (px >= 0 && px < textureWidth && py >= 0 && py < textureHeight)
                        {
                            // Draw cross pattern (center + vertical + horizontal + diagonals)
                            if (dx == 0 || dy == 0 || (Mathf.Abs(dx) == Mathf.Abs(dy)))
                            {
                                pixels[py * textureWidth + px] = Color.yellow;
                            }
                        }
                    }
                }
            }
        }

        private void DrawConnections(Color[] pixels, int textureWidth, int textureHeight)
        {
            // Draw red lines between connected points
            foreach (var connection in connections)
            {
                Vector2 pointA = biomePoints[connection.Item1];
                Vector2 pointB = biomePoints[connection.Item2];

                // Convert to pixel coordinates
                int x0 = Mathf.RoundToInt(pointA.x * pixelsPerUnit);
                int y0 = Mathf.RoundToInt(pointA.y * pixelsPerUnit);
                int x1 = Mathf.RoundToInt(pointB.x * pixelsPerUnit);
                int y1 = Mathf.RoundToInt(pointB.y * pixelsPerUnit);

                // Draw line using Bresenham's line algorithm
                DrawLine(pixels, textureWidth, textureHeight, x0, y0, x1, y1, Color.red);
            }
        }

        private void DrawLine(Color[] pixels, int textureWidth, int textureHeight, int x0, int y0, int x1, int y1, Color color)
        {
            // Bresenham's line algorithm
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                // Draw pixel if in bounds
                if (x0 >= 0 && x0 < textureWidth && y0 >= 0 && y0 < textureHeight)
                {
                    pixels[y0 * textureWidth + x0] = color;
                }

                // Check if we've reached the end
                if (x0 == x1 && y0 == y1) break;

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        private Vector2? GetBiomePointForVisualization(int biomeX, int biomeY)
        {
            // Same logic as NoiseGenerator.GetBiomePoint to match the noise generation
            int biomeSeed = seed;
            biomeSeed ^= biomeX.GetHashCode();
            biomeSeed = (biomeSeed << 5) + biomeSeed + biomeY.GetHashCode();

            System.Random random = new System.Random(biomeSeed);

            // 10% chance this biome has no point
            if (random.NextDouble() < 0.1)
            {
                return null;
            }

            // Generate random point within the biome, but never in the exact middle
            // Use polar coordinates to ensure point is at least a certain distance from center
            float centerX = biomeSize * 0.5f;
            float centerY = biomeSize * 0.5f;

            float minDistanceFromCenter = biomeSize * 0.2f; // At least 20% away from center
            float maxDistanceFromCenter = biomeSize * 0.85f; // Max ~85% (to stay within biome bounds)

            // Random angle (0 to 2π)
            float angle = (float)(random.NextDouble() * 2.0 * Mathf.PI);

            // Random distance between min and max
            float distance = minDistanceFromCenter + (float)random.NextDouble() * (maxDistanceFromCenter - minDistanceFromCenter);

            // Convert polar to cartesian and add to center
            float offsetX = centerX + distance * Mathf.Cos(angle);
            float offsetY = centerY + distance * Mathf.Sin(angle);
            

            return new Vector2(biomeX * biomeSize + offsetX, biomeY * biomeSize + offsetY);
        }

        private void CreatePlaneMesh(int worldWidth, int worldHeight)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Cellular Noise Plane";

            // Create a simple quad centered at origin
            float halfWidth = worldWidth * 0.5f;
            float halfHeight = worldHeight * 0.5f;

            Vector3[] vertices = new Vector3[4]
            {
                new Vector3(-halfWidth, 0, -halfHeight),
                new Vector3(halfWidth, 0, -halfHeight),
                new Vector3(-halfWidth, 0, halfHeight),
                new Vector3(halfWidth, 0, halfHeight)
            };

            Vector2[] uv = new Vector2[4]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1)
            };

            int[] triangles = new int[6]
            {
                0, 2, 1,
                2, 3, 1
            };

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();

            meshFilter.mesh = mesh;
        }

        private void OnDisable()
        {
            if (noiseTexture != null)
            {
                DestroyImmediate(noiseTexture);
            }
        }

        private void CalculateAndStoreCellCorners()
        {
            cellCorners.Clear();

            // Calculate world bounds
            float worldWidth = worldSizeInBiomes.x * biomeSize;
            float worldHeight = worldSizeInBiomes.y * biomeSize;

            // For each triplet of points, calculate circumcenter
            for (int i = 0; i < biomePoints.Count; i++)
            {
                for (int j = i + 1; j < biomePoints.Count; j++)
                {
                    for (int k = j + 1; k < biomePoints.Count; k++)
                    {
                        Vector2 p1 = biomePoints[i];
                        Vector2 p2 = biomePoints[j];
                        Vector2 p3 = biomePoints[k];

                        // Calculate circumcenter of triangle formed by p1, p2, p3
                        Vector2? circumcenter = CalculateCircumcenter(p1, p2, p3);

                        if (circumcenter.HasValue)
                        {
                            Vector2 center = circumcenter.Value;

                            // Check if this point is within world bounds
                            if (center.x < 0 || center.x > worldWidth || center.y < 0 || center.y > worldHeight)
                                continue;

                            // Check if this is a valid Voronoi vertex
                            // (no other points should be closer to this center than p1, p2, p3)
                            float radius = Vector2.Distance(center, p1);
                            bool isValid = true;

                            for (int m = 0; m < biomePoints.Count; m++)
                            {
                                if (m == i || m == j || m == k) continue;

                                float dist = Vector2.Distance(center, biomePoints[m]);
                                if (dist < radius - 0.1f) // Small tolerance for floating point errors
                                {
                                    isValid = false;
                                    break;
                                }
                            }

                            if (isValid)
                            {
                                // Create corner data with connected regions
                                CellCornerData cornerData = new CellCornerData(center);

                                // Get region IDs for the three points that form this corner
                                int regionI = pointToRegionID[i];
                                int regionJ = pointToRegionID[j];
                                int regionK = pointToRegionID[k];

                                // Add unique region IDs
                                if (!cornerData.connectedRegions.Contains(regionI))
                                    cornerData.connectedRegions.Add(regionI);
                                if (!cornerData.connectedRegions.Contains(regionJ))
                                    cornerData.connectedRegions.Add(regionJ);
                                if (!cornerData.connectedRegions.Contains(regionK))
                                    cornerData.connectedRegions.Add(regionK);

                                cellCorners.Add(cornerData);
                            }
                        }
                    }
                }
            }

            // Calculate edge corners (where 2 cells meet at world boundaries)
            CalculateEdgeCorners(worldWidth, worldHeight);

            Debug.Log($"[CellularNoiseVisualizer] Generated {cellCorners.Count} cell corners");
        }

        private void CalculateEdgeCorners(float worldWidth, float worldHeight)
        {
            // For each pair of points, find where their Voronoi edge intersects with world boundaries
            for (int i = 0; i < biomePoints.Count; i++)
            {
                for (int j = i + 1; j < biomePoints.Count; j++)
                {
                    Vector2 p1 = biomePoints[i];
                    Vector2 p2 = biomePoints[j];

                    // Calculate midpoint
                    Vector2 midpoint = (p1 + p2) * 0.5f;

                    // Calculate perpendicular direction (Voronoi edge direction)
                    Vector2 edge = p2 - p1;
                    Vector2 perpendicular = new Vector2(-edge.y, edge.x).normalized;

                    // Check if these two points are neighbors (their cells share an edge)
                    // They're likely neighbors if they're relatively close
                    float distance = Vector2.Distance(p1, p2);
                    if (distance > biomeSize * 3f) continue; // Skip distant points

                    // Cast ray in both perpendicular directions to find boundary intersections
                    Vector2[] directions = { perpendicular, -perpendicular };

                    foreach (Vector2 direction in directions)
                    {
                        Vector2? intersection = RaycastToBoundary(midpoint, direction, worldWidth, worldHeight);

                        if (intersection.HasValue)
                        {
                            Vector2 intersectionPoint = intersection.Value;

                            // Check if this edge point is valid (not closer to any other point)
                            bool isValid = true;
                            float dist1 = Vector2.Distance(intersectionPoint, p1);

                            for (int k = 0; k < biomePoints.Count; k++)
                            {
                                if (k == i || k == j) continue;

                                float distK = Vector2.Distance(intersectionPoint, biomePoints[k]);
                                if (distK < dist1 - 1f) // Tolerance for edge cases
                                {
                                    isValid = false;
                                    break;
                                }
                            }

                            if (isValid)
                            {
                                // Check if this corner already exists (avoid duplicates)
                                bool alreadyExists = false;
                                foreach (var existing in cellCorners)
                                {
                                    if (Vector2.Distance(existing.position, intersectionPoint) < 1f)
                                    {
                                        alreadyExists = true;
                                        break;
                                    }
                                }

                                if (!alreadyExists)
                                {
                                    // Create edge corner with 2 connected regions
                                    CellCornerData cornerData = new CellCornerData(intersectionPoint);
                                    int regionI = pointToRegionID[i];
                                    int regionJ = pointToRegionID[j];

                                    if (!cornerData.connectedRegions.Contains(regionI))
                                        cornerData.connectedRegions.Add(regionI);
                                    if (!cornerData.connectedRegions.Contains(regionJ))
                                        cornerData.connectedRegions.Add(regionJ);

                                    cellCorners.Add(cornerData);
                                }
                            }
                        }
                    }
                }
            }
        }

        private Vector2? RaycastToBoundary(Vector2 origin, Vector2 direction, float worldWidth, float worldHeight)
        {
            // Find intersection with world boundaries
            Vector2? closestIntersection = null;
            float closestDistance = float.MaxValue;

            // Check intersection with each boundary
            // Left boundary (x = 0)
            if (direction.x < 0)
            {
                float t = (0 - origin.x) / direction.x;
                Vector2 point = origin + direction * t;
                if (point.y >= 0 && point.y <= worldHeight && t > 0)
                {
                    float dist = Vector2.Distance(origin, point);
                    if (dist < closestDistance)
                    {
                        closestDistance = dist;
                        closestIntersection = point;
                    }
                }
            }

            // Right boundary (x = worldWidth)
            if (direction.x > 0)
            {
                float t = (worldWidth - origin.x) / direction.x;
                Vector2 point = origin + direction * t;
                if (point.y >= 0 && point.y <= worldHeight && t > 0)
                {
                    float dist = Vector2.Distance(origin, point);
                    if (dist < closestDistance)
                    {
                        closestDistance = dist;
                        closestIntersection = point;
                    }
                }
            }

            // Bottom boundary (y = 0)
            if (direction.y < 0)
            {
                float t = (0 - origin.y) / direction.y;
                Vector2 point = origin + direction * t;
                if (point.x >= 0 && point.x <= worldWidth && t > 0)
                {
                    float dist = Vector2.Distance(origin, point);
                    if (dist < closestDistance)
                    {
                        closestDistance = dist;
                        closestIntersection = point;
                    }
                }
            }

            // Top boundary (y = worldHeight)
            if (direction.y > 0)
            {
                float t = (worldHeight - origin.y) / direction.y;
                Vector2 point = origin + direction * t;
                if (point.x >= 0 && point.x <= worldWidth && t > 0)
                {
                    float dist = Vector2.Distance(origin, point);
                    if (dist < closestDistance)
                    {
                        closestDistance = dist;
                        closestIntersection = point;
                    }
                }
            }

            return closestIntersection;
        }

        private Vector2? CalculateCircumcenter(Vector2 a, Vector2 b, Vector2 c)
        {
            // Calculate circumcenter using the formula for the circumcenter of a triangle
            float d = 2 * (a.x * (b.y - c.y) + b.x * (c.y - a.y) + c.x * (a.y - b.y));

            // If points are collinear, no circumcenter exists
            if (Mathf.Abs(d) < 0.001f)
                return null;

            float aSq = a.x * a.x + a.y * a.y;
            float bSq = b.x * b.x + b.y * b.y;
            float cSq = c.x * c.x + c.y * c.y;

            float ux = (aSq * (b.y - c.y) + bSq * (c.y - a.y) + cSq * (a.y - b.y)) / d;
            float uy = (aSq * (c.x - b.x) + bSq * (a.x - c.x) + cSq * (b.x - a.x)) / d;

            return new Vector2(ux, uy);
        }

        private void OnDrawGizmosSelected()
        {
            // Calculate world size
            float worldWidth = worldSizeInBiomes.x * biomeSize;
            float worldHeight = worldSizeInBiomes.y * biomeSize;
            float halfWidth = worldWidth * 0.5f;
            float halfHeight = worldHeight * 0.5f;

            // Draw world bounds centered at origin
            Gizmos.color = Color.cyan;
            Vector3 center = transform.position;
            Vector3 size = new Vector3(worldWidth, 0.1f, worldHeight);
            Gizmos.DrawWireCube(center, size);

            // Draw biome grid centered at origin
            Gizmos.color = new Color(0, 1, 1, 0.3f);
            for (int x = 0; x <= worldSizeInBiomes.x; x++)
            {
                float xPos = x * biomeSize - halfWidth;
                Vector3 start = transform.position + new Vector3(xPos, 0, -halfHeight);
                Vector3 end = transform.position + new Vector3(xPos, 0, halfHeight);
                Gizmos.DrawLine(start, end);
            }
            for (int z = 0; z <= worldSizeInBiomes.y; z++)
            {
                float zPos = z * biomeSize - halfHeight;
                Vector3 start = transform.position + new Vector3(-halfWidth, 0, zPos);
                Vector3 end = transform.position + new Vector3(halfWidth, 0, zPos);
                Gizmos.DrawLine(start, end);
            }

            // Generate temporary point list for gizmo visualization
            List<Vector2> gizmoPoints = new List<Vector2>();
            for (int biomeY = 0; biomeY < worldSizeInBiomes.y; biomeY++)
            {
                for (int biomeX = 0; biomeX < worldSizeInBiomes.x; biomeX++)
                {
                    Vector2? point = GetBiomePointForVisualization(biomeX, biomeY);
                    if (point.HasValue)
                    {
                        gizmoPoints.Add(point.Value);
                    }
                }
            }

            // Draw yellow spheres at biome points (centered)
            Gizmos.color = Color.yellow;
            foreach (var point in gizmoPoints)
            {
                Vector3 worldPos = transform.position + new Vector3(point.x - halfWidth, 0.5f, point.y - halfHeight);
                Gizmos.DrawSphere(worldPos, 0.5f);
            }

            // Draw cell corners (Voronoi vertices) from stored data
            if (cellCorners != null && cellCorners.Count > 0)
            {
                Gizmos.color = new Color(1f, 0f, 1f, 1f); // Bright magenta/purple
                foreach (var corner in cellCorners)
                {
                    Vector3 worldPos = transform.position + new Vector3(corner.position.x - halfWidth, 2f, corner.position.y - halfHeight);
                    Gizmos.DrawSphere(worldPos, 5f); // Extra large sphere - 5 units radius
                }
            }

#if UNITY_EDITOR
            // Draw region numbers at center of mass
            if (showRegionNumbers && biomePoints != null && biomePoints.Count > 0 && pointToRegionID != null && pointToRegionID.Count > 0)
            {
                // Calculate center of mass for each unique region
                Dictionary<int, Vector2> regionCenters = new Dictionary<int, Vector2>();
                Dictionary<int, int> regionPointCounts = new Dictionary<int, int>();

                for (int i = 0; i < biomePoints.Count; i++)
                {
                    int regionID = pointToRegionID[i];

                    if (!regionCenters.ContainsKey(regionID))
                    {
                        regionCenters[regionID] = Vector2.zero;
                        regionPointCounts[regionID] = 0;
                    }

                    regionCenters[regionID] += biomePoints[i];
                    regionPointCounts[regionID]++;
                }

                // Average the positions to get center of mass
                foreach (var kvp in regionCenters)
                {
                    int regionID = kvp.Key;
                    Vector2 regionCenter = kvp.Value / (float)regionPointCounts[regionID];

                    Vector3 worldPos = transform.position + new Vector3(regionCenter.x - halfWidth, 1f, regionCenter.y - halfHeight);

                    // Draw white text with black outline for visibility
                    GUIStyle style = new GUIStyle();
                    style.normal.textColor = Color.white;
                    style.fontSize = 20;
                    style.fontStyle = FontStyle.Bold;
                    style.alignment = TextAnchor.MiddleCenter;

                    Handles.Label(worldPos, regionID.ToString(), style);
                }
            }
#endif
        }
    }
}
