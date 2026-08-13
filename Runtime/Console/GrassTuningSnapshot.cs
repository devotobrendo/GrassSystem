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

            if (overrideThinning)
            {
                profile.overrideThinning = true;
                profile.farKeepFraction = Mathf.Clamp01(farKeepFraction);
                profile.thinStartDistance = Mathf.Max(0f, thinStartDistance);
                profile.thinRampDistance = Mathf.Max(0.01f, thinRampDistance);
                profile.coverageCompensation = Mathf.Clamp01(coverageCompensation);
                profile.sizeScale = sizeScale;
            }

            if (overrideInstanceDensity)
            {
                profile.overrideInstanceDensity = true;
                profile.instanceDensity = Mathf.Clamp(instanceDensity, 0.01f, 1f);
            }

            if (overrideDrawDistance)
            {
                profile.overrideDrawDistance = true;
                profile.minFadeDistance = Mathf.Max(0f, minFadeDistance);
                profile.maxDrawDistance = Mathf.Max(profile.minFadeDistance, maxDrawDistance);
            }

            if (overrideMesh)
            {
                profile.overrideMesh = true;
                profile.meshMode = meshMode;
                profile.proceduralType = proceduralType;
            }

            if (overrideAlbedo)
            {
                profile.overrideAlbedo = true;
                profile.useFlatAlbedo = useFlatAlbedo;
            }
        }

        public string DescribeDiff(GrassPlatformProfile profile)
        {
            if (profile == null) return string.Empty;

            var lines = new System.Collections.Generic.List<string>();

            if (overrideThinning)
            {
                AddFloat(lines, "Far Keep Fraction", profile.farKeepFraction, Mathf.Clamp01(farKeepFraction));
                AddFloat(lines, "Thin Start Distance", profile.thinStartDistance, Mathf.Max(0f, thinStartDistance));
                AddFloat(lines, "Thin Ramp Distance", profile.thinRampDistance, Mathf.Max(0.01f, thinRampDistance));
                AddFloat(lines, "Coverage Compensation", profile.coverageCompensation, Mathf.Clamp01(coverageCompensation));
                if (profile.sizeScale != sizeScale)
                    lines.Add($"Size Scale  {profile.sizeScale} -> {sizeScale}");
            }

            if (overrideInstanceDensity)
                AddFloat(lines, "Instance Density", profile.instanceDensity, Mathf.Clamp(instanceDensity, 0.01f, 1f));

            if (overrideDrawDistance)
            {
                AddFloat(lines, "Min Fade Distance", profile.minFadeDistance, Mathf.Max(0f, minFadeDistance));
                AddFloat(lines, "Max Draw Distance", profile.maxDrawDistance, Mathf.Max(minFadeDistance, maxDrawDistance));
            }

            if (overrideMesh)
            {
                if (profile.meshMode != meshMode)
                    lines.Add($"Mesh Mode  {profile.meshMode} -> {meshMode}");
                if (profile.proceduralType != proceduralType)
                    lines.Add($"Blade Type  {profile.proceduralType} -> {proceduralType}");
            }

            if (overrideAlbedo && profile.useFlatAlbedo != useFlatAlbedo)
                lines.Add($"Flat Albedo  {profile.useFlatAlbedo} -> {useFlatAlbedo}");

            return lines.Count == 0 ? "No value changes." : string.Join("\n", lines);
        }

        public string DescribeUntouched()
        {
            var groups = new System.Collections.Generic.List<string>();
            if (!overrideThinning) groups.Add("Thinning + Size");
            if (!overrideInstanceDensity) groups.Add("Instance Density");
            if (!overrideDrawDistance) groups.Add("Draw Distance");
            if (!overrideMesh) groups.Add("Mesh");
            if (!overrideAlbedo) groups.Add("Albedo");

            return groups.Count == 0
                ? string.Empty
                : $"The dump carries no override for {string.Join(", ", groups)} - those groups stay exactly as they are on the profile.";
        }

        private static void AddFloat(System.Collections.Generic.List<string> lines, string label, float current, float next)
        {
            if (Mathf.Approximately(current, next)) return;
            lines.Add($"{label}  {current:0.###} -> {next:0.###}");
        }

        public string DescribeUnapplied()
        {
            if (!groundBlendOverride) return string.Empty;
            return $"Ground Blend {groundBlend:F2} was not applied - it lives on the ground material, not on the profile. Set _GrassBlend on the ground material yourself.";
        }
    }
}
