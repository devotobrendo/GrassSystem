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
        private SerializedProperty propInstanceDensity;
        private SerializedProperty propSizeScale;
        private SerializedProperty propVariantMode;
        private SerializedProperty propProfileSet;

        private SO_GrassSettings cachedSettings;
        private SerializedObject serializedSettings;

        private bool showData = true;
        private bool showVariant = true;
        private bool showPerformance = true;
        private bool showSize = true;
        private bool showLook;
        private bool showAdvanced;

        private static readonly string[] ReferencesProps = { "grassMode", "proceduralType", "cullingShader", "grassMaterial", "grassMesh" };
        private static readonly string[] WindProps = { "windSpeed", "windStrength", "windFrequency" };
        private static readonly string[] TiltProps = { "maxTiltAngle", "tiltVariation" };
        private static readonly string[] ShadowsLightProps = { "castShadows", "useReceiveShadows", "shadowIntensity" };
        private static readonly string[] DepthProps = { "useDepthPerception", "instanceColorVariation", "heightDarkening", "backfaceDarkening" };
        private static readonly string[] TipProps = { "albedoTexture", "defaultModeAlbedo" };
        private static readonly string[] InteractionProps = { "interactorStrength", "maxInteractors", "maxBendAngle" };
        private static readonly string[] DistanceProps = { "minFadeDistance", "maxDrawDistance" };
        private static readonly string[] BakeDefaultProps = { "minWidth", "maxWidth", "minHeight", "maxHeight" };
        private static readonly string[] MeshCustomProps = { "customMeshes", "minSize", "maxSize", "meshRotationOffset" };

        private static readonly System.Collections.Generic.Dictionary<string, GUIContent> LabelOverrides = new System.Collections.Generic.Dictionary<string, GUIContent>
        {
            { "useReceiveShadows", new GUIContent("Receive Shadows", "Makes the unlit grass receive shadows: enables the _RECEIVE_SHADOWS_ON path plus Shadow Intensity. The standard URP receive-shadows flag is forced on automatically for the console path.") },
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
            propInstanceDensity = serializedObject.FindProperty("instanceDensity");
            propSizeScale = serializedObject.FindProperty("sizeScale");
            propVariantMode = serializedObject.FindProperty("variantMode");
            propProfileSet = serializedObject.FindProperty("profileSet");
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
            DrawVariant();
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

        private void DrawVariant()
        {
            showVariant = EditorGUILayout.Foldout(showVariant, "Platform Variant", true);
            if (!showVariant) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(propVariantMode);
            EditorGUILayout.PropertyField(propProfileSet);

            GrassPlatformProfileSet set = propProfileSet.objectReferenceValue as GrassPlatformProfileSet;
            if (set == null)
            {
                EditorGUILayout.HelpBox("No profile set. Using the scene knobs below (current behaviour).", MessageType.None);
                EditorGUI.indentLevel--;
                return;
            }

            PlatformVariant mode = (PlatformVariant)propVariantMode.enumValueIndex;
            GrassPlatformProfile resolved = set.Resolve(mode);

            if (resolved == null)
            {
                EditorGUILayout.HelpBox($"'{mode}' resolves to an empty slot in {set.name}. Falling back to the scene knobs.", MessageType.Warning);
                EditorGUI.indentLevel--;
                return;
            }

            string overrides = DescribeOverrides(resolved);
            EditorGUILayout.LabelField("Resolved", resolved.name);
            EditorGUILayout.HelpBox(overrides.Length == 0
                ? $"{resolved.name} has no overrides enabled — the scene knobs below are in charge."
                : $"{resolved.name} overrides: {overrides}", MessageType.None);
            EditorGUI.indentLevel--;
        }

        private static string DescribeOverrides(GrassPlatformProfile profile)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (profile.overrideMesh) parts.Add("Mesh");
            if (profile.overrideThinning) parts.Add("Thinning");
            if (profile.overrideInstanceDensity) parts.Add("Instance Density");
            if (profile.overrideAlbedo) parts.Add("Albedo");
            if (profile.overrideDrawDistance) parts.Add("Draw Distance");
            if (profile.overrideShadows) parts.Add("Cast Shadows");
            if (profile.overrideReceiveShadows) parts.Add("Receive Shadows");
            return string.Join(", ", parts);
        }

        private void DrawPerformance()
        {
            showPerformance = EditorGUILayout.Foldout(showPerformance, "Performance", true);
            if (!showPerformance) return;

            EditorGUI.indentLevel++;

            GrassPlatformProfileSet set = propProfileSet.objectReferenceValue as GrassPlatformProfileSet;
            GrassPlatformProfile resolved = set != null ? set.Resolve((PlatformVariant)propVariantMode.enumValueIndex) : null;
            bool thinningOverridden = resolved != null && resolved.overrideThinning;
            if (thinningOverridden)
                EditorGUILayout.HelpBox($"Overridden by {resolved.name} — these values are ignored while that profile is active.", MessageType.Warning);

            using (new EditorGUI.DisabledScope(thinningOverridden))
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(propFarKeepFraction);
                EditorGUILayout.PropertyField(propThinStartDistance);
                EditorGUILayout.PropertyField(propCoverageCompensation);
                if (EditorGUI.EndChangeCheck())
                    SceneView.RepaintAll();
            }

            EditorGUILayout.Space();
            bool densityOverridden = resolved != null && resolved.overrideInstanceDensity;
            if (densityOverridden)
                EditorGUILayout.HelpBox($"Instance Density is overridden by {resolved.name} ({resolved.instanceDensity:P0}) — this value is ignored while that profile is active.", MessageType.Warning);

            using (new EditorGUI.DisabledScope(densityOverridden))
                EditorGUILayout.PropertyField(propInstanceDensity);

            int baked = console.BakedInstanceCount;
            int uploaded = console.TotalGrassCount;
            if (baked > 0 && uploaded < baked)
            {
                float savedMb = (baked - uploaded) * 20f / (1024f * 1024f);
                EditorGUILayout.HelpBox($"Uploading {uploaded:N0} of {baked:N0} baked instances ({(float)uploaded / baked:P0}). Source buffer and cull dispatch shrink with it — about {savedMb:F1} MB less on the GPU. Preview only; the data asset is untouched.", MessageType.Info);
            }

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
            DrawGroup("Shadows", ShadowsLightProps);
            EditorGUILayout.Space();
            DrawGroup("Depth", DepthProps);
            EditorGUILayout.Space();
            DrawGroup("Textures", TipProps);
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
