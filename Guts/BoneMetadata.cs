using UnityEngine;

namespace NobleMuffins.LimbHacker.Guts
{
    public class BoneMetadata
    {
        public BoneMetadata(int index, string nameInViewGraph, Matrix4x4 bindPose)
        {
            Index = index;
            NameInViewGraph = nameInViewGraph;
            BindPose = bindPose;
        }

        public int Index { get; private set; }
        public string NameInViewGraph { get; private set; }
        public Matrix4x4 BindPose { get; private set; }
    }
}
