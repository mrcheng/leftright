using UnityEngine;
using System.Collections;

public class DespawnTimer : MonoBehaviour 
{

    public float _despawnInSeconds = 5;

	void Start () 
    {
        StartCoroutine(DespawnSomething());
	}

    IEnumerator DespawnSomething()
    {
        yield return new WaitForSeconds(_despawnInSeconds);
        Destroy(gameObject);
    }

}
