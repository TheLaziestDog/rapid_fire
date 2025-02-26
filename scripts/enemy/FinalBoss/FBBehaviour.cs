using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class FBBehaviour : MonoBehaviour
{
    [Header("HP")]
    [SerializeField] private float currentHP = 1000f;
    [SerializeField] private float damagePerSecond = 1f;
    
    [Header("Skill")]
    [SerializeField] private float lineOfSight;
    [SerializeField] private GameObject fallingBox;
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float boxSpawnHeight = 13;
    [SerializeField] private float cooldownTime = 2f; // Time between box spawns in seconds
    [SerializeField] private float fallboxTreshold = 5f; // Time between box spawns in seconds    
    [SerializeField] private float spawnDroneTreshold = 5f; // Time between box spawns in seconds    

    private Transform playerTransform;
    private float cooldownTimer = 0f; // Timer to track cooldown
    private bool canSpawn = true; // Flag to check if spawning is allowed
    private bool stun = false; // -> this will have more uses in the future.
    private int stunUsed = 0;
    
    void Start()
    {
        playerTransform = GameObject.FindGameObjectWithTag("Player").GetComponent<Transform>();
    }

    void Update()
    {
        // Handle cooldown timer
        if (!canSpawn)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0f)
            {
                canSpawn = true;
            }
        }
        
        // Check if player is in range and cooldown allows spawning
        float targetDistance = Vector2.Distance(transform.position, playerTransform.position);
        if (targetDistance <= lineOfSight && canSpawn && !stun){
            stun = true;
            
            // Reset cooldown
            cooldownTimer = cooldownTime;
            canSpawn = false;
        }

        if (stun){
            StunWave();
            stunUsed++;
            stun = false;
        }

        if (stunUsed >= fallboxTreshold){
            stun = false;
            FallingBoxes(playerTransform,boxSpawnHeight);
            stunUsed = 0;
        }

        if (currentHP <= spawnDroneTreshold){
            Debug.Log("drone mode!");
        }
    }

    public void StunWave()
    {
        if (enemyPrefab != null)
        {
            Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;
            Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
        }
        else
        {
            Debug.LogError("Enemy prefab is not assigned in the inspector!");
        }
    }

    private void FallingBoxes(Transform target, float height){
        Vector2 boxPos = new Vector2(target.position.x, target.position.y + height);
        Instantiate(fallingBox, boxPos, Quaternion.identity);
    }

    // TEST VERSION
    public void TakeDamage()
    {
        float damage = damagePerSecond;
        currentHP -= damage;

        if (currentHP <= 0)
        {
            Destroy(gameObject);
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, lineOfSight);
    }
}
