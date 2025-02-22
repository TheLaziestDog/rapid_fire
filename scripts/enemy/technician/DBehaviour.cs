using UnityEngine;
using Pathfinding;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

public class DBehaviour : MonoBehaviour
{
    [SerializeField] private float speed = 5f;
    [Header("Range")]
    [SerializeField] private float attackRange = 5;
    [SerializeField] private float lineOfSight = 5f;

    [Header("HP")]
    [SerializeField] private float currentHP = 100;
    [SerializeField] private float damage = 1f;
    [Header("Attack")]
    [SerializeField] private float fireRate = 1f;
    private float nextFireTime = 0f;

    [Header("Spacing")]
    [SerializeField] private float distanceLimit = 10f;
    [SerializeField] private float minimumDroneDistance = 2f;
    [SerializeField] private float heightOffset = 2f;
    [SerializeField] private float retreatMultiplier = 1.5f;

    [Header("Patrol")]
    [SerializeField] private float patrolObstacleCheckDistance = 2f; // Distance to check for obstacles
    [SerializeField] private LayerMask obstacleLayer; // Layer mask for obstacles
    
    private AIPath path;
    private Transform target;
    private float targetDistance;
    private TBehaviour turret;
    private Animator animator;
    public Shoot shoot;
    private Vector2 patrolCenterPosition;
    private int patrolDirection = -1;
    private Vector3 desiredPosition;

    private void Start()
    {
        animator = GetComponent<Animator>();
        path = GetComponent<AIPath>();
        target = GameObject.FindGameObjectWithTag("Player").GetComponent<Transform>();
        patrolCenterPosition = transform.position; // Store initial position as patrol center

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
        animator.SetBool("isShooting", false);
    }

    private void Update()
    {
        if (target == null || path == null || turret == null) return;
        
        targetDistance = Vector2.Distance(transform.position, target.position);
        path.maxSpeed = speed;
        
        if (targetDistance < distanceLimit)
        {
            desiredPosition = CalculateRetreatPosition();
        }
        else if (targetDistance < lineOfSight)
        {
            desiredPosition = CalculateDesiredPosition();
        }
        else
        {
            Patrol();
        }

        if (targetDistance <= attackRange){
            if (Time.time >= nextFireTime){
                StartCoroutine(shootTarget());
                nextFireTime = Time.time + 1f / fireRate;
            }
        }

        path.destination = desiredPosition;
    }

    private void Patrol()
    {
        // Check for obstacles on both sides
        RaycastHit2D leftHit = Physics2D.Raycast(transform.position, Vector2.left, lineOfSight, obstacleLayer);
        RaycastHit2D rightHit = Physics2D.Raycast(transform.position, Vector2.right, lineOfSight, obstacleLayer);

        // Calculate distances to obstacles (if they exist)
        float leftDistance = leftHit.collider != null ? leftHit.distance : float.MaxValue;
        float rightDistance = rightHit.collider != null ? rightHit.distance : float.MaxValue;

        // Change direction if too close to an obstacle
        if (leftDistance <= patrolObstacleCheckDistance || rightDistance <= patrolObstacleCheckDistance)
        {
            // If obstacle is closer on the left, move right (and vice versa)
            patrolDirection = (leftDistance < rightDistance) ? 1 : -1;
        }
        // Change direction if reached patrol limit
        else if (Mathf.Abs(transform.position.x - patrolCenterPosition.x) >= lineOfSight)
        {
            patrolDirection *= -1;
            patrolCenterPosition = transform.position; // Update patrol center
        }

        // Move the drone
        Vector2 movement = new Vector2(patrolDirection * speed * Time.deltaTime, 0);
        transform.Translate(movement);
        desiredPosition = transform.position;
    }

    private IEnumerator shootTarget(){
        animator.SetBool("isShooting", true);
        yield return new WaitForSeconds(1.5f);
        shoot.justShoot(target, 4);
        animator.SetBool("isShooting", false);
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
        // Existing gizmos
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, lineOfSight);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, distanceLimit);

        // New patrol obstacle detection gizmos
        if (Application.isPlaying)
        {
            // Draw patrol obstacle detection rays
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, Vector2.left * lineOfSight);
            Gizmos.DrawRay(transform.position, Vector2.right * lineOfSight);

            // Draw minimum obstacle distance threshold
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, patrolObstacleCheckDistance);
        }
    }

    public void SetTurret(TBehaviour turret)
    {
        this.turret = turret;
    }
}
