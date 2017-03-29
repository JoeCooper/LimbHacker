using System.Collections.Generic;

namespace NobleMuffins.LimbHacker.Guts
{
	public class JobYield
	{
		public JobYield(JobSpecification job, IEnumerable<MeshSnapshot> alfa, IEnumerable<MeshSnapshot> bravo) {
			Job = job;
			Alfa = alfa;
			Bravo = bravo;
		}

		public JobSpecification Job { get; private set; }
		public IEnumerable<MeshSnapshot> Alfa { get; private set; }
		public IEnumerable<MeshSnapshot> Bravo { get; private set; }
	}
}

