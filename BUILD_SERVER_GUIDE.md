# How to Build a Headless Dedicated Server

This guide shows you how to create a server build that runs in CMD only (no game window).

## Step-by-Step: Build Headless Server

### 1. Open Build Settings

In Unity:
- Go to **File > Build Settings**

### 2. Configure Platform

1. Select **"Dedicated Server"** in the platform list (left side)
   - If you don't see "Dedicated Server", select **"Windows, Mac, Linux"** instead

2. Click **"Switch Platform"** if needed (wait for it to finish)

### 3. Configure Server Build

**If using "Dedicated Server" platform:**
- Just click **Build**
- Choose output folder: `Builds/Server/`
- Name it: `PerspecitveController.exe`

**If using "Windows, Mac, Linux" platform:**
1. Set **Target Platform**: `Windows`
2. Set **Architecture**: `x86_64`
3. **Check this box:** ☑ **Server Build** (IMPORTANT!)
4. Click **Build**
5. Choose output folder: `Builds/Server/`
6. Name it: `PerspecitveController.exe`

### 4. Build Regular Client (For Players)

After building the server, build a regular client:

1. Go back to **File > Build Settings**
2. Select **"Windows, Mac, Linux"**
3. **Uncheck:** ☐ Server Build
4. Click **Build**
5. Choose output folder: `Builds/Client/`
6. Name it: `PerspecitveController.exe`

## What's the Difference?

| Build Type | Window? | Graphics? | For |
|------------|---------|-----------|-----|
| **Server Build** | ❌ No window | ❌ No graphics | Running dedicated servers |
| **Regular Build** | ✅ Has window | ✅ Has graphics | Players connecting to server |

## Update Your StartServer.cmd

After building the server, update the path in `StartServer.cmd`:

```batch
Builds\Server\PerspecitveController.exe -server -port %SERVER_PORT% -maxplayers %MAX_PLAYERS% -logFile "%LOG_FILE%"
```

## Testing

### Test 1: Start Headless Server
1. Double-click `StartServer.cmd`
2. Should see CMD window only (no game window)
3. Console shows server logs

### Test 2: Connect Client
1. Run `Builds\Client\PerspecitveController.exe`
2. Enter IP: `127.0.0.1`
3. Port: `7777`
4. Click "Join Server"
5. Client should connect and spawn player

## If You Still See a Window

### Option A: Use -batchmode

If you built a regular build by mistake, you can force headless mode:

Edit `StartServer.cmd`:
```batch
Builds\PerspecitveController.exe -batchmode -nographics -server -port %SERVER_PORT% -maxplayers %MAX_PLAYERS% -logFile "%LOG_FILE%"
```

**Arguments:**
- `-batchmode` - Run without UI
- `-nographics` - Don't initialize graphics

**Warning:** This is less efficient than a proper Server Build.

### Option B: Rebuild as Server Build

Go back to Unity Build Settings and check the **Server Build** checkbox.

## Auto-Start Server on Launch

If you want the server to start automatically without the UI:

1. In Unity, select **NetworkManager GameObject**
2. Find **ServerManager** component
3. Set:
   - `autoStartServer`: ✓ **true**
   - `autoStartOnlyIfHeadless`: ✓ **true**

This way the server will auto-start when running a server build, but still show UI for regular builds.

## Folder Structure After Building

Your `Builds` folder should look like:

```
Builds/
├── Server/                        ← Headless server build
│   ├── PerspecitveController.exe  (no window)
│   └── PerspecitveController_Data/
│
└── Client/                        ← Regular client build
    ├── PerspecitveController.exe  (with window)
    └── PerspecitveController_Data/
```

## Quick Build Checklist

**For Server:**
- [ ] File > Build Settings
- [ ] Select "Dedicated Server" OR "Windows, Mac, Linux"
- [ ] Check ☑ "Server Build" (if using Windows/Mac/Linux)
- [ ] Build to `Builds/Server/`
- [ ] Test: Should run in CMD only

**For Client:**
- [ ] File > Build Settings
- [ ] Select "Windows, Mac, Linux"
- [ ] Uncheck ☐ "Server Build"
- [ ] Build to `Builds/Client/`
- [ ] Test: Should have game window and UI

## Common Issues

### "I don't see the Server Build checkbox"

- Make sure you're on **Windows, Mac, Linux** platform
- Click "Switch Platform" if needed
- The checkbox appears below the Architecture dropdown

### "Server still shows a window"

- You probably forgot to check "Server Build"
- Or you're running the Client build instead of Server build
- Rebuild with Server Build checked

### "Server crashes immediately"

- Use `-logFile "server_log.txt"` to see errors
- Make sure ServerManager has `autoStartServer = true`
- Or use command-line args: `-server -port 7777`

### "Can't find Server Build option in Unity"

Your Unity version might use different names:
- Unity 2021+: Look for "Dedicated Server" platform
- Unity 2020 or older: Use "Windows, Mac, Linux" with "Server Build" checkbox

If you still can't find it, use `-batchmode -nographics` flags as a workaround.
