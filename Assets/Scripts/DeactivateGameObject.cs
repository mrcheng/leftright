using UnityEngine;
using System.Collections;

public class DeactivateGameObject : MonoBehaviour {
    void Start () 
    {
        gameObject.SetActive(false);
    }
}
