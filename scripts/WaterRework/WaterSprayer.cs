using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(SPHSimulation))]
public class WaterSprayer : MonoBehaviour
{
    [Header("Spray Controls")]
    [SerializeField] private Transform cursor;
    [SerializeField] private float emissionRate = 100f;
    [SerializeField] private float sprayForce = 10f;
    [SerializeField] private float spraySpread = 0.05f;
    
    [Header("Water Resource")]
    [SerializeField] private float maxWaterStorage = 100f;
    [SerializeField] private float waterConsumptionRate = 20f;
    [SerializeField] private float waterRefillRate = 10f;

    private SPHSimulation sphSystem;
    private float currentWaterStorage;
    private float emissionTimer;
    private bool isSpraying;

    private void Awake()
    {
        sphSystem = GetComponent<SPHSimulation>();
        currentWaterStorage = maxWaterStorage;
    }

    private void Update()
    {
        if (isSpraying && currentWaterStorage > 0)
        {
            // Consume water
            currentWaterStorage = Mathf.Max(0, currentWaterStorage - waterConsumptionRate * Time.deltaTime);
            
            // Handle emission
            emissionTimer += Time.deltaTime;
            if (emissionTimer >= 1f / emissionRate)
            {
                EmitWaterParticles();
                emissionTimer = 0f;
            }
        }
        else
        {
            // Refill water storagea
            currentWaterStorage = Mathf.Min(maxWaterStorage, currentWaterStorage + waterRefillRate * Time.deltaTime);
        }
    }

    private void EmitWaterParticles()
    {
        Vector3 sprayDirection = (cursor.position - transform.position).normalized;
        
        // Calculate emission point with spread
        //Vector3 randomOffset = Random.insideUnitSphere * spraySpread;
        Vector3 emissionPoint = transform.position;
        Vector3 particleVelocity = sprayDirection;
        
        // Pass destination point (cursor position) to the particle
        sphSystem.EmitParticle(emissionPoint, particleVelocity.normalized * sprayForce, cursor.position);
    }

    // Input System callback
    public void OnSpray(InputValue value)
    {
        isSpraying = value.isPressed && currentWaterStorage > 0;
    }

    // Optional: Public methods for external control
    public void StartSpray()
    {
        if (currentWaterStorage > 0)
        {
            isSpraying = true;
        }
    }

    public void StopSpray()
    {
        isSpraying = false;
    }

    public float GetWaterLevel()
    {
        return currentWaterStorage / maxWaterStorage;
    }
}
