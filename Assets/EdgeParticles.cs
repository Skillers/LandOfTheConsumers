using UnityEngine;

public class EdgeParticles : MonoBehaviour
{
    [Header("Edge Particles")]
    public bool enableEdgeParticles = true;
    public int edgeParticleEmissionRate = 30;
    public float edgeParticleSpeed = 12f;
    public float edgeParticleSize = 0.25f;
    public float edgeParticleAngle = 15f;
    public float beamRadius = 2f;
    public float minLifetime = 2f;
    public float maxLifetime = 5f;

    [Header("Edge Particle Pulse")]
    public bool syncWithBeam = true;
    public float pulseSpeed = 2f; // Beam pulse speed (for syncing)
    public float edgePulseSpeed = 1.5f; // Independent pulse speed
    public float edgePulseMinScale = 0.6f;
    public float edgePulseMaxScale = 1.0f;

    private ParticleSystem edgeParticles;
    private GameObject edgeParticleObj;
    private float timeOffset;

    void Start()
    {
        timeOffset = Random.Range(0f, 100f);
        CreateEdgeParticles();
    }

    void CreateEdgeParticles()
    {
        if (!enableEdgeParticles)
            return;

        edgeParticleObj = new GameObject("EdgeParticles");
        edgeParticleObj.transform.parent = transform;
        edgeParticleObj.transform.localPosition = Vector3.zero;

        edgeParticles = edgeParticleObj.AddComponent<ParticleSystem>();

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
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;

        var emission = edgeParticles.emission;
        emission.rateOverTime = edgeParticleEmissionRate;

        emission.burstCount = 1;
        ParticleSystem.Burst burst = new ParticleSystem.Burst(0f, edgeParticleEmissionRate);
        burst.cycleCount = 0;
        burst.repeatInterval = 1f;
        emission.SetBurst(0, burst);

        var shape = edgeParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = beamRadius - 0.2f;
        shape.radiusThickness = 0f;
        shape.arc = 360f;
        shape.arcMode = ParticleSystemShapeMultiModeValue.Loop;
        shape.arcSpread = 0f;
        shape.position = new Vector3(0, 0, 0);
        shape.rotation = new Vector3(-90, 0, 0);
        shape.randomDirectionAmount = 0f;
        shape.sphericalDirectionAmount = 0f;

        // Align particles to face outward radially
        shape.alignToDirection = true;

        var velocityOverLifetime = edgeParticles.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;

        float angleRad = edgeParticleAngle * Mathf.Deg2Rad;
        float upwardSpeed = edgeParticleSpeed * Mathf.Cos(angleRad);

        velocityOverLifetime.x = 0f;
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(upwardSpeed);
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
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(0.5f, 0.8f);
        sizeCurve.AddKey(1f, 0.3f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var renderer = edgeParticles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 1.0f;
        renderer.velocityScale = 0.2f;
        renderer.cameraVelocityScale = 0f;
        renderer.normalDirection = 1f;
        renderer.alignment = ParticleSystemRenderSpace.Velocity; // Align with velocity direction

        Material particleMat = new Material(Shader.Find("Particles/Standard Unlit"));
        particleMat.SetColor("_Color", Color.white);
        particleMat.EnableKeyword("_ALPHABLEND_ON");
        renderer.material = particleMat;

        edgeParticles.Play();

        Debug.Log("Edge particles created!");
    }

    void Update()
    {
        if (edgeParticleObj == null)
            return;

        float time = Time.time + timeOffset;

        float edgePulseValue;
        if (syncWithBeam)
        {
            edgePulseValue = Mathf.Lerp(edgePulseMinScale, edgePulseMaxScale,
                (Mathf.Sin(time * pulseSpeed) + 1f) * 0.5f);
        }
        else
        {
            edgePulseValue = Mathf.Lerp(edgePulseMinScale, edgePulseMaxScale,
                (Mathf.Sin(time * edgePulseSpeed) + 1f) * 0.5f);
        }
        edgeParticleObj.transform.localScale = new Vector3(edgePulseValue, 1f, edgePulseValue);
    }

    public void SetBeamRadius(float radius)
    {
        beamRadius = radius;
    }

    public void SetPulseSpeed(float speed)
    {
        pulseSpeed = speed;
    }
}