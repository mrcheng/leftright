using UnityEngine;
using System.Collections;

public class LaneController : MonoBehaviour
{
    public void RemoveLane()
    {
        rigidbody.useGravity = true;
        rigidbody.isKinematic = false;
    }
}
