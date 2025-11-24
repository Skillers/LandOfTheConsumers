using UnityEngine;
using Unity.Netcode;

public class Stick : NetworkBehaviour
{
    [Header("Outline Settings")]
    public Color hoverColor = Color.yellow;
    public Color interactColor = Color.green;
    public float outlineWidth = 7f;

    [Header("Pickup Settings")]
    public float pickupRange = 3f;

    private Outline outline;
    private CameraManager cameraManager;
    private bool isBeingInteracted = false;
    private Transform player;

    void Start()
    {
        // Ensure stick has a renderer (required for Outline to work)
        Renderer renderer = GetComponent<Renderer>();
        if (renderer == null)
        {
            Debug.LogError($"[Stick] {gameObject.name} has no Renderer! Outline won't work. Add a MeshRenderer or other renderer component.");
        }

        // Ensure stick has a collider
        if (GetComponent<Collider>() == null)
        {
            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            Debug.Log("[Stick] Added BoxCollider to stick");
        }

        // Add outline component
        outline = gameObject.AddComponent<Outline>();
        outline.OutlineColor = hoverColor;
        outline.OutlineWidth = outlineWidth;
        outline.enabled = false;

        // Find camera manager
        cameraManager = FindObjectOfType<CameraManager>();

        // Find local player (for multiplayer)
        FindLocalPlayer();

        Debug.Log($"[Stick] Initialized: {gameObject.name}, Has Renderer: {renderer != null}, Has Collider: {GetComponent<Collider>() != null}");
    }

    void FindLocalPlayer()
    {
        // First try to find via CameraManager (it tracks the local player)
        if (cameraManager != null && cameraManager.player != null)
        {
            player = cameraManager.player;
            Debug.Log($"[Stick] Found local player via CameraManager: {player.name}");
            return;
        }

        // Fallback: Find all players and check for IsOwner (multiplayer)
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (PlayerController pc in players)
        {
            if (pc.IsOwner)
            {
                player = pc.transform;
                Debug.Log($"[Stick] Found local player via IsOwner: {player.name}");
                return;
            }
        }

        // Last resort: just use tag
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            Debug.Log($"[Stick] Found player via tag: {player.name}");
        }
        else
        {
            Debug.LogWarning("[Stick] No player found!");
        }
    }

    void Update()
    {
        // Try to find player again if we don't have one yet
        if (player == null)
        {
            FindLocalPlayer();
        }

        UpdateOutlineVisibility();
    }

    void UpdateOutlineVisibility()
    {
        if (outline == null)
            return;

        bool shouldShowOutline = false;
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
            return;

        // Check which camera mode we're in
        CameraManager.CameraMode currentMode = CameraManager.CameraMode.Isometric;
        if (cameraManager != null)
        {
            currentMode = cameraManager.GetCurrentMode();
        }

        if (currentMode == CameraManager.CameraMode.ThirdPerson)
        {
            // Third person mode: raycast from screen center
            Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 100f))
            {
                // Check if we hit this stick
                if (hit.collider.gameObject == gameObject)
                {
                    shouldShowOutline = true;
                    Debug.Log($"[Stick] Third-person raycast hit stick: {gameObject.name}");

                    // Check for E key press
                    if (Input.GetKey(KeyCode.E))
                    {
                        isBeingInteracted = true;
                    }
                    else
                    {
                        isBeingInteracted = false;
                    }

                    // E key handling is now done by PlayerController's interaction system
                }
            }
            else
            {
                isBeingInteracted = false;
            }
        }
        else if (currentMode == CameraManager.CameraMode.Isometric)
        {
            // Isometric mode: raycast from mouse position
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 1000f))
            {
                // Check if we hit this stick
                if (hit.collider.gameObject == gameObject)
                {
                    shouldShowOutline = true;
                    Debug.Log($"[Stick] Isometric raycast hit stick: {gameObject.name}");

                    // Check for mouse button
                    if (Input.GetMouseButton(0)) // Left mouse button
                    {
                        isBeingInteracted = true;
                    }
                    else
                    {
                        isBeingInteracted = false;
                    }

                    // Click handling is now done by PlayerController's interaction system
                }
            }
            else
            {
                isBeingInteracted = false;
            }
        }

        // Enable or disable outline based on result
        if (outline.enabled != shouldShowOutline)
        {
            outline.enabled = shouldShowOutline;
            Debug.Log($"[Stick] Outline {(shouldShowOutline ? "ENABLED" : "DISABLED")} on {gameObject.name}");
        }

        // Update outline color based on interaction state
        if (shouldShowOutline)
        {
            if (isBeingInteracted)
            {
                outline.OutlineColor = interactColor;
            }
            else
            {
                outline.OutlineColor = hoverColor;
            }
        }
    }

    // Public method called by PlayerController's interaction system
    public void Interact(PlayerController playerController)
    {
        if (playerController == null)
        {
            Debug.LogWarning("[Stick] Cannot pick up - no player controller provided!");
            return;
        }

        // Check distance to player (safety check, PlayerController should already verify this)
        float distance = Vector3.Distance(transform.position, playerController.transform.position);

        if (distance <= pickupRange)
        {
            Debug.Log($"[Stick] Picked up by {playerController.OwnerClientId}! Distance: {distance:F2}");

            // Get the player's NetworkObject to identify them to the server
            if (playerController.TryGetComponent<NetworkObject>(out NetworkObject playerNetObj))
            {
                // Request server to process pickup with player identification
                RequestPickupServerRpc(playerNetObj.NetworkObjectId);
            }
            else
            {
                Debug.LogError("[Stick] Player doesn't have NetworkObject!");
            }
        }
        else
        {
            Debug.Log($"[Stick] Too far to pick up! Distance: {distance:F2}, Required: {pickupRange}");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void RequestPickupServerRpc(ulong playerNetworkObjectId)
    {
        // Server validates and processes the pickup
        Debug.Log($"[Stick] Server processing pickup request from player NetworkObjectId: {playerNetworkObjectId}");

        // Find the player's NetworkObject
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkObjectId, out NetworkObject playerNetObj))
        {
            PlayerController playerController = playerNetObj.GetComponent<PlayerController>();
            if (playerController != null)
            {
                // Server increments the player's stick count
                playerController.AddStick();
                Debug.Log($"[Stick] Server validated pickup for player {playerController.OwnerClientId}. New count: {playerController.GetStickCount()}");
            }
            else
            {
                Debug.LogError("[Stick] Player NetworkObject found but no PlayerController component!");
            }
        }
        else
        {
            Debug.LogError($"[Stick] Could not find player with NetworkObjectId: {playerNetworkObjectId}");
        }

        // Server destroys the stick, which will sync to all clients
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn();
            Destroy(gameObject);
        }
    }
}
