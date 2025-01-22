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
    [SerializeField] private LayerMask boostSurfaces;
    [SerializeField] private Rigidbody2D playerRigidbody;

    private GameObject currentWater;
    private bool _isHolding;
    private float _currentScale;
    private float _targetScale;
    private bool _collisionDetected;
    private Vector2 _lastHitNormal;
    private float _maxScale; // Store the maximum scale when hitting something

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
        if (_isHolding && currentWater != null)
        {
            CheckCollisionAndUpdateMaxScale();
            UpdateWaterStream();
        }
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
        Vector2 waterDirection = (cursor.position - hoseTip.position).normalized;
        Debug.Log("water dir. : " + waterDirection);
        Vector2 boostDirection = -waterDirection;
        Debug.Log("boost dir. : " + boostDirection);
        Debug.Log("force : " + boostDirection * boostForce);

        playerRigidbody.AddForce(boostDirection * boostForce, ForceMode2D.Impulse);
        
        if (currentWater != null)
        {
            Vector3 waterScale = currentWater.transform.localScale;
            waterScale.x = _currentScale * 1.5f;
            currentWater.transform.localScale = waterScale;
        }
    }
}
