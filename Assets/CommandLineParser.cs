using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Parses command-line arguments to automatically configure and start the server
/// This is useful for headless server builds
///
/// Example usage:
/// MyGame.exe -server -port 7777 -maxplayers 20
/// MyGame.exe -client -ip 192.168.1.100 -port 7777
/// </summary>
public class CommandLineParser : MonoBehaviour
{
    [Header("Debug")]
    public bool logArguments = true;

    void Start()
    {
        string[] args = System.Environment.GetCommandLineArgs();

        if (logArguments)
        {
            Debug.Log($"[CommandLine] Received {args.Length} arguments:");
            for (int i = 0; i < args.Length; i++)
            {
                Debug.Log($"[CommandLine] Arg[{i}]: {args[i]}");
            }
        }

        ParseArguments(args);
    }

    void ParseArguments(string[] args)
    {
        bool isServer = false;
        bool isClient = false;
        string ip = "127.0.0.1";
        ushort port = 7777;
        int maxPlayers = 10;

        // Parse arguments
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i].ToLower();

            switch (arg)
            {
                case "-server":
                case "--server":
                    isServer = true;
                    Debug.Log("[CommandLine] Server mode enabled");
                    break;

                case "-client":
                case "--client":
                    isClient = true;
                    Debug.Log("[CommandLine] Client mode enabled");
                    break;

                case "-port":
                case "--port":
                    if (i + 1 < args.Length && ushort.TryParse(args[i + 1], out ushort parsedPort))
                    {
                        port = parsedPort;
                        Debug.Log($"[CommandLine] Port set to: {port}");
                        i++; // Skip next argument
                    }
                    break;

                case "-ip":
                case "--ip":
                case "-address":
                case "--address":
                    if (i + 1 < args.Length)
                    {
                        ip = args[i + 1];
                        Debug.Log($"[CommandLine] IP address set to: {ip}");
                        i++; // Skip next argument
                    }
                    break;

                case "-maxplayers":
                case "--maxplayers":
                case "-max":
                case "--max":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int parsedMax))
                    {
                        maxPlayers = parsedMax;
                        Debug.Log($"[CommandLine] Max players set to: {maxPlayers}");
                        i++; // Skip next argument
                    }
                    break;
            }
        }

        // Execute based on parsed arguments
        if (isServer)
        {
            StartDedicatedServer(port, maxPlayers);
        }
        else if (isClient)
        {
            StartClient(ip, port);
        }
        else
        {
            Debug.Log("[CommandLine] No server/client mode specified - showing UI");
        }
    }

    void StartDedicatedServer(ushort port, int maxPlayers)
    {
        Debug.Log($"[CommandLine] Starting dedicated server on port {port} with max {maxPlayers} players...");

        ServerManager serverManager = FindObjectOfType<ServerManager>();

        if (serverManager == null)
        {
            // Add ServerManager if it doesn't exist
            GameObject networkManagerObj = NetworkManager.Singleton.gameObject;
            serverManager = networkManagerObj.AddComponent<ServerManager>();
            Debug.Log("[CommandLine] ServerManager component added to NetworkManager");
        }

        // Configure server
        serverManager.serverPort = port;
        serverManager.maxPlayers = maxPlayers;

        // Start the server
        serverManager.StartServer();

        // Hide the network UI if it exists
        SimpleNetworkUI networkUI = FindObjectOfType<SimpleNetworkUI>();
        if (networkUI != null)
        {
            networkUI.gameObject.SetActive(false);
        }
    }

    void StartClient(string ip, ushort port)
    {
        Debug.Log($"[CommandLine] Starting client - connecting to {ip}:{port}...");

        // Configure transport and connect
        var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        if (transport != null)
        {
            transport.ConnectionData.Address = ip;
            transport.ConnectionData.Port = port;

            NetworkManager.Singleton.StartClient();

            // Hide the network UI if it exists
            SimpleNetworkUI networkUI = FindObjectOfType<SimpleNetworkUI>();
            if (networkUI != null)
            {
                networkUI.gameObject.SetActive(false);
            }
        }
        else
        {
            Debug.LogError("[CommandLine] UnityTransport not found!");
        }
    }
}
