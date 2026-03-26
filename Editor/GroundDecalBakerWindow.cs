// Copyright (c) 2026 MegaCats. All rights reserved.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;

namespace GrassSystem
{
    /// <summary>
    /// EditorWindow that bakes multiple GroundDecalProjector instances into a
    /// single combined mesh + texture atlas, reducing draw calls significantly.
    /// Access via: Tools > Grass System > Ground Decal Baker
    /// </summary>
    public class GroundDecalBakerWindow : EditorWindow
    {
        // --- EditorPrefs keys ---
        private const string PrefOutputFolder  = "GrassDecalBaker_OutputFolder";
        private const string PrefAssetName     = "GrassDecalBaker_AssetName";
        private const string PrefGoName        = "GrassDecalBaker_GoName";
        private const string PrefMaxAtlasSize  = "GrassDecalBaker_MaxAtlasSize";
        private const string PrefFilterMode    = "GrassDecalBaker_FilterMode";
        private const string PrefPadding       = "GrassDecalBaker_Padding";
        private const string PrefDisableOrig   = "GrassDecalBaker_DisableOriginals";

        // --- Settings ---
        private string     _outputFolder           = "Assets/BakedDecals";
        private string     _assetName              = "BakedDecalMesh";
        private string     _gameObjectName         = "BakedDecals";
        private int        _maxAtlasSizeIndex      = 2; // 2048
        private FilterMode _atlasFilterMode        = FilterMode.Bilinear;
        private int        _padding                = 4;
        private bool       _disableOriginalsAfterBake = true;

        private static readonly int[] AtlasSizeOptions = { 512, 1024, 2048, 4096 };
        private static readonly string[] AtlasSizeLabels = { "512", "1024", "2048", "4096" };

        // --- State ---
        private readonly List<GroundDecalProjector> _decals = new();
        private ReorderableList _reorderableList;
        private Vector2 _scrollPos;

        // Validation results
        private readonly List<string> _errors   = new();
        private readonly List<string> _warnings = new();

        // Post-bake results
        private string     _resultMeshPath;
        private string     _resultMatPath;
        private string     _resultAtlasPath;
        private GameObject _resultGameObject;

        [MenuItem("Tools/Grass System/Ground Decal Baker")]
        public static void OpenWindow()
        {
            var window = GetWindow<GroundDecalBakerWindow>("Ground Decal Baker");
            window.minSize = new Vector2(380, 520);
            window.Show();
        }

        private void OnEnable()
        {
            LoadPrefs();
            BuildReorderableList();
        }

        private void OnDisable() => SavePrefs();

        private void OnSelectionChange() => Repaint();

        private void LoadPrefs()
        {
            _outputFolder              = EditorPrefs.GetString(PrefOutputFolder, "Assets/BakedDecals");
            _assetName                 = EditorPrefs.GetString(PrefAssetName,    "BakedDecalMesh");
            _gameObjectName            = EditorPrefs.GetString(PrefGoName,       "BakedDecals");
            _maxAtlasSizeIndex         = EditorPrefs.GetInt   (PrefMaxAtlasSize, 2);
            _atlasFilterMode           = (FilterMode)EditorPrefs.GetInt(PrefFilterMode, (int)FilterMode.Bilinear);
            _padding                   = EditorPrefs.GetInt   (PrefPadding,      4);
            _disableOriginalsAfterBake = EditorPrefs.GetBool  (PrefDisableOrig,  true);
        }

        private void SavePrefs()
        {
            EditorPrefs.SetString(PrefOutputFolder, _outputFolder);
            EditorPrefs.SetString(PrefAssetName,    _assetName);
            EditorPrefs.SetString(PrefGoName,       _gameObjectName);
            EditorPrefs.SetInt   (PrefMaxAtlasSize, _maxAtlasSizeIndex);
            EditorPrefs.SetInt   (PrefFilterMode,   (int)_atlasFilterMode);
            EditorPrefs.SetInt   (PrefPadding,      _padding);
            EditorPrefs.SetBool  (PrefDisableOrig,  _disableOriginalsAfterBake);
        }

        private void BuildReorderableList()
        {
            _reorderableList = new ReorderableList(_decals, typeof(GroundDecalProjector),
                draggable: true, displayHeader: true,
                displayAddButton: false, displayRemoveButton: false);

            _reorderableList.drawHeaderCallback = rect =>
                EditorGUI.LabelField(rect, "Decals to Bake");

            _reorderableList.drawElementCallback = (rect, index, _, _) =>
            {
                if (index >= _decals.Count) return;
                var decal = _decals[index];

                float removeW = 22f;
                float infoW   = rect.width - removeW - 4f;

                string goName  = decal != null ? decal.gameObject.name : "(null)";
                string matName = decal != null && decal.decalMaterial != null
                    ? decal.decalMaterial.name : "(no material)";

                EditorGUI.LabelField(
                    new Rect(rect.x, rect.y + 2, infoW, EditorGUIUtility.singleLineHeight),
                    goName, matName);

                if (GUI.Button(
                    new Rect(rect.x + infoW + 4, rect.y + 2, removeW, EditorGUIUtility.singleLineHeight),
                    "X"))
                {
                    _decals.RemoveAt(index);
                    Repaint();
                }
            };

            _reorderableList.elementHeight = EditorGUIUtility.singleLineHeight + 4f;
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawDecalListSection();
            EditorGUILayout.Space(8);
            DrawAtlasSection();
            EditorGUILayout.Space(8);
            DrawOutputSection();
            EditorGUILayout.Space(8);

            RefreshValidation();
            DrawValidationSection();
            EditorGUILayout.Space(8);

            DrawBakeButton();

            if (!string.IsNullOrEmpty(_resultMeshPath))
            {
                EditorGUILayout.Space(8);
                DrawResultSection();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawDecalListSection()
        {
            EditorGUILayout.LabelField("Decals to Bake", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Scene Selection")) PopulateFromSelection();
                if (GUILayout.Button("Clear")) _decals.Clear();
            }

            if (_decals.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Select GameObjects with GroundDecalProjector in the scene, then click 'Use Scene Selection'.",
                    MessageType.Warning);
            }
            else
            {
                _reorderableList.DoLayoutList();
            }

            EditorGUILayout.LabelField($"{_decals.Count} decal(s) selected", EditorStyles.miniLabel);
        }

        private void PopulateFromSelection()
        {
            _decals.Clear();
            foreach (var go in Selection.gameObjects)
            {
                var projectors = go.GetComponentsInChildren<GroundDecalProjector>(includeInactive: true);
                foreach (var p in projectors)
                {
                    if (!_decals.Contains(p))
                        _decals.Add(p);
                }
            }
            Repaint();
        }

        private void DrawAtlasSection()
        {
            EditorGUILayout.LabelField("Atlas Settings", EditorStyles.boldLabel);

            _maxAtlasSizeIndex = EditorGUILayout.Popup("Max Atlas Size", _maxAtlasSizeIndex, AtlasSizeLabels);
            _atlasFilterMode   = (FilterMode)EditorGUILayout.EnumPopup("Atlas Filter Mode", _atlasFilterMode);
            _padding           = Mathf.Max(0, EditorGUILayout.IntField("Padding (px)", _padding));
        }

        private void DrawOutputSection()
        {
            EditorGUILayout.LabelField("Output Settings", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                _outputFolder = EditorGUILayout.TextField("Output Folder", _outputFolder);
                if (GUILayout.Button("Browse", GUILayout.Width(60)))
                {
                    string chosen = EditorUtility.OpenFolderPanel("Select Output Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(chosen))
                    {
                        if (chosen.StartsWith(Application.dataPath))
                            chosen = "Assets" + chosen.Substring(Application.dataPath.Length);
                        _outputFolder = chosen;
                    }
                }
            }

            _assetName = EditorGUILayout.TextField("Asset Name", _assetName);
            _gameObjectName = EditorGUILayout.TextField("GameObject Name", _gameObjectName);
            _disableOriginalsAfterBake = EditorGUILayout.Toggle("Disable Originals After Bake", _disableOriginalsAfterBake);
        }

        private void RefreshValidation()
        {
            _errors.Clear();
            _warnings.Clear();

            if (_decals.Count == 0)
            {
                _errors.Add("No decals in the list.");
                return;
            }

            var nullMat = _decals.Where(d => d != null && d.decalMaterial == null).ToList();
            if (nullMat.Count > 0)
                _errors.Add($"{nullMat.Count} decal(s) have a null decalMaterial.");

            var validDecals  = _decals.Where(d => d != null && d.decalMaterial != null).ToList();
            var shaderGroups = validDecals.GroupBy(d => d.decalMaterial.shader.name).ToList();
            if (shaderGroups.Count > 1)
                _errors.Add($"Decals use {shaderGroups.Count} different shaders. Only the most common shader group will be baked.");

            foreach (var d in validDecals)
            {
                var tex = d.decalMaterial.GetTexture("_MainTex") as Texture2D;
                if (tex == null) continue;
                string path = AssetDatabase.GetAssetPath(tex);
                if (string.IsNullOrEmpty(path)) continue;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null && !importer.isReadable)
                    _warnings.Add($"Texture '{tex.name}' is not Read/Write enabled (will be fixed automatically).");
            }

            if (!AssetDatabase.IsValidFolder(_outputFolder))
                _warnings.Add($"Output folder '{_outputFolder}' does not exist and will be created.");

            int maxAtlas = AtlasSizeOptions[_maxAtlasSizeIndex];
            if (validDecals.Count > 16 && maxAtlas < 4096)
                _warnings.Add("Large number of decals may exceed atlas capacity. Consider increasing Max Atlas Size.");
        }

        private void DrawValidationSection()
        {
            if (_errors.Count == 0 && _warnings.Count == 0) return;

            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);

            foreach (var err in _errors)
                EditorGUILayout.HelpBox(err, MessageType.Error);

            foreach (var warn in _warnings)
                EditorGUILayout.HelpBox(warn, MessageType.Warning);
        }

        private void DrawBakeButton()
        {
            bool realErrors = _decals.Count == 0
                || _decals.Any(d => d != null && d.decalMaterial == null);

            using (new EditorGUI.DisabledScope(realErrors))
            {
                if (GUILayout.Button("Bake Decals", GUILayout.Height(36)))
                    ExecuteBake();
            }
        }

        private void DrawResultSection()
        {
            EditorGUILayout.LabelField("Result", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Mesh:", _resultMeshPath, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Material:", _resultMatPath, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Atlas:", _resultAtlasPath, EditorStyles.miniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Ping Mesh Asset"))
                {
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(_resultMeshPath);
                    if (mesh != null) EditorGUIUtility.PingObject(mesh);
                }

                if (GUILayout.Button("Select Baked Object") && _resultGameObject != null)
                    Selection.activeGameObject = _resultGameObject;
            }
        }

        private void ExecuteBake()
        {
            _resultMeshPath = null;
            _resultMatPath = null;
            _resultAtlasPath = null;
            _resultGameObject = null;

            bool assetEditingStarted = false;

            try
            {
                EditorUtility.DisplayProgressBar("Baking Decals", "Validating decals...", 0.05f);
                var (validDecals, shaderName, skippedCount) = ValidateAndGroupDecals();

                EditorUtility.DisplayProgressBar("Baking Decals", "Ensuring textures are readable...", 0.15f);
                var textures = EnsureTexturesReadable(validDecals);

                EditorUtility.DisplayProgressBar("Baking Decals", "Building texture atlas...", 0.30f);
                EnsureOutputFolder();

                AssetDatabase.StartAssetEditing();
                assetEditingStarted = true;

                string atlasPath = $"{_outputFolder}/{_assetName}_Atlas.png";
                DeleteAssetIfExists(atlasPath);
                var (atlas, atlasRects) = BuildAtlas(textures, atlasPath);

                AssetDatabase.StopAssetEditing();
                assetEditingStarted = false;

                AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceUpdate);
                MakeAtlasReadable(atlasPath, AtlasSizeOptions[_maxAtlasSizeIndex]);
                var atlasAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);

                EditorUtility.DisplayProgressBar("Baking Decals", "Building combined mesh...", 0.55f);
                string meshPath = $"{_outputFolder}/{_assetName}.asset";
                DeleteAssetIfExists(meshPath);
                var bakedMesh = BuildCombinedMesh(validDecals, textures, atlasRects, meshPath);

                EditorUtility.DisplayProgressBar("Baking Decals", "Creating baked material...", 0.70f);
                string matPath = $"{_outputFolder}/{_assetName}_Mat.mat";
                DeleteAssetIfExists(matPath);
                var bakedMat = CreateBakedMaterial(atlasAsset ?? atlas, validDecals, shaderName, matPath);

                EditorUtility.DisplayProgressBar("Baking Decals", "Creating baked GameObject...", 0.85f);
                _resultGameObject = CreateBakedGameObject(bakedMesh, bakedMat);

                EditorUtility.DisplayProgressBar("Baking Decals", "Disabling originals...", 0.92f);
                DisableOriginals(validDecals);

                EditorUtility.DisplayProgressBar("Baking Decals", "Finalizing assets...", 0.98f);
                FinalizeAssets(meshPath, matPath, atlasPath);

                string msg = $"Bake complete!\n" +
                             $"Decals baked: {validDecals.Count}\n" +
                             $"Mesh: {meshPath}\n" +
                             $"Material: {matPath}\n" +
                             $"Atlas: {atlasPath}";

                if (skippedCount > 0)
                    msg += $"\n\nWarning: {skippedCount} decal(s) skipped due to shader mismatch.";

                EditorUtility.DisplayDialog("Bake Successful", msg, "OK");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GroundDecalBaker] {e}");
                EditorUtility.DisplayDialog("Bake Failed", e.Message, "OK");
            }
            finally
            {
                if (assetEditingStarted)
                    AssetDatabase.StopAssetEditing();

                EditorUtility.ClearProgressBar();
            }
        }

        private (List<GroundDecalProjector> decals, string shaderName, int skipped) ValidateAndGroupDecals()
        {
            var withMaterial = _decals.Where(d => d != null && d.decalMaterial != null).ToList();

            if (withMaterial.Count == 0)
                throw new System.Exception("No decals with valid materials found.");

            var groups = withMaterial
                .GroupBy(d => d.decalMaterial.shader.name)
                .OrderByDescending(g => g.Count())
                .ToList();

            string dominant = groups[0].Key;
            var dominantDecals = groups[0].ToList();
            int skipped = withMaterial.Count - dominantDecals.Count;

            if (groups.Count > 1)
                Debug.LogWarning($"[GroundDecalBaker] Multiple shaders detected. Using '{dominant}' ({dominantDecals.Count} decals). {skipped} decal(s) skipped.");

            return (dominantDecals, dominant, skipped);
        }

        private List<Texture2D> EnsureTexturesReadable(List<GroundDecalProjector> decals)
        {
            Texture2D whiteFallback = null;
            var result = new List<Texture2D>(decals.Count);

            foreach (var decal in decals)
            {
                var tex = decal.decalMaterial.GetTexture("_MainTex") as Texture2D;

                if (tex == null)
                {
                    if (whiteFallback == null)
                    {
                        whiteFallback = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                        var pixels = new Color32[16];
                        for (int i = 0; i < 16; i++) pixels[i] = new Color32(255, 255, 255, 255);
                        whiteFallback.SetPixels32(pixels);
                        whiteFallback.Apply();
                    }

                    result.Add(whiteFallback);
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(tex);
                if (!string.IsNullOrEmpty(path))
                {
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer != null && !importer.isReadable)
                    {
                        importer.isReadable = true;
                        importer.SaveAndReimport();
                    }
                }

                result.Add(tex);
            }

            return result;
        }

        private (Texture2D atlas, Dictionary<Texture2D, Rect> atlasRects) BuildAtlas(
            List<Texture2D> textures, string atlasPath)
        {
            int maxSize = AtlasSizeOptions[_maxAtlasSizeIndex];

            var unique = new List<Texture2D>();
            var uniqueSet = new HashSet<Texture2D>();
            foreach (var t in textures)
            {
                if (uniqueSet.Add(t))
                    unique.Add(t);
            }

            var atlas = new Texture2D(maxSize, maxSize, TextureFormat.RGBA32, true);
            Rect[] rects = atlas.PackTextures(unique.ToArray(), _padding, maxSize, false);

            var atlasRects = new Dictionary<Texture2D, Rect>(unique.Count);
            for (int i = 0; i < unique.Count; i++)
                atlasRects[unique[i]] = rects[i];

            atlas.filterMode = _atlasFilterMode;
            atlas.Apply();

            File.WriteAllBytes(atlasPath, atlas.EncodeToPNG());

            return (atlas, atlasRects);
        }

        private static void MakeAtlasReadable(string atlasPath, int maxTextureSize)
        {
            var importer = AssetImporter.GetAtPath(atlasPath) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = maxTextureSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.crunchedCompression = true;
            importer.compressionQuality = (int)TextureCompressionQuality.Normal;
            importer.npotScale = TextureImporterNPOTScale.None;

            TextureImporterPlatformSettings defaultSettings = new TextureImporterPlatformSettings();
            defaultSettings.name = "DefaultTexturePlatform";
            defaultSettings.overridden = false;
            defaultSettings.maxTextureSize = maxTextureSize;
            defaultSettings.textureCompression = TextureImporterCompression.Compressed;
            defaultSettings.crunchedCompression = true;
            defaultSettings.compressionQuality = (int)TextureCompressionQuality.Normal;
            importer.SetPlatformTextureSettings(defaultSettings);

            importer.SaveAndReimport();
        }

        private Mesh BuildCombinedMesh(
            List<GroundDecalProjector> decals,
            List<Texture2D> textures,
            Dictionary<Texture2D, Rect> atlasRects,
            string meshPath)
        {
            int totalVerts = decals.Count * 4;
            var vertices  = new Vector3[totalVerts];
            var uvs       = new Vector2[totalVerts];
            var colors    = new Color[totalVerts];
            var normals   = new Vector3[totalVerts];
            var triangles = new int[decals.Count * 6];

            for (int i = 0; i < decals.Count; i++)
            {
                var decal = decals[i];
                var tex   = textures[i];

                atlasRects.TryGetValue(tex, out Rect atlasRegion);

                float halfW = decal.width  * 0.5f;
                float halfH = decal.height * 0.5f;
                float yOff  = decal.yOffset;

                var localVerts = new Vector3[]
                {
                    new(-halfW, yOff, -halfH),
                    new( halfW, yOff, -halfH),
                    new( halfW, yOff,  halfH),
                    new(-halfW, yOff,  halfH),
                };

                var baseUVs = new Vector2[]
                {
                    new(0f, 0f),
                    new(1f, 0f),
                    new(1f, 1f),
                    new(0f, 1f),
                };

                int baseVert = i * 4;

                for (int v = 0; v < 4; v++)
                {
                    vertices[baseVert + v] = decal.transform.TransformPoint(localVerts[v]);
                    normals [baseVert + v] = Vector3.up;
                    colors  [baseVert + v] = new Color(1f, 1f, 1f, decal.opacity);

                    float u = baseUVs[v].x * decal.tiling.x + decal.offset.x;
                    float vCoord = baseUVs[v].y * decal.tiling.y + decal.offset.y;

                    uvs[baseVert + v] = new Vector2(
                        atlasRegion.x + u * atlasRegion.width,
                        atlasRegion.y + vCoord * atlasRegion.height);
                }

                int baseTri = i * 6;
                triangles[baseTri + 0] = baseVert + 0;
                triangles[baseTri + 1] = baseVert + 2;
                triangles[baseTri + 2] = baseVert + 1;
                triangles[baseTri + 3] = baseVert + 0;
                triangles[baseTri + 4] = baseVert + 3;
                triangles[baseTri + 5] = baseVert + 2;
            }

            var bakedMesh = new Mesh { name = _assetName };

            if (totalVerts > 65535)
                bakedMesh.indexFormat = IndexFormat.UInt32;

            bakedMesh.vertices  = vertices;
            bakedMesh.uv        = uvs;
            bakedMesh.colors    = colors;
            bakedMesh.normals   = normals;
            bakedMesh.triangles = triangles;
            bakedMesh.RecalculateBounds();

            AssetDatabase.CreateAsset(bakedMesh, meshPath);
            return bakedMesh;
        }

        private Material CreateBakedMaterial(
            Texture2D atlas,
            List<GroundDecalProjector> decals,
            string shaderName,
            string matPath)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
                throw new System.Exception($"Shader '{shaderName}' not found. Make sure it is included in the build.");

            var bakedMat = new Material(decals[0].decalMaterial) { name = _assetName };

            bakedMat.SetTexture("_MainTex", atlas);
            bakedMat.SetVector("_MainTex_ST", new Vector4(1f, 1f, 0f, 0f));
            float avgOpacity = decals.Average(d => d.opacity);
            if (bakedMat.HasProperty("_Blend"))
                bakedMat.SetFloat("_Blend", avgOpacity);

            float avgDraw = decals.Average(d => d.drawDistance);
            float avgFade = decals.Average(d => d.startFade);
            if (bakedMat.HasProperty("_DrawDistance"))
                bakedMat.SetFloat("_DrawDistance", avgDraw);
            if (bakedMat.HasProperty("_StartFade"))
                bakedMat.SetFloat("_StartFade", avgFade);

            AssetDatabase.CreateAsset(bakedMat, matPath);
            return bakedMat;
        }

        private GameObject CreateBakedGameObject(Mesh mesh, Material material)
        {
            var go = new GameObject(_gameObjectName);

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            mr.allowOcclusionWhenDynamic = false;

            go.transform.position = Vector3.zero;

            Undo.RegisterCreatedObjectUndo(go, "Bake Ground Decals");
            return go;
        }

        private void DisableOriginals(List<GroundDecalProjector> decals)
        {
            if (!_disableOriginalsAfterBake) return;

            foreach (var decal in decals)
            {
                if (decal == null) continue;
                Undo.RecordObject(decal.gameObject, "Bake Ground Decals");
                decal.gameObject.SetActive(false);
            }
        }

        private void FinalizeAssets(string meshPath, string matPath, string atlasPath)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _resultMeshPath  = meshPath;
            _resultMatPath   = matPath;
            _resultAtlasPath = atlasPath;
        }

        private void EnsureOutputFolder()
        {
            if (AssetDatabase.IsValidFolder(_outputFolder)) return;

            string[] parts = _outputFolder.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void DeleteAssetIfExists(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return;

            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                AssetDatabase.DeleteAsset(assetPath);
        }
    }
}
