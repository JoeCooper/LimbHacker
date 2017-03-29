using UnityEngine;
using System.Collections.Generic;

namespace NobleMuffins.LimbHacker.Guts
{
    public class Slicer : MonoBehaviour
    {
        class PendingSlice
        {
            public PendingSlice(Vector3 _point, ISliceable _target)
            {
                point = _point;
                target = _target;
            }

            public readonly Vector3 point;
            public readonly ISliceable target;
        }

        public Transform planeDefiner1, planeDefiner2, planeDefiner3;
        public MeshRenderer editorVisualization;

        private readonly Queue<PendingSlice> pendingSlices = new Queue<PendingSlice>();

        // Use this for initialization
        void Start()
        {
            if (editorVisualization != null)
            {
                editorVisualization.enabled = false;
            }

            bool hasAllPlaneDefiners = true;

            hasAllPlaneDefiners = planeDefiner1 != null;
            hasAllPlaneDefiners &= planeDefiner2 != null;
            hasAllPlaneDefiners &= planeDefiner3 != null;

            if (hasAllPlaneDefiners == false)
            {
                Debug.LogError("Slicer '" + gameObject.name + "' is missing a plane definer!");
            }
        }

        private List<GameObject> suppressUntilContactCeases = new List<GameObject>();

        void OnTriggerEnter(Collider other)
        {
            if (suppressUntilContactCeases.Contains(other.gameObject) == false)
            {
                ISliceable sliceable = other.GetComponent(typeof(ISliceable)) as ISliceable;

                if (sliceable != null)
                {
                    Vector3 point = other.ClosestPointOnBounds(positionInWorldSpace);

                    pendingSlices.Enqueue(new PendingSlice(point, sliceable));
                }
            }
        }

        void OnTriggerExit(Collider other)
        {
            ContactCeased(other.gameObject);
        }

        void OnCollisionEnter(Collision other)
        {
            if (suppressUntilContactCeases.Contains(other.gameObject) == false)
            {
                ISliceable sliceable = other.gameObject.GetComponent(typeof(ISliceable)) as ISliceable;

                if (sliceable != null)
                {
                    Vector3 point = other.contacts[0].point;

                    pendingSlices.Enqueue(new PendingSlice(point, sliceable));
                }
            }
        }

        void OnCollisionExit(Collision other)
        {
            ContactCeased(other.gameObject);
        }

        private void ContactCeased(GameObject other)
        {
            if (suppressUntilContactCeases.Contains(other))
            {
                suppressUntilContactCeases.Remove(other);
            }
        }

        private Vector3 positionInWorldSpace
        {
            get
            {
                return (planeDefiner1.position + planeDefiner2.position + planeDefiner3.position) / 3f;

            }
        }

        private Vector3 normalInWorldSpace
        {
            get
            {
                Vector3 t0 = planeDefiner1.position;
                Vector3 t1 = planeDefiner2.position;
                Vector3 t2 = planeDefiner3.position;

                Vector3 v;

                v.x = t0.y * (t1.z - t2.z) + t1.y * (t2.z - t0.z) + t2.y * (t0.z - t1.z);
                v.y = t0.z * (t1.x - t2.x) + t1.z * (t2.x - t0.x) + t2.z * (t0.x - t1.x);
                v.z = t0.x * (t1.y - t2.y) + t1.x * (t2.y - t0.y) + t2.x * (t0.y - t1.y);

                return v;
            }
        }

        // Update is called once per frame
        void LateUpdate()
        {
            while (pendingSlices.Count > 0)
            {
                PendingSlice pendingSlice = pendingSlices.Dequeue();

                var component = pendingSlice.target as MonoBehaviour;

                if (component != null)
                {
                    var targetGameObject = component.gameObject;

                    if (suppressUntilContactCeases.Contains(targetGameObject) == false)
                    {

                        pendingSlice.target.Sliced += PendingSlice_target_Sliced;

                        pendingSlice.target.Slice(pendingSlice.point, normalInWorldSpace);
                    }
                }
            }
        }

        void PendingSlice_target_Sliced(object sender, SliceEventArgs e)
        {
            if (e.Parts.Length > 1)
            {
                suppressUntilContactCeases.AddRange(e.Parts);
            }
        }
    }
}