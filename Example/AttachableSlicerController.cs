using UnityEngine;

[RequireComponent(typeof(SwordVelocityFilter))]
public class AttachableSlicerController : MonoBehaviour
{
	private SwordVelocityFilter swordVelocityFilter;
	public GameObject slicer;
	
	void Start ()
	{
		swordVelocityFilter = GetComponent<SwordVelocityFilter>();
	}

	void Update ()
	{
		slicer.SetActive( swordVelocityFilter.IsFastEnoughToCut );
	}
}
