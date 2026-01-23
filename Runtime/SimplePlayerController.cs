// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace GrassSystem
{
    /// <summary>
    /// Simple first-person player controller for testing the Grass System.
    /// Supports both keyboard/mouse and gamepad (including Nintendo Switch).
    /// 
    /// Controls:
    /// - Keyboard: WASD to move, Space to jump, Shift to sprint, Mouse to look
    /// - Gamepad: Left stick to move, A/B (South) to jump, Left trigger to sprint, Right stick to look
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class SimplePlayerController : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 5f;
        public float sprintMultiplier = 2f;
        public float jumpHeight = 1.5f;
        public float gravity = -20f;
        
        [Header("Look Sensitivity")]
        public float mouseSensitivity = 100f;
        [Tooltip("Gamepad stick sensitivity for looking around")]
        public float gamepadLookSensitivity = 150f;
        public Transform cameraTransform;
        
        [Header("Gamepad Settings")]
        [Tooltip("Invert Y axis on gamepad right stick")]
        public bool invertGamepadY = false;
        [Range(0.1f, 0.9f)]
        [Tooltip("Dead zone for gamepad sticks")]
        public float stickDeadzone = 0.2f;
        
        private CharacterController controller;
        private Vector3 velocity;
        private float xRotation;
        private float yRotation;
        private int skipFrames;
        private bool usingGamepad;
        
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
            
            HandleLook();
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
            // Keyboard input
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
            
            // Gamepad input (additive - works alongside keyboard)
            var gp = Gamepad.current;
            if (gp != null)
            {
                Vector2 leftStick = gp.leftStick.ReadValue();
                
                // Apply deadzone
                if (leftStick.magnitude > stickDeadzone)
                {
                    x += leftStick.x;
                    z += leftStick.y;
                    usingGamepad = true;
                }
                
                // Jump: A button (South)
                if (gp.buttonSouth.wasPressedThisFrame)
                    jump = true;
                
                // Sprint: Left trigger or left shoulder
                if (gp.leftTrigger.isPressed || gp.leftShoulder.isPressed)
                    sprint = true;
                
                // Pause/menu: Start button
                if (gp.startButton.wasPressedThisFrame)
                    toggleCursor = true;
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
        
        private void HandleLook()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;
            
            float lookX = 0f, lookY = 0f;
            
            #if ENABLE_INPUT_SYSTEM
            // Mouse look
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue();
                if (delta.sqrMagnitude > 0.01f)
                {
                    lookX = delta.x * mouseSensitivity * Time.deltaTime;
                    lookY = delta.y * mouseSensitivity * Time.deltaTime;
                    usingGamepad = false;
                }
            }
            
            // Gamepad right stick look
            var gp = Gamepad.current;
            if (gp != null)
            {
                Vector2 rightStick = gp.rightStick.ReadValue();
                
                if (rightStick.magnitude > stickDeadzone)
                {
                    lookX += rightStick.x * gamepadLookSensitivity * Time.deltaTime;
                    lookY += rightStick.y * gamepadLookSensitivity * Time.deltaTime;
                    
                    if (invertGamepadY)
                        lookY = -lookY;
                    
                    usingGamepad = true;
                }
            }
            #else
            lookX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
            lookY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;
            #endif
            
            xRotation -= lookY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);
            yRotation += lookX;
            
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
        
        /// <summary>
        /// Returns true if the player is currently using a gamepad
        /// </summary>
        public bool IsUsingGamepad => usingGamepad;
    }
}
