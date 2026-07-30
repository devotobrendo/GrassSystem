using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using GrassSystem;
using GrassSystem.Consoles;

namespace GrassSystem.Consoles.Editor
{
    public class GrassMigrationDashboard : EditorWindow
    {
        private const string ConsoleRendererGuid = "a4bd31211e9a73847bf15feb2638de85";
        private const string SlimCullingShaderGuid = "85419baaf3e610d4183771d77ad1d592";
        private const string UnattributedKey = "Unattributed";

        private const string ScanProgressTitle = "Grass Migration Scan";
        private const float ScanPhaseData = 0f / 6f;
        private const float ScanPhaseConsoleData = 1f / 6f;
        private const float ScanPhaseSettings = 2f / 6f;
        private const float ScanPhaseDecals = 3f / 6f;
        private const float ScanPhaseRenderers = 4f / 6f;
        private const float ScanPhaseFolders = 5f / 6f;

        private const string RendererScanNameToken = "Grass";
        private static readonly string[] RendererScanPrefabFolders =
        {
            "Assets/GrassSystem-Test/",
            "Assets/Scenes/Fields/",
            "Assets/Prefabs/Fields/",
        };
        private const string RendererScanSceneFolder = "Assets/Scenes/";
        private const string RendererScanScopeNote = "Renderer scan is scoped to prefabs containing \"Grass\" or under GrassSystem-Test/, Scenes/Fields/, Prefabs/Fields/, plus every scene under Scenes/, for speed. A console renderer in an oddly-named or -located prefab could be missed.";

        private static readonly Dictionary<string, string> CodeToScene = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "BCS", "Big City Stadium" },
            { "DY", "Dirt Yards" },
            { "EA", "Eckman Acres" },
            { "PC", "Playground Commons" },
            { "PDF", "Parks Dept Fields No. 2" },
            { "SCD", "Super Colossal Dome" },
            { "SS", "Steele Stadium" },
            { "WB", "Wiffle Ball" },
            { "MainTitle", "Main Title" },
            { "TeamPicker", "Team Picker" },
        };

        private static readonly float[] ColumnWidths = { 170f, 90f, 90f, 120f, 130f, 90f, 90f, 90f };

        private class SceneRow
        {
            public string DisplayName;
            public int OriginalCount;
            public int ConsoleCount;
            public bool HasConsoleSettings;
            public bool HasConsoleRenderer;
            public bool HasBakedDecal;
            public bool HasOrganizedFolder;
            public UnityEngine.Object PingTarget;

            public string Status
            {
                get
                {
                    bool anyConsole = ConsoleCount > 0 || HasConsoleSettings || HasConsoleRenderer;
                    if (!anyConsole) return "Not started";
                    bool migrated = ConsoleCount > 0 && HasConsoleSettings && HasConsoleRenderer;
                    return migrated ? "Migrated" : "Partial";
                }
            }
        }

        private readonly List<SceneRow> rows = new List<SceneRow>();
        private readonly Dictionary<string, SceneRow> rowsByKey = new Dictionary<string, SceneRow>(StringComparer.OrdinalIgnoreCase);
        private Vector2 scrollPos;
        private bool scanned;
        private int selectedTab;

        private GUIContent refreshLabel;
        private GUIContent scanProjectLabel;
        private GUIContent notScannedLabel;
        private GUIContent summaryLabel;
        private GUIContent[] tabLabels;
        private GUIContent[] headerLabels;
        private GUIStyle headerStyle;
        private GUIStyle cellStyle;
        private GUIStyle linkStyle;
        private GUIStyle statusMigratedStyle;
        private GUIStyle statusPartialStyle;
        private GUIStyle statusNotStartedStyle;

        [MenuItem("Tools/Grass System/Migration Dashboard")]
        private static void Open()
        {
            GrassMigrationDashboard window = GetWindow<GrassMigrationDashboard>("Grass Migration");
            window.minSize = new Vector2(920, 320);
        }

        private void InitStyles()
        {
            if (headerStyle != null) return;

            refreshLabel = new GUIContent("Refresh", "Re-scan the project for grass migration assets.");
            scanProjectLabel = new GUIContent("Scan Project", "Scan the project for grass migration status. Can take a while on large projects.");
            notScannedLabel = new GUIContent("Not scanned yet.");
            summaryLabel = new GUIContent(string.Empty);
            tabLabels = new[]
            {
                new GUIContent("Status"),
                new GUIContent("Migrate"),
                new GUIContent("Standardize"),
            };

            headerLabels = new[]
            {
                new GUIContent("Scene"),
                new GUIContent("Original", "GrassDataAsset instance count"),
                new GUIContent("Console", "GrassDataConsoleAsset instance count"),
                new GUIContent("Console Settings", "SO_GrassSettings whose cullingShader is GrassCullingSlim.compute"),
                new GUIContent("Console Renderer", "GrassRendererConsole component found in a prefab or scene"),
                new GUIContent("Baked Decal", "GrassDecalBakeAsset found for this scene"),
                new GUIContent("Organized", "Assets/Grass/<Scene>/ folder exists"),
                new GUIContent("Status"),
            };

            headerStyle = new GUIStyle(EditorStyles.boldLabel);
            cellStyle = new GUIStyle(EditorStyles.label);
            linkStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft };

            statusMigratedStyle = new GUIStyle(EditorStyles.boldLabel);
            statusMigratedStyle.normal.textColor = new Color(0.35f, 0.8f, 0.35f);

            statusPartialStyle = new GUIStyle(EditorStyles.boldLabel);
            statusPartialStyle.normal.textColor = new Color(0.85f, 0.75f, 0.25f);

            statusNotStartedStyle = new GUIStyle(EditorStyles.boldLabel);
            statusNotStartedStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
        }

        private void OnGUI()
        {
            InitStyles();

            selectedTab = GUILayout.Toolbar(selectedTab, tabLabels);
            GUILayout.Space(4);

            switch (selectedTab)
            {
                case 0:
                    DrawStatusTab();
                    break;
                case 1:
                    DrawMigrateTab();
                    break;
                case 2:
                    DrawStandardizeTab();
                    break;
            }
        }

        private void DrawStatusTab()
        {
            if (!scanned)
            {
                DrawEmptyState();
                return;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(refreshLabel, EditorStyles.toolbarButton, GUILayout.Width(70)))
                    Scan();

                GUILayout.Label(summaryLabel, EditorStyles.toolbarButton);
                GUILayout.FlexibleSpace();
            }

            DrawHeader();

            if (rows.Count == 0)
            {
                EditorGUILayout.HelpBox("No grass migration assets found. Click Refresh after the project finishes importing.", MessageType.Info);
                return;
            }

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            for (int i = 0; i < rows.Count; i++)
                DrawRow(rows[i]);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.HelpBox(RendererScanScopeNote, MessageType.Info);
        }

        private void DrawEmptyState()
        {
            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(220)))
                {
                    if (GUILayout.Button(scanProjectLabel, GUILayout.Height(40)))
                        Scan();
                    GUILayout.Space(6);
                    GUILayout.Label(notScannedLabel, EditorStyles.centeredGreyMiniLabel);
                }
                GUILayout.FlexibleSpace();
            }
            GUILayout.FlexibleSpace();
        }

        private void DrawMigrateTab()
        {
            EditorGUILayout.HelpBox("Automated migration — coming. Pending architecture sign-off.", MessageType.Info);
        }

        private void DrawStandardizeTab()
        {
            EditorGUILayout.HelpBox("Asset standardization (folders + naming) — coming. Pending architecture sign-off.", MessageType.Info);
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < headerLabels.Length; i++)
                    GUILayout.Label(headerLabels[i], headerStyle, GUILayout.Width(ColumnWidths[i]));
            }
        }

        private void DrawRow(SceneRow row)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(row.DisplayName, linkStyle, GUILayout.Width(ColumnWidths[0])))
                    PingRow(row);

                GUILayout.Label(row.OriginalCount.ToString("N0"), cellStyle, GUILayout.Width(ColumnWidths[1]));
                GUILayout.Label(row.ConsoleCount.ToString("N0"), cellStyle, GUILayout.Width(ColumnWidths[2]));
                GUILayout.Label(row.HasConsoleSettings ? "Y" : "N", cellStyle, GUILayout.Width(ColumnWidths[3]));
                GUILayout.Label(row.HasConsoleRenderer ? "Y" : "N", cellStyle, GUILayout.Width(ColumnWidths[4]));
                GUILayout.Label(row.HasBakedDecal ? "Y" : "N", cellStyle, GUILayout.Width(ColumnWidths[5]));
                GUILayout.Label(row.HasOrganizedFolder ? "Y" : "N", cellStyle, GUILayout.Width(ColumnWidths[6]));

                GUIStyle statusStyle = row.Status == "Migrated" ? statusMigratedStyle
                    : row.Status == "Partial" ? statusPartialStyle
                    : statusNotStartedStyle;
                GUILayout.Label(row.Status, statusStyle, GUILayout.Width(ColumnWidths[7]));
            }
        }

        private static void PingRow(SceneRow row)
        {
            if (row.PingTarget != null)
                EditorGUIUtility.PingObject(row.PingTarget);
        }

        private void Scan()
        {
            List<SceneRow> backupRows = new List<SceneRow>(rows);
            Dictionary<string, SceneRow> backupByKey = new Dictionary<string, SceneRow>(rowsByKey, StringComparer.OrdinalIgnoreCase);
            bool previousScanned = scanned;
            bool completed = false;

            try
            {
                rows.Clear();
                rowsByKey.Clear();

                if (EditorUtility.DisplayCancelableProgressBar(ScanProgressTitle, "Scanning grass data assets...", ScanPhaseData))
                    return;
                ScanDataAssets();

                if (EditorUtility.DisplayCancelableProgressBar(ScanProgressTitle, "Scanning console data assets...", ScanPhaseConsoleData))
                    return;
                ScanConsoleDataAssets();

                if (EditorUtility.DisplayCancelableProgressBar(ScanProgressTitle, "Scanning console settings...", ScanPhaseSettings))
                    return;
                ScanSettings();

                if (EditorUtility.DisplayCancelableProgressBar(ScanProgressTitle, "Scanning baked decals...", ScanPhaseDecals))
                    return;
                ScanDecals();

                if (!ScanRendererPrefabsAndScenes())
                    return;

                if (EditorUtility.DisplayCancelableProgressBar(ScanProgressTitle, "Scanning organized folders...", ScanPhaseFolders))
                    return;
                ScanOrganizedFolders();

                rows.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));

                if (rowsByKey.TryGetValue(UnattributedKey, out SceneRow unattributed))
                {
                    rows.Remove(unattributed);
                    rows.Add(unattributed);
                }

                scanned = true;
                RecomputeSummary();
                completed = true;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (!completed)
                {
                    rows.Clear();
                    rows.AddRange(backupRows);
                    rowsByKey.Clear();
                    foreach (KeyValuePair<string, SceneRow> kvp in backupByKey)
                        rowsByKey[kvp.Key] = kvp.Value;
                    scanned = previousScanned;
                }
            }

            Repaint();
        }

        private void RecomputeSummary()
        {
            int migrated = 0;
            int total = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].DisplayName == UnattributedKey) continue;
                total++;
                if (rows[i].Status == "Migrated") migrated++;
            }
            summaryLabel = new GUIContent($"{migrated} / {total} scenes migrated");
        }

        private SceneRow GetOrCreateRow(string key)
        {
            if (string.IsNullOrEmpty(key)) key = UnattributedKey;
            if (!rowsByKey.TryGetValue(key, out SceneRow row))
            {
                row = new SceneRow { DisplayName = key };
                rowsByKey[key] = row;
                rows.Add(row);
            }
            return row;
        }

        private void ScanDataAssets()
        {
            string[] guids = AssetDatabase.FindAssets("t:GrassDataAsset");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GrassDataAsset asset = AssetDatabase.LoadAssetAtPath<GrassDataAsset>(path);
                if (asset == null) continue;

                SceneRow row = GetOrCreateRow(NormalizeSceneKey(asset.SourceScene));
                row.OriginalCount += asset.InstanceCount;
                if (row.PingTarget == null)
                    row.PingTarget = asset;
            }
        }

        private void ScanConsoleDataAssets()
        {
            string[] guids = AssetDatabase.FindAssets("t:GrassDataConsoleAsset");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GrassDataConsoleAsset asset = AssetDatabase.LoadAssetAtPath<GrassDataConsoleAsset>(path);
                if (asset == null) continue;

                SceneRow row = GetOrCreateRow(NormalizeSceneKey(asset.SourceScene));
                row.ConsoleCount += asset.InstanceCount;
                row.PingTarget = asset;
            }
        }

        private void ScanSettings()
        {
            string[] guids = AssetDatabase.FindAssets("t:SO_GrassSettings");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                SO_GrassSettings settings = AssetDatabase.LoadAssetAtPath<SO_GrassSettings>(path);
                if (settings == null || !IsConsoleCullingShader(settings.cullingShader)) continue;

                string code = CodeFromPrefixedName(Path.GetFileNameWithoutExtension(path));
                GetOrCreateRow(code).HasConsoleSettings = true;
            }
        }

        private static bool IsConsoleCullingShader(ComputeShader shader)
        {
            if (shader == null) return false;
            string path = AssetDatabase.GetAssetPath(shader);
            if (string.IsNullOrEmpty(path)) return false;
            return AssetDatabase.AssetPathToGUID(path) == SlimCullingShaderGuid;
        }

        private void ScanDecals()
        {
            string[] guids = AssetDatabase.FindAssets("t:GrassDecalBakeAsset");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GetOrCreateRow(CodeFromDecalPath(path)).HasBakedDecal = true;
            }
        }

        private void ScanOrganizedFolders()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                SceneRow row = rows[i];
                if (row.DisplayName == UnattributedKey) continue;
                row.HasOrganizedFolder = AssetDatabase.IsValidFolder($"Assets/Grass/{row.DisplayName}");
            }
        }

        private bool ScanRendererPrefabsAndScenes()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            List<string> prefabCandidates = new List<string>();
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (IsRendererScanPrefabCandidate(path))
                    prefabCandidates.Add(path);
            }

            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
            List<string> sceneCandidates = new List<string>();
            for (int i = 0; i < sceneGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                if (IsRendererScanSceneCandidate(path))
                    sceneCandidates.Add(path);
            }

            int total = prefabCandidates.Count + sceneCandidates.Count;
            int processed = 0;

            for (int i = 0; i < prefabCandidates.Count; i++)
            {
                string path = prefabCandidates[i];
                if (EditorUtility.DisplayCancelableProgressBar(ScanProgressTitle, $"Renderer scan (prefab): {Path.GetFileName(path)}", RendererScanProgress(processed, total)))
                    return false;
                processed++;

                if (ContainsGuid(path, ConsoleRendererGuid))
                {
                    string code = CodeFromPrefixedName(Path.GetFileNameWithoutExtension(path));
                    GetOrCreateRow(code).HasConsoleRenderer = true;
                }
            }

            for (int i = 0; i < sceneCandidates.Count; i++)
            {
                string path = sceneCandidates[i];
                if (EditorUtility.DisplayCancelableProgressBar(ScanProgressTitle, $"Renderer scan (scene): {Path.GetFileName(path)}", RendererScanProgress(processed, total)))
                    return false;
                processed++;

                if (ContainsGuid(path, ConsoleRendererGuid))
                {
                    string key = ResolveSceneFileKey(Path.GetFileNameWithoutExtension(path));
                    GetOrCreateRow(key).HasConsoleRenderer = true;
                }
            }

            return true;
        }

        private static float RendererScanProgress(int processed, int total)
        {
            if (total <= 0) return ScanPhaseRenderers;
            return ScanPhaseRenderers + (ScanPhaseFolders - ScanPhaseRenderers) * (processed / (float)total);
        }

        private static bool IsRendererScanPrefabCandidate(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            if (assetPath.IndexOf(RendererScanNameToken, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            for (int i = 0; i < RendererScanPrefabFolders.Length; i++)
            {
                if (assetPath.StartsWith(RendererScanPrefabFolders[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool IsRendererScanSceneCandidate(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) && assetPath.StartsWith(RendererScanSceneFolder, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsGuid(string assetPath, string guid)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            try
            {
                return File.ReadAllText(assetPath).IndexOf(guid, StringComparison.Ordinal) >= 0;
            }
            catch
            {
                return false;
            }
        }

        private static string TrimDayNightSuffix(string s)
        {
            if (s.EndsWith("_Day", StringComparison.OrdinalIgnoreCase))
                return s.Substring(0, s.Length - 4).TrimEnd();
            if (s.EndsWith("_Night", StringComparison.OrdinalIgnoreCase))
                return s.Substring(0, s.Length - 6).TrimEnd();
            return s;
        }

        private static bool TryResolveKnownScene(string trimmed, out string scene)
        {
            if (string.Equals(trimmed, "TreehouseScene", StringComparison.OrdinalIgnoreCase))
            {
                scene = "Team Picker";
                return true;
            }

            foreach (KeyValuePair<string, string> kvp in CodeToScene)
            {
                if (string.Equals(trimmed, kvp.Key, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(trimmed, kvp.Value, StringComparison.OrdinalIgnoreCase))
                {
                    scene = kvp.Value;
                    return true;
                }
            }

            scene = null;
            return false;
        }

        private static string NormalizeSceneKey(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return UnattributedKey;
            string trimmed = TrimDayNightSuffix(raw.Trim());
            return TryResolveKnownScene(trimmed, out string scene) ? scene : trimmed;
        }

        private static string ResolveSceneFileKey(string nameNoExt)
        {
            if (string.IsNullOrWhiteSpace(nameNoExt)) return null;
            string trimmed = TrimDayNightSuffix(nameNoExt.Trim());
            return TryResolveKnownScene(trimmed, out string scene) ? scene : null;
        }

        private static string CodeFromPrefixedName(string nameNoExt)
        {
            if (string.IsNullOrEmpty(nameNoExt)) return null;
            int idx = nameNoExt.IndexOf('_');
            string prefix = idx > 0 ? nameNoExt.Substring(0, idx) : nameNoExt;
            return CodeToScene.TryGetValue(prefix, out string scene) ? scene : null;
        }

        private static string CodeFromDecalPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            string[] parts = assetPath.Split('/');
            if (parts.Length < 4) return null;
            if (!string.Equals(parts[0], "Assets", StringComparison.OrdinalIgnoreCase)) return null;
            if (!string.Equals(parts[1], "BakedDecals", StringComparison.OrdinalIgnoreCase)) return null;
            return CodeToScene.TryGetValue(parts[2], out string scene) ? scene : null;
        }
    }
}
