using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GrassSystem.Consoles;

namespace GrassSystem.Consoles.Editor
{
    public class GrassHubWindow : EditorWindow
    {
        private enum SceneGrassState { NoGrass, Authored, NeedsDecalBake, NeedsConvert, NeedsProfile, Ready }

        private class SceneScanInfo
        {
            public string scenePath;
            public string sceneName;
            public UnityEngine.Object sceneAsset;
            public bool hasOriginalSettings;
            public bool hasOriginalDataAsset;
            public bool hasConsoleData;
            public bool hasDecal;
            public HashSet<string> deps;
        }

        private class SceneRow
        {
            public SceneScanInfo info;
            public SceneGrassState state;
            public string displayName;
            public string stateLabel;
            public string actionLabel;
            public bool isOpenScene;
        }

        private const string MenuPathPainter = "Tools/Grass System/Grass Painter";
        private const string MenuPathDecalBaker = "Tools/Grass System/Grass Decal Baker";
        private const string MenuPathConverter = "Tools/Grass System/Convert to Console (Slim)";
        private const string MenuPathDashboard = "Tools/Grass System/Migration Dashboard";

        private const string ProfileSetDefaultFolder = "Assets/Grass/_Shared";
        private const string ProfileSetPrefsKey = "GrassHub.SelectedProfileSetPath";

        private const float SceneColumnWidth = 250f;
        private const float StateColumnWidth = 130f;
        private const float ActionColumnWidth = 170f;
        private const float OpenColumnWidth = 56f;

        private readonly List<SceneScanInfo> scanInfos = new List<SceneScanInfo>();
        private readonly List<SceneRow> sceneRows = new List<SceneRow>();

        private GrassPlatformProfileSet[] foundProfileSets = Array.Empty<GrassPlatformProfileSet>();
        private string[] foundProfileSetNames = Array.Empty<string>();
        private int selectedProfileSetIndex = -1;
        private GrassPlatformProfileSet profileSet;
        private int scenesWithConsoleDataCount;
        private string blastRadiusWarningText = string.Empty;

        private List<StandardizePlanEntry> standardizePlan;
        private StandardizeApplyResult lastApply;
        private int standardizeMovableCount;
        private string standardizeSummaryText = string.Empty;

        private MigrationResult lastMigration;
        private GrassObjectRenameResult lastRename;

        private bool scanned;
        private Vector2 boardScrollPos;

        private string tuningJson = string.Empty;
        private GrassPlatformProfile tuningTarget;
        private string tuningStatus = string.Empty;
        private MessageType tuningStatusType = MessageType.None;
        private Vector2 tuningScrollPos;

        private GUIStyle titleStyle;
        private GUIStyle linkStyle;
        private GUIStyle linkBoldStyle;
        private GUIStyle linkGrayStyle;
        private GUIStyle stateReadyStyle;
        private GUIStyle stateWarnStyle;
        private GUIStyle stateNeutralStyle;
        private GUIStyle stateGrayStyle;

        private static readonly GUIContent BuildFilterContent = new GUIContent("Scenes in build", "Scans only the scenes enabled in Build Settings. Uncheck to scan a folder instead. Either way, scenes without grass are left out.");
        private static readonly GUIContent OpenButtonContent = new GUIContent("Open", "Opens this scene, prompting to save the current one first.");
        private static readonly GUIContent MigrateButtonContent = new GUIContent("Migrate Open Scene", "Adds console renderers and creates assets for the currently open scene. Requires a valid open scene.");
        private static readonly GUIContent RevertButtonContent = new GUIContent("Revert Open Scene", "Reverts console migration in the currently open scene. Requires a valid open scene.");
        private static readonly GUIContent RenameButtonContent = new GUIContent("Rename Scene Objects", "Renames grass renderers, console objects, and GrassDecal objects in the open scene to the Grass_<Veg> convention. Undoable. Requires a valid open scene.");

        [MenuItem("Tools/Grass System/Grass Hub", priority = 0)]
        private static void Open()
        {
            GrassHubWindow window = GetWindow<GrassHubWindow>("Grass Hub");
            window.minSize = new Vector2(820, 620);
        }

        private const string SceneFolderPrefKey = "GrassHub.SceneSearchFolder";
        private const string OnlyInBuildPrefKey = "GrassHub.OnlyScenesInBuild";
        private const string DefaultSceneFolder = "Assets/Scenes";
        private string sceneSearchFolder = DefaultSceneFolder;
        private bool onlyScenesInBuild = true;

        private void OnEnable()
        {
            minSize = new Vector2(820, 620);
            sceneSearchFolder = EditorPrefs.GetString(SceneFolderPrefKey, DefaultSceneFolder);
            onlyScenesInBuild = EditorPrefs.GetBool(OnlyInBuildPrefKey, true);
            ResolveProfileSets();
        }

        private void InitStyles()
        {
            if (titleStyle != null) return;

            titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };

            linkStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft };
            linkBoldStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleLeft };
            linkGrayStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft };
            linkGrayStyle.normal.textColor = Color.gray;

            stateReadyStyle = new GUIStyle(EditorStyles.boldLabel);
            stateReadyStyle.normal.textColor = new Color(0.35f, 0.8f, 0.35f);

            stateWarnStyle = new GUIStyle(EditorStyles.boldLabel);
            stateWarnStyle.normal.textColor = new Color(0.85f, 0.75f, 0.25f);

            stateNeutralStyle = new GUIStyle(EditorStyles.label);

            stateGrayStyle = new GUIStyle(EditorStyles.label);
            stateGrayStyle.normal.textColor = Color.gray;

        }

        private void OnGUI()
        {
            InitStyles();

            DrawHeader();
            EditorGUILayout.Space();
            DrawBoard();
            EditorGUILayout.Space();
            DrawSceneActions();
            EditorGUILayout.Space();
            DrawAssetsSection();
            EditorGUILayout.Space();
            DrawTuningSection();
            EditorGUILayout.Space();
            DrawToolsSection();
        }

        private void DrawTuningSection()
        {
            EditorGUILayout.LabelField("Device Tuning", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Tune on device, press the dump button in the overlay, then paste the [GrassTuning] line here.", EditorStyles.miniLabel);

            tuningTarget = (GrassPlatformProfile)EditorGUILayout.ObjectField("Target Profile", tuningTarget, typeof(GrassPlatformProfile), false);

            tuningScrollPos = EditorGUILayout.BeginScrollView(tuningScrollPos, GUILayout.Height(80));
            tuningJson = EditorGUILayout.TextArea(tuningJson, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Paste From Clipboard", GUILayout.Width(160)))
                {
                    tuningJson = EditorGUIUtility.systemCopyBuffer;
                    ClearTuningStatus();
                    GUI.FocusControl(null);
                }

                using (new EditorGUI.DisabledScope(tuningTarget == null || string.IsNullOrWhiteSpace(tuningJson)))
                {
                    if (GUILayout.Button("Apply To Profile", GUILayout.Width(140)))
                        ApplyTuningToProfile();
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Clear", GUILayout.Width(70)))
                {
                    tuningJson = string.Empty;
                    ClearTuningStatus();
                    GUI.FocusControl(null);
                }
            }

            if (!string.IsNullOrEmpty(tuningStatus))
                EditorGUILayout.HelpBox(tuningStatus, tuningStatusType);
        }

        private void ClearTuningStatus()
        {
            tuningStatus = string.Empty;
            tuningStatusType = MessageType.None;
        }

        private void ApplyTuningToProfile()
        {
            string payload = ExtractTuningJson(tuningJson);

            if (!GrassTuningSnapshot.TryParse(payload, out GrassTuningSnapshot snapshot, out string error))
            {
                tuningStatus = error;
                tuningStatusType = MessageType.Error;
                return;
            }

            Undo.RecordObject(tuningTarget, "Apply Grass Tuning");
            snapshot.ApplyTo(tuningTarget);
            EditorUtility.SetDirty(tuningTarget);
            AssetDatabase.SaveAssets();

            string note = snapshot.DescribeUnapplied();
            if (string.IsNullOrEmpty(note))
            {
                tuningStatus = $"Applied to {tuningTarget.name}.";
                tuningStatusType = MessageType.Info;
            }
            else
            {
                tuningStatus = $"Applied to {tuningTarget.name}.\n{note}";
                tuningStatusType = MessageType.Warning;
            }
        }

        private static string ExtractTuningJson(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;

            int start = raw.IndexOf('{');
            int end = raw.LastIndexOf('}');
            if (start < 0 || end <= start) return raw;

            return raw.Substring(start, end - start + 1);
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Grass Hub", titleStyle);
                GUILayout.FlexibleSpace();
                EditorGUI.BeginChangeCheck();
                onlyScenesInBuild = GUILayout.Toggle(onlyScenesInBuild, BuildFilterContent, GUILayout.Width(112));
                if (EditorGUI.EndChangeCheck())
                    EditorPrefs.SetBool(OnlyInBuildPrefKey, onlyScenesInBuild);

                using (new EditorGUI.DisabledScope(onlyScenesInBuild))
                {
                    EditorGUILayout.LabelField("in", GUILayout.Width(14));
                    EditorGUI.BeginChangeCheck();
                    sceneSearchFolder = EditorGUILayout.TextField(sceneSearchFolder, GUILayout.Width(170));
                    if (EditorGUI.EndChangeCheck())
                        EditorPrefs.SetString(SceneFolderPrefKey, sceneSearchFolder);
                }

                if (GUILayout.Button(scanned ? "Rescan" : "Scan", GUILayout.Width(80)))
                    Rescan();
            }

            if (!scanned)
            {
                string source = onlyScenesInBuild
                    ? "the scenes enabled in Build Settings"
                    : $"the scenes under '{sceneSearchFolder}'";
                EditorGUILayout.HelpBox($"Not scanned yet. Press Scan to walk {source}. Each scene's full dependency graph is resolved, so keeping this list short is what keeps the scan fast. Scenes without grass never show up.", MessageType.Info);
            }

            EditorGUILayout.Space(2);

            if (foundProfileSets.Length > 1)
            {
                EditorGUI.BeginChangeCheck();
                selectedProfileSetIndex = EditorGUILayout.Popup("Profile Set", selectedProfileSetIndex, foundProfileSetNames);
                if (EditorGUI.EndChangeCheck())
                {
                    profileSet = foundProfileSets[selectedProfileSetIndex];
                    EditorPrefs.SetString(ProfileSetPrefsKey, AssetDatabase.GetAssetPath(profileSet));
                    RecomputeRows();
                }
            }
            else if (foundProfileSets.Length == 1)
            {
                EditorGUILayout.LabelField("Profile Set", profileSet != null ? profileSet.name : "-");
            }

            if (foundProfileSets.Length == 0)
            {
                EditorGUILayout.HelpBox("No GrassPlatformProfileSet found in the project.", MessageType.Warning);
                if (GUILayout.Button("Create Profile Set", GUILayout.Width(160)))
                    CreateProfileSet();
            }
            else if (profileSet != null)
            {
                EditorGUILayout.HelpBox(blastRadiusWarningText, MessageType.Warning);
            }
        }

        private void DrawBoard()
        {
            EditorGUILayout.LabelField("Scenes", EditorStyles.boldLabel);

            if (sceneRows.Count == 0)
            {
                EditorGUILayout.HelpBox("No scenes with grass data found in the project.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Scene", EditorStyles.boldLabel, GUILayout.Width(SceneColumnWidth));
                GUILayout.Label(GUIContent.none, EditorStyles.boldLabel, GUILayout.Width(OpenColumnWidth));
                GUILayout.Label("State", EditorStyles.boldLabel, GUILayout.Width(StateColumnWidth));
                GUILayout.Label("Next Action", EditorStyles.boldLabel, GUILayout.Width(ActionColumnWidth));
                GUILayout.FlexibleSpace();
            }

            boardScrollPos = EditorGUILayout.BeginScrollView(boardScrollPos, GUILayout.ExpandHeight(true), GUILayout.MinHeight(140));
            for (int i = 0; i < sceneRows.Count; i++)
                DrawSceneRow(sceneRows[i]);
            EditorGUILayout.EndScrollView();
        }

        private void DrawSceneRow(SceneRow row)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUIStyle nameStyle = row.state == SceneGrassState.NoGrass ? linkGrayStyle : (row.isOpenScene ? linkBoldStyle : linkStyle);
                if (GUILayout.Button(row.displayName, nameStyle, GUILayout.Width(SceneColumnWidth)))
                    EditorGUIUtility.PingObject(row.info.sceneAsset);

                using (new EditorGUI.DisabledScope(row.isOpenScene))
                {
                    if (GUILayout.Button(OpenButtonContent, GUILayout.Width(OpenColumnWidth)) &&
                        OpenSceneIfNeeded(row.info.scenePath))
                    {
                        RecomputeRows();
                        Repaint();
                        GUIUtility.ExitGUI();
                    }
                }

                GUILayout.Label(row.stateLabel, GetStateStyle(row.state), GUILayout.Width(StateColumnWidth));

                if (row.state == SceneGrassState.NoGrass)
                    GUILayout.Label("-", GUILayout.Width(ActionColumnWidth));
                else if (GUILayout.Button(row.actionLabel, GUILayout.Width(ActionColumnWidth)))
                    ExecuteRowAction(row);

                GUILayout.FlexibleSpace();
            }
        }

        private GUIStyle GetStateStyle(SceneGrassState state)
        {
            switch (state)
            {
                case SceneGrassState.Ready: return stateReadyStyle;
                case SceneGrassState.NeedsProfile:
                case SceneGrassState.NeedsConvert:
                case SceneGrassState.NeedsDecalBake: return stateWarnStyle;
                case SceneGrassState.Authored: return stateNeutralStyle;
                default: return stateGrayStyle;
            }
        }

        private void DrawSceneActions()
        {
            EditorGUILayout.LabelField("Open Scene Actions", EditorStyles.boldLabel);

            Scene activeScene = SceneManager.GetActiveScene();
            bool validScene = activeScene.IsValid();

            EditorGUILayout.LabelField("Active Scene", validScene ? activeScene.name : "(none)");

            using (new EditorGUI.DisabledScope(!validScene))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(MigrateButtonContent, GUILayout.Height(24)))
                    {
                        if (EditorUtility.DisplayDialog("Migrate Scene", $"Migrate scene '{activeScene.name}'? This adds console renderers and creates assets.", "Migrate", "Cancel"))
                        {
                            lastMigration = GrassSceneMigrator.MigrateOpenScene();
                            Rescan();
                        }
                    }

                    if (GUILayout.Button(RevertButtonContent, GUILayout.Height(24)))
                    {
                        if (EditorUtility.DisplayDialog("Revert Scene", $"Revert console migration in scene '{activeScene.name}'?", "Revert", "Cancel"))
                        {
                            GrassSceneMigrator.RevertOpenScene();
                            lastMigration = null;
                            Rescan();
                        }
                    }

                    if (GUILayout.Button(RenameButtonContent, GUILayout.Height(24)))
                    {
                        if (EditorUtility.DisplayDialog("Rename Scene Objects", "Rename grass renderers, console objects, and GrassDecal objects in the open scene to the Grass_<Veg> convention? (Undoable)", "Rename", "Cancel"))
                        {
                            lastRename = GrassSceneObjectRenamer.RenameOpenScene();
                            Rescan();
                        }
                    }
                }
            }

            if (!validScene)
                EditorGUILayout.HelpBox("No valid open scene - open one from the board above to migrate, revert, or rename its grass objects.", MessageType.Info);

            if (lastMigration != null)
                DrawMigrationResult();

            if (lastRename != null)
                EditorGUILayout.LabelField($"Renamed {lastRename.renamed}   Skipped {lastRename.skipped}", EditorStyles.miniLabel);
        }

        private void DrawMigrationResult()
        {
            if (!lastMigration.ok)
            {
                EditorGUILayout.HelpBox(lastMigration.error, MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField($"Renderers migrated: {lastMigration.renderersMigrated}   Decal: {lastMigration.decalOutcome}", EditorStyles.miniLabel);
        }

        private void DrawAssetsSection()
        {
            EditorGUILayout.LabelField("Assets", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(standardizePlan == null ? "Build Plan" : "Rebuild Plan", GUILayout.Width(110)))
                    BuildStandardizePlan();

                GUILayout.Label(standardizeSummaryText);
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(standardizeMovableCount == 0))
                {
                    if (GUILayout.Button("Apply", GUILayout.Width(90)))
                    {
                        if (EditorUtility.DisplayDialog("Standardize Assets", $"Move {standardizeMovableCount} grass items? Used assets are standardized into Assets/Grass/<Scene>/, unused ones are quarantined into Assets/Grass/_Unused/. GUIDs are preserved. Do this on a clean branch and coordinate with the team.", "Apply", "Cancel"))
                        {
                            lastApply = GrassAssetStandardizer.Apply(standardizePlan);
                            BuildStandardizePlan();
                            Rescan();
                        }
                    }
                }
            }

            if (lastApply != null)
                EditorGUILayout.LabelField($"Moved {lastApply.moved}   Skipped {lastApply.skipped}   Failed {lastApply.failed}", EditorStyles.miniLabel);
        }

        private void DrawToolsSection()
        {
            EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Grass Painter", GUILayout.Height(28)))
                    EditorApplication.ExecuteMenuItem(MenuPathPainter);
                if (GUILayout.Button("Grass Decal Baker", GUILayout.Height(28)))
                    EditorApplication.ExecuteMenuItem(MenuPathDecalBaker);
                if (GUILayout.Button("Console Converter", GUILayout.Height(28)))
                    EditorApplication.ExecuteMenuItem(MenuPathConverter);
                if (GUILayout.Button("Migration Dashboard", GUILayout.Height(28)))
                    EditorApplication.ExecuteMenuItem(MenuPathDashboard);
            }
        }

        private void ExecuteRowAction(SceneRow row)
        {
            switch (row.state)
            {
                case SceneGrassState.Authored:
                    if (OpenSceneIfNeeded(row.info.scenePath))
                        EditorApplication.ExecuteMenuItem(MenuPathPainter);
                    break;
                case SceneGrassState.NeedsDecalBake:
                    if (OpenSceneIfNeeded(row.info.scenePath))
                        EditorApplication.ExecuteMenuItem(MenuPathDecalBaker);
                    break;
                case SceneGrassState.NeedsConvert:
                    if (OpenSceneIfNeeded(row.info.scenePath))
                        EditorApplication.ExecuteMenuItem(MenuPathConverter);
                    break;
                case SceneGrassState.NeedsProfile:
                case SceneGrassState.Ready:
                    OpenSceneAndSelectConsoleRenderer(row.info.scenePath);
                    break;
            }

            RecomputeRows();
            Repaint();
        }

        private static bool OpenSceneIfNeeded(string scenePath)
        {
            Scene active = SceneManager.GetActiveScene();
            if (string.Equals(active.path, scenePath, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            return true;
        }

        private static void OpenSceneAndSelectConsoleRenderer(string scenePath)
        {
            if (!OpenSceneIfNeeded(scenePath))
                return;

            Scene scene = SceneManager.GetActiveScene();
            GrassRendererConsole[] all = UnityEngine.Object.FindObjectsByType<GrassRendererConsole>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].gameObject.scene == scene)
                {
                    Selection.activeGameObject = all[i].gameObject;
                    EditorGUIUtility.PingObject(all[i].gameObject);
                    return;
                }
            }
        }

        private void CreateProfileSet()
        {
            EnsureFolder(ProfileSetDefaultFolder);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{ProfileSetDefaultFolder}/GrassProfileSet.asset");

            var asset = ScriptableObject.CreateInstance<GrassPlatformProfileSet>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(asset);
            ResolveProfileSets();
            RecomputeRows();
            Repaint();
        }

        private static void EnsureFolder(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath)) return;

            string[] segments = assetFolderPath.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = $"{current}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }

        private void BuildStandardizePlan()
        {
            standardizePlan = GrassAssetStandardizer.BuildPlan();

            int ready = 0, ambiguous = 0, already = 0, deferred = 0, unused = 0;
            for (int i = 0; i < standardizePlan.Count; i++)
            {
                switch (standardizePlan[i].status)
                {
                    case GrassAssetStandardizer.StatusReady: ready++; break;
                    case GrassAssetStandardizer.StatusAmbiguous: ambiguous++; break;
                    case GrassAssetStandardizer.StatusAlreadyStandard: already++; break;
                    case GrassAssetStandardizer.StatusDeferred: deferred++; break;
                    case GrassAssetStandardizer.StatusUnused: unused++; break;
                }
            }

            standardizeMovableCount = ready + unused;
            standardizeSummaryText = $"Ready {ready} | Ambiguous {ambiguous} | Unused {unused} | AlreadyStandard {already} | Deferred {deferred}";
            Repaint();
        }

        private void Rescan()
        {
            List<SceneScanInfo> backup = new List<SceneScanInfo>(scanInfos);
            bool previousScanned = scanned;
            bool completed = false;

            try
            {
                ResolveProfileSets();

                HashSet<string> dataAssetPaths = CollectPaths("t:GrassDataAsset");
                HashSet<string> consoleDataPaths = CollectPaths("t:GrassDataConsoleAsset");
                HashSet<string> decalPaths = CollectPaths("t:GrassDecalBakeAsset");
                HashSet<string> settingsPaths = CollectPaths("t:SO_GrassSettings");

                string[] sceneGuids = FindSceneGuids();
                scanInfos.Clear();
                bool cancelled = false;

                for (int i = 0; i < sceneGuids.Length; i++)
                {
                    string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                    float progress = sceneGuids.Length == 0 ? 0f : (float)i / sceneGuids.Length;
                    if (EditorUtility.DisplayCancelableProgressBar("Grass Hub", $"Scanning {Path.GetFileNameWithoutExtension(scenePath)}", progress))
                    {
                        cancelled = true;
                        break;
                    }

                    HashSet<string> deps = new HashSet<string>(AssetDatabase.GetDependencies(scenePath, true), StringComparer.OrdinalIgnoreCase);

                    bool hasSettings = deps.Overlaps(settingsPaths);
                    bool hasOriginalData = deps.Overlaps(dataAssetPaths);
                    bool hasConsole = deps.Overlaps(consoleDataPaths);
                    bool hasDecal = deps.Overlaps(decalPaths);

                    if (!hasSettings && !hasOriginalData && !hasConsole && !hasDecal)
                        continue;

                    scanInfos.Add(new SceneScanInfo
                    {
                        scenePath = scenePath,
                        sceneName = Path.GetFileNameWithoutExtension(scenePath),
                        sceneAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(scenePath),
                        hasOriginalSettings = hasSettings,
                        hasOriginalDataAsset = hasOriginalData,
                        hasConsoleData = hasConsole,
                        hasDecal = hasDecal,
                        deps = deps,
                    });
                }

                if (cancelled)
                {
                    scanInfos.Clear();
                    scanInfos.AddRange(backup);
                    scanned = previousScanned;
                }
                else
                {
                    scanInfos.Sort((a, b) => string.Compare(a.sceneName, b.sceneName, StringComparison.OrdinalIgnoreCase));
                    scanned = true;
                }

                completed = true;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (!completed)
                {
                    scanInfos.Clear();
                    scanInfos.AddRange(backup);
                    scanned = previousScanned;
                }
            }

            RecomputeRows();
            Repaint();
        }

        private void ResolveProfileSets()
        {
            string[] guids = AssetDatabase.FindAssets("t:GrassPlatformProfileSet");
            foundProfileSets = new GrassPlatformProfileSet[guids.Length];
            foundProfileSetNames = new string[guids.Length];

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                foundProfileSets[i] = AssetDatabase.LoadAssetAtPath<GrassPlatformProfileSet>(path);
                foundProfileSetNames[i] = foundProfileSets[i] != null ? foundProfileSets[i].name : Path.GetFileNameWithoutExtension(path);
            }

            if (foundProfileSets.Length == 1)
            {
                selectedProfileSetIndex = 0;
                profileSet = foundProfileSets[0];
            }
            else if (foundProfileSets.Length > 1)
            {
                string savedPath = EditorPrefs.GetString(ProfileSetPrefsKey, string.Empty);
                int idx = -1;
                if (!string.IsNullOrEmpty(savedPath))
                {
                    for (int i = 0; i < foundProfileSets.Length; i++)
                    {
                        if (foundProfileSets[i] != null && string.Equals(AssetDatabase.GetAssetPath(foundProfileSets[i]), savedPath, StringComparison.OrdinalIgnoreCase))
                        {
                            idx = i;
                            break;
                        }
                    }
                }
                selectedProfileSetIndex = idx >= 0 ? idx : 0;
                profileSet = foundProfileSets[selectedProfileSetIndex];
            }
            else
            {
                selectedProfileSetIndex = -1;
                profileSet = null;
            }
        }

        private void RecomputeRows()
        {
            sceneRows.Clear();

            string activeScenePath = SceneManager.GetActiveScene().path;
            string profilePath = profileSet != null ? AssetDatabase.GetAssetPath(profileSet) : null;
            bool profileSetResolved = profileSet != null;
            scenesWithConsoleDataCount = 0;

            for (int i = 0; i < scanInfos.Count; i++)
            {
                SceneScanInfo info = scanInfos[i];
                bool hasProfileRef = profilePath != null && info.deps.Contains(profilePath);
                SceneGrassState state = DeriveState(info, profileSetResolved, hasProfileRef);

                if (state == SceneGrassState.NeedsProfile || state == SceneGrassState.Ready)
                    scenesWithConsoleDataCount++;

                bool isOpen = string.Equals(info.scenePath, activeScenePath, StringComparison.OrdinalIgnoreCase);

                sceneRows.Add(new SceneRow
                {
                    info = info,
                    state = state,
                    displayName = isOpen ? $"> {info.sceneName}" : info.sceneName,
                    stateLabel = StateLabel(state),
                    actionLabel = ActionLabel(state),
                    isOpenScene = isOpen,
                });
            }

            blastRadiusWarningText = $"Profiles are GLOBAL - editing them affects {scenesWithConsoleDataCount} scene(s).";
        }

        private static SceneGrassState DeriveState(SceneScanInfo info, bool profileSetResolved, bool hasProfileRef)
        {
            bool any = info.hasOriginalSettings || info.hasOriginalDataAsset || info.hasConsoleData || info.hasDecal;
            if (!any)
                return SceneGrassState.NoGrass;

            if (info.hasConsoleData)
            {
                if (info.hasDecal && (!profileSetResolved || hasProfileRef))
                    return SceneGrassState.Ready;
                if (profileSetResolved && !hasProfileRef)
                    return SceneGrassState.NeedsProfile;
                return SceneGrassState.NeedsDecalBake;
            }

            if (info.hasDecal)
                return SceneGrassState.NeedsConvert;

            if (info.hasOriginalDataAsset)
                return SceneGrassState.NeedsDecalBake;

            return SceneGrassState.Authored;
        }

        private static string StateLabel(SceneGrassState state)
        {
            switch (state)
            {
                case SceneGrassState.NoGrass: return "No grass";
                case SceneGrassState.Authored: return "Authored";
                case SceneGrassState.NeedsDecalBake: return "Needs decal bake";
                case SceneGrassState.NeedsConvert: return "Needs convert";
                case SceneGrassState.NeedsProfile: return "Needs profile";
                case SceneGrassState.Ready: return "Ready";
                default: return "?";
            }
        }

        private static string ActionLabel(SceneGrassState state)
        {
            switch (state)
            {
                case SceneGrassState.Authored: return "Open Painter";
                case SceneGrassState.NeedsDecalBake: return "Open Decal Baker";
                case SceneGrassState.NeedsConvert: return "Open Converter";
                case SceneGrassState.NeedsProfile: return "Open Scene";
                case SceneGrassState.Ready: return "Open Scene";
                default: return "-";
            }
        }

        private string[] FindSceneGuids()
        {
            if (onlyScenesInBuild)
                return FindBuildSceneGuids();

            if (!string.IsNullOrEmpty(sceneSearchFolder) && AssetDatabase.IsValidFolder(sceneSearchFolder))
                return AssetDatabase.FindAssets("t:Scene", new[] { sceneSearchFolder });

            return AssetDatabase.FindAssets("t:Scene");
        }

        private static string[] FindBuildSceneGuids()
        {
            var guids = new List<string>();

            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene == null || !scene.enabled || string.IsNullOrEmpty(scene.path))
                    continue;

                if (!File.Exists(scene.path))
                    continue;

                string guid = AssetDatabase.AssetPathToGUID(scene.path);
                if (!string.IsNullOrEmpty(guid) && !guids.Contains(guid))
                    guids.Add(guid);
            }

            return guids.ToArray();
        }

        private static HashSet<string> CollectPaths(string filter)
        {
            string[] guids = AssetDatabase.FindAssets(filter);
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < guids.Length; i++)
                set.Add(AssetDatabase.GUIDToAssetPath(guids[i]));
            return set;
        }
    }
}
