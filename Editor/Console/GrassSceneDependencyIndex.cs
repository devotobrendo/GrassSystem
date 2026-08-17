// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace GrassSystem.Consoles.Editor
{
    public class GrassSceneDependencies
    {
        public string scenePath;
        public string sceneName;
        public HashSet<string> deps;
    }

    public static class GrassSceneDependencyIndex
    {
        private const string ScenesFolder = "Assets/Scenes";

        private static readonly Dictionary<string, GrassSceneDependencies> entries =
            new Dictionary<string, GrassSceneDependencies>(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> stale =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static bool built;

        public static bool HasCache => built;

        public static int SceneCount => entries.Count;

        public static int StaleCount => stale.Count;

        public static void Invalidate()
        {
            entries.Clear();
            stale.Clear();
            built = false;
        }

        public static void MarkSceneStale(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath)) return;
            if (!built) return;
            stale.Add(scenePath);
        }

        public static void RewritePath(string oldPath, string newPath)
        {
            if (!built) return;
            if (string.IsNullOrEmpty(oldPath) || string.IsNullOrEmpty(newPath)) return;
            if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase)) return;

            if (oldPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) &&
                entries.TryGetValue(oldPath, out GrassSceneDependencies moved))
            {
                entries.Remove(oldPath);
                moved.scenePath = newPath;
                moved.sceneName = Path.GetFileNameWithoutExtension(newPath);
                entries[newPath] = moved;

                if (stale.Remove(oldPath))
                    stale.Add(newPath);
            }

            foreach (GrassSceneDependencies entry in entries.Values)
            {
                if (entry.deps == null) continue;
                if (!entry.deps.Remove(oldPath)) continue;
                entry.deps.Add(newPath);
            }
        }

        public static bool TryGet(out List<GrassSceneDependencies> result)
        {
            result = null;

            if (!built)
            {
                if (!Rebuild())
                    return false;
            }
            else if (stale.Count > 0 && !RefreshStale())
            {
                return false;
            }

            result = new List<GrassSceneDependencies>(entries.Values);
            return true;
        }

        private static bool Rebuild()
        {
            entries.Clear();
            stale.Clear();

            List<string> scenePaths = CollectScenePaths();
            bool cancelled = false;

            try
            {
                for (int i = 0; i < scenePaths.Count; i++)
                {
                    string scenePath = scenePaths[i];
                    string sceneName = Path.GetFileNameWithoutExtension(scenePath);

                    if (EditorUtility.DisplayCancelableProgressBar(
                            "Grass Hub",
                            $"Reading {sceneName} ({i + 1}/{scenePaths.Count}) - this runs once per session",
                            scenePaths.Count == 0 ? 0f : (float)i / scenePaths.Count))
                    {
                        cancelled = true;
                        break;
                    }

                    entries[scenePath] = Read(scenePath, sceneName);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (cancelled)
            {
                entries.Clear();
                built = false;
                return false;
            }

            built = true;
            return true;
        }

        private static bool RefreshStale()
        {
            var pending = new List<string>(stale);
            bool cancelled = false;

            try
            {
                for (int i = 0; i < pending.Count; i++)
                {
                    string scenePath = pending[i];
                    string sceneName = Path.GetFileNameWithoutExtension(scenePath);

                    if (EditorUtility.DisplayCancelableProgressBar(
                            "Grass Hub",
                            $"Re-reading {sceneName} ({i + 1}/{pending.Count})",
                            pending.Count == 0 ? 0f : (float)i / pending.Count))
                    {
                        cancelled = true;
                        break;
                    }

                    if (File.Exists(scenePath))
                        entries[scenePath] = Read(scenePath, sceneName);
                    else
                        entries.Remove(scenePath);

                    stale.Remove(scenePath);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return !cancelled;
        }

        private static GrassSceneDependencies Read(string scenePath, string sceneName)
        {
            return new GrassSceneDependencies
            {
                scenePath = scenePath,
                sceneName = sceneName,
                deps = new HashSet<string>(AssetDatabase.GetDependencies(scenePath, true), StringComparer.OrdinalIgnoreCase),
            };
        }

        private static List<string> CollectScenePaths()
        {
            var ordered = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string[] guids = AssetDatabase.IsValidFolder(ScenesFolder)
                ? AssetDatabase.FindAssets("t:Scene", new[] { ScenesFolder })
                : AssetDatabase.FindAssets("t:Scene");

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) || !seen.Add(path)) continue;
                ordered.Add(path);
            }

            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene == null || string.IsNullOrEmpty(scene.path)) continue;
                if (!File.Exists(scene.path) || !seen.Add(scene.path)) continue;
                ordered.Add(scene.path);
            }

            ordered.Sort(StringComparer.OrdinalIgnoreCase);
            return ordered;
        }

        private class ChangeWatcher : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(
                string[] importedAssets,
                string[] deletedAssets,
                string[] movedAssets,
                string[] movedFromAssetPaths)
            {
                if (!built) return;

                int pairs = Math.Min(movedAssets.Length, movedFromAssetPaths.Length);
                for (int i = 0; i < pairs; i++)
                    RewritePath(movedFromAssetPaths[i], movedAssets[i]);

                for (int i = 0; i < importedAssets.Length; i++)
                    if (importedAssets[i].EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                        MarkSceneStale(importedAssets[i]);

                for (int i = 0; i < deletedAssets.Length; i++)
                    if (deletedAssets[i].EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                        MarkSceneStale(deletedAssets[i]);
            }
        }
    }
}
