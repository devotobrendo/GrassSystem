// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using UnityEngine;
using UnityEngine.InputSystem;
using System.Text;

namespace GrassSystem
{
    /// <summary>
    /// Performance overlay modes: Full shows all stats, Minimal shows only FPS (lighter on performance)
    /// </summary>
    public enum OverlayDisplayMode
    {
        Full,       // Complete stats display
        Minimal     // FPS only (recommended for Switch)
    }
    
    /// <summary>
    /// Gamepad buttons that can be used to toggle the overlay
    /// </summary>
    public enum GamepadButton
    {
        Select,         // Minus button on Switch
        Start,          // Plus button on Switch
        LeftShoulder,   // L button
        RightShoulder,  // R button
        LeftStick,      // Press left stick
        RightStick      // Press right stick
    }
    
    /// <summary>
    /// In-game performance overlay for the Grass System.
    /// Works on all platforms including Nintendo Switch.
    /// 
    /// Controls:
    /// - F1 (keyboard) or Select/Minus (gamepad): Toggle overlay on/off
    /// - F2 (keyboard) or Start/Plus (gamepad): Switch between Full and Minimal mode
    /// </summary>
    public class GrassPerformanceOverlay : MonoBehaviour
    {
        [Header("Display Settings")]
        [Tooltip("Display mode: Full shows all stats, Minimal shows only FPS (lighter for Switch)")]
        public OverlayDisplayMode displayMode = OverlayDisplayMode.Full;
        
        public Key toggleKey = Key.F1;
        public Key switchModeKey = Key.F2;
        
        [Tooltip("Gamepad button to toggle overlay (Select = minus button on Switch)")]
        public GamepadButton gamepadToggleButton = GamepadButton.Select;
        [Tooltip("Gamepad button to switch display mode (Start = plus button on Switch)")]
        public GamepadButton gamepadSwitchModeButton = GamepadButton.Start;
        
        public TextAnchor anchor = TextAnchor.UpperLeft;
        [Range(12, 32)]
        public int fontSize = 18;
        [Range(0f, 1f)]
        public float backgroundAlpha = 0.7f;
        
        [Header("Debug")]
        [Tooltip("Log lifecycle events (Initialize, Cleanup, Scene events) to Console")]
        public bool logLifecycleEvents = false;
        
        /// <summary>
        /// Static accessor for other scripts to check if logging is enabled
        /// </summary>
        public static bool LogLifecycleEventsEnabled { get; private set; }
        
        private bool showOverlay = true;
        private GUIStyle boxStyle;
        private GUIStyle labelStyle;
        private StringBuilder sb = new StringBuilder(512);
        
        private float fps;
        private float smoothFPS;
        private float minFPS = float.MaxValue;
        private float maxFPS;
        private int frameCount;
        private float fpsUpdateTimer;
        private const float FPS_UPDATE_INTERVAL = 0.5f;
        
        private float[] frameTimes = new float[60];
        private int frameTimeIndex;
        private float avgFrameTime;
        
        private GrassRenderer grassRenderer;
        private int totalGrassCount;
        private int visibleGrassCount;
        
        private void Start()
        {
            grassRenderer = FindAnyObjectByType<GrassRenderer>();
            LogLifecycleEventsEnabled = logLifecycleEvents;
        }
        
        private void OnValidate()
        {
            LogLifecycleEventsEnabled = logLifecycleEvents;
        }
        
        private void Update()
        {
            HandleInput();
            
            if (!showOverlay) return;
            
            UpdateMetrics();
        }
        
        private void HandleInput()
        {
            // Toggle overlay: F1 (keyboard) or Select (gamepad)
            bool keyboardToggle = Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame;
            bool gamepadToggle = Gamepad.current != null && GetGamepadButton(gamepadToggleButton).wasPressedThisFrame;
            
            if (keyboardToggle || gamepadToggle)
                showOverlay = !showOverlay;
            
            // Switch display mode: F2 (keyboard) or Start (gamepad)
            bool keyboardSwitchMode = Keyboard.current != null && Keyboard.current[switchModeKey].wasPressedThisFrame;
            bool gamepadSwitchMode = Gamepad.current != null && GetGamepadButton(gamepadSwitchModeButton).wasPressedThisFrame;
            
            if (keyboardSwitchMode || gamepadSwitchMode)
                displayMode = displayMode == OverlayDisplayMode.Full ? OverlayDisplayMode.Minimal : OverlayDisplayMode.Full;
        }
        
        private void UpdateMetrics()
        {
            float deltaTime = Time.unscaledDeltaTime;
            frameTimes[frameTimeIndex] = deltaTime * 1000f;
            frameTimeIndex = (frameTimeIndex + 1) % frameTimes.Length;
            
            frameCount++;
            fpsUpdateTimer += deltaTime;
            
            if (fpsUpdateTimer >= FPS_UPDATE_INTERVAL)
            {
                fps = frameCount / fpsUpdateTimer;
                smoothFPS = Mathf.Lerp(smoothFPS, fps, 0.5f);
                
                if (fps < minFPS && frameCount > 10) minFPS = fps;
                if (fps > maxFPS) maxFPS = fps;
                
                float sum = 0f;
                for (int i = 0; i < frameTimes.Length; i++)
                    sum += frameTimes[i];
                avgFrameTime = sum / frameTimes.Length;
                
                frameCount = 0;
                fpsUpdateTimer = 0f;
            }
            
            if (grassRenderer != null && grassRenderer.GrassDataList != null)
            {
                totalGrassCount = grassRenderer.GrassDataList.Count;
                visibleGrassCount = grassRenderer.VisibleGrassCount;
            }
        }
        
        private void OnGUI()
        {
            if (!showOverlay) return;
            
            InitStyles();
            
            if (displayMode == OverlayDisplayMode.Minimal)
                DrawMinimalOverlay();
            else
                DrawFullOverlay();
        }
        
        private void DrawMinimalOverlay()
        {
            float width = 150;
            float height = 40;
            float x = 10;
            float y = 10;
            
            if (anchor == TextAnchor.UpperRight || anchor == TextAnchor.MiddleRight || anchor == TextAnchor.LowerRight)
                x = Screen.width - width - 10;
            if (anchor == TextAnchor.LowerLeft || anchor == TextAnchor.LowerCenter || anchor == TextAnchor.LowerRight)
                y = Screen.height - height - 10;
            
            GUI.Box(new Rect(x, y, width, height), "", boxStyle);
            
            string fpsColor = smoothFPS >= 30f ? "#00FF00" : (smoothFPS >= 24f ? "#FFFF00" : "#FF4444");
            string text = $"<color={fpsColor}><b>FPS: {smoothFPS:F1}</b></color>";
            
            GUI.Label(new Rect(x + 10, y + 8, width - 20, height - 16), text, labelStyle);
        }
        
        private void DrawFullOverlay()
        {
            float width = 300;
            float height = 180;
            float x = 10;
            float y = 10;
            
            if (anchor == TextAnchor.UpperRight || anchor == TextAnchor.MiddleRight || anchor == TextAnchor.LowerRight)
                x = Screen.width - width - 10;
            if (anchor == TextAnchor.LowerLeft || anchor == TextAnchor.LowerCenter || anchor == TextAnchor.LowerRight)
                y = Screen.height - height - 10;
            
            GUI.Box(new Rect(x, y, width, height), "", boxStyle);
            
            sb.Clear();
            sb.AppendLine("<b>GRASS SYSTEM</b>");
            sb.AppendLine("─────────────────────");
            
            string fpsColor = smoothFPS >= 30f ? "#00FF00" : (smoothFPS >= 24f ? "#FFFF00" : "#FF4444");
            sb.AppendLine($"<color={fpsColor}><b>FPS: {smoothFPS:F1}</b></color>  (Min: {minFPS:F0} / Max: {maxFPS:F0})");
            sb.AppendLine($"Frame: {avgFrameTime:F2}ms");
            
            sb.AppendLine();
            sb.AppendLine($"Total Grass: <b>{totalGrassCount:N0}</b>");
            
            string visibleColor = visibleGrassCount < totalGrassCount ? "#00FFFF" : "#FFFFFF";
            sb.AppendLine($"<color={visibleColor}>Visible: <b>{visibleGrassCount:N0}</b></color>");
            
            if (totalGrassCount > 0)
            {
                float cullPercent = (1f - (float)visibleGrassCount / totalGrassCount) * 100f;
                sb.AppendLine($"Culled: {cullPercent:F1}%");
            }
            
            sb.AppendLine();
            bool hasGamepad = Gamepad.current != null;
            string toggleHint = hasGamepad ? $"[-] toggle  [+] mode" : "[F1] toggle  [F2] mode";
            sb.AppendLine($"<size=11><color=#666666>{toggleHint}</color></size>");
            
            GUI.Label(new Rect(x + 10, y + 5, width - 20, height - 10), sb.ToString(), labelStyle);
        }
        
        private Texture2D bgTexture;
        
        private void InitStyles()
        {
            if (boxStyle != null) return;
            
            boxStyle = new GUIStyle(GUI.skin.box);
            bgTexture = new Texture2D(1, 1);
            bgTexture.SetPixel(0, 0, new Color(0.1f, 0.1f, 0.15f, backgroundAlpha));
            bgTexture.Apply();
            boxStyle.normal.background = bgTexture;
            
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = fontSize;
            labelStyle.richText = true;
            labelStyle.wordWrap = true;
            labelStyle.normal.textColor = Color.white;
        }
        
        private void OnDisable()
        {
            if (bgTexture != null)
            {
                if (Application.isPlaying)
                    Destroy(bgTexture);
                else
                    DestroyImmediate(bgTexture);
                bgTexture = null;
            }
            boxStyle = null;
            labelStyle = null;
        }
        
        public void ResetStats()
        {
            minFPS = float.MaxValue;
            maxFPS = 0;
            frameCount = 0;
        }
        
        private UnityEngine.InputSystem.Controls.ButtonControl GetGamepadButton(GamepadButton button)
        {
            var gp = Gamepad.current;
            return button switch
            {
                GamepadButton.Select => gp.selectButton,
                GamepadButton.Start => gp.startButton,
                GamepadButton.LeftShoulder => gp.leftShoulder,
                GamepadButton.RightShoulder => gp.rightShoulder,
                GamepadButton.LeftStick => gp.leftStickButton,
                GamepadButton.RightStick => gp.rightStickButton,
                _ => gp.selectButton
            };
        }
    }
}
