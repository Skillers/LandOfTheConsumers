using UnityEngine;
using UnityEditor;

namespace LandOfTheConsumers.Procedural
{
    [CustomEditor(typeof(RegionQuadVisualizer))]
    public class RegionQuadVisualizerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            RegionQuadVisualizer visualizer = (RegionQuadVisualizer)target;

            // Generate buttons
            EditorGUILayout.Space(10);

            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button("Generate Single Region Quads", GUILayout.Height(40)))
            {
                visualizer.GenerateRegionQuads();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(5);

            GUI.backgroundColor = Color.yellow;
            if (GUILayout.Button("Generate All Regions", GUILayout.Height(40)))
            {
                visualizer.GenerateAllRegionsWithRandomHeights();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Quick Guide", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "REGION QUAD VISUALIZER:\n\n" +
                "MODE 1: SINGLE REGION\n" +
                "1. Assign a CellularNoiseVisualizer reference\n" +
                "2. Generate noise on the CellularNoiseVisualizer first\n" +
                "3. Set the Region Index to visualize (0, 1, 2, etc.)\n" +
                "4. Click 'Generate Single Region Quads'\n\n" +
                "MODE 2: ALL REGIONS (with 3 LOD levels)\n" +
                "1. Assign a CellularNoiseVisualizer reference\n" +
                "2. Generate noise on the CellularNoiseVisualizer first\n" +
                "3. Click 'Generate All Regions'\n\n" +
                "SETTINGS:\n" +
                "• Quad Size: Size of each pixel quad (default 0.5)\n" +
                "• Region Color: Color (single region mode)\n" +
                "• Use Random Colors: Random or HSV colors per region\n\n" +
                "LOD LEVELS (Always Active):\n" +
                "Each region automatically gets 3 LOD levels:\n" +
                "• LOD 0 (highest detail): Quad size 0.5\n" +
                "• LOD 1 (medium detail): Quad size 1.0 (1/4 quads)\n" +
                "• LOD 2 (lowest detail): Quad size 2.0 (1/4 quads)\n" +
                "• Transition values: Screen % for LOD switching\n\n" +
                "All regions spawn at height 0. Each LOD level has\n" +
                "1/4 the quads of the previous level for performance.",
                MessageType.Info
            );
        }
    }
}
