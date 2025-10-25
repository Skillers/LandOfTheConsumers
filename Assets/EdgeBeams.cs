using UnityEngine;

public class EdgeBeams : MonoBehaviour
{
    [Header("Particle Settings")]
    public float beamRadius = 2f;
    public float minLifetime = 2f;
    public float maxLifetime = 5f;

    [Header("Edge Particles")]
    public bool enableEdgeParticles = true;
    public int edgeParticleEmissionRate = 10; // Reduced from 30 to 10
    public float edgeParticleSpeed = 12f;
    public float edgeParticleSize = 0.25f;
    public float edgeParticleAngle = 15f;

    [Header("Rotation")]
    public float rotationSpeed = 30f;

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

        // Add ParticleSystem directly to this GameObject
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
        main.maxParticles = 150; // Reduced from 300 to 150
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

        velocityOverLifetime.x = 0f;
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(edgeParticleSpeed);
        velocityOverLifetime.z = 0f;
        velocityOverLifetime.orbitalX = 0f;
        velocityOverLifetime.orbitalY = 0f;
        velocityOverLifetime.orbitalZ = 0f;

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
        sizeOverLifetime.enabled = false;

        var renderer = edgeParticles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 1.0f;
        renderer.velocityScale = 0.2f;
        renderer.cameraVelocityScale = 0f;
        renderer.normalDirection = 1f;

        Material particleMat = new Material(Shader.Find("Particles/Standard Unlit"));
        particleMat.SetColor("_Color", Color.white);
        particleMat.EnableKeyword("_ALPHABLEND_ON");
        renderer.material = particleMat;

        edgeParticles.Play();

        Debug.Log("Edge particles created on main GameObject!");
    }

    void Update()
    {
        // Rotate the entire GameObject (which rotates the particle emitter with it)
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
    }
}