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
                "USAGE:\n" +
                "1. Assign a CellularNoiseVisualizer reference\n" +
                "2. Generate noise on the CellularNoiseVisualizer first\n" +
                "3. (Optional) Assign a PerlinSettings reference that\n" +
                "   contains your Terrain Noise Presets\n" +
                "4. Click 'Generate All Regions'\n\n" +
                "SETTINGS:\n" +
                "• Use Random Colors: Random or HSV colors per region\n" +
                "• Perlin Settings Reference: GameObject with PerlinSettings\n" +
                "  component that has preset list\n\n" +
                "REGION NAMING & MAPPING:\n" +
                "• Each region GameObject: R_{PresetName}_{Index}\n" +
                "  Example: R_Basic Hills_0, R_Spikes_1\n" +
                "• Region has RegionTerrainGenerator component\n" +
                "• Preset mapping stored in the main visualizer's\n" +
                "  regionPresetMapping dictionary\n\n" +
                "TERRAIN GENERATION:\n" +
                "• Region parent has RegionTerrainGenerator component\n" +
                "• LOD 0 is an empty shell container\n" +
                "• Terrain chunks generated as children of LOD 0\n" +
                "• Generates marching cubes terrain at 2 pixels/unit\n" +
                "• Uses the assigned Perlin preset for generation\n" +
                "• LOD 1 & 2 use quad-based visualization\n\n" +
                "LOD LEVELS (Always Active):\n" +
                "• LOD 0: Full marching cubes terrain (highest detail)\n" +
                "• LOD 1: Quad mesh with size 1.0 (medium detail)\n" +
                "• LOD 2: Quad mesh with size 2.0 (lowest detail)\n\n" +
                "Terrain generates asynchronously in chunks to prevent lag.",
                MessageType.Info
            );
        }
    }
}
