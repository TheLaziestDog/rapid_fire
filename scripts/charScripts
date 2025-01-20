using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float runSpeed = 40f;
    public float jumpForce = 5f; // Force applied when jumping
    public float fastFallMultiplier = 2f; // Multiplier for fast-fall speed

    private float horizontalMove = 0f;
    private bool jump = false;
    private bool facingRight = true; // To track the direction the character is facing
    private Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // Get horizontal input for movement
        horizontalMove = Input.GetAxisRaw("Horizontal") * runSpeed;

        // Check for jump input
        if (Input.GetButtonDown("Jump") && Mathf.Abs(rb.velocity.y) < 0.01f)
        {
            jump = true;
        }

        // Check for fast-fall input (S key or Down Arrow)
        if (Input.GetKey(KeyCode.S) && rb.velocity.y > 0)
        {
            rb.velocity += Vector2.down * fastFallMultiplier;
        }

        // Flip character based on movement direction
        if (horizontalMove > 0 && !facingRight)
        {
            Flip();
        }
        else if (horizontalMove < 0 && facingRight)
        {
            Flip();
        }
    }

    void FixedUpdate()
    {
        // Horizontal movement
        Vector2 targetVelocity = new Vector2(horizontalMove * Time.fixedDeltaTime, rb.velocity.y);
        rb.velocity = targetVelocity;

        // Apply jump force
        if (jump)
        {
            rb.AddForce(new Vector2(0f, jumpForce), ForceMode2D.Impulse);
            jump = false;
        }
    }

    void Flip()
    {
        // Switch the facing direction
        facingRight = !facingRight;

        // Multiply the scale.x by -1 to flip the character
        Vector3 localScale = transform.localScale;
        localScale.x *= -1;
        transform.localScale = localScale;
    }
}
