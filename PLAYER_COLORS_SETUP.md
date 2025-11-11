# Player Random Colors Setup

Quick guide to add random player colors to your game.

## Setup Instructions

### 1. Add PlayerAppearance to Your Player Prefab

1. In Unity, find your **Capsule** prefab (in Assets folder)
2. Select it
3. Click **Add Component**
4. Search for **PlayerAppearance**
5. Add it

### 2. Assign the Renderer

The **PlayerAppearance** component needs to know which renderer to color:

1. With Capsule prefab selected, look at **PlayerAppearance** component
2. Find the **"Player Renderer"** field
3. **Option A (Auto):** Leave it empty - it will auto-find the MeshRenderer
4. **Option B (Manual):** Drag the Capsule's MeshRenderer component into this field

### 3. (Optional) Customize Color Palette

By default, players get colors from a nice preset palette. You can customize this:

In **PlayerAppearance** component:
- **Color Palette:** Array of colors players can be assigned
- You can:
  - Add more colors (increase Array size)
  - Remove colors
  - Clear the array completely for fully random RGB colors

**Default Palette:**
- Red, Blue, Green, Yellow, Magenta, Cyan, Orange, Purple, Pink, Light Green

### 4. Test It!

1. **Build your game** (or rebuild if already built)
2. **Start server:** `StartServer.cmd`
3. **Connect clients:** Run your game multiple times
4. Each player should have a **different random color**!

## What You'll See

### In Server Console

When a player joins:
```
╔════════════════════════════════════════════════╗
║          🎮 NEW PLAYER JOINED! 🎮              ║
╠════════════════════════════════════════════════╣
║  Player ID: 0                                  ║
║  Total Players: 1/10                           ║
║  Time: 14:30:22                                ║
╚════════════════════════════════════════════════╝

[SERVER] Assigned color to Player 0: RGB(1.00, 0.30, 0.30)
[SERVER] ✓ Player object spawned at position (0.0, 1.0, 0.0)
```

### In Client Console

When you connect:
```
[CLIENT] ✓ CONNECTED TO SERVER!
[CLIENT] Your ClientId: 0
[CLIENT] 🎮 PLAYER SPAWNED!
[CLIENT] Your player color: RGB(1.00, 0.30, 0.30)
[CLIENT] Ready to play!
```

### In Game

- Your player capsule will be colored
- Other players will see your color
- Each player has a unique color

## How It Works

The system uses **NetworkVariable** to sync colors:
1. **Server** generates random color when player spawns
2. **Server** assigns it to the player
3. **NetworkVariable** syncs the color to all clients automatically
4. **All clients** see the same color for each player

## Customization Options

### Use Specific Colors Only

Edit the color palette in PlayerAppearance:
```csharp
public Color[] colorPalette = new Color[]
{
    Color.red,
    Color.blue,
    Color.green,
    Color.yellow
};
```

### Fully Random Colors

Clear the color palette array (set size to 0) and colors will be:
- Random RGB values between 0.3 and 1.0
- Different every time
- Avoids very dark colors

### Change Player Color During Game

You can change a player's color from the server:
```csharp
// In any server-side code
PlayerAppearance appearance = playerObject.GetComponent<PlayerAppearance>();
appearance.SetColor(Color.red); // Change to red
```

## Troubleshooting

### Players are all white/default color

- Make sure **PlayerAppearance** component is on the **Capsule prefab**, not just in the scene
- Rebuild your game after adding the component
- Check that the **Player Renderer** field is assigned (or leave empty for auto-detect)

### Colors don't sync to other clients

- Verify **NetworkObject** component is on the Capsule prefab
- Make sure **PlayerAppearance** is added to the prefab, not scene instances
- Check that the prefab is in NetworkManager's prefab list

### Server console shows "Renderer not found"

- The PlayerRenderer field is not assigned
- Or the capsule doesn't have a MeshRenderer component
- Assign it manually: Drag the MeshRenderer into the Player Renderer field

### All players get the same color

- Make sure you're testing with multiple clients
- The random color is assigned once per player on spawn
- If you see this, check that each client is getting a unique ClientId

## Advanced: Team Colors

You could extend this to support teams:

```csharp
// Example: Assign colors based on team
public void AssignTeamColor(int teamId)
{
    if (IsServer)
    {
        Color teamColor = teamId == 0 ? Color.red : Color.blue;
        SetColor(teamColor);
    }
}
```

## Component Placement

The PlayerAppearance component should be:
- ✅ On the **Capsule prefab** (in Assets folder)
- ✅ Same GameObject as **NetworkObject**
- ✅ Same GameObject as **PlayerController**

**GameObject Structure:**
```
Capsule (Prefab)
├── NetworkObject
├── NetworkTransform
├── CharacterController
├── PlayerController
├── PlayerAppearance ← Add this
└── MeshRenderer (child or on same GameObject)
```

That's it! Players will now spawn with random colors. 🎨
