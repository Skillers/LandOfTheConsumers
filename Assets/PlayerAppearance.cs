using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Manages player appearance including random colors
/// Add this component to your player prefab (Capsule)
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class PlayerAppearance : NetworkBehaviour
{
    [Header("Appearance Settings")]
    [Tooltip("The renderer to apply color to (usually the MeshRenderer)")]
    public Renderer playerRenderer;

    [Tooltip("Predefined color palette (leave empty for fully random colors)")]
    public Color[] colorPalette = new Color[]
    {
        new Color(1f, 0.3f, 0.3f),      // Red
        new Color(0.3f, 0.3f, 1f),      // Blue
        new Color(0.3f, 1f, 0.3f),      // Green
        new Color(1f, 1f, 0.3f),        // Yellow
        new Color(1f, 0.3f, 1f),        // Magenta
        new Color(0.3f, 1f, 1f),        // Cyan
        new Color(1f, 0.6f, 0.3f),      // Orange
        new Color(0.6f, 0.3f, 1f),      // Purple
        new Color(1f, 0.7f, 0.8f),      // Pink
        new Color(0.5f, 1f, 0.5f)       // Light Green
    };

    // Network variable to sync color across all clients
    private NetworkVariable<Color> playerColor = new NetworkVariable<Color>(
        Color.white,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    void Awake()
    {
        // Auto-find renderer if not assigned
        if (playerRenderer == null)
        {
            playerRenderer = GetComponentInChildren<Renderer>();
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // SERVER: Assign random color when player spawns
        if (IsServer)
        {
            Color randomColor;

            if (colorPalette != null && colorPalette.Length > 0)
            {
                // Pick from palette
                randomColor = colorPalette[Random.Range(0, colorPalette.Length)];
            }
            else
            {
                // Generate fully random color
                randomColor = new Color(
                    Random.Range(0.3f, 1f),
                    Random.Range(0.3f, 1f),
                    Random.Range(0.3f, 1f)
                );
            }

            playerColor.Value = randomColor;

            Debug.Log($"[SERVER] Assigned color to Player {OwnerClientId}: RGB({randomColor.r:F2}, {randomColor.g:F2}, {randomColor.b:F2})");
        }

        // ALL CLIENTS: Subscribe to color changes
        playerColor.OnValueChanged += OnColorChanged;

        // Apply initial color
        ApplyColor(playerColor.Value);

        // Log on client side
        if (IsOwner)
        {
            Debug.Log($"[CLIENT] Your player color: RGB({playerColor.Value.r:F2}, {playerColor.Value.g:F2}, {playerColor.Value.b:F2})");
        }
    }

    /// <summary>
    /// Called when the color network variable changes
    /// </summary>
    private void OnColorChanged(Color oldColor, Color newColor)
    {
        ApplyColor(newColor);
    }

    /// <summary>
    /// Applies the color to the player's renderer
    /// </summary>
    private void ApplyColor(Color color)
    {
        if (playerRenderer != null)
        {
            // Create a new material instance to avoid changing shared material
            if (playerRenderer.material != null)
            {
                playerRenderer.material.color = color;
            }
        }
        else
        {
            Debug.LogWarning($"[PlayerAppearance] Renderer not found on player {OwnerClientId}!");
        }
    }

    public override void OnNetworkDespawn()
    {
        // Unsubscribe from color changes
        if (playerColor != null)
        {
            playerColor.OnValueChanged -= OnColorChanged;
        }

        base.OnNetworkDespawn();
    }

    /// <summary>
    /// Server can call this to change a player's color
    /// </summary>
    public void SetColor(Color newColor)
    {
        if (IsServer)
        {
            playerColor.Value = newColor;
            Debug.Log($"[SERVER] Changed Player {OwnerClientId} color to RGB({newColor.r:F2}, {newColor.g:F2}, {newColor.b:F2})");
        }
    }

    /// <summary>
    /// Get the current player color
    /// </summary>
    public Color GetColor()
    {
        return playerColor.Value;
    }
}
