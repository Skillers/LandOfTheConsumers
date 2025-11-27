# Marching Cubes Terrain Setup Guide

## 🆕 New Features

✨ **Settings Persistence System** - Save and load your terrain settings, even from Play mode!
📝 **Detailed Tooltips** - Hover over any setting to see comprehensive explanations
🎛️ **Enhanced Inspector** - Easy-to-use buttons with visual feedback

See **SETTINGS_PERSISTENCE_GUIDE.md** for details on the save/load system.

---

## What Was Created

A complete marching cubes terrain system with multi-level Perlin noise has been implemented with the following components:

### New Scripts
1. **NoiseGenerator.cs** (`Assets/Scripts/Procedural/`)
   - 3D Perlin noise implementation
   - Multi-octave fractal noise (for natural terrain variation)
   - Configurable frequency, amplitude, lacunarity, and persistence

2. **MarchingCubes.cs** (`Assets/Scripts/Terrain/`)
   - Complete marching cubes algorithm
   - Lookup tables for 256 cube configurations
   - Generates smooth terrain meshes from voxel data

3. **TerrainChunk.cs** (`Assets/Scripts/Terrain/`)
   - Individual chunk management
   - Generates 16x16x16 voxel chunks
   - Automatic mesh and collider generation
   - Configurable noise and terrain parameters

4. **TerrainGenerator.cs** (`Assets/Scripts/Terrain/`)
   - World manager that spawns multiple chunks
   - Configurable world size
   - Progress tracking during generation
   - Context menu options for easy regeneration

5. **TerrainGeneratorEditor.cs** (`Assets/Scripts/Editor/`)
   - Custom Inspector with convenient buttons
   - Settings persistence system (Save/Load)
   - Play mode warning system
   - Quick settings guide panel

## How to Set Up in Unity

### Step 1: Create a Terrain Material
1. In Unity, go to **Assets** folder
2. Right-click → **Create** → **Material**
3. Name it "TerrainMaterial"
4. Configure the material:
   - Set shader to **Standard** or **Universal Render Pipeline/Lit**
   - Set color to something visible (e.g., green for grass)
   - Adjust smoothness/metallic as desired

### Step 2: Set Up the Terrain Generator
1. Open your main scene (`Assets/Scenes/SampleScene.unity`)
2. Create a new empty GameObject: **GameObject** → **Create Empty**
3. Name it "TerrainGenerator"
4. Set position to (0, 0, 0)
5. Add the TerrainGenerator component:
   - Click **Add Component**
   - Search for "TerrainGenerator"
   - Select it

6. Configure the TerrainGenerator in the Inspector:
   - **World Settings:**
     - World Size: (4, 2, 4) - generates 4x2x4 chunks (32 chunks total)
     - Terrain Material: Drag the TerrainMaterial you created

   - **Noise Settings:**
     - Octaves: 4 (more = more detail)
     - Frequency: 0.05 (lower = larger features)
     - Amplitude: 1.0
     - Lacunarity: 2.0 (frequency multiplier between octaves)
     - Persistence: 0.5 (amplitude multiplier between octaves)

   - **Terrain Shape:**
     - Ground Height: 8.0 (base terrain height)
     - Height Multiplier: 5.0 (how much noise affects height)

   - **Generation:**
     - Generate On Start: ✓ (enabled)
     - Show Progress In Console: ✓ (enabled)

### Step 3: Configure Layers for Physics
1. Create a new layer for terrain:
   - **Edit** → **Project Settings** → **Tags and Layers**
   - Add a new layer called "Ground" (or use an existing ground layer)

2. Set the terrain chunks to use this layer:
   - Once terrain generates, select any "Chunk_X_Y_Z" object
   - Set Layer to "Ground"
   - Click "Yes, change children" when prompted

### Step 4: Remove the Old Plane
1. In the Hierarchy, find the existing Plane object (if any)
2. Either:
   - Delete it entirely, OR
   - Disable it by unchecking the checkbox next to its name

### Step 5: Update StickSpawner Settings
The StickSpawner is already compatible! Just ensure:
1. Select the StickSpawner GameObject in your scene
2. In the Inspector, check **Ground Layer** setting
3. Make sure it includes the "Ground" layer you assigned to terrain chunks
4. Adjust **Spawn Center** if needed (default Vector3.zero should work)
5. Adjust **Spawn Radius** to match your terrain size (e.g., 30-50)

### Step 6: Test the Terrain
1. Press **Play** in Unity
2. The terrain should generate automatically
3. Check the Console for generation progress
4. Your player should be able to walk on the terrain
5. Sticks should spawn on the terrain surface

## Using the Terrain Controls

The TerrainGenerator Inspector has convenient buttons for controlling terrain:

1. Select the TerrainGenerator object in the Hierarchy
2. Look at the Inspector - you'll see several button sections:

### Settings Persistence (Top Section)
   - **💾 Save Current Settings** (Blue) - Save settings to persist across Play mode sessions
   - **📁 Load Saved Settings** (Orange) - Restore previously saved settings

### Terrain Controls (Bottom Section)
   - **Generate Terrain** (Green) - Create new terrain (keeps existing)
   - **Regenerate** (Yellow) - Clear everything and generate fresh terrain
   - **Clear Terrain** (Red) - Remove all terrain chunks

**Important Tips:**
- 💡 **Hover over any setting** to see a detailed tooltip explaining what it does!
- 💡 **Changes in Play mode** are lost unless you click "💾 Save Current Settings"
- 💡 See **SETTINGS_PERSISTENCE_GUIDE.md** for full details on saving/loading

### Alternative Method (Context Menu)
You can also right-click the TerrainGenerator component header and select:
- Generate World
- Clear World
- Regenerate World

## Understanding the Settings

Each parameter has a tooltip when you hover over it in the Inspector. Here's a detailed guide:

### Noise Settings (Multi-Level Detail)
- **Octaves**: How many noise layers are combined (1-8)
  - More octaves = more detail levels (big mountains + small rocks)
  - Fewer octaves = simpler, smoother terrain

- **Frequency**: Base noise scale (0.01-0.2)
  - Lower = large features like mountains and valleys
  - Higher = small bumps and details

- **Amplitude**: Maximum height variation (0.1-3.0)
  - Controls overall noise contribution to terrain

- **Lacunarity**: Frequency multiplier between octaves (1.5-3.0)
  - Standard is 2.0 (each octave is 2x more detailed)

- **Persistence**: Amplitude multiplier between octaves (0.1-0.9)
  - Standard is 0.5 (each octave is 50% less pronounced)

### Terrain Shape
- **Ground Height**: Base elevation/"sea level" (try 8-12)
- **Height Multiplier**: How much noise affects terrain height (1-20)
  - Higher = dramatic mountains and valleys

## Adjusting Terrain Appearance

### Making Terrain Flatter
- Decrease **Height Multiplier** (try 2.0-3.0)
- Decrease **Amplitude** (try 0.5-0.8)

### Making Terrain More Hilly
- Increase **Height Multiplier** (try 8.0-12.0)
- Increase **Amplitude** (try 1.5-2.0)

### Adding More Detail
- Increase **Octaves** (try 5-6) - WARNING: slower generation
- Increase **Frequency** (try 0.08-0.1)

### Larger/Smoother Features
- Decrease **Frequency** (try 0.02-0.03)
- Decrease **Lacunarity** (try 1.5)

### Making Terrain Bigger
- Increase **World Size** (e.g., 6x3x6 for 108 chunks)
- NOTE: More chunks = longer generation time

## Performance Tips

1. **Start Small**: Test with (2, 1, 2) world size first
2. **Optimize Later**: Add LOD system if terrain is too slow
3. **Collider Optimization**: Consider using simplified colliders for distant chunks
4. **Async Generation**: For larger worlds, implement async terrain generation

## Troubleshooting

### Terrain Doesn't Appear
- Check that Terrain Material is assigned
- Check Console for errors
- Verify shaders are compiled correctly
- Make sure Generate On Start is enabled

### Player Falls Through Terrain
- Ensure terrain chunks have MeshCollider components
- Check that Ground layer is set correctly
- Verify player's CharacterController is using proper layers

### Sticks Don't Spawn
- Check StickSpawner's Ground Layer matches terrain layer
- Increase Spawn Radius if terrain is spread out
- Check Console for spawn warnings

### Terrain Looks Blocky
- This is normal for marching cubes at lower voxel density
- Decrease voxelSize in TerrainChunk (requires code change)
- Increase chunk resolution (16x16x16 → 32x32x32, requires code change)
- Add a smoothing shader/material

### Generation is Slow
- Reduce World Size
- Reduce Octaves (fewer = faster)
- Disable Show Progress In Console
- Consider implementing chunk streaming (load chunks near player only)

## Next Steps

1. **Add Textures**: Create a triplanar shader for better texturing
2. **Add Caves**: Modify noise calculation for 3D features
3. **Add Biomes**: Blend different noise patterns for variety
4. **Optimize**: Implement chunk LOD system
5. **Network Sync**: Add deterministic seed-based generation for multiplayer
6. **Editing**: Implement runtime terrain modification (if desired later)

## Integration Status

✅ **Working Systems:**
- Multi-octave Perlin noise generation
- Marching cubes mesh generation
- Automatic collider generation
- Chunk-based terrain management
- Compatible with existing PlayerController
- Compatible with existing StickSpawner
- Compatible with existing camera systems

🔄 **Future Enhancements:**
- Chunk streaming (load/unload based on player distance)
- Level of Detail (LOD) system
- Texture splatting/triplanar mapping
- Caves and overhangs (3D noise)
- Biome system
- Network synchronization
- Runtime terrain editing

Enjoy your new marching cubes terrain!
