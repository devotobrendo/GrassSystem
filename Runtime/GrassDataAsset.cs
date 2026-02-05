// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using System.Collections.Generic;
using UnityEngine;

namespace GrassSystem
{
    /// <summary>
    /// ScriptableObject asset that stores grass instance data externally.
    /// This allows grass data to be stored separately from scenes/prefabs,
    /// avoiding persistence issues and reducing scene file sizes.
    /// </summary>
    [CreateAssetMenu(fileName = "GrassData", menuName = "Grass System/Grass Data Asset")]
    public class GrassDataAsset : ScriptableObject
    {
        [SerializeField]
        private List<GrassData> grassInstances = new List<GrassData>();
        
        /// <summary>
        /// Version number for future data migration support.
        /// </summary>
        [SerializeField]
        private int dataVersion = 1;
        
        /// <summary>
        /// Metadata: name of the scene this data was created from.
        /// </summary>
        [SerializeField]
        private string sourceScene;
        
        /// <summary>
        /// Metadata: when the data was last saved.
        /// </summary>
        [SerializeField]
        private string lastSaveTime;
        
        /// <summary>
        /// Gets or sets the list of grass instances.
        /// </summary>
        public List<GrassData> GrassInstances
        {
            get => grassInstances;
            set => grassInstances = value ?? new List<GrassData>();
        }
        
        /// <summary>
        /// Gets the number of grass instances stored.
        /// </summary>
        public int InstanceCount => grassInstances?.Count ?? 0;
        
        /// <summary>
        /// Gets the data version.
        /// </summary>
        public int DataVersion => dataVersion;
        
        /// <summary>
        /// Gets the source scene name.
        /// </summary>
        public string SourceScene => sourceScene;
        
        /// <summary>
        /// Gets the last save time as a formatted string.
        /// </summary>
        public string LastSaveTime => lastSaveTime;
        
        /// <summary>
        /// Saves grass data from a GrassRenderer to this asset.
        /// </summary>
        /// <param name="data">The grass data list to save.</param>
        /// <param name="sceneName">Optional scene name for metadata.</param>
        public void SaveData(List<GrassData> data, string sceneName = null)
        {
            if (data == null)
            {
                grassInstances = new List<GrassData>();
            }
            else
            {
                // Create a deep copy to avoid reference issues
                grassInstances = new List<GrassData>(data.Count);
                for (int i = 0; i < data.Count; i++)
                {
                    grassInstances.Add(data[i]);
                }
            }
            
            sourceScene = sceneName ?? "Unknown";
            lastSaveTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
        
        /// <summary>
        /// Loads grass data from this asset into a list.
        /// Returns a NEW list (deep copy) to avoid reference issues.
        /// </summary>
        public List<GrassData> LoadData()
        {
            if (grassInstances == null || grassInstances.Count == 0)
            {
                return new List<GrassData>();
            }
            
            // Return a deep copy
            var result = new List<GrassData>(grassInstances.Count);
            for (int i = 0; i < grassInstances.Count; i++)
            {
                result.Add(grassInstances[i]);
            }
            return result;
        }
        
        /// <summary>
        /// Clears all grass data from this asset.
        /// </summary>
        public void ClearData()
        {
            grassInstances.Clear();
            lastSaveTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}
