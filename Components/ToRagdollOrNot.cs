using UnityEngine;
using System.Collections.Generic;

namespace NobleMuffins.LimbHacker
{
    [RequireComponent(typeof(Hackable))]
    public class ToRagdollOrNot : AbstractSliceHandler
    {
        public enum Operator { And, Or };
        public enum Presentness { Equals, Not };

        public Operator groupRule = Operator.And;
        public Presentness totalityRule = Presentness.Not;

        public Transform[] bones;

        public override bool cloneAlternate(Dictionary<string, bool> hierarchyPresence)
        {
            List<bool> relevantStates = new List<bool>(bones.Length);

            foreach (Transform t in bones)
            {
                string key = t.name;

                if (hierarchyPresence.ContainsKey(key))
                {
                    relevantStates.Add(hierarchyPresence[key]);
                }
            }

            bool totality = false;

            if (groupRule == Operator.And)
            {
                totality = true;
                foreach (bool b in relevantStates)
                {
                    totality &= b;
                }
            }
            else if (groupRule == Operator.Or)
            {
                totality = false;
                foreach (bool b in relevantStates)
                {
                    totality |= b;
                }
            }

            if (totalityRule == Presentness.Not)
            {
                totality = !totality;
            }

            return totality;
        }

        public override void handleSlice(GameObject[] results)
        {
            //Do nothing.
        }
    }
}