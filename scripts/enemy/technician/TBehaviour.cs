using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class TBehaviour : MonoBehaviour
{
    [Header("Detection")]
    public float detectionRange = 5f;
    public LayerMask playerLayer;
    public LayerMask obstacleLayer;
    public LayerMask droneLayer;

    [Header("Drone Management")]
    [SerializeField] private int maxDroneCount = 5;
    [SerializeField] private int initialDroneCount = 3;
    [SerializeField] private float deployCooldown = 3f;
    [SerializeField] private GameObject dronePrefab;
    [SerializeField] private Transform droneSpawner;
    [SerializeField] private Transform playerTransform;

    private List<DBehaviour> activeDrones = new List<DBehaviour>();
    private bool initialDeploymentDone = false;
    private bool maxDronesReached = false;
    private float nextDeployTime;

    [Header("Child Activation")]
    [SerializeField] private GameObject childToActivate;
    [SerializeField] private float activationDuration = 0.5f;
    private bool isDeploying = false; // New flag to track deployment state

    [Header("HP")]
    [SerializeField] private float maxHP = 3f;
    [SerializeField] private float currentHP;
    [SerializeField] private float damagePerSecond = 1f;

    private void Start()
    {
        currentHP = maxHP;
        nextDeployTime = Time.time; // Initialize to current time

        if (childToActivate != null)
        {
            childToActivate.SetActive(false);
        }

        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }
    }

    private void Update()
    {
        activeDrones.RemoveAll(d => d == null);

        if (activeDrones.Count >= maxDroneCount)
        {
            maxDronesReached = true;
        }

        Collider2D playerInSight = GetPlayerInSight();

        // Handle initial deployment
        if (playerInSight != null && !initialDeploymentDone && !maxDronesReached)
        {
            StartCoroutine(DeployInitialDrones());
            initialDeploymentDone = true;
            nextDeployTime = Time.time + deployCooldown; // Set initial cooldown after first deployment
        }

        // Handle regular deployment with cooldown
        if (playerInSight != null && initialDeploymentDone && !maxDronesReached && !isDeploying)
        {
            if (Time.time >= nextDeployTime)
            {
                StartCoroutine(DeployDroneWithCooldown());
            }
        }
    }

    private IEnumerator DeployDroneWithCooldown()
    {
        isDeploying = true;
        DeployMoreDrones();
        nextDeployTime = Time.time + deployCooldown;
        yield return new WaitForSeconds(activationDuration);
        isDeploying = false;
    }

    private IEnumerator DeployInitialDrones()
    {
        for (int i = 0; i < initialDroneCount; i++)
        {
            if (!maxDronesReached)
            {
                yield return new WaitForSeconds(0.2f);
                StartCoroutine(DeployDroneRoutine());
            }
            else
            {
                yield break;
            }
        }
    }

    private IEnumerator DeployDroneRoutine()
    {
        if (maxDronesReached)
        {
            yield break;
        }

        if (childToActivate != null)
        {
            childToActivate.SetActive(true);
            yield return new WaitForSeconds(activationDuration);
            childToActivate.SetActive(false);
        }

        GameObject droneObj = Instantiate(dronePrefab, droneSpawner.position, Quaternion.identity);
        DBehaviour droneBehaviour = droneObj.GetComponent<DBehaviour>();

        if (droneBehaviour != null && playerTransform != null)
        {
            activeDrones.Add(droneBehaviour);
            droneBehaviour.SetTurret(this);

            if (activeDrones.Count >= maxDroneCount)
            {
                maxDronesReached = true;
            }
        }
    }

    private Collider2D GetPlayerInSight()
    {
        Collider2D[] potentialTargets = Physics2D.OverlapCircleAll(transform.position, detectionRange, playerLayer);
        foreach (Collider2D target in potentialTargets)
        {
            Vector2 directionToTarget = (target.transform.position - transform.position).normalized;
            float distanceToTarget = Vector2.Distance(transform.position, target.transform.position);
            RaycastHit2D hit = Physics2D.Raycast(transform.position, directionToTarget, distanceToTarget, obstacleLayer);

            if (hit.collider == null)
            {
                return target;
            }
        }
        return null;
    }

    public List<DBehaviour> GetActiveDrones()
    {
        return activeDrones;
    }


    // Called by drones when they spot the player (now ignores if max is reached)
    public void DeployMoreDrones()
    {
        if (!maxDronesReached && activeDrones.Count < maxDroneCount)
        {
            StartCoroutine(DeployDroneRoutine());
        }
    }

    // Called by drones when they are destroyed (now irrelevant, but kept for potential future use)
    public void OnDroneDestroyed(DBehaviour drone)
    {
        activeDrones.Remove(drone);
        activeDrones.RemoveAll(d => d == null);
    }

    public void TakeDamage()
    {
        float damage = damagePerSecond;
        currentHP -= damage;
        if (currentHP <= 0)
        {
            DestroyEnemy();
        }
    }

    public void DestroyEnemy()
    {
        Destroy(gameObject);
    }

    // Visualization of detection range in scene view
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Visualize actual detection rays for debugging
        if (Application.isPlaying)
        {
            Collider2D[] targets = Physics2D.OverlapCircleAll(transform.position, detectionRange, playerLayer);
            foreach (Collider2D target in targets)
            {
                Vector2 direction = (target.transform.position - transform.position).normalized;
                float distance = Vector2.Distance(transform.position, target.transform.position);
                RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, distance, obstacleLayer);

                if (hit.collider == null)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(transform.position, target.transform.position);
                }
                else
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(transform.position, hit.point);
                }
            }
        }
    }
}
