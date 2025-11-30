using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LandOfTheConsumers.Procedural
{
    [ExecuteInEditMode]
    public class CellularNoiseVisualizer : MonoBehaviour
    {
        [Header("World Size")]
        [Tooltip("World size in chunks (X, Z). Example: (10, 10) = 10x10 chunks")]
        [SerializeField] public Vector2Int worldSizeInChunks = new Vector2Int(10, 10);

        private const int chunkSize = 32;

        [Tooltip("Pixels per world unit (controls texture detail)\n\n" +
                 "• 1 = 1 pixel per unit (low detail, fast)\n" +
                 "• 2-4 = Medium detail (recommended)\n" +
                 "• 8-16 = High detail (slower)")]
        [SerializeField] [Range(1, 16)] public int pixelsPerUnit = 2;

        [Header("Noise Settings")]
        [Tooltip("SEED - Random seed for noise generation\n\n" +
                 "Same seed = identical pattern every time\n" +
                 "Change seed = completely different pattern\n\n" +
                 "Each chunk has a 90% chance of having ONE random point\n" +
                 "10% of chunks will have no point (more organic!)")]
        [SerializeField] public int seed = 12345;

        [Tooltip("CONNECTION DISTANCE - Points within this distance connect into one larger region\n\n" +
                 "• 0 = No connections (default Voronoi)\n" +
                 "• 20-40 = Small connections\n" +
                 "• 50-80 = Medium connections\n" +
                 "• 100+ = Large connected regions\n\n" +
                 "Connected points shown with RED LINES")]
        [SerializeField] [Range(0f, 150f)] public float connectionDistance = 50f;

        [Header("Display")]
        [Tooltip("Automatically regenerate when settings change (Editor only)")]
        [SerializeField] private bool autoRegenerate = false;

        [Tooltip("Log empty chunks to console")]
        [SerializeField] private bool logEmptyChunks = true;

        [Tooltip("Show region numbers in scene view")]
        [SerializeField] private bool showRegionNumbers = true;

        private Texture2D noiseTexture;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;

        private int lastSeed;
        private Vector2Int lastWorldSize;
        private int lastChunkSize;
        private int lastPixelsPerUnit;
        private float lastConnectionDistance;

        // Store chunk points and connections for visualization
        private List<Vector2> chunkPoints = new List<Vector2>();
        private List<(int, int)> connections = new List<(int, int)>(); // Pairs of point indices that are connected
        private Dictionary<int, int> pointToRegionID = new Dictionary<int, int>(); // Maps point index to region ID

        private void OnEnable()
        {
            SetupComponents();
            GenerateNoiseTexture();
        }

        private void Update()
        {
            if (!Application.isPlaying && autoRegenerate)
            {
                // Check if any settings changed
                if (seed != lastSeed ||
                    worldSizeInChunks != lastWorldSize ||
                    chunkSize != lastChunkSize ||
                    pixelsPerUnit != lastPixelsPerUnit ||
                    !Mathf.Approximately(connectionDistance, lastConnectionDistance))
                {
                    GenerateNoiseTexture();
                }
            }
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

                // Create a default unlit material
                Material mat = new Material(Shader.Find("Unlit/Texture"));
                meshRenderer.sharedMaterial = mat;
            }
        }

        [ContextMenu("Generate Noise")]
        public void GenerateNoiseTexture()
        {
            // Validate settings
            if (worldSizeInChunks.x <= 0 || worldSizeInChunks.y <= 0)
            {
                Debug.LogWarning("[CellularNoiseVisualizer] World size must be greater than 0");
                return;
            }

            if (chunkSize <= 0)
            {
                Debug.LogWarning("[CellularNoiseVisualizer] Chunk size must be greater than 0");
                return;
            }

            SetupComponents();

            // Clear previous data
            chunkPoints.Clear();
            connections.Clear();
            pointToRegionID.Clear();

            // Calculate world size in units
            int worldWidth = worldSizeInChunks.x * chunkSize;
            int worldHeight = worldSizeInChunks.y * chunkSize;

            // Generate all chunk points and log empty chunks
            GenerateChunkPoints();

            // Build region groups from connections
            BuildRegionGroups();

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

                    for (int i = 0; i < chunkPoints.Count; i++)
                    {
                        float dist = Vector2.Distance(pixelPos, chunkPoints[i]);
                        if (dist < minDistanceToAnyPoint)
                        {
                            minDistanceToAnyPoint = dist;
                            nearestPointIndex = i;
                        }
                    }

                    if (nearestPointIndex >= 0)
                    {
                        // Get the region ID for this pixel
                        int regionID = pointToRegionID[nearestPointIndex];

                        // Calculate center of mass for the entire region
                        Vector2 regionCenter = Vector2.zero;
                        int regionPointCount = 0;

                        for (int i = 0; i < chunkPoints.Count; i++)
                        {
                            if (pointToRegionID[i] == regionID)
                            {
                                regionCenter += chunkPoints[i];
                                regionPointCount++;
                            }
                        }

                        if (regionPointCount > 0)
                        {
                            regionCenter /= regionPointCount;

                            // Calculate distance from pixel to region center
                            float distanceToRegionCenter = Vector2.Distance(pixelPos, regionCenter);

                            // Normalize the distance for grayscale
                            float maxDist = chunkSize * 1.5f * Mathf.Sqrt(regionPointCount); // Scale by region size
                            float noise = Mathf.Clamp01(distanceToRegionCenter / maxDist);

                            // Convert to grayscale color
                            pixels[y * textureWidth + x] = new Color(noise, noise, noise);
                        }
                        else
                        {
                            pixels[y * textureWidth + x] = Color.white;
                        }
                    }
                    else
                    {
                        // No points found - use white
                        pixels[y * textureWidth + x] = Color.white;
                    }
                }
            }

            // Apply to texture
            noiseTexture.SetPixels(pixels);

            // Highlight the actual random points in yellow and draw red connection lines
            DrawChunkPoints(pixels, textureWidth, textureHeight, worldWidth, worldHeight);
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

            // Store last values
            lastSeed = seed;
            lastWorldSize = worldSizeInChunks;
            lastChunkSize = chunkSize;
            lastPixelsPerUnit = pixelsPerUnit;
            lastConnectionDistance = connectionDistance;

            // Count unique regions
            HashSet<int> uniqueRegions = new HashSet<int>();
            foreach (var regionID in pointToRegionID.Values)
            {
                uniqueRegions.Add(regionID);
            }

            Debug.Log($"[CellularNoiseVisualizer] Generated {textureWidth}x{textureHeight} texture " +
                      $"for {worldSizeInChunks.x}x{worldSizeInChunks.y} chunks ({worldWidth}x{worldHeight} world units) " +
                      $"- {chunkPoints.Count} points, {connections.Count} connections, {uniqueRegions.Count} regions (Seed: {seed})");
        }

        private void GenerateChunkPoints()
        {
            int emptyChunkCount = 0;
            List<Vector2Int> emptyChunks = new List<Vector2Int>();

            // Generate all chunk points
            for (int chunkY = 0; chunkY < worldSizeInChunks.y; chunkY++)
            {
                for (int chunkX = 0; chunkX < worldSizeInChunks.x; chunkX++)
                {
                    Vector2? point = GetChunkPointForVisualization(chunkX, chunkY);

                    if (point.HasValue)
                    {
                        chunkPoints.Add(point.Value);
                    }
                    else
                    {
                        emptyChunkCount++;
                        emptyChunks.Add(new Vector2Int(chunkX, chunkY));
                    }
                }
            }

            // Log empty chunks if enabled
            if (logEmptyChunks && emptyChunkCount > 0)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine($"[CellularNoiseVisualizer] {emptyChunkCount} empty chunks (10% chance per chunk):");
                foreach (var chunk in emptyChunks)
                {
                    sb.AppendLine($"  - Chunk ({chunk.x}, {chunk.y}) has no point");
                }
                Debug.Log(sb.ToString());
            }

            // Find connections between nearby points
            if (connectionDistance > 0)
            {
                for (int i = 0; i < chunkPoints.Count; i++)
                {
                    for (int j = i + 1; j < chunkPoints.Count; j++)
                    {
                        float distance = Vector2.Distance(chunkPoints[i], chunkPoints[j]);
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
            for (int i = 0; i < chunkPoints.Count; i++)
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
                    for (int i = 0; i < chunkPoints.Count; i++)
                    {
                        if (pointToRegionID[i] == regionB)
                        {
                            pointToRegionID[i] = regionA;
                        }
                    }
                }
            }
        }

        private int FindRegion(int pointIndex)
        {
            return pointToRegionID[pointIndex];
        }

        private void DrawChunkPoints(Color[] pixels, int textureWidth, int textureHeight, int worldWidth, int worldHeight)
        {
            // Draw yellow markers at all point locations
            foreach (var point in chunkPoints)
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
                Vector2 pointA = chunkPoints[connection.Item1];
                Vector2 pointB = chunkPoints[connection.Item2];

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

        private Vector2? GetChunkPointForVisualization(int chunkX, int chunkY)
        {
            // Same logic as NoiseGenerator.GetChunkPoint to match the noise generation
            int chunkSeed = seed;
            chunkSeed ^= chunkX.GetHashCode();
            chunkSeed = (chunkSeed << 5) + chunkSeed + chunkY.GetHashCode();

            System.Random random = new System.Random(chunkSeed);

            // 10% chance this chunk has no point
            if (random.NextDouble() < 0.1)
            {
                return null;
            }

            // Generate random point within the chunk
            float offsetX = (float)random.NextDouble() * chunkSize;
            float offsetY = (float)random.NextDouble() * chunkSize;
            return new Vector2(chunkX * chunkSize + offsetX, chunkY * chunkSize + offsetY);
        }

        private void CreatePlaneMesh(int worldWidth, int worldHeight)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Cellular Noise Plane";

            // Create a simple quad
            Vector3[] vertices = new Vector3[4]
            {
                new Vector3(0, 0, 0),
                new Vector3(worldWidth, 0, 0),
                new Vector3(0, 0, worldHeight),
                new Vector3(worldWidth, 0, worldHeight)
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

        private void OnDrawGizmosSelected()
        {
            // Calculate world size
            float worldWidth = worldSizeInChunks.x * chunkSize;
            float worldHeight = worldSizeInChunks.y * chunkSize;

            // Draw world bounds
            Gizmos.color = Color.cyan;
            Vector3 center = transform.position + new Vector3(worldWidth * 0.5f, 0, worldHeight * 0.5f);
            Vector3 size = new Vector3(worldWidth, 0.1f, worldHeight);
            Gizmos.DrawWireCube(center, size);

            // Draw chunk grid
            Gizmos.color = new Color(0, 1, 1, 0.3f);
            for (int x = 0; x <= worldSizeInChunks.x; x++)
            {
                Vector3 start = transform.position + new Vector3(x * chunkSize, 0, 0);
                Vector3 end = transform.position + new Vector3(x * chunkSize, 0, worldHeight);
                Gizmos.DrawLine(start, end);
            }
            for (int z = 0; z <= worldSizeInChunks.y; z++)
            {
                Vector3 start = transform.position + new Vector3(0, 0, z * chunkSize);
                Vector3 end = transform.position + new Vector3(worldWidth, 0, z * chunkSize);
                Gizmos.DrawLine(start, end);
            }

            // Generate temporary point list for gizmo visualization
            List<Vector2> gizmoPoints = new List<Vector2>();
            for (int chunkY = 0; chunkY < worldSizeInChunks.y; chunkY++)
            {
                for (int chunkX = 0; chunkX < worldSizeInChunks.x; chunkX++)
                {
                    Vector2? point = GetChunkPointForVisualization(chunkX, chunkY);
                    if (point.HasValue)
                    {
                        gizmoPoints.Add(point.Value);
                    }
                }
            }

            // Draw yellow spheres at chunk points
            Gizmos.color = Color.yellow;
            foreach (var point in gizmoPoints)
            {
                Vector3 worldPos = transform.position + new Vector3(point.x, 0.5f, point.y);
                Gizmos.DrawSphere(worldPos, 0.5f);
            }

#if UNITY_EDITOR
            // Draw region numbers at center of mass
            if (showRegionNumbers && chunkPoints != null && chunkPoints.Count > 0 && pointToRegionID != null && pointToRegionID.Count > 0)
            {
                // Calculate center of mass for each unique region
                Dictionary<int, Vector2> regionCenters = new Dictionary<int, Vector2>();
                Dictionary<int, int> regionPointCounts = new Dictionary<int, int>();

                for (int i = 0; i < chunkPoints.Count; i++)
                {
                    int regionID = pointToRegionID[i];

                    if (!regionCenters.ContainsKey(regionID))
                    {
                        regionCenters[regionID] = Vector2.zero;
                        regionPointCounts[regionID] = 0;
                    }

                    regionCenters[regionID] += chunkPoints[i];
                    regionPointCounts[regionID]++;
                }

                // Average the positions to get center of mass
                foreach (var kvp in regionCenters)
                {
                    int regionID = kvp.Key;
                    Vector2 regionCenter = kvp.Value / (float)regionPointCounts[regionID];

                    Vector3 worldPos = transform.position + new Vector3(regionCenter.x, 1f, regionCenter.y);

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
