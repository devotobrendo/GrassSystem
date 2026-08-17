// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using UnityEditor;
using UnityEngine;
using GrassSystem.Consoles;

namespace GrassSystem.Consoles.Editor
{
    public class GrassDeviceTuningWindow : EditorWindow
    {
        public const string MenuPath = "Tools/Grass System/Device Tuning";

        private string tuningJson = string.Empty;
        private GrassPlatformProfile tuningTarget;
        private string tuningStatus = string.Empty;
        private MessageType tuningStatusType = MessageType.None;
        private Vector2 tuningScrollPos;

        private static readonly GUIContent TargetLabel = new GUIContent("Target Profile", "The profile that receives the values from the dump - normally the Switch profile of the scene you were testing.");
        private static readonly GUIContent PasteLabel = new GUIContent("Paste From Clipboard", "Reads whatever you copied from the devkit log into the box above.");
        private static readonly GUIContent ApplyLabel = new GUIContent("Apply To Profile", "Shows what would change, then writes the dump onto the profile. Undoable.");

        [MenuItem(MenuPath, priority = 50)]
        public static void Open()
        {
            GrassDeviceTuningWindow window = GetWindow<GrassDeviceTuningWindow>("Device Tuning");
            window.minSize = new Vector2(420, 300);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Device Tuning", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "For values tuned on a real devkit. Run the game on the console, adjust the grass with the in-game overlay, press its dump button, then paste the [GrassTuning] line the log prints here.",
                EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space();

            tuningTarget = (GrassPlatformProfile)EditorGUILayout.ObjectField(TargetLabel, tuningTarget, typeof(GrassPlatformProfile), false);

            EditorGUILayout.Space();

            tuningScrollPos = EditorGUILayout.BeginScrollView(tuningScrollPos, GUILayout.ExpandHeight(true));
            tuningJson = EditorGUILayout.TextArea(tuningJson, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(PasteLabel, GUILayout.Width(160)))
                {
                    tuningJson = EditorGUIUtility.systemCopyBuffer;
                    ClearStatus();
                    GUI.FocusControl(null);
                }

                using (new EditorGUI.DisabledScope(tuningTarget == null || string.IsNullOrWhiteSpace(tuningJson)))
                {
                    if (GUILayout.Button(ApplyLabel, GUILayout.Width(140)))
                        ApplyToProfile();
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Clear", GUILayout.Width(70)))
                {
                    tuningJson = string.Empty;
                    ClearStatus();
                    GUI.FocusControl(null);
                }
            }

            if (!string.IsNullOrEmpty(tuningStatus))
                EditorGUILayout.HelpBox(tuningStatus, tuningStatusType);
        }

        private void ClearStatus()
        {
            tuningStatus = string.Empty;
            tuningStatusType = MessageType.None;
        }

        private void ApplyToProfile()
        {
            string payload = ExtractTuningJson(tuningJson);

            if (!GrassTuningSnapshot.TryParse(payload, out GrassTuningSnapshot snapshot, out string error))
            {
                tuningStatus = error;
                tuningStatusType = MessageType.Error;
                return;
            }

            string diff = snapshot.DescribeDiff(tuningTarget);
            string untouched = snapshot.DescribeUntouched();

            string confirm = $"Apply this dump to {tuningTarget.name}?\n\n{diff}";
            if (!string.IsNullOrEmpty(untouched))
                confirm += $"\n\n{untouched}";

            if (!EditorUtility.DisplayDialog("Apply Grass Tuning", confirm, "Apply", "Cancel"))
                return;

            Undo.RecordObject(tuningTarget, "Apply Grass Tuning");
            snapshot.ApplyTo(tuningTarget);
            EditorUtility.SetDirty(tuningTarget);
            AssetDatabase.SaveAssets();

            string note = snapshot.DescribeUnapplied();
            if (string.IsNullOrEmpty(note))
            {
                tuningStatus = $"Applied to {tuningTarget.name}.";
                tuningStatusType = MessageType.Info;
            }
            else
            {
                tuningStatus = $"Applied to {tuningTarget.name}.\n{note}";
                tuningStatusType = MessageType.Warning;
            }
        }

        private static string ExtractTuningJson(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;

            int start = raw.IndexOf('{');
            int end = raw.LastIndexOf('}');
            if (start < 0 || end <= start) return raw;

            return raw.Substring(start, end - start + 1);
        }
    }
}
