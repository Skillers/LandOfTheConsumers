using UnityEngine;
using UnityEditor;

namespace LandOfTheConsumers.Procedural
{
    [CustomEditor(typeof(CellularNoiseVisualizer))]
    public class CellularNoiseVisualizerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            CellularNoiseVisualizer visualizer = (CellularNoiseVisualizer)target;

            // Big generate button at the top
            EditorGUILayout.Space(10);
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Generate Noise", GUILayout.Height(40)))
            {
                visualizer.GenerateNoiseTexture();
            }
            GUI.backgroundColor = Color.white;

            // Calculate and show world info
            const int biomeSize = 128;
            int worldWidth = visualizer.worldSizeInBiomes.x * biomeSize;
            int worldHeight = visualizer.worldSizeInBiomes.y * biomeSize;
            int textureWidth = worldWidth * visualizer.pixelsPerUnit;
            int textureHeight = worldHeight * visualizer.pixelsPerUnit;

            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                $"World Size: {worldWidth} x {worldHeight} units\n" +
                $"Texture Resolution: {textureWidth} x {textureHeight} pixels",
                MessageType.None
            );

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Seed Controls", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField($"Current Seed: {visualizer.seed}", EditorStyles.miniLabel);

            if (GUILayout.Button("Random Seed", GUILayout.Width(120), GUILayout.Height(20)))
            {
                Undo.RecordObject(visualizer, "Randomize Seed");
                visualizer.seed = Random.Range(1, 999999);
                EditorUtility.SetDirty(visualizer);
                visualizer.GenerateNoiseTexture();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Quick Guide", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "LIVE UPDATES: Adjust any slider to see instant changes!\n\n" +
                "BIOME-BASED CELLULAR NOISE:\n" +
                "• 90% of biomes have ONE random center point\n" +
                "• 10% of biomes have NO point (more organic!)\n" +
                "• Darker areas = closer to region centers\n" +
                "• Lighter areas = farther from region centers\n" +
                "• YELLOW MARKERS show actual point locations!\n" +
                "• RED LINES show connected regions\n" +
                "• WHITE NUMBERS show region IDs (select object to see)\n\n" +
                "WORLD SIZE:\n" +
                "• Set biomes (10x10) - each biome is 128x128 units\n" +
                "• Plane will be created automatically at correct size\n\n" +
                "PIXELS PER UNIT:\n" +
                "• Higher = more detailed texture (but slower)\n" +
                "• Lower = faster generation (but less smooth)\n\n" +
                "CONNECTION DISTANCE:\n" +
                "• 0 = No connections (standard Voronoi)\n" +
                "• Higher = points merge into larger regions\n" +
                "• Connected points share the same region ID!\n\n" +
                "Perfect for: biome generation, region-based systems",
                MessageType.Info
            );
        }
    }
}
