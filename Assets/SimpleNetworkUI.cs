using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;

public class SimpleNetworkUI : MonoBehaviour
{
    [Header("UI References")]
    public Button joinButton;
    public Button hostButton;

    [Header("Connection Settings")]
    public TMP_InputField ipAddressInput;
    public TMP_InputField portInput;

    [Header("Default Values")]
    public string defaultIP = "127.0.0.1";
    public ushort defaultPort = 7777;

    private UnityTransport transport;

    void Start()
    {
        // Get Unity Transport component
        transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        if (transport == null)
        {
            Debug.LogError("UnityTransport not found on NetworkManager!");
            return;
        }

        // Set default values
        if (ipAddressInput != null)
            ipAddressInput.text = defaultIP;

        if (portInput != null)
            portInput.text = defaultPort.ToString();

        // Connect buttons
        if (joinButton != null)
        {
            joinButton.onClick.AddListener(OnJoinButtonClicked);
        }

        if (hostButton != null)
        {
            hostButton.onClick.AddListener(OnHostButtonClicked);
        }
    }

    void OnJoinButtonClicked()
    {
        // Client connects to the specified IP and port
        string ip = GetIPFromInput();
        ushort port = GetPortFromInput();

        transport.ConnectionData.Address = ip;
        transport.ConnectionData.Port = port;

        NetworkManager.Singleton.StartClient();
        gameObject.SetActive(false);
        Debug.Log($"Started as Client - connecting to {ip}:{port}");
    }

    string GetIPFromInput()
    {
        if (ipAddressInput != null && !string.IsNullOrEmpty(ipAddressInput.text))
        {
            return ipAddressInput.text;
        }
        return defaultIP;
    }

    ushort GetPortFromInput()
    {
        if (portInput != null && !string.IsNullOrEmpty(portInput.text))
        {
            if (ushort.TryParse(portInput.text, out ushort port))
            {
                return port;
            }
        }
        return defaultPort;
    }

    void OnHostButtonClicked()
    {
        // Host acts as both server and client (perfect for editor testing!)
        ushort port = GetPortFromInput();

        transport.ConnectionData.Address = "127.0.0.1";
        transport.ConnectionData.Port = port;

        NetworkManager.Singleton.StartHost();
        gameObject.SetActive(false);
        Debug.Log($"Started as Host on port {port} (Server + Local Client)");
    }
}