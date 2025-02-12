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
    public bool hasReachedDestination;
    public bool hasCollided;
    
    public Vector3 origin;
    public Vector3 destination;
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
        hasReachedDestination = false;
        hasCollided = false;
        
        origin = orig;
        destination = dest;
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
    public float pathWidth;
    public float emissionForce;
    public float fallSpeed;
}

public class SPHSimulation : MonoBehaviour
{
    [Header("Camera Shake")]
    [SerializeField] private CameraFollower cameraFollower;
    
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
    [SerializeField] private Transform hoseRotation;
    private BasicMovement playerScript;
    private Rigidbody2D playerRigidbody;

    [Header("Interaction Settings")]
    [SerializeField] private float hitArea = 1f;
    [SerializeField] private Vector2 raycastSize = new Vector2(0.2f, 0.2f);
    [SerializeField] private float boostForce = 0.1f;
    private const float MIN_BOOST_MULTIPLIER = 0.1f;
    private float storedBoostMultiplier;
    
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
    [SerializeField] private float minPathFollowDistance = 2f;
    [SerializeField] private float fallSpeed = 9.81f;
    [SerializeField] private float pathWidth = 0.1f;
    [SerializeField] private LayerMask collisionMask;

    // Add these new fields
    private bool isBoostActive = false;
    
    // Add this public property to check spray state from WaterSprayer
    public bool IsBoostActive => isBoostActive;

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
        cameraFollower = Camera.main.GetComponent<CameraFollower>();
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


    [Header("Offset Control")]
[SerializeField] private float cursorOffset = 2f; // Adjust this value to control the overshoot
[SerializeField] private bool visualizeOffset = true; // For debugging

    public void EmitParticle(Vector3 position, Vector3 velocity, Vector3 destination)
    {
        if (activeParticles.Count >= maxParticles) return;

        // Cache direction for consistent boost calculations
    currentDirection = (cursor.position - transform.position).normalized;
    
    float distance = Vector2.Distance(transform.position, cursor.position);
    storedBoostMultiplier = Mathf.Lerp(
        MIN_BOOST_MULTIPLIER,
        1f,
        1f - Mathf.Clamp01(distance / maxTreshold) // Use maxTreshold instead of maxTrajectoryDistance
    );

    // Calculate original distance and direction
    Vector3 toDestination = destination - position;
    float originalDistance = toDestination.magnitude;
    Vector3 direction = toDestination.normalized;
    
    // Simply subtract the offset from the original distance
    //float adjustedDistance = Mathf.Max(originalDistance - cursorOffset, minPathFollowDistance);
    float dynamicOffset = GetDynamicOffset(originalDistance);
float adjustedDistance = Mathf.Max(originalDistance - dynamicOffset, minPathFollowDistance);
    
    // Calculate new destination using adjusted distance
    Vector3 adjustedDestination = position + (direction * adjustedDistance);

    // Create or get particle from pool
    FluidParticle particle;
    if (particlePool.Count < maxParticles)
    {
        particle = new FluidParticle(position, parameters.particleLifetime, position, adjustedDestination);
        particlePool.Add(particle);
    }
    else
    {
        particle = particlePool.Find(p => !p.isActive);
        if (particle == null) return;
        
        // Reset particle
        particle.position = position;
        particle.origin = position;
        particle.destination = adjustedDestination;
        particle.hasReachedDestination = false;
        particle.hasCollided = false;
        particle.lifetime = parameters.particleLifetime;
        particle.isActive = true;
    }

    // Initialize velocity
    particle.velocity = velocity * emissionForce;
    particle.journeyDistance = adjustedDistance;

        /* Initialize movement
        particle.velocity = velocity * emissionForce;
        particle.journeyDistance = Vector3.Distance(position, clampedDestination); */

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

    float GetDynamicOffset(float distance)
{
    if (distance <= 5f)
    {
        return cursorOffset; // Full offset for close distances
    }
    else
    {
        // Gradually reduce offset for longer distances
        float t = (distance - 5f) / 2f; // Transition over 2 units
        t = Mathf.Clamp01(t);
        return Mathf.Lerp(cursorOffset, cursorOffset * 0.5f, t);
    }
}

    private void Update()
    {
        WaterSprayer sprayer = GetComponent<WaterSprayer>();
        
        // If spray has stopped and we were boosting
        if ((!sprayer || !sprayer.isSpraying) && isBoostActive)
        {
            // Stop upward momentum
            if (playerRigidbody != null)
            {
                Vector2 velocity = playerRigidbody.velocity;
                if (velocity.y > 0)
                {
                    velocity.y *= 0.5f;
                    playerRigidbody.velocity = velocity;
                }
            }
            
            // Release horizontal lock
            playerScript.SwitchHorizLock(false);
            isBoostActive = false;

            if (cameraFollower != null)
            {
                cameraFollower.SetBoostScale(false);
            }
        }
        
        if (activeParticles.Count == 0) return;

        ComputeDensityPressure();
        ComputeForces();
        UpdatePositions();
        UpdateParticlesAndLifetime();

        float distanceFromStart = Vector3.Distance(transform.position, cursor.position);
        Debug.Log(distanceFromStart);
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
    float maxPathDistance = parameters.smoothingRadius * 100f; // Adjust this multiplier as needed

    for (int i = activeParticles.Count - 1; i >= 0; i--)
    {
        FluidParticle p = activeParticles[i];
        
        float distanceFromStart = Vector3.Distance(p.origin, p.position);
        Vector3 directionToDestination = (p.destination - p.position).normalized;
        
        if (!p.hasReachedDestination && distanceFromStart < maxPathDistance)
        {
            // Get target point along the path with controlled variation
            Vector3 targetPoint = p.origin + (p.destination - p.origin).normalized * distanceFromStart;
            Vector3 randomOffset = Random.insideUnitSphere * pathWidth;
            randomOffset.z = 0; // Keep it 2D
            targetPoint += randomOffset;

            // Calculate path following force
            Vector3 towardsPath = (targetPoint - p.position);
            p.force = towardsPath * emissionForce;

            // Smooth velocity transition
            Vector3 desiredVelocity = directionToDestination * emissionForce;
            p.velocity = Vector3.Lerp(p.velocity, desiredVelocity, dt * 5f);

            // Check if we should transition to falling
            if (Vector3.Distance(p.position, p.destination) < 0.1f)
            {
                p.hasReachedDestination = true;
            }
        }
        else
        {
            // Falling state with smooth transition
            p.hasReachedDestination = true;
            
            // Apply gravity gradually
            p.force = Vector3.down * fallSpeed;
            
            // Smooth horizontal velocity dampening
            float horizontalDamping = 0.99f;
            p.velocity.x *= horizontalDamping;
            p.velocity.z *= horizontalDamping;
            
            // Gradual vertical velocity adjustment
            float targetFallSpeed = -fallSpeed;
            p.velocity.y = Mathf.Lerp(p.velocity.y, targetFallSpeed, dt * 2f);
        }

        // Update final velocity and position
        p.velocity += (p.force / p.density) * dt;
        Vector3 newPosition = p.position + p.velocity * dt;

        // Handle collisions
        if (CheckCollision(p.position, newPosition, out RaycastHit2D hit))
        {
            HandleOriginalInteractions(hit, i);
            continue;
        }

        // Update position and visual representation
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
        float angle = hoseRotation.eulerAngles.z;
        
        LayerMask combinedLayers = boostSurfaces | enemyLayer | waterPlatform | 
                                  breakableWall | waterPushableLayer | fireLayer | 
                                  particleCollisionMask;

        hit = Physics2D.BoxCast(
            currentPos,
            raycastSize,
            angle,
            direction,
            distance,
            combinedLayers
        );

        return hit.collider != null;
    }

    private void HandleOriginalInteractions(RaycastHit2D hit, int particleIndex)
    {
        bool shouldDestroyParticle = false;
        WaterSprayer sprayer = GetComponent<WaterSprayer>();
        bool isSprayActive = sprayer != null && sprayer.isSpraying;

        if (cameraFollower != null)
        {
            cameraFollower.TryShake(hit.point);
        }

        if (((1 << hit.collider.gameObject.layer) & waterPlatform) != 0)
        {
            if (hit.collider.TryGetComponent<WaterPlatform>(out var platform))
            {
                platform.SetColliderState(true);
                shouldDestroyParticle = true;
                
                if (hit.collider.enabled && isSprayActive)
                {
                    ApplyPlayerBoost();
                }
            }
        }
        else if (((1 << hit.collider.gameObject.layer) & boostSurfaces) != 0)
        {
            if (isSprayActive)
            {
                ApplyPlayerBoost();
            }
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
        // Instead of removing immediately, mark as collided and start lifetime countdown
        activeParticles[particleIndex].hasCollided = true;
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

    private Vector2 currentDirection;
    [SerializeField] private float maxTreshold = 10f;

    private void ApplyPlayerBoost()
    {
        playerScript.SwitchHorizLock(true);
        Vector2 boostDirection = -currentDirection; // Using cached direction as per previous fix
        float finalBoostForce = boostForce * storedBoostMultiplier;
        
        playerRigidbody.AddForce(boostDirection * finalBoostForce, ForceMode2D.Impulse);
        isBoostActive = true;

        if (cameraFollower != null)
        {
            cameraFollower.SetBoostScale(true);
        }
    }

    private void UpdateParticlesAndLifetime()
{
    float dt = Time.deltaTime;

    for (int i = activeParticles.Count - 1; i >= 0; i--)
    {
        FluidParticle p = activeParticles[i];

        // Only decrease lifetime if particle has collided
        if (p.hasCollided)
        {
            p.lifetime -= dt;

            if (p.lifetime <= 0)
            {
                // Return to pools
                p.isActive = false;
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

private void OnDrawGizmos()
{
    if (visualizeOffset && cursor != null)
    {
        Gizmos.color = Color.yellow;
        Vector3 direction = (cursor.position - transform.position).normalized;
        Vector3 offsetPoint = cursor.position - (direction * cursorOffset);
        Gizmos.DrawWireSphere(offsetPoint, 0.1f);
        Gizmos.DrawLine(transform.position, offsetPoint);
    }
}
}
