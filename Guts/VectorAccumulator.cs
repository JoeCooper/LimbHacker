using UnityEngine;

namespace NobleMuffins.LimbHacker.Guts
{
	public class VectorAccumulator
	{
		private Vector3 aggregatedFigures = Vector3.zero;
		private int count = 0;
		
		public void Add(Vector3 v)
		{
			aggregatedFigures += v;
			count++;
		}
		
		public Vector3 Mean {
			get {
				if(count == 0)
				{
					return Vector3.zero;
				}
				else
				{
					float f = (float) count;
					
					return aggregatedFigures / f;
				}
			}
		}
	}
}