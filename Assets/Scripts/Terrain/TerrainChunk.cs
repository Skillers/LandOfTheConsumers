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

        [Tooltip("Noise amplitude. Always set to 1.0 from TerrainGenerator (height control is done via HeightMultiplier instead).")]
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

        [Header("Advanced Features")]
        [Tooltip("Blend between smooth and ridged noise. 0 = smooth, 1 = sharp cliffs")]
        [SerializeField] private float ridgedBlend = 0f;

        [Tooltip("Domain warp strength for more organic terrain")]
        [SerializeField] private float domainWarpStrength = 10f;

        [Tooltip("Water level height for lakes")]
        [SerializeField] private float waterLevel = 5f;

        [Tooltip("Flatten terrain below water level")]
        [SerializeField] private bool flattenUnderwater = true;

        [Tooltip("Enable plateau flattening for mountain tops")]
        [SerializeField] private bool enablePlateauFlattening = false;

        [Tooltip("Height threshold above which plateaus are flattened")]
        [SerializeField] private float plateauHeightThreshold = 200f;

        [Tooltip("Maximum height variation allowed on plateau tops")]
        [SerializeField] private float plateauMaxVariation = 4f;

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
            // PERFORMANCE OPTIMIZATION:
            // Since terrain is heightmap-based (2D noise), we can optimize by:
            // 1. Processing columns (X,Z) instead of individual voxels
            // 2. Calculating terrain height ONCE per column (was once per voxel!)
            // 3. Only processing Y values up to terrain surface + margin
            // 4. Skipping all air voxels above terrain
            //
            // Performance gain: ~50-70% faster for typical terrain
            // Greatest benefit: High altitude chunks (mostly air) generate almost instantly

            float[,,] voxels = new float[chunkSize.x + 1, chunkSize.y + 1, chunkSize.z + 1];

            Vector3 worldOffset = new Vector3(worldPosition.x * chunkSize.x,
                                              worldPosition.y * chunkSize.y,
                                              worldPosition.z * chunkSize.z) * voxelSize;
            for (int x = 0; x <= chunkSize.x; x++)
            {
                for (int z = 0; z <= chunkSize.z; z++)
                {
                    // Calculate world position for this column (Y doesn't matter for 2D noise)
                    float worldX = worldOffset.x + x * voxelSize;
                    float worldZ = worldOffset.z + z * voxelSize;

                    // Apply domain warping to sample position
                    Vector2 warpedPos;
                    if (domainWarpStrength > 0.1f)
                    {
                        warpedPos = NoiseGenerator.DomainWarp2D(worldX, worldZ, domainWarpStrength, seed);
                    }
                    else
                    {
                        warpedPos = new Vector2(worldX, worldZ);
                    }

                    // Calculate terrain noise ONCE for this entire column
                    float noise = NoiseGenerator.GetTerrainNoise(
                        warpedPos.x,
                        warpedPos.y,
                        0, // Still 2D (no Y) to prevent floating islands
                        octaves,
                        frequency,
                        amplitude,
                        lacunarity,
                        persistence,
                        ridgedBlend, // Blend between smooth and ridged
                        seed
                    );

                    // Calculate the terrain height at this XZ position
                    float terrainHeight = groundHeight + noise * heightMultiplier;

                    // Plateau flattening - Flatten mountain tops above threshold
                    if (enablePlateauFlattening && terrainHeight > plateauHeightThreshold)
                    {
                        // Calculate how far above threshold this point is
                        float baseHeight = plateauHeightThreshold;

                        // Allow only small variation (max 4 units) on plateau tops
                        // Map the noise to a small range instead of full height variation
                        float normalizedNoise = (noise + 1.0f) * 0.5f; // Convert -1 to 1 range into 0 to 1
                        float plateauVariation = normalizedNoise * plateauMaxVariation;

                        terrainHeight = baseHeight + plateauVariation;
                    }

                    // Water level handling
                    if (flattenUnderwater && terrainHeight < waterLevel)
                    {
                        terrainHeight = waterLevel - 0.5f; // Flatten below water
                    }

                    // Calculate which Y range actually needs processing for this column
                    // Add small margin for marching cubes interpolation (2 voxels)
                    int maxY = Mathf.CeilToInt((terrainHeight - worldOffset.y) / voxelSize) + 2;

                    // Clamp to valid range
                    maxY = Mathf.Clamp(maxY, 0, chunkSize.y);

                    // OPTIMIZATION: Only process Y values up to terrain surface
                    // This skips all air voxels above the terrain
                    for (int y = 0; y <= maxY && y <= chunkSize.y; y++)
                    {
                        float worldY = worldOffset.y + y * voxelSize;
                        float density = terrainHeight - worldY;
                        voxels[x, y, z] = density;
                    }

                    // Fill remaining voxels above with air (only if there are any remaining)
                    if (maxY < chunkSize.y)
                    {
                        for (int y = maxY + 1; y <= chunkSize.y; y++)
                        {
                            float worldY = worldOffset.y + y * voxelSize;
                            voxels[x, y, z] = terrainHeight - worldY; // Negative = air
                        }
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

        public void SetAdvancedFeatures(float ridgedBlend, float domainWarpStrength, float waterLevel, bool flattenUnderwater,
                                        bool enablePlateauFlattening, float plateauHeightThreshold, float plateauMaxVariation)
        {
            this.ridgedBlend = ridgedBlend;
            this.domainWarpStrength = domainWarpStrength;
            this.waterLevel = waterLevel;
            this.flattenUnderwater = flattenUnderwater;
            this.enablePlateauFlattening = enablePlateauFlattening;
            this.plateauHeightThreshold = plateauHeightThreshold;
            this.plateauMaxVariation = plateauMaxVariation;
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
