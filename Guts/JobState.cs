using UnityEngine;
using System;

namespace NobleMuffins.LimbHacker.Guts
{
	public class JobState
	{
		public JobState(JobSpecification specification) {
			Specification = specification;
		}

		public JobSpecification Specification { get; private set; }

		JobYield yield;
		public JobYield Yield {
			get {
				return yield;
			}
			set {
				Debug.Assert(IsDone == false, "JobYield was given a yield more than once.");
				yield = value;
				HasYield = true;
			}
		}
		public bool HasYield { get; private set; }

		Exception exception;
		public Exception Exception {
			get {
				return exception;
			}
			set {
				Debug.Assert(IsDone == false, "JobYield was given an exception more than once.");
				exception = value;
				HasException = true;
			}
		}
		public bool HasException { get; private set; }

		public bool IsDone { get {
				return HasException || HasYield;
			} }
	}
}
