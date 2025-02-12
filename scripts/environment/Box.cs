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
    
    [Header("Debug Visualization")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color gizmoColor = new Color(1f, 0f, 0f, 0.5f); // Semi-transparent red

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
            Vector2 movement = targetVelocity * Time.fixedDeltaTime;
            if (!WillCollide(movement))
            {
                rb.MovePosition(rb.position + movement);
            }
            else
            {
                targetVelocity = Vector2.zero;
            }
        }

        targetVelocity = Vector2.Lerp(targetVelocity, Vector2.zero, Time.fixedDeltaTime * 5f);
    }

    private bool WillCollide(Vector2 movement)
    {
        float raycastOriginOffset = boxCollider.bounds.extents.x;
        Vector2 direction = movement.normalized;
        
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

    private void OnDrawGizmos()
    {
        if (!showGizmos || boxCollider == null) return;

        // Store the original Gizmos color
        Color originalColor = Gizmos.color;
        Gizmos.color = gizmoColor;

        // Get the direction based on current velocity or facing direction
        Vector2 direction = targetVelocity.normalized;
        if (direction == Vector2.zero)
        {
            // Draw both left and right when stationary
            DrawRaycastGizmos(Vector2.left);
            DrawRaycastGizmos(Vector2.right);
        }
        else
        {
            // Draw in the movement direction
            DrawRaycastGizmos(direction);
        }

        // Restore original Gizmos color
        Gizmos.color = originalColor;
    }

    private void DrawRaycastGizmos(Vector2 direction)
    {
        Vector3 position = transform.position;
        float halfHeight = boxCollider.bounds.extents.y - 0.1f;

        // Draw lines for center, top, and bottom raycasts
        Gizmos.DrawLine(position, position + (Vector3)(direction * raycastDistance));
        Gizmos.DrawLine(
            position + Vector3.up * halfHeight, 
            position + Vector3.up * halfHeight + (Vector3)(direction * raycastDistance)
        );
        Gizmos.DrawLine(
            position + Vector3.down * halfHeight, 
            position + Vector3.down * halfHeight + (Vector3)(direction * raycastDistance)
        );

        // Draw spheres at raycast endpoints
        float sphereRadius = 0.05f;
        Gizmos.DrawSphere(position + (Vector3)(direction * raycastDistance), sphereRadius);
        Gizmos.DrawSphere(
            position + Vector3.up * halfHeight + (Vector3)(direction * raycastDistance), 
            sphereRadius
        );
        Gizmos.DrawSphere(
            position + Vector3.down * halfHeight + (Vector3)(direction * raycastDistance), 
            sphereRadius
        );
    }
}
