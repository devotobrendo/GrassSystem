// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using UnityEngine;

namespace GrassSystem.Consoles
{
    public enum PlatformVariant { Auto, ForceFull, ForceSwitch }

    [CreateAssetMenu(fileName = "GrassProfileSet", menuName = "Grass System/Platform Profile Set")]
    public class GrassPlatformProfileSet : ScriptableObject
    {
        public GrassPlatformProfile fullProfile;
        public GrassPlatformProfile switchProfile;

        public GrassPlatformProfile Resolve(PlatformVariant mode)
        {
            switch (mode)
            {
                case PlatformVariant.ForceFull: return fullProfile;
                case PlatformVariant.ForceSwitch: return switchProfile;
                default:
                    return Application.platform == RuntimePlatform.Switch ? switchProfile : fullProfile;
            }
        }
    }
}
