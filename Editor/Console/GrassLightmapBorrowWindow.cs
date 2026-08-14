// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using UnityEditor;
using UnityEngine;

namespace GrassSystem.Consoles.Editor
{
    public class GrassLightmapBorrowWindow : EditorWindow
    {
        private Renderer donor;
        private Renderer target;
        private string status = string.Empty;
        private MessageType statusType = MessageType.None;

        private static readonly GUIContent DonorLabel = new GUIContent("Donor", "A renderer that already carries a baked lightmap and covers the same ground.");
        private static readonly GUIContent TargetLabel = new GUIContent("Target", "The renderer that should read the donor's lightmap region.");
        private static readonly GUIContent FindLabel = new GUIContent("Find donor", "Picks the lightmapped renderer whose bounds overlap the target the most.");
        private static readonly GUIContent CopyLabel = new GUIContent("Copy donor to target", "Writes the donor's lightmap index and tiling onto the target. Undoable, no bake.");
        private static readonly GUIContent ClearLabel = new GUIContent("Clear target", "Puts the target back to no lightmap.");

        [MenuItem("Tools/Grass System/Borrow Lightmap", priority = 40)]
        private static void Open()
        {
            GrassLightmapBorrowWindow window = GetWindow<GrassLightmapBorrowWindow>("Borrow Lightmap");
            window.minSize = new Vector2(460, 320);
            window.TakeSelectionAsTarget();
        }

        private void OnSelectionChange()
        {
            TakeSelectionAsTarget();
            Repaint();
        }

        private void TakeSelectionAsTarget()
        {
            if (Selection.activeGameObject == null) return;

            var selected = Selection.activeGameObject.GetComponent<Renderer>();
            if (selected != null && selected != donor)
                target = selected;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Borrow Lightmap", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Copies a baked lightmap assignment from one renderer to another. Nothing is re-baked - only the two fields that tell a renderer which atlas region to read.", EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space();

            donor = (Renderer)EditorGUILayout.ObjectField(DonorLabel, donor, typeof(Renderer), true);
            DrawRendererState(donor);

            EditorGUILayout.Space();

            target = (Renderer)EditorGUILayout.ObjectField(TargetLabel, target, typeof(Renderer), true);
            DrawRendererState(target);

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(target == null))
                {
                    if (GUILayout.Button(FindLabel, GUILayout.Width(110)))
                        FindDonorForTarget();
                }

                using (new EditorGUI.DisabledScope(donor == null || target == null || donor == target || donor.lightmapIndex < 0))
                {
                    if (GUILayout.Button(CopyLabel, GUILayout.Height(22)))
                        CopyLightmap();
                }

                using (new EditorGUI.DisabledScope(target == null))
                {
                    if (GUILayout.Button(ClearLabel, GUILayout.Width(100)))
                        ClearTarget();
                }
            }

            if (!string.IsNullOrEmpty(status))
                EditorGUILayout.HelpBox(status, statusType);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "This is exact only when both renderers map their lightmap UVs the same way - same mesh and same transform is the safe case. " +
                "A different mesh will read a region that does not line up, and you will see someone else's shadows on your ground. " +
                "Check it in the Scene view right after copying; Undo puts it back.",
                MessageType.Warning);
        }

        private void DrawRendererState(Renderer renderer)
        {
            if (renderer == null)
            {
                EditorGUILayout.LabelField("   -", EditorStyles.miniLabel);
                return;
            }

            EditorGUI.indentLevel++;
            if (renderer.lightmapIndex < 0 || renderer.lightmapIndex == 65534 || renderer.lightmapIndex == 65535)
            {
                EditorGUILayout.LabelField("Lightmap", "none", EditorStyles.miniLabel);
            }
            else
            {
                Vector4 st = renderer.lightmapScaleOffset;
                EditorGUILayout.LabelField("Lightmap", $"index {renderer.lightmapIndex}   tiling ({st.x:0.###}, {st.y:0.###})   offset ({st.z:0.###}, {st.w:0.###})", EditorStyles.miniLabel);
            }
            EditorGUI.indentLevel--;
        }

        private void FindDonorForTarget()
        {
            Bounds targetBounds = target.bounds;
            Renderer best = null;
            float bestVolume = 0f;

            Renderer[] all = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                Renderer candidate = all[i];
                if (candidate == null || candidate == target) continue;
                if (candidate.lightmapIndex < 0 || candidate.lightmapIndex >= 65534) continue;
                if (!candidate.bounds.Intersects(targetBounds)) continue;

                float volume = IntersectionVolume(candidate.bounds, targetBounds);
                if (volume > bestVolume)
                {
                    bestVolume = volume;
                    best = candidate;
                }
            }

            if (best == null)
            {
                status = "No lightmapped renderer overlaps the target. Nothing in this scene has a region to lend.";
                statusType = MessageType.Warning;
                return;
            }

            donor = best;
            status = $"Picked '{best.name}' - it overlaps the target the most among lightmapped renderers. Confirm it actually covers the same ground before copying.";
            statusType = MessageType.Info;
        }

        private static float IntersectionVolume(Bounds a, Bounds b)
        {
            Vector3 min = Vector3.Max(a.min, b.min);
            Vector3 max = Vector3.Min(a.max, b.max);
            Vector3 size = Vector3.Max(max - min, Vector3.zero);
            return size.x * size.y * size.z;
        }

        private void CopyLightmap()
        {
            var so = new SerializedObject(target);
            SerializedProperty index = so.FindProperty("m_LightmapIndex");
            SerializedProperty tiling = so.FindProperty("m_LightmapTilingOffset");

            if (index == null || tiling == null)
            {
                status = "This renderer does not expose m_LightmapIndex / m_LightmapTilingOffset.";
                statusType = MessageType.Error;
                return;
            }

            Undo.RecordObject(target, "Borrow Lightmap");
            index.intValue = donor.lightmapIndex;
            tiling.vector4Value = donor.lightmapScaleOffset;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);

            status = $"'{target.name}' now reads index {donor.lightmapIndex} with the same tiling as '{donor.name}'. Save the scene to keep it. If the shading looks like it belongs to another object, Undo.";
            statusType = MessageType.Info;
            SceneView.RepaintAll();
        }

        private void ClearTarget()
        {
            var so = new SerializedObject(target);
            SerializedProperty index = so.FindProperty("m_LightmapIndex");
            if (index == null) return;

            Undo.RecordObject(target, "Clear Borrowed Lightmap");
            index.intValue = -1;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);

            status = $"'{target.name}' is back to no lightmap - it falls back to light probes.";
            statusType = MessageType.Info;
            SceneView.RepaintAll();
        }
    }
}
