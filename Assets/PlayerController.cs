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
    public GameObject clickMarker;
    public Vector3 clickMoveTarget; // Made public so indicator can access it
    public bool isMovingToTarget = false; // Made public so indicator can access it
    private float clickMoveStopDistance = 0.5f;
    
    private Vector3 velocity;
    private bool isGrounded;
    
    void Start()
    {
        controller = GetComponent<CharacterController>();
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
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
    }
    
    void HandleIsometricMovement()
    {
        // Click-to-move only in isometric mode
        if (Input.GetMouseButton(0)) // Left click (hold or click)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit, 1000f, groundLayer))
            {
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
    
    void MoveToClickTarget()
    {
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
}