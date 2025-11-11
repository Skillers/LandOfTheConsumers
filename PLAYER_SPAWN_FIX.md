# Player Spawning Fix Guide

If players aren't spawning when connecting to the server, follow these steps to fix it.

## The Problem

When a client connects to the server, they should automatically get a player character spawned. If this doesn't happen, it's usually a NetworkManager configuration issue.

## Solution: Configure NetworkManager

### Step 1: Open Your Scene

Open the scene with your NetworkManager (likely `SampleScene.unity`)

### Step 2: Select NetworkManager GameObject

Find and select the GameObject that has the `NetworkManager` component.

### Step 3: Configure Player Prefab

In the **NetworkManager** component inspector, you'll see several sections:

#### Option A: Set Default Player Prefab (Recommended)

1. Find the **"Player Prefab"** field at the top of NetworkManager
2. Drag your `Capsule` prefab into this field
3. This tells NetworkManager what to spawn for each connecting player

#### Option B: Use Network Prefabs List

If the Player Prefab field doesn't work, use the prefabs list:

1. Find the **"Network Prefabs List"** section
2. Make sure your `Capsule` prefab is in this list
3. If it's not there, click the + button and add it

### Step 4: Verify Player Prefab Setup

Select your **Capsule** prefab (in Assets folder) and verify it has:

✅ **Required Components:**
- `NetworkObject` component
- `NetworkTransform` component
- `CharacterController` component
- `PlayerController` script (your custom script)

✅ **NetworkObject Settings:**
- Check the box: **"Spawn With Observer"** ✓
- Leave unchecked: "Don't Destroy With Owner"

### Step 5: Verify Connection Approval

In your `ServerManager` component settings, make sure:
- It's attached to the NetworkManager GameObject
- `autoStartServer` is set correctly for your testing method

### Step 6: Test Again

1. **Start the server:**
   - Run `StartServer.cmd`
   - Look for: `[SERVER] Waiting for client connections...`

2. **Connect a client:**
   - Run your game build
   - Enter IP: `127.0.0.1`
   - Port: `7777`
   - Click "Join Server"

3. **Check the logs:**

   **On Server console, you should see:**
   ```
   [SERVER] ========================================
   [SERVER] CONNECTION REQUEST from ClientId: 0
   [SERVER] Current players: 0/10
   [SERVER] ✓ Connection APPROVED for ClientId: 0
   [SERVER] Player object will be spawned
   [SERVER] ========================================
   [SERVER] ========================================
   [SERVER] 🎮 PLAYER CONNECTED!
   [SERVER] ClientId: 0
   [SERVER] Total Players: 1/10
   [SERVER] ========================================
   [SERVER] ✓ Player object spawned for ClientId 0 at position (0.0, 1.0, 0.0)
   ```

   **On Client console, you should see:**
   ```
   [CLIENT] ========================================
   [CLIENT] ✓ CONNECTED TO SERVER!
   [CLIENT] Your ClientId: 0
   [CLIENT] Waiting for player spawn...
   [CLIENT] ========================================
   [CLIENT] ========================================
   [CLIENT] 🎮 PLAYER SPAWNED!
   [CLIENT] GameObject: Capsule(Clone)
   [CLIENT] Position: (0.0, 1.0, 0.0)
   [CLIENT] Ready to play!
   [CLIENT] ========================================
   ```

## If It Still Doesn't Work

### Check 1: Console Errors

Look for these errors in the console:

**"Player object NOT spawned"**
- NetworkManager's Player Prefab field is not set
- Or the prefab isn't in the Network Prefabs List

**"Player spawn timeout"**
- The prefab is missing `NetworkObject` component
- Or the NetworkObject is not configured correctly

**"Failed to spawn network object"**
- The prefab is not in the Network Prefabs List
- Or you have a duplicate NetworkObject GlobalObjectIdHash conflict

### Check 2: Verify Build Settings

Make sure you're building correctly:

1. **File > Build Settings**
2. **Add your scene** to "Scenes In Build"
3. Click **Build** (not "Build and Run" for server)

### Check 3: NetworkManager Connection Approval

In the `ServerManager.cs` ApprovalCheck method, verify this line exists:
```csharp
response.CreatePlayerObject = true; // Must be true!
```

If it's `false`, change it to `true`.

### Check 4: Test in Editor First

Before building, test in the Unity Editor:

1. Start server with `StartServer.cmd`
2. Press Play in Unity Editor
3. Enter `127.0.0.1` and port `7777`
4. Click Join
5. Watch the console for spawn messages

This helps you see if it's a build issue or a configuration issue.

## Common Mistakes

❌ **Mistake 1:** Player Prefab field is empty in NetworkManager
✅ **Fix:** Drag your Capsule prefab into the Player Prefab field

❌ **Mistake 2:** Capsule prefab is missing NetworkObject component
✅ **Fix:** Select Capsule prefab, click Add Component, add NetworkObject

❌ **Mistake 3:** Server has `response.CreatePlayerObject = false`
✅ **Fix:** Change to `true` in ServerManager.cs

❌ **Mistake 4:** Multiple instances of NetworkManager in scene
✅ **Fix:** Delete duplicate NetworkManager GameObjects

❌ **Mistake 5:** Prefab is in scene but not saved as actual prefab
✅ **Fix:** Drag the configured GameObject to Assets folder to create prefab

## Visual Checklist

Here's what your NetworkManager GameObject should look like:

```
NetworkManager GameObject
├── NetworkManager component
│   ├── Player Prefab: [Capsule] ← IMPORTANT!
│   └── Network Prefabs List:
│       └── [0] Capsule ← Should be here
├── UnityTransport component
├── ServerManager component
├── CommandLineParser component
└── ClientConnectionManager component ← NEW! Add this for better logs
```

## Need More Help?

If you're still having issues:

1. **Check Unity Console** - Look for red error messages
2. **Check Player Logs** - Look in `[Build Folder]/[GameName]_Data/output_log.txt`
3. **Check Server Logs** - Look in the server window console or log file
4. **Verify Network Stats** - In Unity Editor, Window > Multiplayer > NetStats Monitor

## Quick Test Script

Add this to NetworkManager to test spawning:

```csharp
void Update()
{
    if (Input.GetKeyDown(KeyCode.F1))
    {
        Debug.Log($"=== SPAWN DEBUG ===");
        Debug.Log($"IsServer: {NetworkManager.Singleton.IsServer}");
        Debug.Log($"IsClient: {NetworkManager.Singleton.IsClient}");
        Debug.Log($"Connected Clients: {NetworkManager.Singleton.ConnectedClientsList.Count}");
        Debug.Log($"Player Prefab: {NetworkManager.Singleton.NetworkConfig.PlayerPrefab}");

        if (NetworkManager.Singleton.LocalClient != null)
        {
            Debug.Log($"Local Client PlayerObject: {NetworkManager.Singleton.LocalClient.PlayerObject}");
        }
    }
}
```

Press F1 in-game to see spawn status.
