using UnityEngine;
using LandOfTheConsumers.Procedural;

namespace LandOfTheConsumers.Terrain
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class TerrainChunk : MonoBehaviour
    {
        [Header("Chunk Settings")]
        [Tooltip("Voxel resolution per chunk (X, Y, Z). Default: 16x16x16. Higher = smoother but slower. Usually set by TerrainGenerator.")]
        [SerializeField] private Vector3Int chunkSize = new Vector3Int(16, 16, 16);

        [Tooltip("Size of each voxel in world units. Default: 1.0. Smaller = more detail but more vertices.")]
        [SerializeField] private float voxelSize = 1f;

        [Header("Noise Settings")]
        [Tooltip("Number of noise layers (octaves). More = more detail. Usually inherited from TerrainGenerator.")]
        [SerializeField] private int octaves = 4;

        [Tooltip("Base noise frequency. Lower = larger features. Usually inherited from TerrainGenerator.")]
        [SerializeField] private float frequency = 0.05f;

        [Tooltip("Noise amplitude. Higher = more dramatic terrain. Usually inherited from TerrainGenerator.")]
        [SerializeField] private float amplitude = 1f;

        [Tooltip("Frequency multiplier between octaves. Usually inherited from TerrainGenerator.")]
        [SerializeField] private float lacunarity = 2f;

        [Tooltip("Amplitude multiplier between octaves. Usually inherited from TerrainGenerator.")]
        [SerializeField] private float persistence = 0.5f;

        [Tooltip("Marching cubes surface threshold. Values above this are solid, below are air. Default: 0.5")]
        [SerializeField] private float surfaceLevel = 0.5f;

        [Header("Terrain Shape")]
        [Tooltip("Base terrain height. Usually inherited from TerrainGenerator.")]
        [SerializeField] private float groundHeight = 8f;

        [Tooltip("Height variation multiplier. Usually inherited from TerrainGenerator.")]
        [SerializeField] private float heightMultiplier = 5f;

        [Tooltip("Seed for random generation. Usually inherited from TerrainGenerator.")]
        [SerializeField] private int seed = 12345;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private MeshCollider meshCollider;
        private Vector3Int worldPosition;

        private void Awake()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            meshCollider = GetComponent<MeshCollider>();

            if (meshCollider == null)
            {
                meshCollider = gameObject.AddComponent<MeshCollider>();
            }
        }

        public void Initialize(Vector3Int position, Material material)
        {
            worldPosition = position;

            if (material != null)
            {
                meshRenderer.material = material;
            }

            GenerateTerrain();
        }

        public void GenerateTerrain()
        {
            float[,,] voxelData = GenerateVoxelData();

            MarchingCubes.MeshData meshData = MarchingCubes.GenerateMesh(voxelData, surfaceLevel);

            if (meshData.vertices.Count == 0)
            {
                return;
            }

            Mesh mesh = CreateMesh(meshData);
            meshFilter.mesh = mesh;

            if (meshCollider != null)
            {
                meshCollider.sharedMesh = mesh;
            }
        }

        private float[,,] GenerateVoxelData()
        {
            float[,,] voxels = new float[chunkSize.x + 1, chunkSize.y + 1, chunkSize.z + 1];

            Vector3 worldOffset = new Vector3(worldPosition.x * chunkSize.x,
                                              worldPosition.y * chunkSize.y,
                                              worldPosition.z * chunkSize.z) * voxelSize;

            for (int x = 0; x <= chunkSize.x; x++)
            {
                for (int y = 0; y <= chunkSize.y; y++)
                {
                    for (int z = 0; z <= chunkSize.z; z++)
                    {
                        Vector3 worldPos = worldOffset + new Vector3(x, y, z) * voxelSize;

                        // Use 2D noise (only X and Z) to prevent floating blobs
                        // This creates heightmap-style terrain that builds from the ground up
                        float noise = NoiseGenerator.GetFractalNoise(
                            worldPos.x,
                            worldPos.z,
                            0, // No Y component = no floating islands
                            octaves,
                            frequency,
                            amplitude,
                            lacunarity,
                            persistence,
                            seed
                        );

                        // Calculate the terrain height at this XZ position
                        float terrainHeight = groundHeight + noise * heightMultiplier;

                        // Density: positive below terrain surface, negative above
                        // This ensures terrain only generates from ground up
                        float density = terrainHeight - worldPos.y;

                        voxels[x, y, z] = density;
                    }
                }
            }

            return voxels;
        }

        private Mesh CreateMesh(MarchingCubes.MeshData meshData)
        {
            Mesh mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(meshData.vertices);
            mesh.SetTriangles(meshData.triangles, 0);

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        public void SetNoiseParameters(int octaves, float frequency, float amplitude, float lacunarity, float persistence, int seed)
        {
            this.octaves = octaves;
            this.frequency = frequency;
            this.amplitude = amplitude;
            this.lacunarity = lacunarity;
            this.persistence = persistence;
            this.seed = seed;
        }

        public void SetTerrainShape(float groundHeight, float heightMultiplier)
        {
            this.groundHeight = groundHeight;
            this.heightMultiplier = heightMultiplier;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Vector3 center = transform.position + new Vector3(chunkSize.x, chunkSize.y, chunkSize.z) * voxelSize * 0.5f;
            Vector3 size = new Vector3(chunkSize.x, chunkSize.y, chunkSize.z) * voxelSize;
            Gizmos.DrawWireCube(center, size);
        }
    }
}
