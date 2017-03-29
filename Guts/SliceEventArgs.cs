using System;
using UnityEngine;

namespace NobleMuffins.LimbHacker.Guts
{
	public class SliceEventArgs: EventArgs
	{		
		public SliceEventArgs (GameObject[] parts): base()
		{
			Parts = parts;
		}

		public GameObject[] Parts { get; private set; }
	}
}

