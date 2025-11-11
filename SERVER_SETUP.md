# Dedicated Server Setup Guide

This guide will help you set up and run a dedicated server for your multiplayer game.

## Overview

The game now supports three network modes:
- **Server**: Dedicated server (headless or with graphics)
- **Host**: Combined server + client (one player acts as host)
- **Client**: Player connecting to a server or host

## Unity Editor Setup

### 1. Configure NetworkManager

1. Open your scene (e.g., `SampleScene.unity`)
2. Select the GameObject with `NetworkManager` component
3. Add the following components if not already present:
   - `ServerManager` - Manages dedicated server functionality
   - `CommandLineParser` - Parses command-line arguments for headless builds
   - `UnityTransport` - Should already be present

### 2. Configure ServerManager

Select the NetworkManager GameObject and configure ServerManager:
- **Server Port**: `7777` (default, can be changed)
- **Max Players**: `10` (adjust as needed)
- **Auto Start Server**: `false` (for development, `true` for headless builds)
- **Auto Start Only If Headless**: `true` (recommended)

### 3. Update Network UI (Client Join Screen)

The UI is client-only - players use it to join servers (server is started via batch files or command line).

1. Find your UI Canvas with `SimpleNetworkUI` component
2. Create the following UI elements:

   **IP Address Input Field:**
   - Create a TextMeshPro InputField
   - Name it "IPAddressInput"
   - Set placeholder text: "127.0.0.1"

   **Port Input Field:**
   - Create a TextMeshPro InputField
   - Name it "PortInput"
   - Set placeholder text: "7777"

   **Join Button:**
   - Create a Button (or keep your existing one)
   - Name it "JoinButton"
   - Set button text: "Join Server"

3. Wire up the UI references in `SimpleNetworkUI`:
   - Drag IPAddressInput to `ipAddressInput` field
   - Drag PortInput to `portInput` field
   - Drag JoinButton to `joinButton` field

### 4. Configure UnityTransport

Select NetworkManager and configure UnityTransport:
- **Protocol Type**: `UnityTransport`
- **Connection Data**:
  - Address: `127.0.0.1` (default, will be overridden)
  - Port: `7777`
  - Server Listen Address: `0.0.0.0`

## Testing Locally

### Option 1: Command Line Server + Standalone Build

1. **Build the game:**
   - File > Build Settings
   - Add your scene
   - Build to a folder (e.g., `Builds/Client`)

2. **Start server via command line:**
   - Copy `QuickStart_Server.cmd` to your build folder
   - Double-click to start server on port 7777
   - Or run: `LandOfTheConsumers.exe -server -port 7777`

3. **Connect with standalone build:**
   - Run the built executable again
   - Enter IP: `127.0.0.1`
   - Enter Port: `7777`
   - Click "Join Server"

### Option 2: Using Batch Files (Easiest)

1. **Build the game** (as above)

2. **Copy batch files to build folder:**
   - Copy `TestLocal.cmd` to your build folder

3. **Double-click TestLocal.cmd:**
   - Automatically opens server window
   - Automatically opens client window
   - Client auto-connects to server
   - Start playing!

## Building a Headless Server

### Windows Headless Build

1. **File > Build Settings**
2. Select **Windows, Mac, Linux**
3. Set **Target Platform**: Windows
4. Check **Server Build** (this creates a headless build)
5. Set **Architecture**: x86_64
6. Click **Build** and save to `Builds/Server`

### Linux Headless Build (Recommended for servers)

1. **File > Build Settings**
2. Select **Windows, Mac, Linux**
3. Set **Target Platform**: Linux
4. Check **Server Build**
5. Set **Architecture**: x86_64
6. Click **Build** and save to `Builds/LinuxServer`

## Running the Headless Server

### Command-Line Arguments

The server supports the following command-line arguments:

```bash
# Start as dedicated server
-server               # Enable server mode
-port [number]        # Set port (default: 7777)
-maxplayers [number]  # Set max players (default: 10)

# Start as client
-client               # Enable client mode
-ip [address]         # Server IP to connect to
-port [number]        # Server port

# Debug
-logFile [path]       # Set log file location (Unity default)
```

### Windows Server Examples

```powershell
# Start server on default port 7777 with 10 players
.\LandOfTheConsumers.exe -server

# Start server on custom port with 20 players
.\LandOfTheConsumers.exe -server -port 8888 -maxplayers 20

# Start server with logging
.\LandOfTheConsumers.exe -server -port 7777 -logFile "server.log"
```

### Linux Server Examples

```bash
# Make executable (first time only)
chmod +x LandOfTheConsumers.x86_64

# Start server on default port
./LandOfTheConsumers.x86_64 -server

# Start server on custom port with 20 players
./LandOfTheConsumers.x86_64 -server -port 8888 -maxplayers 20

# Start server in background with nohup
nohup ./LandOfTheConsumers.x86_64 -server -port 7777 &
```

### Auto-Start Server

To have the server start automatically when launched:

1. Set `ServerManager.autoStartServer = true`
2. Build as server build
3. Run without arguments - will auto-start if headless

## Network Configuration

### Port Forwarding (For Internet Play)

If hosting from home, you need to forward the server port:

1. **Find your internal IP:**
   - Windows: `ipconfig` (look for IPv4 Address)
   - Linux: `ip addr` or `ifconfig`

2. **Access your router:**
   - Usually at `192.168.1.1` or `192.168.0.1`
   - Login to admin panel

3. **Forward the port:**
   - Protocol: UDP
   - External Port: 7777 (or your chosen port)
   - Internal IP: Your server's local IP
   - Internal Port: 7777

4. **Find your public IP:**
   - Visit https://whatismyipaddress.com
   - Give this IP to clients

### Firewall Configuration

**Windows:**
```powershell
# Allow incoming connections (run as Administrator)
netsh advfirewall firewall add rule name="Game Server" dir=in action=allow protocol=UDP localport=7777
```

**Linux (UFW):**
```bash
sudo ufw allow 7777/udp
```

## Testing the Server

### 1. Start the Server

```bash
# Windows
.\LandOfTheConsumers.exe -server -port 7777

# Linux
./LandOfTheConsumers.x86_64 -server -port 7777
```

Watch for these log messages:
```
[SERVER] Transport configured - Listening on 0.0.0.0:7777
[SERVER] Dedicated server started successfully on port 7777
[SERVER] Max players: 10
[SERVER] Waiting for client connections...
```

### 2. Connect as Client

**From Unity Editor (for testing):**
1. Press Play
2. Enter IP: `127.0.0.1` (or server IP)
3. Enter Port: `7777`
4. Click "Join Server"

**From Build (via UI):**
1. Run the executable
2. Enter IP and Port in the UI
3. Click "Join Server"

**From Build (via command line):**
```bash
# Auto-connect via command line
.\LandOfTheConsumers.exe -client -ip 127.0.0.1 -port 7777
```

### 3. Verify Connection

Server logs should show:
```
[SERVER] Connection approved for client
[SERVER] Client 1 connected! (1/10 players)
```

Client logs should show:
```
Started as Client - connecting to 127.0.0.1:7777
Local player spawned - ClientId: 1
```

## Troubleshooting

### "Failed to start server"
- Check if port is already in use
- Ensure UnityTransport component is on NetworkManager
- Check firewall settings

### "Connection timeout"
- Verify server is running
- Check IP address is correct
- Ensure port is forwarded (if over internet)
- Verify firewall allows UDP traffic on the port

### "Server is full"
- Increase `maxPlayers` in ServerManager
- Or disconnect some clients

### Players can't see each other
- Verify player prefab has `NetworkObject` component
- Check that player prefab is registered in NetworkManager's prefab list
- Ensure `NetworkTransform` is on the player prefab

## Performance Optimization

### Server Build Optimization

For best performance on dedicated servers:

1. **Player Settings (Edit > Project Settings > Player):**
   - Run In Background: `true`
   - Display Resolution Dialog: `Disabled`
   - Resizable Window: `false`

2. **Quality Settings (Edit > Project Settings > Quality):**
   - Create a "Server" quality level
   - Disable all graphics features
   - Set as default for Server platform

3. **Remove unnecessary components:**
   - Camera rendering for server-only builds
   - Audio listeners
   - UI elements

## Deployment to Cloud

### Basic DigitalOcean Deployment

```bash
# 1. Create a droplet (Ubuntu 22.04)
# 2. Upload your Linux build via SCP
scp -r Builds/LinuxServer/* user@your-server-ip:/home/user/gameserver/

# 3. SSH into server
ssh user@your-server-ip

# 4. Install dependencies (if needed)
sudo apt-get update
sudo apt-get install -y libgl1-mesa-glx

# 5. Run the server
cd /home/user/gameserver
chmod +x LandOfTheConsumers.x86_64
./LandOfTheConsumers.x86_64 -server -port 7777
```

### Using Screen/Tmux (Keep server running after disconnect)

```bash
# Install screen
sudo apt-get install screen

# Start server in screen session
screen -S gameserver
./LandOfTheConsumers.x86_64 -server -port 7777

# Detach: Press Ctrl+A, then D
# Reattach: screen -r gameserver
```

## Next Steps

Consider implementing:
- [ ] Authentication system
- [ ] Player save/load from database
- [ ] Server browser / matchmaking
- [ ] Admin commands (kick, ban, etc.)
- [ ] Server monitoring dashboard
- [ ] Automatic server restart on crash
- [ ] Log rotation and management
