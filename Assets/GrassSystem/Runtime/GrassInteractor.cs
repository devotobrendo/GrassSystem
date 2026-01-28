// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using UnityEngine;

namespace GrassSystem
{
    [ExecuteAlways]
    public class GrassInteractor : MonoBehaviour
    {
        [Tooltip("Radius of the bending effect on XZ plane")]
        [Range(0.1f, 5f)]
        public float radius = 1f;
        
        [Tooltip("Strength of the bending")]
        [Range(0f, 10f)]
        public float strength = 1f;
        
        [Header("Vertical Position")]
        [Tooltip("Use the bottom of the attached Collider for Y position")]
        public bool useColliderBounds = true;
        
        [Tooltip("Height of the object (used when useColliderBounds is disabled)")]
        [Range(0f, 5f)]
        public float objectHeight = 0.5f;
        
        [Tooltip("Additional vertical offset")]
        public float heightOffset = 0f;
        
        private Collider _cachedCollider;
        
        private void Awake()
        {
            CacheCollider();
        }
        
        private void OnValidate()
        {
            CacheCollider();
        }
        
        private void CacheCollider()
        {
            _cachedCollider = GetComponent<Collider>();
        }
        
        private float GetBottomY()
        {
            if (useColliderBounds && _cachedCollider != null)
            {
                return _cachedCollider.bounds.min.y + heightOffset;
            }
            else
            {
                return transform.position.y - objectHeight + heightOffset;
            }
        }
        
        public Vector4 GetInteractionData()
        {
            Vector3 pos = transform.position;
            pos.y = GetBottomY();
            return new Vector4(pos.x, pos.y, pos.z, radius);
        }
        
        private static readonly System.Collections.Generic.List<GrassInteractor> _activeInteractors = new();
        
        public static System.Collections.Generic.IReadOnlyList<GrassInteractor> ActiveInteractors => _activeInteractors;
        
        private void OnEnable()
        {
            CacheCollider();
            if (!_activeInteractors.Contains(this))
                _activeInteractors.Add(this);
        }
        
        private void OnDisable()
        {
            _activeInteractors.Remove(this);
        }
        
        private void OnDrawGizmosSelected()
        {
            if (_cachedCollider == null)
                _cachedCollider = GetComponent<Collider>();
                
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
            Vector3 pos = transform.position;
            pos.y = GetBottomY();
            Gizmos.DrawWireSphere(pos, radius);
            Gizmos.DrawSphere(pos, 0.1f);
        }
    }
}
