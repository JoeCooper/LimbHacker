using UnityEngine;
using System.Collections.Generic;

public class MarkOfCain : MonoBehaviour
{
	private readonly static List<GameObject> markedObjects = new List<GameObject>();
	
	void Start()
	{
		markedObjects.Add(gameObject);
	}
	
	public static void DestroyAllMarkedObjects()
	{
		foreach(var go in markedObjects)
		{
			if(go != null) Destroy(go);
		}
		
		markedObjects.Clear();
	}
}
