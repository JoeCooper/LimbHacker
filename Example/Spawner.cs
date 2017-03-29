using UnityEngine;

public class Spawner : MonoBehaviour {
	
	public GameObject puppet;
	
	public Object prefab;
	
	public System.Action<GameObject> instantiationListeners;
	
	public GUISkin skin;

	// Use this for initialization
	void Start ()
	{
		Instantiate();
	}
	
	public void Instantiate()
	{
		if(CanInstantiate)
		{
			MarkOfCain.DestroyAllMarkedObjects();
			
			if(puppet == null)
			{
				puppet = GameObject.Instantiate(prefab, transform.position, transform.rotation) as GameObject;
			}
			
			instantiationListeners(puppet);
		}
	}

	public bool CanInstantiate
	{
		get {
			return puppet == null;
		}
	}
}
