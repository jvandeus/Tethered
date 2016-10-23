using System;
using UnityEngine;

public class LinkSegment : MonoBehaviour {
	public bool disableUpdate = false;
	public GameObject AttachedObject
	{
		get { return attachedObject; }
		set {
			if (value) {
				attachedObject = value;
				LinkManager = attachedObject.GetComponent<DynamicLink>();
			}
		}
	}
	private DynamicLink LinkManager
	{
		get { return linkManager; }
		set {
			linkManager = value;
			if (linkManager) {
				linkNum = linkManager.GetNumHinges();
				maxNum = linkManager.maxHinges;
			} else {
				linkNum = 0;
				maxNum = 1;
			}
		}
	}
	private int linkNum = 0;
	private int maxNum = 1;
	private GameObject attachedObject;
	private DynamicLink linkManager;
	private Renderer visual;
	private Vector3 worldSize;

	// All essential intialization after the component is instantiated and before the component gets enabled.
	void Awake ()
	{
		// get the Renderer
		visual = GetComponent<Renderer>();
		if (visual) {
			// assign it a unique color for this segmant.
			visual.material.color = GetColorByOffset((linkNum/maxNum), Color.red);
			// get the size of this object from the rederer bouds.
			worldSize = visual.bounds.size;
		}
		// for now we will use lossyScale, though the docs mention incorrect representation if used under certain conditions.
		// Note that visual.bounds.size and collider.bounds.size will be different if an object is rotated or scaled.
		worldSize = transform.lossyScale;
	}

	// Initialization after the component first gets enabled. This happens on the very next Update() call.
	void Start ()
	{
		// get the related DynamicLink script component if we have not already.
		if (attachedObject && !linkManager) {
			LinkManager = attachedObject.GetComponent<DynamicLink>();
		}
		// Debug.Log("worldSize"+linkNum.ToString());
		// Debug.Log(worldSize);
	}

	// Called once per frame. The amount of code that is run in this function will effect performance the most.
	void Update ()
	{
		if(!Input.GetKey(KeyCode.Space) && !disableUpdate) {
			// position, rotate, and scale the object between the two anchor points the parent script.
			if (linkManager) {
				StretchBetweenPoints(linkManager.GetStartPoint(), linkManager.GetEndPoint());
			}
		}
	}

	Color GetColorByOffset(float offset, Color startingColor = new Color())
	{
		System.Random random = new System.Random();
		float value = (startingColor.r + startingColor.g + startingColor.b)/3;
		float newValue = value + (2*(float)random.NextDouble() * offset) - offset;
		float valueRatio = newValue / value;
		Color newColor = new Color();
		newColor.r = startingColor.r * valueRatio;
		newColor.g = startingColor.g * valueRatio;
		newColor.b = startingColor.b * valueRatio;
		return newColor;
	}

	// When an object enters the collider, the object will "break" into 2 conneceted by a hinge joint
	// void OnTriggerEnter2D(Collider2D other)
	// {
	// 	if (other.gameObject.layer == LayerMask.NameToLayer("Ground") && numHinges < maxHinges) {
	// 		numHinges++;
	// 		HingeLinkAt(new Vector2(0.5f,0.5f));
	// 		Debug.Log("triggered");
	// 	}
	// }
	void OnCollisionEnter2D(Collision2D coll)
	{
		DynamicLink newLink;
		// TODO: get the direction of the angular monentum of the hinge joint or otherise record the conditions this became a hinge so we know when to break it.
		
		// Check for objects we want to "break" the link at, which should be environment objects, or objects of a specific group.
		if (attachedObject && linkManager && coll.collider.gameObject.layer == LayerMask.NameToLayer("Ground")) {
			Debug.Log("collided with hinge-able object");
			// use a ray from the collision point in the direction opposing the momentum to get the hinge point
			// OR: get the center of the link that is in line with the collision point and store as the hinge point
			foreach(ContactPoint2D thisContact in coll.contacts)
			{
				// TODO: get the "buffer" radius from the distance between the edge of the collision box and the anchor point.
				// for now, break at the first point and exit the loop. Later we will break at each point if they are not within a certain distance of each other
				// Break and create a hinge by passing the point on global space.
				newLink = linkManager.HingeToObjectAtPoint(coll.collider.gameObject, thisContact.point);
				// Debug.Log(newLink);
				if (!newLink) {
					break;
				}
				// Debug.Log(thisContact.point);
				// Debug.Log("self");
				// Debug.Log(linkManager.TargetEnd);
				// Debug.Log(linkManager.TargetStart);
				// Debug.Log("child");
				// Debug.Log(newLink.TargetEnd);
				// Debug.Log(newLink.TargetStart);
				// Debug.Log(newLink.AnchorPointStart);
				// Debug.Log(newLink.AnchorPointEnd);
			}
		} else {
			Debug.Log("collided with something else");
		}
	}

	// change the scale and rotation of the link segment to stretch between the two points in world space.
	private void StretchBetweenPoints(Vector3 startPoint, Vector3 endPoint)
	{
		Vector3 distanceVector;
		Vector3 stretchDirection;
		Vector3 thickDirection;
		Vector3 scaleVector;
		// TODO: maybe use anchor points instead to determine scale factors?
		float scaleFactor = 1f;
		float thickness = .5f;
		// TODO: adjust for 3d so to the "depth" can also be dynamic.
		float depth = .5f;
		
		// get the distance between the points we are stretching across.
		distanceVector = (endPoint - startPoint);
		// get the direction in which to stretch the object (for now we will only stretch in the object's "right" direction)
		stretchDirection = Vector3.right;
		// use the direction vector to determine the direction for thickness scaling.
		thickDirection = new Vector3(-stretchDirection.y, stretchDirection.x, 0f);
		// get the absolute value of each component so it can be used as a positive scale.
		stretchDirection = new Vector3(Mathf.Abs(stretchDirection.x), Mathf.Abs(stretchDirection.y), Mathf.Abs(stretchDirection.z));
		// project onto the vector that represents the object's size in world space to find the scale vector for the stretch direction.
		scaleVector = Vector3.Project(worldSize, stretchDirection.normalized);
		// adust the magnitude of the scale vector based on its size in world space, the distance it must stretch, and the finite scale factor.
		scaleVector = scaleVector.normalized * (distanceVector.magnitude / scaleVector.magnitude * scaleFactor);
		// add the finite thickness and depth to the vector, relative to the direction we are stretching in.
		scaleVector = scaleVector + thickDirection * thickness + Vector3.forward * depth;
		// apply the new scale to this transform
		transform.localScale = scaleVector;
		// set the position to the midpoint between these points
		transform.position = Vector3.Lerp(startPoint, endPoint, 0.5f);
		// rotate the object so its direction matches the direction we are stretching in
		transform.rotation = Quaternion.FromToRotation(stretchDirection, new Vector3(distanceVector.normalized.x, distanceVector.normalized.y, 0));
	}
}