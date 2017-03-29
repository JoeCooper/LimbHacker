using UnityEngine;
using System;

namespace NobleMuffins.LimbHacker.Guts
{
    public interface ISliceable
    {
        void Slice(Vector3 positionInWorldSpace, Vector3 normalInWorldSpace);
        event EventHandler<SliceEventArgs> Sliced;
    }
}