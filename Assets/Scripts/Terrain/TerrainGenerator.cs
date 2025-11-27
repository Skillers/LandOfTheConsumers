using UnityEngine;
using System.Collections.Generic;

namespace LandOfTheConsumers.Terrain
{
    public class TerrainGenerator : MonoBehaviour
    {
        [Header("World Settings")]
        [Tooltip("Number of chunks to generate (X, Y, Z). Each chunk is 16x16x16 units. Example: (4,2,4) = 64x32x64 world")]
        [SerializeField] public Vector3Int worldSize = new Vector3Int(4, 2, 4);

        [Tooltip("Material to apply to all terrain chunks. Use a solid color material for testing.")]
        [SerializeField] public Material terrainMaterial;

        [Header("Noise Settings - Multi-Level Detail")]
        [Tooltip("OCTAVES - Adds detail layers to terrain\n\n" +
                 "EFFECT: Controls how detailed and natural the terrain looks\n" +
                 "• 1-2 = Smooth, simple terrain (like sand dunes)\n" +
                 "• 3-4 = Natural terrain with variety (recommended)\n" +
                 "• 5-6 = Very detailed, realistic terrain\n" +
                 "• 7-8 = Maximum detail, includes tiny bumps and cracks\n\n" +
                 "VISUAL: More octaves = more layers of detail\n" +
                 "Think: Big mountains + medium hills + small rocks = realistic")]
        [SerializeField] [Range(1, 8)] public int octaves = 4;

        [Tooltip("FREQUENCY - Controls the size of terrain features\n\n" +
                 "EFFECT: Determines if you get huge mountains or small bumps\n" +
                 "• 0.01-0.03 = Massive mountains, vast valleys (continental scale)\n" +
                 "• 0.04-0.06 = Rolling hills, medium features (landscape scale)\n" +
                 "• 0.07-0.1 = Small hills and bumps\n" +
                 "• 0.1-0.2 = Tiny bumps, almost flat\n\n" +
                 "VISUAL: Lower = bigger/wider features, Higher = smaller/tighter features\n" +
                 "Try: 0.03 for mountains, 0.08 for gentle hills")]
        [SerializeField] [Range(0.01f, 0.2f)] public float frequency = 0.05f;

        [Tooltip("AMPLITUDE - Controls the strength of the noise effect\n\n" +
                 "EFFECT: How much the noise affects terrain shape\n" +
                 "• 0.1-0.5 = Subtle variation, mostly flat\n" +
                 "• 0.6-1.2 = Moderate variation, normal terrain\n" +
                 "• 1.3-2.0 = Strong variation, dramatic landscape\n" +
                 "• 2.0-3.0 = Extreme variation, wild terrain\n\n" +
                 "VISUAL: Higher amplitude = more dramatic height differences\n" +
                 "Works together with Height Multiplier")]
        [SerializeField] [Range(0.1f, 3f)] public float amplitude = 1f;

        [Tooltip("LACUNARITY - Detail size multiplier between layers\n\n" +
                 "EFFECT: How much each detail layer differs in size from the previous\n" +
                 "• 1.5-1.8 = Details are similar sizes (smoother, softer terrain)\n" +
                 "• 2.0 = Standard (each layer 2x more detailed) - RECOMMENDED\n" +
                 "• 2.2-3.0 = Big contrast between layers (sharper, more varied)\n\n" +
                 "VISUAL: Higher = more contrast between big features and small details\n" +
                 "Try: 1.8 for gentle terrain, 2.5 for rough/rocky terrain\n" +
                 "Formula: Each octave's frequency = previous × lacunarity")]
        [SerializeField] [Range(1.5f, 3f)] public float lacunarity = 2f;

        [Tooltip("PERSISTENCE - Detail strength multiplier between layers\n\n" +
                 "EFFECT: How visible smaller details are compared to large features\n" +
                 "• 0.1-0.3 = Small details barely visible (smooth, simple)\n" +
                 "• 0.4-0.6 = Balanced detail visibility - RECOMMENDED\n" +
                 "• 0.7-0.9 = Small details very visible (rough, complex)\n\n" +
                 "VISUAL: Lower = large mountains dominate, Higher = all details matter\n" +
                 "Try: 0.3 for clean simple terrain, 0.7 for rough detailed terrain\n" +
                 "Formula: Each octave's amplitude = previous × persistence")]
        [SerializeField] [Range(0.1f, 0.9f)] public float persistence = 0.5f;

        [Header("Terrain Shape")]
        [Tooltip("Base terrain elevation in units. This is the 'sea level' of your terrain. Try: 8-12")]
        [SerializeField] public float groundHeight = 8f;

        [Tooltip("Multiplier for terrain height variation. Higher = taller mountains and deeper valleys. Try: 3-10")]
        [SerializeField] [Range(1f, 20f)] public float heightMultiplier = 5f;

        [Header("Generation")]
        [Tooltip("Automatically generate terrain when the scene starts")]
        [SerializeField] public bool generateOnStart = true;

        [Tooltip("Display generation progress messages in the Console window")]
        [SerializeField] public bool showProgressInConsole = true;

        private Dictionary<Vector3Int, TerrainChunk> chunks = new Dictionary<Vector3Int, TerrainChunk>();
        private GameObject chunksContainer;

        private void Start()
        {
            if (generateOnStart)
            {
                GenerateWorld();
            }
        }

        [ContextMenu("Generate World")]
        public void GenerateWorld()
        {
            ClearWorld();

            chunksContainer = new GameObject("Terrain Chunks");
            chunksContainer.transform.SetParent(transform);

            int totalChunks = worldSize.x * worldSize.y * worldSize.z;
            int currentChunk = 0;

            for (int x = 0; x < worldSize.x; x++)
            {
                for (int y = 0; y < worldSize.y; y++)
                {
                    for (int z = 0; z < worldSize.z; z++)
                    {
                        Vector3Int chunkPos = new Vector3Int(x, y, z);
                        GenerateChunk(chunkPos);

                        currentChunk++;
                        if (showProgressInConsole && currentChunk % 5 == 0)
                        {
                            Debug.Log($"Generating terrain: {currentChunk}/{totalChunks} chunks");
                        }
                    }
                }
            }

            if (showProgressInConsole)
            {
                Debug.Log($"Terrain generation complete! Generated {totalChunks} chunks.");
            }
        }

        private void GenerateChunk(Vector3Int position)
        {
            GameObject chunkObj = new GameObject($"Chunk_{position.x}_{position.y}_{position.z}");
            chunkObj.transform.SetParent(chunksContainer.transform);

            // Position chunk based on its grid coordinates
            // Default chunk size is 16x16x16 with voxel size of 1
            Vector3 worldPosition = new Vector3(position.x * 16f, position.y * 16f, position.z * 16f);
            chunkObj.transform.localPosition = worldPosition;

            TerrainChunk chunk = chunkObj.AddComponent<TerrainChunk>();

            chunk.SetNoiseParameters(octaves, frequency, amplitude, lacunarity, persistence);
            chunk.SetTerrainShape(groundHeight, heightMultiplier);

            chunk.Initialize(position, terrainMaterial);

            chunks[position] = chunk;
        }

        [ContextMenu("Clear World")]
        public void ClearWorld()
        {
            chunks.Clear();

            if (chunksContainer != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(chunksContainer);
                }
                else
                {
                    DestroyImmediate(chunksContainer);
                }
            }
        }

        [ContextMenu("Regenerate World")]
        public void RegenerateWorld()
        {
            GenerateWorld();
        }

        public TerrainChunk GetChunk(Vector3Int position)
        {
            chunks.TryGetValue(position, out TerrainChunk chunk);
            return chunk;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 worldSizeInUnits = new Vector3(worldSize.x * 16, worldSize.y * 16, worldSize.z * 16);
            Gizmos.DrawWireCube(transform.position + worldSizeInUnits * 0.5f, worldSizeInUnits);
        }
    }
}
