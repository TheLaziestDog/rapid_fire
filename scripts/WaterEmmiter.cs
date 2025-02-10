/*
TODO: Fix the raycast click/hit issue
TODO: The water need to reach the object first before it does anything, not right after the player click the object
TODO: Clip the particles once it hit something
TODO: Fix the travel distance, as it still flying off from the cursor
*/

using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class FluidParticle
{
    public Vector3 position;
    public Vector3 velocity;
    public Vector3 force;
    public float density;
    public float pressure;
    public float lifetime;
    public bool isFollowingPath;
     public bool isDestroyed;
    // Add these new variables to store initial path information
    public Vector3 initialEmissionPoint;
    public Vector3 initialTargetDirection;

    public FluidParticle(Vector3 pos, float maxLifetime, Vector3 emissionPoint, Vector3 targetDir)
    {
        position = pos;
        velocity = Vector3.zero;
        force = Vector3.zero;
        density = 0f;
        pressure = 0f;
        lifetime = maxLifetime;
        isFollowingPath = true;
        initialEmissionPoint = emissionPoint;
        initialTargetDirection = targetDir;
        isDestroyed = false;
    }
}

public class WaterEmitter : MonoBehaviour
{
    [Header("Path Control")]
    [SerializeField] [Range(0f, 1f)] private float pressure = 1f;
    [SerializeField] private float pathWidth = 0.1f;
    [SerializeField] private float minPathFollowDistance = 1f;
    [SerializeField] private float maxPathFollowDistance = 10f;
    [SerializeField] private float fallSpeed = 9.81f;
    [SerializeField] private float pathFollowStrength = 10f;
    
    [Header("External References")]
    [SerializeField] private Transform cursor;
    [SerializeField] private Transform hoseRotation;
    private BasicMovement playerScript;
    private Rigidbody2D playerRigidbody;

    [Header("Interaction Settings")]
    [SerializeField] private float hitArea = 1f;
    [SerializeField] private Vector2 raycastSize = new Vector2(0.2f, 0.2f);
    [SerializeField] private float boostForce = 0.1f;
    [SerializeField] private float maxTreshold = 10f;

    [Header("Layer Masks")]
    [SerializeField] private LayerMask boostSurfaces;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private LayerMask waterPlatform;
    [SerializeField] private LayerMask breakableWall;
    [SerializeField] private LayerMask waterPushableLayer;
    [SerializeField] private LayerMask fireLayer;
    [SerializeField] private LayerMask particleCollisionMask;

    [Header("Collision Settings")]
    [SerializeField] private float collisionCheckRadius = 0.1f;

    [Header("Simulation Parameters")]
    [SerializeField] private float particleMass = 0.02f;
    [SerializeField] private float smoothingRadius = 0.1f;
    [SerializeField] private float targetDensity = 1000f;
    [SerializeField] private float pressureConstant = 200f;
    [SerializeField] private float viscosityConstant = 0.1f;
    [SerializeField] private float particleLifetime = 5f;
    [SerializeField] private Vector3 gravity = new Vector3(0, -9.81f, 0);
    
    [Header("Emission Settings")]
    [SerializeField] private float emissionRate = 100f;
    [SerializeField] private float emissionForce = 10f;
    [SerializeField] private GameObject particlePrefab;
    [SerializeField] private float particleScale = 0.1f;

    [Header("Water Storage Settings")]
    [SerializeField] private float maxStorage = 100f;
    [SerializeField] private float refillRate = 10f;
    [SerializeField] private float consumptionRate = 20f;
    
    private List<FluidParticle> particles = new List<FluidParticle>();
    private List<GameObject> particleObjects = new List<GameObject>();
    private float emissionTimer = 0f;
    private float smoothingRadiusSqr;
    private float currentStorage;
    private bool isSpraying = false;
    private const float MIN_BOOST_MULTIPLIER = 0.1f;
    private float storedBoostMultiplier;
    private Vector2 currentDirection;

    private void Start()
    {
        smoothingRadiusSqr = smoothingRadius * smoothingRadius;
        currentStorage = maxStorage;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        playerScript = player.GetComponent<BasicMovement>();
        playerRigidbody = player.GetComponent<Rigidbody2D>();
    }

    public void OnSpray(InputValue value)
    {
        if (value.isPressed && currentStorage > 0)
        {
            StartSpraying();
        }
        else
        {
            StopSpraying();
        }
    }

    private void StartSpraying()
    {
        if (currentStorage <= 0) return;

        float distance = Vector2.Distance(transform.position, cursor.position);
        storedBoostMultiplier = Mathf.Lerp(
            MIN_BOOST_MULTIPLIER,
            1f,
            1f - Mathf.Clamp01(distance / maxTreshold)
        );

        isSpraying = true;
    }

    private void StopSpraying()
    {
        isSpraying = false;
    }

    private void Update()
    {
        if (isSpraying && currentStorage > 0)
        {
            currentStorage = Mathf.Max(0, currentStorage - consumptionRate * Time.deltaTime);
            
            // Handle emission
            emissionTimer += Time.deltaTime;
            if (emissionTimer >= 1f / emissionRate)
            {
                EmitParticle();
                emissionTimer = 0f;
            }

            HandleWaterCollisions();
        }
        else 
        {
            currentStorage = Mathf.Min(maxStorage, currentStorage + refillRate * Time.deltaTime);
        }

        // Update simulation
        if (particles.Count > 0)
        {
            ComputeDensityPressure();
            ComputeForces();
            UpdatePositions();
            UpdateParticlesAndLifetime();
        }

        float distanceFromStart = Vector3.Distance(transform.position, cursor.position);
        Debug.Log(distanceFromStart);
    }

    // Water Path
    private Vector3 GetTargetPointOnPath(FluidParticle particle)
{
    float distanceFromStart = Vector3.Distance(particle.initialEmissionPoint, particle.position);
    return particle.initialEmissionPoint + particle.initialTargetDirection * distanceFromStart;
}

    private float GetMaxPathDistance()
    {
        // Pressure controls how far particles follow the path
        return Mathf.Lerp(minPathFollowDistance, maxPathFollowDistance, pressure);
    }

    private void UpdatePositions()
    {
        float dt = Time.deltaTime;
        float maxPathDistance = GetMaxPathDistance();

        for (int i = 0; i < particles.Count; i++)
        {
            FluidParticle p = particles[i];
            
            if (p.isDestroyed) continue;

            float distanceFromStart = Vector3.Distance(p.initialEmissionPoint, p.position);

            if (p.isFollowingPath && distanceFromStart < maxPathDistance)
            {
                Vector3 targetPoint = GetTargetPointOnPath(p);
                Vector3 randomOffset = Random.insideUnitSphere * pathWidth;
                randomOffset.z = 0;
                targetPoint += randomOffset;

                Vector3 towardsPath = (targetPoint - p.position);
                p.force = towardsPath * pathFollowStrength;
                p.velocity = Vector3.Lerp(p.velocity, p.initialTargetDirection * emissionForce, dt * 5f);
            }
            else
            {
                p.isFollowingPath = false;
                p.force += Vector3.down * fallSpeed;
                p.velocity.x *= 0.99f;
            }

            p.velocity += (p.force / p.density) * dt;
            Vector3 newPosition = p.position + p.velocity * dt;

            if (CheckParticleCollision(p.position, newPosition))
            {
                p.isDestroyed = true;
                continue;
            }

            p.position = newPosition;
        }
    }

    private bool CheckParticleCollision(Vector3 currentPos, Vector3 nextPos)
    {
        // Cast a small circle between current and next position to check for collisions
        RaycastHit2D hit = Physics2D.CircleCast(
            currentPos,
            collisionCheckRadius,
            (nextPos - currentPos).normalized,
            Vector3.Distance(currentPos, nextPos),
            particleCollisionMask
        );

        return hit.collider != null;
    }

    // Raycasts

    private void HandleWaterCollisions()
    {
        currentDirection = (cursor.position - transform.position).normalized;
        float maxDistance = Vector2.Distance(transform.position, cursor.position);
        float angle = hoseRotation.eulerAngles.z;
        Vector2 cursorPosition = cursor.position;

        // Combine all layers we want to check
        LayerMask combinedLayers = boostSurfaces | enemyLayer | waterPlatform | breakableWall | waterPushableLayer | fireLayer;

        RaycastHit2D hit = Physics2D.BoxCast(
            transform.position, 
            raycastSize, 
            angle,
            currentDirection, 
            maxDistance, 
            combinedLayers
        );

        if (hit.collider != null)
        {
            if (((1 << hit.collider.gameObject.layer) & waterPlatform) != 0)
            {
                if (Vector2.Distance(cursorPosition, hit.point) < hitArea)
                {
                    if (hit.collider.TryGetComponent<WaterPlatform>(out var platform))
                    {
                        platform.SetColliderState(true);
                        
                        if (hit.collider.enabled && isSpraying)
                        {
                            ApplyPlayerBoost();
                        }
                    }
                }
            }
            else if (((1 << hit.collider.gameObject.layer) & breakableWall) != 0)
            {
                if (Vector2.Distance(cursorPosition, hit.point) < hitArea)
                {
                    if (hit.collider.TryGetComponent<BreakableWall>(out var destructible))
                    {
                        destructible.TakeDamage(Time.deltaTime);
                    }
                }
            }
            else if (((1 << hit.collider.gameObject.layer) & enemyLayer) != 0)
            {
                if (hit.collider.TryGetComponent<Behaviour>(out var enemyScript))
                {
                    enemyScript.TakeDamage(Time.deltaTime);
                }
            }
            else if (((1 << hit.collider.gameObject.layer) & boostSurfaces) != 0)
            {
                if (isSpraying)
                {
                    ApplyPlayerBoost();
                }
            }
            else if (((1 << hit.collider.gameObject.layer) & waterPushableLayer) != 0)
            {
                if (hit.collider.TryGetComponent<Box>(out var pushable))
                {
                    pushable.ApplyWaterForce(transform.position);
                }
            }
            else if (((1 << hit.collider.gameObject.layer) & fireLayer) != 0)
            {
                if (hit.collider.TryGetComponent<Fire>(out var fireScript))
                {
                    fireScript.Extinguish(Time.deltaTime);
                }
            }
        }
    }

    private void ApplyPlayerBoost()
    {
        playerScript.SwitchHorizLock(true);
        Vector2 boostDirection = -currentDirection;
        float finalBoostForce = boostForce * storedBoostMultiplier;
        
        playerRigidbody.AddForce(boostDirection * finalBoostForce, ForceMode2D.Impulse);
    }

    // Fluid simulation methods
    
    private void EmitParticle()
{
    Vector3 emissionDirection = (cursor.position - transform.position).normalized;
    Vector3 randomOffset = Random.insideUnitSphere * 0.05f;
    Vector3 position = transform.position + randomOffset;
    
    // Store initial emission point and direction when creating particle
    FluidParticle particle = new FluidParticle(
        position, 
        particleLifetime, 
        transform.position,  // Store initial emission point
        emissionDirection   // Store initial direction
    );
    particle.velocity = emissionDirection * emissionForce;
    particles.Add(particle);

    GameObject particleObj = Instantiate(particlePrefab, position, Quaternion.identity);
    particleObj.transform.localScale = Vector3.one * particleScale;
    particleObjects.Add(particleObj);
}

    private void ComputeDensityPressure()
    {
        float h2 = smoothingRadiusSqr;
        float h6 = h2 * h2 * h2;
        float poly6Constant = 315f / (64f * Mathf.PI * h6);

        for (int i = 0; i < particles.Count; i++)
        {
            float density = 0f;
            FluidParticle pi = particles[i];

            for (int j = 0; j < particles.Count; j++)
            {
                FluidParticle pj = particles[j];
                Vector3 diff = pj.position - pi.position;
                float r2 = diff.sqrMagnitude;

                if (r2 < h2)
                {
                    density += particleMass * poly6Constant * Mathf.Pow(h2 - r2, 3);
                }
            }

            pi.density = density;
            pi.pressure = pressureConstant * (density - targetDensity);
        }
    }

    private void ComputeForces()
    {
        float h = smoothingRadius;
        float h2 = smoothingRadiusSqr;
        float h6 = h2 * h2 * h2;
        
        for (int i = 0; i < particles.Count; i++)
        {
            FluidParticle pi = particles[i];
            Vector3 forcePressure = Vector3.zero;
            Vector3 forceViscosity = Vector3.zero;

            for (int j = 0; j < particles.Count; j++)
            {
                if (i == j) continue;

                FluidParticle pj = particles[j];
                Vector3 diff = pj.position - pi.position;
                float r = diff.magnitude;

                if (r < h)
                {
                    // Pressure force
                    float pressureForce = -particleMass * (pi.pressure + pj.pressure) / (2f * pj.density);
                    Vector3 gradW = diff.normalized * pressureForce * (45f / (Mathf.PI * h6)) * Mathf.Pow(h - r, 2);
                    forcePressure += gradW;

                    // Viscosity force
                    float viscosityForce = viscosityConstant * particleMass * (pj.velocity - pi.velocity).magnitude / pj.density;
                    forceViscosity += viscosityForce * (45f / (Mathf.PI * h6)) * (h - r) * diff.normalized;
                }
            }

            pi.force = forcePressure + forceViscosity + gravity * pi.density;
        }
    }

    private void UpdateParticlesAndLifetime()
    {
        float dt = Time.deltaTime;
        
        // Iterate backwards for safe removal
        for (int i = particles.Count - 1; i >= 0; i--)
        {
            // Remove particles that are either destroyed or expired
            if (particles[i].isDestroyed || particles[i].lifetime <= 0)
            {
                // Destroy visual object
                Destroy(particleObjects[i]);
                particleObjects.RemoveAt(i);
                
                // Remove particle data
                particles.RemoveAt(i);
                continue;
            }
            
            // Update lifetime for surviving particles
            particles[i].lifetime -= dt;
            
            // Update visual position
            if (i < particleObjects.Count)
            {
                particleObjects[i].transform.position = particles[i].position;
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (transform != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, 0.1f);
            Gizmos.DrawRay(transform.position, transform.forward);
        }
    }
}
