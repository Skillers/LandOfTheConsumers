using UnityEngine;

public class CameraManager : MonoBehaviour
{
    public enum CameraMode { ThirdPerson, Isometric }
    
    [Header("Camera References")]
    public Camera mainCamera;
    public Transform player;
    public CameraCrosshairIndicator crosshairIndicator;
    
    [Header("Third Person Settings")]
    public float thirdPersonDistance = 5f;
    public float thirdPersonHeight = 1.5f;
    public float shoulderOffset = 0.5f; // How far to the side
    public float mouseSensitivity = 3f;
    public float minVerticalAngle = -30f;
    public float maxVerticalAngle = 60f;
    
    [Header("Isometric Settings")]
    public float isometricAngle = 45f; // Vertical tilt angle
    public float isometricRotation = 45f; // Horizontal rotation (0=north, 45=northeast, 90=east)
    public float isometricDistance = 20f; // Distance from player (higher = further back)
    public float isometricHeight = 15f; // Height above player
    public float isometricLookAheadDistance = 3f; // How far ahead to look based on movement
    public float isometricLookAheadSpeed = 3f; // How fast camera pans towards movement
    
    [Header("Transition")]
    public float transitionSpeed = 5f; // Speed when switching between perspectives
    public float shoulderSwitchSpeed = 8f; // Speed when switching shoulders with Q
    
    private CameraMode currentMode = CameraMode.ThirdPerson;
    private float rotationX = 0f;
    private float rotationY = 0f;
    private bool isRightShoulder = true; // true = right shoulder, false = left shoulder
    private float currentShoulderOffset = 0.5f;
    private Vector3 isometricLookAheadOffset = Vector3.zero; // Smooth camera pan for movement
    private Vector3 lastPlayerPosition; // Track player movement
    
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    
    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    
        // Only lock cursor if this is the local player's camera
        if (player != null)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    
        InitializeThirdPerson();
        lastPlayerPosition = player.position;
    }
    
    void Update()
    {
        // Toggle camera mode with Tab key
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleCameraMode();
        }
        
        // Switch shoulder with Q key (only in third person)
        if (Input.GetKeyDown(KeyCode.Q) && currentMode == CameraMode.ThirdPerson)
        {
            SwitchShoulder();
        }
        
        // Update camera based on current mode
        if (currentMode == CameraMode.ThirdPerson)
        {
            UpdateThirdPersonCamera();
        }
        else
        {
            UpdateIsometricCamera();
        }
        
        // Apply camera position and rotation
        if (currentMode == CameraMode.ThirdPerson)
        {
            // Instant follow for third person
            mainCamera.transform.position = targetPosition;
            mainCamera.transform.rotation = targetRotation;
        }
        else
        {
            // Smooth transition for isometric mode
            mainCamera.transform.position = Vector3.Lerp(
                mainCamera.transform.position,
                targetPosition,
                Time.deltaTime * transitionSpeed
            );
            mainCamera.transform.rotation = Quaternion.Slerp(
                mainCamera.transform.rotation,
                targetRotation,
                Time.deltaTime * transitionSpeed
            );
        }
    }
    
    void ToggleCameraMode()
    {
        currentMode = (currentMode == CameraMode.ThirdPerson) 
            ? CameraMode.Isometric 
            : CameraMode.ThirdPerson;
            
        if (currentMode == CameraMode.ThirdPerson)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
    
    void InitializeThirdPerson()
    {
        rotationY = player.eulerAngles.y;
        rotationX = 20f;
        currentShoulderOffset = shoulderOffset;
    }
    
    void SwitchShoulder()
    {
        isRightShoulder = !isRightShoulder;
    }
    
    void UpdateThirdPersonCamera()
    {
        // Mouse look
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        
        rotationY += mouseX;
        rotationX -= mouseY;
        rotationX = Mathf.Clamp(rotationX, minVerticalAngle, maxVerticalAngle);
        
        // Smoothly transition shoulder offset
        float targetOffset = isRightShoulder ? shoulderOffset : -shoulderOffset;
        currentShoulderOffset = Mathf.Lerp(currentShoulderOffset, targetOffset, Time.deltaTime * shoulderSwitchSpeed);
        
        // Calculate camera position with shoulder offset
        Quaternion rotation = Quaternion.Euler(rotationX, rotationY, 0);
        
        // Create offset: back from player + up + to the side (shoulder)
        Vector3 offset = new Vector3(currentShoulderOffset, 0, -thirdPersonDistance);
        Vector3 rotatedOffset = rotation * offset;
        
        targetPosition = player.position + new Vector3(0, thirdPersonHeight, 0) + rotatedOffset;
        targetRotation = rotation;
    }
    
    void UpdateIsometricCamera()
    {
        // Get the red indicator position (in front of player)
        Vector3 lookAtPosition = player.position;

        if (crosshairIndicator != null && crosshairIndicator.IsIndicatorActive())
        {
            // Use the red indicator position as the look-at target
            lookAtPosition = crosshairIndicator.GetIndicatorPosition();
        }

        // Calculate camera rotation
        Quaternion rotation = Quaternion.Euler(isometricAngle, isometricRotation, 0);

        // Calculate direction from rotation
        Vector3 backward = rotation * Vector3.back;

        // Position camera behind the look-at point
        Vector3 cameraPos = lookAtPosition + backward * isometricDistance;

        targetPosition = cameraPos;
        targetRotation = rotation;
    }
    
    public CameraMode GetCurrentMode()
    {
        return currentMode;
    }
    
    public Vector3 GetCameraForward()
    {
        if (currentMode == CameraMode.ThirdPerson)
        {
            return Quaternion.Euler(0, rotationY, 0) * Vector3.forward;
        }
        else
        {
            // In isometric, forward is based on screen space
            return new Vector3(1, 0, 1).normalized;
        }
    }
}