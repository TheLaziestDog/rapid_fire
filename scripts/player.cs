using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class player : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float _speed = 7f;
    [SerializeField] private float _jumpForce = 5f;

    [Header("Look")]
    [SerializeField]
    private Transform _hosePivot;

    [Header("Ground Check")]
    [SerializeField] private Transform _groundCheck;  // Empty GameObject child positioned at feet
    [SerializeField] private float _groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask _groundLayer;  // Layer for ground objects
    private bool _isGrounded;

    private Rigidbody2D _rb;
    private Vector3 _mouseWorldPos;

    private bool isControlledByCursor = true;
    private Vector2 horizInput = Vector2.zero; // movement cache
    private bool jump;

    public charScript character;

    private void Awake(){
        _rb = GetComponent<Rigidbody2D>();
    }

    private void Update(){

        // Handle looking at the cursor if enabled
        if (isControlledByCursor)
        {
            LookAtMouse();
        }
    }

    private void OnWalk(InputValue value){
        horizInput = value.Get<Vector2>();

        if (horizInput.x != 0)
        {
            isControlledByCursor = false;

            float direction = Mathf.Sign(horizInput.x);
            character.FlipX(direction);
        }
    }

    private void OnJump(InputValue value){
        if (value.isPressed)
        {
            jump = value.isPressed;
            Debug.Log("jump!");
        }
    }

    private void OnLook(InputValue value){
        isControlledByCursor = true;
        _mouseWorldPos = Camera.main.ScreenToWorldPoint(value.Get<Vector2>());
    }

    private void FixedUpdate(){
        _isGrounded = Physics2D.OverlapCircle(_groundCheck.position, _groundCheckRadius, _groundLayer);

        // Only modify x velocity
        _rb.velocity = new Vector2(horizInput.x * _speed, _rb.velocity.y);
        
        // Only allow jumping if grounded
        if (jump && _isGrounded){
            _rb.AddForce(new Vector2(0f, _jumpForce), ForceMode2D.Impulse);
            jump = false;
        }
    }

    private void LookAtMouse(){
        var dir = ((Vector2)_mouseWorldPos - (Vector2)_hosePivot.position).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        _hosePivot.rotation = Quaternion.Euler(0, 0, angle);

        if (angle <= 90 && angle >= -99){
            character.FlipX(1);
        }
        else{
            character.FlipX(-1);
        }
    }
}
