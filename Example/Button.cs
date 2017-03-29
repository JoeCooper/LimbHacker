using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class Button : MonoBehaviour
{
	const float changeTime = 0.033f;

	public new Camera camera;

	public GameObject target;
	public string message;
	
	public AudioClip clickSound;

	public bool visible;
	
	private new Transform transform;
	private new Collider collider;
	private Vector3 scaleAtStart;
	
	private float size = 1f, sizeDelta = 0f;
	private bool pressedAsButton = false;
	
	void Start()
	{
		transform = GetComponent<Transform>();
		collider = GetComponent<Collider>();
		
		scaleAtStart = transform.localScale;
		
		if(camera == null) camera = Camera.main;
	}

	private bool firstRun = true;
	
	void Update()
	{
		Ray ray = camera.ScreenPointToRay(Input.mousePosition);
		
		RaycastHit hitInfo;
		
		bool hover = collider.Raycast(ray, out hitInfo, 2f);
		
		pressedAsButton |= hover && Input.GetMouseButtonDown(0);
		
		bool released = Input.GetMouseButtonUp(0);
		
		bool releasedAsButton = pressedAsButton && hover && released;
		
		if(released)
		{
			pressedAsButton = false;
		}
		
		if(releasedAsButton)
		{
			target.SendMessage(message);

			if(clickSound != null)
			{
				AudioSource.PlayClipAtPoint(clickSound, Vector3.zero);
			}
		}
		
		bool enlarge = hover || pressedAsButton;
		
		float idealSize = (enlarge ? 1.1f : 1f) * (visible ? 1f : 0f);

		size = firstRun ? idealSize : Mathf.SmoothDamp(size, idealSize, ref sizeDelta, changeTime);

		firstRun = false;
		
		transform.localScale = size * scaleAtStart;
	}
}
