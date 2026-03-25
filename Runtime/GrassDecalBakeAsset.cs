using UnityEngine;

namespace GrassSystem
{
    [CreateAssetMenu(fileName = "GrassDecalBake", menuName = "Grass System/Grass Decal Bake Asset")]
    public class GrassDecalBakeAsset : ScriptableObject
    {
        public Texture2D overrideMap;
        public Texture2D multiplyMap;
        public Texture2D additiveMap;
        public Vector4 bounds;
    }
}
