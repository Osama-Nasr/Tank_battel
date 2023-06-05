using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraDrag : MonoBehaviour
{
    private Camera mainCamera;
    private Vector3 origin;
    private Vector3 difference;

    private bool isDragging;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    public void OnDrag(InputAction.CallbackContext ctx)
    {
        if (ctx.started) origin = GetMousePosition;
        isDragging = ctx.started || ctx.performed;
    }

    private void LateUpdate()
    {
        if (!isDragging) return;

        difference = GetMousePosition - transform.position;
        transform.position = origin - difference;
    }

    private Vector3 GetMousePosition => mainCamera.ScreenToWorldPoint((Vector3)Mouse.current.position.ReadValue());
}
