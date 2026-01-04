using UnityEngine;
using UnityEditor;

namespace LandOfTheConsumers.Procedural
{
    [CustomEditor(typeof(EdgeManager))]
    public class EdgeManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EdgeManager edgeManager = (EdgeManager)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Edge Generation Controls", EditorStyles.boldLabel);

            // Generate All Edges button
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Generate All Edges", GUILayout.Height(40)))
            {
                edgeManager.GenerateAllEdges();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            // Cancel button
            GUI.backgroundColor = Color.yellow;
            if (GUILayout.Button("Cancel Generation", GUILayout.Height(30)))
            {
                edgeManager.CancelGeneration();
            }

            // Clear button
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Clear All Edges", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Clear All Edges",
                    "Are you sure you want to delete all generated edges?",
                    "Yes, Clear",
                    "Cancel"))
                {
                    edgeManager.ClearAndCancel();
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            // Show edge statistics
            if (edgeManager.AllEdges != null && edgeManager.AllEdges.Count > 0)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Edge Statistics", EditorStyles.boldLabel);

                int totalCenterPixels = 0;
                foreach (var edge in edgeManager.AllEdges)
                {
                    totalCenterPixels += edge.centerPixels.Count;
                }

                EditorGUILayout.HelpBox(
                    $"Edge Pairs: {edgeManager.AllEdges.Count}\n" +
                    $"Total Center Pixels: {totalCenterPixels}\n" +
                    $"Child Objects: {edgeManager.transform.childCount}",
                    MessageType.Info
                );
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Quick Guide", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "EDGE GENERATION SYSTEM:\n\n" +
                "PREREQUISITES:\n" +
                "• Generate regions first using CellularNoiseVisualizer\n" +
                "• Assign CellularNoiseVisualizer reference\n" +
                "• Assign RegionQuadVisualizer reference\n\n" +
                "AUTO-GENERATION (PLAY MODE):\n" +
                "• Edges automatically generate after LOD0 completes\n" +
                "• Enable/disable with 'Auto Generate In Play Mode' toggle\n" +
                "• Perfect for runtime procedural generation\n\n" +
                "WHAT IT DOES:\n" +
                "• Creates 3-pixel-wide edges between region pairs\n" +
                "• Uses Voronoi edge data from cellular noise\n" +
                "• Generates LOD0 (high detail) meshes\n" +
                "• Averages heights from both adjacent regions\n\n" +
                "EDGE NAMING:\n" +
                "• EdgePair_{RegionA}_{RegionB}\n" +
                "• Each edge pair contains multiple chunks\n" +
                "• Chunks follow same 32x32 pattern as regions\n\n" +
                "TIPS:\n" +
                "• Generation is sequential to prevent freezing\n" +
                "• Use Cancel if generation takes too long\n" +
                "• Edges render slightly above regions (no z-fighting)",
                MessageType.Info
            );
        }
    }
}
