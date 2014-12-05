using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class GameManager : MonoBehaviour {

    // Players
    public GameObject lucy;
    public Vector3 lucySpawnCoordinates;
    public bool lucyDead = false;
    public GameObject mary;
    public Vector3 marySpawnCoordinates;
    public bool maryDead = false;

    // Pickups
    public GameObject healthPickup;
    public GameObject healthPickupP2;

    // Obsticles
    public GameObject obsticleCube;
    public GameObject obsticleCubeP2;

    public bool gameIsRunning = false;

    public float timePlayed;

    // Gui text
    public Text txt_TimePlayed;

    // Audio
    public AudioSource GameStartMusic;
    public AudioSource StartMenuMusic;

    // Buttons
    public GameObject startGameButton;
    public Text startButtonText;

    // To be able to send start message to Directioncontroller.cs
    public GameObject nazi;

	void Start ()
    {
        lucyDead = false;
        maryDead = false;
        startButtonText.text = "START";
        StartMenuMusic.Play();
        gameIsRunning = false;
	}

    // This function is called from StartGameButton
    public void StartGame()
    {
        gameIsRunning = true;
        SpawnPlayer();


        StartCoroutine(SpawnHealthPickup());
        StartCoroutine(SpawnObsticleCube());

        StartCoroutine(SpawnHealthPickupP2());
        StartCoroutine(SpawnObsticleCubeP2()); 

        StartMenuMusic.Stop();
        GameStartMusic.Play();
        startGameButton.SetActive(false);
        nazi.SendMessage("ActivateDirections"); // Sends message to DirectionController.cs to activate directions when game is starting
    }

    void Update()
    {
        if (gameIsRunning)
            GameTimer();
        if (lucyDead && maryDead)
        {
            GameOver();
        }
            
    }

    public void GameTimer()
    {
        timePlayed += Time.deltaTime;
        txt_TimePlayed.text = ("Time Played " + timePlayed);
    }

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
                Instantiate(healthPickup, new Vector3(Random.Range(-9, 9), 0.5f), Quaternion.identity);
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
                Instantiate(healthPickupP2, new Vector3(Random.Range(-9, 9), 0.5f, 1.1f), Quaternion.identity);
            }
        }
    }

    IEnumerator SpawnObsticleCube()
    {
        while (gameIsRunning)
        {
            yield return new WaitForSeconds(Random.Range(10, 20));
            for (var i = 0; i < 1; i++)
            {
                Instantiate(obsticleCube, new Vector3(Random.Range(-9, 9), 0.6f), Quaternion.identity);
            }
        }
    }

    IEnumerator SpawnObsticleCubeP2()
    {
        while (gameIsRunning)
        {
            yield return new WaitForSeconds(Random.Range(10, 20));
            for (var i = 0; i < 1; i++)
            {
                Instantiate(obsticleCubeP2, new Vector3(Random.Range(-9, 9), 0.6f, 1.1f), Quaternion.identity);
            }
        }
    }

    void LucyDead()
    {
        lucyDead = true;
    }

    void MaryDead() 
    {
        maryDead = true;
    }

    void GameOver()
    {
        Application.LoadLevel(Application.loadedLevel);
    }

}
