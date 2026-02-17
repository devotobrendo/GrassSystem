// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using UnityEditor;
using UnityEngine;

namespace GrassSystem
{
    [CustomEditor(typeof(GroundDecalProjector))]
    [CanEditMultipleObjects]
    public class GroundDecalProjectorEditor : Editor
    {
        private SerializedProperty _width;
        private SerializedProperty _height;
        private SerializedProperty _yOffset;
        private SerializedProperty _decalMaterial;
        private SerializedProperty _tiling;
        private SerializedProperty _offset;
        private SerializedProperty _opacity;
        private SerializedProperty _drawDistance;
        private SerializedProperty _startFade;

        private static readonly Color HandleColor = new(0.2f, 0.8f, 1f, 0.8f);

        private void OnEnable()
        {
            _width = serializedObject.FindProperty("width");
            _height = serializedObject.FindProperty("height");
            _yOffset = serializedObject.FindProperty("yOffset");
            _decalMaterial = serializedObject.FindProperty("decalMaterial");
            _tiling = serializedObject.FindProperty("tiling");
            _offset = serializedObject.FindProperty("offset");
            _opacity = serializedObject.FindProperty("opacity");
            _drawDistance = serializedObject.FindProperty("drawDistance");
            _startFade = serializedObject.FindProperty("startFade");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var projector = (GroundDecalProjector)target;

            // Size
            EditorGUILayout.PropertyField(_width);
            EditorGUILayout.PropertyField(_height);

            EditorGUILayout.Space(2);

            // Material with New button
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(_decalMaterial, new GUIContent("Material"));
            if (GUILayout.Button("New", GUILayout.Width(40)))
                CreateNewDecalMaterial(projector);
            EditorGUILayout.EndHorizontal();

            if (projector.decalMaterial == null)
            {
                EditorGUILayout.HelpBox("Assign a material using GrassSystem/GroundDecal shader.", MessageType.Warning);
            }
            else if (projector.decalMaterial.shader.name != "GrassSystem/GroundDecal")
            {
                EditorGUILayout.HelpBox("Material should use GrassSystem/GroundDecal shader.", MessageType.Info);
            }

            EditorGUILayout.Space(2);

            // UV overrides (inline X/Y like URP)
            DrawVector2Field("Tiling", _tiling);
            DrawVector2Field("Offset", _offset);

            // Opacity
            EditorGUILayout.Slider(_opacity, 0f, 1f, new GUIContent("Opacity"));

            EditorGUILayout.Space(2);

            // Draw Distance
            EditorGUILayout.PropertyField(_drawDistance, new GUIContent("Draw Distance"));
            EditorGUI.indentLevel++;
            EditorGUILayout.Slider(_startFade, 0f, 1f, new GUIContent("Start Fade"));
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(2);

            // Y Offset (advanced)
            EditorGUILayout.PropertyField(_yOffset, new GUIContent("Y Offset"));

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawVector2Field(string label, SerializedProperty prop)
        {
            EditorGUI.BeginChangeCheck();
            Rect rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
            rect = EditorGUI.PrefixLabel(rect, new GUIContent(label));

            int prevIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            float halfWidth = rect.width * 0.5f;
            float labelW = 14f;
            float fieldW = halfWidth - labelW - 2f;

            Rect lx = new(rect.x, rect.y, labelW, rect.height);
            Rect fx = new(rect.x + labelW, rect.y, fieldW, rect.height);
            EditorGUI.LabelField(lx, "X");
            float x = EditorGUI.FloatField(fx, prop.vector2Value.x);

            float yStart = rect.x + halfWidth + 4f;
            Rect ly = new(yStart, rect.y, labelW, rect.height);
            Rect fy = new(yStart + labelW, rect.y, fieldW - 4f, rect.height);
            EditorGUI.LabelField(ly, "Y");
            float y = EditorGUI.FloatField(fy, prop.vector2Value.y);

            EditorGUI.indentLevel = prevIndent;

            if (EditorGUI.EndChangeCheck())
                prop.vector2Value = new Vector2(x, y);
        }

        private static void CreateNewDecalMaterial(GroundDecalProjector projector)
        {
            var shader = Shader.Find("GrassSystem/GroundDecal");
            if (shader == null)
            {
                EditorUtility.DisplayDialog("Shader Not Found",
                    "Could not find 'GrassSystem/GroundDecal' shader.", "OK");
                return;
            }

            string path = EditorUtility.SaveFilePanelInProject(
                "Create Decal Material", "NewGroundDecal", "mat",
                "Choose where to save the new Ground Decal material.");

            if (string.IsNullOrEmpty(path)) return;

            var material = new Material(shader)
            {
                name = System.IO.Path.GetFileNameWithoutExtension(path)
            };
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();

            Undo.RecordObject(projector, "Assign New Decal Material");
            projector.decalMaterial = material;
            EditorUtility.SetDirty(projector);
        }

        private void OnSceneGUI()
        {
            var projector = (GroundDecalProjector)target;

            Handles.color = HandleColor;
            Handles.matrix = projector.transform.localToWorldMatrix;

            float halfW = projector.width * 0.5f;
            float halfH = projector.height * 0.5f;
            float y = projector.yOffset;

            Vector3[] corners =
            {
                new(-halfW, y, -halfH),
                new( halfW, y, -halfH),
                new( halfW, y,  halfH),
                new(-halfW, y,  halfH)
            };
            Handles.DrawSolidRectangleWithOutline(corners, new Color(0.2f, 0.8f, 1f, 0.05f), HandleColor);

            float handleSize = HandleUtility.GetHandleSize(Vector3.zero) * 0.06f;

            EditorGUI.BeginChangeCheck();
            Vector3 rh = Handles.Slider(new Vector3(halfW, y, 0), Vector3.right,
                handleSize, Handles.DotHandleCap, 0.01f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(projector, "Resize Decal Width");
                projector.width = Mathf.Max(0.01f, rh.x * 2f);
            }

            EditorGUI.BeginChangeCheck();
            Vector3 fh = Handles.Slider(new Vector3(0, y, halfH), Vector3.forward,
                handleSize, Handles.DotHandleCap, 0.01f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(projector, "Resize Decal Height");
                projector.height = Mathf.Max(0.01f, fh.z * 2f);
            }
        }
    }
}
