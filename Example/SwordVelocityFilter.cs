using UnityEngine;

namespace NobleMuffins.LimbHacker.Examples
{
	public class SwordVelocityFilter : MonoBehaviour
	{
		public float tipSpeedForCutting = 1f;
		public float lengthInWorldUnits = 5f;

		private new Transform transform;

		void Start()
		{
			transform = GetComponent<Transform>();

			priorTipPositionInWorldSpace = deriveTipPosition();
		}

		private Vector3 priorTipPositionInWorldSpace;

		private bool _IsFastEnoughToCut = false;
		public bool IsFastEnoughToCut
		{
			get
			{
				return _IsFastEnoughToCut;
			}
		}

		private Vector3 deriveTipPosition()
		{
			return transform.localToWorldMatrix.MultiplyPoint3x4(Vector3.forward * lengthInWorldUnits);
		}

		// Update is called once per frame
		void Update()
		{
			Vector3 tipPositionInWorldSpace = deriveTipPosition();

			Vector3 tipDelta = tipPositionInWorldSpace - priorTipPositionInWorldSpace;

			float tipSpeed = tipDelta.magnitude / Time.deltaTime;

			_IsFastEnoughToCut = tipSpeed > tipSpeedForCutting;

			priorTipPositionInWorldSpace = tipPositionInWorldSpace;
		}
	}
}
