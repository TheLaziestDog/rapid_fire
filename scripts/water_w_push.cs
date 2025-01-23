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
    private float _minBoostMultiplier = 0.1f;
    private float _initialBoostDistance;
    
    [Header("Boost Settings")]
    [SerializeField] private float boostForce = 0.1f;
    [SerializeField] private LayerMask boostSurfaces; // Layer for character boosting surfaces
    [SerializeField] private LayerMask pushableSurfaces; // New layer for pushable objects
    [SerializeField] private Rigidbody2D playerRigidbody;

    [Header("Water Storage Settings")]
    [SerializeField] private float maxStorage = 100f;
    [SerializeField] private float refillRate = 10f;
    [SerializeField] private float consumptionRate = 20f;
    private float currentStorage;

    [Header("Push Settings")]
    [SerializeField] private float pushForce = 0.5f; // Force applied to pushable objects

    private GameObject currentWater;
    private bool _isHolding;
    private float _currentScale;
    private float _targetScale;
    private bool _collisionDetected;
    private float _maxScale;
    private float _storedBoostMultiplier;

    private void Start()
    {
        currentStorage = maxStorage;
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
        if (currentStorage <= 0) return;
        
        _initialBoostDistance = Vector2.Distance(hoseTip.position, cursor.position);
        
        float boostMultiplier = Mathf.Lerp(
            _minBoostMultiplier, 
            1f, 
            1f - Mathf.Clamp01(_initialBoostDistance / maxTreshold)
        );
        
        _storedBoostMultiplier = boostMultiplier;

        _isHolding = true;
        _collisionDetected = false;
        _currentScale = minScale;
        _maxScale = float.MaxValue;

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
        if (_isHolding && currentWater != null)
        {
            if (currentStorage > 0)
            {
                currentStorage -= consumptionRate * Time.deltaTime;
                currentStorage = Mathf.Max(currentStorage, 0);
                CheckCollisionAndUpdateMaxScale();
                UpdateWaterStream();
            }
            else
            {
                StopSpray();
            }
        }
        else
        {
            RefillWater();
        }
    }

    private void RefillWater()
    {
        currentStorage += refillRate * Time.deltaTime;
        currentStorage = Mathf.Min(currentStorage, maxStorage);
    }

    private void CheckCollisionAndUpdateMaxScale()
    {
        Vector2 direction = (cursor.position - hoseTip.position).normalized;
        float maxDistance = Vector2.Distance(hoseTip.position, cursor.position);
        
        // Check for pushable objects first
        RaycastHit2D pushHit = Physics2D.BoxCast(
            hoseTip.position,
            new Vector2(0.2f, 0.2f),
            currentWater.transform.eulerAngles.z,
            direction,
            maxDistance,
            pushableSurfaces
        );
        
        // Check for boost surfaces
        RaycastHit2D boostHit = Physics2D.BoxCast(
            hoseTip.position,
            new Vector2(0.2f, 0.2f),
            currentWater.transform.eulerAngles.z,
            direction,
            maxDistance,
            boostSurfaces
        );
        
        // If a pushable object is hit before the boost surface, prevent boosting
        if (pushHit.collider != null && 
            (boostHit.collider == null || pushHit.distance < boostHit.distance))
        {
            _collisionDetected = true;
            _maxScale = pushHit.distance;
            
            if (_isHolding)
            {
                ApplyObjectPush(pushHit.collider.gameObject);
            }
        }
        // Only boost if boost surface is hit and no pushable object is closer
        else if (boostHit.collider != null && 
                 (pushHit.collider == null || boostHit.distance < pushHit.distance))
        {
            _collisionDetected = true;
            _maxScale = boostHit.distance;
            
            if (_isHolding)
            {
                ApplyPlayerBoost();
            }
        }
    }

    private void UpdateWaterStream()
    {
        currentWater.transform.position = hoseTip.position;
        
        Vector2 directionToCursor = (cursor.position - hoseTip.position).normalized;
        
        float angle = Mathf.Atan2(directionToCursor.y, directionToCursor.x) * Mathf.Rad2Deg;
        currentWater.transform.rotation = Quaternion.Euler(0, 0, angle);
        
        float distanceToCursor = Vector2.Distance(hoseTip.position, cursor.position);
        _targetScale = Mathf.Min(distanceToCursor, _maxScale);
        _currentScale = Mathf.MoveTowards(_currentScale, _targetScale, scaleSpeed * Time.deltaTime);
        
        Vector3 waterScale = currentWater.transform.localScale;
        waterScale.x = _currentScale;
        waterScale.z = 0;
        currentWater.transform.localScale = waterScale;
    }

    private void ApplyPlayerBoost()
    {        
        playerScript.SwitchHorizLock(true);
        Vector2 waterDirection = (cursor.position - hoseTip.position).normalized;
        Vector2 boostDirection = -waterDirection;

        float finalBoostForce = boostForce * _storedBoostMultiplier;

        playerRigidbody.AddForce(boostDirection * finalBoostForce, ForceMode2D.Impulse);
        
        if (currentWater != null)
        {
            Vector3 waterScale = currentWater.transform.localScale;
            waterScale.x = _currentScale * 1.5f;
            currentWater.transform.localScale = waterScale;
        }
    }

    private void ApplyObjectPush(GameObject hitObject)
    {
        Rigidbody2D objectRigidbody = hitObject.GetComponent<Rigidbody2D>();
        
        if (objectRigidbody != null)
        {
            Vector2 waterDirection = (cursor.position - hoseTip.position).normalized;
            Vector2 pushDirection = waterDirection;

            float finalPushForce = pushForce * _storedBoostMultiplier;

            objectRigidbody.AddForce(pushDirection * finalPushForce, ForceMode2D.Impulse);
        }
    }
}
