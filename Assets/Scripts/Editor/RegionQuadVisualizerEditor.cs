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

            // Big generate button
            EditorGUILayout.Space(10);
            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button("Generate Region Quads", GUILayout.Height(40)))
            {
                visualizer.GenerateRegionQuads();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Quick Guide", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "REGION QUAD VISUALIZER:\n\n" +
                "1. Assign a CellularNoiseVisualizer reference\n" +
                "2. Generate noise on the CellularNoiseVisualizer first\n" +
                "3. Set the Region Index to visualize (0, 1, 2, etc.)\n" +
                "4. Click 'Generate Region Quads' to create the mesh\n\n" +
                "SETTINGS:\n" +
                "• Quad Size: Size of each pixel quad (default 0.5)\n" +
                "• Height Offset: Y position of the quads\n" +
                "• Region Color: Color of the generated quads\n\n" +
                "This creates a 3D mesh representation of a single region,\n" +
                "with one quad per pixel that belongs to that region.",
                MessageType.Info
            );
        }
    }
}
