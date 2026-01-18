// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using UnityEngine;
using UnityEngine.InputSystem;
using System.Text;

namespace GrassSystem
{
    public class GrassPerformanceOverlay : MonoBehaviour
    {
        [Header("Display Settings")]
        public Key toggleKey = Key.F1;
        public TextAnchor anchor = TextAnchor.UpperLeft;
        [Range(12, 32)]
        public int fontSize = 18;
        [Range(0f, 1f)]
        public float backgroundAlpha = 0.7f;
        
        [Header("Target Benchmarks")]
        public int targetFPS = 30;
        
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
            Application.targetFrameRate = targetFPS;
        }
        
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                showOverlay = !showOverlay;
            
            if (!showOverlay) return;
            
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
            
            float width = 300;
            float height = 200;
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
            
            string fpsColor = smoothFPS >= targetFPS ? "#00FF00" : (smoothFPS >= targetFPS * 0.8f ? "#FFFF00" : "#FF4444");
            sb.AppendLine($"<color={fpsColor}><b>FPS: {smoothFPS:F1}</b></color>  (Min: {minFPS:F0} / Max: {maxFPS:F0})");
            
            float targetMs = 1000f / targetFPS;
            string frameColor = avgFrameTime <= targetMs ? "#00FF00" : "#FF4444";
            sb.AppendLine($"<color={frameColor}>Frame: {avgFrameTime:F2}ms</color>  (Target: {targetMs:F1}ms)");
            
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
            sb.AppendLine("<size=11><color=#666666>[F1] toggle</color></size>");
            
            GUI.Label(new Rect(x + 10, y + 5, width - 20, height - 10), sb.ToString(), labelStyle);
        }
        
        private void InitStyles()
        {
            if (boxStyle != null) return;
            
            boxStyle = new GUIStyle(GUI.skin.box);
            Texture2D bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, new Color(0.1f, 0.1f, 0.15f, backgroundAlpha));
            bgTex.Apply();
            boxStyle.normal.background = bgTex;
            
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = fontSize;
            labelStyle.richText = true;
            labelStyle.wordWrap = true;
            labelStyle.normal.textColor = Color.white;
        }
        
        public void ResetStats()
        {
            minFPS = float.MaxValue;
            maxFPS = 0;
            frameCount = 0;
        }
    }
}
