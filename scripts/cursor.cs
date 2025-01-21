using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class cursor : MonoBehaviour
{
    private Vector3 _mouseWorldPos;

    private void OnLook(InputValue value)
    {
        _mouseWorldPos = Camera.main.ScreenToWorldPoint(value.Get<Vector2>());
        transform.position = new Vector3(_mouseWorldPos.x, _mouseWorldPos.y, transform.position.z);
    }
}
