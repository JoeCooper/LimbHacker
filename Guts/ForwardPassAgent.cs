using System.Collections.Generic;
using UnityEngine;

namespace NobleMuffins.LimbHacker.Guts
{
	public class ForwardPassAgent : MonoBehaviour {
		public IEnumerable<MeshSnapshot> Snapshot { get; set; }
	}
}