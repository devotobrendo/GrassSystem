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
        /// Optional: The grass settings that were used when this data was created.
        /// </summary>
        [SerializeField]
        private SO_GrassSettings associatedSettings;
        
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
        /// Gets or sets the associated grass settings.
        /// </summary>
        public SO_GrassSettings AssociatedSettings
        {
            get => associatedSettings;
            set => associatedSettings = value;
        }
        
        /// <summary>
        /// Saves grass data from a GrassRenderer to this asset.
        /// </summary>
        /// <param name="data">The grass data list to save.</param>
        /// <param name="sceneName">Optional scene name for metadata.</param>
        /// <param name="settings">Optional settings reference for proper color restoration.</param>
        public void SaveData(List<GrassData> data, string sceneName = null, SO_GrassSettings settings = null)
        {
            if (data == null)
            {
                grassInstances = new List<GrassData>();
            }
            else
            {
                // List<T>(IEnumerable<T>) uses Array.Copy internally for List sources,
                // which is a fast memcpy for value types like GrassData (struct).
                // ~10x faster than manual loop for large collections (50k+ instances).
                grassInstances = new List<GrassData>(data);
            }
            
            sourceScene = sceneName ?? "Unknown";
            lastSaveTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            
            // Save associated settings for proper restoration
            if (settings != null)
            {
                associatedSettings = settings;
            }
            
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
            
            // Return a value copy via List constructor (uses Array.Copy internally)
            return new List<GrassData>(grassInstances);
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
        
#if UNITY_EDITOR
        /// <summary>
        /// Creates a JSON backup file alongside the asset for crash recovery.
        /// </summary>
        public void CreateBackup()
        {
            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(this);
            if (string.IsNullOrEmpty(assetPath)) return;
            
            string backupPath = assetPath.Replace(".asset", "_backup.json");
            
            try
            {
                var backupData = new GrassBackupData
                {
                    instanceCount = grassInstances?.Count ?? 0,
                    sourceScene = sourceScene,
                    lastSaveTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    settingsName = associatedSettings != null ? associatedSettings.name : "",
                    instances = new List<GrassDataSerializable>()
                };
                
                if (grassInstances != null)
                {
                    foreach (var g in grassInstances)
                    {
                        backupData.instances.Add(new GrassDataSerializable
                        {
                            px = g.position.x, py = g.position.y, pz = g.position.z,
                            nx = g.normal.x, ny = g.normal.y, nz = g.normal.z,
                            w = g.widthHeight.x, h = g.widthHeight.y,
                            cx = g.color.x, cy = g.color.y, cz = g.color.z,
                            pattern = g.patternMask
                        });
                    }
                }
                
                string json = JsonUtility.ToJson(backupData);
                System.IO.File.WriteAllText(backupPath, json);
                Debug.Log($"GrassDataAsset: Backup saved to {backupPath} ({backupData.instanceCount:N0} instances)");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"GrassDataAsset: Failed to create backup: {e.Message}");
            }
        }
        
        /// <summary>
        /// Attempts to restore data from a JSON backup file.
        /// </summary>
        /// <returns>True if restore was successful.</returns>
        public bool RestoreFromBackup()
        {
            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(this);
            if (string.IsNullOrEmpty(assetPath)) return false;
            
            string backupPath = assetPath.Replace(".asset", "_backup.json");
            
            if (!System.IO.File.Exists(backupPath))
            {
                Debug.LogWarning($"GrassDataAsset: No backup file found at {backupPath}");
                return false;
            }
            
            try
            {
                string json = System.IO.File.ReadAllText(backupPath);
                var backupData = JsonUtility.FromJson<GrassBackupData>(json);
                
                if (backupData.instances == null || backupData.instances.Count == 0)
                {
                    Debug.LogWarning("GrassDataAsset: Backup file is empty or corrupted");
                    return false;
                }
                
                grassInstances = new List<GrassData>(backupData.instances.Count);
                foreach (var g in backupData.instances)
                {
                    grassInstances.Add(new GrassData(
                        new Vector3(g.px, g.py, g.pz),
                        new Vector3(g.nx, g.ny, g.nz),
                        g.w, g.h,
                        new Color(g.cx, g.cy, g.cz),
                        g.pattern
                    ));
                }
                
                sourceScene = backupData.sourceScene;
                lastSaveTime = backupData.lastSaveTime;
                
                UnityEditor.EditorUtility.SetDirty(this);
                Debug.Log($"GrassDataAsset: Restored {grassInstances.Count:N0} instances from backup");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"GrassDataAsset: Failed to restore from backup: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Checks if a backup file exists for this asset.
        /// </summary>
        public bool HasBackup()
        {
            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(this);
            if (string.IsNullOrEmpty(assetPath)) return false;
            
            string backupPath = assetPath.Replace(".asset", "_backup.json");
            return System.IO.File.Exists(backupPath);
        }
        
        /// <summary>
        /// Gets info about the backup file if it exists.
        /// </summary>
        public string GetBackupInfo()
        {
            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(this);
            if (string.IsNullOrEmpty(assetPath)) return "No backup";
            
            string backupPath = assetPath.Replace(".asset", "_backup.json");
            if (!System.IO.File.Exists(backupPath)) return "No backup";
            
            try
            {
                var fileInfo = new System.IO.FileInfo(backupPath);
                return $"Backup: {fileInfo.LastWriteTime:HH:mm:ss}";
            }
            catch
            {
                return "Backup exists";
            }
        }
#endif
    }
    
#if UNITY_EDITOR
    /// <summary>
    /// Serializable backup data structure for JSON persistence.
    /// </summary>
    [System.Serializable]
    public class GrassBackupData
    {
        public int instanceCount;
        public string sourceScene;
        public string lastSaveTime;
        public string settingsName;
        public List<GrassDataSerializable> instances;
    }
    
    /// <summary>
    /// Flat serializable structure for grass data (no Vector3/Color).
    /// </summary>
    [System.Serializable]
    public class GrassDataSerializable
    {
        public float px, py, pz;
        public float nx, ny, nz;
        public float w, h;
        public float cx, cy, cz;
        public float pattern;
    }
#endif
}
