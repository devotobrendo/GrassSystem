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
    public class GrassProfileSceneStatus
    {
        public string scenePath;
        public string sceneName;
        public string groupName;
        public string folder;
        public string setPath;
        public string fullPath;
        public string switchPath;

        public GrassPlatformProfileSet set;
        public GrassPlatformProfile full;
        public GrassPlatformProfile switchProfile;

        public int rendererCount;
        public int renderersUnassigned;
        public int renderersOnOtherSet;
        public string blocker;

        public bool AssetsComplete => set != null && full != null && switchProfile != null;
        public bool SetWiringBroken => set != null && (set.fullProfile != full || set.switchProfile != switchProfile);
        public bool CanCreate => blocker == null && (!AssetsComplete || SetWiringBroken || renderersUnassigned > 0);
    }

    public class GrassProfileApplyResult
    {
        public int profilesCreated;
        public int renderersAssigned;
        public bool setCreated;
        public bool setRepaired;
        public readonly List<string> problems = new List<string>();
    }

    public static class GrassProfileFactory
    {
        private const string GrassRootFolder = "Assets/Grass";
        private const string ProfilesFolderSegment = "Profiles";
        private const string SetPrefix = "GrassProfileSet";
        private const string ProfilePrefix = "GrassProfile";

        public static GrassProfileSceneStatus InspectOpenScene()
        {
            var status = new GrassProfileSceneStatus();

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                status.blocker = "No valid open scene.";
                return status;
            }

            status.scenePath = scene.path;
            status.sceneName = scene.name;

            if (string.IsNullOrEmpty(scene.path))
            {
                status.blocker = "The open scene has never been saved - save it first so its profiles get a folder.";
                return status;
            }

            ResolveNaming(scene.name, out string groupName, out string folderScene);
            status.groupName = groupName;
            status.folder = $"{GrassRootFolder}/{folderScene}/{ProfilesFolderSegment}";
            status.setPath = $"{status.folder}/{SetPrefix}_{groupName}.asset";
            status.fullPath = $"{status.folder}/{ProfilePrefix}_{groupName}_Full.asset";
            status.switchPath = $"{status.folder}/{ProfilePrefix}_{groupName}_Switch.asset";

            status.set = AssetDatabase.LoadAssetAtPath<GrassPlatformProfileSet>(status.setPath);
            status.full = AssetDatabase.LoadAssetAtPath<GrassPlatformProfile>(status.fullPath);
            status.switchProfile = AssetDatabase.LoadAssetAtPath<GrassPlatformProfile>(status.switchPath);

            List<GrassRendererConsole> renderers = FindRenderersInScene(scene);
            status.rendererCount = renderers.Count;

            for (int i = 0; i < renderers.Count; i++)
            {
                GrassPlatformProfileSet assigned = renderers[i].profileSet;
                if (assigned == null)
                    status.renderersUnassigned++;
                else if (status.set == null || assigned != status.set)
                    status.renderersOnOtherSet++;
            }

            if (renderers.Count == 0)
                status.blocker = "This scene has no GrassRendererConsole - run the console migration first.";

            return status;
        }

        public static GrassProfileApplyResult CreateForOpenScene()
        {
            var result = new GrassProfileApplyResult();
            GrassProfileSceneStatus status = InspectOpenScene();

            if (status.blocker != null)
            {
                result.problems.Add(status.blocker);
                return result;
            }

            Scene scene = SceneManager.GetActiveScene();
            List<GrassRendererConsole> renderers = FindRenderersInScene(scene);
            if (renderers.Count == 0)
            {
                result.problems.Add("No GrassRendererConsole in the open scene.");
                return result;
            }

            EnsureFolder(status.folder);

            GrassPlatformProfile full = status.full;
            if (full == null)
            {
                full = CreateProfile(status.fullPath, GrassPlatformClass.Full, renderers[0]);
                result.profilesCreated++;
            }

            GrassPlatformProfile switchProfile = status.switchProfile;
            if (switchProfile == null)
            {
                switchProfile = CreateProfile(status.switchPath, GrassPlatformClass.Switch, renderers[0]);
                result.profilesCreated++;
            }

            GrassPlatformProfileSet set = status.set;
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<GrassPlatformProfileSet>();
                set.fullProfile = full;
                set.switchProfile = switchProfile;
                AssetDatabase.CreateAsset(set, status.setPath);
                result.setCreated = true;
            }
            else if (set.fullProfile != full || set.switchProfile != switchProfile)
            {
                set.fullProfile = full;
                set.switchProfile = switchProfile;
                EditorUtility.SetDirty(set);
                result.setRepaired = true;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            for (int i = 0; i < renderers.Count; i++)
            {
                GrassRendererConsole renderer = renderers[i];
                if (renderer.profileSet == set) continue;

                if (renderer.profileSet != null)
                {
                    result.problems.Add($"{renderer.name} already points at '{renderer.profileSet.name}', left untouched.");
                    continue;
                }

                Undo.RecordObject(renderer, "Assign Grass Profile Set");
                renderer.profileSet = set;
                EditorUtility.SetDirty(renderer);
                result.renderersAssigned++;
            }

            if (result.renderersAssigned > 0)
                EditorSceneManager.MarkSceneDirty(scene);

            return result;
        }

        private static GrassPlatformProfile CreateProfile(string path, GrassPlatformClass targetClass, GrassRendererConsole renderer)
        {
            var profile = ScriptableObject.CreateInstance<GrassPlatformProfile>();
            profile.targetClass = targetClass;

            profile.overrideMesh = true;
            profile.overrideThinning = true;
            profile.overrideInstanceDensity = true;
            profile.overrideAlbedo = true;
            profile.overrideDrawDistance = true;
            profile.overrideShadows = true;
            profile.overrideReceiveShadows = true;

            profile.farKeepFraction = renderer.farKeepFraction;
            profile.thinStartDistance = renderer.thinStartDistance;
            profile.thinRampDistance = renderer.thinRampDistance;
            profile.coverageCompensation = renderer.coverageCompensation;
            profile.sizeScale = renderer.sizeScale;
            profile.instanceDensity = renderer.instanceDensity;
            profile.useFlatAlbedo = false;

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

            AssetDatabase.CreateAsset(profile, path);
            return profile;
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

        private static void ResolveNaming(string sceneName, out string groupName, out string folderScene)
        {
            string canonical = GrassAssetStandardizer.CanonicalSceneName(sceneName);

            if (string.IsNullOrEmpty(canonical))
            {
                groupName = Sanitize(sceneName);
                folderScene = groupName;
                return;
            }

            folderScene = canonical;

            string dayNight = GrassAssetStandardizer.SceneDayNight(sceneName);
            groupName = string.IsNullOrEmpty(dayNight) ? canonical : $"{canonical}_{dayNight}";
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
