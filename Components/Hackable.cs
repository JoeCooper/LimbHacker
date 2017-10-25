using UnityEngine;
using System.Collections.Generic;
using System;
using NobleMuffins.LimbHacker.Guts;

namespace NobleMuffins.LimbHacker
{
    public class Hackable : MonoBehaviour, ISliceable
    {
        public UnityEngine.Object alternatePrefab = null;

        public Transform[] severables = new Transform[0];
        public Dictionary<string, float> maximumTiltBySeverableName = new Dictionary<string, float>();

        public Material infillMaterial = null;

        public InfillMode infillMode = InfillMode.Sloppy;

        private bool destructionPending = false;

        void Start()
        {
            Collider[] childColliders = gameObject.GetComponentsInChildren<Collider>();

            foreach (Collider c in childColliders)
            {
                GameObject go = c.gameObject;

                ChildOfHackable referencer = go.GetComponent<ChildOfHackable>();

                if (referencer == null)
                    referencer = go.AddComponent<ChildOfHackable>();

                referencer.parentHackable = this;
            }
        }

        public event EventHandler<SliceEventArgs> Sliced;

        public void Slice(Vector3 positionInWorldSpace, Vector3 normalInWorldSpace)
        {
            if (destructionPending) return;
            
            var decisionMaker = gameObject.GetComponent<AbstractHackDecisionMaker>();

            string jointName = null;
            float rootTipProgression = 0f;
            if (LimbHackerAgent.DetermineSlice(this, positionInWorldSpace, ref jointName, ref rootTipProgression) && (decisionMaker == null || decisionMaker.ShouldHack(jointName)))
            {
                LimbHackerAgent.instance.SeverByJoint(gameObject, jointName, rootTipProgression, normalInWorldSpace);
            }
        }

		public void handleSlice(GameObject[] results, Vector4 planeInWorldSpace, Vector3 focalPointInWorldSpace)
        {
            bool originalRemainsAfterSlice = false;

            for (int i = 0; i < results.Length; i++) originalRemainsAfterSlice |= results[i] == gameObject;

            destructionPending = !originalRemainsAfterSlice;

            AbstractSliceHandler[] handlers = gameObject.GetComponents<AbstractSliceHandler>();

            foreach (AbstractSliceHandler handler in handlers)
            {
                handler.handleSlice(results);
            }

			if (Sliced != null)
			{
				Sliced(this, new SliceEventArgs(new Plane(planeInWorldSpace, planeInWorldSpace.w), focalPointInWorldSpace, results));
			}
        }

        public bool cloneAlternate(Dictionary<string, bool> hierarchyPresence)
        {
            if (alternatePrefab == null)
            {
                return false;
            }
            else
            {
                AbstractSliceHandler[] handlers = gameObject.GetComponents<AbstractSliceHandler>();

                bool result = false;

                if (handlers.Length == 0)
                {
                    result = true;
                }
                else
                {
                    foreach (AbstractSliceHandler handler in handlers)
                    {
                        result |= handler.cloneAlternate(hierarchyPresence);
                    }
                }

                return result;

            }
        }
        
    }

}