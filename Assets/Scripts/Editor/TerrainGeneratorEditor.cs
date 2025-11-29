using UnityEngine;
using UnityEditor;
using System;
using System.IO;

namespace LandOfTheConsumers.Terrain
{
    [System.Serializable]
    public class TerrainSettings
    {
        public string exportDate;
        public BiomeType biomeType;
        public int seed;
        public int octaves;
        public float frequency;
        public float lacunarity;
        public float persistence;
        public float groundHeight;
        public float heightMultiplier;
        public float ridgedNoiseBlend;
        public float domainWarpStrength;
        public float waterLevel;
        public bool flattenUnderwater;
        public bool enablePlateauFlattening;
        public float plateauHeightThreshold;
        public float plateauMaxVariation;
        public int worldSizeX;
        public int worldSizeY;
        public int worldSizeZ;
        public bool generateOneAtATime;
        public float chunkGenerationDelay;
    }

    [CustomEditor(typeof(TerrainGenerator))]
    public class TerrainGeneratorEditor : Editor
    {
        private const string PREFS_PREFIX = "TerrainGen_";
        private static bool isSubscribed = false;

        private void OnEnable()
        {
            if (!isSubscribed)
            {
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                isSubscribed = true;
            }
        }

        private void OnDisable()
        {
            if (isSubscribed)
            {
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                isSubscribed = false;
            }
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Find the TerrainGenerator in the scene
            TerrainGenerator generator = FindObjectOfType<TerrainGenerator>();
            if (generator == null) return;

            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    // Save settings before entering play mode
                    SaveSettings(generator);
                    Debug.Log("[TerrainGenerator] Auto-save: Settings saved before entering Play mode");
                    break;

                case PlayModeStateChange.ExitingPlayMode:
                    // Save any changes made during play mode
                    SaveSettings(generator);
                    Debug.Log("[TerrainGenerator] Auto-save: Settings saved from Play mode");
                    break;

                case PlayModeStateChange.EnteredEditMode:
                    // Restore settings after exiting play mode
                    LoadSettings(generator);
                    Debug.Log("[TerrainGenerator] Auto-restore: Settings restored after exiting Play mode");
                    break;
            }
        }

        public override void OnInspectorGUI()
        {
            // Show info if in play mode
            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "✅ AUTO-SAVE ACTIVE: Changes will automatically persist when you exit Play mode!",
                    MessageType.Info
                );
                EditorGUILayout.Space(5);
            }

            DrawDefaultInspector();

            TerrainGenerator generator = (TerrainGenerator)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Seed Controls", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField($"Current Seed: {generator.seed}", EditorStyles.miniLabel);

            if (GUILayout.Button("🎲 Random Seed", GUILayout.Width(120), GUILayout.Height(20)))
            {
                Undo.RecordObject(generator, "Randomize Seed");
                generator.seed = UnityEngine.Random.Range(1, 999999);
                EditorUtility.SetDirty(generator);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Export/Import Settings", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.5f, 1f, 0.5f);
            if (GUILayout.Button("📄 Export JSON", GUILayout.Height(25)))
            {
                ExportToJSON(generator);
            }

            GUI.backgroundColor = new Color(0.5f, 0.8f, 1f);
            if (GUILayout.Button("📂 Import JSON", GUILayout.Height(25)))
            {
                ImportFromJSON(generator);
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Biome Controls", EditorStyles.boldLabel);

            GUI.backgroundColor = new Color(0.7f, 0.5f, 1f);
            if (GUILayout.Button("🏔️ Apply Biome Preset", GUILayout.Height(30)))
            {
                Undo.RecordObject(generator, "Apply Biome Preset");
                generator.ApplyBiomePreset();
                EditorUtility.SetDirty(generator);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                $"Current Biome: {generator.biomeType}\n\n" +
                "Click 'Apply Biome Preset' to load preset settings for the selected biome type.\n" +
                "• Basic: Standard rolling hills and mountains\n" +
                "• Mountain Plateaus: Flat-topped mountains with sharp cliffs",
                MessageType.Info
            );

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Terrain Controls", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Generate Terrain", GUILayout.Height(30)))
            {
                generator.GenerateWorld();
            }

            GUI.backgroundColor = Color.yellow;
            if (GUILayout.Button("Regenerate", GUILayout.Height(30)))
            {
                generator.RegenerateWorld();
            }

            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Clear Terrain", GUILayout.Height(30)))
            {
                generator.ClearWorld();
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                "Generate: Create new terrain (keeps existing)\n" +
                "Regenerate: Clear and generate fresh terrain\n" +
                "Clear: Remove all terrain chunks",
                MessageType.Info
            );

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Quick Settings Guide", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "FLATTER TERRAIN: Decrease Height Multiplier (2-3)\n" +
                "HILLIER TERRAIN: Increase Height Multiplier (8-12)\n" +
                "MORE DETAIL: Increase Octaves (5-6)\n" +
                "LARGER FEATURES: Decrease Frequency (0.02-0.03)\n" +
                "SMALLER FEATURES: Increase Frequency (0.08-0.1)",
                MessageType.None
            );
        }

        private void SaveSettings(TerrainGenerator generator)
        {
            // Biome settings
            EditorPrefs.SetInt(PREFS_PREFIX + "BiomeType", (int)generator.biomeType);

            // Noise settings
            EditorPrefs.SetInt(PREFS_PREFIX + "Octaves", generator.octaves);
            EditorPrefs.SetFloat(PREFS_PREFIX + "Frequency", generator.frequency);
            EditorPrefs.SetFloat(PREFS_PREFIX + "Lacunarity", generator.lacunarity);
            EditorPrefs.SetFloat(PREFS_PREFIX + "Persistence", generator.persistence);

            // Terrain shape
            EditorPrefs.SetFloat(PREFS_PREFIX + "GroundHeight", generator.groundHeight);
            EditorPrefs.SetFloat(PREFS_PREFIX + "HeightMultiplier", generator.heightMultiplier);

            // Advanced features
            EditorPrefs.SetFloat(PREFS_PREFIX + "RidgedNoiseBlend", generator.ridgedNoiseBlend);
            EditorPrefs.SetFloat(PREFS_PREFIX + "DomainWarpStrength", generator.domainWarpStrength);
            EditorPrefs.SetFloat(PREFS_PREFIX + "WaterLevel", generator.waterLevel);
            EditorPrefs.SetInt(PREFS_PREFIX + "FlattenUnderwater", generator.flattenUnderwater ? 1 : 0);
            EditorPrefs.SetInt(PREFS_PREFIX + "EnablePlateauFlattening", generator.enablePlateauFlattening ? 1 : 0);
            EditorPrefs.SetFloat(PREFS_PREFIX + "PlateauHeightThreshold", generator.plateauHeightThreshold);
            EditorPrefs.SetFloat(PREFS_PREFIX + "PlateauMaxVariation", generator.plateauMaxVariation);

            // World settings
            EditorPrefs.SetInt(PREFS_PREFIX + "WorldSizeX", generator.worldSize.x);
            EditorPrefs.SetInt(PREFS_PREFIX + "WorldSizeY", generator.worldSize.y);
            EditorPrefs.SetInt(PREFS_PREFIX + "WorldSizeZ", generator.worldSize.z);
            EditorPrefs.SetInt(PREFS_PREFIX + "Seed", generator.seed);

            // Generation settings
            EditorPrefs.SetInt(PREFS_PREFIX + "GenerateOneAtATime", generator.generateOneAtATime ? 1 : 0);
            EditorPrefs.SetFloat(PREFS_PREFIX + "ChunkDelay", generator.chunkGenerationDelay);

            Debug.Log("[TerrainGenerator] Settings saved: " +
                $"Biome={generator.biomeType}, Seed={generator.seed}, Octaves={generator.octaves}, " +
                $"Frequency={generator.frequency:F3}, HeightMult={generator.heightMultiplier:F1}");
        }

        private void LoadSettings(TerrainGenerator generator, bool showDialog = false)
        {
            if (EditorPrefs.HasKey(PREFS_PREFIX + "Octaves"))
            {
                Undo.RecordObject(generator, "Load Terrain Settings");

                // Biome settings
                generator.biomeType = (BiomeType)EditorPrefs.GetInt(PREFS_PREFIX + "BiomeType", 0);

                // Noise settings
                generator.octaves = EditorPrefs.GetInt(PREFS_PREFIX + "Octaves", 4);
                generator.frequency = EditorPrefs.GetFloat(PREFS_PREFIX + "Frequency", 0.05f);
                generator.lacunarity = EditorPrefs.GetFloat(PREFS_PREFIX + "Lacunarity", 2f);
                generator.persistence = EditorPrefs.GetFloat(PREFS_PREFIX + "Persistence", 0.5f);

                // Terrain shape
                generator.groundHeight = EditorPrefs.GetFloat(PREFS_PREFIX + "GroundHeight", 8f);
                generator.heightMultiplier = EditorPrefs.GetFloat(PREFS_PREFIX + "HeightMultiplier", 250f);

                // Advanced features
                generator.ridgedNoiseBlend = EditorPrefs.GetFloat(PREFS_PREFIX + "RidgedNoiseBlend", 0f);
                generator.domainWarpStrength = EditorPrefs.GetFloat(PREFS_PREFIX + "DomainWarpStrength", 10f);
                generator.waterLevel = EditorPrefs.GetFloat(PREFS_PREFIX + "WaterLevel", 5f);
                generator.flattenUnderwater = EditorPrefs.GetInt(PREFS_PREFIX + "FlattenUnderwater", 1) == 1;
                generator.enablePlateauFlattening = EditorPrefs.GetInt(PREFS_PREFIX + "EnablePlateauFlattening", 0) == 1;
                generator.plateauHeightThreshold = EditorPrefs.GetFloat(PREFS_PREFIX + "PlateauHeightThreshold", 200f);
                generator.plateauMaxVariation = EditorPrefs.GetFloat(PREFS_PREFIX + "PlateauMaxVariation", 4f);

                // World settings
                int worldX = EditorPrefs.GetInt(PREFS_PREFIX + "WorldSizeX", 4);
                int worldY = EditorPrefs.GetInt(PREFS_PREFIX + "WorldSizeY", 2);
                int worldZ = EditorPrefs.GetInt(PREFS_PREFIX + "WorldSizeZ", 4);
                generator.worldSize = new Vector3Int(worldX, worldY, worldZ);
                generator.seed = EditorPrefs.GetInt(PREFS_PREFIX + "Seed", 12345);

                // Generation settings
                generator.generateOneAtATime = EditorPrefs.GetInt(PREFS_PREFIX + "GenerateOneAtATime", 0) == 1;
                generator.chunkGenerationDelay = EditorPrefs.GetFloat(PREFS_PREFIX + "ChunkDelay", 0.1f);

                EditorUtility.SetDirty(generator);

                Debug.Log("[TerrainGenerator] Settings loaded: " +
                    $"Biome={generator.biomeType}, Seed={generator.seed}, HeightMult={generator.heightMultiplier:F1}");
            }
            else if (showDialog)
            {
                EditorUtility.DisplayDialog("No Saved Settings",
                    "No saved settings found. Use 'Save Preset' first.",
                    "OK");
            }
        }

        private void ExportToJSON(TerrainGenerator generator)
        {
            TerrainSettings settings = new TerrainSettings
            {
                exportDate = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"),
                biomeType = generator.biomeType,
                seed = generator.seed,
                octaves = generator.octaves,
                frequency = generator.frequency,
                lacunarity = generator.lacunarity,
                persistence = generator.persistence,
                groundHeight = generator.groundHeight,
                heightMultiplier = generator.heightMultiplier,
                ridgedNoiseBlend = generator.ridgedNoiseBlend,
                domainWarpStrength = generator.domainWarpStrength,
                waterLevel = generator.waterLevel,
                flattenUnderwater = generator.flattenUnderwater,
                enablePlateauFlattening = generator.enablePlateauFlattening,
                plateauHeightThreshold = generator.plateauHeightThreshold,
                plateauMaxVariation = generator.plateauMaxVariation,
                worldSizeX = generator.worldSize.x,
                worldSizeY = generator.worldSize.y,
                worldSizeZ = generator.worldSize.z,
                generateOneAtATime = generator.generateOneAtATime,
                chunkGenerationDelay = generator.chunkGenerationDelay
            };

            string json = JsonUtility.ToJson(settings, true);
            string fileName = $"TerrainSettings_{settings.exportDate}.json";
            string path = EditorUtility.SaveFilePanel("Export Terrain Settings", Application.dataPath, fileName, "json");

            if (!string.IsNullOrEmpty(path))
            {
                File.WriteAllText(path, json);
                Debug.Log($"[TerrainGenerator] Settings exported to: {path}");
                EditorUtility.DisplayDialog("Export Successful",
                    $"Terrain settings exported to:\n{Path.GetFileName(path)}\n\nSeed: {settings.seed}",
                    "OK");
            }
        }

        private void ImportFromJSON(TerrainGenerator generator)
        {
            string path = EditorUtility.OpenFilePanel("Import Terrain Settings", Application.dataPath, "json");

            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    TerrainSettings settings = JsonUtility.FromJson<TerrainSettings>(json);

                    Undo.RecordObject(generator, "Import Terrain Settings");

                    generator.biomeType = settings.biomeType;
                    generator.seed = settings.seed;
                    generator.octaves = settings.octaves;
                    generator.frequency = settings.frequency;
                    generator.lacunarity = settings.lacunarity;
                    generator.persistence = settings.persistence;
                    generator.groundHeight = settings.groundHeight;
                    generator.heightMultiplier = settings.heightMultiplier;
                    generator.ridgedNoiseBlend = settings.ridgedNoiseBlend;
                    generator.domainWarpStrength = settings.domainWarpStrength;
                    generator.waterLevel = settings.waterLevel;
                    generator.flattenUnderwater = settings.flattenUnderwater;
                    generator.enablePlateauFlattening = settings.enablePlateauFlattening;
                    generator.plateauHeightThreshold = settings.plateauHeightThreshold;
                    generator.plateauMaxVariation = settings.plateauMaxVariation;
                    generator.worldSize = new Vector3Int(settings.worldSizeX, settings.worldSizeY, settings.worldSizeZ);
                    generator.generateOneAtATime = settings.generateOneAtATime;
                    generator.chunkGenerationDelay = settings.chunkGenerationDelay;

                    EditorUtility.SetDirty(generator);

                    Debug.Log($"[TerrainGenerator] Settings imported from: {path}");
                    EditorUtility.DisplayDialog("Import Successful",
                        $"Terrain settings imported!\n\nExported: {settings.exportDate}\nSeed: {settings.seed}\n\nClick 'Regenerate' to apply.",
                        "OK");
                }
                catch (Exception e)
                {
                    EditorUtility.DisplayDialog("Import Failed",
                        $"Failed to import settings:\n{e.Message}",
                        "OK");
                }
            }
        }
    }
}
