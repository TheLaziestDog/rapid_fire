using System.Collections;
using UnityEngine;

public class Box : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float stopThreshold = 0.1f;

    [Header("Collision Check")]
    [SerializeField] private float raycastDistance = 0.6f; // Distance to check for collisions
    [SerializeField] private LayerMask collidableLayers;

    private Rigidbody2D rb;
    private Vector2 targetVelocity;
    private BoxCollider2D boxCollider;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        rb.isKinematic = true;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionY;
    }

    private void FixedUpdate()
    {
        if (targetVelocity.magnitude > stopThreshold)
        {
            // Check for collisions before moving
            Vector2 movement = targetVelocity * Time.fixedDeltaTime;
            if (!WillCollide(movement))
            {
                rb.MovePosition(rb.position + movement);
            }
            else
            {
                // Stop movement when collision detected
                targetVelocity = Vector2.zero;
            }
        }

        // Gradually slow down
        targetVelocity = Vector2.Lerp(targetVelocity, Vector2.zero, Time.fixedDeltaTime * 5f);
    }

    private bool WillCollide(Vector2 movement)
    {
        // Calculate bounds for the raycast
        float raycastOriginOffset = boxCollider.bounds.extents.x;
        Vector2 direction = movement.normalized;
        
        // Cast rays from the edges of the box in the movement direction
        RaycastHit2D hitCenter = Physics2D.Raycast(
            rb.position,
            direction,
            raycastDistance,
            collidableLayers
        );

        RaycastHit2D hitTop = Physics2D.Raycast(
            rb.position + Vector2.up * (boxCollider.bounds.extents.y - 0.1f),
            direction,
            raycastDistance,
            collidableLayers
        );

        RaycastHit2D hitBottom = Physics2D.Raycast(
            rb.position - Vector2.up * (boxCollider.bounds.extents.y - 0.1f),
            direction,
            raycastDistance,
            collidableLayers
        );

        // Debug visualization
        Debug.DrawRay(rb.position, direction * raycastDistance, Color.red);
        Debug.DrawRay(rb.position + Vector2.up * (boxCollider.bounds.extents.y - 0.1f), 
            direction * raycastDistance, Color.red);
        Debug.DrawRay(rb.position - Vector2.up * (boxCollider.bounds.extents.y - 0.1f), 
            direction * raycastDistance, Color.red);

        // Check if any ray hit something (excluding self)
        if (hitCenter.collider != null && hitCenter.collider != boxCollider)
            return true;
        if (hitTop.collider != null && hitTop.collider != boxCollider)
            return true;
        if (hitBottom.collider != null && hitBottom.collider != boxCollider)
            return true;

        return false;
    }

    public void ApplyWaterForce(Vector2 playerPosition)
    {
        float pushDirection = playerPosition.x > transform.position.x ? -1f : 1f;
        targetVelocity = new Vector2(pushDirection * moveSpeed, 0);
    }
}
