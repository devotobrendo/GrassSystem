using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GrassSystem
{
    public class GrassDecalBakerWindow : EditorWindow
    {
        private int resolution = 2048;
        private string outputFolder = "Assets/BakedDecals";
        private string assetName = "BakedGrassDecalMap";
        private bool disableOriginalsAfterBake = true;

        // Per-renderer target list
        private List<GrassRenderer> targetRenderers = new List<GrassRenderer>();
        private ReorderableList rendererList;

        private Vector2 scrollPos;
        private GrassDecalBakeAsset loadedBakeAsset;

        [MenuItem("Tools/Grass System/Grass Decal Baker")]
        public static void ShowWindow()
        {
            var window = GetWindow<GrassDecalBakerWindow>("Grass Decal Baker");
            window.minSize = new Vector2(400, 380);
        }

        private void OnEnable()
        {
            resolution = EditorPrefs.GetInt("GrassDecalBaker_Resolution", 2048);
            outputFolder = EditorPrefs.GetString("GrassDecalBaker_OutputFolder", "Assets/BakedDecals");
            assetName = EditorPrefs.GetString("GrassDecalBaker_AssetName", "BakedGrassDecalMap");
            disableOriginalsAfterBake = EditorPrefs.GetBool("GrassDecalBaker_DisableOriginals", true);
            InitRendererList();
        }

        private void OnDisable()
        {
            EditorPrefs.SetInt("GrassDecalBaker_Resolution", resolution);
            EditorPrefs.SetString("GrassDecalBaker_OutputFolder", outputFolder);
            EditorPrefs.SetString("GrassDecalBaker_AssetName", assetName);
            EditorPrefs.SetBool("GrassDecalBaker_DisableOriginals", disableOriginalsAfterBake);
        }

        private void InitRendererList()
        {
            rendererList = new ReorderableList(targetRenderers, typeof(GrassRenderer), true, true, true, true);
            rendererList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Target GrassRenderers (leave empty = all)");
            rendererList.drawElementCallback = (rect, index, active, focused) =>
            {
                rect.y += 2;
                rect.height = EditorGUIUtility.singleLineHeight;
                targetRenderers[index] = (GrassRenderer)EditorGUI.ObjectField(rect, targetRenderers[index], typeof(GrassRenderer), true);
            };
            rendererList.onAddCallback = list =>
            {
                targetRenderers.Add(null);
            };
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            GUILayout.Label("Baked Decal Color Map", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Composites all active GrassDecal layers from the scene into a single world-space color map. " +
                "Target Renderers specifies which GrassRenderers receive the baked map — leave the list empty to apply to all.",
                MessageType.Info);

            EditorGUILayout.Space();

            // Target renderers list
            if (rendererList == null) InitRendererList();
            rendererList.DoLayoutList();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add All from Scene"))
            {
                var all = FindObjectsByType<GrassRenderer>(FindObjectsSortMode.None);
                foreach (var r in all)
                {
                    if (!targetRenderers.Contains(r))
                        targetRenderers.Add(r);
                }
            }
            if (GUILayout.Button("Clear"))
                targetRenderers.Clear();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Settings
            GUILayout.Label("Settings", EditorStyles.boldLabel);
            resolution = EditorGUILayout.IntPopup("Resolution", resolution,
                new[] { "512", "1024", "2048", "4096" },
                new[] { 512, 1024, 2048, 4096 });

            EditorGUILayout.BeginHorizontal();
            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Output Folder", "Assets", "");
                if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
                    outputFolder = "Assets" + path.Substring(Application.dataPath.Length);
            }
            EditorGUILayout.EndHorizontal();

            assetName = EditorGUILayout.TextField("Asset Name", assetName);
            disableOriginalsAfterBake = EditorGUILayout.Toggle("Disable Originals After Bake", disableOriginalsAfterBake);

            EditorGUILayout.Space();

            // Scene info
            GrassRenderer[] effectiveRenderers = GetEffectiveRenderers();
            var activeDecals = FindActiveDecals(effectiveRenderers);
            EditorGUILayout.LabelField("Active GrassDecals in scene", activeDecals.Count.ToString());
            EditorGUILayout.LabelField("Renderers to bake", effectiveRenderers.Length.ToString());

            if (activeDecals.Count == 0)
                EditorGUILayout.HelpBox("No active GrassDecals with textures found in the scene.", MessageType.Warning);

            EditorGUILayout.Space();

            GUILayout.Label("Actions", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Bake Maps gera os arquivos, cria/atualiza o Bake Asset e aplica automaticamente o resultado nos GrassRenderers selecionados.",
                MessageType.None);

            EditorGUI.BeginDisabledGroup(activeDecals.Count == 0);
            if (GUILayout.Button("Bake Maps And Apply", GUILayout.Height(30)))
                ExecuteBake(activeDecals, effectiveRenderers);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();
            GUILayout.Label("Loaded Bake", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            loadedBakeAsset = (GrassDecalBakeAsset)EditorGUILayout.ObjectField("Loaded Bake", loadedBakeAsset, typeof(GrassDecalBakeAsset), false);
            EditorGUI.EndChangeCheck();

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(loadedBakeAsset == null);
            if (GUILayout.Button("Ping Bake Asset"))
                EditorGUIUtility.PingObject(loadedBakeAsset);
            if (GUILayout.Button("Unload Loaded Bake"))
            {
                loadedBakeAsset = null;
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginDisabledGroup(loadedBakeAsset == null);
            if (GUILayout.Button("Apply Loaded Bake to Selected Renderers"))
                ApplyBakeToRenderers(loadedBakeAsset, effectiveRenderers, disableOriginalsAfterBake);
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Remove Bake And Re-enable Decals"))
                RemoveBakeAndReenableDecals(effectiveRenderers);

            // Result preview
            if (loadedBakeAsset != null)
            {
                EditorGUILayout.Space();
                GUILayout.Label("Generated Maps", EditorStyles.boldLabel);
                EditorGUILayout.ObjectField("Bake Asset", loadedBakeAsset, typeof(GrassDecalBakeAsset), false);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField("Override", loadedBakeAsset.overrideMap, typeof(Texture2D), false);
                if (GUILayout.Button("Ping", GUILayout.Width(50)) && loadedBakeAsset.overrideMap != null)
                    EditorGUIUtility.PingObject(loadedBakeAsset.overrideMap);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField("Multiply", loadedBakeAsset.multiplyMap, typeof(Texture2D), false);
                if (GUILayout.Button("Ping", GUILayout.Width(50)) && loadedBakeAsset.multiplyMap != null)
                    EditorGUIUtility.PingObject(loadedBakeAsset.multiplyMap);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField("Additive", loadedBakeAsset.additiveMap, typeof(Texture2D), false);
                if (GUILayout.Button("Ping", GUILayout.Width(50)) && loadedBakeAsset.additiveMap != null)
                    EditorGUIUtility.PingObject(loadedBakeAsset.additiveMap);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Vector4Field("Bounds", loadedBakeAsset.bounds);
            }

            EditorGUILayout.EndScrollView();
        }

        private GrassRenderer[] GetEffectiveRenderers()
        {
            var valid = targetRenderers.Where(r => r != null).ToArray();
            if (valid.Length == 0)
                return FindObjectsByType<GrassRenderer>(FindObjectsSortMode.None);
            return valid;
        }

        private List<GrassDecal> FindActiveDecals(GrassRenderer[] targetRendererArray)
        {
            var targetRenderers = new HashSet<GrassRenderer>(targetRendererArray.Where(r => r != null));
            return FindObjectsByType<GrassDecal>(FindObjectsSortMode.None)
                .Where(d => d.isActiveAndEnabled && d.decalTexture != null && DecalAffectsTargets(d, targetRenderers))
                .ToList();
        }

        private bool DecalAffectsTargets(GrassDecal decal, HashSet<GrassRenderer> targetRenderers)
        {
            if (targetRenderers == null || targetRenderers.Count == 0)
                return true;

            if (decal.autoFindAll || decal.targetRenderers == null || decal.targetRenderers.Count == 0)
                return true;

            for (int i = 0; i < decal.targetRenderers.Count; i++)
            {
                GrassRenderer renderer = decal.targetRenderers[i];
                if (renderer != null && targetRenderers.Contains(renderer))
                    return true;
            }

            return false;
        }

        private Vector4 ComputeMapBounds(List<GrassDecal> decals)
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

            // 1 world-unit padding
            return new Vector4(minX - 1f, minZ - 1f, (maxX - minX) + 2f, (maxZ - minZ) + 2f);
        }

        private void EnsureFolderExists(string folder)
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

        private void ExecuteBake(List<GrassDecal> decals, GrassRenderer[] renderers)
        {
            try
            {
                EditorUtility.DisplayProgressBar("Baking Decal Map", "Preparing...", 0f);

                EnsureTexturesReadable(decals);

                Vector4 mapBounds = ComputeMapBounds(decals);

                // Sort by layer priority so lower layers render first (higher layers override)
                var sorted = decals.OrderBy(d => (int)d.layer).ToList();

                Shader bakeShader = Shader.Find("Hidden/GrassSystem/DecalBake");
                if (bakeShader == null)
                    throw new System.Exception("Shader 'Hidden/GrassSystem/DecalBake' not found. Ensure GrassDecalBake.shader is in the project.");

                Material bakeMat = new Material(bakeShader);

                EnsureFolderExists(outputFolder);
                var overrideResult = BakeModeMap(sorted, mapBounds, bakeMat, DecalBlendMode.Override, $"{assetName}_Override");
                var multiplyResult = BakeModeMap(sorted, mapBounds, bakeMat, DecalBlendMode.Multiply, $"{assetName}_Multiply");
                var additiveResult = BakeModeMap(sorted, mapBounds, bakeMat, DecalBlendMode.Additive, $"{assetName}_Additive");

                DestroyImmediate(bakeMat);

                loadedBakeAsset = SaveOrUpdateBakeAsset(overrideResult, multiplyResult, additiveResult, mapBounds);
                ApplyBakeToRenderers(loadedBakeAsset, renderers, disableOriginalsAfterBake);

                EditorUtility.DisplayDialog("Bake Complete",
                    $"Baked {decals.Count} decal(s) and applied to {renderers.Length} renderer(s).\n\n" +
                    $"Bake Asset: {AssetDatabase.GetAssetPath(loadedBakeAsset)}\n" +
                    $"Override: {overrideResult.path}\n" +
                    $"Multiply: {multiplyResult.path}\n" +
                    $"Additive: {additiveResult.path}\n" +
                    $"Bounds: ({mapBounds.x:F1}, {mapBounds.y:F1})  size {mapBounds.z:F1} x {mapBounds.w:F1} m",
                    "OK");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GrassDecalBaker] {e}");
                EditorUtility.DisplayDialog("Bake Failed", e.Message, "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void RemoveBakeAndReenableDecals(GrassRenderer[] renderers)
        {
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                Undo.RecordObject(renderer, "Remove Grass Decal Bake");
                renderer.ClearBakedDecalAsset();
                EditorUtility.SetDirty(renderer);
            }

            var allDecals = FindObjectsByType<GrassDecal>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var decal in allDecals)
            {
                if (decal != null && !decal.gameObject.activeSelf)
                {
                    Undo.RecordObject(decal.gameObject, "Clear Baked Decal Map");
                    decal.gameObject.SetActive(true);
                }
            }

            Repaint();
            Debug.Log($"[GrassDecalBaker] Removed baked maps from {renderers.Length} renderer(s) and re-enabled original decals.");
        }

        private void EnsureTexturesReadable(List<GrassDecal> decals)
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

        private BakedMapResult BakeModeMap(
            List<GrassDecal> sortedDecals,
            Vector4 mapBounds,
            Material bakeMat,
            DecalBlendMode targetMode,
            string fileNameBase)
        {
            RenderTextureFormat rtFormat = RenderTextureFormat.ARGBHalf;
            RenderTextureReadWrite readWrite = RenderTextureReadWrite.Linear;
            TextureFormat textureFormat = TextureFormat.RGBAHalf;
            bool linearTexture = true;
            string extension = "exr";
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

            var modeDecals = sortedDecals.Where(d => d.blendMode == targetMode).ToList();
            for (int i = 0; i < modeDecals.Count; i++)
            {
                var decal = modeDecals[i];
                float modeBaseProgress = GetModeProgressStart(targetMode);
                EditorUtility.DisplayProgressBar(
                    "Baking Decal Map",
                    $"{targetMode} {i + 1}/{modeDecals.Count}: {decal.gameObject.name}",
                    modeBaseProgress + 0.20f * ((float)i / Mathf.Max(1, modeDecals.Count)));

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

            EditorUtility.DisplayProgressBar(
                "Baking Decal Map",
                $"Saving {targetMode} map...",
                GetModeSaveProgress(targetMode));

            Texture2D result = new Texture2D(resolution, resolution, textureFormat, false, linearTexture);
            RenderTexture.active = src;
            result.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
            result.Apply();
            RenderTexture.active = null;

            RenderTexture.ReleaseTemporary(rtA);
            RenderTexture.ReleaseTemporary(rtB);

            string savePath = $"{outputFolder}/{fileNameBase}.{extension}";
            byte[] bytes = result.EncodeToEXR(Texture2D.EXRFlags.None);
            File.WriteAllBytes(savePath, bytes);
            DestroyImmediate(result);

            AssetDatabase.ImportAsset(savePath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(savePath) as TextureImporter;
            if (importer != null)
            {
                importer.sRGBTexture = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = resolution;
                importer.alphaIsTransparency = false;
                importer.SaveAndReimport();
            }

            return new BakedMapResult
            {
                path = savePath,
                asset = AssetDatabase.LoadAssetAtPath<Texture2D>(savePath)
            };
        }

        private GrassDecalBakeAsset SaveOrUpdateBakeAsset(
            BakedMapResult overrideResult,
            BakedMapResult multiplyResult,
            BakedMapResult additiveResult,
            Vector4 bounds)
        {
            string bakeAssetPath = $"{outputFolder}/{assetName}.asset";
            var bakeAsset = AssetDatabase.LoadAssetAtPath<GrassDecalBakeAsset>(bakeAssetPath);
            if (bakeAsset == null)
            {
                bakeAsset = CreateInstance<GrassDecalBakeAsset>();
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

        private void ApplyBakeToRenderers(GrassDecalBakeAsset bakeAsset, GrassRenderer[] renderers, bool disableOriginalDecals)
        {
            if (bakeAsset == null)
                return;

            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                Undo.RecordObject(renderer, "Apply Grass Decal Bake");
                renderer.SetBakedDecalAsset(bakeAsset);
                EditorUtility.SetDirty(renderer);
            }

            if (disableOriginalDecals)
            {
                var effectiveRenderers = new HashSet<GrassRenderer>(renderers.Where(r => r != null));
                var decalsToDisable = FindActiveDecals(effectiveRenderers.ToArray());
                foreach (var decal in decalsToDisable)
                {
                    Undo.RecordObject(decal.gameObject, "Apply Grass Decal Bake");
                    decal.gameObject.SetActive(false);
                }
            }
            Debug.Log($"[GrassDecalBaker] Applied bake '{bakeAsset.name}' to {renderers.Length} renderer(s).");
        }

        private struct BakedMapResult
        {
            public string path;
            public Texture2D asset;
        }

        private float GetModeProgressStart(DecalBlendMode mode)
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

        private float GetModeSaveProgress(DecalBlendMode mode)
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

