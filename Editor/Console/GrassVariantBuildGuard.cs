// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using GrassSystem.Consoles;

namespace GrassSystem.Consoles.Editor
{
    public class GrassVariantBuildGuard : IProcessSceneWithReport
    {
        public const string BypassEnvVar = "GRASS_SKIP_VARIANT_GUARD";

        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null) return;

            var violations = new List<string>();

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GrassRendererConsole[] renderers = roots[i].GetComponentsInChildren<GrassRendererConsole>(true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    GrassRendererConsole renderer = renderers[r];
                    if (renderer == null || renderer.variantMode == PlatformVariant.Auto)
                        continue;

                    violations.Add($"\"{BuildHierarchyPath(renderer.transform)}\" is set to {renderer.variantMode}");
                }
            }

            if (violations.Count == 0) return;

            string message =
                $"GrassVariantBuildGuard: scene \"{scene.name}\" ships {violations.Count} grass renderer(s) that ignore the target platform - " +
                string.Join("; ", violations) + ". " +
                "Auto is what picks the Switch profile on Switch and the Full profile everywhere else; Force* is an Editor-only debug override. " +
                $"Set Platform Variant back to Auto on the prefab, or set {BypassEnvVar}=1 to bypass.";

            if (Environment.GetEnvironmentVariable(BypassEnvVar) == "1")
            {
                Debug.LogWarning(message);
                return;
            }

            throw new BuildFailedException(message);
        }

        private static string BuildHierarchyPath(Transform transform)
        {
            var parts = new List<string>();
            Transform current = transform;

            while (current != null)
            {
                parts.Add(current.name);
                current = current.parent;
            }

            parts.Reverse();

            var sb = new StringBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0) sb.Append('/');
                sb.Append(parts[i]);
            }

            return sb.ToString();
        }
    }
}
