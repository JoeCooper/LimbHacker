using System.Collections.Generic;
using UnityEngine;

namespace NobleMuffins.LimbHacker.Guts
{
	public class JobSpecification
	{
		public JobSpecification(GameObject subject, Hackable hackable,
            IEnumerable<MeshSnapshot> meshSnapshots,
            Dictionary<string, NodeMetadata> nodeMetadata,
            Material infillMaterial,
            string jointName, float rootTipProgression, Vector3? tiltPlane, InfillMode infillMode,
            bool destroyOriginal) {
			Subject = subject;
            Hackable = hackable;
			MeshSnapshots = meshSnapshots;
            InfillMaterial = infillMaterial;
            NodeMetadata = nodeMetadata;
            JointName = jointName;
            RootTipProgression = rootTipProgression;
            TiltPlane = tiltPlane;
            InfillMode = infillMode;
			DestroyOriginal = destroyOriginal;
		}

		public GameObject Subject { get; private set; }

        public Hackable Hackable { get; private set; }

        public Material InfillMaterial { get; private set; }

		public bool DestroyOriginal { get; private set; }

		public IEnumerable<MeshSnapshot> MeshSnapshots { get; private set; }

        public Dictionary<string, NodeMetadata> NodeMetadata { get; private set; }

        public string JointName { get; private set; }

        public float RootTipProgression { get; private set; }

        public Vector3? TiltPlane { get; private set; }

        public InfillMode InfillMode { get; private set; }
    }
}