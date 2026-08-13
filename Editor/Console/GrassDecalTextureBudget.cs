// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using GrassSystem;

namespace GrassSystem.Consoles.Editor
{
    public class DecalTextureEntry
    {
        public string owner;
        public string texturePath;
        public Texture2D texture;
        public int sourceSize;
        public int switchSizeNow;
        public int switchSizeAfter;
        public bool hasOverride;

        public float MegabytesNow => Megabytes(switchSizeNow);
        public float MegabytesAfter => Megabytes(switchSizeAfter);
        public bool Changes => switchSizeAfter != switchSizeNow || !hasOverride;

        public static float Megabytes(int size) => size * (long)size / (1024f * 1024f);
    }

    public class DecalTextureBudget
    {
        public readonly List<DecalTextureEntry> entries = new List<DecalTextureEntry>();
        public int missingOverride;
        public float megabytesNow;
        public float megabytesAfter;

        public int ChangeCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < entries.Count; i++)
                    if (entries[i].Changes) n++;
                return n;
            }
        }
    }

    public static class GrassDecalTextureBudget
    {
        public static DecalTextureBudget Scan(int desiredSwitchMax)
        {
            var budget = new DecalTextureBudget();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string[] guids = AssetDatabase.FindAssets("t:GrassDecalBakeAsset");
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                var bake = AssetDatabase.LoadAssetAtPath<GrassDecalBakeAsset>(assetPath);
                if (bake == null) continue;

                string owner = Path.GetFileNameWithoutExtension(assetPath);
                AddMap(budget, seen, owner, bake.overrideMap, desiredSwitchMax);
                AddMap(budget, seen, owner, bake.multiplyMap, desiredSwitchMax);
                AddMap(budget, seen, owner, bake.additiveMap, desiredSwitchMax);
            }

            budget.entries.Sort((a, b) => b.MegabytesNow.CompareTo(a.MegabytesNow));
            return budget;
        }

        public static int Apply(DecalTextureBudget budget, int switchMax)
        {
            if (budget == null) return 0;

            int applied = 0;
            try
            {
                AssetDatabase.StartAssetEditing();

                for (int i = 0; i < budget.entries.Count; i++)
                {
                    DecalTextureEntry entry = budget.entries[i];
                    if (!entry.Changes) continue;

                    var importer = AssetImporter.GetAtPath(entry.texturePath) as TextureImporter;
                    if (importer == null) continue;

                    foreach (string platform in GrassDecalBakeService.SwitchPlatformNames)
                    {
                        TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
                        settings.name = platform;
                        settings.overridden = true;
                        settings.maxTextureSize = switchMax;
                        settings.textureCompression = TextureImporterCompression.Compressed;
                        settings.format = TextureImporterFormat.Automatic;
                        importer.SetPlatformTextureSettings(settings);
                    }

                    importer.SaveAndReimport();
                    applied++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            return applied;
        }

        private static void AddMap(DecalTextureBudget budget, HashSet<string> seen, string owner, Texture2D map, int desiredSwitchMax)
        {
            if (map == null) return;

            string path = AssetDatabase.GetAssetPath(map);
            if (string.IsNullOrEmpty(path) || !seen.Add(path)) return;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            int sourceSize = Mathf.Max(map.width, map.height);
            int defaultMax = importer.maxTextureSize;

            int switchMaxNow = defaultMax;
            bool hasOverride = false;
            foreach (string platform in GrassDecalBakeService.SwitchPlatformNames)
            {
                TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
                if (settings != null && settings.overridden)
                {
                    switchMaxNow = settings.maxTextureSize;
                    hasOverride = true;
                    break;
                }
            }

            var entry = new DecalTextureEntry
            {
                owner = owner,
                texturePath = path,
                texture = map,
                sourceSize = sourceSize,
                switchSizeNow = Mathf.Min(sourceSize, switchMaxNow),
                switchSizeAfter = Mathf.Min(sourceSize, desiredSwitchMax),
                hasOverride = hasOverride,
            };

            budget.entries.Add(entry);
            budget.megabytesNow += entry.MegabytesNow;
            budget.megabytesAfter += entry.MegabytesAfter;
            if (!hasOverride) budget.missingOverride++;
        }
    }
}
