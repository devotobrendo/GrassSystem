using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GrassSystem;
using GrassSystem.Consoles;

namespace GrassSystem.Consoles.Editor
{
    public class MigrationResult
    {
        public bool ok;
        public string error;
        public int renderersMigrated;
        public string decalOutcome;
        public List<string> notes = new List<string>();
    }

    public static class GrassSceneMigrator
    {
        private const string SlimCullingShaderGuid = "85419baaf3e610d4183771d77ad1d592";
        private const string SlimGrassMaterialGuid = "15cb192cd89821c4fa8582990e4eaea9";
        private const string DecalBakeAssetName = "BakedGrassDecalMap";
        private const int DecalBakeResolution = 2048;

        public const string ConsoleObjectName = "GrassSystem_Main_Console";

        public static string BuildContainerName(string sceneName)
        {
            string canonical = GrassAssetStandardizer.CanonicalSceneName(sceneName);
            string dayNight = GrassAssetStandardizer.SceneDayNight(sceneName);

            var parts = new List<string>
            {
                string.IsNullOrEmpty(canonical) ? SanitizeForName(sceneName) : canonical,
                "Grass",
            };

            if (!string.IsNullOrEmpty(dayNight))
                parts.Add(dayNight);

            parts.Add("Console");
            return string.Join("_", parts);
        }

        public static MigrationResult MigrateOpenScene()
        {
            var result = new MigrationResult();

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                result.ok = false;
                result.error = "No valid open scene to migrate.";
                return result;
            }

            ComputeShader slimCullingShader = ResolveSlimCullingShader();
            if (slimCullingShader == null)
            {
                result.ok = false;
                result.error = "GrassCullingSlim.compute not found. Is the Grass System plugin imported? Right-click the package in the Project window and choose Reimport.";
                return result;
            }

            Material slimGrassMaterial = ResolveOrCreateSlimMaterial(result);
            if (slimGrassMaterial == null)
                return result;

            string sceneName = scene.name;

            List<GrassRenderer> renderers = Object.FindObjectsByType<GrassRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(r => r != null && r.gameObject.scene == scene)
                .ToList();

            if (renderers.Count == 0)
            {
                result.notes.Add("No GrassRenderer components found in the open scene.");
            }
            else
            {
                GrassRenderer mainRenderer = renderers[0];
                int maxCount = mainRenderer.HasExternalData ? mainRenderer.externalDataAsset.InstanceCount : mainRenderer.GrassDataList.Count;

                for (int i = 1; i < renderers.Count; i++)
                {
                    GrassRenderer candidate = renderers[i];
                    int count = candidate.HasExternalData ? candidate.externalDataAsset.InstanceCount : candidate.GrassDataList.Count;
                    if (count > maxCount)
                    {
                        maxCount = count;
                        mainRenderer = candidate;
                    }
                }

                foreach (GrassRenderer r in renderers)
                {
                    if (r == mainRenderer)
                        continue;

                    int count = r.HasExternalData ? r.externalDataAsset.InstanceCount : r.GrassDataList.Count;
                    result.notes.Add($"skipped {r.name} ({count:N0} instances) - not the main grass system");
                }

                result.notes.Add($"Main grass: {mainRenderer.name} ({maxCount:N0} instances)");

                renderers = new List<GrassRenderer> { mainRenderer };
            }

            GrassDecalBakeAsset fallbackDecal = ResolveFallbackDecal(scene, sceneName, renderers, result);

            Transform container = renderers.Count > 0 ? ResolveOrCreateContainer(scene, sceneName) : null;

            foreach (GrassRenderer renderer in renderers)
            {
                try
                {
                    MigrateRenderer(renderer, sceneName, container, slimCullingShader, slimGrassMaterial, fallbackDecal, result);
                }
                catch (System.Exception ex)
                {
                    result.notes.Add($"{renderer.name}: error - {ex.Message}");
                }
            }

            if (container != null && container.childCount == 0)
                Undo.DestroyObjectImmediate(container.gameObject);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();

            result.ok = true;
            return result;
        }

        public static void RevertOpenScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            var touchedContainers = new List<GameObject>();

            GrassRendererConsole[] consoles = Object.FindObjectsByType<GrassRendererConsole>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < consoles.Length; i++)
            {
                GrassRendererConsole console = consoles[i];
                if (console == null || console.gameObject.scene != scene)
                    continue;

                Transform parent = console.transform.parent;
                if (parent != null && IsBareContainer(parent.gameObject) && !touchedContainers.Contains(parent.gameObject))
                    touchedContainers.Add(parent.gameObject);

                GrassRenderer sameObjectRenderer = console.GetComponent<GrassRenderer>();
                if (sameObjectRenderer != null)
                {
                    Undo.RecordObject(sameObjectRenderer.gameObject, "Revert Grass Console Migration");
                    sameObjectRenderer.gameObject.SetActive(true);
                    Undo.RecordObject(sameObjectRenderer, "Revert Grass Console Migration");
                    sameObjectRenderer.enabled = true;
                    EditorUtility.SetDirty(sameObjectRenderer);
                    Undo.DestroyObjectImmediate(console);
                    continue;
                }

                string consoleName = console.gameObject.name;
                string originalName = consoleName.EndsWith("_Console")
                    ? consoleName.Substring(0, consoleName.Length - "_Console".Length)
                    : consoleName;

                GameObject originalGO = FindSiblingWithComponent<GrassRenderer>(console.transform.parent, scene, originalName);
                if (originalGO != null)
                {
                    Undo.RecordObject(originalGO, "Revert Grass Console Migration");
                    originalGO.SetActive(true);

                    GrassRenderer originalRenderer = originalGO.GetComponent<GrassRenderer>();
                    if (originalRenderer != null)
                    {
                        Undo.RecordObject(originalRenderer, "Revert Grass Console Migration");
                        originalRenderer.enabled = true;
                        EditorUtility.SetDirty(originalRenderer);
                    }
                }

                Undo.DestroyObjectImmediate(console.gameObject);
            }

            GrassRenderer[] allRenderers = Object.FindObjectsByType<GrassRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < allRenderers.Length; i++)
            {
                GrassRenderer renderer = allRenderers[i];
                if (renderer == null || renderer.gameObject.scene != scene || renderer.gameObject.activeSelf)
                    continue;

                Undo.RecordObject(renderer.gameObject, "Revert Grass Console Migration - Safety Net");
                renderer.gameObject.SetActive(true);
            }

            for (int i = 0; i < touchedContainers.Count; i++)
            {
                GameObject container = touchedContainers[i];
                if (container == null || container.transform.childCount > 0)
                    continue;

                Undo.DestroyObjectImmediate(container);
            }

            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void MigrateRenderer(
            GrassRenderer renderer,
            string sceneName,
            Transform container,
            ComputeShader slimCullingShader,
            Material slimGrassMaterial,
            GrassDecalBakeAsset fallbackDecal,
            MigrationResult result)
        {
            List<GrassData> source = renderer.HasExternalData ? renderer.externalDataAsset.LoadData() : renderer.GrassDataList;
            if (source == null || source.Count == 0)
            {
                result.notes.Add($"{renderer.name}: skipped - no grass data loaded.");
                return;
            }

            if (renderer.settings == null)
            {
                result.notes.Add($"{renderer.name}: skipped - no SO_GrassSettings assigned.");
                return;
            }

            string originalSettingsPath = AssetDatabase.GetAssetPath(renderer.settings);
            SO_GrassSettings consoleSettings = ResolveConsoleSettings(originalSettingsPath, slimCullingShader, slimGrassMaterial);
            if (consoleSettings == null)
            {
                result.notes.Add($"{renderer.name}: skipped - could not resolve/clone console settings.");
                return;
            }

            string originAssetPath = renderer.HasExternalData ? AssetDatabase.GetAssetPath(renderer.externalDataAsset) : null;
            string consoleDataPath = BuildConsoleDataPath(originAssetPath, sceneName, renderer.name);

            GrassDataConsoleAsset slimData = AssetDatabase.LoadAssetAtPath<GrassDataConsoleAsset>(consoleDataPath);
            if (slimData == null)
            {
                slimData = ScriptableObject.CreateInstance<GrassDataConsoleAsset>();
                AssetDatabase.CreateAsset(slimData, consoleDataPath);
            }
            GrassConsoleDataBakeService.Bake(source, sceneName, originAssetPath, slimData);

            Scene scene = renderer.gameObject.scene;
            string legacyConsoleName = $"{renderer.gameObject.name}_Console";
            GameObject consoleGO = FindExistingConsoleObject(scene, container, legacyConsoleName);

            if (consoleGO == null)
            {
                consoleGO = new GameObject(ConsoleObjectName);
                Undo.RegisterCreatedObjectUndo(consoleGO, "Create Grass Console Object");
                consoleGO.transform.SetParent(container, false);
                consoleGO.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
                consoleGO.transform.localScale = renderer.transform.lossyScale;
            }
            else
            {
                if (consoleGO.transform.parent != container)
                {
                    Undo.SetTransformParent(consoleGO.transform, container, "Move Grass Console Into Container");
                    result.notes.Add($"Moved '{consoleGO.name}' into {container.name}.");
                }

                if (!string.Equals(consoleGO.name, ConsoleObjectName, System.StringComparison.Ordinal))
                {
                    Undo.RecordObject(consoleGO, "Rename Grass Console Object");
                    consoleGO.name = ConsoleObjectName;
                }
            }

            GrassRendererConsole console = consoleGO.GetComponent<GrassRendererConsole>() ?? Undo.AddComponent<GrassRendererConsole>(consoleGO);

            GrassRendererConsole staleConsole = renderer.GetComponent<GrassRendererConsole>();
            if (staleConsole != null && staleConsole != console)
                Undo.DestroyObjectImmediate(staleConsole);

            GrassDecalBakeAsset decalForConsole = renderer.BakedDecalAsset != null ? renderer.BakedDecalAsset : fallbackDecal;

            GrassPlatformProfileSet existingSet = GrassProfileFactory.InspectOpenScene().set;

            Undo.RecordObject(console, "Migrate Grass To Console");
            console.settings = consoleSettings;
            console.dataAsset = slimData;
            if (decalForConsole != null)
                console.SetBakedDecalAsset(decalForConsole);
            if (console.profileSet == null && existingSet != null)
                console.profileSet = existingSet;
            console.enabled = true;
            EditorUtility.SetDirty(console);

            Undo.RecordObject(renderer.gameObject, "Disable Original Grass Object");
            renderer.gameObject.SetActive(false);

            result.renderersMigrated++;
            string decalNote = decalForConsole != null ? "" : " (no decal - bake one and re-run)";
            result.notes.Add($"{renderer.name}: migrated to '{container.name}/{ConsoleObjectName}' ({source.Count:N0} instances){decalNote}.");
        }

        private static Transform ResolveOrCreateContainer(Scene scene, string sceneName)
        {
            string containerName = BuildContainerName(sceneName);

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (string.Equals(roots[i].name, containerName, System.StringComparison.Ordinal))
                    return roots[i].transform;
            }

            Transform adopted = FindLegacyContainer(scene);
            if (adopted != null)
            {
                Undo.RecordObject(adopted.gameObject, "Rename Grass Console Container");
                adopted.gameObject.name = containerName;
                return adopted;
            }

            var created = new GameObject(containerName);
            Undo.RegisterCreatedObjectUndo(created, "Create Grass Console Container");
            created.transform.SetParent(null, false);
            created.transform.localPosition = Vector3.zero;
            created.transform.localRotation = Quaternion.identity;
            created.transform.localScale = Vector3.one;
            return created.transform;
        }

        private static Transform FindLegacyContainer(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (IsBareContainer(roots[i]) && roots[i].GetComponentInChildren<GrassRendererConsole>(true) != null)
                    return roots[i].transform;
            }
            return null;
        }

        public static bool IsBareContainer(GameObject go)
        {
            if (go == null) return false;
            if (go.transform.parent != null) return false;

            Component[] components = go.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
                if (!(components[i] is Transform))
                    return false;

            for (int i = 0; i < go.transform.childCount; i++)
                if (go.transform.GetChild(i).GetComponent<GrassRendererConsole>() == null)
                    return false;

            return true;
        }

        private static GameObject FindExistingConsoleObject(Scene scene, Transform container, string legacyConsoleName)
        {
            List<GrassRendererConsole> consoles = Object.FindObjectsByType<GrassRendererConsole>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(c => c != null && c.gameObject.scene == scene)
                .ToList();

            if (consoles.Count == 0)
                return null;

            foreach (GrassRendererConsole console in consoles)
            {
                if (console.transform.parent == container &&
                    string.Equals(console.gameObject.name, ConsoleObjectName, System.StringComparison.Ordinal))
                    return console.gameObject;
            }

            if (consoles.Count == 1)
                return consoles[0].gameObject;

            foreach (GrassRendererConsole console in consoles)
            {
                if (string.Equals(console.gameObject.name, legacyConsoleName, System.StringComparison.Ordinal))
                    return console.gameObject;
            }

            return null;
        }

        private static SO_GrassSettings ResolveConsoleSettings(string originalSettingsPath, ComputeShader slimCullingShader, Material slimGrassMaterial)
        {
            if (string.IsNullOrEmpty(originalSettingsPath))
                return null;

            string dir = Path.GetDirectoryName(originalSettingsPath)?.Replace('\\', '/');
            string name = Path.GetFileNameWithoutExtension(originalSettingsPath);
            if (string.IsNullOrEmpty(dir))
                return null;

            string consolePath = $"{dir}/{name}_Console.asset";
            SO_GrassSettings existing = AssetDatabase.LoadAssetAtPath<SO_GrassSettings>(consolePath);
            if (existing != null)
                return existing;

            if (!AssetDatabase.CopyAsset(originalSettingsPath, consolePath))
                return null;

            SO_GrassSettings clone = AssetDatabase.LoadAssetAtPath<SO_GrassSettings>(consolePath);
            if (clone == null)
                return null;

            clone.cullingShader = slimCullingShader;
            clone.grassMaterial = slimGrassMaterial;
            EditorUtility.SetDirty(clone);
            return clone;
        }

        private static string BuildConsoleDataPath(string originAssetPath, string sceneName, string rendererName)
        {
            if (!string.IsNullOrEmpty(originAssetPath))
            {
                string dir = Path.GetDirectoryName(originAssetPath)?.Replace('\\', '/');
                string name = Path.GetFileNameWithoutExtension(originAssetPath);
                if (!string.IsNullOrEmpty(dir))
                    return $"{dir}/{name}_Console.asset";
            }

            return $"Assets/GrassDataConsole_{SanitizeForPath(sceneName)}_{SanitizeForPath(rendererName)}.asset";
        }

        private static GrassDecalBakeAsset ResolveFallbackDecal(Scene scene, string sceneName, List<GrassRenderer> renderers, MigrationResult result)
        {
            bool anyNeedsFallback = renderers.Any(r => r != null && r.BakedDecalAsset == null);
            if (!anyNeedsFallback)
            {
                result.decalOutcome = "All renderers inherited an existing baked decal.";
                return null;
            }

            List<GrassDecal> activeDecals = FindActiveDecalsInScene(scene);
            if (activeDecals.Count == 0)
            {
                result.decalOutcome = "Some renderers have no baked decal and no active GrassDecal to bake - left manual.";
                return null;
            }

            string decalOutputFolder = $"Assets/BakedDecals/{SanitizeForPath(sceneName)}";
            string decalAssetPath = $"{decalOutputFolder}/{DecalBakeAssetName}.asset";
            GrassDecalBakeAsset existing = AssetDatabase.LoadAssetAtPath<GrassDecalBakeAsset>(decalAssetPath);
            if (existing != null)
            {
                result.decalOutcome = $"Reused existing fallback decal at {decalAssetPath}.";
                return existing;
            }

            try
            {
                GrassDecalBakeAsset baked = GrassDecalBakeService.Bake(activeDecals, decalOutputFolder, DecalBakeAssetName, DecalBakeResolution, false, false);
                result.decalOutcome = $"Baked fallback decal ({activeDecals.Count} decal(s)) for renderers without one.";
                return baked;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static List<GrassDecal> FindActiveDecalsInScene(Scene scene)
        {
            return Object.FindObjectsByType<GrassDecal>(FindObjectsSortMode.None)
                .Where(d => d != null && d.isActiveAndEnabled && d.gameObject.scene == scene)
                .ToList();
        }

        private static GameObject FindSiblingWithComponent<T>(Transform parent, Scene scene, string objectName) where T : Component
        {
            if (parent != null)
            {
                for (int i = 0; i < parent.childCount; i++)
                {
                    Transform child = parent.GetChild(i);
                    if (child.name == objectName && child.GetComponent<T>() != null)
                        return child.gameObject;
                }
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == objectName && roots[i].GetComponent<T>() != null)
                    return roots[i];
            }
            return null;
        }

        private static ComputeShader ResolveSlimCullingShader()
        {
            ComputeShader shader = LoadByGuid<ComputeShader>(SlimCullingShaderGuid);
            if (shader != null)
                return shader;

            string[] guids = AssetDatabase.FindAssets("GrassCullingSlim t:ComputeShader");
            if (guids.Length == 0)
                return null;

            return AssetDatabase.LoadAssetAtPath<ComputeShader>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static Material ResolveOrCreateSlimMaterial(MigrationResult result)
        {
            Material material = LoadByGuid<Material>(SlimGrassMaterialGuid);
            if (material != null)
                return material;

            string[] byMaterialName = AssetDatabase.FindAssets("MT_Grass_Slim t:Material");
            if (byMaterialName.Length > 0)
                return AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(byMaterialName[0]));

            string[] bySharedName = AssetDatabase.FindAssets("GrassMat_Shared_Main_Console t:Material");
            if (bySharedName.Length > 0)
                return AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(bySharedName[0]));

            string[] shaderGuids = AssetDatabase.FindAssets("GrassUnlitConsole t:Shader");
            if (shaderGuids.Length == 0)
            {
                result.ok = false;
                result.error = "GrassUnlitConsole.shader not found - the Grass System plugin is not imported correctly.";
                return null;
            }

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(shaderGuids[0]));

            if (!AssetDatabase.IsValidFolder("Assets/Grass"))
                AssetDatabase.CreateFolder("Assets", "Grass");
            if (!AssetDatabase.IsValidFolder("Assets/Grass/_Shared"))
                AssetDatabase.CreateFolder("Assets/Grass", "_Shared");

            string path = "Assets/Grass/_Shared/GrassMat_Shared_Main_Console.mat";
            Material created = new Material(shader) { name = "GrassMat_Shared_Main_Console" };
            AssetDatabase.CreateAsset(created, path);
            result.notes.Add($"Created shared console material at {path}.");
            return created;
        }

        private static T LoadByGuid<T>(string guid) where T : Object
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static string SanitizeForName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "Unnamed";

            var sb = new System.Text.StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
                if (char.IsLetterOrDigit(value[i]))
                    sb.Append(value[i]);

            return sb.Length == 0 ? "Unnamed" : sb.ToString();
        }

        private static string SanitizeForPath(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "Unnamed";

            char[] invalid = Path.GetInvalidFileNameChars();
            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (System.Array.IndexOf(invalid, chars[i]) >= 0)
                    chars[i] = '_';
            }
            return new string(chars);
        }
    }
}
