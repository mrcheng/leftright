using UnityEngine;
using System.Collections;

public class LaneController : MonoBehaviour {

    // Recieves message from method PlayerDied() in PlayerController.cs when player dies
    void RemoveLane()
    {
        rigidbody.useGravity = true;
        rigidbody.isKinematic = false;
    }
    void RemoveLane2()
    {
        rigidbody.useGravity = true;
        rigidbody.isKinematic = false;
    }
}
