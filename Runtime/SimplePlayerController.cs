// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace GrassSystem
{
    [RequireComponent(typeof(CharacterController))]
    public class SimplePlayerController : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 5f;
        public float sprintMultiplier = 2f;
        public float jumpHeight = 1.5f;
        public float gravity = -20f;
        
        [Header("Mouse Look")]
        public float mouseSensitivity = 100f;
        public Transform cameraTransform;
        
        private CharacterController controller;
        private Vector3 velocity;
        private float xRotation;
        private float yRotation;
        private int skipFrames;
        
        private void Start()
        {
            controller = GetComponent<CharacterController>();
            LockCursor();
            
            if (cameraTransform == null)
                cameraTransform = Camera.main?.transform;
            
            yRotation = transform.eulerAngles.y;
        }
        
        private void Update()
        {
            #if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlaying) return;
            #endif
            
            HandleMovement();
            
            if (skipFrames > 0)
            {
                skipFrames--;
                return;
            }
            
            HandleMouseLook();
        }
        
        private void HandleMovement()
        {
            bool grounded = controller.isGrounded;
            
            if (grounded && velocity.y < 0)
                velocity.y = -2f;
            
            float x = 0f, z = 0f;
            bool jump = false;
            bool toggleCursor = false;
            bool sprint = false;
            
            #if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed) z += 1f;
                if (kb.sKey.isPressed) z -= 1f;
                if (kb.aKey.isPressed) x -= 1f;
                if (kb.dKey.isPressed) x += 1f;
                jump = kb.spaceKey.wasPressedThisFrame;
                toggleCursor = kb.escapeKey.wasPressedThisFrame;
                sprint = kb.leftShiftKey.isPressed;
            }
            #else
            x = Input.GetAxis("Horizontal");
            z = Input.GetAxis("Vertical");
            jump = Input.GetButtonDown("Jump");
            toggleCursor = Input.GetKeyDown(KeyCode.Escape);
            sprint = Input.GetKey(KeyCode.LeftShift);
            #endif
            
            if (jump && grounded)
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            
            if (toggleCursor)
                ToggleCursor();
            
            float speed = sprint ? moveSpeed * sprintMultiplier : moveSpeed;
            Vector3 move = transform.right * x + transform.forward * z;
            controller.Move(move.normalized * speed * Time.deltaTime);
            
            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
        }
        
        private void HandleMouseLook()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;
            
            float mouseX = 0f, mouseY = 0f;
            
            #if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue();
                mouseX = delta.x * mouseSensitivity * Time.deltaTime;
                mouseY = delta.y * mouseSensitivity * Time.deltaTime;
            }
            #else
            mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
            mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;
            #endif
            
            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);
            yRotation += mouseX;
            
            transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
            
            if (cameraTransform != null)
                cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }
        
        private void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            skipFrames = 5;
        }
        
        private void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            skipFrames = 5;
        }
        
        private void ToggleCursor()
        {
            if (Cursor.lockState == CursorLockMode.Locked)
                UnlockCursor();
            else
                LockCursor();
        }
        
        private void OnDisable()
        {
            UnlockCursor();
        }
    }
}
