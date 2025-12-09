using UnityEngine;
using Unity.Netcode;

public class EnergyBeam : NetworkBehaviour
{
    [Header("Beam Settings")]
    public float beamHeight = 100f;
    public float beamRadius = 2f;
    public Color beamColorStart = new Color(0.15f, 0.03f, 0.25f, 0.4f);
    public Color beamColorEnd = new Color(0.25f, 0.05f, 0.35f, 0.4f);
    public float colorCycleSpeed = 0.5f;
    public int segments = 32;

    [Header("Beam Scale (Read Only)")]
    [SerializeField] private float currentBeamScaleXZ = 1f;

    [Header("Animation")]
    public bool animate = true;
    public float pulseSpeed = 2f;
    public float rotationSpeed = 30f;
    public float scrollSpeed = 2f;
    public float minPulseScale = 0.8f;
    public float maxPulseScale = 1.2f;

    [Header("Interaction")]
    public float interactionRange = 3f;
    public LayerMask beamLayer;

    [Header("Energy System")]
    public float maxEnergy = 100f;
    public float startingEnergy = 10f;
    public float energyDecayRate = 1f / 30f; // 1 energy per 30 seconds
    public float stickEnergyValue = 0.25f;
    private NetworkVariable<float> currentEnergy = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Particles")]
    public bool enableParticles = true;
    public int particleEmissionRate = 4;
    public float particleSpeed = 15f;
    public float particleSize = 0.15f;
    public float particleBeamLengthMin = 0.1f;
    public float particleBeamLengthMax = 0.4f;
    public float minLifetime = 2f;
    public float maxLifetime = 5f;
    public float minSpawnHeight = 0f;
    public float maxSpawnHeight = 20f;

    [Header("Material (Optional)")]
    [SerializeField] private Material customBeamMaterial;

    private GameObject beamMesh;
    private GameObject beamColliderObj;
    private Material beamMaterial;
    private float timeOffset;
    private Collider beamCollider;
    private ParticleSystem particles;
    private Outline outline;
    private CameraManager cameraManager;
    private bool isBeingInteracted = false;
    private bool wasHoveringLastFrame = false;
    private Color originalBeamColor;

    // Track hoverable objects
    private GameObject currentHoveredObject;
    private Material currentHoveredMaterial;
    private Color originalHoverableColor;

    // Public property to get/set beam size at runtime
    public float BeamSize
    {
        get { return beamRadius; }
        set
        {
            beamRadius = value;
            UpdateBeamSize();
        }
    }

    // Public property to view the current beam scale (X and Z)
    public float CurrentBeamScale
    {
        get
        {
            return currentBeamScaleXZ;
        }
    }

    void Awake()
    {
        // Ensure this GameObject has a NetworkObject component
        if (GetComponent<NetworkObject>() == null)
        {
            NetworkObject netObj = gameObject.AddComponent<NetworkObject>();
            Debug.Log("[EnergyBeam] Added NetworkObject component to beam");
        }
    }

    void Start()
    {
        CreateBeam();
        AddCollider();
        CreateParticles();

        // Find camera manager
        cameraManager = FindObjectOfType<CameraManager>();

        // Disable outline by default
        if (outline != null)
        {
            outline.enabled = false;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Initialize energy on server
        if (IsServer)
        {
            currentEnergy.Value = startingEnergy;
            Debug.Log($"[EnergyBeam] Server initialized energy to {startingEnergy}");
        }
    }


    

    void AddCollider()
    {
        MeshCollider meshCollider = beamMesh.AddComponent<MeshCollider>();
        meshCollider.convex = false;
        meshCollider.sharedMesh = beamMesh.GetComponent<MeshFilter>().mesh;
        beamCollider = meshCollider;

        beamMesh.layer = LayerMask.NameToLayer("Default");
        beamMesh.tag = "EnergyBeam";
    }

    void CreateParticles()
    {
        if (!enableParticles)
            return;

        GameObject particleObj = new GameObject("BeamParticles");
        particleObj.transform.parent = transform;
        particleObj.transform.localPosition = new Vector3(0, minSpawnHeight, 0);

        particles = particleObj.AddComponent<ParticleSystem>();

        var main = particles.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(minLifetime, maxLifetime);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(particleSize * 0.2f, particleSize * 0.6f);

        Gradient colorGradient = new Gradient();
        colorGradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.2f, 0.05f, 0.3f), 0f),
                new GradientColorKey(new Color(0.3f, 0.08f, 0.4f), 0.33f),
                new GradientColorKey(new Color(0.4f, 0.1f, 0.5f), 0.66f),
                new GradientColorKey(new Color(0.25f, 0.08f, 0.35f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.8f, 0f),
                new GradientAlphaKey(0.8f, 1f)
            }
        );
        main.startColor = new ParticleSystem.MinMaxGradient(colorGradient);

        main.maxParticles = 200;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;

        var emission = particles.emission;
        emission.rateOverTime = particleEmissionRate;

        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        float spawnRadius = beamRadius * 0.4f;
        shape.scale = new Vector3(spawnRadius * 2f, maxSpawnHeight - minSpawnHeight, spawnRadius * 2f);
        shape.position = new Vector3(0, 0, 0);
        shape.rotation = new Vector3(0, 0, 0);

        var velocityOverLifetime = particles.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        velocityOverLifetime.x = 0f;
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(particleSpeed);
        velocityOverLifetime.z = 0f;

        var colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient lifetimeGradient = new Gradient();
        lifetimeGradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 0.5f),
                new GradientColorKey(new Color(0.9f, 0.7f, 1f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.8f, 0f),
                new GradientAlphaKey(0.6f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = lifetimeGradient;

        var sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(0.5f, 0.8f);
        sizeCurve.AddKey(1f, 0.3f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = particleBeamLengthMax;
        renderer.velocityScale = 0.3f;
        renderer.cameraVelocityScale = 0f;
        renderer.normalDirection = 1f;

        Material particleMat = new Material(Shader.Find("Particles/Standard Unlit"));
        particleMat.SetColor("_Color", Color.white);
        particleMat.EnableKeyword("_ALPHABLEND_ON");
        renderer.material = particleMat;

        particles.Play();

        Debug.Log("Beam particles created!");
    }

    void CreateBeam()
    {
        beamMesh = new GameObject("BeamMesh");
        beamMesh.transform.parent = transform;
        beamMesh.transform.localPosition = Vector3.zero;

        MeshFilter meshFilter = beamMesh.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = beamMesh.AddComponent<MeshRenderer>();

        Mesh cylinderMesh = CreateCylinderMesh();
        meshFilter.mesh = cylinderMesh;

        // Use custom material if assigned, otherwise create one from scratch
        if (customBeamMaterial != null)
        {
            // Create an instance of the custom material
            beamMaterial = Instantiate(customBeamMaterial);
            beamMaterial.name = customBeamMaterial.name + " (Instance)";
            Debug.Log("Using custom beam material: " + customBeamMaterial.name);
        }
        else
        {
            // Create default material programmatically
            beamMaterial = new Material(Shader.Find("Standard"));
            beamMaterial.color = beamColorStart;

            beamMaterial.SetFloat("_Mode", 3);
            beamMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            beamMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            beamMaterial.SetInt("_ZWrite", 0);
            beamMaterial.DisableKeyword("_ALPHATEST_ON");
            beamMaterial.EnableKeyword("_ALPHABLEND_ON");
            beamMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            beamMaterial.renderQueue = 3000;

            beamMaterial.EnableKeyword("_EMISSION");
            beamMaterial.SetColor("_EmissionColor", beamColorStart * 2f);

            Debug.Log("Beam material created programmatically");
        }

        meshRenderer.material = beamMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Add outline to beam
        outline = beamMesh.AddComponent<Outline>();
        outline.OutlineColor = Color.red;
        outline.OutlineWidth = 100f;

        timeOffset = Random.Range(0f, 100f);
    }

    Mesh CreateCylinderMesh()
    {
        Mesh mesh = new Mesh();

        int vertexCount = (segments + 1) * 2;
        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        Color[] colors = new Color[vertexCount];
        int[] triangles = new int[segments * 6];

        float angleStep = 360f / segments;

        for (int i = 0; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            float x = Mathf.Cos(angle) * beamRadius;
            float z = Mathf.Sin(angle) * beamRadius;

            vertices[i * 2] = new Vector3(x, 0, z);
            uvs[i * 2] = new Vector2((float)i / segments, 0);
            colors[i * 2] = Color.white;

            vertices[i * 2 + 1] = new Vector3(x, beamHeight, z);
            uvs[i * 2 + 1] = new Vector2((float)i / segments, 1);
            colors[i * 2 + 1] = Color.white;
        }

        int triIndex = 0;
        for (int i = 0; i < segments; i++)
        {
            int bottomLeft = i * 2;
            int bottomRight = (i + 1) * 2;
            int topLeft = i * 2 + 1;
            int topRight = (i + 1) * 2 + 1;

            triangles[triIndex++] = bottomLeft;
            triangles[triIndex++] = topLeft;
            triangles[triIndex++] = bottomRight;

            triangles[triIndex++] = bottomRight;
            triangles[triIndex++] = topLeft;
            triangles[triIndex++] = topRight;
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        return mesh;
    }

    void Update()
    {
        if (beamMesh == null)
            return;

        // Server handles energy decay
        if (IsServer)
        {
            UpdateEnergyDecay();
        }

        // Update outline visibility based on camera mode and raycast
        UpdateOutlineVisibility();

        if (!animate)
            return;

        float time = Time.time + timeOffset;

        // Rotate beam
        beamMesh.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);

        // Pulse effect on beam with clamped values
        float pulseValue = (Mathf.Sin(time * pulseSpeed) + 1f) * 0.5f; // 0 to 1
        float pulse = Mathf.Lerp(Mathf.Max(0.1f, minPulseScale), Mathf.Max(0.1f, maxPulseScale), pulseValue);
        beamMesh.transform.localScale = new Vector3(pulse, 1f, pulse);
        currentBeamScaleXZ = pulse;

        // Color cycling between start and end colors
        float colorCycle = (Mathf.Sin(time * colorCycleSpeed) + 1f) * 0.5f; // 0 to 1
        Color currentColor = Color.Lerp(beamColorStart, beamColorEnd, colorCycle);

        if (beamMaterial != null)
        {
            beamMaterial.color = currentColor;
            beamMaterial.SetColor("_EmissionColor", currentColor * 2f);
        }

        // UV scroll
        if (beamMaterial != null && beamMaterial.HasProperty("_MainTex"))
        {
            float offset = time * scrollSpeed;
            beamMaterial.SetTextureOffset("_MainTex", new Vector2(0, offset));
        }
    }

    void UpdateOutlineVisibility()
    {
        if (outline == null)
            return;

        bool shouldShowOutline = false;
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
            return;

        // Check which camera mode we're in (if camera manager exists)
        // Default to isometric (mouse-based) if no camera manager found
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
                Debug.Log($"[EnergyBeam] Third-person raycast hit: {hit.collider.gameObject.name}, Tag: {hit.collider.tag}");

                // Check if we hit this beam
                if (hit.collider.gameObject == beamMesh)
                {
                    shouldShowOutline = true;

                    // Check for E key press to interact
                    if (Input.GetKey(KeyCode.E))
                    {
                        isBeingInteracted = true;
                    }
                    else
                    {
                        isBeingInteracted = false;
                    }

                    // E key handling is now done by PlayerController's interaction system

                    // Restore any hoverable object since we're not hovering it anymore
                    RestoreHoverableObjectColor();
                }
                // Check if we hit a hoverable object
                else if (hit.collider.CompareTag("Hoverable"))
                {
                    // Don't show outline for hoverable objects, only for the beam
                    shouldShowOutline = false;

                    // Handle color change for hoverable object
                    HandleHoverableObjectColor(hit.collider.gameObject);
                }
                else
                {
                    // Hit something else - restore hoverable color
                    RestoreHoverableObjectColor();
                }
            }
            else
            {
                isBeingInteracted = false;
                RestoreHoverableObjectColor();
            }
        }
        else if (currentMode == CameraManager.CameraMode.Isometric)
        {
            // Isometric mode: raycast from mouse position
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 1000f))
            {
                Debug.Log($"[EnergyBeam] Isometric raycast hit: {hit.collider.gameObject.name}, Tag: {hit.collider.tag}");

                // Check if we hit this beam
                if (hit.collider.gameObject == beamMesh)
                {
                    shouldShowOutline = true;

                    // Check for mouse click to interact
                    if (Input.GetMouseButton(0)) // Left mouse button
                    {
                        isBeingInteracted = true;
                    }
                    else
                    {
                        isBeingInteracted = false;
                    }

                    // Click handling is now done by PlayerController's interaction system

                    // Restore any hoverable object since we're not hovering it anymore
                    RestoreHoverableObjectColor();
                }
                // Check if we hit a hoverable object
                else if (hit.collider.CompareTag("Hoverable"))
                {
                    // Don't show outline for hoverable objects, only for the beam
                    shouldShowOutline = false;

                    // Check for mouse click to interact
                    if (Input.GetMouseButton(0)) // Left mouse button
                    {
                        isBeingInteracted = true;
                    }
                    else
                    {
                        isBeingInteracted = false;
                    }

                    // Handle color change for hoverable object
                    HandleHoverableObjectColor(hit.collider.gameObject);
                }
                else
                {
                    // Hit something else - restore hoverable color
                    RestoreHoverableObjectColor();
                }
            }
            else
            {
                isBeingInteracted = false;
                RestoreHoverableObjectColor();
            }
        }

        // Enable or disable outline based on result
        outline.enabled = shouldShowOutline;

        // Update outline color based on interaction state
        if (shouldShowOutline)
        {
            if (isBeingInteracted)
            {
                outline.OutlineColor = new Color(0.5f, 0f, 0.5f); // Purple
            }
            else
            {
                outline.OutlineColor = Color.red;
            }
        }

        // === COLOR CHANGE TEST FOR HOVER DETECTION ===
        // This changes the beam's material color to test if hover detection is working
        // If the color changes but outline doesn't show, the hover works but outline shader has issues
        if (shouldShowOutline)
        {
            // Store original color on first hover
            if (!wasHoveringLastFrame && beamMaterial != null)
            {
                originalBeamColor = beamMaterial.color;
                Debug.Log("[EnergyBeam] Started hovering - storing original color: " + originalBeamColor);
            }

            // Change beam color based on interaction
            if (beamMaterial != null)
            {
                if (isBeingInteracted)
                {
                    beamMaterial.color = Color.blue; // Blue when interacting
                    Debug.Log("[EnergyBeam] Interacting - beam color changed to BLUE");
                }
                else
                {
                    beamMaterial.color = Color.green; // Green when hovering
                    Debug.Log("[EnergyBeam] Hovering - beam color changed to GREEN");
                }
            }

            wasHoveringLastFrame = true;
        }
        else
        {
            // Restore original color when not hovering
            if (wasHoveringLastFrame && beamMaterial != null)
            {
                beamMaterial.color = originalBeamColor;
                Debug.Log("[EnergyBeam] Stopped hovering - restored original color: " + originalBeamColor);
            }

            wasHoveringLastFrame = false;
        }
    }

    void HandleHoverableObjectColor(GameObject hitObject)
    {
        // If this is a different object than we were hovering, restore the old one first
        if (currentHoveredObject != null && currentHoveredObject != hitObject)
        {
            RestoreHoverableObjectColor();
        }

        // Get the renderer and material
        Renderer hitRenderer = hitObject.GetComponent<Renderer>();
        if (hitRenderer == null || hitRenderer.material == null)
            return;

        // If this is a new object, store its original color
        if (currentHoveredObject != hitObject)
        {
            currentHoveredObject = hitObject;
            currentHoveredMaterial = hitRenderer.material;
            originalHoverableColor = currentHoveredMaterial.color;
            Debug.Log($"[EnergyBeam] Started hovering {hitObject.name}, original color: {originalHoverableColor}");
        }

        // Apply hover color
        if (isBeingInteracted)
        {
            currentHoveredMaterial.color = Color.blue;
        }
        else
        {
            currentHoveredMaterial.color = Color.green;
        }
    }

    void RestoreHoverableObjectColor()
    {
        if (currentHoveredObject != null && currentHoveredMaterial != null)
        {
            currentHoveredMaterial.color = originalHoverableColor;
            Debug.Log($"[EnergyBeam] Stopped hovering {currentHoveredObject.name}, restored color: {originalHoverableColor}");
            currentHoveredObject = null;
            currentHoveredMaterial = null;
        }
    }

    void OnDestroy()
    {
        // Restore any hoverable object color before destroying
        RestoreHoverableObjectColor();

        if (beamMaterial != null)
            Destroy(beamMaterial);
    }

    public void Interact()
    {
        Debug.Log("Energy Beam Selected!");

        if (beamMaterial != null)
        {
            StartCoroutine(InteractionFlash());
        }
    }

    System.Collections.IEnumerator InteractionFlash()
    {
        Color originalColor = beamColorStart;

        for (int i = 0; i < 3; i++)
        {
            if (beamMaterial != null)
                beamMaterial.SetColor("_EmissionColor", Color.white * 5f);
            yield return new WaitForSeconds(0.1f);

            if (beamMaterial != null)
                beamMaterial.SetColor("_EmissionColor", originalColor * 3f);
            yield return new WaitForSeconds(0.1f);
        }
    }

    public bool IsInRange(Vector3 playerPosition)
    {
        float distance = Vector3.Distance(transform.position, playerPosition);
        return distance <= interactionRange;
    }

    void UpdateBeamSize()
    {
        if (beamMesh == null)
            return;

        // Recreate the mesh with new radius
        MeshFilter meshFilter = beamMesh.GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            Mesh newMesh = CreateCylinderMesh();
            meshFilter.mesh = newMesh;

            // Update collider
            MeshCollider meshCollider = beamMesh.GetComponent<MeshCollider>();
            if (meshCollider != null)
            {
                meshCollider.sharedMesh = newMesh;
            }
        }

        // Update particle spawn area
        if (particles != null)
        {
            var shape = particles.shape;
            float spawnRadius = beamRadius * 0.4f;
            shape.scale = new Vector3(spawnRadius * 2f, maxSpawnHeight - minSpawnHeight, spawnRadius * 2f);
        }
    }

    // === ENERGY SYSTEM METHODS ===

    void UpdateEnergyDecay()
    {
        // Decay energy over time
        if (currentEnergy.Value > 0)
        {
            currentEnergy.Value = Mathf.Max(0f, currentEnergy.Value - energyDecayRate * Time.deltaTime);
        }
    }

    public float GetCurrentEnergy()
    {
        return currentEnergy.Value;
    }

    public float GetMaxEnergy()
    {
        return maxEnergy;
    }

    // Server-only method to add energy from a stick
    public bool TryHandInStick(PlayerController player)
    {
        if (!IsServer)
        {
            Debug.LogError("[EnergyBeam] TryHandInStick called on client! This should only be called on server.");
            return false;
        }

        // Check if player has at least 1 stick
        if (player.GetStickCount() < 1)
        {
            Debug.LogWarning($"[EnergyBeam] Player {player.OwnerClientId} tried to hand in stick but has none!");
            return false;
        }

        // Check if we can accept more energy
        if (currentEnergy.Value >= maxEnergy)
        {
            Debug.LogWarning($"[EnergyBeam] Energy is at max ({maxEnergy}), cannot accept more sticks!");
            return false;
        }

        // Add energy from the stick
        currentEnergy.Value = Mathf.Min(maxEnergy, currentEnergy.Value + stickEnergyValue);
        Debug.Log($"[EnergyBeam] Player {player.OwnerClientId} handed in stick. Energy: {currentEnergy.Value:F2}/{maxEnergy}");

        return true;
    }

    // Public method called by PlayerController's interaction system
    public void InteractWithPlayer(PlayerController playerController)
    {
        if (playerController == null)
        {
            Debug.LogWarning("[EnergyBeam] Cannot interact - no player controller provided!");
            return;
        }

        // Check if player has at least 1 stick
        if (playerController.GetStickCount() < 1)
        {
            Debug.Log("[EnergyBeam] Cannot hand in stick - you don't have any sticks!");
            return;
        }

        // Check if we're in range (safety check, PlayerController should already verify this)
        if (!IsInRange(playerController.transform.position))
        {
            Debug.Log("[EnergyBeam] Cannot hand in stick - too far from core!");
            return;
        }

        // Get the player's NetworkObject to send to server
        if (playerController.TryGetComponent<NetworkObject>(out NetworkObject playerNetObj))
        {
            Debug.Log($"[EnergyBeam] Player {playerController.OwnerClientId} handing in stick. NetworkObjectId: {playerNetObj.NetworkObjectId}");
            RequestHandInStickServerRpc(playerNetObj.NetworkObjectId);
        }
        else
        {
            Debug.LogError("[EnergyBeam] Player doesn't have NetworkObject!");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestHandInStickServerRpc(ulong playerNetworkObjectId)
    {
        Debug.Log($"[EnergyBeam] Received hand-in request from player NetworkObjectId: {playerNetworkObjectId}");

        // Find the player's NetworkObject
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkObjectId, out NetworkObject playerNetObj))
        {
            PlayerController playerController = playerNetObj.GetComponent<PlayerController>();
            if (playerController != null)
            {
                // Try to hand in the stick
                if (TryHandInStick(playerController))
                {
                    // Success! Remove one stick from player
                    playerController.RemoveStick();
                    Debug.Log($"[EnergyBeam] Successfully processed stick hand-in from player {playerController.OwnerClientId}");
                }
            }
            else
            {
                Debug.LogError("[EnergyBeam] Player NetworkObject found but no PlayerController component!");
            }
        }
        else
        {
            Debug.LogError($"[EnergyBeam] Could not find player with NetworkObjectId: {playerNetworkObjectId}");
        }
    }
}