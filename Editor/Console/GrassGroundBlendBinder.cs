// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using UnityEditor;
using UnityEngine;

namespace GrassSystem.Consoles.Editor
{
    public static class GrassGroundBlendBinder
    {
        private const string MenuPath = "Tools/Grass System/Bind Ground Blend To Selection";

        private static readonly int PropOverrideMap = Shader.PropertyToID("_GrassOverrideMap");
        private static readonly int PropMultiplyMap = Shader.PropertyToID("_GrassMultiplyMap");
        private static readonly int PropBounds = Shader.PropertyToID("_GrassDecalBounds");
        private static readonly int PropBlend = Shader.PropertyToID("_GrassBlend");

        [MenuItem(MenuPath, true)]
        private static bool Validate()
        {
            return Selection.activeGameObject != null &&
                   Selection.activeGameObject.GetComponent<Renderer>() != null;
        }

        [MenuItem(MenuPath)]
        private static void Bind()
        {
            Renderer renderer = Selection.activeGameObject.GetComponent<Renderer>();
            if (renderer == null)
            {
                EditorUtility.DisplayDialog("Ground Blend", "Select a GameObject with a Renderer (the ground mesh).", "OK");
                return;
            }

            GrassDecalBakeAsset bake = FindBakeAsset();
            if (bake == null)
            {
                EditorUtility.DisplayDialog("Ground Blend",
                    "No baked decal asset found in the open scene. The ground samples the same maps the grass uses, so a GrassRendererConsole with a Baked Decal Asset has to be present.",
                    "OK");
                return;
            }

            int bound = 0;
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null || material.shader == null || !material.HasProperty(PropBounds))
                    continue;

                Undo.RecordObject(material, "Bind Ground Blend");
                material.SetTexture(PropOverrideMap, bake.overrideMap);
                material.SetTexture(PropMultiplyMap, bake.multiplyMap);
                material.SetVector(PropBounds, bake.bounds);
                if (material.GetFloat(PropBlend) <= 0f)
                    material.SetFloat(PropBlend, 0.6f);
                EditorUtility.SetDirty(material);
                bound++;
            }

            AssetDatabase.SaveAssets();

            if (bound == 0)
            {
                EditorUtility.DisplayDialog("Ground Blend",
                    "None of this renderer's materials expose _GrassDecalBounds. Switch the ground material to a shader that includes GrassGroundBlend.hlsl first.",
                    "OK");
                return;
            }

            Debug.Log($"Ground Blend: bound {bound} material(s) on '{renderer.name}' to '{bake.name}' (bounds {bake.bounds}).", renderer);
        }

        private static GrassDecalBakeAsset FindBakeAsset()
        {
            var consoles = Object.FindObjectsByType<GrassRendererConsole>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < consoles.Length; i++)
            {
                if (consoles[i] != null && consoles[i].BakedDecalAsset != null)
                    return consoles[i].BakedDecalAsset;
            }

            return null;
        }
    }
}
