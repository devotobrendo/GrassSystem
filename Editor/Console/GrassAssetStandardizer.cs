using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using GrassSystem;
using GrassSystem.Consoles;

namespace GrassSystem.Consoles.Editor
{
    public class StandardizePlanEntry
    {
        public string assetPath;
        public string targetPath;
        public string category;
        public string scene;
        public string status;
        public string note;
        public UnityEngine.Object pingTarget;
    }

    public class StandardizeApplyResult
    {
        public int moved;
        public int skipped;
        public int failed;
        public List<string> notes = new List<string>();
    }

    public static class GrassAssetStandardizer
    {
        public const string CategoryData = "Data";
        public const string CategorySettings = "Settings";
        public const string CategoryDecal = "Decal";
        public const string CategoryMaterial = "Material";

        public const string StatusReady = "Ready";
        public const string StatusAlreadyStandard = "AlreadyStandard";
        public const string StatusAmbiguous = "Ambiguous";
        public const string StatusDeferred = "Deferred";
        public const string StatusUnused = "Unused";

        private const string ScenesFolder = "Assets/Scenes";
        private const string GrassRootFolder = "Assets/Grass";
        private const string DecalFolderSegment = "Decals";
        private const string MaterialFolder = "Assets/GrassSystem-Test/Material";

        private const string MaterialLeadingPrefix = "MT";

        private static readonly HashSet<string> MaterialBoilerplateTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Grass",
            "Mat",
            "Material",
            "Turf",
            "Foliage",
            "System",
        };

        private static readonly HashSet<string> StandardizedMarkerTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Shared",
            "GrassData",
            "GrassSettings",
            "GrassMat",
            "MT",
        };

        private static readonly KeyValuePair<string, string>[] FullNameMap =
            new[]
            {
                new KeyValuePair<string, string>("Steele Stadium", "SteeleStadium"),
                new KeyValuePair<string, string>("Big City Stadium", "BigCityStadium"),
                new KeyValuePair<string, string>("Dirt Yards", "DirtYards"),
                new KeyValuePair<string, string>("Eckman Acres", "EckmanAcres"),
                new KeyValuePair<string, string>("Playground Commons", "PlaygroundCommons"),
                new KeyValuePair<string, string>("Parks Dept Fields No. 2", "ParksDeptFieldsNo2"),
                new KeyValuePair<string, string>("Super Colossal Dome", "SuperColossalDome"),
                new KeyValuePair<string, string>("Wiffle Ball", "WiffleBall"),
                new KeyValuePair<string, string>("TreehouseScene", "TeamPicker"),
                new KeyValuePair<string, string>("TeamPicker", "TeamPicker"),
                new KeyValuePair<string, string>("MainTitle", "MainTitle"),
            }
            .OrderByDescending(kvp => kvp.Key.Length)
            .ToArray();

        private static readonly Dictionary<string, string> CodeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "SS", "SteeleStadium" },
            { "BCS", "BigCityStadium" },
            { "DY", "DirtYards" },
            { "EA", "EckmanAcres" },
            { "PC", "PlaygroundCommons" },
            { "PDF", "ParksDeptFieldsNo2" },
            { "SCD", "SuperColossalDome" },
            { "WB", "WiffleBall" },
            { "TP", "TeamPicker" },
        };

        private struct SceneUsageInfo
        {
            public string canonicalScene;
            public string dayNight;
            public HashSet<string> deps;
        }

        public static List<StandardizePlanEntry> BuildPlan()
        {
            var entries = new List<StandardizePlanEntry>();
            List<SceneUsageInfo> sceneInfos;

            try
            {
                sceneInfos = BuildSceneInfos();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            CollectDataEntries(entries, sceneInfos);
            CollectSettingsEntries(entries, sceneInfos);
            CollectDecalEntries(entries, sceneInfos);
            CollectMaterialEntries(entries, sceneInfos);

            ResolveCollisions(entries);

            return entries;
        }

        public static StandardizeApplyResult Apply(List<StandardizePlanEntry> plan)
        {
            var result = new StandardizeApplyResult();

            foreach (StandardizePlanEntry entry in plan)
            {
                bool isMovableStatus = entry.status == StatusReady || entry.status == StatusUnused;
                if (!isMovableStatus || string.IsNullOrEmpty(entry.targetPath))
                {
                    result.skipped++;
                    continue;
                }

                if (string.IsNullOrEmpty(entry.targetPath) || AssetDatabase.LoadMainAssetAtPath(entry.assetPath) == null)
                {
                    result.skipped++;
                    result.notes.Add($"skip {Path.GetFileName(entry.assetPath)}: source missing or no target");
                    continue;
                }

                if (AssetDatabase.LoadMainAssetAtPath(entry.targetPath) != null)
                {
                    result.skipped++;
                    result.notes.Add($"skip {Path.GetFileName(entry.assetPath)}: target already exists");
                    continue;
                }

                EnsureFolder(Path.GetDirectoryName(entry.targetPath));

                string sourceDir = Path.GetDirectoryName(entry.assetPath)?.Replace('\\', '/');
                string sourceName = Path.GetFileNameWithoutExtension(entry.assetPath);
                string targetDir = Path.GetDirectoryName(entry.targetPath)?.Replace('\\', '/');
                string targetName = Path.GetFileNameWithoutExtension(entry.targetPath);

                string backupSourcePath = $"{sourceDir}/{sourceName}_backup.json";
                if (AssetDatabase.LoadMainAssetAtPath(backupSourcePath) != null)
                {
                    string backupTargetPath = $"{targetDir}/{targetName}_backup.json";
                    string backupErr = AssetDatabase.MoveAsset(backupSourcePath, backupTargetPath);
                    if (!string.IsNullOrEmpty(backupErr))
                        result.notes.Add($"backup move failed for {Path.GetFileName(entry.assetPath)}: {backupErr}");
                }

                string err = AssetDatabase.MoveAsset(entry.assetPath, entry.targetPath);
                if (string.IsNullOrEmpty(err))
                {
                    result.moved++;
                }
                else
                {
                    result.failed++;
                    result.notes.Add($"FAIL {Path.GetFileName(entry.assetPath)}: {err}");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return result;
        }

        public static string CanonicalSceneName(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return null;
            return ResolveScenePascal(sceneName, new List<string>(sceneName.Split('_')));
        }

        public static string SceneDayNight(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return null;
            return DetectDayNight(sceneName);
        }

        private static void EnsureFolder(string assetFolderPath)
        {
            if (string.IsNullOrEmpty(assetFolderPath)) return;

            string normalized = assetFolderPath.Replace('\\', '/');
            string[] segments = normalized.Split('/');
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

        private static string[] FindPathsInFolder(string filter, string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return Array.Empty<string>();
            string[] guids = AssetDatabase.FindAssets(filter, new[] { folder });
            var paths = new string[guids.Length];
            for (int i = 0; i < guids.Length; i++)
                paths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);
            return paths;
        }

        private static string[] FindPathsProjectWide(string filter)
        {
            string[] guids = AssetDatabase.FindAssets(filter);
            var paths = new string[guids.Length];
            for (int i = 0; i < guids.Length; i++)
                paths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);
            return paths;
        }

        private static List<SceneUsageInfo> BuildSceneInfos()
        {
            var sceneInfos = new List<SceneUsageInfo>();
            string[] scenePaths = AssetDatabase.IsValidFolder(ScenesFolder)
                ? FindPathsInFolder("t:Scene", ScenesFolder)
                : FindPathsProjectWide("t:Scene");

            for (int i = 0; i < scenePaths.Length; i++)
            {
                string scenePath = scenePaths[i];
                string sceneName = Path.GetFileNameWithoutExtension(scenePath);

                EditorUtility.DisplayProgressBar("Standardize", sceneName, (float)i / scenePaths.Length);

                var tokens = new List<string>(sceneName.Split('_'));
                string canonicalScene = ResolveScenePascal(sceneName, tokens);
                if (canonicalScene == null) continue;

                string dayNight = DetectDayNight(sceneName);
                var deps = new HashSet<string>(AssetDatabase.GetDependencies(scenePath, true), StringComparer.OrdinalIgnoreCase);

                sceneInfos.Add(new SceneUsageInfo
                {
                    canonicalScene = canonicalScene,
                    dayNight = dayNight,
                    deps = deps,
                });
            }

            return sceneInfos;
        }

        private static string DetectDayNight(string nameNoExt)
        {
            string[] tokens = nameNoExt.Split(new[] { ' ', '_' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string token in tokens)
            {
                if (string.Equals(token, "Day", StringComparison.OrdinalIgnoreCase)) return "Day";
                if (string.Equals(token, "Night", StringComparison.OrdinalIgnoreCase)) return "Night";
            }
            return null;
        }

        private static void CollectDataEntries(List<StandardizePlanEntry> entries, List<SceneUsageInfo> sceneInfos)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string path in FindPathsProjectWide("t:GrassDataAsset"))
            {
                if (!seen.Add(path)) continue;
                GrassDataAsset asset = AssetDatabase.LoadAssetAtPath<GrassDataAsset>(path);
                if (asset == null) continue;
                entries.Add(BuildUsageEntry(path, asset, CategoryData, "GrassData", "Data", "GrassData", false, false, null, sceneInfos));
            }

            foreach (string path in FindPathsProjectWide("t:GrassDataConsoleAsset"))
            {
                if (!seen.Add(path)) continue;
                GrassDataConsoleAsset asset = AssetDatabase.LoadAssetAtPath<GrassDataConsoleAsset>(path);
                if (asset == null) continue;
                entries.Add(BuildUsageEntry(path, asset, CategoryData, "GrassData", "Data", "GrassData", false, true, null, sceneInfos));
            }
        }

        private static void CollectSettingsEntries(List<StandardizePlanEntry> entries, List<SceneUsageInfo> sceneInfos)
        {
            foreach (string path in FindPathsProjectWide("t:SO_GrassSettings"))
            {
                SO_GrassSettings asset = AssetDatabase.LoadAssetAtPath<SO_GrassSettings>(path);
                if (asset == null) continue;
                entries.Add(BuildUsageEntry(path, asset, CategorySettings, "GrassSettings", "Settings", null, false, false, null, sceneInfos));
            }
        }

        private static void CollectDecalEntries(List<StandardizePlanEntry> entries, List<SceneUsageInfo> sceneInfos)
        {
            foreach (string path in FindPathsProjectWide("t:GrassDecalBakeAsset"))
            {
                GrassDecalBakeAsset bake = AssetDatabase.LoadAssetAtPath<GrassDecalBakeAsset>(path);
                if (bake == null) continue;

                bool used = ResolveUsage(path, sceneInfos, out string targetScene, out string dayNight);

                if (!used)
                {
                    entries.Add(new StandardizePlanEntry
                    {
                        assetPath = path,
                        category = CategoryDecal,
                        pingTarget = bake,
                        status = StatusUnused,
                        note = "not referenced by any scene",
                        targetPath = $"{GrassRootFolder}/_Unused/{DecalFolderSegment}/{Path.GetFileName(path)}",
                    });
                    continue;
                }

                string sceneFolder = targetScene ?? "_Shared";
                string baseName = BuildDecalName(targetScene ?? "Shared", dayNight);
                string folder = $"{GrassRootFolder}/{sceneFolder}/{DecalFolderSegment}";

                entries.Add(BuildDecalEntry(path, bake, targetScene, $"{folder}/{baseName}.asset",
                    targetScene == null ? "used by more than one scene" : null));

                AddDecalMapEntry(entries, bake.overrideMap, "Override", folder, baseName, targetScene);
                AddDecalMapEntry(entries, bake.multiplyMap, "Multiply", folder, baseName, targetScene);
                AddDecalMapEntry(entries, bake.additiveMap, "Additive", folder, baseName, targetScene);
            }
        }

        private static void AddDecalMapEntry(
            List<StandardizePlanEntry> entries,
            Texture2D map,
            string suffix,
            string folder,
            string baseName,
            string targetScene)
        {
            if (map == null) return;

            string path = AssetDatabase.GetAssetPath(map);
            if (string.IsNullOrEmpty(path)) return;

            string ext = Path.GetExtension(path);
            entries.Add(BuildDecalEntry(path, map, targetScene, $"{folder}/{baseName}_{suffix}{ext}", null));
        }

        private static StandardizePlanEntry BuildDecalEntry(
            string path,
            UnityEngine.Object asset,
            string targetScene,
            string targetPath,
            string note)
        {
            return new StandardizePlanEntry
            {
                assetPath = path,
                category = CategoryDecal,
                scene = targetScene,
                pingTarget = asset,
                targetPath = targetPath,
                note = note,
                status = string.Equals(path, targetPath, StringComparison.OrdinalIgnoreCase)
                    ? StatusAlreadyStandard
                    : StatusReady,
            };
        }

        private static string BuildDecalName(string scenePascal, string dayNight)
        {
            var parts = new List<string> { "GrassDecal" };
            if (!string.IsNullOrEmpty(scenePascal)) parts.Add(scenePascal);
            if (!string.IsNullOrEmpty(dayNight)) parts.Add(dayNight);
            return string.Join("_", parts);
        }

        private static bool ResolveUsage(string path, List<SceneUsageInfo> sceneInfos, out string targetScene, out string dayNight)
        {
            targetScene = null;
            dayNight = null;

            List<SceneUsageInfo> usedScenes = sceneInfos.Where(si => si.deps.Contains(path)).ToList();
            if (usedScenes.Count == 0)
                return false;

            List<string> distinctScenes = usedScenes
                .Select(si => si.canonicalScene)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            string scene = distinctScenes.Count == 1 ? distinctScenes[0] : null;

            if (scene != null)
            {
                List<string> dnValues = usedScenes
                    .Where(si => string.Equals(si.canonicalScene, scene, StringComparison.OrdinalIgnoreCase))
                    .Select(si => si.dayNight)
                    .ToList();

                if (dnValues.Count > 0 && dnValues.All(v => v != null) && dnValues.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1)
                    dayNight = dnValues[0];
            }

            targetScene = scene;
            return true;
        }

        private static void CollectMaterialEntries(List<StandardizePlanEntry> entries, List<SceneUsageInfo> sceneInfos)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int nonGrassCount = 0;

            foreach (string folder in new[] { MaterialFolder, GrassRootFolder })
            {
                foreach (string path in FindPathsInFolder("t:Material", folder))
                {
                    if (!seen.Add(path)) continue;

                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (mat == null) continue;

                    string nameNoExt = Path.GetFileNameWithoutExtension(path);
                    bool isBladeByShader = mat.shader != null && mat.shader.name.IndexOf("GrassUnlit", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool isBladeByName = nameNoExt.StartsWith("MT_Grass", StringComparison.OrdinalIgnoreCase);

                    if (!isBladeByShader && !isBladeByName)
                    {
                        nonGrassCount++;
                        continue;
                    }

                    string shaderName = mat.shader != null ? mat.shader.name : null;
                    entries.Add(BuildUsageEntry(path, mat, CategoryMaterial, "GrassMat", "Materials", MaterialLeadingPrefix, true, false, shaderName, sceneInfos));
                }
            }

            entries.Add(new StandardizePlanEntry
            {
                assetPath = MaterialFolder,
                targetPath = string.Empty,
                category = CategoryMaterial,
                status = StatusDeferred,
                note = $"{nonGrassCount} non-grass materials left untouched",
                pingTarget = AssetDatabase.IsValidFolder(MaterialFolder) ? AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(MaterialFolder) : null,
            });
        }

        private static StandardizePlanEntry BuildUsageEntry(
            string path,
            UnityEngine.Object asset,
            string category,
            string namePrefix,
            string folderSegment,
            string leadingPrefix,
            bool stripMaterialBoilerplate,
            bool isConsoleType,
            string shaderName,
            List<SceneUsageInfo> sceneInfos)
        {
            var entry = new StandardizePlanEntry
            {
                assetPath = path,
                category = category,
                pingTarget = asset,
            };

            try
            {
                string nameNoExt = Path.GetFileNameWithoutExtension(path);

                if (!ResolveUsage(path, sceneInfos, out string targetScene, out string dayNight))
                {
                    entry.status = StatusUnused;
                    entry.targetPath = $"{GrassRootFolder}/_Unused/{folderSegment}/{Path.GetFileName(path)}";
                    entry.note = "not referenced by any scene";
                    return entry;
                }

                entry.scene = targetScene;

                string veg = ComputeVegFromName(nameNoExt, leadingPrefix, stripMaterialBoilerplate, targetScene, out bool verifyName);
                bool consoleTier = DetectConsoleTier(nameNoExt, isConsoleType, shaderName);

                string sceneNameToken = targetScene ?? "Shared";
                string standardName = BuildStandardName(namePrefix, sceneNameToken, dayNight, veg, consoleTier);
                string ext = Path.GetExtension(path);

                string targetPath;
                if (category == CategoryMaterial)
                {
                    targetPath = targetScene != null
                        ? $"{GrassRootFolder}/{targetScene}/Materials/{standardName}{ext}"
                        : $"{GrassRootFolder}/_Shared/{standardName}{ext}";
                }
                else
                {
                    string sceneFolder = targetScene ?? "_Shared";
                    targetPath = $"{GrassRootFolder}/{sceneFolder}/{folderSegment}/{standardName}{ext}";
                }

                entry.targetPath = targetPath;
                entry.note = verifyName ? "verify name" : null;
                entry.status = string.Equals(path, targetPath, StringComparison.OrdinalIgnoreCase)
                    ? StatusAlreadyStandard
                    : StatusReady;
            }
            catch (Exception e)
            {
                entry.status = StatusAmbiguous;
                entry.targetPath = string.Empty;
                entry.note = $"Failed to parse name: {e.Message}";
            }

            return entry;
        }

        private static string ComputeVegFromName(string nameNoExt, string leadingPrefix, bool stripMaterialBoilerplate, string usageScene, out bool verifyName)
        {
            verifyName = false;

            var tokens = new List<string>(nameNoExt.Split('_'));
            StripLeadingPrefix(tokens, leadingPrefix);

            string nameScenePascal = ResolveScenePascal(nameNoExt, tokens);
            var redundantSceneTokens = new List<string>();
            redundantSceneTokens.AddRange(GetRedundantSceneTokens(nameScenePascal));
            redundantSceneTokens.AddRange(GetRedundantSceneTokens(usageScene));

            var leftover = new List<string>();
            foreach (string token in tokens)
            {
                if (redundantSceneTokens.Any(t => string.Equals(t, token, StringComparison.OrdinalIgnoreCase)))
                    continue;

                if (string.Equals(token, "Day", StringComparison.OrdinalIgnoreCase) || string.Equals(token, "Night", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.Equals(token, "Console", StringComparison.OrdinalIgnoreCase) || string.Equals(token, "Slim", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (stripMaterialBoilerplate && MaterialBoilerplateTokens.Contains(token))
                    continue;

                if (StandardizedMarkerTokens.Contains(token))
                    continue;

                leftover.Add(token);
            }

            if (leftover.Count == 0)
                return "Main";

            var sb = new StringBuilder();
            foreach (string token in leftover)
                sb.Append(CleanToken(token));

            verifyName = leftover.Count > 1;
            return NormalizeVeg(sb.ToString());
        }

        private static bool DetectConsoleTier(string nameNoExt, bool isConsoleType, string shaderName)
        {
            if (isConsoleType) return true;
            if (nameNoExt.IndexOf("Console", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (nameNoExt.IndexOf("Slim", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (shaderName != null && shaderName.IndexOf("GrassUnlitConsole", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static void ResolveCollisions(List<StandardizePlanEntry> entries)
        {
            var groups = entries
                .Where(e => e.status == StatusReady && !string.IsNullOrEmpty(e.targetPath))
                .GroupBy(e => e.targetPath, StringComparer.OrdinalIgnoreCase);

            foreach (var group in groups)
            {
                if (group.Count() < 2) continue;

                foreach (StandardizePlanEntry entry in group)
                {
                    entry.status = StatusAmbiguous;
                    entry.note = "name collision - resolve manually";
                    entry.targetPath = string.Empty;
                }
            }
        }

        private static string ResolveScenePascal(string nameNoExt, IList<string> tokens)
        {
            foreach (var kvp in FullNameMap)
                if (nameNoExt.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                    return kvp.Value;

            foreach (var kvp in FullNameMap)
                if (nameNoExt.IndexOf(kvp.Value, StringComparison.OrdinalIgnoreCase) >= 0)
                    return kvp.Value;

            foreach (string token in tokens)
                if (CodeMap.TryGetValue(token, out string sceneFromCode))
                    return sceneFromCode;

            return null;
        }

        private static List<string> GetRedundantSceneTokens(string scenePascal)
        {
            var redundant = new List<string>();
            if (!string.IsNullOrEmpty(scenePascal)) redundant.Add(scenePascal);
            foreach (var kvp in FullNameMap)
                if (kvp.Value == scenePascal) redundant.Add(kvp.Key);
            foreach (var kvp in CodeMap)
                if (kvp.Value == scenePascal) redundant.Add(kvp.Key);
            return redundant;
        }

        private static void StripLeadingPrefix(List<string> tokens, string prefix)
        {
            if (prefix != null && tokens.Count > 1 && string.Equals(tokens[0], prefix, StringComparison.OrdinalIgnoreCase))
                tokens.RemoveAt(0);
        }

        private static string CleanToken(string token)
        {
            var sb = new StringBuilder(token.Length);
            foreach (char c in token)
                if (char.IsLetterOrDigit(c)) sb.Append(c);

            if (sb.Length == 0) return string.Empty;

            string result = sb.ToString();
            return char.ToUpperInvariant(result[0]) + result.Substring(1);
        }

        private static string NormalizeVeg(string rawVeg)
        {
            if (string.IsNullOrEmpty(rawVeg)) return "Main";

            int split = rawVeg.Length;
            while (split > 0 && char.IsDigit(rawVeg[split - 1]))
                split--;

            string descriptor = rawVeg.Substring(0, split);

            string stripped = StripTrailingWord(descriptor, "Settings") ?? StripTrailingWord(descriptor, "System");
            if (stripped != null)
                descriptor = stripped;

            if (descriptor.Length == 0 || string.Equals(descriptor, "Grass", StringComparison.OrdinalIgnoreCase))
                descriptor = "Main";

            return descriptor;
        }

        private static string StripTrailingWord(string value, string suffix)
        {
            if (value.Length > suffix.Length && value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return value.Substring(0, value.Length - suffix.Length);
            return null;
        }

        private static string BuildStandardName(string categoryPrefix, string scenePascal, string dayNight, string veg, bool consoleTier)
        {
            var parts = new List<string> { categoryPrefix };
            if (!string.IsNullOrEmpty(scenePascal)) parts.Add(scenePascal);
            if (!string.IsNullOrEmpty(dayNight)) parts.Add(dayNight);
            parts.Add(veg);
            if (consoleTier) parts.Add("Console");
            return string.Join("_", parts);
        }
    }
}
