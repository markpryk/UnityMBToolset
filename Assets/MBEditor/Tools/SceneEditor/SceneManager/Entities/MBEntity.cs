using UnityEngine;

namespace MountAndBlade.Data
{
    /// <summary>
    /// Base class for all M&B prefab components.
    /// Provides common functionality for prefab management.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class MBEntity : MonoBehaviour
    {
        #region Serialized Fields

        [SerializeField] protected string _prefabId;
        [SerializeField] protected string _sourceModule;

        #endregion

        #region Properties

        /// <summary>
        /// Unique identifier for this prefab
        /// </summary>
        public string PrefabID
        {
            get => _prefabId;
            set => _prefabId = value;
        }

        /// <summary>
        /// Module this prefab originated from (e.g., "Native", "MyMod")
        /// </summary>
        public string SourceModule
        {
            get => _sourceModule;
            set => _sourceModule = value;
        }

        #endregion

        #region Virtual Methods

        /// <summary>
        /// Called when the prefab is instantiated in a scene
        /// </summary>
        public virtual void OnSpawn()
        {
        }

        /// <summary>
        /// Called when the prefab is being removed from a scene
        /// </summary>
        public virtual void OnDespawn()
        {
        }

        /// <summary>
        /// Validates the prefab data. Override in derived classes.
        /// </summary>
        public virtual bool Validate(out string error)
        {
            error = null;
            
            if (string.IsNullOrEmpty(_prefabId))
            {
                error = "Prefab ID is not set";
                return false;
            }
            
            return true;
        }

        #endregion

        #region Editor Helpers

#if UNITY_EDITOR
        /// <summary>
        /// Called when the component is reset in the editor
        /// </summary>
        protected virtual void Reset()
        {
            // Try to auto-fill prefab ID from GameObject name
            if (string.IsNullOrEmpty(_prefabId))
            {
                _prefabId = gameObject.name;
            }
        }

        /// <summary>
        /// Draws gizmos for this prefab in the scene view
        /// </summary>
        protected virtual void OnDrawGizmosSelected()
        {
            // Override in derived classes for custom gizmos
        }
#endif

        #endregion
    }
}