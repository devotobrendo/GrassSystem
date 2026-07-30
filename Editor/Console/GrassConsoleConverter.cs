// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using GrassSystem;

namespace GrassSystem.Consoles.Editor
{
    public class GrassConsoleConverterWindow : EditorWindow
    {
        private GrassRenderer sourceRenderer;
        private GrassDataAsset sourceDataAsset;
        private GrassRendererConsole targetRenderer;

        [MenuItem("Tools/Grass System/Convert to Console (Slim)")]
        private static void Open()
        {
            var window = GetWindow<GrassConsoleConverterWindow>(true, "Grass Console Converter", true);
            window.minSize = new Vector2(440, 280);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Bakes a NEW slim console asset (position + width/height only, 20 bytes per blade) from the existing 48-byte grass data. " +
                "The original asset is read-only source data and is never modified, overwritten, or deleted.",
                MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Source (48B, authored data)", EditorStyles.boldLabel);
            sourceRenderer = (GrassRenderer)EditorGUILayout.ObjectField(
                new GUIContent("Scene GrassRenderer", "A GrassRenderer in an open scene. Its currently loaded grass data (GrassDataList) is read, never modified."),
                sourceRenderer, typeof(GrassRenderer), true);

            sourceDataAsset = (GrassDataAsset)EditorGUILayout.ObjectField(
                new GUIContent("Grass Data Asset", "Explicit GrassDataAsset to read from. Takes priority over the Scene GrassRenderer if both are assigned."),
                sourceDataAsset, typeof(GrassDataAsset), false);

            if (sourceRenderer != null && sourceDataAsset == null)
            {
                EditorGUILayout.HelpBox(
                    sourceRenderer.HasExternalData
                        ? "This renderer uses an external data asset. Assign it above (Grass Data Asset) for a reliable read even if the scene isn't loaded, or leave it to read whatever is currently loaded in memory."
                        : "This renderer stores grass data embedded in the scene. Its in-memory data will be read directly.",
                    MessageType.None);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Target (optional)", EditorStyles.boldLabel);
            targetRenderer = (GrassRendererConsole)EditorGUILayout.ObjectField(
                new GUIContent("Console Renderer to Wire", "If assigned, the baked asset is automatically wired onto this GrassRendererConsole's Data Asset field, and re-running the bake regenerates in place."),
                targetRenderer, typeof(GrassRendererConsole), true);

            EditorGUILayout.Space();

            int previewCount = GetSourceCountPreview();
            if (previewCount > 0)
            {
                long originalBytes = (long)previewCount * GrassData.Stride;
                long slimBytes = (long)previewCount * GrassDataConsole.Stride;
                EditorGUILayout.LabelField($"Source instances: {previewCount:N0}");
                EditorGUILayout.LabelField($"Original size: {originalBytes / 1024f:N1} KB   →   Slim size: {slimBytes / 1024f:N1} KB");
            }
            else
            {
                EditorGUILayout.LabelField("Source instances: none found");
            }

            EditorGUILayout.Space();

            GUI.enabled = previewCount > 0;
            if (GUILayout.Button("Bake Slim Console Asset", GUILayout.Height(32)))
            {
                Convert();
            }
            GUI.enabled = true;
        }

        private int GetSourceCountPreview()
        {
            List<GrassData> source = GetEffectiveSourceList(out _, out _);
            return source?.Count ?? 0;
        }

        private List<GrassData> GetEffectiveSourceList(out string originAssetPath, out string sceneName)
        {
            originAssetPath = null;
            sceneName = null;

            if (sourceDataAsset != null)
            {
                originAssetPath = AssetDatabase.GetAssetPath(sourceDataAsset);
                sceneName = sourceDataAsset.SourceScene;
                return sourceDataAsset.LoadData();
            }

            if (sourceRenderer != null && sourceRenderer.GrassDataList != null && sourceRenderer.GrassDataList.Count > 0)
            {
                sceneName = sourceRenderer.gameObject.scene.name;
                return sourceRenderer.GrassDataList;
            }

            return null;
        }

        private void Convert()
        {
            List<GrassData> source = GetEffectiveSourceList(out string originAssetPath, out string sceneName);

            if (source == null || source.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Grass Console Converter",
                    "No source grass data found. Assign a Grass Data Asset, or a GrassRenderer that currently has data loaded in memory.",
                    "OK");
                return;
            }

            GrassDataConsoleAsset targetAsset = ResolveTargetAsset(originAssetPath);
            if (targetAsset == null)
                return;

            GrassConsoleDataBakeService.Bake(source, sceneName, originAssetPath, targetAsset);
            AssetDatabase.Refresh();

            string savedPath = AssetDatabase.GetAssetPath(targetAsset);
            Debug.Log($"GrassConsoleConverter: Baked {source.Count:N0} slim instance(s) ({source.Count * GrassDataConsole.Stride / 1024f:N1} KB) -> {savedPath}. Source data untouched.", targetAsset);

            if (targetRenderer != null)
            {
                Undo.RecordObject(targetRenderer, "Wire Console Grass Data Asset");
                targetRenderer.dataAsset = targetAsset;
                EditorUtility.SetDirty(targetRenderer);
            }

            EditorGUIUtility.PingObject(targetAsset);
        }

        private GrassDataConsoleAsset ResolveTargetAsset(string originAssetPath)
        {
            if (targetRenderer != null && targetRenderer.dataAsset != null)
                return targetRenderer.dataAsset;

            string defaultPath = BuildDefaultPath(originAssetPath);

            GrassDataConsoleAsset existing = AssetDatabase.LoadAssetAtPath<GrassDataConsoleAsset>(defaultPath);
            if (existing != null)
                return existing;

            string directory = Path.GetDirectoryName(defaultPath);
            if (string.IsNullOrEmpty(directory))
                directory = "Assets";

            string chosen = EditorUtility.SaveFilePanelInProject(
                "Save Slim Console Grass Data",
                Path.GetFileNameWithoutExtension(defaultPath),
                "asset",
                "Choose where to save the derived slim console grass data asset. This is a NEW file — the original grass data is never modified.",
                directory);

            if (string.IsNullOrEmpty(chosen))
                return null;

            var asset = ScriptableObject.CreateInstance<GrassDataConsoleAsset>();
            AssetDatabase.CreateAsset(asset, chosen);
            return asset;
        }

        private static string BuildDefaultPath(string originAssetPath)
        {
            if (!string.IsNullOrEmpty(originAssetPath))
            {
                string dir = Path.GetDirectoryName(originAssetPath)?.Replace('\\', '/');
                string name = Path.GetFileNameWithoutExtension(originAssetPath);
                if (!string.IsNullOrEmpty(dir))
                    return $"{dir}/{name}_Console.asset";
            }

            return "Assets/GrassDataConsole.asset";
        }
    }
}
