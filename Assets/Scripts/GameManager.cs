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
    public GameObject damagePickup;
    public GameObject dizzyPickup;

    // Score
    public float score;

    // Audio
    public AudioSource GameStartMusic;
    public AudioSource StartMenuMusic;

    // Gui text
    public Text txt_TimePlayed;
    public Text GameOverText;
    public Text HighScore;
    public Text txt_commentator;
    //string _gameOverTextTemplate;

    // Buttons
    public GameObject startGameButton;
    public Text startButtonText;
    public GameObject GameOverButton;

    // To be able to send start message to Directioncontroller.cs
    public DirectionController Nazi;

    public LaneController[] Lanes;

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
        HighScore.text = "Highscore: " + PlayerPrefs.GetFloat("HighScore");
        txt_commentator.text = "Prepare to follow your fearless leader! Press START to obey...";

    }

	void Start ()
    {
        GameOverButton.SetActive(false);
        startButtonText.text = "START";
        StartMenuMusic.Play();
	}

    // This function is called from StartGameButton
    public void StartGame()
    {
        _gameIsRunning = true;

        txt_commentator.text = "Be prepared, I'm choosing direction!";

        SpawnPlayers();

        StartCoroutine(SpawnHealthPickup());
        StartCoroutine(SpawnFreezePickup());
        StartCoroutine(SpawnSpeedPickup());
        StartCoroutine(SpawnDamagedPickup());
        StartCoroutine(SpawnDizzyPickup());

        StartMenuMusic.Stop();
        GameStartMusic.Play();
        startGameButton.SetActive(false);
        Nazi.ActivateDirections(); // Sends message to DirectionController.cs to activate directions when game is starting
    }

    void Update()
    {
        if (_gameIsRunning)
        {
            GameTimer();
            score = _timePlayed;
        }
    }

    public void GameTimer()
    {
        _timePlayed += Time.deltaTime;
        txt_TimePlayed.text = "Time played: " + System.Math.Round(_timePlayed, 2);
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

    IEnumerator SpawnDamagedPickup()
    {
        while (_gameIsRunning)
        {
            yield return new WaitForSeconds(Random.Range(6, 12));
            var lane = Lanes[Random.Range(0, Lanes.Length)];
            Instantiate(damagePickup, lane.transform.position + new Vector3(Random.Range(-9, 9), 0.5f), Quaternion.identity);
        }
    }

    IEnumerator SpawnDizzyPickup()
    {
        while (_gameIsRunning)
        {
            yield return new WaitForSeconds(Random.Range(6, 12));
            var lane = Lanes[Random.Range(0, Lanes.Length)];
            Instantiate(dizzyPickup, lane.transform.position + new Vector3(Random.Range(-9, 9), 0.5f), Quaternion.identity);
        }
    }

    public void PlayerDied(PlayerManager player)
    {
        // Prints #place of player when dead
		player.LifeText.text += " #" + _activePlayers.Count.ToString();
        txt_commentator.text = player.PlayerName + " died!";
        _activePlayers.Remove(player);
        if (_activePlayers.Count == 0)
            GameOver(player);
    }

    void GameOver(PlayerManager winner)
    {
        _gameIsRunning = false;
        GameOverText.color = winner.PlayerColor;
        GameOverButton.SetActive(true);
        // High Score
        if (score > PlayerPrefs.GetFloat("HighScore"))
        {
            PlayerPrefs.SetFloat("HighScore", score);
            txt_commentator.text = "Congratulations " + winner.PlayerName + "!";
            GameOverText.text = winner.PlayerName + " got a new highscore: " + PlayerPrefs.GetFloat("HighScore");
        }
        else 
        {
            GameOverText.text = "RESTART";
            txt_commentator.text = "Congratulations " + winner.PlayerName + "!";
        }
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
