using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class character : MonoBehaviour
{

    private void OnWalk(InputValue value){
        var moveInput = value.Get<Vector2>();
        FlipX(moveInput.x);
    }

    public void FlipX(float x){
        if (x != 0){
            transform.localScale = new Vector3(Mathf.Sign(x), 1, 1);
        }
    }
}
