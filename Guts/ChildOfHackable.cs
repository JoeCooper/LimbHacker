using System;
using UnityEngine;

namespace NobleMuffins.LimbHacker.Guts
{
    public class ChildOfHackable : MonoBehaviour, ISliceable
    {
        [HideInInspector]
        public Hackable parentHackable;

        void Start()
        {
            if (parentHackable == null)
            {
                Debug.LogWarning("Unconfigured ChildOfHackable found. Removing. If you added this to an object yourself, please remove it.");
                GameObject.DestroyImmediate(this);
            }
        }
        
        void ISliceable.Slice(Vector3 positionInWorldSpace, Vector3 normalInWorldSpace)
        {
            parentHackable.Slice(positionInWorldSpace, normalInWorldSpace);
        }

        public event EventHandler<SliceEventArgs> Sliced;
    }
}