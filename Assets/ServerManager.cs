using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class ServerManager : MonoBehaviour
{
    [Header("Server Configuration")]
    [Tooltip("Port for the server to listen on")]
    public ushort serverPort = 7777;

    [Tooltip("Maximum number of players allowed")]
    public int maxPlayers = 10;

    [Header("Auto-Start Options")]
    [Tooltip("Automatically start as server on launch (useful for headless builds)")]
    public bool autoStartServer = false;

    [Tooltip("Start as server only in headless mode")]
    public bool autoStartOnlyIfHeadless = true;

    private UnityTransport transport;

    void Start()
    {
        // Get the Unity Transport component
        transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        if (transport == null)
        {
            Debug.LogError("UnityTransport component not found on NetworkManager!");
            return;
        }

        // Check if we should auto-start
        if (autoStartServer)
        {
            bool shouldStart = !autoStartOnlyIfHeadless || IsHeadlessMode();

            if (shouldStart)
            {
                StartServer();
            }
        }
    }

    /// <summary>
    /// Starts the dedicated server
    /// </summary>
    public void StartServer()
    {
        if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
        {
            Debug.LogWarning("Server is already running!");
            return;
        }

        // Configure the transport
        ConfigureTransport();

        // Set connection approval if you want to limit players
        NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;

        // Start as server (not host - server only, no local player)
        bool success = NetworkManager.Singleton.StartServer();

        if (success)
        {
            Debug.Log($"[SERVER] Dedicated server started successfully on port {serverPort}");
            Debug.Log($"[SERVER] Max players: {maxPlayers}");
            Debug.Log($"[SERVER] Waiting for client connections...");

            // Subscribe to connection events
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
        else
        {
            Debug.LogError("[SERVER] Failed to start server!");
        }
    }

    /// <summary>
    /// Stops the dedicated server
    /// </summary>
    public void StopServer()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            Debug.Log("[SERVER] Shutting down server...");
            NetworkManager.Singleton.Shutdown();
        }
    }

    /// <summary>
    /// Configures the Unity Transport with server settings
    /// </summary>
    private void ConfigureTransport()
    {
        if (transport != null)
        {
            // Set server to listen on all network interfaces (0.0.0.0)
            transport.ConnectionData.Address = "0.0.0.0";
            transport.ConnectionData.Port = serverPort;
            transport.ConnectionData.ServerListenAddress = "0.0.0.0";

            Debug.Log($"[SERVER] Transport configured - Listening on 0.0.0.0:{serverPort}");
        }
    }

    /// <summary>
    /// Connection approval callback - controls who can join
    /// </summary>
    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        ulong clientId = request.ClientNetworkId;

        Debug.Log($"[SERVER] ========================================");
        Debug.Log($"[SERVER] CONNECTION REQUEST from ClientId: {clientId}");
        Debug.Log($"[SERVER] Current players: {NetworkManager.Singleton.ConnectedClientsList.Count}/{maxPlayers}");

        // Check if server is full
        if (NetworkManager.Singleton.ConnectedClientsList.Count >= maxPlayers)
        {
            response.Approved = false;
            response.Reason = "Server is full";
            Debug.LogWarning($"[SERVER] ❌ Connection REJECTED - Server is full");
            Debug.Log($"[SERVER] ========================================");
            return;
        }

        // Approve the connection
        response.Approved = true;
        response.CreatePlayerObject = true; // Spawn player object for this client

        Debug.Log($"[SERVER] ✓ Connection APPROVED for ClientId: {clientId}");
        Debug.Log($"[SERVER] Player object will be spawned");
        Debug.Log($"[SERVER] ========================================");
    }

    /// <summary>
    /// Called when a client connects to the server
    /// </summary>
    private void OnClientConnected(ulong clientId)
    {
        int playerCount = NetworkManager.Singleton.ConnectedClientsList.Count;

        // Print prominent message in console
        Debug.Log($"\n");
        Debug.Log($"╔════════════════════════════════════════════════╗");
        Debug.Log($"║          🎮 NEW PLAYER JOINED! 🎮              ║");
        Debug.Log($"╠════════════════════════════════════════════════╣");
        Debug.Log($"║  Player ID: {clientId,-35} ║");
        Debug.Log($"║  Total Players: {playerCount}/{maxPlayers,-28} ║");
        Debug.Log($"║  Time: {System.DateTime.Now:HH:mm:ss,-36} ║");
        Debug.Log($"╚════════════════════════════════════════════════╝");
        Debug.Log($"\n");

        // Check if player object was spawned
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            if (client.PlayerObject != null)
            {
                Debug.Log($"[SERVER] ✓ Player object spawned at position {client.PlayerObject.transform.position}");
            }
            else
            {
                Debug.LogWarning($"[SERVER] ⚠ Player object NOT spawned for ClientId {clientId}!");
            }
        }
    }

    /// <summary>
    /// Called when a client disconnects from the server
    /// </summary>
    private void OnClientDisconnected(ulong clientId)
    {
        int playerCount = NetworkManager.Singleton.ConnectedClientsList.Count;

        Debug.Log($"\n");
        Debug.Log($"╔════════════════════════════════════════════════╗");
        Debug.Log($"║          👋 PLAYER LEFT 👋                     ║");
        Debug.Log($"╠════════════════════════════════════════════════╣");
        Debug.Log($"║  Player ID: {clientId,-35} ║");
        Debug.Log($"║  Remaining Players: {playerCount}/{maxPlayers,-24} ║");
        Debug.Log($"║  Time: {System.DateTime.Now:HH:mm:ss,-36} ║");
        Debug.Log($"╚════════════════════════════════════════════════╝");
        Debug.Log($"\n");
    }

    /// <summary>
    /// Checks if the game is running in headless mode (no graphics)
    /// </summary>
    private bool IsHeadlessMode()
    {
        return SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;
    }

    /// <summary>
    /// Display server info
    /// </summary>
    void OnGUI()
    {
        // Only show GUI if we're running as server and NOT in headless mode
        if (NetworkManager.Singleton.IsServer && !IsHeadlessMode())
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label("=== DEDICATED SERVER ===");
            GUILayout.Label($"Port: {serverPort}");
            GUILayout.Label($"Connected Players: {NetworkManager.Singleton.ConnectedClientsList.Count}/{maxPlayers}");

            if (GUILayout.Button("Stop Server"))
            {
                StopServer();
            }

            GUILayout.EndArea();
        }
    }

    void OnDestroy()
    {
        // Cleanup event subscriptions
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }
}
