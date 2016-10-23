using System;
using UnityEngine;

// TODO: figure out the proper components needed
// [RequireComponent(typeof(Rigidbody2D))]
// [RequireComponent(typeof(DistanceJoint2D))]
public class DynamicLink : MonoBehaviour {

	// This script should be attached to the object at the "start" of the link
	// TODO: if the "start" changes we will need a way to swap the component to another object.
	public Rigidbody2D TargetStart
	{
		get { return targetStart; }
		set
		{
			// TODO: if the "start" changes we will need a way to swap the component to another object.
		}
	}

	// object at the "end" of the link (this will attach to another thing, like a player, or nothing if not set)
	public Rigidbody2D TargetEnd
	{
		get { return distanceLimiter ? distanceLimiter.connectedBody : targetEnd; }
		set
		{
			targetEnd = value;
			if (distanceLimiter) {
				distanceLimiter.connectedBody = value;
			}
		}
	}
	// Anchor point for the connection in the starting object's local space.
	public Vector2 AnchorPointStart
	{
		get { return distanceLimiter ? distanceLimiter.anchor : Vector2.zero; }
		set
		{
			if (distanceLimiter) {
				distanceLimiter.anchor = value;
			}
		}
	}
	// Anchor point for the connection in the ending object's local space.
	public Vector2 AnchorPointEnd
	{
		get { return distanceLimiter ? distanceLimiter.connectedAnchor : Vector2.zero; }
		set
		{
			if (distanceLimiter) {
				distanceLimiter.connectedAnchor = value;
			}
		}
	}
	public int maxHinges; // the maximum allowed number of hinge points.
	public GameObject segmentPrefab; // requires a Collider2D
	public GameObject anchorPrefab; // requires a RigidBody2D
	private LinkSegment actorSegment; // the script that provides the visual, collider, and possible other interative elements.
	private DynamicLink attachedHead; // the adjacent link toward the "start" of the link (if any)
	private DynamicLink attachedTail; // the adjacent link toward the "end" of the link (if any)
	// private GameObject linkAnchor;
	// targetStart assumed to be the rigidBody2d that is attached to this object
	private Rigidbody2D targetStart;
	private Rigidbody2D targetEnd;
	private DistanceJoint2D distanceLimiter;
	private DistanceJoint2D anchorJoint;
	private static int numLinks;

	// All essential intialization after the component is instantiated and before the component gets enabled.
	void Awake () {
		// get all the necessary components
		distanceLimiter = GetComponent<DistanceJoint2D>();
		// get the Rigidbody2D for the "start" of the link
		targetStart = GetComponent<Rigidbody2D>();
		// set the anchor points for the "end" of the link
		targetEnd = distanceLimiter.connectedBody;
	}

	// Initialization after the component first gets enabled. This happens on the very next Update() call.
	void Start () {
		Quaternion thisRot = Quaternion.LookRotation(new Vector3(0f,0f,1f), DirectionFromAnchors());
		GameObject segment;

		// Make an object to visially represent the link and provide a hitbox.
		segment = (GameObject)Instantiate(segmentPrefab, GetMidPoint(), thisRot);
		actorSegment = segment.GetComponent<LinkSegment>();
		if (!actorSegment) {
			actorSegment = segment.AddComponent<LinkSegment>();
		}
		actorSegment.AttachedObject = gameObject;
		// linkAnchor = (GameObject)Instantiate(anchorPrefab, -distanceLimiter.anchor, childRot, transform); // this will put the anchor object at the opposite of the starting anchor.
	}
	
	// Called once per frame. The amount of code that is run in this function will effect performance the most.
	void Update ()
	{
	}

	// get the point in world space that is the "start" of this link
	public Vector3 GetStartPoint()
	{
		return transform.TransformPoint(AnchorPointStart);
	}

	// get the point in world space that is the "end" of this link. If no end is defined, default to right of this object.
	public Vector3 GetEndPoint()
	{
		return targetEnd ? targetEnd.transform.TransformPoint(AnchorPointEnd) : transform.TransformPoint(Vector3.right);
	}

	// get the point in world space that is the midpoint between the connected objects. if there is no connected body, this will return the starting object's position.
	public Vector3 GetMidPoint()
	{
		return targetEnd ? Vector3.Lerp(GetStartPoint(), GetEndPoint(), 0.5f) : transform.position;
	}

	// gets the vector of the line between the two objects in world space.
	private Vector2 VectorFromAnchors()
	{
		// TODO: check to see if another method of checking variables being set or not is needed.
		// default to a vector that is directly to the right.
		return targetEnd ? (GetEndPoint() - GetStartPoint()) : transform.TransformPoint(Vector3.right);
	}

	private float DistanceFromAnchors()
	{
		Vector2 linkLine = VectorFromAnchors();
		// defaults to a distance of 1;
		return linkLine.magnitude;
	}

	private Vector2 DirectionFromAnchors()
	{
		Vector2 linkLine = VectorFromAnchors();
		// defaults to right;
		return linkLine.normalized;
	}

	// get the point in world space that is along the line that represents the link between 2 objects.
	private Vector2 ClosestPointOnAnchorLine(Vector2 pointInWorld)
	{
		Vector2 startPoint = transform.TransformPoint(distanceLimiter.anchor);
		Vector2 anchorLine = VectorFromAnchors();
		float projection;

		projection = Vector2.Dot((pointInWorld - startPoint), anchorLine.normalized);
		projection = Mathf.Clamp(projection, 0f, anchorLine.magnitude);
		// TODO: check if i need to adjust when the anchor is not defined.
		return startPoint + (anchorLine.normalized * projection);
	}

	// counts the amount of hinges starting from this link to the end of the origional link. returns 0 if there is none.
	public int GetNumHinges()
	{
		// int totalHinges = CountHingesToStart(0) + CountHingesToEnd(0);
		// return totalHinges;
		return numLinks;
	}

	// interal function to recursively count the amount of hinges in a link by using the attached dynamic links objects from this link toward the Start of a link.
	private int CountHingesToStart(int count)
	{
		return attachedHead ? attachedHead.CountHingesToStart(count+1) : count;
	}

	// interal function to recursively count the amount of hinges in a link by using the attached dynamic links objects from this link toward the end of a link.
	private int CountHingesToEnd(int count)
	{
		return attachedTail ? attachedTail.CountHingesToEnd(count+1) : count;
	}

	// Break at the point along this link that is closest to "hingePoint" in world space and attach it to a specified "target" GameObject
	// returns the new link GamObject that was made.
	public DynamicLink HingeToObjectAtPoint(GameObject target, Vector2 hingePoint)
	{
		// first check to see we have not exceeded the maximum hinge limit.
		// if (GetNumHinges() >= maxHinges) {
		if (numLinks >= maxHinges) {
			return null;
		}
		numLinks++;
		DynamicLink newScript;
		DistanceJoint2D newJoint;
		// TODO: check if the object has a RigidBody2d and possibly spawn an anchor object if not.
		// transpose the point onto the line between the two conected objects
		hingePoint = ClosestPointOnAnchorLine(hingePoint);
		Vector3 hingePoint3d = hingePoint;
		// figure out the needed distances between the end points and the hinge point.
		float startSegmentDistance = (transform.TransformPoint(distanceLimiter.anchor) - hingePoint3d).magnitude;
		float endSegmentDistance = (transform.TransformPoint(distanceLimiter.connectedAnchor) - hingePoint3d).magnitude;
		float leftoverDistance = (distanceLimiter.distance - DistanceFromAnchors())/2;
		// add a new distance joint and script component to the ending object
		// TODO: manage/check if target has a distance join already or not. Also check if there is a DynamicLink Component and enable it instead?
		newJoint = targetEnd.gameObject.AddComponent<DistanceJoint2D>();
		// copy properties from this distance joint and tranfer them to the new distance joint.
		newJoint.maxDistanceOnly = distanceLimiter.maxDistanceOnly;
		newJoint.autoConfigureDistance = distanceLimiter.autoConfigureDistance;
		newJoint.enableCollision = distanceLimiter.enableCollision;
		// set the new distance for each distance limiter joint
		distanceLimiter.distance = startSegmentDistance + leftoverDistance;
		newJoint.distance = endSegmentDistance + leftoverDistance;
		// add a new script component to the ending object
		newScript = targetEnd.gameObject.AddComponent<DynamicLink>();
		// make sure the new script has the same prefab as this one for the link component
		newScript.segmentPrefab = segmentPrefab;
		// TODO: possibly use a co-routine for monitering when to un-hinge the divided link segments back into the origional.
		// set the starting anchorpoint of the new link to the current links ending anchorpoint
		newScript.AnchorPointStart = AnchorPointEnd;
		// set the new link's end to the targetObject
		// TODO: MUST redesign to use an anchor prefab that has a kinematic rigidbody attached to it.
		//       MAYBE enable colisions on distance joind and add rigidbody to the ground???
		newScript.TargetEnd = target.GetComponent<Rigidbody2D>();
		newScript.AnchorPointEnd = target.transform.InverseTransformPoint(hingePoint);
		// set this targetEnd to the same point
		TargetEnd = newScript.TargetEnd;
		AnchorPointEnd = newScript.AnchorPointEnd;
		// set the new link's attached tail to this link's tail to keep the chain
		newScript.attachedTail = attachedTail;
		// set this link's attched tail to point to the new link script
		attachedTail = newScript;
		// set the new link's attached head to this link script
		newScript.attachedHead = this;
		// TODO: set max distance for this link and the new link to a proper length.
		// TEMP!!! disable update on the new, to see where/how it gets spawned
		// newScript.disableUpdate();
		return newScript;
	}

	public void disableUpdate() {
		if (actorSegment) {
			actorSegment.disableUpdate = true;
		}
	}

	// private void UpdateLink()
	// {
	// 	// Check and Update variables used to connect the two links
	// 	// TODO: look into a better way to keep the kinematic value sysnced with the rigidbody component's value... pointers maybe? some kind of event when its changed, so I can disable components?
	// 	if (thisRigidbody && thisRigidbody.isKinematic != isKinematic) {
	// 		IsKinematic = thisRigidbody.isKinematic;
	// 	}
	// 	LinkTargets();
	// }
	
	// TODO: REDESIGN PLAN -V-below-V-
	// - one script to manage all segments, and segmants of a class with its own functions
	// - The class "LinkSegmant" will contain functions to manage link "hinges" and display of the segments.
	//   - function for updating scale and direction to stretch between endpoints
	//   - function to determine when to hinge a link split it into 2 segments.
	// - function in this script to manage how a link will break and be monitored.
	//   - break will move the segment endpoint to the collided object and create ativate a distance joint and DynamicLink script at ending object
	//     - this DynamicLink will start at the previous ending object and end at the collided object (or anchor object).
	//   - possibly a co-routine which ends when the link goes strait again, and un-hinges the link?
	//   - might have to consider instead spawning an anchor object prefab to link to instead of the collided object that is a child of the collided object.
	//     - maybe only if the collided object does not have a rigid body.
	//   - save a pointer to the new DynamicLink script to interact with it/change max distances?
	//     - include functions to "transfer" forces from the main object to the attached body and vice versa. Something like PullFromStart() and PullFromEnd().
	//     - possibly watch the distances, and transfer the max distances based on player movement.
	//     - another option would be to read forces of one object, and apply it to the other.

	// --- other general use functions to put in a library later ---
	public static Vector2 NearestPointOnFiniteLine(Vector2 start, Vector2 end, Vector2 pnt)
	{
	    var line = (end - start);
	    var len = line.magnitude;
	    line.Normalize();
	   
	    var v = pnt - start;
	    var d = Vector2.Dot(v, line);
	    d = Mathf.Clamp(d, 0f, len);
	    return start + line * d;
	}
}

