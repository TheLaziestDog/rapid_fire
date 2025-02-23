using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class BasicMovement : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private SPHSimulation boostScript;
    public float boost;


    [Header("Speed")]
    [SerializeField] private float _run = 7f;
    [SerializeField] private float _jump = 5f;
    [SerializeField] private float verticalMultiplier = 1;
    [SerializeField] private float hortizontalMultiplier = 1;
    
    [Header("Ground Check")]
    [SerializeField] private Transform _groundCheck;
    [SerializeField] private float _groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask _groundLayer;
    public bool _isGrounded;

    [Header("Fire Chamber")]
[SerializeField] private float _transitionDuration = 1f;
[SerializeField] private float _archHeight = 3f;
private bool _isFireChamber;
private bool _isTransitioning = false;
private Vector2 _startPosition;
private bool _isFireChamberMoving = false; // Add this new flag

    // Movement Manager
    private Vector2 horizInput = Vector2.zero;
    private bool jump;
    private float interpolationWeight = 1f;
    private Rigidbody2D _rb;
    private LogicManager logics;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        logics = GameObject.FindGameObjectWithTag("System").GetComponent<LogicManager>();
    }

    public void die(){
        gameObject.SetActive(false);
    }
    private void OnRun(InputValue value)
    {
        if (_isTransitioning) return;
        horizInput = value.Get<Vector2>();
        if (horizInput.x != 0) SwitchHorizLock(false);
    }

    private void OnJump(InputValue value)
    {
        if (_isTransitioning) return;
        if (value.isPressed)
        {
            jump = value.isPressed;
        }
    }

    public void SwitchHorizLock(bool value)
    {
        interpolationWeight = value ? 0.1f : 0.9f;
    }

    private void FixedUpdate()
    {
        if (_isTransitioning) return;

    _isGrounded = Physics2D.OverlapCircle(_groundCheck.position, _groundCheckRadius, _groundLayer);

    Vector2 movement = new Vector2(horizInput.x * _run, _rb.velocity.y);
    
    // Special handling for FireChamber movement
    if (_isFireChamberMoving)
    {
        // Don't modify horizontal velocity during FireChamber movement
        _rb.velocity = new Vector2(
            _rb.velocity.x,  // Keep current horizontal velocity
            _rb.velocity.y
        );
    }
    else
    {
        // Normal movement with interpolation for other cases
        _rb.velocity = new Vector2(
            Mathf.Lerp(_rb.velocity.x, movement.x, interpolationWeight),
            _rb.velocity.y
        );
    }

        if (jump && _isGrounded)
        {
            animator.SetBool("isJumping", true);
            _rb.AddForce(new Vector2(0f, _jump), ForceMode2D.Impulse);
            animator.SetFloat("yAxis", _rb.velocity.y);
            jump = false;
        } else {
            animator.SetBool("isJumping", false);
        }

        
        if (!boostScript.isBoostActive && _isGrounded)
        {
            boost = -1f;
            if (!jump){
                animator.SetFloat("xAxis", Mathf.Abs(horizInput.x));
            }
        }
        else if (boostScript.isBoostActive && _isGrounded && boostScript.boostDir.y > 0.9) // ensure the animation only happen with vertical boosting
        {
            boost = 1f;
        }
        // + condition for shooting while still flying

        //Debug.Log($"Mode : {boost}, Cond {boostScript.isBoostActive}, input {Mathf.Abs(horizInput.x)}");
    }

    /*private void Update()
    {
        if (boost == -1){
            animator.SetFloat("xAxis", Mathf.Abs(horizInput.x));
        }   
    }*/

    public void StartFireChamberTransition(Transform point)
{
    logics.damage(3);
    
    // Set the FireChamber flag
    _isFireChamberMoving = true;
    
    // Calculate direction to return point
    Vector2 direction = (point.position - transform.position).normalized;
    float distance = Vector2.Distance(transform.position, point.position);
    
    // Reset velocity first
    _rb.velocity = Vector2.zero;
    
    // Calculate appropriate forces for a nice arc
    float upwardForce = Mathf.Sqrt(distance) * _jump * verticalMultiplier;
    float horizontalForce = distance * hortizontalMultiplier;
    
    // Create a force vector that uses the direction.x component
    Vector2 forceVector = new Vector2(
        direction.x * horizontalForce,
        upwardForce
    );
    
    _rb.AddForce(forceVector, ForceMode2D.Impulse);
    
    // Disable input briefly but keep the flag set
    StartCoroutine(BrieflyDisableInput(0.5f));
    
    // Start another coroutine to reset the flag after a suitable time
    StartCoroutine(ResetFireChamberFlag(1.5f));
}

private IEnumerator ResetFireChamberFlag(float duration)
{
    yield return new WaitForSeconds(duration);
    _isFireChamberMoving = false;
}

private IEnumerator BrieflyDisableInput(float duration)
{
    // Store current input state
    Vector2 savedInput = horizInput;
    bool savedJump = jump;
    
    // Disable input
    horizInput = Vector2.zero;
    jump = false;
    
    yield return new WaitForSeconds(duration);
    
    // Restore input state
    horizInput = savedInput;
    jump = savedJump;
}
}
