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

        private const float SceneColumnWidth = 250f;
        private const float StateColumnWidth = 130f;
        private const float ActionColumnWidth = 170f;
        private const float OpenColumnWidth = 56f;

        private readonly List<SceneScanInfo> scanInfos = new List<SceneScanInfo>();
        private readonly List<SceneRow> sceneRows = new List<SceneRow>();

        private readonly HashSet<string> profileSetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private GrassProfileSceneStatus profileSceneStatus;
        private GrassProfileApplyResult lastProfileApply;

        private List<StandardizePlanEntry> standardizePlan;
        private StandardizeApplyResult lastApply;
        private int standardizeMovableCount;
        private string standardizeSummaryText = string.Empty;
        private string standardizeScopeNote = string.Empty;
        private string standardizeCacheNote = string.Empty;
        private bool standardizeOpenSceneOnly = true;

        private DecalTextureBudget decalBudget;
        private int decalSwitchMax = 512;
        private bool decalBudgetFoldout;
        private Vector2 decalBudgetScrollPos;

        private static readonly string[] DecalSizeNames = { "256", "512", "1024", "2048", "4096" };
        private static readonly int[] DecalSizeValues = { 256, 512, 1024, 2048, 4096 };

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
        private static readonly GUIContent CreateProfilesContent = new GUIContent("Create Profiles For Open Scene", "Creates the Full and Switch profiles for the open scene under Assets/Grass/<Scene>/Profiles, fills both with the values this scene is using right now, and assigns the set to its renderers. Existing profiles are never overwritten.");
        private static readonly GUIContent SaveProfilesContent = new GUIContent("Save Assets", "Writes edited profiles to disk. Changing a profile in the Inspector only marks it dirty - Unity flushes it on File > Save Project, on quit, or here.");
        private static readonly GUIContent RefreshProfilesContent = new GUIContent("Refresh", "Re-reads the open scene and its profile assets, and updates that scene's row on the board.");
        private static readonly GUIContent StandardizeScopeContent = new GUIContent("Open scene only", "Limits the plan to the assets this scene owns - settings, data, decal maps and material. Uncheck to standardize the whole project in one go.");

        [MenuItem("Tools/Grass System/Grass Hub", priority = 0)]
        private static void Open()
        {
            GrassHubWindow window = GetWindow<GrassHubWindow>("Grass Hub");
            window.minSize = new Vector2(820, 620);
        }

        private const string SceneFolderPrefKey = "GrassHub.SceneSearchFolder";
        private const string OnlyInBuildPrefKey = "GrassHub.OnlyScenesInBuild";
        private const string StandardizeScopePrefKey = "GrassHub.StandardizeOpenSceneOnly";
        private const string DecalSwitchMaxPrefKey = "GrassHub.DecalSwitchMax";
        private const string DefaultSceneFolder = "Assets/Scenes";
        private string sceneSearchFolder = DefaultSceneFolder;
        private bool onlyScenesInBuild = true;

        private void OnEnable()
        {
            minSize = new Vector2(820, 620);
            sceneSearchFolder = EditorPrefs.GetString(SceneFolderPrefKey, DefaultSceneFolder);
            onlyScenesInBuild = EditorPrefs.GetBool(OnlyInBuildPrefKey, true);
            standardizeOpenSceneOnly = EditorPrefs.GetBool(StandardizeScopePrefKey, true);
            decalSwitchMax = EditorPrefs.GetInt(DecalSwitchMaxPrefKey, 512);
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
            DrawProfilesSection();
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

            string diff = snapshot.DescribeDiff(tuningTarget);
            string untouched = snapshot.DescribeUntouched();

            string confirm = $"Apply this dump to {tuningTarget.name}?\n\n{diff}";
            if (!string.IsNullOrEmpty(untouched))
                confirm += $"\n\n{untouched}";

            if (!EditorUtility.DisplayDialog("Apply Grass Tuning", confirm, "Apply", "Cancel"))
                return;

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

        private void DrawProfilesSection()
        {
            EditorGUILayout.LabelField("Profiles", EditorStyles.boldLabel);

            RefreshProfileStatusIfNeeded();
            GrassProfileSceneStatus status = profileSceneStatus;

            if (status.blocker != null)
            {
                EditorGUILayout.HelpBox(status.blocker, MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Folder", status.folder, EditorStyles.miniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!status.CanCreate))
                {
                    if (GUILayout.Button(CreateProfilesContent, GUILayout.Width(200), GUILayout.Height(22)))
                        CreateProfilesForOpenScene();
                }

                GUILayout.Label(ProfileStatusText(status), status.CanCreate ? stateWarnStyle : stateReadyStyle);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(SaveProfilesContent, GUILayout.Width(90)))
                {
                    AssetDatabase.SaveAssets();
                    profileSceneStatus = null;
                    Repaint();
                }

                if (GUILayout.Button(RefreshProfilesContent, GUILayout.Width(70)))
                {
                    profileSceneStatus = null;
                    RecomputeRows();
                    Repaint();
                }
            }

            if (status.AssetsComplete)
            {
                DrawProfileAssetRow("Set", status.set);
                DrawProfileAssetRow("Full", status.full);
                DrawProfileAssetRow("Switch", status.switchProfile);
                EditorGUILayout.LabelField("Tune them from the renderer's inspector - it edits the profile in place.", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField($"Creates {Path.GetFileNameWithoutExtension(status.setPath)} plus its Full and Switch pair, both filled with what this scene renders right now.", EditorStyles.miniLabel);
            }

            if (status.renderersOnOtherSet > 0)
                EditorGUILayout.HelpBox($"{status.renderersOnOtherSet} renderer(s) here point at a different profile set. An existing assignment is never overwritten - clear the field on the renderer to repoint it.", MessageType.Warning);

            if (lastProfileApply != null)
                DrawProfileApplyResult();
        }

        private void DrawProfileAssetRow(string label, UnityEngine.Object asset)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(label, GUILayout.Width(56));
                if (GUILayout.Button(asset != null ? asset.name : "(missing)", asset != null ? linkStyle : linkGrayStyle, GUILayout.Width(SceneColumnWidth)))
                    EditorGUIUtility.PingObject(asset);
                GUILayout.FlexibleSpace();
            }
        }

        private static string ProfileStatusText(GrassProfileSceneStatus status)
        {
            if (!status.AssetsComplete) return "Not created yet";
            if (status.SetWiringBroken) return "Set is not wired to both profiles";
            if (status.renderersUnassigned > 0) return $"{status.renderersUnassigned} renderer(s) without a set";
            return "Ready";
        }

        private void DrawProfileApplyResult()
        {
            EditorGUILayout.LabelField(
                $"Profiles {lastProfileApply.profilesCreated}   Renderers {lastProfileApply.renderersAssigned}   Set {(lastProfileApply.setCreated ? "created" : lastProfileApply.setRepaired ? "rewired" : "kept")}",
                EditorStyles.miniLabel);

            if (lastProfileApply.renderersAssigned > 0)
                EditorGUILayout.HelpBox("Save the scene to keep the assignment. The board above only picks it up on the next Scan after saving.", MessageType.Info);

            if (lastProfileApply.problems.Count > 0)
                EditorGUILayout.HelpBox(string.Join("\n", lastProfileApply.problems), MessageType.Warning);
        }

        private void RefreshProfileStatusIfNeeded()
        {
            string activePath = SceneManager.GetActiveScene().path;
            if (profileSceneStatus != null && string.Equals(profileSceneStatus.scenePath, activePath, StringComparison.OrdinalIgnoreCase))
                return;

            profileSceneStatus = GrassProfileFactory.InspectOpenScene();
            lastProfileApply = null;
        }

        private void CreateProfilesForOpenScene()
        {
            lastProfileApply = GrassProfileFactory.CreateForOpenScene();
            profileSceneStatus = GrassProfileFactory.InspectOpenScene();
            ResolveProfileSets();
            RecomputeRows();
            Repaint();
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

                EditorGUI.BeginChangeCheck();
                standardizeOpenSceneOnly = GUILayout.Toggle(standardizeOpenSceneOnly, StandardizeScopeContent, GUILayout.Width(112));
                if (EditorGUI.EndChangeCheck())
                {
                    EditorPrefs.SetBool(StandardizeScopePrefKey, standardizeOpenSceneOnly);
                    if (standardizePlan != null)
                        BuildStandardizePlan();
                }

                GUILayout.Label(standardizeSummaryText);
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(standardizeMovableCount == 0))
                {
                    if (GUILayout.Button("Apply", GUILayout.Width(90)))
                    {
                        string scope = standardizeOpenSceneOnly
                            ? $"the {standardizeMovableCount} item(s) owned by the open scene"
                            : $"{standardizeMovableCount} grass item(s) across the whole project";

                        if (EditorUtility.DisplayDialog("Standardize Assets", $"Move {scope}? Used assets are filed into Assets/Grass/<Scene>/<Category>/, unused ones are quarantined into Assets/Grass/_Unused/. GUIDs are preserved, so every reference survives the move. Do this on a clean branch and coordinate with the team.", "Apply", "Cancel"))
                        {
                            lastApply = GrassAssetStandardizer.Apply(standardizePlan);
                            BuildStandardizePlan();
                            Rescan();
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(standardizeScopeNote))
                EditorGUILayout.LabelField(standardizeScopeNote, EditorStyles.miniLabel);

            if (!string.IsNullOrEmpty(standardizeCacheNote))
                EditorGUILayout.LabelField(standardizeCacheNote, EditorStyles.miniLabel);

            if (lastApply != null)
                EditorGUILayout.LabelField($"Moved {lastApply.moved}   Skipped {lastApply.skipped}   Failed {lastApply.failed}", EditorStyles.miniLabel);

            EditorGUILayout.Space(4);
            DrawDecalBudget();
        }

        private void DrawDecalBudget()
        {
            if (decalBudget == null)
                ScanDecalBudget();

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Decal textures", GUILayout.Width(110));

                EditorGUI.BeginChangeCheck();
                decalSwitchMax = EditorGUILayout.IntPopup(decalSwitchMax, DecalSizeNames, DecalSizeValues, GUILayout.Width(70));
                if (EditorGUI.EndChangeCheck())
                {
                    EditorPrefs.SetInt(DecalSwitchMaxPrefKey, decalSwitchMax);
                    ScanDecalBudget();
                }

                GUILayout.Label($"{decalBudget.megabytesNow:0.#} MB -> {decalBudget.megabytesAfter:0.#} MB on Switch",
                    decalBudget.ChangeCount > 0 ? stateWarnStyle : stateReadyStyle);

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(decalBudget.ChangeCount == 0))
                {
                    if (GUILayout.Button("Apply", GUILayout.Width(90)))
                        ApplyDecalBudget();
                }
            }

            EditorGUILayout.LabelField(
                decalBudget.ChangeCount == 0
                    ? $"{decalBudget.entries.Count} map(s), all already capped at {decalSwitchMax} for Switch."
                    : $"{decalBudget.entries.Count} map(s), {decalBudget.missingOverride} with no Switch override. Only the importer is touched - no re-bake, no pixel change, PC build untouched.",
                EditorStyles.miniLabel);

            if (decalBudget.entries.Count == 0)
                return;

            decalBudgetFoldout = EditorGUILayout.Foldout(decalBudgetFoldout, $"Maps ({decalBudget.ChangeCount} to change)", true);
            if (!decalBudgetFoldout)
                return;

            decalBudgetScrollPos = EditorGUILayout.BeginScrollView(decalBudgetScrollPos, GUILayout.Height(Mathf.Min(150f, 20f + decalBudget.entries.Count * 18f)));
            for (int i = 0; i < decalBudget.entries.Count; i++)
                DrawDecalBudgetRow(decalBudget.entries[i]);
            EditorGUILayout.EndScrollView();
        }

        private void DrawDecalBudgetRow(DecalTextureEntry entry)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(Path.GetFileNameWithoutExtension(entry.texturePath), linkStyle, GUILayout.Width(SceneColumnWidth)))
                    EditorGUIUtility.PingObject(entry.texture);

                GUILayout.Label($"{entry.switchSizeNow} -> {entry.switchSizeAfter}", entry.Changes ? stateWarnStyle : stateGrayStyle, GUILayout.Width(100));
                GUILayout.Label($"{entry.MegabytesNow:0.##} -> {entry.MegabytesAfter:0.##} MB", stateGrayStyle, GUILayout.Width(140));
                GUILayout.Label(entry.hasOverride ? string.Empty : "no override", stateGrayStyle);
                GUILayout.FlexibleSpace();
            }
        }

        private void ScanDecalBudget()
        {
            decalBudget = GrassDecalTextureBudget.Scan(decalSwitchMax);
            Repaint();
        }

        private void ApplyDecalBudget()
        {
            string message =
                $"Cap {decalBudget.ChangeCount} baked decal map(s) at {decalSwitchMax} for the Switch build?\n\n" +
                $"{decalBudget.megabytesNow:0.#} MB -> {decalBudget.megabytesAfter:0.#} MB (estimate at 8bpp).\n\n" +
                "Only the importer is written. The PNGs are not re-baked, the Editor and the PC build keep the full size, and clearing the override undoes it.\n\n" +
                "The decals do get softer on Switch - check it on the devkit before shipping.";

            if (!EditorUtility.DisplayDialog("Decal Texture Budget", message, "Apply", "Cancel"))
                return;

            int applied = GrassDecalTextureBudget.Apply(decalBudget, decalSwitchMax);
            Debug.Log($"Decal Texture Budget: capped {applied} map(s) at {decalSwitchMax} for Switch.");
            ScanDecalBudget();
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

        private List<StandardizePlanEntry> FilterStandardizePlan(List<StandardizePlanEntry> plan)
        {
            standardizeScopeNote = string.Empty;

            if (!standardizeOpenSceneOnly)
                return plan;

            string sceneName = SceneManager.GetActiveScene().name;
            string canonical = GrassAssetStandardizer.CanonicalSceneName(sceneName);

            if (string.IsNullOrEmpty(canonical))
            {
                standardizeScopeNote = $"'{sceneName}' is not in the scene naming map, so no asset can be filed under it. Uncheck to see the whole project.";
                return new List<StandardizePlanEntry>();
            }

            var filtered = new List<StandardizePlanEntry>();
            for (int i = 0; i < plan.Count; i++)
            {
                if (string.Equals(plan[i].scene, canonical, StringComparison.OrdinalIgnoreCase))
                    filtered.Add(plan[i]);
            }

            standardizeScopeNote = $"Only the assets owned by {canonical}. Shared and unused ones are hidden - uncheck to see them.";
            return filtered;
        }

        private static bool OpenSceneRenderersHaveProfileSet()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
                return false;

            GrassRendererConsole[] all = UnityEngine.Object.FindObjectsByType<GrassRendererConsole>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            bool any = false;

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || all[i].gameObject.scene != scene) continue;
                if (all[i].profileSet == null) return false;
                any = true;
            }

            return any;
        }

        private void BuildStandardizePlan()
        {
            bool reusedCache = GrassAssetStandardizer.HasSceneDependencyCache;
            standardizePlan = FilterStandardizePlan(GrassAssetStandardizer.BuildPlan());

            standardizeCacheNote = reusedCache
                ? "Reusing this session's scene dependency snapshot. Press Rescan up top if scenes or references changed since."
                : string.Empty;

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
                GrassAssetStandardizer.InvalidateSceneDependencyCache();

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
            profileSceneStatus = null;
            ScanDecalBudget();
            Repaint();
        }

        private void ResolveProfileSets()
        {
            string[] guids = AssetDatabase.FindAssets("t:GrassPlatformProfileSet");
            profileSetPaths.Clear();

            for (int i = 0; i < guids.Length; i++)
                profileSetPaths.Add(AssetDatabase.GUIDToAssetPath(guids[i]));
        }

        private void RecomputeRows()
        {
            sceneRows.Clear();

            string activeScenePath = SceneManager.GetActiveScene().path;
            bool anyProfileSetExists = profileSetPaths.Count > 0;
            bool openSceneHasProfile = OpenSceneRenderersHaveProfileSet();

            for (int i = 0; i < scanInfos.Count; i++)
            {
                SceneScanInfo info = scanInfos[i];
                bool isOpen = string.Equals(info.scenePath, activeScenePath, StringComparison.OrdinalIgnoreCase);

                bool hasProfileRef = isOpen
                    ? openSceneHasProfile
                    : anyProfileSetExists && info.deps.Overlaps(profileSetPaths);

                SceneGrassState state = DeriveState(info, anyProfileSetExists, hasProfileRef);

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
