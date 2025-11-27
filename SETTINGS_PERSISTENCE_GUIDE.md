# Settings Persistence Guide

## ✨ Automatic Settings Persistence

Your terrain settings now **automatically persist** when you exit Play mode!

### How It Works

The system automatically saves and restores your settings:

1. **Before entering Play mode**: Current settings are saved
2. **During Play mode**: You can adjust settings freely in the Inspector
3. **When exiting Play mode**: Settings are saved again
4. **After exiting Play mode**: Your Play mode settings are automatically restored!

**No buttons to click - it just works!** ✅

---

## Two Ways Settings Persist

### 1. ✅ **Edit Mode Changes (Automatic - Scene File)**
Changes made when **NOT in Play mode** are automatically saved to your scene:
- Adjust any setting in the Inspector
- Settings are saved when you save the scene (Ctrl+S)
- These changes persist in your project forever

### 2. ✅ **Play Mode Changes (Automatic - EditorPrefs)**
Changes made **during Play mode** are automatically saved and restored:
- Unity normally discards Play mode changes
- Our system captures changes when exiting Play mode
- Settings are automatically restored to the Edit mode object
- Works seamlessly without any manual intervention

---

## Manual Save/Load Buttons (Optional)

**Note:** Auto-save handles Play mode persistence automatically, but you can still manually save/load presets!

When you select the TerrainGenerator object, you'll see two buttons for manual preset management:

### 💾 **Save Preset**
- Manually saves current settings as a preset
- Useful for:
  - Creating named configurations (Mountains, Hills, etc.)
  - Backing up your favorite terrain settings
  - Sharing settings between different scenes

### 📁 **Load Preset**
- Manually restores previously saved preset
- Asks for confirmation before overwriting current values
- Loads all noise, terrain shape, and world size settings

**These buttons are optional!** The auto-save system already handles Play mode persistence.

---

## Recommended Workflow

### Testing Terrain While Playing (Now Easier!)

1. **Enter Play mode** to see your terrain in action
2. **Adjust settings** in the Inspector while playing
3. **Exit Play mode** - that's it! Your settings are automatically restored! ✨
4. **Click "Regenerate"** if you want to apply the changes to the terrain

**It's that simple!** No manual saving required.

### Creating Preset Configurations

You can create multiple presets by saving settings at different times:

**Preset 1: Mountains**
- Adjust settings for mountainous terrain
- Enter Play mode and test
- Click "💾 Save Current Settings"
- Name it mentally as "Mountains preset"

**Preset 2: Hills** (Using a Different Scene)
- Adjust settings for hilly terrain
- Save Current Settings
- This overwrites the previous save

**Note:** Currently only one preset can be saved. For multiple presets, consider duplicating the TerrainGenerator prefab or GameObject.

---

## What Gets Saved

The Save/Load system preserves:

✅ **Noise Settings:**
- Octaves
- Frequency
- Amplitude
- Lacunarity
- Persistence

✅ **Terrain Shape:**
- Ground Height
- Height Multiplier

✅ **World Settings:**
- World Size (X, Y, Z)

❌ **NOT Saved:**
- Terrain Material (scene-specific)
- Generate On Start flag
- Show Progress In Console flag

---

## Warning System

When you're in Play mode, you'll see a **yellow warning box** at the top of the Inspector:

```
⚠️ PLAY MODE: Changes made now will be lost when you exit play mode!
Use the 'Save Current Settings' button below to preserve your changes.
```

This reminds you to save your settings before exiting Play mode.

---

## Advanced: Understanding EditorPrefs

Settings are saved using Unity's `EditorPrefs` system:
- Stored on your computer (not in the project)
- Persists across Unity sessions
- Specific to your machine (not shared via version control)
- Stored at: `HKEY_CURRENT_USER\Software\Unity Technologies\Unity Editor 5.x` on Windows

**Tip:** If you want to share settings with teammates, use the Copy/Paste method or duplicate the TerrainGenerator GameObject.

---

## Detailed Tooltips

Every setting now has **detailed tooltips** that explain:
- What the setting does
- How it affects terrain
- Recommended value ranges
- Mathematical formulas (for Lacunarity and Persistence)
- Visual descriptions

**How to use:**
1. Hover your mouse over any setting label in the Inspector
2. Wait 1 second
3. A detailed tooltip appears explaining the setting

### Example Tooltips

**Octaves:**
```
OCTAVES: Number of noise layers combined together to create natural-looking terrain.

• Low (1-2): Simple smooth terrain, very fast generation
• Medium (3-5): Natural varied terrain with detail, balanced performance
• High (6-8): Highly detailed terrain with features at many scales, slower

Think of it like layering: large mountains + medium hills + small rocks = realistic terrain
```

**Lacunarity:**
```
LACUNARITY: How much the frequency increases between each octave layer.

• Low (1.5-1.8): Octaves have similar feature sizes, smoother blending
• Standard (2.0): Each octave is 2x more detailed than the last (recommended)
• High (2.2-3.0): Octaves vary greatly in size, more contrast between layers

Formula: frequencyN = frequency × (lacunarity ^ N)
Example: If frequency=0.05 and lacunarity=2.0, octave 2 uses frequency 0.1
```

---

## Troubleshooting

### "My settings disappeared after exiting Play mode!"
**Solution:**
1. Check the Console for auto-save messages:
   - "Auto-save: Settings saved from Play mode"
   - "Auto-restore: Settings restored after exiting Play mode"
2. If you don't see these messages, the TerrainGenerator might not be selected
3. Make sure the TerrainGenerator GameObject exists in your scene
4. Try restarting Unity if the issue persists

### "I see the auto-save message but settings still reset"
**Solution:**
- Make sure you're looking at the same TerrainGenerator object
- Check that the settings you changed are public fields (they should all be visible in Inspector)
- Save your scene (Ctrl+S) after exiting Play mode to make changes permanent

### "Load Preset says 'No saved settings found'"
**Solution:** You haven't manually saved a preset yet. Use "💾 Save Preset" first (this is different from auto-save).

### "Tooltips aren't showing up"
**Solution:**
1. Make sure you're hovering over the setting **label** (not the value field)
2. Wait 1-2 seconds for the tooltip to appear
3. If still not working, restart Unity

### "I want multiple saved presets"
**Workaround:**
1. Create multiple TerrainGenerator GameObjects
2. Configure each with different settings
3. Disable the ones you're not using
4. Or use Prefab variants for different configurations

---

## Quick Reference

| Action | When to Use | Result |
|--------|-------------|--------|
| Adjust settings in Edit mode | Tweaking terrain before Play | Auto-saved with scene |
| Adjust settings in Play mode | Testing in real-time | Lost unless you save |
| 💾 Save Current Settings | Found good settings | Stored to EditorPrefs |
| 📁 Load Saved Settings | Want to restore saved settings | Applies saved values |
| Hover over setting | Want to understand it | Shows detailed tooltip |

---

**Remember:** The best workflow is to test in Play mode, save when you find good settings, then apply them in Edit mode!
