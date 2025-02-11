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
    
    // Trajectory control
    public Vector3 origin;
    public Vector3 destination;
    public bool hasReachedDestination;
    public float journeyDistance;

    public FluidParticle(Vector3 pos, float maxLifetime, Vector3 orig, Vector3 dest)
    {
        position = pos;
        velocity = Vector3.zero;
        force = Vector3.zero;
        density = 0f;
        pressure = 0f;
        lifetime = maxLifetime;
        isActive = true;
        
        origin = orig;
        destination = dest;
        hasReachedDestination = false;
        journeyDistance = Vector3.Distance(orig, dest);
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
    [Header("Original Interaction Layers")]
    [SerializeField] private LayerMask boostSurfaces;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private LayerMask waterPlatform;
    [SerializeField] private LayerMask breakableWall;
    [SerializeField] private LayerMask waterPushableLayer;
    [SerializeField] private LayerMask fireLayer;
    [SerializeField] private LayerMask particleCollisionMask;

    [Header("External References")]
    [SerializeField] private Transform cursor;
    private BasicMovement playerScript;
    private Rigidbody2D playerRigidbody;

    [Header("Interaction Settings")]
    [SerializeField] private float hitArea = 1f;
    [SerializeField] private Vector2 raycastSize = new Vector2(0.2f, 0.2f);
    [SerializeField] private float boostForce = 0.1f;
    
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

    [Header("Trajectory Control")]
[SerializeField] private float maxTrajectoryDistance = 10f; // Add this variable
[SerializeField] private float fallSpeed = 9.81f;
[SerializeField] private float pathWidth = 0.1f;
[SerializeField] private LayerMask collisionMask;

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

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        playerScript = player.GetComponent<BasicMovement>();
        playerRigidbody = player.GetComponent<Rigidbody2D>();
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

    

    public void EmitParticle(Vector3 position, Vector3 velocity, Vector3 destination)
    {
        if (activeParticles.Count >= maxParticles) return;

        // Calculate direction and distance to desired destination
        Vector3 toDestination = destination - position;
        float desiredDistance = toDestination.magnitude;
        
        // Clamp the destination to maximum distance
        Vector3 clampedDestination = position + (toDestination.normalized * Mathf.Min(desiredDistance, maxTrajectoryDistance));

        // Create or get particle from pool
        FluidParticle particle;
        if (particlePool.Count < maxParticles)
        {
            particle = new FluidParticle(position, parameters.particleLifetime, position, clampedDestination);
            particlePool.Add(particle);
        }
        else
        {
            particle = particlePool.Find(p => !p.isActive);
            if (particle == null) return;
            
            // Reset particle
            particle.position = position;
            particle.origin = position;
            particle.destination = clampedDestination;
            particle.hasReachedDestination = false;
            particle.lifetime = parameters.particleLifetime;
            particle.isActive = true;
        }

        // Initialize movement
        particle.velocity = velocity * emissionForce;
        particle.journeyDistance = Vector3.Distance(position, clampedDestination);

        // Create or get visual
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

    for (int i = activeParticles.Count - 1; i >= 0; i--)
    {
        FluidParticle p = activeParticles[i];
        
        // Check if we've reached destination
        Vector3 originalToDestination = p.destination - p.origin;
        Vector3 currentToDestination = p.destination - p.position;
        float dotProduct = Vector3.Dot(originalToDestination.normalized, currentToDestination.normalized);
        
        // Ensure transition to falling state
        if (!p.hasReachedDestination && (dotProduct < 0 || Vector3.Distance(p.position, p.destination) < 0.1f))
        {
            p.hasReachedDestination = true;
            p.position = new Vector3(p.destination.x, p.position.y, p.position.z); // Only snap X coordinate
            // Initialize falling velocity
            p.velocity = new Vector3(0, -1f, 0); // Give initial downward velocity
            p.force = Vector3.down * fallSpeed;
        }

        if (!p.hasReachedDestination)
        {
            // Normal trajectory following
            Vector3 directionToDestination = (p.destination - p.position).normalized;
            Vector3 randomOffset = Random.insideUnitSphere * pathWidth;
            Vector3 targetDirection = (directionToDestination + randomOffset.normalized * 0.1f).normalized;
            
            p.force = targetDirection;
            p.velocity = Vector3.Lerp(p.velocity, targetDirection * emissionForce, dt * 5f);
        }
        else
        {
            // Enhanced falling behavior
            p.force = Vector3.down * fallSpeed;
            // Gradually reduce horizontal velocity
            float horizontalDamping = 0.95f;
            p.velocity.x *= horizontalDamping;
            p.velocity.z *= horizontalDamping;
            
            // Ensure there's always downward acceleration
            p.velocity += Vector3.down * fallSpeed * dt;
        }

        // Update velocity and position
        p.velocity += (p.force / p.density) * dt;
        Vector3 newPosition = p.position + p.velocity * dt;

        if (CheckCollision(p.position, newPosition, out RaycastHit2D hit))
        {
            HandleOriginalInteractions(hit, i);
            continue;
        }

        p.position = newPosition;
        if (i < activeVisuals.Count)
        {
            activeVisuals[i].transform.position = p.position;
        }
    }
}

    private bool CheckCollision(Vector3 currentPos, Vector3 newPos, out RaycastHit2D hit)
    {
        Vector2 direction = (newPos - currentPos).normalized;
        float distance = Vector2.Distance(currentPos, newPos);
        
        LayerMask combinedLayers = boostSurfaces | enemyLayer | waterPlatform | 
                                  breakableWall | waterPushableLayer | fireLayer | 
                                  particleCollisionMask;

        hit = Physics2D.BoxCast(
            currentPos,
            raycastSize,
            0f, // No rotation
            direction,
            distance,
            combinedLayers
        );

        return hit.collider != null;
    }

    private void HandleOriginalInteractions(RaycastHit2D hit, int particleIndex)
    {
        bool shouldDestroyParticle = false;

        if (((1 << hit.collider.gameObject.layer) & waterPlatform) != 0)
        {
            if (hit.collider.TryGetComponent<WaterPlatform>(out var platform))
            {
                platform.SetColliderState(true);
                shouldDestroyParticle = true;
                
                if (hit.collider.enabled)
                {
                    ApplyPlayerBoost();
                }
            }
        }
        else if (((1 << hit.collider.gameObject.layer) & boostSurfaces) != 0)
        {
            ApplyPlayerBoost();
            shouldDestroyParticle = true;
        }
        else if (((1 << hit.collider.gameObject.layer) & breakableWall) != 0)
        {
            if (hit.collider.TryGetComponent<BreakableWall>(out var destructible))
            {
                destructible.TakeDamage(Time.deltaTime);
                shouldDestroyParticle = true;
            }
        }
        else if (((1 << hit.collider.gameObject.layer) & enemyLayer) != 0)
        {
            if (hit.collider.TryGetComponent<Behaviour>(out var enemyScript))
            {
                enemyScript.TakeDamage(Time.deltaTime);
                shouldDestroyParticle = true;
            }
        }
        else if (((1 << hit.collider.gameObject.layer) & waterPushableLayer) != 0)
        {
            if (hit.collider.TryGetComponent<Box>(out var pushable))
            {
                pushable.ApplyWaterForce(activeParticles[particleIndex].origin);
                shouldDestroyParticle = true;
            }
        }
        else if (((1 << hit.collider.gameObject.layer) & fireLayer) != 0)
        {
            if (hit.collider.TryGetComponent<Fire>(out var fireScript))
            {
                fireScript.Extinguish(Time.deltaTime);
                shouldDestroyParticle = true;
            }
        }
        else if (((1 << hit.collider.gameObject.layer) & particleCollisionMask) != 0)
        {
            shouldDestroyParticle = true;
        }

        if (shouldDestroyParticle)
        {
            RemoveParticle(particleIndex);
        }
    }

    private void RemoveParticle(int index)
    {
        if (index >= 0 && index < activeParticles.Count)
        {
            GameObject visual = activeVisuals[index];
            visual.SetActive(false);
            visualPool.Enqueue(visual);

            activeParticles[index].isActive = false;
            activeParticles.RemoveAt(index);
            activeVisuals.RemoveAt(index);
        }
    }

    private void ApplyPlayerBoost()
    {
        playerScript.SwitchHorizLock(true);
        Vector2 boostDirection = -(cursor.position - transform.position).normalized;
        float finalBoostForce = boostForce;
        
        playerRigidbody.AddForce(boostDirection * finalBoostForce, ForceMode2D.Impulse);
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
