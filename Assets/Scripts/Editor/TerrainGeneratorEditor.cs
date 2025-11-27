using UnityEngine;
using UnityEditor;

namespace LandOfTheConsumers.Terrain
{
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
            // Noise settings
            EditorPrefs.SetInt(PREFS_PREFIX + "Octaves", generator.octaves);
            EditorPrefs.SetFloat(PREFS_PREFIX + "Frequency", generator.frequency);
            EditorPrefs.SetFloat(PREFS_PREFIX + "Amplitude", generator.amplitude);
            EditorPrefs.SetFloat(PREFS_PREFIX + "Lacunarity", generator.lacunarity);
            EditorPrefs.SetFloat(PREFS_PREFIX + "Persistence", generator.persistence);

            // Terrain shape
            EditorPrefs.SetFloat(PREFS_PREFIX + "GroundHeight", generator.groundHeight);
            EditorPrefs.SetFloat(PREFS_PREFIX + "HeightMultiplier", generator.heightMultiplier);

            // World settings
            EditorPrefs.SetInt(PREFS_PREFIX + "WorldSizeX", generator.worldSize.x);
            EditorPrefs.SetInt(PREFS_PREFIX + "WorldSizeY", generator.worldSize.y);
            EditorPrefs.SetInt(PREFS_PREFIX + "WorldSizeZ", generator.worldSize.z);

            Debug.Log("[TerrainGenerator] Settings saved: " +
                $"Octaves={generator.octaves}, Frequency={generator.frequency:F3}, " +
                $"Amplitude={generator.amplitude:F2}, Lacunarity={generator.lacunarity:F2}, " +
                $"Persistence={generator.persistence:F2}, GroundHeight={generator.groundHeight:F1}, " +
                $"HeightMult={generator.heightMultiplier:F1}, WorldSize=({generator.worldSize.x},{generator.worldSize.y},{generator.worldSize.z})");
        }

        private void LoadSettings(TerrainGenerator generator, bool showDialog = false)
        {
            if (EditorPrefs.HasKey(PREFS_PREFIX + "Octaves"))
            {
                Undo.RecordObject(generator, "Load Terrain Settings");

                // Noise settings
                generator.octaves = EditorPrefs.GetInt(PREFS_PREFIX + "Octaves", 4);
                generator.frequency = EditorPrefs.GetFloat(PREFS_PREFIX + "Frequency", 0.05f);
                generator.amplitude = EditorPrefs.GetFloat(PREFS_PREFIX + "Amplitude", 1f);
                generator.lacunarity = EditorPrefs.GetFloat(PREFS_PREFIX + "Lacunarity", 2f);
                generator.persistence = EditorPrefs.GetFloat(PREFS_PREFIX + "Persistence", 0.5f);

                // Terrain shape
                generator.groundHeight = EditorPrefs.GetFloat(PREFS_PREFIX + "GroundHeight", 8f);
                generator.heightMultiplier = EditorPrefs.GetFloat(PREFS_PREFIX + "HeightMultiplier", 5f);

                // World settings
                int worldX = EditorPrefs.GetInt(PREFS_PREFIX + "WorldSizeX", 4);
                int worldY = EditorPrefs.GetInt(PREFS_PREFIX + "WorldSizeY", 2);
                int worldZ = EditorPrefs.GetInt(PREFS_PREFIX + "WorldSizeZ", 4);
                generator.worldSize = new Vector3Int(worldX, worldY, worldZ);

                EditorUtility.SetDirty(generator);

                Debug.Log("[TerrainGenerator] Settings loaded: " +
                    $"Octaves={generator.octaves}, Frequency={generator.frequency:F3}, " +
                    $"Amplitude={generator.amplitude:F2}, Lacunarity={generator.lacunarity:F2}, " +
                    $"Persistence={generator.persistence:F2}, GroundHeight={generator.groundHeight:F1}, " +
                    $"HeightMult={generator.heightMultiplier:F1}, WorldSize=({generator.worldSize.x},{generator.worldSize.y},{generator.worldSize.z})");
            }
            else if (showDialog)
            {
                EditorUtility.DisplayDialog("No Saved Settings",
                    "No saved settings found. Use 'Save Preset' first.",
                    "OK");
            }
        }
    }
}
