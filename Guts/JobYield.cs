using UnityEngine;
using System.Collections.Generic;

namespace NobleMuffins.LimbHacker.Guts
{
	public class JobYield
	{
		public JobYield(JobSpecification job, Vector4 planeInWorldSpace, Vector3 focalPointInWorldSpace, IEnumerable<MeshSnapshot> alfa, IEnumerable<MeshSnapshot> bravo) {
			Job = job;
			PlaneInWorldSpace = planeInWorldSpace;
			FocalPointInWorldSpace = focalPointInWorldSpace;
			Alfa = alfa;
			Bravo = bravo;
		}

		public JobSpecification Job { get; private set; }
		public Vector4 PlaneInWorldSpace { get; private set; }
		public IEnumerable<MeshSnapshot> Alfa { get; private set; }
		public IEnumerable<MeshSnapshot> Bravo { get; private set; }
		public Vector3 FocalPointInWorldSpace { get; private set; }
	}
}

