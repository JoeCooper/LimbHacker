using UnityEngine;

namespace NobleMuffins.LimbHacker.Guts
{
    public class NodeMetadata
    {
        public NodeMetadata(Transform t, bool isConsideredSevered)
        {
            Key = t.name;
            LocalPosition = t.localPosition;
            LocalScale = t.localScale;
            LocalRotation = t.localRotation;
            IsActive = t.gameObject.activeSelf;
            IsConsideredSevered = isConsideredSevered;
            WorldToLocalMatrix = t.worldToLocalMatrix;
            var parent = t.parent;
            if (parent != null)
            {
                ParentKey = parent.name;
            }
        }

        public string Key { get; private set; }

        public string ParentKey { get; private set; }

        public bool IsActive { get; private set; }

        public bool IsConsideredSevered { get; private set; }

        public Vector3 LocalPosition { get; private set; }

        public Vector3 LocalScale { get; private set; }

        public Quaternion LocalRotation { get; private set; }

        public Matrix4x4 WorldToLocalMatrix { get; private set; }
    }
}
