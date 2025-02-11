using UnityEngine;
using System.Collections.Generic;

public class FluidParticle
{
    public Vector3 position;
    public Vector3 velocity;
    public Vector3 force;
    public float density;
    public float pressure;
    public float lifetime;
    public bool isActive;

    public FluidParticle(Vector3 pos, float maxLifetime)
    {
        position = pos;
        velocity = Vector3.zero;
        force = Vector3.zero;
        density = 0f;
        pressure = 0f;
        lifetime = maxLifetime;
        isActive = true;
    }
}

[System.Serializable]
public struct SPHParameters
{
    public float particleMass;
    public float smoothingRadius;
    public float targetDensity;
    public float pressureConstant;
    public float viscosityConstant;
    public float damping;
    public Vector3 gravity;
    public float particleLifetime;
}

public class SPHSimulation : MonoBehaviour
{
    [Header("Simulation Parameters")]
    [SerializeField] private SPHParameters parameters = new SPHParameters
    {
        particleMass = 0.02f,
        smoothingRadius = 0.1f,
        targetDensity = 1000f,
        pressureConstant = 200f,
        viscosityConstant = 0.1f,
        damping = 0.99f,
        gravity = new Vector3(0, -9.81f, 0),
        particleLifetime = 5f
    };

    [Header("Particle Settings")]
    [SerializeField] private GameObject particlePrefab;
    [SerializeField] private float particleScale = 0.1f;
    [SerializeField] private int maxParticles = 1000;
    [SerializeField] private float emissionForce = 10f;

    // Optimization: Pre-calculated constants
    private float smoothingRadiusSqr;
    private float poly6Constant;
    private float spikyConstant;
    private float viscosityLaplaceConstant;

    // Object pooling for better performance
    private List<FluidParticle> particlePool;
    private Queue<GameObject> visualPool;
    private List<FluidParticle> activeParticles;
    private List<GameObject> activeVisuals;

    [Header("Organization")]
    [SerializeField] private bool createParticleContainer = true;
    [SerializeField] private string containerName = "WaterParticles";
    private Transform particleContainer;


     private void Awake()
    {
        if (createParticleContainer)
        {
            // Create a container for particles if it doesn't exist
            GameObject container = GameObject.Find(containerName);
            if (container == null)
            {
                container = new GameObject(containerName);
            }
            particleContainer = container.transform;
        }

        InitializeConstants();
        // Don't pre-instantiate particles anymore
        particlePool = new List<FluidParticle>(maxParticles);
        visualPool = new Queue<GameObject>(maxParticles);
        activeParticles = new List<FluidParticle>();
        activeVisuals = new List<GameObject>();
    }

    private GameObject CreateParticle()
    {
        GameObject obj = Instantiate(particlePrefab);
        obj.transform.localScale = Vector3.one * particleScale;
        
        // Parent to container if we have one
        if (particleContainer != null)
        {
            obj.transform.parent = particleContainer;
        }
        
        return obj;
    }

    private void InitializeConstants()
    {
        float h = parameters.smoothingRadius;
        smoothingRadiusSqr = h * h;
        float h6 = smoothingRadiusSqr * smoothingRadiusSqr * smoothingRadiusSqr;
        float h9 = h6 * h * h * h;

        poly6Constant = 315f / (64f * Mathf.PI * h6);
        spikyConstant = -45f / (Mathf.PI * h6);
        viscosityLaplaceConstant = 45f / (Mathf.PI * h6);
    }

    /*
    private void InitializeParticlePools()
    {
        particlePool = new List<FluidParticle>(maxParticles);
        visualPool = new Queue<GameObject>(maxParticles);
        activeParticles = new List<FluidParticle>();
        activeVisuals = new List<GameObject>();

        // Pre-instantiate particles
        for (int i = 0; i < maxParticles; i++)
        {
            particlePool.Add(new FluidParticle(Vector3.zero, parameters.particleLifetime));
            GameObject obj = Instantiate(particlePrefab);
            obj.transform.localScale = Vector3.one * particleScale;
            obj.SetActive(false);
            visualPool.Enqueue(obj);
        }
    }
    */

    public void EmitParticle(Vector3 position, Vector3 velocity)
    {
        if (activeParticles.Count >= maxParticles) return;

        // Create new particle data if needed
        FluidParticle particle;
        if (particlePool.Count < maxParticles)
        {
            particle = new FluidParticle(position, parameters.particleLifetime);
            particlePool.Add(particle);
        }
        else
        {
            // Find an inactive particle
            particle = particlePool.Find(p => !p.isActive);
            if (particle == null) return;
        }

        // Initialize particle
        particle.position = position;
        particle.velocity = velocity * emissionForce;
        particle.force = Vector3.zero;
        particle.density = 0f;
        particle.pressure = 0f;
        particle.lifetime = parameters.particleLifetime;
        particle.isActive = true;

        // Get or create visual
        GameObject visual;
        if (visualPool.Count > 0)
        {
            visual = visualPool.Dequeue();
            visual.SetActive(true);
        }
        else
        {
            visual = CreateParticle();
        }

        visual.transform.position = position;
        
        activeParticles.Add(particle);
        activeVisuals.Add(visual);
    }

    private void Update()
    {
        if (activeParticles.Count == 0) return;

        ComputeDensityPressure();
        ComputeForces();
        UpdatePositions();
        UpdateParticlesAndLifetime();
    }

    private void ComputeDensityPressure()
    {
        // Using spatial partitioning would improve performance here
        for (int i = 0; i < activeParticles.Count; i++)
        {
            FluidParticle pi = activeParticles[i];
            float density = 0f;

            for (int j = 0; j < activeParticles.Count; j++)
            {
                FluidParticle pj = activeParticles[j];
                Vector3 diff = pj.position - pi.position;
                float r2 = diff.sqrMagnitude;

                if (r2 < smoothingRadiusSqr)
                {
                    density += parameters.particleMass * poly6Constant * Mathf.Pow(smoothingRadiusSqr - r2, 3);
                }
            }

            pi.density = Mathf.Max(density, parameters.targetDensity * 0.1f);
            pi.pressure = parameters.pressureConstant * (density - parameters.targetDensity);
        }
    }

    private void ComputeForces()
    {
        for (int i = 0; i < activeParticles.Count; i++)
        {
            FluidParticle pi = activeParticles[i];
            Vector3 forcePressure = Vector3.zero;
            Vector3 forceViscosity = Vector3.zero;

            for (int j = 0; j < activeParticles.Count; j++)
            {
                if (i == j) continue;

                FluidParticle pj = activeParticles[j];
                Vector3 diff = pj.position - pi.position;
                float r = diff.magnitude;

                if (r < parameters.smoothingRadius)
                {
                    // Pressure force using spiky kernel
                    float pressureForce = parameters.particleMass * (pi.pressure + pj.pressure) / (2f * pj.density);
                    forcePressure += -diff.normalized * pressureForce * spikyConstant * Mathf.Pow(parameters.smoothingRadius - r, 2);

                    // Viscosity force using viscosity kernel
                    forceViscosity += parameters.viscosityConstant * parameters.particleMass * 
                                    (pj.velocity - pi.velocity) / pj.density * 
                                    viscosityLaplaceConstant * (parameters.smoothingRadius - r);
                }
            }

            pi.force = forcePressure + forceViscosity + parameters.gravity * pi.density;
        }
    }

    private void UpdatePositions()
    {
        float dt = Time.deltaTime;

        for (int i = 0; i < activeParticles.Count; i++)
        {
            FluidParticle p = activeParticles[i];
            p.velocity += (p.force / p.density) * dt;
            p.velocity *= parameters.damping;
            p.position += p.velocity * dt;

            // Update visual position
            activeVisuals[i].transform.position = p.position;
        }
    }

    private void UpdateParticlesAndLifetime()
    {
        float dt = Time.deltaTime;

        for (int i = activeParticles.Count - 1; i >= 0; i--)
        {
            activeParticles[i].lifetime -= dt;

            if (activeParticles[i].lifetime <= 0)
            {
                // Return to pools
                activeParticles[i].isActive = false;
                GameObject visual = activeVisuals[i];
                visual.SetActive(false);
                visualPool.Enqueue(visual);

                // Remove from active lists
                activeParticles.RemoveAt(i);
                activeVisuals.RemoveAt(i);
            }
        }
    }
}
