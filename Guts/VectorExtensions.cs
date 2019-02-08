using System;
using UnityEngine;

namespace NobleMuffins.LimbHacker.Guts
{
	public static class VectorExtensions
	{
		public static Vector3 ClampNormalToBicone(this Vector3 normal, Vector3 axis, float maximumDegrees)
		{
			var minimumDotProduct = Mathf.Cos(maximumDegrees * Mathf.Deg2Rad);

			var dotProduct = Vector3.Dot(normal, axis);

			var result = normal;

			if (Mathf.Abs(dotProduct) < minimumDotProduct)
			{
				var sign = Mathf.Sign(dotProduct);

				var differenceBetweenNowAndIdeal = minimumDotProduct - Mathf.Abs(dotProduct);

				var repairativeContribution = axis * differenceBetweenNowAndIdeal * sign;

				var currentCorrective = 1f;

				var lowCorrective = 1f;
				var highCorrective = 100f;

				var iterations = 16;

				while (iterations > 0)
				{
					result = (normal + repairativeContribution * currentCorrective).normalized;

					float dp = Mathf.Abs(Vector3.Dot(result, axis));

					if (dp > minimumDotProduct)
					{
						highCorrective = currentCorrective;
						currentCorrective = (currentCorrective + lowCorrective) / 2f;
					}
					else if (dp < minimumDotProduct)
					{
						lowCorrective = currentCorrective;
						currentCorrective = (currentCorrective + highCorrective) / 2f;
					}

					iterations--;
				}
			}

			return result;
		}
	}
}
