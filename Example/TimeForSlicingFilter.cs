using UnityEngine;
using System.Collections;

namespace NobleMuffins.LimbHacker.Examples
{
	public class TimeForSlicingFilter : MonoBehaviour
	{
		public string nameOfAnimationStateForSlicing = "Standing";
		public Spawner spawner;

		public bool isAlwaysTimeForSlicing;

		private Animator animator;

		void Awake()
		{
			spawner.instantiationListeners += HandleInstantiation;
		}

		void HandleInstantiation(GameObject go)
		{
			animator = go.GetComponent<Animator>();
		}

		public bool IsTimeForSlicing
		{
			get
			{
				if (Input.GetKey(KeyCode.A)) return true;
				if (isAlwaysTimeForSlicing) return true;
#if UNITY_5 || UNITY_5_3_OR_NEWER //Starting with 5.3 we can use UNITY_X_Y_OR_NEWER
				return animator != null && animator.GetCurrentAnimatorStateInfo(0).fullPathHash == Animator.StringToHash(nameOfAnimationStateForSlicing);
#else
				return animator != null && animator.GetCurrentAnimatorStateInfo(0).nameHash == Animator.StringToHash(nameOfAnimationStateForSlicing);
#endif
			}
		}
	}
}
