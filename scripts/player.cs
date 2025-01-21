/*
here's my current player code :

and here's my charScript code :

if the player presses a or d, the hose angle resetted back to either 0 or 180 (a = 180, d = 0)
and the :
    if (angle <= 90 && angle >= -99){
        character.FlipX(1);
    } else{
        character.FlipX(-1);
    }
only activated again when the player cursor moved again.
make it work like this so the player script does not override the charscript in terms of calling the FlipX function
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class player : MonoBehaviour
{
    [Header("Move")]
    [SerializeField]
    private float _speed = 3f;

    [Header("Look")]
    [SerializeField]
    private Transform _hosePivot;

    private Rigidbody2D _rb;
    private Vector3 _mouseWorldPos;

    private bool isControlledByCursor = true;

    public charScript character;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (isControlledByCursor)
        {
            LookAtMouse();
        }
    }

    private void OnWalk(InputValue value)
    {
        var moveInput = value.Get<Vector2>();
        _rb.velocity = moveInput * _speed;

        if (moveInput.x != 0)
        {
            isControlledByCursor = false;
            float direction = Mathf.Sign(moveInput.x);
            character.FlipX(direction);

            float hoseAngle = direction > 0 ? 0 : 170; 
            _hosePivot.rotation = Quaternion.Euler(0, 0, hoseAngle);
        }
    }

    private void OnLook(InputValue value)
    {
        isControlledByCursor = true;
        _mouseWorldPos = Camera.main.ScreenToWorldPoint(value.Get<Vector2>());
    }

    private void LookAtMouse()
    {
        var dir = ((Vector2)_mouseWorldPos - (Vector2)_hosePivot.position).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        _hosePivot.rotation = Quaternion.Euler(0, 0, angle);

        if (angle <= 90 && angle >= -99)
        {
            character.FlipX(1);
        }
        else
        {
            character.FlipX(-1);
        }
    }
}
