using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StickCounterUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI stickCountText;

    private PlayerController localPlayer;
    private int lastKnownCount = -1;

    void Start()
    {
        if (stickCountText == null)
        {
            Debug.LogError("[StickCounterUI] Stick count text not assigned!");
        }
    }

    void Update()
    {
        // Try to find local player if we don't have one
        if (localPlayer == null)
        {
            FindLocalPlayer();
        }

        // Update UI with current stick count
        if (localPlayer != null)
        {
            int currentCount = localPlayer.GetStickCount();

            // Only update text if count changed (optimization)
            if (currentCount != lastKnownCount)
            {
                UpdateUI(currentCount);
                lastKnownCount = currentCount;
            }
        }
        else
        {
            // Show waiting message if no player found yet
            if (stickCountText != null && lastKnownCount != -1)
            {
                stickCountText.text = "Sticks: --";
                lastKnownCount = -1;
            }
        }
    }

    void FindLocalPlayer()
    {
        // Find all players and get the one that is owned by this client
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (PlayerController player in players)
        {
            if (player.IsOwner)
            {
                localPlayer = player;
                Debug.Log("[StickCounterUI] Found local player!");
                return;
            }
        }
    }

    void UpdateUI(int count)
    {
        if (stickCountText != null)
        {
            stickCountText.text = $"Sticks: {count}";
        }
    }
}
