// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using System;
using UnityEngine;
using GrassSystem;

namespace GrassSystem.Consoles
{
    [Serializable]
    public class GrassTuningSnapshot
    {
        public const string CurrentSchema = "grass-tuning-v1";

        public string schema;

        public bool overrideThinning;
        public float farKeepFraction = 1f;
        public float thinStartDistance;
        public float thinRampDistance = 5f;
        public float coverageCompensation;
        public Vector2 sizeScale = Vector2.one;

        public bool overrideInstanceDensity;
        public float instanceDensity = 1f;

        public bool overrideDrawDistance;
        public float minFadeDistance = 30f;
        public float maxDrawDistance = 50f;

        public bool overrideMesh;
        public GrassMode meshMode = GrassMode.CustomMesh;
        public GrassProceduralType proceduralType = GrassProceduralType.Blade;

        public bool overrideAlbedo;
        public bool useFlatAlbedo;

        public bool groundBlendOverride;
        public float groundBlend;

        public static GrassTuningSnapshot Capture()
        {
            return new GrassTuningSnapshot
            {
                schema = CurrentSchema,

                overrideThinning = GrassConsoleDebug.OverrideEnabled,
                farKeepFraction = GrassConsoleDebug.FarKeepFraction,
                thinStartDistance = GrassConsoleDebug.ThinStartDistance,
                thinRampDistance = GrassConsoleDebug.ThinRampDistance,
                coverageCompensation = GrassConsoleDebug.CoverageCompensation,
                sizeScale = GrassConsoleDebug.SizeScale,

                overrideInstanceDensity = GrassConsoleDebug.InstanceDensityOverrideEnabled,
                instanceDensity = GrassConsoleDebug.InstanceDensity,

                overrideDrawDistance = GrassConsoleDebug.DrawDistanceOverrideEnabled,
                minFadeDistance = GrassConsoleDebug.MinFadeDistance,
                maxDrawDistance = GrassConsoleDebug.MaxDrawDistance,

                overrideMesh = GrassConsoleDebug.ModeOverrideEnabled || GrassConsoleDebug.BladeTypeOverrideEnabled,
                meshMode = GrassConsoleDebug.ModeOverride,
                proceduralType = GrassConsoleDebug.BladeTypeOverride,

                overrideAlbedo = GrassConsoleDebug.AlbedoOverrideEnabled,
                useFlatAlbedo = GrassConsoleDebug.FlatAlbedoEnabled,

                groundBlendOverride = GrassConsoleDebug.GroundBlendOverrideEnabled,
                groundBlend = GrassConsoleDebug.GroundBlend
            };
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(this, true);
        }

        public static bool TryParse(string json, out GrassTuningSnapshot snapshot, out string error)
        {
            snapshot = null;
            error = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Nothing to parse - the text is empty.";
                return false;
            }

            GrassTuningSnapshot parsed;
            try
            {
                parsed = JsonUtility.FromJson<GrassTuningSnapshot>(json);
            }
            catch (Exception ex)
            {
                error = $"Not valid JSON: {ex.Message}";
                return false;
            }

            if (parsed == null)
            {
                error = "Not valid JSON.";
                return false;
            }

            if (parsed.schema != CurrentSchema)
            {
                error = string.IsNullOrEmpty(parsed.schema)
                    ? $"Missing schema field - expected '{CurrentSchema}'. Paste the whole [GrassTuning] line from the log."
                    : $"Schema is '{parsed.schema}', expected '{CurrentSchema}'.";
                return false;
            }

            snapshot = parsed;
            return true;
        }

        public void ApplyTo(GrassPlatformProfile profile)
        {
            if (profile == null) return;

            profile.overrideThinning = overrideThinning;
            profile.farKeepFraction = Mathf.Clamp01(farKeepFraction);
            profile.thinStartDistance = Mathf.Max(0f, thinStartDistance);
            profile.thinRampDistance = Mathf.Max(0.01f, thinRampDistance);
            profile.coverageCompensation = Mathf.Clamp01(coverageCompensation);
            profile.sizeScale = sizeScale;

            profile.overrideInstanceDensity = overrideInstanceDensity;
            profile.instanceDensity = Mathf.Clamp(instanceDensity, 0.01f, 1f);

            profile.overrideDrawDistance = overrideDrawDistance;
            profile.minFadeDistance = Mathf.Max(0f, minFadeDistance);
            profile.maxDrawDistance = Mathf.Max(profile.minFadeDistance, maxDrawDistance);

            profile.overrideMesh = overrideMesh;
            profile.meshMode = meshMode;
            profile.proceduralType = proceduralType;

            profile.overrideAlbedo = overrideAlbedo;
            profile.useFlatAlbedo = useFlatAlbedo;
        }

        public string DescribeUnapplied()
        {
            if (!groundBlendOverride) return string.Empty;
            return $"Ground Blend {groundBlend:F2} was not applied - it lives on the ground material, not on the profile. Set _GrassBlend on the ground material yourself.";
        }
    }
}
