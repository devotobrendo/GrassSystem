// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using UnityEngine;
using UnityEngine.InputSystem;
using GrassSystem;
using System.Text;

namespace GrassSystem.Consoles
{
    public class GrassConsoleDebugOverlay : MonoBehaviour
    {
        [Header("Toggle")]
        public Key toggleKey = Key.F8;
        public GamepadButton gamepadToggleButton = GamepadButton.RightStick;
        public bool useShoulderComboToggle = false;

        [Header("Display")]
        public TextAnchor anchor = TextAnchor.UpperRight;
        [Range(12, 24)]
        public int fontSize = 16;
        [Range(0f, 1f)]
        public float backgroundAlpha = 0.75f;

        private const int RowOverride = 0;
        private const int RowGlobalDensity = 1;
        private const int RowFarKeep = 2;
        private const int RowThinStart = 3;
        private const int RowCoverage = 4;
        private const int RowSizeX = 5;
        private const int RowSizeY = 6;
        private const int RowGrassMode = 7;
        private const int RowBladeType = 8;
        private const int RowSystem = 9;

        private static readonly string[] RowLabels =
        {
            "Override",
            "Global Density",
            "Far Keep",
            "Thin Start",
            "Coverage",
            "Size X (width)",
            "Size Y (height)",
            "Grass Mode",
            "Blade Type",
            "System"
        };

        private static readonly float[] FineStep = { 0f, 0.05f, 0.05f, 1f, 0.05f, 0.02f, 0.02f, 0f, 0f, 0f };
        private static readonly float[] CoarseStep = { 0f, 0.2f, 0.2f, 5f, 0.2f, 0.15f, 0.15f, 0f, 0f, 0f };

        private enum GrassSystemState
        {
            Console,
            Original,
            Off
        }

        private const float STICK_DEADZONE = 0.5f;
        private const float REPEAT_INITIAL_DELAY = 0.35f;
        private const float REPEAT_INTERVAL = 0.05f;

        private bool showOverlay;
        private int selectedRow;
        private float globalDensityValue = 1f;
        private GrassSystemState systemState;
        private int lastStickYDirection;
        private float fineRepeatTimer;
        private float coarseRepeatTimer;

        private float smoothFPS;
        private int fpsFrameCount;
        private float fpsUpdateTimer;
        private float avgFrameTime;
        private int frameTimeIndex;
        private readonly float[] frameTimes = new float[60];
        private const float FPS_UPDATE_INTERVAL = 0.5f;

        private readonly FrameTiming[] frameTimings = new FrameTiming[1];
        private float gpuFrameTime;
        private float cpuFrameTime;
        private bool gpuTimingAvailable;

        private GUIStyle boxStyle;
        private GUIStyle labelStyle;
        private Texture2D bgTexture;
        private readonly StringBuilder sb = new StringBuilder(512);

        private void OnEnable()
        {
            GrassConsoleDebug.ReadoutEnabled = showOverlay;
        }

        private void OnDisable()
        {
            GrassConsoleDebug.ReadoutEnabled = false;

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

        private void Update()
        {
            HandleInput();
            if (showOverlay)
            {
                UpdateFpsMetrics();
                UpdateGpuTiming();
            }
        }

        private void UpdateGpuTiming()
        {
            FrameTimingManager.CaptureFrameTimings();
            uint received = FrameTimingManager.GetLatestTimings(1, frameTimings);
            if (received == 0)
            {
                gpuTimingAvailable = false;
                return;
            }

            gpuTimingAvailable = true;
            gpuFrameTime = Mathf.Lerp(gpuFrameTime, (float)frameTimings[0].gpuFrameTime, 0.25f);
            cpuFrameTime = Mathf.Lerp(cpuFrameTime, (float)frameTimings[0].cpuFrameTime, 0.25f);
        }

        private void UpdateFpsMetrics()
        {
            float deltaTime = Time.unscaledDeltaTime;
            frameTimes[frameTimeIndex] = deltaTime * 1000f;
            frameTimeIndex = (frameTimeIndex + 1) % frameTimes.Length;

            fpsFrameCount++;
            fpsUpdateTimer += deltaTime;

            if (fpsUpdateTimer >= FPS_UPDATE_INTERVAL)
            {
                float fps = fpsFrameCount / fpsUpdateTimer;
                smoothFPS = Mathf.Lerp(smoothFPS, fps, 0.5f);

                float sum = 0f;
                for (int i = 0; i < frameTimes.Length; i++)
                    sum += frameTimes[i];
                avgFrameTime = sum / frameTimes.Length;

                fpsFrameCount = 0;
                fpsUpdateTimer = 0f;
            }
        }

        private void HandleInput()
        {
            bool keyboardTogglePressed = Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame;
            bool gamepadTogglePressed = GetGamepadTogglePressed();

            if (keyboardTogglePressed || gamepadTogglePressed)
            {
                showOverlay = !showOverlay;
                GrassConsoleDebug.ReadoutEnabled = showOverlay;
                if (showOverlay)
                {
                    SeedFromActiveRenderer();
                    DetectSystemState();
                }
                else
                {
                    fineRepeatTimer = 0f;
                    coarseRepeatTimer = 0f;
                }
            }

            if (!showOverlay) return;

            HandleNavigation();
            HandleAdjustment();
        }

        private bool GetGamepadTogglePressed()
        {
            var gp = Gamepad.current;
            if (gp == null) return false;

            if (useShoulderComboToggle)
            {
                bool bothHeld = gp.leftShoulder.isPressed && gp.rightShoulder.isPressed;
                bool edge = (gp.leftShoulder.wasPressedThisFrame && gp.rightShoulder.isPressed) ||
                            (gp.rightShoulder.wasPressedThisFrame && gp.leftShoulder.isPressed);
                return bothHeld && edge;
            }

            return GetGamepadButtonControl(gamepadToggleButton).wasPressedThisFrame;
        }

        private void HandleNavigation()
        {
            bool up = Keyboard.current != null && Keyboard.current[Key.UpArrow].wasPressedThisFrame;
            bool down = Keyboard.current != null && Keyboard.current[Key.DownArrow].wasPressedThisFrame;

            var gp = Gamepad.current;
            if (gp != null)
            {
                up |= gp.dpad.up.wasPressedThisFrame;
                down |= gp.dpad.down.wasPressedThisFrame;

                float stickY = gp.leftStick.y.ReadValue();
                int stickDir = stickY > STICK_DEADZONE ? 1 : (stickY < -STICK_DEADZONE ? -1 : 0);
                if (stickDir != lastStickYDirection)
                {
                    if (stickDir == 1) up = true;
                    else if (stickDir == -1) down = true;
                }
                lastStickYDirection = stickDir;
            }

            int rowCount = RowLabels.Length;
            if (up)
                selectedRow = (selectedRow - 1 + rowCount) % rowCount;
            else if (down)
                selectedRow = (selectedRow + 1) % rowCount;
        }

        private void HandleAdjustment()
        {
            bool keyLeft = Keyboard.current != null && Keyboard.current[Key.LeftArrow].wasPressedThisFrame;
            bool keyRight = Keyboard.current != null && Keyboard.current[Key.RightArrow].wasPressedThisFrame;

            var gp = Gamepad.current;
            bool padLeftPressed = gp != null && gp.dpad.left.wasPressedThisFrame;
            bool padRightPressed = gp != null && gp.dpad.right.wasPressedThisFrame;
            bool padLeftHeld = gp != null && gp.dpad.left.isPressed;
            bool padRightHeld = gp != null && gp.dpad.right.isPressed;
            bool southPressed = gp != null && gp.buttonSouth.wasPressedThisFrame;

            if (selectedRow == RowOverride)
            {
                if (keyLeft || keyRight || padLeftPressed || padRightPressed || southPressed)
                {
                    bool turningOn = !GrassConsoleDebug.OverrideEnabled;
                    GrassConsoleDebug.OverrideEnabled = turningOn;
                    if (turningOn)
                        SeedFromActiveRenderer();
                }
                fineRepeatTimer = 0f;
                coarseRepeatTimer = 0f;
                return;
            }

            if (selectedRow == RowGrassMode)
            {
                if (keyLeft || keyRight || padLeftPressed || padRightPressed || southPressed)
                {
                    if (!GrassConsoleDebug.ModeOverrideEnabled)
                    {
                        GrassConsoleDebug.ModeOverrideEnabled = true;
                        GrassConsoleDebug.ModeOverride = GrassMode.Default;
                    }
                    else if (GrassConsoleDebug.ModeOverride == GrassMode.Default)
                    {
                        GrassConsoleDebug.ModeOverride = GrassMode.CustomMesh;
                    }
                    else
                    {
                        GrassConsoleDebug.ModeOverrideEnabled = false;
                    }
                }
                fineRepeatTimer = 0f;
                coarseRepeatTimer = 0f;
                return;
            }

            if (selectedRow == RowBladeType)
            {
                if (keyLeft || keyRight || padLeftPressed || padRightPressed || southPressed)
                {
                    if (!GrassConsoleDebug.BladeTypeOverrideEnabled)
                    {
                        GrassConsoleDebug.BladeTypeOverrideEnabled = true;
                        GrassConsoleDebug.BladeTypeOverride = GrassProceduralType.Blade;
                    }
                    else if (GrassConsoleDebug.BladeTypeOverride == GrassProceduralType.Blade)
                    {
                        GrassConsoleDebug.BladeTypeOverride = GrassProceduralType.Tapered;
                    }
                    else if (GrassConsoleDebug.BladeTypeOverride == GrassProceduralType.Tapered)
                    {
                        GrassConsoleDebug.BladeTypeOverride = GrassProceduralType.Quad;
                    }
                    else if (GrassConsoleDebug.BladeTypeOverride == GrassProceduralType.Quad)
                    {
                        GrassConsoleDebug.BladeTypeOverride = GrassProceduralType.Cross;
                    }
                    else if (GrassConsoleDebug.BladeTypeOverride == GrassProceduralType.Cross)
                    {
                        GrassConsoleDebug.BladeTypeOverride = GrassProceduralType.Tuft;
                    }
                    else
                    {
                        GrassConsoleDebug.BladeTypeOverrideEnabled = false;
                    }
                }
                fineRepeatTimer = 0f;
                coarseRepeatTimer = 0f;
                return;
            }

            if (selectedRow == RowSystem)
            {
                if (keyLeft || keyRight || padLeftPressed || padRightPressed || southPressed)
                {
                    systemState = (GrassSystemState)(((int)systemState + 1) % 3);
                    ApplySystemState(systemState);
                }
                fineRepeatTimer = 0f;
                coarseRepeatTimer = 0f;
                return;
            }

            int fineDir = 0;
            if (keyLeft) fineDir = -1;
            else if (keyRight) fineDir = 1;
            else if (ConsumeRepeat(padLeftPressed, padLeftHeld, ref fineRepeatTimer)) fineDir = -1;
            else if (ConsumeRepeat(padRightPressed, padRightHeld, ref fineRepeatTimer)) fineDir = 1;

            bool shoulderAvailableForStep = gp != null && !useShoulderComboToggle;
            bool coarseLeftPressed = shoulderAvailableForStep && gp.leftShoulder.wasPressedThisFrame;
            bool coarseRightPressed = shoulderAvailableForStep && gp.rightShoulder.wasPressedThisFrame;
            bool coarseLeftHeld = shoulderAvailableForStep && gp.leftShoulder.isPressed;
            bool coarseRightHeld = shoulderAvailableForStep && gp.rightShoulder.isPressed;

            int coarseDir = 0;
            if (ConsumeRepeat(coarseLeftPressed, coarseLeftHeld, ref coarseRepeatTimer)) coarseDir = -1;
            else if (ConsumeRepeat(coarseRightPressed, coarseRightHeld, ref coarseRepeatTimer)) coarseDir = 1;

            if (fineDir == 0 && coarseDir == 0)
                return;

            var range = GetRowRange(selectedRow);
            float step = coarseDir != 0 ? CoarseStep[selectedRow] * coarseDir : FineStep[selectedRow] * fineDir;
            float next = Mathf.Clamp(GetRowValue(selectedRow) + step, range.min, range.max);
            SetRowValue(selectedRow, next);
        }

        private bool ConsumeRepeat(bool pressedThisFrame, bool isHeld, ref float timer)
        {
            if (pressedThisFrame)
            {
                timer = REPEAT_INITIAL_DELAY;
                return true;
            }
            if (isHeld)
            {
                timer -= Time.unscaledDeltaTime;
                if (timer <= 0f)
                {
                    timer = REPEAT_INTERVAL;
                    return true;
                }
                return false;
            }
            timer = 0f;
            return false;
        }

        private void SeedFromActiveRenderer()
        {
            if (GrassConsoleDebug.ActiveRenderers.Count == 0) return;
            var source = GrassConsoleDebug.ActiveRenderers[0];
            if (source == null) return;

            GrassConsoleDebug.FarKeepFraction = source.farKeepFraction;
            GrassConsoleDebug.ThinStartDistance = source.thinStartDistance;
            GrassConsoleDebug.CoverageCompensation = source.coverageCompensation;
            GrassConsoleDebug.SizeScale = source.sizeScale;
            globalDensityValue = source.farKeepFraction;
        }

        private void ApplySystemState(GrassSystemState state)
        {
            bool consolesActive = state == GrassSystemState.Console;
            bool originalsActive = state == GrassSystemState.Original;

            var consoles = Object.FindObjectsByType<GrassRendererConsole>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (consoles != null)
            {
                for (int i = 0; i < consoles.Length; i++)
                {
                    if (consoles[i] != null)
                        consoles[i].gameObject.SetActive(consolesActive);
                }
            }

            var originals = Object.FindObjectsByType<GrassRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (originals != null)
            {
                for (int i = 0; i < originals.Length; i++)
                {
                    if (originals[i] != null)
                        originals[i].gameObject.SetActive(originalsActive);
                }
            }
        }

        private void DetectSystemState()
        {
            var consoles = Object.FindObjectsByType<GrassRendererConsole>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            bool anyConsoleActive = false;
            if (consoles != null)
            {
                for (int i = 0; i < consoles.Length; i++)
                {
                    if (consoles[i] != null && consoles[i].gameObject.activeSelf)
                    {
                        anyConsoleActive = true;
                        break;
                    }
                }
            }

            bool anyOriginalActive = false;
            if (!anyConsoleActive)
            {
                var originals = Object.FindObjectsByType<GrassRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (originals != null)
                {
                    for (int i = 0; i < originals.Length; i++)
                    {
                        if (originals[i] != null && originals[i].gameObject.activeSelf)
                        {
                            anyOriginalActive = true;
                            break;
                        }
                    }
                }
            }

            if (anyConsoleActive)
                systemState = GrassSystemState.Console;
            else if (anyOriginalActive)
                systemState = GrassSystemState.Original;
            else
                systemState = GrassSystemState.Off;
        }

        private float GetRowValue(int row)
        {
            switch (row)
            {
                case RowGlobalDensity: return globalDensityValue;
                case RowFarKeep: return GrassConsoleDebug.FarKeepFraction;
                case RowThinStart: return GrassConsoleDebug.ThinStartDistance;
                case RowCoverage: return GrassConsoleDebug.CoverageCompensation;
                case RowSizeX: return GrassConsoleDebug.SizeScale.x;
                case RowSizeY: return GrassConsoleDebug.SizeScale.y;
                default: return 0f;
            }
        }

        private void SetRowValue(int row, float value)
        {
            switch (row)
            {
                case RowGlobalDensity:
                    globalDensityValue = Mathf.Clamp01(value);
                    GrassConsoleDebug.FarKeepFraction = globalDensityValue;
                    GrassConsoleDebug.ThinStartDistance = 0f;
                    break;
                case RowFarKeep:
                    GrassConsoleDebug.FarKeepFraction = Mathf.Clamp01(value);
                    break;
                case RowThinStart:
                    GrassConsoleDebug.ThinStartDistance = Mathf.Clamp(value, 0f, 50f);
                    break;
                case RowCoverage:
                    GrassConsoleDebug.CoverageCompensation = Mathf.Clamp01(value);
                    break;
                case RowSizeX:
                    GrassConsoleDebug.SizeScale = new Vector2(Mathf.Clamp(value, 0.01f, 3f), GrassConsoleDebug.SizeScale.y);
                    break;
                case RowSizeY:
                    GrassConsoleDebug.SizeScale = new Vector2(GrassConsoleDebug.SizeScale.x, Mathf.Clamp(value, 0.01f, 3f));
                    break;
            }
        }

        private (float min, float max) GetRowRange(int row)
        {
            switch (row)
            {
                case RowThinStart: return (0f, 50f);
                case RowSizeX: return (0.01f, 3f);
                case RowSizeY: return (0.01f, 3f);
                default: return (0f, 1f);
            }
        }

        private string GetRowDisplayText(int row)
        {
            if (row == RowOverride)
                return GrassConsoleDebug.OverrideEnabled ? "ON" : "OFF";

            if (row == RowGrassMode)
            {
                if (!GrassConsoleDebug.ModeOverrideEnabled) return "Scene";
                return GrassConsoleDebug.ModeOverride == GrassMode.Default ? "Default" : "CustomMesh";
            }

            if (row == RowBladeType)
            {
                if (!GrassConsoleDebug.BladeTypeOverrideEnabled) return "Scene";
                return GrassConsoleDebug.BladeTypeOverride.ToString();
            }

            if (row == RowSystem)
            {
                switch (systemState)
                {
                    case GrassSystemState.Console: return "Console";
                    case GrassSystemState.Original: return "Original";
                    default: return "Off";
                }
            }

            float value = GetRowValue(row);
            return row == RowThinStart ? value.ToString("F1") : value.ToString("F2");
        }

        private void OnGUI()
        {
            if (!showOverlay) return;

            InitStyles();
            DrawOverlay();
        }

        private void DrawOverlay()
        {
            float width = 360f;
            float height = 144f + RowLabels.Length * 22f + 30f;
            float x = 10f;
            float y = 10f;

            if (anchor == TextAnchor.UpperRight || anchor == TextAnchor.MiddleRight || anchor == TextAnchor.LowerRight)
                x = Screen.width - width - 10f;
            if (anchor == TextAnchor.LowerLeft || anchor == TextAnchor.LowerCenter || anchor == TextAnchor.LowerRight)
                y = Screen.height - height - 10f;

            GUI.Box(new Rect(x, y, width, height), "", boxStyle);

            sb.Clear();
            sb.AppendLine("<b>GRASS CONSOLE DEBUG</b>");
            sb.AppendLine("─────────────────────────────");

            string fpsColor = smoothFPS >= 30f ? "#00FF00" : (smoothFPS >= 24f ? "#FFFF00" : "#FF4444");
            sb.AppendLine($"<color={fpsColor}>FPS: <b>{smoothFPS:F1}</b>  ({avgFrameTime:F2}ms)</color>");

            if (gpuTimingAvailable)
                sb.AppendLine($"GPU: <b>{gpuFrameTime:F2}ms</b>   CPU: {cpuFrameTime:F2}ms");
            else
                sb.AppendLine("<size=11><color=#888888>GPU: n/a — enable Frame Timing Stats</color></size>");

            int total = GrassConsoleDebug.TotalInstances;
            int visible = GrassConsoleDebug.VisibleInstances;
            float culledPct = total > 0 ? (1f - (float)visible / total) * 100f : 0f;
            sb.AppendLine($"Total: <b>{total:N0}</b>   Visible: <b>{visible:N0}</b>");
            sb.AppendLine($"Culled: {culledPct:F1}%");
            sb.AppendLine();

            for (int i = 0; i < RowLabels.Length; i++)
            {
                bool isSelected = i == selectedRow;
                string arrow = isSelected ? "> " : "  ";
                string color = isSelected ? "#FFFF00" : "#FFFFFF";
                sb.AppendLine($"<color={color}>{arrow}{RowLabels[i]}: <b>{GetRowDisplayText(i)}</b></color>");
            }

            sb.AppendLine();
            bool hasGamepad = Gamepad.current != null;
            string toggleHint = hasGamepad
                ? (useShoulderComboToggle ? "[L+R] toggle" : $"[{GamepadHintName(gamepadToggleButton)}] toggle")
                : "[F8] toggle";
            string navHint = hasGamepad ? "D-Pad nav/adjust  Shoulders coarse  A toggle" : "Arrows nav/adjust";
            sb.AppendLine($"<size=11><color=#666666>{toggleHint}   {navHint}</color></size>");

            GUI.Label(new Rect(x + 10f, y + 5f, width - 20f, height - 10f), sb.ToString(), labelStyle);
        }

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
            labelStyle.wordWrap = false;
            labelStyle.normal.textColor = Color.white;
        }

        private static string GamepadHintName(GamepadButton button)
        {
            switch (button)
            {
                case GamepadButton.Select: return "-";
                case GamepadButton.Start: return "+";
                case GamepadButton.LeftShoulder: return "L";
                case GamepadButton.RightShoulder: return "R";
                case GamepadButton.LeftStick: return "L3";
                case GamepadButton.RightStick: return "R3";
                default: return "R3";
            }
        }

        private static UnityEngine.InputSystem.Controls.ButtonControl GetGamepadButtonControl(GamepadButton button)
        {
            var gp = Gamepad.current;
            switch (button)
            {
                case GamepadButton.Select: return gp.selectButton;
                case GamepadButton.Start: return gp.startButton;
                case GamepadButton.LeftShoulder: return gp.leftShoulder;
                case GamepadButton.RightShoulder: return gp.rightShoulder;
                case GamepadButton.LeftStick: return gp.leftStickButton;
                case GamepadButton.RightStick: return gp.rightStickButton;
                default: return gp.rightStickButton;
            }
        }
    }
}
