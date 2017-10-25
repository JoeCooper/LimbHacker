using System;
using UnityEngine;

namespace NobleMuffins.LimbHacker.Guts
{
	public class SliceEventArgs: EventArgs
	{		
		public SliceEventArgs (Plane planeInWorldSpace, GameObject[] parts): base()
		{
			PlaneInWorldSpace = planeInWorldSpace;
			Parts = parts;
		}

		public Plane PlaneInWorldSpace { get; private set; }

		public GameObject[] Parts { get; private set; }
	}
}

