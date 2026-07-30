// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using System.Collections.Generic;
using UnityEditor;
using GrassSystem;
using GrassSystem.Consoles;

namespace GrassSystem.Consoles.Editor
{
    public static class GrassConsoleDataBakeService
    {
        public static void Bake(List<GrassData> source, string sceneName, string originAssetPath, GrassDataConsoleAsset target)
        {
            var slim = new GrassDataConsole[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                GrassData g = source[i];
                slim[i] = new GrassDataConsole(g.position, g.widthHeight.x, g.widthHeight.y);
            }

            target.SaveData(slim, sceneName, originAssetPath);
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
        }
    }
}
