using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class water : MonoBehaviour
{
    [Header("Water Settings")]
    [SerializeField] private GameObject waterPrefab;
    [SerializeField] private Transform hoseTip;
    [SerializeField] private Transform hoseRotation;
    [SerializeField] private Transform cursor;
    [SerializeField] private float waterLifetime = 1f;
    [SerializeField] private float minScale = 0.05f;
    [SerializeField] private float scaleSpeed = 0.05f;
    
    [Header("Boost Settings")]
    [SerializeField] private float boostForce = 10f;
    [SerializeField] private LayerMask boostSurfaces; // Surfaces that can be used for boosting
    [SerializeField] private Rigidbody2D playerRigidbody; // Reference to player's rigidbody

    private GameObject currentWater;
    private bool _isHolding;
    private float _currentScale;
    private float _targetScale;
    private bool _collisionDetected;
    private Vector2 _lastHitNormal; // Store the normal of the surface we hit

    private void OnSpray(InputValue value)
    {
        if (value.isPressed)
        {
            StartSpray();
        }
        else
        {
            StopSpray();
        }
    }

    private void StartSpray()
    {
        _isHolding = true;
        _collisionDetected = false;
        _currentScale = minScale;

        currentWater = Instantiate(waterPrefab, hoseTip.position, hoseRotation.rotation);
        Vector3 waterScale = currentWater.transform.localScale;
        waterScale.x = _currentScale;
        currentWater.transform.localScale = waterScale;

        // Check for surfaces that can be used for boosting
        CheckForBoostSurface();
    }

    private void StopSpray()
    {
        _isHolding = false;
        if (currentWater != null)
        {
            Destroy(currentWater, waterLifetime);
        }
    }

    private void Update()
    {
        if (_isHolding && currentWater != null)
        {
            // Calculate target scale based on distance to cursor
            _targetScale = Vector2.Distance(hoseTip.position, cursor.position);
            _currentScale = Mathf.MoveTowards(_currentScale, _targetScale, scaleSpeed * Time.deltaTime);

            // Apply scale
            Vector3 waterScale = currentWater.transform.localScale;
            waterScale.x = _currentScale;
            waterScale.z = 0;
            currentWater.transform.localScale = waterScale;

            // Continuously check for boost surfaces
            CheckForBoostSurface();
        }
    }

    private void CheckForBoostSurface()
    {
        Vector2 direction = (cursor.position - hoseTip.position).normalized;
        float distance = Vector2.Distance(hoseTip.position, cursor.position);
        
        // Use multiple raycasts or a boxcast for better collision detection
        RaycastHit2D hit = Physics2D.BoxCast(
            hoseTip.position,          // origin
            new Vector2(0.2f, 0.2f),   // size of the box
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, // angle
            direction,                 // direction
            distance,                 // distance
            boostSurfaces            // layer mask
        );
        
        if (hit.collider != null)
        {
            _lastHitNormal = hit.normal;
            _collisionDetected = true;
            Debug.Log($"Collision detected with normal: {_lastHitNormal}");
            if (_isHolding)
            {
                ApplyBoost();
            }
        }
    }

    // Call this method when player jumps
    public void ApplyBoost()
    {
        // Calculate the actual water direction from hoseTip to cursor
        Vector2 waterDirection = (cursor.position - hoseTip.position).normalized;
        Debug.Log(waterDirection);
        
        // Calculate boost direction based on water direction and surface normal
        Vector2 boostDirection = Vector2.Reflect(waterDirection, _lastHitNormal).normalized;
        
        // Apply the boost force
        playerRigidbody.AddForce(boostDirection * boostForce, ForceMode2D.Impulse);
        
        // Visual feedback remains the same
        if (currentWater != null)
        {
            Vector3 waterScale = currentWater.transform.localScale;
            waterScale.x = _currentScale * 1.5f;
            currentWater.transform.localScale = waterScale;
        }
    }
}
