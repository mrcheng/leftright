using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class GameManager : MonoBehaviour {

    // Players
    public GameObject lucy;
    public Vector3 lucySpawnCoordinates;
    public GameObject mary;
    public Vector3 marySpawnCoordinates;

    // Pickups
    public GameObject healthPickup;
    public GameObject healthPickupP2;

    public bool gameIsRunning = true;

    public float timePlayed;

    // Gui text
    public Text txt_TimePlayed;

	void Start ()
    {
        SpawnPlayer();
        StartCoroutine(SpawnHealthPickup());
        StartCoroutine(SpawnHealthPickupP2());
	}

    void Update()
    {
        GameTimer();
    }

    public void GameTimer()
    {
        timePlayed += Time.deltaTime;
        txt_TimePlayed.text = ("Time Played " + timePlayed);
    }

    //public void StopTimer()
    //{
    //    gameIsRunning = false;
    //}

    void SpawnPlayer()
    {
        Instantiate(lucy, lucySpawnCoordinates, Quaternion.identity);
        Instantiate(mary, marySpawnCoordinates, Quaternion.identity);
    }

    IEnumerator SpawnHealthPickup()
    {
        while (gameIsRunning)
        {
            yield return new WaitForSeconds(Random.Range(5, 10));
            var numOfHealthPickups = Random.Range(0,2);
            for (var i = 0; i < numOfHealthPickups; i++)
            {
                var clone = Instantiate(healthPickup, new Vector3(Random.Range(-9, 9), 0.5f), Quaternion.identity);
            }

        }
    }

    IEnumerator SpawnHealthPickupP2()
    {
        while (gameIsRunning)
        {
            yield return new WaitForSeconds(Random.Range(5, 10));
            var numOfHealthPickups = Random.Range(0, 2);
            for (var i = 0; i < numOfHealthPickups; i++)
            {
                var clone = Instantiate(healthPickupP2, new Vector3(Random.Range(-9, 9), 0.5f, 1.1f), Quaternion.identity);
            }

        }
    }
    
}
