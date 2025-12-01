using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PerlinSettings : MonoBehaviour
{
    //I wish i understood this but if I lower it, it breaks. 
    public int width = 256;
    public int height = 256;

    //What area of perlin do you want to see
    public int offSetX = 0;
    public int offSetY = 0;
    
    //The stuff that makes this actually look cool and work.
    public float heightMultiplier = 1f;
    public float scale = 20f;
    public float xScale = 20f;
    public float zScale = 20f;

    public List<TerrainNoisePreset> terrainNoisePresets = new List<TerrainNoisePreset>();

    [HideInInspector]
    public TerrainNoisePreset selectedPreset;

    public void ApplyPreset(TerrainNoisePreset preset)
    {
        if (preset == null) return;
        selectedPreset = preset;
        heightMultiplier = preset.heightMultiplier;
        scale = preset.scale;
        xScale = preset.scaleX;
        zScale = preset.scaleZ;
    }
}
