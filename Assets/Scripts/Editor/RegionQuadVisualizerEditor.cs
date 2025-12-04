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
            if (GUILayout.Button("Generate All Regions With Random Heights", GUILayout.Height(40)))
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
                "MODE 2: ALL REGIONS WITH RANDOM HEIGHTS\n" +
                "1. Assign a CellularNoiseVisualizer reference\n" +
                "2. Generate noise on the CellularNoiseVisualizer first\n" +
                "3. Adjust Min/Max Random Height (default 0-200)\n" +
                "4. Enable 'Generate LODs' for 3 LOD levels (optional)\n" +
                "5. Click 'Generate All Regions With Random Heights'\n\n" +
                "SETTINGS:\n" +
                "• Quad Size: Size of each pixel quad (default 0.5)\n" +
                "• Height Offset: Y position (single region mode)\n" +
                "• Region Color: Color (single region mode)\n" +
                "• Min/Max Random Height: Height range (all regions mode)\n" +
                "• Use Random Colors: Random or HSV colors per region\n\n" +
                "LOD SETTINGS:\n" +
                "• Generate LODs: Creates 3 LOD levels per region\n" +
                "• LOD 0 (highest detail): Quad size 0.5 (default)\n" +
                "• LOD 1 (medium detail): Quad size 1.0 (default)\n" +
                "• LOD 2 (lowest detail): Quad size 2.0 (default)\n" +
                "• Transition values: Screen % for LOD switching\n\n" +
                "The random height mode is useful for testing if the\n" +
                "system correctly understands region data. LODs help\n" +
                "optimize performance by reducing detail at distance.",
                MessageType.Info
            );
        }
    }
}
