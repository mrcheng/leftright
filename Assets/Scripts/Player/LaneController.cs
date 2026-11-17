using UnityEngine;
using System.Collections;

public class LaneController : MonoBehaviour
{
    Vector3 _originalScale;
    Collider _collider;

    void Awake()
    {
        _originalScale = transform.localScale;
        _collider = GetComponent<Collider>();
    }

    public void SetHealth(float health)
    {
        health = Mathf.Clamp01(health);
        // Scale the solid platform so its visible ends and collision agree.
        _collider.enabled = health > 0f;
        transform.localScale = new Vector3(_originalScale.x * health, _originalScale.y, _originalScale.z);
    }

    public void RemoveLane()
    {
        GetComponent<Rigidbody>().useGravity = true;
        GetComponent<Rigidbody>().isKinematic = false;
    }
}
