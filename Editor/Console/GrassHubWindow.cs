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

        private const float SceneColumnMinWidth = 160f;
        private const float StateColumnWidth = 130f;
        private const float ActionColumnWidth = 170f;
        private const float OpenColumnWidth = 56f;
        private const float AssetNameColumnWidth = 210f;

        private const float BoardRowHeight = 20f;
        private const float BoardMinHeight = 90f;
        private const float BoardMaxHeight = 240f;
        private const float BoardScrollbarWidth = 13f;

        private const float PrimaryButtonHeight = 26f;
        private const float SecondaryButtonHeight = 21f;

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
        private bool housekeepingExpanded;
        private string boardScopeNote = string.Empty;
        private Vector2 boardScrollPos;
        private Vector2 windowScrollPos;

        private GUIStyle titleStyle;
        private GUIStyle linkStyle;
        private GUIStyle linkBoldStyle;
        private GUIStyle linkGrayStyle;
        private GUIStyle stateReadyStyle;
        private GUIStyle stateWarnStyle;
        private GUIStyle stateNeutralStyle;
        private GUIStyle stateGrayStyle;
        private GUIStyle wrapLabelStyle;
        private GUIStyle microCaptionStyle;
        private GUIStyle groupTitleStyle;
        private Color openRowTint;

        private static readonly GUIContent BuildFilterContent = new GUIContent("Scenes in build", "Lists only the scenes enabled in Build Settings. Uncheck to list a folder instead. Either way, scenes without grass are left out.");
        private static readonly GUIContent OpenButtonContent = new GUIContent("Open", "Opens this scene, prompting to save the current one first.");
        private static readonly GUIContent MigrateButtonContent = new GUIContent("Migrate Open Scene", "Adds console renderers and creates assets for the currently open scene.");
        private static readonly GUIContent RevertButtonContent = new GUIContent("Revert Open Scene", "Reverts console migration in the currently open scene.");
        private static readonly GUIContent RenameButtonContent = new GUIContent("Rename Scene Objects", "Renames grass renderers, console objects, and GrassDecal objects in the open scene to the Grass_<Veg> convention. Undoable.");
        private static readonly GUIContent CreateProfilesContent = new GUIContent("Create Profiles For Open Scene", "Creates the Full and Switch profiles for the open scene under Assets/Grass/<Scene>/Profiles, fills both with the values this scene is using right now, and assigns the set to its renderers. Existing profiles are never overwritten.");
        private static readonly GUIContent SaveProfilesContent = new GUIContent("Save Assets", "Writes edited profiles to disk. Changing a profile in the Inspector only marks it dirty - Unity flushes it on File > Save Project, on quit, or here.");
        private static readonly GUIContent RefreshProfilesContent = new GUIContent("Refresh", "Re-reads the open scene and its profile assets, and updates that scene's row on the board.");
        private static readonly GUIContent StandardizeScopeContent = new GUIContent("Open scene only", "Limits the plan to the assets this scene owns - settings, data, decal maps and material. Uncheck to standardize the whole project in one go.");
        private static readonly GUIContent RebuildPlanContent = new GUIContent("Rebuild Plan", "Recomputes where each grass asset should live. The scene dependency read happens once per session - after that this is instant.");
        private static readonly GUIContent ScanContent = new GUIContent("Scan", "Reads every scene once to work out which assets belong to which scene. Everything else in this window reuses that read.");
        private static readonly GUIContent RescanContent = new GUIContent("Rescan", "Throws away this session's scene read and does it again. Only needed if scenes changed outside this window.");

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
        private const string HousekeepingPrefKey = "GrassHub.HousekeepingExpanded";
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
            housekeepingExpanded = EditorPrefs.GetBool(HousekeepingPrefKey, false);
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

            wrapLabelStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };

            microCaptionStyle = new GUIStyle(EditorStyles.miniBoldLabel);

            groupTitleStyle = new GUIStyle(EditorStyles.boldLabel);

            openRowTint = EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.06f)
                : new Color(0f, 0f, 0f, 0.06f);
        }

        private void OnGUI()
        {
            InitStyles();

            DrawToolbar();

            using (var scroll = new EditorGUILayout.ScrollViewScope(windowScrollPos))
            {
                windowScrollPos = scroll.scrollPosition;

                EditorGUILayout.Space(4);
                DrawScenesBlock();

                EditorGUILayout.Space(8);
                DrawOpenSceneBlock();

                EditorGUILayout.Space(8);
                DrawHousekeepingBlock();

                EditorGUILayout.Space(6);
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Grass Hub", titleStyle, GUILayout.Width(90));

                GUILayout.FlexibleSpace();

                EditorGUI.BeginChangeCheck();
                onlyScenesInBuild = GUILayout.Toggle(onlyScenesInBuild, BuildFilterContent, GUILayout.Width(112));
                if (EditorGUI.EndChangeCheck())
                {
                    EditorPrefs.SetBool(OnlyInBuildPrefKey, onlyScenesInBuild);
                    if (scanned) RefreshBoard();
                    GUIUtility.ExitGUI();
                }

                using (new EditorGUI.DisabledScope(onlyScenesInBuild))
                {
                    EditorGUI.BeginChangeCheck();
                    sceneSearchFolder = EditorGUILayout.TextField(sceneSearchFolder, EditorStyles.toolbarTextField, GUILayout.Width(170));
                    if (EditorGUI.EndChangeCheck())
                    {
                        EditorPrefs.SetString(SceneFolderPrefKey, sceneSearchFolder);
                        if (scanned) RefreshBoard();
                    }
                }

                if (GUILayout.Button(scanned ? RescanContent : ScanContent, EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    Rescan();
                    GUIUtility.ExitGUI();
                }
            }
        }

        private void DrawScenesBlock()
        {
            EditorGUILayout.LabelField("Scenes", groupTitleStyle);

            if (!scanned)
            {
                string source = onlyScenesInBuild
                    ? "the scenes enabled in Build Settings"
                    : $"the scenes under '{sceneSearchFolder}'";

                EditorGUILayout.HelpBox(
                    $"Not scanned yet. Press Scan to read {source}. Every scene is read once per session so this window can tell a scene-owned asset from a shared one - after that, everything here is instant. Scenes without grass never show up.",
                    MessageType.Info);
                return;
            }

            if (sceneRows.Count == 0)
            {
                EditorGUILayout.HelpBox("No scenes with grass data found. If you just added grass to a scene, save it and press Rescan.", MessageType.Info);
                return;
            }

            if (!string.IsNullOrEmpty(boardScopeNote))
            {
                if (onlyScenesInBuild)
                    EditorGUILayout.LabelField(boardScopeNote, wrapLabelStyle);
                else
                    EditorGUILayout.HelpBox(boardScopeNote, MessageType.Warning);
            }

            float contentHeight = sceneRows.Count * BoardRowHeight + 8f;
            bool needsScrollbar = contentHeight > BoardMaxHeight;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Scene", EditorStyles.boldLabel, GUILayout.ExpandWidth(true), GUILayout.MinWidth(SceneColumnMinWidth));
                GUILayout.Label(GUIContent.none, GUILayout.Width(OpenColumnWidth));
                GUILayout.Label("State", EditorStyles.boldLabel, GUILayout.Width(StateColumnWidth));
                GUILayout.Label("Next Action", EditorStyles.boldLabel, GUILayout.Width(ActionColumnWidth));
                if (needsScrollbar)
                    GUILayout.Space(BoardScrollbarWidth);
            }

            float boardHeight = Mathf.Clamp(contentHeight, BoardMinHeight, BoardMaxHeight);
            using (var scroll = new EditorGUILayout.ScrollViewScope(boardScrollPos, GUILayout.Height(boardHeight)))
            {
                boardScrollPos = scroll.scrollPosition;
                for (int i = 0; i < sceneRows.Count; i++)
                    DrawSceneRow(sceneRows[i]);
            }
        }

        private void DrawSceneRow(SceneRow row)
        {
            using (var scope = new EditorGUILayout.HorizontalScope())
            {
                if (row.isOpenScene && Event.current.type == EventType.Repaint)
                    EditorGUI.DrawRect(scope.rect, openRowTint);

                GUIStyle nameStyle = row.state == SceneGrassState.NoGrass ? linkGrayStyle : (row.isOpenScene ? linkBoldStyle : linkStyle);
                if (GUILayout.Button(row.displayName, nameStyle, GUILayout.ExpandWidth(true), GUILayout.MinWidth(SceneColumnMinWidth)))
                    EditorGUIUtility.PingObject(row.info.sceneAsset);

                using (new EditorGUI.DisabledScope(row.isOpenScene))
                {
                    if (GUILayout.Button(OpenButtonContent, GUILayout.Width(OpenColumnWidth)) &&
                        OpenSceneIfNeeded(row.info.scenePath))
                    {
                        RefreshBoard();
                        GUIUtility.ExitGUI();
                    }
                }

                GUILayout.Label(row.stateLabel, GetStateStyle(row.state), GUILayout.Width(StateColumnWidth));

                if (row.state == SceneGrassState.NoGrass)
                    GUILayout.Label("-", GUILayout.Width(ActionColumnWidth));
                else if (GUILayout.Button(row.actionLabel, GUILayout.Width(ActionColumnWidth)))
                    ExecuteRowAction(row);
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

        private void DrawOpenSceneBlock()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            bool validScene = activeScene.IsValid();
            SceneRow openRow = FindOpenSceneRow();

            using (new EditorGUILayout.HorizontalScope())
            {
                string header = validScene ? $"Open Scene   {activeScene.name}" : "Open Scene   (none)";
                EditorGUILayout.LabelField(header, groupTitleStyle, GUILayout.ExpandWidth(true));

                if (openRow != null)
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(openRow.stateLabel, GetStateStyle(openRow.state), GUILayout.Width(StateColumnWidth));
                }
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (!validScene)
                {
                    EditorGUILayout.HelpBox("No valid open scene. Open one from the board above to migrate, tune its profiles, or standardize its assets.", MessageType.Info);
                    return;
                }

                if (openRow == null && !OpenSceneHasGrass())
                {
                    EditorGUILayout.HelpBox(
                        $"'{activeScene.name}' has no grass yet, or it is not in the last scan. Paint grass with the Grass Painter, or press Rescan up top if you just changed this scene.",
                        MessageType.Info);
                    return;
                }

                DrawMigrateGroup(activeScene, openRow);
                EditorGUILayout.Space(6);
                DrawProfilesGroup(openRow);
                EditorGUILayout.Space(6);
                DrawStandardizeGroup(openRow);
            }
        }

        private void DrawMigrateGroup(Scene activeScene, SceneRow openRow)
        {
            EditorGUILayout.LabelField("Migrate", microCaptionStyle);

            bool migrateIsPrimary = openRow == null || openRow.state != SceneGrassState.Ready;
            float migrateHeight = migrateIsPrimary ? PrimaryButtonHeight : SecondaryButtonHeight;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(MigrateButtonContent, GUILayout.ExpandWidth(true), GUILayout.Height(migrateHeight)))
                {
                    if (EditorUtility.DisplayDialog("Migrate Scene", $"Migrate scene '{activeScene.name}'? This adds console renderers and creates assets.", "Migrate", "Cancel"))
                    {
                        lastMigration = GrassSceneMigrator.MigrateOpenScene();
                        RefreshBoard();
                        GUIUtility.ExitGUI();
                    }
                }

                if (GUILayout.Button(RevertButtonContent, GUILayout.ExpandWidth(true), GUILayout.Height(SecondaryButtonHeight)))
                {
                    if (EditorUtility.DisplayDialog("Revert Scene", $"Revert console migration in scene '{activeScene.name}'?", "Revert", "Cancel"))
                    {
                        GrassSceneMigrator.RevertOpenScene();
                        lastMigration = null;
                        RefreshBoard();
                        GUIUtility.ExitGUI();
                    }
                }

                if (GUILayout.Button(RenameButtonContent, GUILayout.ExpandWidth(true), GUILayout.Height(SecondaryButtonHeight)))
                {
                    if (EditorUtility.DisplayDialog("Rename Scene Objects", "Rename grass renderers, console objects, and GrassDecal objects in the open scene to the Grass_<Veg> convention? (Undoable)", "Rename", "Cancel"))
                    {
                        lastRename = GrassSceneObjectRenamer.RenameOpenScene();
                        RefreshBoard();
                        GUIUtility.ExitGUI();
                    }
                }
            }

            if (lastMigration != null)
                DrawMigrationResult();

            if (lastRename != null)
                EditorGUILayout.LabelField($"Renamed {lastRename.renamed}   Skipped {lastRename.skipped}", wrapLabelStyle);
        }

        private void DrawMigrationResult()
        {
            if (!lastMigration.ok)
            {
                EditorGUILayout.HelpBox(lastMigration.error, MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField($"Renderers migrated: {lastMigration.renderersMigrated}   Decal: {lastMigration.decalOutcome}", wrapLabelStyle);
        }

        private void DrawProfilesGroup(SceneRow openRow)
        {
            EditorGUILayout.LabelField("Profiles", microCaptionStyle);

            RefreshProfileStatusIfNeeded();
            GrassProfileSceneStatus status = profileSceneStatus;

            if (status.blocker != null)
            {
                EditorGUILayout.LabelField(status.blocker, wrapLabelStyle);
                return;
            }

            EditorGUILayout.LabelField(status.folder, wrapLabelStyle);

            float createHeight = status.CanCreate ? PrimaryButtonHeight : SecondaryButtonHeight;

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!status.CanCreate))
                {
                    if (GUILayout.Button(CreateProfilesContent, GUILayout.Width(220), GUILayout.Height(createHeight)))
                    {
                        CreateProfilesForOpenScene();
                        GUIUtility.ExitGUI();
                    }
                }

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
                    RefreshBoard();
                    GUIUtility.ExitGUI();
                }
            }

            GUILayout.Label(ProfileStatusText(status), status.CanCreate ? stateWarnStyle : stateReadyStyle);

            if (status.AssetsComplete)
            {
                DrawProfileAssetRow("Set", status.set);
                DrawProfileAssetRow("Full", status.full);
                DrawProfileAssetRow("Switch", status.switchProfile);
                EditorGUILayout.LabelField("Tune them from the renderer's inspector - it edits the profile in place.", wrapLabelStyle);
            }
            else
            {
                EditorGUILayout.LabelField($"Creates {Path.GetFileNameWithoutExtension(status.setPath)} plus its Full and Switch pair, both filled with what this scene renders right now.", wrapLabelStyle);
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
                if (GUILayout.Button(asset != null ? asset.name : "(missing)", asset != null ? linkStyle : linkGrayStyle, GUILayout.Width(AssetNameColumnWidth)))
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
                wrapLabelStyle);

            if (lastProfileApply.renderersAssigned > 0)
                EditorGUILayout.HelpBox("Save the scene to keep the assignment. The board above only picks it up once the scene is saved.", MessageType.Info);

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
            RefreshBoard();
        }

        private void DrawStandardizeGroup(SceneRow openRow)
        {
            EditorGUILayout.LabelField("Standardize", microCaptionStyle);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(RebuildPlanContent, GUILayout.Width(110), GUILayout.Height(SecondaryButtonHeight)))
                {
                    BuildStandardizePlan();
                    GUIUtility.ExitGUI();
                }

                EditorGUI.BeginChangeCheck();
                standardizeOpenSceneOnly = GUILayout.Toggle(standardizeOpenSceneOnly, StandardizeScopeContent, GUILayout.Width(112));
                if (EditorGUI.EndChangeCheck())
                {
                    EditorPrefs.SetBool(StandardizeScopePrefKey, standardizeOpenSceneOnly);
                    if (standardizePlan != null)
                    {
                        BuildStandardizePlan();
                        GUIUtility.ExitGUI();
                    }
                }

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(standardizeMovableCount == 0))
                {
                    float applyHeight = standardizeMovableCount > 0 ? PrimaryButtonHeight : SecondaryButtonHeight;
                    if (GUILayout.Button("Apply", GUILayout.Width(90), GUILayout.Height(applyHeight)))
                    {
                        ApplyStandardizePlan();
                        GUIUtility.ExitGUI();
                    }
                }
            }

            if (standardizePlan == null)
            {
                EditorGUILayout.LabelField("Press Rebuild Plan to see what would move.", wrapLabelStyle);
                return;
            }

            EditorGUILayout.LabelField(standardizeSummaryText, wrapLabelStyle);

            if (!string.IsNullOrEmpty(standardizeScopeNote))
                EditorGUILayout.LabelField(standardizeScopeNote, wrapLabelStyle);

            if (lastApply != null)
                EditorGUILayout.LabelField($"Moved {lastApply.moved}   Skipped {lastApply.skipped}   Failed {lastApply.failed}", wrapLabelStyle);
        }

        private void ApplyStandardizePlan()
        {
            string scope = standardizeOpenSceneOnly
                ? $"the {standardizeMovableCount} item(s) owned by the open scene"
                : $"{standardizeMovableCount} grass item(s) across the whole project";

            if (!EditorUtility.DisplayDialog(
                    "Standardize Assets",
                    $"Move {scope}? Used assets are filed into Assets/Grass/<Scene>/<Category>/, unused ones are quarantined into Assets/Grass/_Unused/. GUIDs are preserved, so every reference survives the move. Do this on a clean branch and coordinate with the team.",
                    "Apply", "Cancel"))
                return;

            lastApply = GrassAssetStandardizer.Apply(standardizePlan);
            BuildStandardizePlan();
            RefreshBoard();
        }

        private void DrawHousekeepingBlock()
        {
            EditorGUI.BeginChangeCheck();
            housekeepingExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(housekeepingExpanded, "Housekeeping");
            if (EditorGUI.EndChangeCheck())
                EditorPrefs.SetBool(HousekeepingPrefKey, housekeepingExpanded);

            if (housekeepingExpanded)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawDecalBudget();
                    EditorGUILayout.Space(6);
                    DrawToolsGroup();
                }
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawDecalBudget()
        {
            if (decalBudget == null)
                ScanDecalBudget();

            EditorGUILayout.LabelField("Decal Texture Budget", microCaptionStyle);
            EditorGUILayout.LabelField("Whole project - not scoped to the open scene.", wrapLabelStyle);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Decal textures", GUILayout.Width(90));

                EditorGUI.BeginChangeCheck();
                decalSwitchMax = EditorGUILayout.IntPopup(decalSwitchMax, DecalSizeNames, DecalSizeValues, GUILayout.Width(70));
                if (EditorGUI.EndChangeCheck())
                {
                    EditorPrefs.SetInt(DecalSwitchMaxPrefKey, decalSwitchMax);
                    ScanDecalBudget();
                }

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(decalBudget.ChangeCount == 0))
                {
                    if (GUILayout.Button("Apply", GUILayout.Width(90), GUILayout.Height(SecondaryButtonHeight)))
                    {
                        ApplyDecalBudget();
                        GUIUtility.ExitGUI();
                    }
                }
            }

            if (decalBudget.entries.Count == 0)
            {
                EditorGUILayout.LabelField("No baked decal maps found in the project yet.", wrapLabelStyle);
                return;
            }

            GUILayout.Label($"{decalBudget.megabytesNow:0.#} MB -> {decalBudget.megabytesAfter:0.#} MB on Switch",
                decalBudget.ChangeCount > 0 ? stateWarnStyle : stateReadyStyle);

            EditorGUILayout.LabelField(
                decalBudget.ChangeCount == 0
                    ? $"{decalBudget.entries.Count} map(s), all already capped at {decalSwitchMax} for Switch."
                    : $"{decalBudget.entries.Count} map(s), {decalBudget.missingOverride} with no Switch override. Only the importer is touched - no re-bake, no pixel change, PC build untouched.",
                wrapLabelStyle);

            decalBudgetFoldout = EditorGUILayout.Foldout(decalBudgetFoldout, $"Maps ({decalBudget.ChangeCount} to change)", true);
            if (!decalBudgetFoldout)
                return;

            using (var scroll = new EditorGUILayout.ScrollViewScope(decalBudgetScrollPos, GUILayout.Height(Mathf.Min(150f, 20f + decalBudget.entries.Count * 18f))))
            {
                decalBudgetScrollPos = scroll.scrollPosition;
                for (int i = 0; i < decalBudget.entries.Count; i++)
                    DrawDecalBudgetRow(decalBudget.entries[i]);
            }
        }

        private void DrawDecalBudgetRow(DecalTextureEntry entry)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(Path.GetFileNameWithoutExtension(entry.texturePath), linkStyle, GUILayout.ExpandWidth(true), GUILayout.MinWidth(SceneColumnMinWidth)))
                    EditorGUIUtility.PingObject(entry.texture);

                GUILayout.Label($"{entry.switchSizeNow} -> {entry.switchSizeAfter}", entry.Changes ? stateWarnStyle : stateGrayStyle, GUILayout.Width(100));
                GUILayout.Label($"{entry.MegabytesNow:0.##} -> {entry.MegabytesAfter:0.##} MB", stateGrayStyle, GUILayout.Width(140));
                GUILayout.Label(entry.hasOverride ? string.Empty : "no override", stateGrayStyle, GUILayout.Width(80));
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

        private void DrawToolsGroup()
        {
            EditorGUILayout.LabelField("More Tools", microCaptionStyle);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Grass Painter", GUILayout.ExpandWidth(true), GUILayout.Height(24)))
                    EditorApplication.ExecuteMenuItem(MenuPathPainter);
                if (GUILayout.Button("Grass Decal Baker", GUILayout.ExpandWidth(true), GUILayout.Height(24)))
                    EditorApplication.ExecuteMenuItem(MenuPathDecalBaker);
                if (GUILayout.Button("Console Converter", GUILayout.ExpandWidth(true), GUILayout.Height(24)))
                    EditorApplication.ExecuteMenuItem(MenuPathConverter);
                if (GUILayout.Button("Migration Dashboard", GUILayout.ExpandWidth(true), GUILayout.Height(24)))
                    EditorApplication.ExecuteMenuItem(MenuPathDashboard);
                if (GUILayout.Button("Device Tuning", GUILayout.ExpandWidth(true), GUILayout.Height(24)))
                    GrassDeviceTuningWindow.Open();
            }
        }

        private SceneRow FindOpenSceneRow()
        {
            for (int i = 0; i < sceneRows.Count; i++)
                if (sceneRows[i].isOpenScene)
                    return sceneRows[i];
            return null;
        }

        private static bool OpenSceneHasGrass()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return false;

            GrassRendererConsole[] console = UnityEngine.Object.FindObjectsByType<GrassRendererConsole>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < console.Length; i++)
                if (console[i] != null && console[i].gameObject.scene == scene)
                    return true;

            GrassRenderer[] original = UnityEngine.Object.FindObjectsByType<GrassRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < original.Length; i++)
                if (original[i] != null && original[i].gameObject.scene == scene)
                    return true;

            return false;
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

            RefreshBoard();
            GUIUtility.ExitGUI();
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
            int unusedHidden = 0;
            int sharedHidden = 0;

            for (int i = 0; i < plan.Count; i++)
            {
                StandardizePlanEntry entry = plan[i];
                if (string.Equals(entry.scene, canonical, StringComparison.OrdinalIgnoreCase))
                {
                    filtered.Add(entry);
                    continue;
                }

                if (entry.status == GrassAssetStandardizer.StatusUnused) unusedHidden++;
                else if (string.IsNullOrEmpty(entry.scene)) sharedHidden++;
            }

            standardizeScopeNote = $"Only the assets owned by {canonical}. Hidden: {unusedHidden} unused, {sharedHidden} shared or unattributed - uncheck to see them.";

            if (unusedHidden > 0)
                standardizeScopeNote += " An asset shows up as unused when no SAVED scene references it. If you just migrated, save the scene and press Rescan before trusting this.";

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
            List<StandardizePlanEntry> plan = GrassAssetStandardizer.BuildPlan();

            if (plan == null)
            {
                standardizeScopeNote = "Scene read cancelled - the plan below is from before. Press Rebuild Plan to try again.";
                Repaint();
                return;
            }

            standardizePlan = FilterStandardizePlan(plan);

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
            GrassSceneDependencyIndex.Invalidate();
            RefreshBoard();
        }

        private void RefreshBoard()
        {
            ResolveProfileSets();

            if (!GrassSceneDependencyIndex.TryGet(out List<GrassSceneDependencies> scenes))
            {
                Repaint();
                return;
            }

            HashSet<string> dataAssetPaths = CollectPaths("t:GrassDataAsset");
            HashSet<string> consoleDataPaths = CollectPaths("t:GrassDataConsoleAsset");
            HashSet<string> decalPaths = CollectPaths("t:GrassDecalBakeAsset");
            HashSet<string> settingsPaths = CollectPaths("t:SO_GrassSettings");
            HashSet<string> visibleScenePaths = CollectVisibleScenePaths();

            scanInfos.Clear();

            for (int i = 0; i < scenes.Count; i++)
            {
                GrassSceneDependencies scene = scenes[i];
                if (!visibleScenePaths.Contains(scene.scenePath))
                    continue;

                bool hasSettings = scene.deps.Overlaps(settingsPaths);
                bool hasOriginalData = scene.deps.Overlaps(dataAssetPaths);
                bool hasConsole = scene.deps.Overlaps(consoleDataPaths);
                bool hasDecal = scene.deps.Overlaps(decalPaths);

                if (!hasSettings && !hasOriginalData && !hasConsole && !hasDecal)
                    continue;

                scanInfos.Add(new SceneScanInfo
                {
                    scenePath = scene.scenePath,
                    sceneName = scene.sceneName,
                    sceneAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(scene.scenePath),
                    hasOriginalSettings = hasSettings,
                    hasOriginalDataAsset = hasOriginalData,
                    hasConsoleData = hasConsole,
                    hasDecal = hasDecal,
                    deps = scene.deps,
                });
            }

            scanInfos.Sort((a, b) => string.Compare(a.sceneName, b.sceneName, StringComparison.OrdinalIgnoreCase));
            scanned = true;

            boardScopeNote = onlyScenesInBuild
                ? $"{scanInfos.Count} of the {visibleScenePaths.Count} scene(s) enabled in Build Settings have grass."
                : $"BUILD FILTER OFF - showing every scene under '{sceneSearchFolder}' ({scanInfos.Count} of {visibleScenePaths.Count} have grass). Tick 'Scenes in build' to hide staging and test scenes.";

            RecomputeRows();
            profileSceneStatus = null;
            ScanDecalBudget();
            Repaint();
        }

        private HashSet<string> CollectVisibleScenePaths()
        {
            var visible = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (onlyScenesInBuild)
            {
                foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                {
                    if (scene == null || !scene.enabled || string.IsNullOrEmpty(scene.path)) continue;
                    if (!File.Exists(scene.path)) continue;
                    visible.Add(scene.path);
                }

                return visible;
            }

            string[] guids = !string.IsNullOrEmpty(sceneSearchFolder) && AssetDatabase.IsValidFolder(sceneSearchFolder)
                ? AssetDatabase.FindAssets("t:Scene", new[] { sceneSearchFolder })
                : AssetDatabase.FindAssets("t:Scene");

            for (int i = 0; i < guids.Length; i++)
                visible.Add(AssetDatabase.GUIDToAssetPath(guids[i]));

            return visible;
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
                    displayName = info.sceneName,
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
