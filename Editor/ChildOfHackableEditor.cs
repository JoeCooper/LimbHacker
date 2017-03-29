using UnityEditor;

namespace NobleMuffins.LimbHacker.Guts
{
    [CustomEditor(typeof(ChildOfHackable))]
    public class ChildOfHackableEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("This is a Limb Hacker internal component.");
            EditorGUILayout.LabelField("Do not add this to an object yourself.");
        }

    }
}