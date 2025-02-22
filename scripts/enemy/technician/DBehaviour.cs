using UnityEngine;
using Pathfinding;
using System.Collections.Generic;
using System.Linq;

public class DBehaviour : MonoBehaviour
{
    [SerializeField] private float speed = 5f;
    [SerializeField] private float distanceLimit = 10f;
    [SerializeField] private float lineOfSight = 5f;
    [Header("HP")]
    [SerializeField] private float currentHP = 100;
    [SerializeField] private float damage = 1f;
    [Header("Spacing")]
    [SerializeField] private float minimumDroneDistance = 2f;
    [SerializeField] private float heightOffset = 2f;
    [SerializeField] private float retreatMultiplier = 1.5f; // How far the drone should move when retreating
    
    private AIPath path;
    private Transform target;
    private float targetDistance;
    private TBehaviour turret;

    private void Start()
    {
        path = GetComponent<AIPath>();
        target = GameObject.FindGameObjectWithTag("Player").GetComponent<Transform>();
        if (target == null)
        {
            Debug.LogError("Player not found. Make sure the Player has the 'Player' tag.");
            enabled = false;
        }
        if (path == null)
        {
            Debug.LogError("AIPath component not found. Please add it to this GameObject.");
            enabled = false;
        }
    }

    private void Update()
    {
        if (target == null || path == null || turret == null) return;
        
        targetDistance = Vector2.Distance(transform.position, target.position);
        path.maxSpeed = speed;

        Vector3 desiredPosition;
        
        if (targetDistance < distanceLimit) // Too close to player
        {
            desiredPosition = CalculateRetreatPosition();
        }
        else if (targetDistance < lineOfSight) // Within acceptable range
        {
            desiredPosition = CalculateDesiredPosition();
        }
        else // Too far from player
        {
            desiredPosition = transform.position;
        }

        path.destination = desiredPosition;
    }

    private Vector3 CalculateRetreatPosition()
    {
        // Calculate direction from player to drone
        Vector3 directionFromPlayer = (transform.position - target.position).normalized;
        
        // Calculate how far we need to move to reach minimum distance
        float distanceToMove = (distanceLimit - targetDistance) * retreatMultiplier;
        
        // Calculate retreat position
        Vector3 retreatPosition = target.position + directionFromPlayer * (distanceLimit + distanceToMove);
        retreatPosition.y = transform.position.y; // Maintain current height
        
        // Apply drone spacing
        List<DBehaviour> nearbyDrones = GetNearbyDrones();
        if (nearbyDrones.Any())
        {
            Vector3 separationVector = CalculateSeparationVector(nearbyDrones);
            retreatPosition += separationVector;
        }
        
        return retreatPosition;
    }

    private Vector3 CalculateDesiredPosition()
    {
        Vector3 targetPosition = target.position + Vector3.up * heightOffset;

        List<DBehaviour> nearbyDrones = GetNearbyDrones();
        if (nearbyDrones.Any())
        {
            Vector3 separationVector = CalculateSeparationVector(nearbyDrones);
            targetPosition += separationVector;
        }

        return targetPosition;
    }

    private List<DBehaviour> GetNearbyDrones()
    {
        List<DBehaviour> allDrones = turret.GetActiveDrones();
        List<DBehaviour> nearbyDrones = new List<DBehaviour>();
        foreach (var drone in allDrones)
        {
            if (drone != this && Vector2.Distance(transform.position, drone.transform.position) < minimumDroneDistance)
            {
                nearbyDrones.Add(drone);
            }
        }
        return nearbyDrones;
    }

    private Vector3 CalculateSeparationVector(List<DBehaviour> nearbyDrones)
    {
        Vector3 separationVector = Vector3.zero;
        foreach (var drone in nearbyDrones)
        {
            Vector3 direction = (transform.position - drone.transform.position).normalized;
            separationVector += direction;
        }
        return separationVector.normalized;
    }

    public void DestroyDrone()
    {
        turret.OnDroneDestroyed(this);
        Destroy(gameObject);
    }

    public void TakeDamage()
    {
        currentHP -= damage;
        if (currentHP <= 0)
        {
            DestroyDrone();
        }
    }

    private void OnDestroy()
    {
        if (turret != null)
        {
            turret.OnDroneDestroyed(this);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, lineOfSight);
        
        // Add visualization for minimum distance
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, distanceLimit);
    }

    public void SetTurret(TBehaviour turret)
    {
        this.turret = turret;
    }
}
