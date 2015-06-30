using UnityEngine;
using System.Collections;

public class LaneController : MonoBehaviour
{
    public void RemoveLane()
    {
        GetComponent<Rigidbody>().useGravity = true;
        GetComponent<Rigidbody>().isKinematic = false;
    }
}
