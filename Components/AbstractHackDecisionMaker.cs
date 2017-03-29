using UnityEngine;

namespace NobleMuffins.LimbHacker
{
    public abstract class AbstractHackDecisionMaker : MonoBehaviour
    {
        public abstract bool ShouldHack(string joint);
    }
}