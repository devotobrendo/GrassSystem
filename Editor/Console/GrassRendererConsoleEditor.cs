// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using UnityEditor;
using UnityEngine;
using GrassSystem;
using GrassSystem.Consoles;

namespace GrassSystem.Consoles.Editor
{
    [CustomEditor(typeof(GrassSystem.Consoles.GrassRendererConsole))]
    public class GrassRendererConsoleEditor : UnityEditor.Editor
    {
        private GrassRendererConsole console;

        private SerializedProperty propSettings;
        private SerializedProperty propDataAsset;
        private SerializedProperty propBakedDecalAsset;
        private SerializedProperty propFarKeepFraction;
        private SerializedProperty propThinStartDistance;
        private SerializedProperty propCoverageCompensation;
        private SerializedProperty propSizeScale;

        private SO_GrassSettings cachedSettings;
        private SerializedObject serializedSettings;

        private bool showData = true;
        private bool showPerformance = true;
        private bool showSize = true;
        private bool showLook;
        private bool showAdvanced;

        private static readonly string[] ReferencesProps = { "grassMode", "cullingShader", "grassMaterial", "grassMesh" };
        private static readonly string[] WindProps = { "windSpeed", "windStrength", "windFrequency" };
        private static readonly string[] TiltProps = { "maxTiltAngle", "tiltVariation" };
        private static readonly string[] ShadowsLightProps = { "receiveShadows", "castShadows", "useReceiveShadows", "shadowIntensity", "useLightProbes", "lightProbeInfluence", "ambientBoost" };
        private static readonly string[] DepthProps = { "useDepthPerception", "instanceColorVariation", "heightDarkening", "backfaceDarkening" };
        private static readonly string[] TipProps = { "useTipCutout", "tipMaskTexture", "tipCutoffHeight", "albedoTexture" };
        private static readonly string[] InteractionProps = { "interactorStrength", "maxInteractors", "maxBendAngle" };
        private static readonly string[] DistanceProps = { "minFadeDistance", "maxDrawDistance" };
        private static readonly string[] BakeDefaultProps = { "minWidth", "maxWidth", "minHeight", "maxHeight" };
        private static readonly string[] MeshCustomProps = { "customMeshes", "minSize", "maxSize", "meshRotationOffset" };

        private static readonly System.Collections.Generic.Dictionary<string, GUIContent> LabelOverrides = new System.Collections.Generic.Dictionary<string, GUIContent>
        {
            { "receiveShadows", new GUIContent("Receive Shadows (URP flag)", "Standard URP renderer flag. Keep it on. On its own it does NOT show shadows on the unlit grass - the toggle that does is 'Receive Shadows (Unlit)' below.") },
            { "useReceiveShadows", new GUIContent("Receive Shadows (Unlit)", "THIS is the toggle that makes the grass receive shadows: it enables the _RECEIVE_SHADOWS_ON path plus Shadow Intensity in the unlit shader.") },
        };

        private void OnEnable()
        {
            console = (GrassRendererConsole)target;
            propSettings = serializedObject.FindProperty("settings");
            propDataAsset = serializedObject.FindProperty("dataAsset");
            propBakedDecalAsset = serializedObject.FindProperty("bakedDecalAsset");
            propFarKeepFraction = serializedObject.FindProperty("farKeepFraction");
            propThinStartDistance = serializedObject.FindProperty("thinStartDistance");
            propCoverageCompensation = serializedObject.FindProperty("coverageCompensation");
            propSizeScale = serializedObject.FindProperty("sizeScale");
            RefreshSettingsSerializedObject();
        }

        private void OnDisable()
        {
            serializedSettings?.Dispose();
            serializedSettings = null;
        }

        private void RefreshSettingsSerializedObject()
        {
            if (console == null) return;
            if (console.settings == cachedSettings) return;

            serializedSettings?.Dispose();
            cachedSettings = console.settings;
            serializedSettings = cachedSettings != null ? new SerializedObject(cachedSettings) : null;
        }

        public override void OnInspectorGUI()
        {
            if (console == null) return;

            serializedObject.Update();
            RefreshSettingsSerializedObject();

            DrawStatus();
            EditorGUILayout.Space();
            DrawData();
            EditorGUILayout.Space();
            DrawPerformance();
            EditorGUILayout.Space();
            DrawSize();
            EditorGUILayout.Space();
            DrawLook();
            EditorGUILayout.Space();
            DrawAdvanced();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawStatus()
        {
            bool settingsInvalid = console.settings == null || console.settings.cullingShader == null || console.settings.grassMaterial == null;
            if (settingsInvalid)
                EditorGUILayout.HelpBox("needs a console SO (cullingShader = GrassCullingSlim.compute, material on GrassUnlitConsole)", MessageType.Warning);

            bool dataInvalid = console.dataAsset == null || console.dataAsset.InstanceCount == 0;
            if (dataInvalid)
                EditorGUILayout.HelpBox("No data asset assigned, or it has 0 baked instances. Bake one via Tools/Grass System/Convert to Console (Slim).", MessageType.Warning);

            if (!settingsInvalid && !dataInvalid)
                EditorGUILayout.LabelField($"Instances {console.dataAsset.InstanceCount:N0}   Visible {console.VisibleGrassCount:N0}   Mode {console.settings.grassMode}");
        }

        private void DrawData()
        {
            showData = EditorGUILayout.Foldout(showData, "Data", true);
            if (!showData) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(propSettings);
            EditorGUILayout.PropertyField(propDataAsset);
            EditorGUILayout.PropertyField(propBakedDecalAsset);
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Load From Data Asset"))
                    console.LoadFromDataAsset();
                if (GUILayout.Button("Reinitialize"))
                    console.ForceReinitialize();
            }
            EditorGUI.indentLevel--;
        }

        private void DrawPerformance()
        {
            showPerformance = EditorGUILayout.Foldout(showPerformance, "Performance", true);
            if (!showPerformance) return;

            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(propFarKeepFraction);
            EditorGUILayout.PropertyField(propThinStartDistance);
            EditorGUILayout.PropertyField(propCoverageCompensation);
            if (EditorGUI.EndChangeCheck())
                SceneView.RepaintAll();

            EditorGUILayout.LabelField("Visible Grass Count", console.VisibleGrassCount.ToString("N0"));
            EditorGUI.indentLevel--;
        }

        private void DrawSize()
        {
            showSize = EditorGUILayout.Foldout(showSize, "Size", true);
            if (!showSize) return;

            EditorGUI.indentLevel++;
            bool isCustomMesh = console.settings != null && console.settings.grassMode == GrassMode.CustomMesh;
            if (isCustomMesh)
            {
                Vector2 current = propSizeScale.vector2Value;
                EditorGUI.BeginChangeCheck();
                float uniform = EditorGUILayout.FloatField("Size Scale (Uniform)", current.x);
                if (EditorGUI.EndChangeCheck())
                    propSizeScale.vector2Value = new Vector2(uniform, current.y);
                EditorGUILayout.HelpBox("Custom Mesh: x = uniform scale, y ignored", MessageType.None);
            }
            else
            {
                EditorGUILayout.PropertyField(propSizeScale);
                EditorGUILayout.HelpBox("Default: x = width, y = height", MessageType.None);
            }
            EditorGUILayout.HelpBox("Base blade size is BAKED into the data asset; sizeScale is the live multiplier.", MessageType.Info);
            EditorGUI.indentLevel--;
        }

        private void DrawLook()
        {
            showLook = EditorGUILayout.Foldout(showLook, "Look", true);
            if (!showLook) return;

            EditorGUI.indentLevel++;
            if (serializedSettings == null)
            {
                EditorGUILayout.HelpBox("No settings assigned.", MessageType.Warning);
                EditorGUI.indentLevel--;
                return;
            }

            serializedSettings.Update();

            DrawGroup("References", ReferencesProps);
            EditorGUILayout.Space();
            DrawGroup("Wind", WindProps);
            EditorGUILayout.Space();
            DrawGroup("Tilt", TiltProps);
            EditorGUILayout.Space();
            DrawGroup("Shadows & Light", ShadowsLightProps);
            EditorGUILayout.Space();
            DrawGroup("Depth", DepthProps);
            EditorGUILayout.Space();
            DrawGroup("Tip", TipProps);
            EditorGUILayout.Space();
            DrawGroup("Interaction", InteractionProps);
            EditorGUILayout.Space();
            DrawGroup("Distance", DistanceProps);

            if (cachedSettings.grassMode == GrassMode.Default)
            {
                EditorGUILayout.Space();
                DrawGroup("Bake — Default mode (affects next re-bake)", BakeDefaultProps);
            }
            else if (cachedSettings.grassMode == GrassMode.CustomMesh)
            {
                EditorGUILayout.Space();
                DrawGroup("Mesh — Custom mode", MeshCustomProps);
            }

            if (serializedSettings.ApplyModifiedProperties())
            {
                console.MarkMaterialDirty();
                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();
            }
            EditorGUI.indentLevel--;
        }

        private void DrawGroup(string header, string[] propNames)
        {
            EditorGUILayout.LabelField(header, EditorStyles.boldLabel);
            foreach (string propName in propNames)
            {
                SerializedProperty prop = serializedSettings.FindProperty(propName);
                if (prop == null) continue;
                if (LabelOverrides.TryGetValue(propName, out GUIContent label))
                    EditorGUILayout.PropertyField(prop, label, true);
                else
                    EditorGUILayout.PropertyField(prop, true);
            }
        }

        private void DrawAdvanced()
        {
            showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced", true);
            if (!showAdvanced) return;

            EditorGUI.indentLevel++;
            if (GUILayout.Button("Select Full Settings Asset") && console.settings != null)
                Selection.activeObject = console.settings;

            if (GUILayout.Button("Force Reinitialize"))
                console.ForceReinitialize();

            if (serializedSettings != null)
            {
                SerializedProperty boundsProp = serializedSettings.FindProperty("drawCullingBounds");
                if (boundsProp != null)
                {
                    serializedSettings.Update();
                    EditorGUILayout.PropertyField(boundsProp);
                    serializedSettings.ApplyModifiedProperties();
                }
            }
            EditorGUI.indentLevel--;
        }
    }
}
