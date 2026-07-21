// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using UnityEngine;

namespace GrassSystem.Consoles
{
    [System.Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct GrassDataConsole
    {
        public Vector3 position;
        public Vector2 widthHeight;

        public static int Stride => sizeof(float) * 5;

        public GrassDataConsole(Vector3 pos, float width, float height)
        {
            position = pos;
            widthHeight = new Vector2(width, height);
        }
    }

    [CreateAssetMenu(fileName = "GrassDataConsole", menuName = "Grass System/Console/Grass Data Console Asset")]
    public class GrassDataConsoleAsset : ScriptableObject
    {
        [SerializeField]
        private GrassDataConsole[] grassInstances = new GrassDataConsole[0];

        [SerializeField]
        private int dataVersion = 1;

        [SerializeField]
        private string sourceScene;

        [SerializeField]
        private string sourceAssetPath;

        [SerializeField]
        private string lastBakeTime;

        public GrassDataConsole[] GrassInstances
        {
            get => grassInstances;
            set => grassInstances = value ?? System.Array.Empty<GrassDataConsole>();
        }

        public int InstanceCount => grassInstances?.Length ?? 0;
        public int DataVersion => dataVersion;
        public string SourceScene => sourceScene;
        public string SourceAssetPath => sourceAssetPath;
        public string LastBakeTime => lastBakeTime;

        public void SaveData(GrassDataConsole[] data, string sceneName = null, string originAssetPath = null)
        {
            grassInstances = data ?? System.Array.Empty<GrassDataConsole>();

            if (sceneName != null)
                sourceScene = sceneName;
            if (originAssetPath != null)
                sourceAssetPath = originAssetPath;

            lastBakeTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        public GrassDataConsole[] LoadData()
        {
            if (grassInstances == null || grassInstances.Length == 0)
                return System.Array.Empty<GrassDataConsole>();

            var copy = new GrassDataConsole[grassInstances.Length];
            System.Array.Copy(grassInstances, copy, grassInstances.Length);
            return copy;
        }

#if UNITY_EDITOR
        public void ClearData()
        {
            grassInstances = System.Array.Empty<GrassDataConsole>();
            lastBakeTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
