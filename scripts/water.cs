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
    [SerializeField] private float scaleSpeed = 40f; // Units per second (can be adjusted in inspector)

    private GameObject currentWater;
    private Vector3 _mouseWorldPos;
    private bool _isHolding;
    private float _currentScale;
    private float _targetScale;

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
        _currentScale = minScale;
        currentWater = Instantiate(waterPrefab, hoseTip.position, hoseRotation.rotation);
        
        Vector3 waterScale = currentWater.transform.localScale;
        waterScale.x = _currentScale;
        currentWater.transform.localScale = waterScale;
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
            
            // Increment scale at constant speed
            _currentScale = Mathf.MoveTowards(_currentScale, _targetScale, scaleSpeed * Time.deltaTime);
            
            // Apply scale
            Vector3 waterScale = currentWater.transform.localScale;
            waterScale.x = _currentScale;
            currentWater.transform.localScale = waterScale;
        }
    }
}
