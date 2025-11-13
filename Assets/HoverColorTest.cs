using UnityEngine;

/// <summary>
/// Simple hover test script that changes the color of objects with the "Hoverable" tag.
/// Supports both third-person (screen center) and isometric (mouse) camera modes.
/// </summary>
public class HoverColorTest : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Color hoverColor = Color.green;
    [SerializeField] private Color interactColor = Color.blue;
    [SerializeField] private float raycastDistance = 100f;

    private Camera mainCamera;
    private GameObject currentHoveredObject;
    private Material currentObjectMaterial;
    private Color originalColor;
    private bool isHovering = false;

    private void Start()
    {
        mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError("[HoverTest] No main camera found!");
        }
    }

    private void Update()
    {
        // Determine which camera mode we're in by checking if Tab key toggles it
        // For now, we'll support both modes simultaneously

        bool hitSomething = false;
        GameObject hitObject = null;

        // Try raycast from screen center (third-person mode)
        Ray centerRay = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit centerHit;
        if (Physics.Raycast(centerRay, out centerHit, raycastDistance))
        {
            if (centerHit.collider.CompareTag("Hoverable"))
            {
                hitSomething = true;
                hitObject = centerHit.collider.gameObject;
                Debug.Log($"[HoverTest] Screen center hit: {hitObject.name}");
            }
        }

        // If nothing hit from center, try mouse position (isometric mode)
        if (!hitSomething)
        {
            Ray mouseRay = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit mouseHit;
            if (Physics.Raycast(mouseRay, out mouseHit, raycastDistance * 10f))
            {
                if (mouseHit.collider.CompareTag("Hoverable"))
                {
                    hitSomething = true;
                    hitObject = mouseHit.collider.gameObject;
                    Debug.Log($"[HoverTest] Mouse hit: {hitObject.name}");
                }
            }
        }

        // Handle hover state changes
        if (hitSomething && hitObject != null)
        {
            // New object hovered
            if (currentHoveredObject != hitObject)
            {
                // Restore previous object
                RestorePreviousObject();

                // Set new object
                currentHoveredObject = hitObject;
                Renderer renderer = hitObject.GetComponent<Renderer>();

                if (renderer != null && renderer.material != null)
                {
                    currentObjectMaterial = renderer.material;
                    originalColor = currentObjectMaterial.color;
                    isHovering = true;

                    Debug.Log($"[HoverTest] Started hovering: {hitObject.name}, Original color: {originalColor}");
                }
            }

            // Update color based on interaction
            if (isHovering && currentObjectMaterial != null)
            {
                // Check for interaction input (E key or left mouse button)
                if (Input.GetKey(KeyCode.E) || Input.GetMouseButton(0))
                {
                    currentObjectMaterial.color = interactColor;
                    Debug.Log($"[HoverTest] Interacting with: {hitObject.name}");
                }
                else
                {
                    currentObjectMaterial.color = hoverColor;
                }
            }
        }
        else
        {
            // Nothing hovered, restore previous object
            RestorePreviousObject();
        }
    }

    private void RestorePreviousObject()
    {
        if (isHovering && currentHoveredObject != null && currentObjectMaterial != null)
        {
            currentObjectMaterial.color = originalColor;
            Debug.Log($"[HoverTest] Stopped hovering: {currentHoveredObject.name}, Restored color: {originalColor}");

            currentHoveredObject = null;
            currentObjectMaterial = null;
            isHovering = false;
        }
    }

    private void OnDisable()
    {
        // Make sure to restore colors when disabled
        RestorePreviousObject();
    }
}
