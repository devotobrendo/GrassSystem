using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GrassSystem
{
    public static class GrassDecalBakeService
    {
        private struct BakedMapResult
        {
            public string path;
            public Texture2D asset;
        }

        public static readonly string[] SwitchPlatformNames = { "Nintendo Switch", "Switch" };

        public static GrassDecalBakeAsset Bake(
            List<GrassDecal> decals,
            string outputFolder,
            string assetName,
            int resolution,
            bool disableOriginalsAfterBake,
            bool silent,
            int switchResolution = 0)
        {
            if (switchResolution <= 0)
                switchResolution = Mathf.Max(256, resolution / 2);
            switchResolution = Mathf.Min(switchResolution, resolution);

            EnsureTexturesReadable(decals);

            Vector4 mapBounds = ComputeMapBounds(decals);

            var sorted = decals.OrderBy(d => (int)d.layer).ToList();

            Shader bakeShader = Shader.Find("Hidden/GrassSystem/DecalBake");
            if (bakeShader == null)
                throw new System.Exception("Shader 'Hidden/GrassSystem/DecalBake' not found. Ensure GrassDecalBake.shader is in the project.");

            Material bakeMat = new Material(bakeShader);

            EnsureFolderExists(outputFolder);
            var overrideResult = BakeModeMap(sorted, mapBounds, bakeMat, DecalBlendMode.Override, $"{assetName}_Override", outputFolder, resolution, switchResolution, silent);
            var multiplyResult = BakeModeMap(sorted, mapBounds, bakeMat, DecalBlendMode.Multiply, $"{assetName}_Multiply", outputFolder, resolution, switchResolution, silent);
            var additiveResult = BakeModeMap(sorted, mapBounds, bakeMat, DecalBlendMode.Additive, $"{assetName}_Additive", outputFolder, resolution, switchResolution, silent);

            Object.DestroyImmediate(bakeMat);

            var bakeAsset = SaveOrUpdateBakeAsset(overrideResult, multiplyResult, additiveResult, mapBounds, outputFolder, assetName);

            if (disableOriginalsAfterBake)
            {
                foreach (var decal in decals)
                {
                    if (decal == null) continue;
                    Undo.RecordObject(decal.gameObject, "Apply Grass Decal Bake");
                    decal.gameObject.SetActive(false);
                }
            }

            return bakeAsset;
        }

        private static void EnsureTexturesReadable(List<GrassDecal> decals)
        {
            foreach (var decal in decals)
            {
                if (decal.decalTexture == null) continue;
                string path = AssetDatabase.GetAssetPath(decal.decalTexture);
                if (string.IsNullOrEmpty(path)) continue;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null && !importer.isReadable)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                }
            }
        }

        private static Vector4 ComputeMapBounds(List<GrassDecal> decals)
        {
            float minX = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxZ = float.MinValue;

            foreach (var decal in decals)
            {
                float halfX = decal.size.x / 2f;
                float halfZ = decal.size.y / 2f;
                Vector3[] corners = new Vector3[]
                {
                    decal.transform.TransformPoint(new Vector3(-halfX, 0, -halfZ)),
                    decal.transform.TransformPoint(new Vector3( halfX, 0, -halfZ)),
                    decal.transform.TransformPoint(new Vector3( halfX, 0,  halfZ)),
                    decal.transform.TransformPoint(new Vector3(-halfX, 0,  halfZ))
                };
                foreach (var c in corners)
                {
                    minX = Mathf.Min(minX, c.x);
                    minZ = Mathf.Min(minZ, c.z);
                    maxX = Mathf.Max(maxX, c.x);
                    maxZ = Mathf.Max(maxZ, c.z);
                }
            }

            return new Vector4(minX - 1f, minZ - 1f, (maxX - minX) + 2f, (maxZ - minZ) + 2f);
        }

        private static void EnsureFolderExists(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static BakedMapResult BakeModeMap(
            List<GrassDecal> sortedDecals,
            Vector4 mapBounds,
            Material bakeMat,
            DecalBlendMode targetMode,
            string fileNameBase,
            string outputFolder,
            int resolution,
            int switchResolution,
            bool silent)
        {
            var modeDecals = sortedDecals.Where(d => d.blendMode == targetMode).ToList();
            if (modeDecals.Count == 0)
            {
                DeleteExistingBakeAsset($"{outputFolder}/{fileNameBase}.png");
                return new BakedMapResult();
            }

            bool isColorMap = targetMode == DecalBlendMode.Override;
            RenderTextureFormat rtFormat = RenderTextureFormat.ARGB32;
            RenderTextureReadWrite readWrite = isColorMap ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear;
            TextureFormat textureFormat = TextureFormat.RGBA32;
            bool linearTexture = !isColorMap;
            string extension = "png";
            Color clearColor = targetMode == DecalBlendMode.Multiply
                ? new Color(1f, 1f, 1f, 0f)
                : new Color(0f, 0f, 0f, 0f);

            var rtA = RenderTexture.GetTemporary(resolution, resolution, 0, rtFormat, readWrite);
            var rtB = RenderTexture.GetTemporary(resolution, resolution, 0, rtFormat, readWrite);
            rtA.filterMode = FilterMode.Bilinear;
            rtB.filterMode = FilterMode.Bilinear;

            RenderTexture.active = rtA;
            GL.Clear(true, true, clearColor);
            RenderTexture.active = null;

            RenderTexture src = rtA;
            RenderTexture dst = rtB;

            for (int i = 0; i < modeDecals.Count; i++)
            {
                var decal = modeDecals[i];
                if (!silent)
                {
                    float modeBaseProgress = GetModeProgressStart(targetMode);
                    EditorUtility.DisplayProgressBar(
                        "Baking Decal Map",
                        $"{targetMode} {i + 1}/{modeDecals.Count}: {decal.gameObject.name}",
                        modeBaseProgress + 0.20f * ((float)i / Mathf.Max(1, modeDecals.Count)));
                }

                float totalRotation = (decal.rotation + decal.transform.eulerAngles.y) * Mathf.Deg2Rad;
                bakeMat.SetTexture("_DecalTex", decal.decalTexture);
                bakeMat.SetTexture("_PreviousMap", src);
                bakeMat.SetVector("_DecalBounds", new Vector4(
                    decal.transform.position.x,
                    decal.transform.position.z,
                    decal.size.x,
                    decal.size.y));
                bakeMat.SetFloat("_DecalRotation", totalRotation);
                bakeMat.SetFloat("_DecalBlend", decal.blend);
                bakeMat.SetFloat("_DecalBlendMode", (float)decal.blendMode);
                bakeMat.SetFloat("_BakeTargetMode", (float)targetMode);
                bakeMat.SetVector("_MapBounds", mapBounds);

                Graphics.Blit(src, dst, bakeMat);
                (src, dst) = (dst, src);
            }

            if (!silent)
            {
                EditorUtility.DisplayProgressBar(
                    "Baking Decal Map",
                    $"Saving {targetMode} map...",
                    GetModeSaveProgress(targetMode));
            }

            Texture2D result = new Texture2D(resolution, resolution, textureFormat, false, linearTexture);
            RenderTexture.active = src;
            result.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
            result.Apply();
            RenderTexture.active = null;

            RenderTexture.ReleaseTemporary(rtA);
            RenderTexture.ReleaseTemporary(rtB);

            string savePath = $"{outputFolder}/{fileNameBase}.{extension}";
            byte[] bytes = result.EncodeToPNG();
            File.WriteAllBytes(savePath, bytes);
            Object.DestroyImmediate(result);

            AssetDatabase.ImportAsset(savePath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(savePath) as TextureImporter;
            if (importer != null)
            {
                importer.sRGBTexture = isColorMap;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.crunchedCompression = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = resolution;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = isColorMap;
                importer.ignoreMipmapLimit = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                ApplySwitchPlatformSettings(importer, switchResolution);
                importer.SaveAndReimport();
            }

            return new BakedMapResult
            {
                path = savePath,
                asset = AssetDatabase.LoadAssetAtPath<Texture2D>(savePath)
            };
        }

        private static void ApplySwitchPlatformSettings(TextureImporter importer, int switchResolution)
        {
            foreach (string platform in SwitchPlatformNames)
            {
                TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
                settings.name = platform;
                settings.overridden = true;
                settings.maxTextureSize = switchResolution;
                settings.textureCompression = TextureImporterCompression.Compressed;
                settings.format = TextureImporterFormat.Automatic;
                importer.SetPlatformTextureSettings(settings);
            }
        }

        private static void DeleteExistingBakeAsset(string assetPath)
        {
            if (!File.Exists(assetPath))
                return;

            AssetDatabase.DeleteAsset(assetPath);
        }

        private static GrassDecalBakeAsset SaveOrUpdateBakeAsset(
            BakedMapResult overrideResult,
            BakedMapResult multiplyResult,
            BakedMapResult additiveResult,
            Vector4 bounds,
            string outputFolder,
            string assetName)
        {
            string bakeAssetPath = $"{outputFolder}/{assetName}.asset";
            var bakeAsset = AssetDatabase.LoadAssetAtPath<GrassDecalBakeAsset>(bakeAssetPath);
            if (bakeAsset == null)
            {
                bakeAsset = ScriptableObject.CreateInstance<GrassDecalBakeAsset>();
                AssetDatabase.CreateAsset(bakeAsset, bakeAssetPath);
            }

            bakeAsset.overrideMap = overrideResult.asset;
            bakeAsset.multiplyMap = multiplyResult.asset;
            bakeAsset.additiveMap = additiveResult.asset;
            bakeAsset.bounds = bounds;

            EditorUtility.SetDirty(bakeAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return bakeAsset;
        }

        private static float GetModeProgressStart(DecalBlendMode mode)
        {
            switch (mode)
            {
                case DecalBlendMode.Override:
                    return 0.10f;
                case DecalBlendMode.Multiply:
                    return 0.35f;
                default:
                    return 0.60f;
            }
        }

        private static float GetModeSaveProgress(DecalBlendMode mode)
        {
            switch (mode)
            {
                case DecalBlendMode.Override:
                    return 0.30f;
                case DecalBlendMode.Multiply:
                    return 0.55f;
                default:
                    return 0.80f;
            }
        }
    }
}
