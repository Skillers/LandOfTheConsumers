using UnityEngine;
using System.Collections.Generic;

namespace LandOfTheConsumers.Terrain
{
    public enum BiomeType
    {
        Basic,
        MountainPlateaus
    }

    public class AdvancedTerrainGenerator : MonoBehaviour
    {
        [Header("Biome Settings")]
        [Tooltip("BIOME TYPE - Select terrain biome preset\n\n" +
                 "• Basic: Standard rolling hills and mountains\n" +
                 "• Mountain Plateaus: Flat-topped mountains with sharp cliffs\n\n" +
                 "After changing biome type, click 'Apply Biome Preset' to load settings")]
        [SerializeField] public BiomeType biomeType = BiomeType.Basic;

        [Header("World Settings")]
        [Tooltip("Number of chunks to generate (X, Y, Z). Each chunk is 16x16x16 units. Example: (4,2,4) = 64x32x64 world")]
        [SerializeField] public Vector3Int worldSize = new Vector3Int(4, 2, 4);

        [Tooltip("Material to apply to all terrain chunks. Use a solid color material for testing.")]
        [SerializeField] public Material terrainMaterial;

        [Tooltip("SEED - Random seed for terrain generation\n\n" +
                 "EFFECT: Same seed + same settings = identical terrain every time\n" +
                 "• Change seed = completely different terrain\n" +
                 "• Use 0 for random seed each time\n" +
                 "• Share seed with others to generate the same world\n\n" +
                 "VISUAL: Acts like a 'world ID' for reproducible terrain")]
        [SerializeField] public int seed = 12345;

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

        [Tooltip("HEIGHT MULTIPLIER - Controls vertical scale of terrain\n\n" +
                 "EFFECT: Controls vertical height of all terrain features\n" +
                 "• 50-150 = Gentle rolling hills\n" +
                 "• 200-400 = Normal mountains and valleys\n" +
                 "• 450-750 = Tall dramatic mountains\n" +
                 "• 800-1000 = Extreme towering peaks\n\n" +
                 "Formula: terrainHeight = groundHeight + (noise × heightMultiplier)\n" +
                 "Note: This is the ONLY setting that controls vertical height (amplitude removed for simplicity)")]
        [SerializeField] [Range(1f, 1000f)] public float heightMultiplier = 250f;

        [Header("Advanced Terrain Features")]
        [Tooltip("RIDGED NOISE BLEND - Creates sharp cliffs and mountain ridges (like Cube World)\n\n" +
                 "EFFECT: Blends smooth terrain with sharp ridged features\n" +
                 "• 0.0 = Pure smooth terrain (rolling hills)\n" +
                 "• 0.3-0.5 = Balanced (some cliffs, mostly smooth)\n" +
                 "• 0.6-0.8 = Dramatic cliffs and ridges\n" +
                 "• 1.0 = Maximum sharp features everywhere\n\n" +
                 "VISUAL: Creates Minecraft/Cube World-style sharp mountain edges")]
        [SerializeField] [Range(0f, 1f)] public float ridgedNoiseBlend = 0.0f;

        [Tooltip("DOMAIN WARP STRENGTH - Distorts terrain for more organic shapes\n\n" +
                 "EFFECT: Offsets terrain features to break up grid patterns\n" +
                 "• 0 = No warping (can look grid-aligned)\n" +
                 "• 5-15 = Subtle organic distortion (recommended)\n" +
                 "• 20-40 = Strong swirls and curves\n" +
                 "• 50+ = Extreme distortion\n\n" +
                 "VISUAL: Makes terrain less repetitive and more natural")]
        [SerializeField] [Range(0f, 50f)] public float domainWarpStrength = 10f;

        [Tooltip("PLATEAU FLATTENING - Creates flat-topped mountains\n\n" +
                 "EFFECT: Mountains above threshold height get flattened tops\n" +
                 "• Enabled = Flat mesa/plateau tops (Mountain Plateaus biome)\n" +
                 "• Disabled = Natural mountain peaks\n\n" +
                 "When enabled, mountain tops only vary by 'Plateau Max Variation' units")]
        [SerializeField] public bool enablePlateauFlattening = false;

        [Tooltip("PLATEAU HEIGHT THRESHOLD - Minimum height for plateau flattening\n\n" +
                 "EFFECT: Only terrain above this height gets flattened\n" +
                 "• Lower value = more plateaus (more mountains get flat tops)\n" +
                 "• Higher value = fewer plateaus (only tallest mountains flatten)\n\n" +
                 "Recommended: Set to groundHeight + 60% of heightMultiplier")]
        [SerializeField] [Range(0f, 500f)] public float plateauHeightThreshold = 200f;

        [Tooltip("PLATEAU MAX VARIATION - Height variation allowed on plateau tops\n\n" +
                 "EFFECT: Maximum height difference across a plateau top\n" +
                 "• 0 = Perfectly flat (like a table)\n" +
                 "• 2-4 = Slight variation (recommended for natural look)\n" +
                 "• 5-10 = More bumpy plateau tops\n\n" +
                 "You requested max 4 layers for natural-looking flat tops")]
        [SerializeField] [Range(0f, 20f)] public float plateauMaxVariation = 4f;

        [Header("Generation")]
        [Tooltip("Automatically generate terrain when the scene starts")]
        [SerializeField] public bool generateOnStart = true;

        [Tooltip("Display generation progress messages in the Console window")]
        [SerializeField] public bool showProgressInConsole = true;

        [Tooltip("GENERATE ONE AT A TIME - Show visual progress as each chunk generates\n\n" +
                 "EFFECT: See each chunk appear one by one instead of all at once\n" +
                 "• Enabled = Slower but you can watch progress\n" +
                 "• Disabled = Fast generation, all chunks at once\n\n" +
                 "Useful for large worlds to see generation progress")]
        [SerializeField] public bool generateOneAtATime = false;

        [Tooltip("Delay between chunks when generating one at a time (in seconds). Lower = faster.")]
        [SerializeField] [Range(0.01f, 1f)] public float chunkGenerationDelay = 0.1f;

        private Dictionary<Vector3Int, AdvancedTerrainChunk> chunks = new Dictionary<Vector3Int, AdvancedTerrainChunk>();
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

            chunksContainer = new GameObject("Advanced Terrain Chunks");
            chunksContainer.transform.SetParent(transform);

            if (generateOneAtATime)
            {
                StartCoroutine(GenerateWorldSequential());
            }
            else
            {
                GenerateWorldImmediate();
            }
        }

        private void GenerateWorldImmediate()
        {
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
                            Debug.Log($"[Advanced] Generating terrain: {currentChunk}/{totalChunks} chunks");
                        }
                    }
                }
            }

            if (showProgressInConsole)
            {
                Debug.Log($"[Advanced] Terrain generation complete! Generated {totalChunks} chunks.");
            }
        }

        private System.Collections.IEnumerator GenerateWorldSequential()
        {
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
                        if (showProgressInConsole)
                        {
                            Debug.Log($"[Advanced] Generating terrain: {currentChunk}/{totalChunks} chunks");
                        }

                        // Wait before generating next chunk
                        yield return new UnityEngine.WaitForSeconds(chunkGenerationDelay);
                    }
                }
            }

            if (showProgressInConsole)
            {
                Debug.Log($"[Advanced] Terrain generation complete! Generated {totalChunks} chunks.");
            }
        }

        private void GenerateChunk(Vector3Int position)
        {
            GameObject chunkObj = new GameObject($"AdvancedChunk_{position.x}_{position.y}_{position.z}");
            chunkObj.transform.SetParent(chunksContainer.transform);

            // Position chunk based on its grid coordinates
            // Default chunk size is 16x16x16 with voxel size of 1
            Vector3 worldPosition = new Vector3(position.x * 16f, position.y * 16f, position.z * 16f);
            chunkObj.transform.localPosition = worldPosition;

            AdvancedTerrainChunk chunk = chunkObj.AddComponent<AdvancedTerrainChunk>();

            chunk.SetNoiseParameters(octaves, frequency, 1.0f, lacunarity, persistence, seed);
            chunk.SetTerrainShape(groundHeight, heightMultiplier);
            chunk.SetAdvancedFeatures(ridgedNoiseBlend, domainWarpStrength,
                                     enablePlateauFlattening, plateauHeightThreshold, plateauMaxVariation);

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

        [ContextMenu("Apply Biome Preset")]
        public void ApplyBiomePreset()
        {
            switch (biomeType)
            {
                case BiomeType.Basic:
                    ApplyBasicBiome();
                    break;
                case BiomeType.MountainPlateaus:
                    ApplyMountainPlateausBiome();
                    break;
            }

            Debug.Log($"[AdvancedTerrainGenerator] Applied {biomeType} biome preset");
        }

        private void ApplyBasicBiome()
        {
            // Standard rolling hills and mountains
            octaves = 4;
            frequency = 0.05f;
            lacunarity = 2.0f;
            persistence = 0.5f;
            groundHeight = 8f;
            heightMultiplier = 250f;
            ridgedNoiseBlend = 0.0f; // Smooth terrain
            domainWarpStrength = 10f;

            // No plateau flattening for basic terrain
            enablePlateauFlattening = false;
            plateauHeightThreshold = 200f;
            plateauMaxVariation = 4f;
        }

        private void ApplyMountainPlateausBiome()
        {
            // Flat-topped mountains with sharp cliffs (Mesa/Plateau style)
            octaves = 5;
            frequency = 0.04f;
            lacunarity = 2.2f;
            persistence = 0.4f; // Less detail on top = flatter plateaus
            groundHeight = 10f;
            heightMultiplier = 400f; // Tall plateaus
            ridgedNoiseBlend = 0.7f; // Sharp cliff edges
            domainWarpStrength = 15f; // More organic shapes

            // Enable plateau flattening for mountain plateaus
            enablePlateauFlattening = true;
            plateauHeightThreshold = groundHeight + (heightMultiplier * 0.5f); // 50% height threshold = 210
            plateauMaxVariation = 4f; // Max 4 units variation on plateau tops
        }

        public AdvancedTerrainChunk GetChunk(Vector3Int position)
        {
            chunks.TryGetValue(position, out AdvancedTerrainChunk chunk);
            return chunk;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;
            Vector3 worldSizeInUnits = new Vector3(worldSize.x * 16, worldSize.y * 16, worldSize.z * 16);
            Gizmos.DrawWireCube(transform.position + worldSizeInUnits * 0.5f, worldSizeInUnits);
        }
    }
}
