using UnityEngine;

namespace GrassSystem.Consoles
{
    public static class GrassRuntimeDecalCompositor
    {
        private static Material compositeMaterial;

        public static bool Composite(Texture baseMap, Vector4 baseBounds, Texture overlay, Vector4 overlayBounds, ref RenderTexture target)
        {
            if (overlay == null)
                return false;

            if (baseBounds.z == 0f || baseBounds.w == 0f)
                return false;

            int width = baseMap != null ? baseMap.width : 1024;
            int height = baseMap != null ? baseMap.height : 1024;

            if (target == null || target.width != width || target.height != height)
            {
                Release(ref target);
                target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.name = "GrassRuntimeDecal";
                target.wrapMode = TextureWrapMode.Clamp;
                target.Create();
            }

            Material mat = GetMaterial();
            if (mat == null)
                return false;

            float u0 = (overlayBounds.x - baseBounds.x) / baseBounds.z;
            float v0 = (overlayBounds.y - baseBounds.y) / baseBounds.w;
            float du = overlayBounds.z / baseBounds.z;
            float dv = overlayBounds.w / baseBounds.w;

            mat.SetTexture("_OverlayTex", overlay);
            mat.SetVector("_OverlayRect", new Vector4(u0, v0, du, dv));

            Texture source = baseMap != null ? baseMap : (Texture)Texture2D.blackTexture;
            Graphics.Blit(source, target, mat);

            return true;
        }

        public static void Release(ref RenderTexture rt)
        {
            if (rt == null)
                return;

            rt.Release();
            if (Application.isPlaying)
                Object.Destroy(rt);
            else
                Object.DestroyImmediate(rt);
            rt = null;
        }

        private static Material GetMaterial()
        {
            if (compositeMaterial != null)
                return compositeMaterial;

            Shader shader = Shader.Find("Hidden/GrassSystem/DecalComposite");
            if (shader == null)
            {
                Debug.LogError("GrassRuntimeDecalCompositor: Shader 'Hidden/GrassSystem/DecalComposite' not found. Ensure GrassDecalComposite.shader is in the project.");
                return null;
            }

            compositeMaterial = new Material(shader);
            return compositeMaterial;
        }
    }
}
