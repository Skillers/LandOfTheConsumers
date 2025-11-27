# Terrain Quick Reference Guide

## Terrain Control Buttons (in Inspector)

When you select the TerrainGenerator object, you'll see three buttons at the bottom:

🟢 **Generate Terrain** - Creates new terrain (keeps existing chunks)
🟡 **Regenerate** - Clears all and generates fresh terrain
🔴 **Clear Terrain** - Removes all terrain chunks

---

## Settings at a Glance

### 🌍 World Settings

| Setting | What It Does | Example Values |
|---------|--------------|----------------|
| **World Size** | Number of chunks (X, Y, Z) | (4, 2, 4) = 64x32x64 units |
| **Terrain Material** | Visual appearance | Assign any Unity material |

### 🎲 Noise Settings (Multi-Level Detail)

| Setting | Effect | Values | What Changes |
|---------|--------|--------|--------------|
| **Octaves** | Detail layers | 1-8 | 🔽 3 = simple, 🔼 6 = very detailed |
| **Frequency** | Feature size | 0.01-0.2 | 🔽 0.02 = huge mountains, 🔼 0.1 = small bumps |
| **Amplitude** | Noise strength | 0.1-3.0 | 🔽 0.5 = subtle, 🔼 2.0 = dramatic |
| **Lacunarity** | Detail multiplier | 1.5-3.0 | Standard: 2.0 |
| **Persistence** | Detail fade rate | 0.1-0.9 | Standard: 0.5 |

### ⛰️ Terrain Shape

| Setting | Effect | Values | What Changes |
|---------|--------|--------|--------------|
| **Ground Height** | Base elevation | 5-15 | "Sea level" of your world |
| **Height Multiplier** | Vertical variation | 1-20 | 🔽 3 = gentle hills, 🔼 12 = tall mountains |

---

## Common Terrain Presets

### 🏔️ Mountainous Terrain
```
Octaves: 5
Frequency: 0.04
Amplitude: 1.5
Ground Height: 10
Height Multiplier: 10
```

### 🌾 Gentle Hills
```
Octaves: 3
Frequency: 0.05
Amplitude: 0.8
Ground Height: 8
Height Multiplier: 3
```

### 🏜️ Desert Dunes
```
Octaves: 4
Frequency: 0.03
Amplitude: 1.0
Ground Height: 5
Height Multiplier: 4
Lacunarity: 1.8
```

### 🗻 Dramatic Peaks
```
Octaves: 6
Frequency: 0.06
Amplitude: 2.0
Ground Height: 8
Height Multiplier: 15
```

### 🌊 Rolling Plains
```
Octaves: 2
Frequency: 0.02
Amplitude: 0.5
Ground Height: 8
Height Multiplier: 2
```

---

## Quick Troubleshooting

| Problem | Solution |
|---------|----------|
| Terrain too flat | Increase Height Multiplier |
| Terrain too spiky | Decrease Height Multiplier or Amplitude |
| Terrain looks blocky | Increase Octaves (adds detail) |
| Only huge mountains, no detail | Increase Frequency or Octaves |
| Only small bumps, no large features | Decrease Frequency |
| Generation is slow | Decrease Octaves or World Size |
| Chunks overlapping | This was a bug - make sure you have the latest code! |

---

## How Octaves Work (Visual Explanation)

Think of octaves like layering transparencies:

```
Octave 1 (base):     ___/‾‾‾\___        (large mountains)
Octave 2:            _/\_/\_/\_/\       (medium hills)
Octave 3:            ^v^v^v^v^v^v       (small bumps)
Combined Result:     __/\/‾‾\/\__       (natural terrain!)
```

- **Low octaves (1-2)**: Simple, smooth terrain
- **Medium octaves (3-5)**: Natural-looking terrain with variety
- **High octaves (6-8)**: Very detailed but slower to generate

---

## How Frequency Works

```
Low Frequency (0.02):    ___/‾‾‾‾‾‾‾\___    (wide, gentle slopes)
Medium Frequency (0.05): __/‾‾\__/‾‾\__    (rolling hills)
High Frequency (0.1):    _/\_/\_/\_/\_/\   (many small features)
```

---

## Parameter Interaction Guide

These parameters work together:

1. **Frequency + Height Multiplier** = Overall terrain character
   - Low freq + High multiplier = Tall, wide mountains
   - High freq + Low multiplier = Small, gentle bumps

2. **Octaves + Persistence** = Detail distribution
   - High octaves + High persistence = All details equally visible
   - High octaves + Low persistence = Large features dominate

3. **Amplitude + Lacunarity** = Noise shape
   - Usually keep defaults (Amplitude: 1.0, Lacunarity: 2.0)

---

## Performance Tips

💡 **Fastest Generation**: Octaves: 2, World Size: (2, 1, 2)
⚡ **Balanced**: Octaves: 4, World Size: (4, 2, 4)
🐌 **Detailed**: Octaves: 6+, World Size: (6, 3, 6)

---

**Remember:** All settings have tooltips! Just hover over them in the Inspector.
