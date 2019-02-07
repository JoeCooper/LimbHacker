using UnityEngine;
using System.Collections;

namespace NobleMuffins.LimbHacker.Examples
{
	public class CameraSteer : MonoBehaviour
	{

		private Vector3 naturalForward, naturalUp, naturalRight;

		private Vector3 forward, forwardDelta;

		private new Transform transform;

		public float panSpeed = 0.33f;

		void Awake()
		{
			transform = GetComponent<Transform>();

			naturalForward = transform.forward;
			naturalRight = transform.right;
			naturalUp = transform.up;

			forward = naturalForward;
			forwardDelta = Vector3.zero;
		}

		// Update is called once per frame
		void Update()
		{
			Vector2 center = new Vector2(Screen.width / 2f, Screen.height / 2f);
			Vector2 current = (Vector2)Input.mousePosition;

			Vector2 delta = current - center;

			delta.x /= Screen.width;
			delta.y /= Screen.height;

			delta *= 0.33f;

			Vector3 idealForward = (naturalForward + naturalRight * delta.x + naturalUp * delta.y).normalized;

			forward = Vector3.SmoothDamp(forward, idealForward, ref forwardDelta, panSpeed);

			transform.forward = forward;
		}
	}
}
