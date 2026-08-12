// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GrassSystem;

namespace GrassSystem.Consoles.Editor
{
    public struct GrassProfileSceneRef
    {
        public string scenePath;
        public HashSet<string> deps;
    }

    public class GrassProfilePlanEntry
    {
        public string groupKey;
        public string folder;
        public string setPath;
        public string fullPath;
        public string switchPath;
        public readonly List<string> scenePaths = new List<string>();
        public bool setExists;
        public bool fullExists;
        public bool switchExists;
        public bool setWiringBroken;
        public int scenesToAssign;
        public int scenesWithOtherSet;
        public string status;
        public string note;

        public bool NeedsWork => status != GrassProfileFactory.StatusReady;
    }

    public class GrassProfileApplyResult
    {
        public int setsCreated;
        public int profilesCreated;
        public int setsRepaired;
        public int scenesAssigned;
        public int renderersAssigned;
        public int scenesLeftAlone;
        public bool cancelled;
        public readonly List<string> problems = new List<string>();
    }

    public static class GrassProfileFactory
    {
        public const string StatusReady = "Ready";
        public const string StatusCreate = "Create";
        public const string StatusRepair = "Repair";
        public const string StatusAssign = "Assign";

        private const string GrassRootFolder = "Assets/Grass";
        private const string ProfilesFolderSegment = "Profiles";
        private const string SetPrefix = "GrassProfileSet";
        private const string ProfilePrefix = "GrassProfile";

        public static List<GrassProfilePlanEntry> BuildPlan(IEnumerable<GrassProfileSceneRef> scenes, bool splitDayNight)
        {
            var byGroup = new Dictionary<string, GrassProfilePlanEntry>(StringComparer.OrdinalIgnoreCase);
            var ordered = new List<GrassProfilePlanEntry>();
            var pendingAssign = new Dictionary<string, List<GrassProfileSceneRef>>(StringComparer.OrdinalIgnoreCase);

            foreach (GrassProfileSceneRef scene in scenes)
            {
                if (string.IsNullOrEmpty(scene.scenePath)) continue;

                string sceneName = Path.GetFileNameWithoutExtension(scene.scenePath);
                ResolveGroup(sceneName, splitDayNight, out string groupKey, out string folderScene);

                if (!byGroup.TryGetValue(groupKey, out GrassProfilePlanEntry entry))
                {
                    string folder = $"{GrassRootFolder}/{folderScene}/{ProfilesFolderSegment}";
                    entry = new GrassProfilePlanEntry
                    {
                        groupKey = groupKey,
                        folder = folder,
                        setPath = $"{folder}/{SetPrefix}_{groupKey}.asset",
                        fullPath = $"{folder}/{ProfilePrefix}_{groupKey}_Full.asset",
                        switchPath = $"{folder}/{ProfilePrefix}_{groupKey}_Switch.asset",
                    };
                    byGroup.Add(groupKey, entry);
                    pendingAssign.Add(groupKey, new List<GrassProfileSceneRef>());
                    ordered.Add(entry);
                }

                entry.scenePaths.Add(scene.scenePath);
                pendingAssign[groupKey].Add(scene);
            }

            HashSet<string> allSetPaths = CollectProfileSetPaths();

            for (int i = 0; i < ordered.Count; i++)
            {
                GrassProfilePlanEntry entry = ordered[i];
                var set = AssetDatabase.LoadAssetAtPath<GrassPlatformProfileSet>(entry.setPath);
                var full = AssetDatabase.LoadAssetAtPath<GrassPlatformProfile>(entry.fullPath);
                var switchProfile = AssetDatabase.LoadAssetAtPath<GrassPlatformProfile>(entry.switchPath);

                entry.setExists = set != null;
                entry.fullExists = full != null;
                entry.switchExists = switchProfile != null;
                entry.setWiringBroken = set != null && (set.fullProfile != full || set.switchProfile != switchProfile);

                entry.scenesToAssign = 0;
                entry.scenesWithOtherSet = 0;
                foreach (GrassProfileSceneRef scene in pendingAssign[entry.groupKey])
                {
                    if (SceneReferences(scene, entry.setPath))
                        continue;

                    if (SceneReferencesAnyExcept(scene, allSetPaths, entry.setPath))
                        entry.scenesWithOtherSet++;
                    else
                        entry.scenesToAssign++;
                }

                string otherSetNote = entry.scenesWithOtherSet > 0
                    ? $", {entry.scenesWithOtherSet} kept on their current set"
                    : string.Empty;

                if (!entry.setExists || !entry.fullExists || !entry.switchExists)
                {
                    entry.status = StatusCreate;
                    entry.note = entry.scenesToAssign > 0
                        ? $"create + assign to {entry.scenesToAssign} scene(s){otherSetNote}"
                        : $"create assets{otherSetNote}";
                }
                else if (entry.setWiringBroken)
                {
                    entry.status = StatusRepair;
                    entry.note = "set does not point at both profiles";
                }
                else if (entry.scenesToAssign > 0)
                {
                    entry.status = StatusAssign;
                    entry.note = $"assign to {entry.scenesToAssign} scene(s){otherSetNote}";
                }
                else
                {
                    entry.status = StatusReady;
                    entry.note = entry.scenesWithOtherSet > 0
                        ? $"{entry.scenesWithOtherSet} scene(s) point at another set"
                        : null;
                }
            }

            ordered.Sort((a, b) => string.Compare(a.groupKey, b.groupKey, StringComparison.OrdinalIgnoreCase));
            return ordered;
        }

        public static GrassProfileApplyResult Apply(List<GrassProfilePlanEntry> entries)
        {
            var result = new GrassProfileApplyResult();
            if (entries == null || entries.Count == 0)
                return result;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                result.cancelled = true;
                return result;
            }

            string originalScenePath = SceneManager.GetActiveScene().path;
            var needsSeed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    GrassProfilePlanEntry entry = entries[i];
                    if (!entry.NeedsWork) continue;

                    EnsureFolder(entry.folder);

                    GrassPlatformProfile full = LoadOrCreateProfile(entry.fullPath, GrassPlatformClass.Full, result, needsSeed);
                    GrassPlatformProfile switchProfile = LoadOrCreateProfile(entry.switchPath, GrassPlatformClass.Switch, result, needsSeed);

                    var set = AssetDatabase.LoadAssetAtPath<GrassPlatformProfileSet>(entry.setPath);
                    if (set == null)
                    {
                        set = ScriptableObject.CreateInstance<GrassPlatformProfileSet>();
                        set.fullProfile = full;
                        set.switchProfile = switchProfile;
                        AssetDatabase.CreateAsset(set, entry.setPath);
                        result.setsCreated++;
                    }
                    else if (set.fullProfile != full || set.switchProfile != switchProfile)
                    {
                        set.fullProfile = full;
                        set.switchProfile = switchProfile;
                        EditorUtility.SetDirty(set);
                        result.setsRepaired++;
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                HashSet<string> allSetPaths = CollectProfileSetPaths();

                for (int i = 0; i < entries.Count; i++)
                {
                    GrassProfilePlanEntry entry = entries[i];
                    if (!entry.NeedsWork) continue;

                    var set = AssetDatabase.LoadAssetAtPath<GrassPlatformProfileSet>(entry.setPath);
                    if (set == null)
                    {
                        result.problems.Add($"{entry.groupKey}: profile set could not be created at {entry.setPath}");
                        continue;
                    }

                    bool seedGroup = needsSeed.Contains(entry.fullPath) || needsSeed.Contains(entry.switchPath);

                    for (int s = 0; s < entry.scenePaths.Count; s++)
                    {
                        string scenePath = entry.scenePaths[s];
                        InspectSceneSets(scenePath, allSetPaths, entry.setPath, out bool alreadyAssigned, out bool referencesOther);

                        if (!alreadyAssigned && referencesOther)
                        {
                            result.scenesLeftAlone++;
                            result.problems.Add($"{Path.GetFileNameWithoutExtension(scenePath)}: already points at another profile set, left untouched");
                            continue;
                        }

                        bool assignNeeded = !alreadyAssigned;
                        if (!assignNeeded && !seedGroup) continue;

                        float progress = entries.Count == 0 ? 0f : (float)i / entries.Count;
                        if (EditorUtility.DisplayCancelableProgressBar("Grass Profiles", $"Assigning {Path.GetFileNameWithoutExtension(scenePath)}", progress))
                        {
                            result.cancelled = true;
                            return result;
                        }

                        if (!OpenScene(scenePath))
                        {
                            result.problems.Add($"{Path.GetFileNameWithoutExtension(scenePath)}: could not be opened");
                            continue;
                        }

                        Scene scene = SceneManager.GetActiveScene();
                        List<GrassRendererConsole> renderers = FindRenderersInScene(scene);
                        if (renderers.Count == 0)
                        {
                            result.problems.Add($"{scene.name}: no GrassRendererConsole found, nothing to assign");
                            continue;
                        }

                        if (seedGroup)
                        {
                            SeedFromRenderer(AssetDatabase.LoadAssetAtPath<GrassPlatformProfile>(entry.fullPath), renderers[0]);
                            SeedFromRenderer(AssetDatabase.LoadAssetAtPath<GrassPlatformProfile>(entry.switchPath), renderers[0]);
                            seedGroup = false;
                        }

                        bool sceneDirty = false;
                        for (int r = 0; r < renderers.Count; r++)
                        {
                            if (renderers[r].profileSet == set) continue;

                            if (renderers[r].profileSet != null)
                            {
                                result.problems.Add($"{scene.name}/{renderers[r].name}: already has '{renderers[r].profileSet.name}', left untouched");
                                continue;
                            }

                            renderers[r].profileSet = set;
                            EditorUtility.SetDirty(renderers[r]);
                            result.renderersAssigned++;
                            sceneDirty = true;
                        }

                        if (sceneDirty)
                        {
                            EditorSceneManager.MarkSceneDirty(scene);
                            if (EditorSceneManager.SaveScene(scene))
                                result.scenesAssigned++;
                            else
                                result.problems.Add($"{scene.name}: could not be saved, the assignment was not written to disk");
                        }
                    }
                }

                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorUtility.ClearProgressBar();

                if (!string.IsNullOrEmpty(originalScenePath) &&
                    !string.Equals(SceneManager.GetActiveScene().path, originalScenePath, StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(originalScenePath))
                {
                    EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
                }

                AssetDatabase.Refresh();
            }

            return result;
        }

        private static GrassPlatformProfile LoadOrCreateProfile(
            string path,
            GrassPlatformClass targetClass,
            GrassProfileApplyResult result,
            HashSet<string> needsSeed)
        {
            var profile = AssetDatabase.LoadAssetAtPath<GrassPlatformProfile>(path);
            if (profile != null)
                return profile;

            profile = ScriptableObject.CreateInstance<GrassPlatformProfile>();
            profile.targetClass = targetClass;
            profile.overrideMesh = false;
            profile.overrideThinning = false;
            profile.overrideInstanceDensity = false;
            profile.overrideAlbedo = false;
            profile.overrideDrawDistance = false;
            profile.overrideShadows = false;
            profile.overrideReceiveShadows = false;

            AssetDatabase.CreateAsset(profile, path);
            result.profilesCreated++;
            needsSeed.Add(path);
            return profile;
        }

        private static void SeedFromRenderer(GrassPlatformProfile profile, GrassRendererConsole renderer)
        {
            if (profile == null || renderer == null) return;

            profile.farKeepFraction = renderer.farKeepFraction;
            profile.thinStartDistance = renderer.thinStartDistance;
            profile.thinRampDistance = renderer.thinRampDistance;
            profile.coverageCompensation = renderer.coverageCompensation;
            profile.sizeScale = renderer.sizeScale;
            profile.instanceDensity = renderer.instanceDensity;

            SO_GrassSettings settings = renderer.settings;
            if (settings != null)
            {
                profile.meshMode = settings.grassMode;
                profile.proceduralType = settings.proceduralType;
                profile.minFadeDistance = settings.minFadeDistance;
                profile.maxDrawDistance = settings.maxDrawDistance;
                profile.castShadows = settings.castShadows;
                profile.receiveShadows = settings.useReceiveShadows;
            }

            EditorUtility.SetDirty(profile);
        }

        private static List<GrassRendererConsole> FindRenderersInScene(Scene scene)
        {
            var found = new List<GrassRendererConsole>();
            GrassRendererConsole[] all = UnityEngine.Object.FindObjectsByType<GrassRendererConsole>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].gameObject.scene == scene)
                    found.Add(all[i]);
            }
            return found;
        }

        private static bool OpenScene(string scenePath)
        {
            if (string.Equals(SceneManager.GetActiveScene().path, scenePath, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!File.Exists(scenePath))
                return false;

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            return string.Equals(SceneManager.GetActiveScene().path, scenePath, StringComparison.OrdinalIgnoreCase);
        }

        private static bool SceneReferences(GrassProfileSceneRef scene, string assetPath)
        {
            if (scene.deps != null)
                return scene.deps.Contains(assetPath);

            return SceneReferencesAsset(scene.scenePath, assetPath);
        }

        private static bool SceneReferencesAnyExcept(GrassProfileSceneRef scene, HashSet<string> setPaths, string exceptPath)
        {
            if (scene.deps == null)
                return SceneReferencesAnyExcept(scene.scenePath, setPaths, exceptPath);

            foreach (string setPath in setPaths)
            {
                if (string.Equals(setPath, exceptPath, StringComparison.OrdinalIgnoreCase)) continue;
                if (scene.deps.Contains(setPath)) return true;
            }
            return false;
        }

        private static bool SceneReferencesAnyExcept(string scenePath, HashSet<string> setPaths, string exceptPath)
        {
            InspectSceneSets(scenePath, setPaths, exceptPath, out _, out bool referencesOther);
            return referencesOther;
        }

        private static void InspectSceneSets(
            string scenePath,
            HashSet<string> setPaths,
            string targetPath,
            out bool referencesTarget,
            out bool referencesOther)
        {
            referencesTarget = false;
            referencesOther = false;

            string[] deps = AssetDatabase.GetDependencies(scenePath, true);
            for (int i = 0; i < deps.Length; i++)
            {
                if (string.Equals(deps[i], targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    referencesTarget = true;
                    continue;
                }

                if (setPaths.Contains(deps[i]))
                    referencesOther = true;
            }
        }

        private static HashSet<string> CollectProfileSetPaths()
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] guids = AssetDatabase.FindAssets("t:GrassPlatformProfileSet");
            for (int i = 0; i < guids.Length; i++)
                paths.Add(AssetDatabase.GUIDToAssetPath(guids[i]));
            return paths;
        }

        private static bool SceneReferencesAsset(string scenePath, string assetPath)
        {
            string[] deps = AssetDatabase.GetDependencies(scenePath, true);
            for (int i = 0; i < deps.Length; i++)
            {
                if (string.Equals(deps[i], assetPath, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static void ResolveGroup(string sceneName, bool splitDayNight, out string groupKey, out string folderScene)
        {
            string canonical = GrassAssetStandardizer.CanonicalSceneName(sceneName);

            if (string.IsNullOrEmpty(canonical))
            {
                groupKey = Sanitize(sceneName);
                folderScene = groupKey;
                return;
            }

            folderScene = canonical;

            string dayNight = GrassAssetStandardizer.SceneDayNight(sceneName);
            groupKey = splitDayNight && !string.IsNullOrEmpty(dayNight)
                ? $"{canonical}_{dayNight}"
                : canonical;
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "Unnamed";

            char[] invalid = Path.GetInvalidFileNameChars();
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalid, chars[i]) >= 0 || chars[i] == ' ')
                    chars[i] = '_';
            }
            return new string(chars);
        }

        private static void EnsureFolder(string assetFolderPath)
        {
            if (string.IsNullOrEmpty(assetFolderPath)) return;
            if (AssetDatabase.IsValidFolder(assetFolderPath)) return;

            string[] segments = assetFolderPath.Replace('\\', '/').Split('/');
            if (segments.Length == 0) return;

            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = $"{current}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }
    }
}
