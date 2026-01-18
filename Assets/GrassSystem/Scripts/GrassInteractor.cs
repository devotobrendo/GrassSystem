// GrassInteractor.cs - Component for player/character grass interaction
// Add to any GameObject that should bend the grass when moving through it

using UnityEngine;

namespace GrassSystem
{
    /// <summary>
    /// Attach to characters to make grass bend around them.
    /// Automatically registers with the GrassRenderer.
    /// </summary>
    [ExecuteAlways]
    public class GrassInteractor : MonoBehaviour
    {
        [Tooltip("Radius of the bending effect around this object")]
        [Range(0.1f, 5f)]
        public float radius = 1f;
        
        [Tooltip("Strength of the bending (0 = no bend, 1 = full bend)")]
        [Range(0f, 2f)]
        public float strength = 1f;
        
        [Tooltip("Vertical offset from transform position (e.g. for feet level)")]
        public float heightOffset = 0f;
        
        /// <summary>
        /// Returns the interaction data as Vector4 (xyz = position, w = radius * strength)
        /// </summary>
        public Vector4 GetInteractionData()
        {
            Vector3 pos = transform.position;
            pos.y += heightOffset;
            return new Vector4(pos.x, pos.y, pos.z, radius * strength);
        }
        
        // Static registry for all active interactors
        private static readonly System.Collections.Generic.List<GrassInteractor> _activeInteractors = new();
        
        public static System.Collections.Generic.IReadOnlyList<GrassInteractor> ActiveInteractors => _activeInteractors;
        
        private void OnEnable()
        {
            if (!_activeInteractors.Contains(this))
                _activeInteractors.Add(this);
        }
        
        private void OnDisable()
        {
            _activeInteractors.Remove(this);
        }
        
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
            Vector3 pos = transform.position;
            pos.y += heightOffset;
            Gizmos.DrawWireSphere(pos, radius);
            Gizmos.DrawSphere(pos, 0.1f);
        }
    }
}
