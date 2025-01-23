/*
Pressure control based on the distance
*/

using System.Collections;
using System.Collections.Generic;
using Unity.Burst.Intrinsics;
using UnityEngine;
using UnityEngine.InputSystem;

public class water : MonoBehaviour
{
    [Header("External")]
    [SerializeField] private Transform hoseTip;
    [SerializeField] private Transform hoseRotation;
    [SerializeField] private Transform cursor;    
    [SerializeField] private player playerScript;

    [Header("Water Settings")]
    [SerializeField] private GameObject waterPrefab;
    [SerializeField] private float waterLifetime = 1f;
    [SerializeField] private float minScale = 0.05f;
    [SerializeField] private float scaleSpeed = 0.05f;
    [SerializeField] private float maxTreshold = 10f;
    
    [Header("Boost Settings")]
    [SerializeField] private float boostForce = 0.1f;
    [SerializeField] private LayerMask boostSurfaces;
    [SerializeField] private Rigidbody2D playerRigidbody;

    [Header("Water Storage Settings")]
    [SerializeField] private float maxStorage = 100f; // Maximum water storage
    [SerializeField] private float refillRate = 10f;  // Rate at which water refills
    [SerializeField] private float consumptionRate = 20f; // Water consumption per second of spraying
    private float currentStorage; // Current water storage

    private GameObject currentWater;
    private bool _isHolding;
    private float _currentScale;
    private float _targetScale;
    private bool _collisionDetected;
    private Vector2 _lastHitNormal;
    private float _maxScale; // Store the maximum scale when hitting something

    private void Start()
    {
        currentStorage = maxStorage; // Initialize storage to full
    }

    private void OnSpray(InputValue value)
    {
        if (value.isPressed && currentStorage > 0)
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
        if (currentStorage <= 0) return; // Prevent spraying if no water is left

        _isHolding = true;
        _collisionDetected = false;
        _currentScale = minScale;
        _maxScale = float.MaxValue; // Reset max scale

        currentWater = Instantiate(waterPrefab, hoseTip.position, hoseRotation.rotation);
        UpdateWaterStream();
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
        float distance = Vector2.Distance(hoseTip.position, cursor.position);
        Debug.Log($"Distance between hose and cursor: {distance}");

        if (_isHolding && currentWater != null)
        {
            if (currentStorage > 0)
            {
                currentStorage -= consumptionRate * Time.deltaTime; // Decrease storage
                currentStorage = Mathf.Max(currentStorage, 0); // Clamp to zero
                CheckCollisionAndUpdateMaxScale();
                UpdateWaterStream();
            }
            else
            {
                StopSpray(); // Stop spraying if storage is depleted
            }
        }
        else
        {
            RefillWater(); // Refill storage when not spraying
        }
    }

    private void RefillWater()
    {
        currentStorage += refillRate * Time.deltaTime;
        currentStorage = Mathf.Min(currentStorage, maxStorage); // Clamp to maxStorage
    }

    private void CheckCollisionAndUpdateMaxScale()
    {
        Vector2 direction = (cursor.position - hoseTip.position).normalized;
        
        RaycastHit2D hit = Physics2D.BoxCast(
            hoseTip.position,
            new Vector2(0.2f, 0.2f),
            currentWater.transform.eulerAngles.z,
            direction,
            Vector2.Distance(hoseTip.position, cursor.position),
            boostSurfaces
        );
        
        if (hit.collider != null)
        {
            _collisionDetected = true;
            _maxScale = hit.distance; // Set max scale to the collision distance
            
            if (_isHolding)
            {
                ApplyBoost();
            }
        }
    }

    private void UpdateWaterStream()
    {
        // Update water position to always start from hose tip
        currentWater.transform.position = hoseTip.position;
        
        // Calculate direction to cursor
        Vector2 directionToCursor = (cursor.position - hoseTip.position).normalized;
        
        // Update water rotation to point towards cursor
        float angle = Mathf.Atan2(directionToCursor.y, directionToCursor.x) * Mathf.Rad2Deg;
        currentWater.transform.rotation = Quaternion.Euler(0, 0, angle);
        
        // Calculate target scale, but don't exceed max scale from collision
        float distanceToCursor = Vector2.Distance(hoseTip.position, cursor.position);
        _targetScale = Mathf.Min(distanceToCursor, _maxScale);
        _currentScale = Mathf.MoveTowards(_currentScale, _targetScale, scaleSpeed * Time.deltaTime);
        
        // Apply scale - only stretch on X axis to maintain water thickness
        Vector3 waterScale = currentWater.transform.localScale;
        waterScale.x = _currentScale;
        waterScale.y = waterScale.y; // Keep original Y scale
        waterScale.z = 0;
        currentWater.transform.localScale = waterScale;
    }

    public void ApplyBoost()
    {        
        playerScript.SwitchHorizLock(true);
        Vector2 waterDirection = (cursor.position - hoseTip.position).normalized;
        Vector2 boostDirection = -waterDirection;

        float distanceToMax = Vector2.Distance(hoseTip.position, cursor.position);
        float boostMultiplier = 1f - Mathf.Clamp01(distanceToMax / maxTreshold);
        float finalBoostForce = boostForce * boostMultiplier;   

        playerRigidbody.AddForce(boostDirection * finalBoostForce, ForceMode2D.Impulse);
        
        if (currentWater != null)
        {
            Vector3 waterScale = currentWater.transform.localScale;
            waterScale.x = _currentScale * 1.5f;
            currentWater.transform.localScale = waterScale;
        }
    }
}
