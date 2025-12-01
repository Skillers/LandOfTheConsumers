using UnityEngine;

[CreateAssetMenu(fileName = "NewTerrainPreset", menuName = "Terrain/Perlin Noise Preset")]
public class TerrainNoisePreset : ScriptableObject
{
    [Header("Terrain Settings")]
    public string presetName;
    
    [Header("Scale Settings")]
    [Tooltip("Overall noise scale")]
    public float scale = 20f;
    
    [Tooltip("Scale multiplier in X direction")]
    public float scaleX = 1f;
    
    [Tooltip("Scale multiplier in Z direction")]
    public float scaleZ = 1f;
    
    [Header("Height Settings")]
    [Tooltip("Multiplier for terrain height")]
    public float heightMultiplier = 10f;
}