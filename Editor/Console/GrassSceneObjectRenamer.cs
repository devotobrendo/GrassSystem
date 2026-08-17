using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GrassSystem;
using GrassSystem.Consoles;

namespace GrassSystem.Consoles.Editor
{
    public class GrassObjectRenameResult
    {
        public int renamed;
        public int skipped;
        public List<string> notes = new List<string>();
    }

    public static class GrassSceneObjectRenamer
    {
        private const string ConsoleSuffix = "_Console";

        private static readonly HashSet<string> SceneCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SS", "BCS", "DY", "EA", "PC", "PDF", "SCD", "WB", "TP"
        };

        public static GrassObjectRenameResult RenameOpenScene()
        {
            GrassObjectRenameResult result = new GrassObjectRenameResult();

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                result.notes.Add("No valid open scene to rename.");
                return result;
            }

            HashSet<GameObject> targets = new HashSet<GameObject>();
            CollectInScene<GrassRendererConsole>(scene, targets);
            CollectInScene<GrassRenderer>(scene, targets);
            CollectInScene<GrassDecal>(scene, targets);
            CollectContainers(scene, targets);

            if (targets.Count == 0)
            {
                result.notes.Add("No GrassRenderer, GrassRendererConsole, or GrassDecal objects found in the open scene.");
                return result;
            }

            Dictionary<string, int> nameCounts = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (GameObject go in targets)
            {
                string targetName = ComputeTargetName(go, scene.name);
                if (string.IsNullOrEmpty(targetName))
                {
                    result.skipped++;
                    continue;
                }

                if (!string.Equals(go.name, targetName, StringComparison.Ordinal))
                {
                    Undo.RecordObject(go, "Rename Grass Object");
                    go.name = targetName;
                    result.renamed++;
                }
                else
                {
                    result.skipped++;
                }

                nameCounts.TryGetValue(targetName, out int count);
                count++;
                nameCounts[targetName] = count;
                if (count == 2)
                    result.notes.Add($"Duplicate name after rename: \"{targetName}\" — Unity allows this; not auto-numbered.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            return result;
        }

        private static void CollectInScene<T>(Scene scene, HashSet<GameObject> targets) where T : Component
        {
            T[] components = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < components.Length; i++)
            {
                T component = components[i];
                if (component != null && component.gameObject.scene == scene)
                    targets.Add(component.gameObject);
            }
        }

        private static void CollectContainers(Scene scene, HashSet<GameObject> targets)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].transform.childCount == 0) continue;
                if (!GrassSceneMigrator.IsBareContainer(roots[i])) continue;
                if (roots[i].GetComponentInChildren<GrassRendererConsole>(true) == null) continue;

                targets.Add(roots[i]);
            }
        }

        private static string ComputeTargetName(GameObject go, string sceneName)
        {
            if (go.TryGetComponent(out GrassRendererConsole _))
            {
                string baseName = StripConsoleSuffix(go.name);
                string veg = ParseDescriptor(baseName);
                return $"GrassSystem_{veg}_Console";
            }

            if (GrassSceneMigrator.IsBareContainer(go))
                return GrassSceneMigrator.BuildContainerName(sceneName);

            if (go.TryGetComponent(out GrassRenderer _))
            {
                string veg = ParseDescriptor(go.name);
                return $"Grass_{veg}";
            }

            if (go.TryGetComponent(out GrassDecal _))
            {
                string area = ParseDescriptor(go.name);
                return $"GrassDecal_{area}";
            }

            return null;
        }

        private static string StripConsoleSuffix(string name)
        {
            if (name.EndsWith(ConsoleSuffix, StringComparison.OrdinalIgnoreCase))
                return name.Substring(0, name.Length - ConsoleSuffix.Length);
            return name;
        }

        private static string ParseDescriptor(string rawName)
        {
            string[] tokens = rawName.Split('_');
            int startIndex = 0;
            if (tokens.Length > 1 && SceneCodes.Contains(tokens[0]))
                startIndex = 1;

            StringBuilder joined = new StringBuilder();
            for (int i = startIndex; i < tokens.Length; i++)
                joined.Append(tokens[i]);

            string descriptor = joined.ToString();
            descriptor = RemoveAllIgnoreCase(descriptor, "Grass");
            descriptor = RemoveAllIgnoreCase(descriptor, "System");
            descriptor = RemoveAllIgnoreCase(descriptor, "Decal");

            StringBuilder filtered = new StringBuilder();
            for (int i = 0; i < descriptor.Length; i++)
            {
                if (char.IsLetterOrDigit(descriptor[i]))
                    filtered.Append(descriptor[i]);
            }

            string cleaned = filtered.ToString();
            if (string.IsNullOrEmpty(cleaned))
                return "Main";

            return char.ToUpperInvariant(cleaned[0]) + cleaned.Substring(1);
        }

        private static string RemoveAllIgnoreCase(string source, string token)
        {
            if (string.IsNullOrEmpty(source)) return source;
            int index;
            while ((index = source.IndexOf(token, StringComparison.OrdinalIgnoreCase)) >= 0)
                source = source.Remove(index, token.Length);
            return source;
        }
    }
}
