using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class SimpleNetworkUI : MonoBehaviour
{
    [Header("UI References")]
    public Button hostButton;
    public Button joinButton;
    
    void Start()
    {
        // Connect Host button
        hostButton.onClick.AddListener(() => {
            NetworkManager.Singleton.StartHost();
            gameObject.SetActive(false); // Hide UI after connecting
            Debug.Log("Started as Host - waiting for clients to join");
        });
        
        // Connect Join button
        joinButton.onClick.AddListener(() => {
            NetworkManager.Singleton.StartClient();
            gameObject.SetActive(false); // Hide UI after connecting
            Debug.Log("Started as Client - attempting to connect to host");
        });
    }
}