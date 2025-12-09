using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    [Header("References")]
    public CameraManager cameraManager;
    private CharacterController controller;
    
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float runSpeed = 8f;
    public float rotationSpeed = 10f;
    public float gravity = -20f;
    public float jumpHeight = 2f;
    
    [Header("Isometric Click-to-Move")]
    public LayerMask groundLayer;
    public GameObject clickMarker; // Purple sphere - shows where player clicked to move (runtime created)
    public Vector3 clickMoveTarget; // Made public so indicator can access it
    public bool isMovingToTarget = false; // Made public so indicator can access it
    private float clickMoveStopDistance = 0.5f;

    [Header("Interaction System")]
    public float defaultInteractionRange = 3f;
    private GameObject pendingInteractionTarget;
    private float pendingInteractionRange;

    [Header("Stick Counter")]
    private NetworkVariable<int> stickCount = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Vector3 velocity;
    private bool isGrounded;
    
    void Start()
    {
        controller = GetComponent<CharacterController>();

        // Create click marker if it doesn't exist
        if (clickMarker == null)
        {
            CreateClickMarker();
        }
    }

    void CreateClickMarker()
    {
        // Create a sphere as the click marker (purple, size 1)
        clickMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        clickMarker.name = "ClickMarker";

        // Remove collider so it doesn't interfere
        Destroy(clickMarker.GetComponent<Collider>());

        // Set size to 1
        clickMarker.transform.localScale = Vector3.one * 1f;

        // Create material with purple color
        Renderer renderer = clickMarker.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));

        // If URP not found, try Standard
        if (mat.shader.name == "Hidden/InternalErrorShader")
        {
            mat = new Material(Shader.Find("Standard"));
            Debug.Log("[PlayerController] Using Standard shader for click marker");
        }

        // If Standard not found, try Unlit
        if (mat.shader.name == "Hidden/InternalErrorShader")
        {
            mat = new Material(Shader.Find("Unlit/Color"));
            Debug.Log("[PlayerController] Using Unlit/Color shader for click marker");
        }

        // Purple color - RGB(128, 0, 255)
        Color purple = new Color(0.5f, 0f, 1f, 1f);

        // Set the base color property based on shader
        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", purple);
            Debug.Log("[PlayerController] Set _BaseColor to purple");
        }
        else if (mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", purple);
            Debug.Log("[PlayerController] Set _Color to purple");
        }

        mat.color = purple;

        // Make it emissive for better visibility
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", purple * 0.3f);
        }

        // Try to set metallic/smoothness if shader supports it
        if (mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", 0f);
        if (mat.HasProperty("_Glossiness"))
            mat.SetFloat("_Glossiness", 0.5f);
        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", 0.5f);

        renderer.material = mat;

        // Start hidden
        clickMarker.SetActive(false);

        Debug.Log($"[PlayerController] Created purple click marker - Shader: {mat.shader.name}, Color: {mat.color}");
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Subscribe to stick count changes to log them
        stickCount.OnValueChanged += OnStickCountChanged;

        // Only setup camera for YOUR player (the one you control)
        if (IsOwner)
        {
            if (cameraManager == null)
                cameraManager = FindObjectOfType<CameraManager>();

            if (cameraManager != null)
                cameraManager.player = transform;

            Debug.Log($"Local player spawned - ClientId: {OwnerClientId}");
        }
        else
        {
            Debug.Log($"Remote player spawned - ClientId: {OwnerClientId}");
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        stickCount.OnValueChanged -= OnStickCountChanged;
    }

    void OnStickCountChanged(int oldValue, int newValue)
    {
        Debug.Log($"[PlayerController] Player {OwnerClientId} stick count changed: {oldValue} -> {newValue}");
    }
    
    void Update()
    {
        // Debug: Check if this is running
        if (IsOwner)
        {
            Debug.Log($"Update running for owner - ClientId: {OwnerClientId}");
        }
        
        // Only process input for YOUR player
        if (!IsOwner) return;
        
        isGrounded = controller.isGrounded;
        
        // Make sure we have a camera manager
        if (cameraManager == null)
        {
            cameraManager = FindObjectOfType<CameraManager>();
            if (cameraManager != null)
            {
                cameraManager.player = transform;
                Debug.Log($"Found CameraManager for ClientId: {OwnerClientId}");
            }
        }
        
        if (cameraManager == null)
        {
            Debug.LogWarning($"CameraManager not found for ClientId: {OwnerClientId}!");
            return;
        }
        
        if (cameraManager.GetCurrentMode() == CameraManager.CameraMode.ThirdPerson)
        {
            HandleThirdPersonMovement();
        }
        else
        {
            HandleIsometricMovement();
        }
        
        ApplyGravity();
    }
    
    void HandleThirdPersonMovement()
    {
        // WASD movement only in third person
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // Debug input
        if (horizontal != 0 || vertical != 0)
        {
            Debug.Log($"ClientId {OwnerClientId} - Input detected: H={horizontal}, V={vertical}");
        }

        Vector3 cameraForward = cameraManager.GetCameraForward();
        cameraForward.y = 0;
        cameraForward.Normalize();

        Vector3 cameraRight = Quaternion.Euler(0, 90, 0) * cameraForward;

        Vector3 moveDirection = (cameraForward * vertical + cameraRight * horizontal).normalized;

        if (moveDirection.magnitude >= 0.1f)
        {
            float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;
            Vector3 movement = moveDirection * currentSpeed * Time.deltaTime;

            // Tell the server to move us
            MoveServerRpc(movement);

            Debug.Log($"ClientId {OwnerClientId} - Requesting move! Direction: {moveDirection}");

            // Rotate player to face movement direction
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);

            // Tell server to rotate us
            RotateServerRpc(targetRotation);
        }

        // Jump
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // E key interaction in third person mode
        if (Input.GetKeyDown(KeyCode.E))
        {
            TryThirdPersonInteraction();
        }
    }

    void TryThirdPersonInteraction()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;

        // Raycast from screen center
        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f))
        {
            GameObject interactTarget = null;
            float interactionRange = defaultInteractionRange;

            // Check for Stick
            Stick stick = hit.collider.GetComponent<Stick>();
            if (stick != null)
            {
                interactTarget = hit.collider.gameObject;
                interactionRange = stick.pickupRange;
            }
            // Check for EnergyBeam
            else
            {
                EnergyBeam beam = hit.collider.GetComponentInParent<EnergyBeam>();
                if (beam != null)
                {
                    interactTarget = beam.gameObject;
                    interactionRange = beam.interactionRange;
                }
            }
            // Check for Interactable tag
            if (interactTarget == null && hit.collider.CompareTag("Interactable"))
            {
                interactTarget = hit.collider.gameObject;
            }

            // Try to interact if we found something
            if (interactTarget != null)
            {
                float distanceToTarget = Vector3.Distance(transform.position, interactTarget.transform.position);
                if (distanceToTarget <= interactionRange)
                {
                    Debug.Log($"[PlayerController] Third-person interaction with {interactTarget.name}");
                    ExecuteInteraction(interactTarget);
                }
                else
                {
                    Debug.Log($"[PlayerController] Too far to interact ({distanceToTarget:F1}/{interactionRange})");
                }
            }
        }
    }
    
    void HandleIsometricMovement()
    {
        // Click-to-move only in isometric mode
        if (Input.GetMouseButtonDown(0)) // Left click down (single click, not hold)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // First check if we're clicking on an interactable object (without layer mask)
            if (Physics.Raycast(ray, out hit, 1000f))
            {
                // Check for interactable objects: Hoverable, Interactable tag, EnergyBeam, or Stick
                bool isInteractable = false;
                float interactionRange = defaultInteractionRange;
                GameObject interactTarget = null;

                // Check for Hoverable tag
                if (hit.collider.CompareTag("Hoverable"))
                {
                    isInteractable = true;
                    interactTarget = hit.collider.gameObject;
                    Debug.Log($"[PlayerController] Clicked on hoverable: {interactTarget.name}");
                }
                // Check for Interactable tag (for Sticks and other interactables)
                else if (hit.collider.CompareTag("Interactable"))
                {
                    isInteractable = true;
                    interactTarget = hit.collider.gameObject;
                    // Get range from Stick component if available
                    Stick stick = hit.collider.GetComponent<Stick>();
                    if (stick != null)
                    {
                        interactionRange = stick.pickupRange;
                    }
                    Debug.Log($"[PlayerController] Clicked on interactable: {interactTarget.name}");
                }
                // Check for EnergyBeam
                else
                {
                    EnergyBeam beam = hit.collider.GetComponentInParent<EnergyBeam>();
                    if (beam != null)
                    {
                        isInteractable = true;
                        interactTarget = beam.gameObject;
                        interactionRange = beam.interactionRange;
                        Debug.Log($"[PlayerController] Clicked on beam: {interactTarget.name}");
                    }
                }

                // Handle interactable click
                if (isInteractable && interactTarget != null)
                {
                    float distanceToTarget = Vector3.Distance(transform.position, interactTarget.transform.position);

                    if (distanceToTarget <= interactionRange)
                    {
                        // In range - interact immediately
                        Debug.Log($"[PlayerController] In range ({distanceToTarget:F1}/{interactionRange}) - interacting immediately");
                        ExecuteInteraction(interactTarget);
                    }
                    else
                    {
                        // Out of range - move toward it and queue interaction
                        Debug.Log($"[PlayerController] Out of range ({distanceToTarget:F1}/{interactionRange}) - moving to interact");
                        pendingInteractionTarget = interactTarget;
                        pendingInteractionRange = interactionRange;

                        // Set movement target to a point near the interactable (stop well inside interaction range)
                        Vector3 directionToTarget = (interactTarget.transform.position - transform.position).normalized;
                        float stopDistance = interactionRange * 0.4f; // Stop at 40% of interaction range (closer to object)
                        clickMoveTarget = interactTarget.transform.position - directionToTarget * stopDistance;
                        clickMoveTarget.y = transform.position.y; // Keep same Y level
                        isMovingToTarget = true;

                        if (clickMarker != null)
                        {
                            clickMarker.SetActive(true);
                            clickMarker.transform.position = clickMoveTarget + Vector3.up * 0.1f;
                        }
                    }
                    return; // Don't process ground movement
                }
            }

            // Not an interactable - check for ground to move to (with layer mask)
            if (Physics.Raycast(ray, out hit, 1000f, groundLayer))
            {
                // Clear any pending interaction since we're clicking elsewhere
                pendingInteractionTarget = null;

                clickMoveTarget = hit.point;
                isMovingToTarget = true;

                if (clickMarker != null)
                {
                    clickMarker.SetActive(true);
                    clickMarker.transform.position = hit.point + Vector3.up * 0.1f;
                }
            }
        }

        // Move towards click target
        if (isMovingToTarget)
        {
            MoveToClickTarget();
        }
    }

    void ExecuteInteraction(GameObject target)
    {
        if (target == null) return;

        // Try Stick interaction
        Stick stick = target.GetComponent<Stick>();
        if (stick != null)
        {
            stick.Interact(this);
            return;
        }

        // Try EnergyBeam interaction
        EnergyBeam beam = target.GetComponentInParent<EnergyBeam>();
        if (beam != null)
        {
            beam.InteractWithPlayer(this);
            return;
        }

        // Generic hoverable interaction (you can expand this)
        Debug.Log($"[PlayerController] Interacted with: {target.name}");
    }
    
    void MoveToClickTarget()
    {
        // Check if pending interaction target was destroyed (e.g., another player picked up the stick)
        if (pendingInteractionTarget == null && isMovingToTarget)
        {
            // Target gone - check if we were moving to interact with something
            // If so, just continue to destination or stop
        }

        // If we have a pending interaction, check if we're in range yet
        if (pendingInteractionTarget != null)
        {
            float distanceToInteractable = Vector3.Distance(transform.position, pendingInteractionTarget.transform.position);
            if (distanceToInteractable <= pendingInteractionRange)
            {
                // We're in range! Execute the interaction
                Debug.Log($"[PlayerController] Arrived in range - executing pending interaction with {pendingInteractionTarget.name}");
                ExecuteInteraction(pendingInteractionTarget);
                pendingInteractionTarget = null;
                isMovingToTarget = false;
                if (clickMarker != null)
                    clickMarker.SetActive(false);
                return;
            }
        }

        Vector3 direction = clickMoveTarget - transform.position;
        direction.y = 0;

        float distance = direction.magnitude;

        if (distance > clickMoveStopDistance)
        {
            direction.Normalize();

            // Use run speed if shift is held, otherwise walk
            float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;
            Vector3 movement = direction * currentSpeed * Time.deltaTime;

            // Tell the server to move us
            MoveServerRpc(movement);

            // Rotate player to face movement direction
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            RotateServerRpc(targetRotation);
        }
        else
        {
            // Reached destination
            isMovingToTarget = false;
            if (clickMarker != null)
                clickMarker.SetActive(false);

            // Execute pending interaction if we have one (fallback in case range check didn't trigger)
            if (pendingInteractionTarget != null)
            {
                float distanceToInteractable = Vector3.Distance(transform.position, pendingInteractionTarget.transform.position);
                if (distanceToInteractable <= pendingInteractionRange)
                {
                    Debug.Log($"[PlayerController] At destination - executing pending interaction with {pendingInteractionTarget.name}");
                    ExecuteInteraction(pendingInteractionTarget);
                }
                else
                {
                    Debug.Log($"[PlayerController] Reached destination but still out of range ({distanceToInteractable:F1}/{pendingInteractionRange})");
                }
                pendingInteractionTarget = null;
            }
        }
    }
    
    void ApplyGravity()
    {
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
        
        velocity.y += gravity * Time.deltaTime;
        
        // Apply gravity through ServerRpc as well
        if (IsOwner)
        {
            Vector3 gravityMovement = velocity * Time.deltaTime;
            ApplyGravityServerRpc(gravityMovement);
        }
    }
    
    [ServerRpc]
    void ApplyGravityServerRpc(Vector3 gravityMovement)
    {
        if (controller != null)
        {
            controller.Move(gravityMovement);
        }
    }
    
    // Public method to stop movement (useful for interactions, combat, etc.)
    public void StopMovement()
    {
        isMovingToTarget = false;
        if (clickMarker != null)
            clickMarker.SetActive(false);
    }
    
    // Server RPC - Client asks server to move them
    [ServerRpc]
    void MoveServerRpc(Vector3 movement)
    {
        // Server executes the movement using CharacterController
        if (controller != null)
        {
            controller.Move(movement);
        }
    }
    
    // Server RPC - Client asks server to rotate them
    [ServerRpc]
    void RotateServerRpc(Quaternion targetRotation)
    {
        // Server executes the rotation with lerp
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 0.2f);
    }

    // Public method to get stick count (can be called by anyone)
    public int GetStickCount()
    {
        return stickCount.Value;
    }

    // Server-only method to increment stick count
    public void AddStick()
    {
        if (IsServer)
        {
            stickCount.Value++;
            Debug.Log($"[PlayerController] Server incremented stick count for player {OwnerClientId} to {stickCount.Value}");
        }
        else
        {
            Debug.LogError("[PlayerController] AddStick() called on client! This should only be called on server.");
        }
    }

    // Server-only method to remove a stick
    public bool RemoveStick()
    {
        if (!IsServer)
        {
            Debug.LogError("[PlayerController] RemoveStick() called on client! This should only be called on server.");
            return false;
        }

        if (stickCount.Value < 1)
        {
            Debug.LogWarning($"[PlayerController] Cannot remove stick - player {OwnerClientId} has no sticks!");
            return false;
        }

        stickCount.Value--;
        Debug.Log($"[PlayerController] Server decremented stick count for player {OwnerClientId} to {stickCount.Value}");
        return true;
    }
}