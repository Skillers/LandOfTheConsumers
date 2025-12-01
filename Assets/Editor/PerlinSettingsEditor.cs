using UnityEngine;
using UnityEditor;
using System.IO;

[CustomEditor(typeof(PerlinSettings))]
public class PerlinSettingsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        PerlinSettings settings = (PerlinSettings)target;
        serializedObject.Update();

        // Store original values to detect manual changes
        float oldHeight = settings.heightMultiplier;
        float oldScale = settings.scale;
        float oldXScale = settings.xScale;
        float oldZScale = settings.zScale;

        // Draw default inspector for width, height, offsets
        EditorGUILayout.PropertyField(serializedObject.FindProperty("width"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("height"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("offSetX"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("offSetY"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Terrain Settings", EditorStyles.boldLabel);

        // Draw default inspector for the terrain parameters
        EditorGUILayout.PropertyField(serializedObject.FindProperty("heightMultiplier"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("scale"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("xScale"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("zScale"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);

        // Draw the list of presets
        EditorGUILayout.PropertyField(serializedObject.FindProperty("terrainNoisePresets"), true);

        EditorGUILayout.Space();

        // Preset selection dropdown
        if (settings.terrainNoisePresets != null && settings.terrainNoisePresets.Count > 0)
        {
            // Build array of preset names
            string[] presetNames = new string[settings.terrainNoisePresets.Count + 1];
            presetNames[0] = "None";

            for (int i = 0; i < settings.terrainNoisePresets.Count; i++)
            {
                if (settings.terrainNoisePresets[i] != null)
                {
                    presetNames[i + 1] = settings.terrainNoisePresets[i].presetName;
                }
                else
                {
                    presetNames[i + 1] = "Missing Preset";
                }
            }

            // Find current selected preset index
            int currentIndex = 0;
            if (settings.selectedPreset != null)
            {
                for (int i = 0; i < settings.terrainNoisePresets.Count; i++)
                {
                    if (settings.terrainNoisePresets[i] == settings.selectedPreset)
                    {
                        currentIndex = i + 1;
                        break;
                    }
                }
            }

            EditorGUI.BeginChangeCheck();
            int selectedIndex = EditorGUILayout.Popup("Current Preset", currentIndex, presetNames);

            if (EditorGUI.EndChangeCheck())
            {
                if (selectedIndex == 0)
                {
                    // None selected - clear the preset
                    Undo.RecordObject(settings, "Clear Terrain Preset");
                    settings.selectedPreset = null;
                    EditorUtility.SetDirty(settings);
                }
                else
                {
                    TerrainNoisePreset preset = settings.terrainNoisePresets[selectedIndex - 1];
                    if (preset != null)
                    {
                        Undo.RecordObject(settings, "Apply Terrain Preset");
                        settings.ApplyPreset(preset);
                        EditorUtility.SetDirty(settings);
                    }
                }
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Add presets to the list above to enable preset selection.", MessageType.Info);
        }

        EditorGUILayout.Space();

        // Button to create new preset from current settings
        if (GUILayout.Button("Create Preset from Current Settings"))
        {
            CreatePresetFromCurrentSettings(settings);
        }

        // Apply all serialized property changes
        serializedObject.ApplyModifiedProperties();

        // Check if terrain settings changed manually and clear the selected preset
        if (settings.heightMultiplier != oldHeight ||
            settings.scale != oldScale ||
            settings.xScale != oldXScale ||
            settings.zScale != oldZScale)
        {
            if (settings.selectedPreset != null)
            {
                settings.selectedPreset = null;
                EditorUtility.SetDirty(settings);
            }
        }
    }

    private void CreatePresetFromCurrentSettings(PerlinSettings settings)
    {
        // Create new preset asset
        TerrainNoisePreset newPreset = ScriptableObject.CreateInstance<TerrainNoisePreset>();

        // Copy current settings to preset
        newPreset.presetName = "New Preset";
        newPreset.scale = settings.scale;
        newPreset.scaleX = settings.xScale;
        newPreset.scaleZ = settings.zScale;
        newPreset.heightMultiplier = settings.heightMultiplier;

        // Determine save path
        string path = "Assets/PerlinNoisePresets/";

        // Create directory if it doesn't exist
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        // Find unique filename
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(path + "NewTerrainPreset.asset");

        // Create and save the asset
        AssetDatabase.CreateAsset(newPreset, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Add to presets list and select it
        settings.terrainNoisePresets.Add(newPreset);
        settings.selectedPreset = newPreset;

        EditorUtility.SetDirty(settings);

        // Select the new preset in the project window and allow renaming
        Selection.activeObject = newPreset;
        EditorGUIUtility.PingObject(newPreset);

        Debug.Log($"Created new preset at: {assetPath}");
    }
}
