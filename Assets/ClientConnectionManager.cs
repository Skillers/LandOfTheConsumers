using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Manages client-side connection events and logging
/// Add this to the NetworkManager GameObject
/// </summary>
public class ClientConnectionManager : MonoBehaviour
{
    void Start()
    {
        // Subscribe to network events
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        // Only log if this is OUR client connecting
        if (NetworkManager.Singleton.LocalClientId == clientId)
        {
            Debug.Log($"[CLIENT] ========================================");
            Debug.Log($"[CLIENT] ✓ CONNECTED TO SERVER!");
            Debug.Log($"[CLIENT] Your ClientId: {clientId}");
            Debug.Log($"[CLIENT] Waiting for player spawn...");
            Debug.Log($"[CLIENT] ========================================");

            // Start checking for player spawn
            StartCoroutine(CheckPlayerSpawn());
        }
        else
        {
            Debug.Log($"[CLIENT] Another player joined (ClientId: {clientId})");
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        // Check if this is our client disconnecting
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClientId == clientId)
        {
            Debug.Log($"[CLIENT] ========================================");
            Debug.Log($"[CLIENT] ❌ DISCONNECTED FROM SERVER");
            Debug.Log($"[CLIENT] ========================================");
        }
        else
        {
            Debug.Log($"[CLIENT] Another player left (ClientId: {clientId})");
        }
    }

    private System.Collections.IEnumerator CheckPlayerSpawn()
    {
        float timeout = 10f;
        float elapsed = 0f;

        Debug.Log($"[CLIENT] Checking for player spawn...");

        while (elapsed < timeout)
        {
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsConnectedClient &&
                NetworkManager.Singleton.LocalClient != null &&
                NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                var playerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
                Debug.Log($"[CLIENT] ========================================");
                Debug.Log($"[CLIENT] 🎮 PLAYER SPAWNED!");
                Debug.Log($"[CLIENT] GameObject: {playerObj.name}");
                Debug.Log($"[CLIENT] Position: {playerObj.transform.position}");
                Debug.Log($"[CLIENT] Ready to play!");
                Debug.Log($"[CLIENT] ========================================");
                yield break;
            }

            elapsed += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }

        // Timeout - player didn't spawn
        Debug.LogError($"[CLIENT] ========================================");
        Debug.LogError($"[CLIENT] ❌ PLAYER SPAWN TIMEOUT!");
        Debug.LogError($"[CLIENT] Player object was not spawned after {timeout} seconds");
        Debug.LogError($"[CLIENT] Check NetworkManager configuration:");
        Debug.LogError($"[CLIENT]   - Is 'Player Prefab' set in NetworkManager?");
        Debug.LogError($"[CLIENT]   - Is the prefab in NetworkManager's prefab list?");
        Debug.LogError($"[CLIENT] ========================================");
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
