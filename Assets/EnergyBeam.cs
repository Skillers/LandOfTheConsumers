using UnityEngine;

public class EnergyBeam : MonoBehaviour
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
    public float interactionRange = 10f;
    public LayerMask beamLayer;

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

    private GameObject beamMesh;
    private GameObject beamColliderObj;
    private Material beamMaterial;
    private float timeOffset;
    private Collider beamCollider;
    private ParticleSystem particles;

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

    void Start()
    {
        CreateBeam();
        AddCollider();
        CreateParticles();
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

        meshRenderer.material = beamMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Debug.Log("Beam material created and applied!");

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
        if (!animate || beamMesh == null)
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

    void OnDestroy()
    {
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
}