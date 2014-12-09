using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviour {

    // Players
    public GameObject lucy;
    public GameObject mary;
    public GameObject oxy;
    public GameObject tjack;

    // Pickups
    public GameObject healthPickup;
    public GameObject freezePickup;
    public GameObject speedPickup;

    // Obsticles
    //public GameObject obsticleCube;

    // Gui text
    public Text txt_TimePlayed;

    // Audio
    public AudioSource GameStartMusic;
    public AudioSource StartMenuMusic;

    // Buttons
    public GameObject startGameButton;
    public Text startButtonText;

    // To be able to send start message to Directioncontroller.cs
    public DirectionController Nazi;

    public LaneController[] Lanes;

    public GameObject GameOverButton;
    public Text GameOverText;
    string _gameOverTextTemplate;

    bool _gameIsRunning = false;
    float _timePlayed;
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
        GameOverButton.SetActive(false);
        _gameOverTextTemplate = GameOverText.text;
        startButtonText.text = "START";
        StartMenuMusic.Play();
	}

    // This function is called from StartGameButton
    public void StartGame()
    {
        _gameIsRunning = true;
        SpawnPlayers();

        StartCoroutine(SpawnHealthPickup());
        //StartCoroutine(SpawnObsticleCube());
        StartCoroutine(SpawnFreezePickup());
        StartCoroutine(SpawnSpeedPickup());

        StartMenuMusic.Stop();
        GameStartMusic.Play();
        startGameButton.SetActive(false);
        Nazi.ActivateDirections(); // Sends message to DirectionController.cs to activate directions when game is starting
    }

    void Update()
    {
        if (_gameIsRunning)
            GameTimer();
    }

    public void GameTimer()
    {
        _timePlayed += Time.deltaTime;
        txt_TimePlayed.text = "Time played " + System.Math.Round(_timePlayed, 2);
    }

    void SpawnPlayers()
    {
        var lucyInstance = (GameObject)Instantiate(lucy, Lanes[0].transform.position + new Vector3(0, 5, 0), Quaternion.identity);
        var maryInstance = (GameObject)Instantiate(mary, Lanes[1].transform.position + new Vector3(0, 5, 0), Quaternion.identity);
        var oxyInstance = (GameObject)Instantiate(oxy, Lanes[2].transform.position + new Vector3(0, 5, 0), Quaternion.identity);
        var tjackInstance = (GameObject)Instantiate(tjack, Lanes[3].transform.position + new Vector3(0, 5, 0), Quaternion.identity);
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
        while (_gameIsRunning)
        {
            yield return new WaitForSeconds(Random.Range(1, 10));
            var lane = Lanes[Random.Range(0, Lanes.Length)];
            Instantiate(healthPickup, lane.transform.position + new Vector3(Random.Range(-9, 9), 0.5f), Quaternion.identity);
        }
    }

    //IEnumerator SpawnObsticleCube()
    //{
    //    while (_gameIsRunning)
    //    {
    //        yield return new WaitForSeconds(Random.Range(10, 20));
    //        var lane = Lanes[Random.Range(0, Lanes.Length - 1)];
    //        Instantiate(obsticleCube, lane.transform.position + new Vector3(Random.Range(-9, 9), 0.5f), Quaternion.identity);
    //    }
    //}

    IEnumerator SpawnFreezePickup()
    {
        while (_gameIsRunning)
        {
            yield return new WaitForSeconds(Random.Range(5, 10));
            var lane = Lanes[Random.Range(0, Lanes.Length)];
            Instantiate(freezePickup, lane.transform.position + new Vector3(Random.Range(-9, 9), 0.5f), Quaternion.identity);
        }
    }

    IEnumerator SpawnSpeedPickup()
    {
        while (_gameIsRunning)
        {
            yield return new WaitForSeconds(Random.Range(6, 10));
            var lane = Lanes[Random.Range(0, Lanes.Length)];
            Instantiate(speedPickup, lane.transform.position + new Vector3(Random.Range(-9, 9), 0.5f), Quaternion.identity);
        }
    }

    public void PlayerDied(PlayerManager player)
    {
        // Prints #place of player when dead
		player.LifeText.text += " #" + _activePlayers.Count.ToString();

        _activePlayers.Remove(player);
        if (_activePlayers.Count == 0)
            GameOver(player);
    }

    void GameOver(PlayerManager winner)
    {
        _gameIsRunning = false;
        GameOverText.color = winner.PlayerColor;
        GameOverText.text = string.Format(_gameOverTextTemplate, winner.PlayerName);
        GameOverButton.SetActive(true);
    }

    public void Reset()
    {
        Application.LoadLevel(Application.loadedLevel);
    }

    public PlayerManager GetPlayerInLead()
    {
        PlayerManager lead = null;
        foreach (var player in _activePlayers)
        {
            if (lead == null || player.Health > lead.Health)
                lead = player;
        }
        return lead;
    }
}
