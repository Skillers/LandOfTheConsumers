using UnityEngine;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LandOfTheConsumers.Procedural
{
    /// <summary>
    /// Manages generation of edges between region pairs.
    /// Creates 3-pixel-wide edges along Voronoi boundaries.
    /// </summary>
    public class EdgeManager : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Reference to the CellularNoiseVisualizer containing corner and edge data")]
        [SerializeField] private CellularNoiseVisualizer cellularVisualizer;

        [Tooltip("Reference to the RegionQuadVisualizer for accessing region presets")]
        [SerializeField] private RegionQuadVisualizer regionQuadVisualizer;

        [Header("Edge Material")]
        [Tooltip("Material to use for edge meshes (optional - will create default gray if null)")]
        [SerializeField] private Material edgeMaterial;

        [Header("Auto-Generation")]
        [Tooltip("Automatically generate edges in play mode after LOD0 is complete")]
        [SerializeField] private bool autoGenerateInPlayMode = true;

        // Internal data
        private List<EdgeData> allEdges = new List<EdgeData>();
        private Queue<EdgeGenerationTask> edgeQueue = new Queue<EdgeGenerationTask>();
        private bool isProcessingQueue = false;
        private bool cancelRequested = false;

        private class EdgeGenerationTask
        {
            public EdgeData edgeData;
            public GameObject edgePairObject;
            public EdgePairGenerator generator;
        }

        private void Start()
        {
            // In play mode, subscribe to LOD0 completion event
            if (Application.isPlaying && autoGenerateInPlayMode)
            {
                if (regionQuadVisualizer != null)
                {
                    regionQuadVisualizer.OnLOD0GenerationComplete += OnLOD0Complete;
                    Debug.Log("[EdgeManager] Subscribed to LOD0 completion event - will auto-generate edges");
                }
                else
                {
                    Debug.LogWarning("[EdgeManager] RegionQuadVisualizer not assigned - cannot auto-generate edges");
                }
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from event
            if (regionQuadVisualizer != null)
            {
                regionQuadVisualizer.OnLOD0GenerationComplete -= OnLOD0Complete;
            }
        }

        private void OnLOD0Complete()
        {
            Debug.Log("[EdgeManager] LOD0 generation complete - starting edge generation");
            GenerateAllEdges();
        }

        [ContextMenu("Generate All Edges")]
        public void GenerateAllEdges()
        {
            if (cellularVisualizer == null)
            {
                Debug.LogError("[EdgeManager] CellularNoiseVisualizer not assigned!");
                return;
            }

            if (regionQuadVisualizer == null)
            {
                Debug.LogError("[EdgeManager] RegionQuadVisualizer not assigned!");
                return;
            }

            // Clear existing edges
            ClearChildEdges();
            allEdges.Clear();
            edgeQueue.Clear();

            // Get corner and edge data
            List<CellCornerData> corners = cellularVisualizer.CellCorners;
            List<(int, int)> cornerEdges = cellularVisualizer.CornerEdges;

            if (corners == null || cornerEdges == null || corners.Count == 0 || cornerEdges.Count == 0)
            {
                Debug.LogError("[EdgeManager] No corner data available! Generate regions first.");
                return;
            }

            Debug.Log($"[EdgeManager] Processing {cornerEdges.Count} corner edges from {corners.Count} corners");

            // Process each corner edge to extract region pairs
            Dictionary<(int, int), EdgeData> edgePairMap = new Dictionary<(int, int), EdgeData>();

            foreach (var cornerEdge in cornerEdges)
            {
                CellCornerData corner1 = corners[cornerEdge.Item1];
                CellCornerData corner2 = corners[cornerEdge.Item2];

                // Find shared regions (should be exactly 2)
                List<int> sharedRegions = new List<int>();
                foreach (int regionId in corner1.connectedRegions)
                {
                    if (corner2.connectedRegions.Contains(regionId))
                    {
                        sharedRegions.Add(regionId);
                    }
                }

                if (sharedRegions.Count == 2)
                {
                    int regionA = Mathf.Min(sharedRegions[0], sharedRegions[1]);
                    int regionB = Mathf.Max(sharedRegions[0], sharedRegions[1]);

                    // Create or get EdgeData for this pair
                    var key = (regionA, regionB);
                    if (!edgePairMap.ContainsKey(key))
                    {
                        EdgeData edgeData = new EdgeData(regionA, regionB);
                        edgePairMap[key] = edgeData;
                        allEdges.Add(edgeData);
                    }

                    // Rasterize this corner edge segment
                    RasterizeEdgeSegment(edgePairMap[key], corner1.position, corner2.position);
                }
            }

            Debug.Log($"[EdgeManager] Found {allEdges.Count} unique edge pairs");

            // Build generation queue
            BuildEdgeGenerationQueue();

            // Start processing
            if (!isProcessingQueue && edgeQueue.Count > 0)
            {
                StartCoroutine(ProcessEdgeQueue());
            }
        }

        /// <summary>
        /// Rasterizes an edge segment from corner1 to corner2 and adds pixels to edgeData
        /// </summary>
        private void RasterizeEdgeSegment(EdgeData edgeData, Vector2 corner1World, Vector2 corner2World)
        {
            // Convert from world coordinates to pixel coordinates
            int pixelsPerUnit = cellularVisualizer.pixelsPerUnit;

            int x0 = Mathf.RoundToInt(corner1World.x * pixelsPerUnit);
            int y0 = Mathf.RoundToInt(corner1World.y * pixelsPerUnit);
            int x1 = Mathf.RoundToInt(corner2World.x * pixelsPerUnit);
            int y1 = Mathf.RoundToInt(corner2World.y * pixelsPerUnit);

            // 1. Rasterize 1-pixel-wide line using Bresenham's algorithm
            List<Vector2Int> centerLine = BresenhamLine(x0, y0, x1, y1);

            // 2. Store center line pixels (for height calculation)
            foreach (var pixel in centerLine)
            {
                edgeData.AddCenterPixel(pixel);
            }

            // 3. Expand to 3-pixel width
            HashSet<Vector2Int> thickLine = ExpandTo3PixelWidth(centerLine);

            // 4. Add all pixels to edge data (for mesh generation)
            foreach (var pixel in thickLine)
            {
                edgeData.AddPixel(pixel);
            }
        }

        /// <summary>
        /// Bresenham's line algorithm to rasterize a line between two points
        /// </summary>
        private List<Vector2Int> BresenhamLine(int x0, int y0, int x1, int y1)
        {
            List<Vector2Int> pixels = new List<Vector2Int>();

            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                pixels.Add(new Vector2Int(x0, y0));

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

            return pixels;
        }

        /// <summary>
        /// Expands a 1-pixel-wide line to approximately 3-pixel width
        /// by adding all 8-connected neighbors for each center pixel
        /// </summary>
        private HashSet<Vector2Int> ExpandTo3PixelWidth(List<Vector2Int> centerLine)
        {
            HashSet<Vector2Int> result = new HashSet<Vector2Int>();

            // For each pixel in center line, add itself and perpendicular neighbors
            foreach (var pixel in centerLine)
            {
                // Add center pixel
                result.Add(pixel);

                // Add 8-connected neighbors to create 3-pixel width
                // This ensures smooth coverage regardless of line orientation
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        result.Add(new Vector2Int(pixel.x + dx, pixel.y + dy));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Builds the queue of edge generation tasks
        /// </summary>
        private void BuildEdgeGenerationQueue()
        {
            edgeQueue.Clear();

            foreach (var edgeData in allEdges)
            {
                // Create GameObject hierarchy
                GameObject edgePairObject = new GameObject(edgeData.GetPairName());
                edgePairObject.transform.SetParent(transform);
                edgePairObject.transform.localPosition = Vector3.zero;
                edgePairObject.transform.localRotation = Quaternion.identity;
                edgePairObject.transform.localScale = Vector3.one;

                // Add EdgePairGenerator component
                EdgePairGenerator generator = edgePairObject.AddComponent<EdgePairGenerator>();
                generator.edgeData = edgeData;
                generator.cellularVisualizer = cellularVisualizer;
                generator.regionQuadVisualizer = regionQuadVisualizer;
                generator.edgeMaterial = edgeMaterial;

                // Add to queue
                edgeQueue.Enqueue(new EdgeGenerationTask
                {
                    edgeData = edgeData,
                    edgePairObject = edgePairObject,
                    generator = generator
                });
            }

            Debug.Log($"[EdgeManager] Built queue with {edgeQueue.Count} edge pairs");
        }

        /// <summary>
        /// Processes the edge generation queue sequentially
        /// </summary>
        private IEnumerator ProcessEdgeQueue()
        {
            isProcessingQueue = true;
            cancelRequested = false;

            int tasksProcessed = 0;
            int totalTasks = edgeQueue.Count;

            while (edgeQueue.Count > 0 && !cancelRequested)
            {
                EdgeGenerationTask task = edgeQueue.Dequeue();
                tasksProcessed++;

                Debug.Log($"[EdgeManager] Generating edge {task.edgeData.GetPairName()} ({tasksProcessed}/{totalTasks}) with {task.edgeData.edgePixels.Count} pixels");

                bool generationComplete = false;
                task.generator.OnGenerationComplete = () => { generationComplete = true; };

                // Start generation
                task.generator.GenerateEdgeMesh();

                // Wait for completion
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
            }

            if (cancelRequested)
            {
                Debug.Log($"[EdgeManager] Generation cancelled. {tasksProcessed}/{totalTasks} completed.");
            }
            else
            {
                Debug.Log($"[EdgeManager] Completed all {totalTasks} edges");
            }

            isProcessingQueue = false;
            cancelRequested = false;
        }

        [ContextMenu("Cancel Generation")]
        public void CancelGeneration()
        {
            if (isProcessingQueue)
            {
                Debug.Log("[EdgeManager] Cancelling edge generation...");
                cancelRequested = true;
            }
        }

        [ContextMenu("Clear All Edges")]
        public void ClearAndCancel()
        {
            if (isProcessingQueue)
            {
                cancelRequested = true;
            }

            edgeQueue.Clear();
            ClearChildEdges();
        }

        /// <summary>
        /// Clears all child edge GameObjects
        /// </summary>
        private void ClearChildEdges()
        {
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

        /// <summary>
        /// Public accessor for all edge data (for debugging/visualization)
        /// </summary>
        public List<EdgeData> AllEdges => allEdges;
    }
}
