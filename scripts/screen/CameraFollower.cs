using UnityEngine;
using System.Collections;

public class CameraFollower : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private Vector3 offset = new Vector3(0, 0, -10);
    [SerializeField] private float maxX, maxY;
    [SerializeField] private float minX, minY;

    [Header("Shake Parameters")]
    [SerializeField] private float shakeIntensity = 0.1f;
    [SerializeField] private float shakeDuration = 0.1f;
    [SerializeField] private float shakeDecreaseFactor = 1.0f;
    [SerializeField] private float shakeCooldown = 0.5f;
    [SerializeField] private float shakeRadius = 1f;

    [Header("Dynamic Scale Parameters")]
    [SerializeField] private float normalScale = 5f;
    [SerializeField] private float boostedScale = 6f; // Now acts as maximum scale
    [SerializeField] private float scaleTransitionSpeed = 3f;
    [SerializeField] private float velocityScaleFactor = 0.1f; // How much velocity affects scale
    [SerializeField] private float minVelocityThreshold = 2f; // Minimum velocity to start scaling
    
    private Camera mainCamera;
    private Vector3 originalPosition;
    private float currentShakeDuration = 0f;
    private bool isShaking = false;
    private Vector2 lastShakePosition;
    private float cooldownTimer = 0f;
    private bool isBoostScaling = false;
    private Rigidbody2D playerRigidbody;

    private void Awake()
    {
        mainCamera = GetComponent<Camera>();
        mainCamera.orthographicSize = normalScale;
        
        // Get player rigidbody reference
        if (target != null)
        {
            playerRigidbody = target.GetComponent<Rigidbody2D>();
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Update cooldown timer
        if (cooldownTimer > 0)
        {
            cooldownTimer -= Time.deltaTime;
        }

        Vector3 desiredPosition = target.position + offset;
        
        // Add boundaries
        desiredPosition.x = Mathf.Clamp(desiredPosition.x, minX, maxX);
        desiredPosition.y = Mathf.Clamp(desiredPosition.y, minY, maxY);
        
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        // Apply shake if active
        if (isShaking)
        {
            smoothedPosition += GetShakeOffset();
            UpdateShake();
        }

        transform.position = smoothedPosition;

        // Update camera scale based on velocity when boosting
        UpdateDynamicScale();
    }

    private void UpdateDynamicScale()
    {
        float targetScale = normalScale;

        if (isBoostScaling && playerRigidbody != null)
        {
            float currentVelocity = playerRigidbody.velocity.magnitude;
            
            // Only scale if velocity is above threshold
            if (currentVelocity > minVelocityThreshold)
            {
                // Calculate scale based on velocity
                float velocityScale = normalScale + (currentVelocity * velocityScaleFactor);
                // Clamp to maximum scale
                targetScale = Mathf.Clamp(velocityScale, normalScale, boostedScale);
            }
        }

        // Smoothly transition to target scale
        mainCamera.orthographicSize = Mathf.Lerp(
            mainCamera.orthographicSize, 
            targetScale, 
            scaleTransitionSpeed * Time.deltaTime
        );
    }

    private Vector3 GetShakeOffset()
    {
        float shakePercentage = currentShakeDuration / shakeDuration;
        float currentIntensity = shakeIntensity * shakePercentage;
        
        return new Vector3(
            Random.Range(-1f, 1f) * currentIntensity,
            Random.Range(-1f, 1f) * currentIntensity,
            0
        );
    }

    private void UpdateShake()
    {
        if (currentShakeDuration > 0)
        {
            currentShakeDuration -= Time.deltaTime * shakeDecreaseFactor;
            if (currentShakeDuration <= 0)
            {
                isShaking = false;
            }
        }
    }

    public bool TryShake(Vector2 impactPosition)
    {
        if (cooldownTimer > 0 || Vector2.Distance(impactPosition, lastShakePosition) < shakeRadius)
        {
            return false;
        }

        currentShakeDuration = shakeDuration;
        isShaking = true;
        lastShakePosition = impactPosition;
        cooldownTimer = shakeCooldown;
        return true;
    }

    public void SetBoostScale(bool isBoostActive)
    {
        isBoostScaling = isBoostActive;
    }
}
