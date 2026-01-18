// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using UnityEngine;

namespace GrassSystem
{
    [ExecuteAlways]
    public class GrassInteractor : MonoBehaviour
    {
        [Tooltip("Radius of the bending effect")]
        [Range(0.1f, 5f)]
        public float radius = 1f;
        
        [Tooltip("Strength of the bending")]
        [Range(0f, 2f)]
        public float strength = 1f;
        
        [Tooltip("Vertical offset from transform position")]
        public float heightOffset = 0f;
        
        public Vector4 GetInteractionData()
        {
            Vector3 pos = transform.position;
            pos.y += heightOffset;
            return new Vector4(pos.x, pos.y, pos.z, radius * strength);
        }
        
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
