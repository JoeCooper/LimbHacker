using UnityEngine;

public class CameraPositionController : MonoBehaviour
{
	public Transform backAwayPosition, upClosePosition;
	public TimeForSlicingFilter timeForSlicingFilter;
	public float transitionTime = 1f;
	
	private new Transform transform;
	private Vector3 position, positionDelta;	

	// Use this for initialization
	void Start () {
		transform = GetComponent<Transform>();
		
		position = transform.position;
	}
	
	// Update is called once per frame
	void Update ()
	{
		Vector3 idealPosition = timeForSlicingFilter.IsTimeForSlicing ? upClosePosition.position : backAwayPosition.position;
		
		position = Vector3.SmoothDamp(position, idealPosition, ref positionDelta, transitionTime);
		
		transform.position = position;
	}
}
