using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviour {

    // Players
    public GameObject lucy;
    public Vector3 lucySpawnCoordinates;
    public GameObject mary;
    public Vector3 marySpawnCoordinates;
    public GameObject oxy;
    public Vector3 oxySpawnCoordinates;
    public GameObject tjack;
    public Vector3 tjackSpawnCoordinates;

    // Pickups
    public GameObject healthPickup;
    public GameObject healthPickupP2;
    public GameObject freezePickup;

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

    List<PlayerManager> _activePlayers;

    static GameManager _instance;
    public static GameManager Instance
    {
        get { return _instance; }
    }

    void Awake()
    {
        _instance = this;
    }

	void Start ()
    {
        startButtonText.text = "START";
        StartMenuMusic.Play();
        gameIsRunning = false;
	}

    // This function is called from StartGameButton
    public void StartGame()
    {
        gameIsRunning = true;
        SpawnPlayers();

        StartCoroutine(SpawnHealthPickup());
        StartCoroutine(SpawnObsticleCube());

        StartCoroutine(SpawnHealthPickupP2());
        StartCoroutine(SpawnObsticleCubeP2());

        StartCoroutine(SpawnFreezePickup());

        StartMenuMusic.Stop();
        //GameStartMusic.Play();
        startGameButton.SetActive(false);
        nazi.SendMessage("ActivateDirections"); // Sends message to DirectionController.cs to activate directions when game is starting
    }

    void Update()
    {
        if (gameIsRunning)
            GameTimer();
    }

    public void GameTimer()
    {
        timePlayed += Time.deltaTime;
        txt_TimePlayed.text = ("Time played " + timePlayed);
    }

    void SpawnPlayers()
    {
        var lucyInstance = (GameObject)Instantiate(lucy, lucySpawnCoordinates, Quaternion.identity);
        var maryInstance = (GameObject)Instantiate(mary, marySpawnCoordinates, Quaternion.identity);
        var oxyInstance = (GameObject)Instantiate(oxy, marySpawnCoordinates, Quaternion.identity);
        var tjackInstance = (GameObject)Instantiate(tjack, marySpawnCoordinates, Quaternion.identity);
        lucyInstance.GetComponent<PlayerController>()._player = Rewired.ReInput.players.GetPlayer(0);
        maryInstance.GetComponent<PlayerController>()._player = Rewired.ReInput.players.GetPlayer(1);
        oxyInstance.GetComponent<PlayerController>()._player = Rewired.ReInput.players.GetPlayer(2);
        tjackInstance.GetComponent<PlayerController>()._player = Rewired.ReInput.players.GetPlayer(3);
        _activePlayers = new List<PlayerManager>
        {
            lucyInstance.GetComponent<PlayerManager>(),
            maryInstance.GetComponent<PlayerManager>(),
            oxyInstance.GetComponent<PlayerManager>(),
            tjackInstance.GetComponent<PlayerManager>()
        };
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

    IEnumerator SpawnFreezePickup()
    {
        while (gameIsRunning)
        {
            yield return new WaitForSeconds(Random.Range(10, 20));
            for (var i = 0; i < 1; i++)
            {
                Instantiate(freezePickup, new Vector3(Random.Range(-9, 9), 0.5f), Quaternion.identity);
            }
        }
    }

    public void PlayerDied(PlayerManager player)
    {
        _activePlayers.Remove(player);
        if (_activePlayers.Count == 0)
            GameOver();
    }

    void GameOver()
    {
        Application.LoadLevel(Application.loadedLevel);
    }
}
