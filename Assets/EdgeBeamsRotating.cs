using UnityEngine;

public class EdgeBeamsRotating : MonoBehaviour
{
    [Header("Beam Settings")]
    public float beamHeight = 100f;
    public float beamRadius = 2f;
    public int segments = 32;

    [Header("Animation")]
    public bool animate = true;
    public float rotationSpeed = 30f;

    [Header("Interaction")]
    public float interactionRange = 10f;

    [Header("Edge Particles")]
    public bool enableEdgeParticles = true;
    public int edgeParticleEmissionRate = 30;
    public float edgeParticleSpeed = 12f;
    public float edgeParticleSize = 0.25f;
    public float edgeParticleAngle = 15f;
    public float minLifetime = 2f;
    public float maxLifetime = 5f;

    [Header("Edge Particle Pulse")]
    public float edgePulseSpeed = 1.5f;
    public float edgePulseMinScale = 0.6f;
    public float edgePulseMaxScale = 1.0f;

    private float timeOffset;
    private ParticleSystem edgeParticles;

    void Start()
    {
        CreateEdgeParticles();
        timeOffset = Random.Range(0f, 100f);
    }

    void CreateEdgeParticles()
    {
        if (!enableEdgeParticles)
            return;

        edgeParticles = gameObject.AddComponent<ParticleSystem>();

        var main = edgeParticles.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(minLifetime, maxLifetime);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(edgeParticleSize * 0.3f, edgeParticleSize * 0.8f);

        Gradient colorGradient = new Gradient();
        colorGradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.5f, 0.1f, 0.8f), 0f),
                new GradientColorKey(new Color(0.8f, 0.2f, 1f), 0.33f),
                new GradientColorKey(new Color(1f, 0.4f, 1f), 0.66f),
                new GradientColorKey(new Color(0.6f, 0.3f, 1f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.8f, 0f),
                new GradientAlphaKey(0.8f, 1f)
            }
        );
        main.startColor = new ParticleSystem.MinMaxGradient(colorGradient);
        main.maxParticles = 300;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.gravityModifier = 0f;

        var emission = edgeParticles.emission;
        emission.rateOverTime = edgeParticleEmissionRate;

        var shape = edgeParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = beamRadius;
        shape.radiusThickness = 0f;
        shape.arc = 360f;
        shape.arcMode = ParticleSystemShapeMultiModeValue.Random;
        shape.position = new Vector3(0, 0, 0);
        shape.rotation = new Vector3(-90, 0, 0);

        var velocityOverLifetime = edgeParticles.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;

        float angleRad = edgeParticleAngle * Mathf.Deg2Rad;
        float upwardSpeed = edgeParticleSpeed * Mathf.Cos(angleRad);
        float tangentialSpeed = edgeParticleSpeed * Mathf.Sin(angleRad);

        velocityOverLifetime.x = 0f;
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(upwardSpeed);
        velocityOverLifetime.z = 0f;
        velocityOverLifetime.orbitalX = 0f;
        velocityOverLifetime.orbitalY = new ParticleSystem.MinMaxCurve(tangentialSpeed);
        velocityOverLifetime.orbitalZ = 0f;
        velocityOverLifetime.radial = 0f;

        var colorOverLifetime = edgeParticles.colorOverLifetime;
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

        var sizeOverLifetime = edgeParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(0.5f, 0.8f);
        sizeCurve.AddKey(1f, 0.3f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var renderer = edgeParticles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 0.5f;
        renderer.velocityScale = 0.1f;
        renderer.cameraVelocityScale = 0f;
        renderer.normalDirection = 1f;

        Material particleMat = new Material(Shader.Find("Particles/Standard Unlit"));
        particleMat.SetColor("_Color", Color.white);
        particleMat.EnableKeyword("_ALPHABLEND_ON");
        renderer.material = particleMat;

        edgeParticles.Play();

        Debug.Log("Edge particles created!");
    }

    void Update()
    {
        if (!animate)
            return;
    }

    public void Interact()
    {
        Debug.Log("Energy Beam Selected!");
    }

    public bool IsInRange(Vector3 playerPosition)
    {
        float distance = Vector3.Distance(transform.position, playerPosition);
        return distance <= interactionRange;
    }
}