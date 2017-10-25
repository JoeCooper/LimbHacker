using System;
using UnityEngine;

namespace NobleMuffins.LimbHacker.Guts
{
	public class SliceEventArgs: EventArgs
	{		
		public SliceEventArgs (Plane planeInWorldSpace, Vector3 focalPointInWorldSpace, GameObject[] parts): base()
		{
			PlaneInWorldSpace = planeInWorldSpace;
			FocalPointInWorldSpace = focalPointInWorldSpace;
			Parts = parts;
		}

		public Plane PlaneInWorldSpace { get; private set; }

		public Vector3 FocalPointInWorldSpace { get; private set; }

		public GameObject[] Parts { get; private set; }
	}
}

