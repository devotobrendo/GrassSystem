// GrassPerformanceOverlay.cs - In-game performance stats display
// Add to any GameObject in the scene to display grass system metrics

using UnityEngine;
using UnityEngine.InputSystem;
using System.Text;

namespace GrassSystem
{
    /// <summary>
    /// Simple on-screen performance overlay for grass system.
    /// Shows FPS, frame times, grass counts, and memory usage.
    /// Perfect for client demonstrations and Switch validation.
    /// </summary>
    public class GrassPerformanceOverlay : MonoBehaviour
    {
        [Header("Display Settings")]
        [Tooltip("Toggle overlay with this key")]
        public Key toggleKey = Key.F1;
        
        [Tooltip("Position on screen")]
        public TextAnchor anchor = TextAnchor.UpperLeft;
        
        [Tooltip("Font size for stats")]
        [Range(12, 32)]
        public int fontSize = 18;
        
        [Tooltip("Background transparency")]
        [Range(0f, 1f)]
        public float backgroundAlpha = 0.7f;
        
        [Header("Target Benchmarks (Switch)")]
        [Tooltip("Target FPS (Switch typically 30)")]
        public int targetFPS = 30;
        
        [Tooltip("Max acceptable frame time in ms")]
        public float maxFrameTimeMs = 33.33f; // 30 FPS = 33.33ms
        
        [Tooltip("Max grass instances for Switch")]
        public int maxGrassForSwitch = 50000;
        
        // Internal state
        private bool showOverlay = true;
        private GUIStyle boxStyle;
        private GUIStyle labelStyle;
        private GUIStyle headerStyle;
        private StringBuilder sb = new StringBuilder(512);
        
        // FPS calculation
        private float deltaTime;
        private float fps;
        private float smoothFPS;
        private float minFPS = float.MaxValue;
        private float maxFPS;
        private int frameCount;
        private float fpsUpdateTimer;
        private const float FPS_UPDATE_INTERVAL = 0.5f;
        
        // Frame time tracking
        private float[] frameTimes = new float[60];
        private int frameTimeIndex;
        private float avgFrameTime;
        private float maxFrameTime;
        
        // Grass renderer reference
        private GrassRenderer grassRenderer;
        private int totalGrassCount;
        private int visibleGrassCount; // Would need compute buffer readback
        
        // Memory tracking
        private float lastMemoryCheck;
        private long usedMemoryMB;
        private long totalMemoryMB;
        
        private void Start()
        {
            grassRenderer = FindObjectOfType<GrassRenderer>();
            Application.targetFrameRate = targetFPS;
        }
        
        private void Update()
        {
            // Toggle overlay using new Input System (F1 key)
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                showOverlay = !showOverlay;
            
            if (!showOverlay) return;
            
            // Calculate frame time
            deltaTime = Time.unscaledDeltaTime;
            
            // Track frame times for averaging
            frameTimes[frameTimeIndex] = deltaTime * 1000f;
            frameTimeIndex = (frameTimeIndex + 1) % frameTimes.Length;
            
            // Calculate stats
            frameCount++;
            fpsUpdateTimer += deltaTime;
            
            if (fpsUpdateTimer >= FPS_UPDATE_INTERVAL)
            {
                fps = frameCount / fpsUpdateTimer;
                smoothFPS = Mathf.Lerp(smoothFPS, fps, 0.5f);
                
                if (fps < minFPS && frameCount > 10) minFPS = fps;
                if (fps > maxFPS) maxFPS = fps;
                
                // Calculate average frame time
                float sum = 0f;
                maxFrameTime = 0f;
                for (int i = 0; i < frameTimes.Length; i++)
                {
                    sum += frameTimes[i];
                    if (frameTimes[i] > maxFrameTime) maxFrameTime = frameTimes[i];
                }
                avgFrameTime = sum / frameTimes.Length;
                
                frameCount = 0;
                fpsUpdateTimer = 0f;
            }
            
            // Update grass count
            if (grassRenderer != null && grassRenderer.GrassDataList != null)
            {
                totalGrassCount = grassRenderer.GrassDataList.Count;
                visibleGrassCount = grassRenderer.VisibleGrassCount;
            }
            
            // Memory check (less frequent - expensive)
            if (Time.time - lastMemoryCheck > 1f)
            {
                usedMemoryMB = System.GC.GetTotalMemory(false) / (1024 * 1024);
                totalMemoryMB = SystemInfo.systemMemorySize;
                lastMemoryCheck = Time.time;
            }
        }
        
        private void OnGUI()
        {
            if (!showOverlay) return;
            
            InitStyles();
            
            float width = 360;
            float height = 320;
            float x = 10;
            float y = 10;
            
            if (anchor == TextAnchor.UpperRight || anchor == TextAnchor.MiddleRight || anchor == TextAnchor.LowerRight)
                x = Screen.width - width - 10;
            if (anchor == TextAnchor.LowerLeft || anchor == TextAnchor.LowerCenter || anchor == TextAnchor.LowerRight)
                y = Screen.height - height - 10;
            
            Rect boxRect = new Rect(x, y, width, height);
            
            // Draw background
            GUI.Box(boxRect, "", boxStyle);
            
            // Build stats text
            sb.Clear();
            
            // Header
            sb.AppendLine("<b>GRASS SYSTEM BENCHMARK</b>");
            sb.AppendLine("================================");
            
            // FPS with color coding
            string fpsColor = GetFPSColor(smoothFPS);
            sb.AppendLine($"<color={fpsColor}><b>FPS: {smoothFPS:F1}</b></color>  (Min: {minFPS:F0} / Max: {maxFPS:F0})");
            
            // Frame time
            string frameTimeColor = avgFrameTime <= maxFrameTimeMs ? "#00FF00" : "#FF4444";
            sb.AppendLine($"<color={frameTimeColor}>Frame Time: {avgFrameTime:F2}ms</color>  (Peak: {maxFrameTime:F2}ms)");
            
            // Target comparison
            float targetMs = 1000f / targetFPS;
            float headroom = targetMs - avgFrameTime;
            string headroomColor = headroom >= 0 ? "#00FF00" : "#FF4444";
            sb.AppendLine($"Target: {targetFPS} FPS ({targetMs:F1}ms)  <color={headroomColor}>Headroom: {headroom:F1}ms</color>");
            
            sb.AppendLine();
            sb.AppendLine("<b>[GRASS STATS]</b>");
            sb.AppendLine("================================");
            
            // Grass count with Switch recommendation
            string grassColor = totalGrassCount <= maxGrassForSwitch ? "#00FF00" : "#FFAA00";
            sb.AppendLine($"<color={grassColor}>Total Instances: {totalGrassCount:N0}</color>");
            sb.AppendLine($"<color=#00FFFF>Visible (Rendered): {visibleGrassCount:N0}</color>");
            float cullPercent = totalGrassCount > 0 ? (1f - (float)visibleGrassCount / totalGrassCount) * 100f : 0f;
            sb.AppendLine($"Culled: {cullPercent:F1}%");
            sb.AppendLine($"Switch Limit: {maxGrassForSwitch:N0}");
            
            float grassPercent = (float)totalGrassCount / maxGrassForSwitch * 100f;
            sb.AppendLine($"Budget Used: {grassPercent:F1}%");
            
            sb.AppendLine();
            sb.AppendLine($"<size=12><color=#888888>Press [F1] to toggle</color></size>");
            
            // Draw text
            Rect labelRect = new Rect(x + 10, y + 5, width - 20, height - 10);
            GUI.Label(labelRect, sb.ToString(), labelStyle);
            
            // Draw Switch compatibility badge
            DrawSwitchBadge(x + width - 90, y + height - 35);
        }
        
        private void DrawSwitchBadge(float x, float y)
        {
            bool isCompatible = smoothFPS >= targetFPS * 0.9f && totalGrassCount <= maxGrassForSwitch;
            
            GUIStyle badgeStyle = new GUIStyle(GUI.skin.box);
            badgeStyle.fontSize = 12;
            badgeStyle.fontStyle = FontStyle.Bold;
            badgeStyle.alignment = TextAnchor.MiddleCenter;
            
            if (isCompatible)
            {
                GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f, 0.9f);
                GUI.Box(new Rect(x, y, 80, 25), "✓ SWITCH OK", badgeStyle);
            }
            else
            {
                GUI.backgroundColor = new Color(0.9f, 0.3f, 0.2f, 0.9f);
                GUI.Box(new Rect(x, y, 80, 25), "⚠ WARNING", badgeStyle);
            }
            GUI.backgroundColor = Color.white;
        }
        
        private string GetFPSColor(float currentFPS)
        {
            if (currentFPS >= targetFPS) return "#00FF00";      // Green - good
            if (currentFPS >= targetFPS * 0.8f) return "#FFFF00"; // Yellow - acceptable
            return "#FF4444"; // Red - bad
        }
        
        private void InitStyles()
        {
            if (boxStyle != null) return;
            
            // Background box
            boxStyle = new GUIStyle(GUI.skin.box);
            Texture2D bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, new Color(0.1f, 0.1f, 0.15f, backgroundAlpha));
            bgTex.Apply();
            boxStyle.normal.background = bgTex;
            
            // Label style
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = fontSize;
            labelStyle.richText = true;
            labelStyle.wordWrap = true;
            labelStyle.normal.textColor = Color.white;
            
            // Header style
            headerStyle = new GUIStyle(labelStyle);
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.fontSize = fontSize + 2;
        }
        
        /// <summary>
        /// Reset min/max tracking
        /// </summary>
        public void ResetStats()
        {
            minFPS = float.MaxValue;
            maxFPS = 0;
            frameCount = 0;
        }
    }
}
