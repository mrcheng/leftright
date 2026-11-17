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

    // Settings menu
    public Slider numberOfPlayerSlider;
    public Text txt_numberOfPlayers;
    public Slider audioLevel;
    public AudioListener audioMasterVolume;

    // Buttons
    public GameObject startGameButton;
    public Text startButtonText;
    public GameObject GameOverButton;

    public GameObject closeSettings;
    public GameObject openSettings;
    public GameObject SettingsScreen;
    public InputField userNameInput;
    public GameObject btn_setUserName;

    public GameObject closeStart;
    public GameObject openStart;
    public GameObject StartScreen;

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
        txt_commentator.text = "Prepare to follow your fearless leader! Press START to obey...";

    }

	void Start ()
    {
        StartScreen.SetActive(false); // this should be active first time when starting game
        SettingsScreen.SetActive(false);
        GameOverButton.SetActive(false);
        startButtonText.text = "START";
        StartMenuMusic.Play();
        userNameInput.text = PlayerPrefs.GetString("UserName");
        HighScore.text = "Highscore: " + PlayerPrefs.GetFloat("HighScore") + (" ") + PlayerPrefs.GetString("UserName"); // Hmmmmm
        numberOfPlayerSlider.minValue = 1;
        numberOfPlayerSlider.maxValue = Mathf.Min(PlayerTemplates.Length, Lanes.Length);
        numberOfPlayerSlider.wholeNumbers = true;
        numberOfPlayerSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("NumberOfPlayers", numberOfPlayerSlider.value));
        SetNumbersOfPlayers();
        audioLevel.value = PlayerPrefs.GetFloat("AudioLevel", audioLevel.value);
        AudioListener.volume = audioLevel.value;
	}

    // Called from settings button in game window
    public void OpenSettings()
    {
        SettingsScreen.SetActive(true);
        _gameIsRunning = false;
        HighScore.text = "Highscore: " + PlayerPrefs.GetFloat("HighScore") + (" ") + PlayerPrefs.GetString("UserName"); // Hmmmmm
    }

    // Called from close settings button in settings window
    public void CloseSettings()
    {
        SettingsScreen.SetActive(false);
    }

    // Called from start menu button in game window
    public void OpenStart()
    {
        StartScreen.SetActive(true);
        _gameIsRunning = false;
        //HighScore.text = "Highscore: " + PlayerPrefs.GetFloat("HighScore") + (" ") + PlayerPrefs.GetString("UserName"); // Hmmmmm
    }

    // Called from close start menu button in settings window
    public void CloseStart()
    {
        StartScreen.SetActive(false);
    }

    // Called from clear high score button in settings window
    public void ClearHighScore()
    {
        PlayerPrefs.DeleteKey("HighScore");
        HighScore.text = "Highscore: 0";
    }

    // Called from 'ok' button in settings window
    public void SetUserName()
    {
        PlayerPrefs.SetString("UserName", userNameInput.text);
    }

    // From 'numbers of players' slider in settings window
    public void SetNumbersOfPlayers()
    {
        int count = SelectedPlayerCount;
        numberOfPlayerSlider.SetValueWithoutNotify(count);
        txt_numberOfPlayers.text = count.ToString();
        PlayerPrefs.SetFloat("NumberOfPlayers", count);
        if (_activePlayers == null) ApplyPlayerCount(count);
    }

    public void SetAudioLevel()
    {
        PlayerPrefs.SetFloat("AudioLevel", audioLevel.value);
        AudioListener.volume = audioLevel.value;
    }

    // This function is called from StartGameButton
    public void StartGame()
    {
        if (_activePlayers != null) return;
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
        openStart.SetActive(false);
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

    GameObject[] PlayerTemplates => new[] { lucy, mary, oxy, tjack };

    int SelectedPlayerCount => Mathf.Clamp(Mathf.RoundToInt(numberOfPlayerSlider.value),
        1, Mathf.Min(PlayerTemplates.Length, Lanes.Length));

    void ApplyPlayerCount(int count)
    {
        for (int i = 0; i < Lanes.Length; i++)
        {
            bool participating = i < count;
            Lanes[i].gameObject.SetActive(participating);
        }
    }

    void SpawnPlayers()
    {
        int count = SelectedPlayerCount;
        ApplyPlayerCount(count);
        var templates = PlayerTemplates;
        _activePlayers = new List<PlayerManager>(count);
        for (int i = 0; i < count; i++)
        {
            var instance = Instantiate(templates[i], Lanes[i].transform.position + new Vector3(0, 5, 0), Quaternion.identity);
            instance.GetComponent<PlayerController>().PlayerIndex = i;
            var player = instance.GetComponent<PlayerManager>();
            player.Lane = Lanes[i];
            _activePlayers.Add(player);
        }
    }

    IEnumerator SpawnHealthPickup()
    {
        while (_gameIsRunning)
        {
            yield return new WaitForSeconds(Random.Range(1, 10));
            if (!_gameIsRunning || _activePlayers.Count == 0) yield break;
            var lane = _activePlayers[Random.Range(0, _activePlayers.Count)].Lane;
            Instantiate(healthPickup, lane.transform.position + new Vector3(Random.Range(-9, 9), 0.5f), Quaternion.identity);
        }
    }

    IEnumerator SpawnFreezePickup()
    {
        while (_gameIsRunning)
        {
            yield return new WaitForSeconds(Random.Range(5, 10));
            if (!_gameIsRunning || _activePlayers.Count == 0) yield break;
            var lane = _activePlayers[Random.Range(0, _activePlayers.Count)].Lane;
            Instantiate(freezePickup, lane.transform.position + new Vector3(Random.Range(-9, 9), 0.5f), Quaternion.identity);
        }
    }

    IEnumerator SpawnSpeedPickup()
    {
        while (_gameIsRunning)
        {
            yield return new WaitForSeconds(Random.Range(6, 10));
            if (!_gameIsRunning || _activePlayers.Count == 0) yield break;
            var lane = _activePlayers[Random.Range(0, _activePlayers.Count)].Lane;
            Instantiate(speedPickup, lane.transform.position + new Vector3(Random.Range(-9, 9), 0.5f), Quaternion.identity);
        }
    }

    IEnumerator SpawnDamagedPickup()
    {
        while (_gameIsRunning)
        {
            yield return new WaitForSeconds(Random.Range(6, 12));
            if (!_gameIsRunning || _activePlayers.Count == 0) yield break;
            var lane = _activePlayers[Random.Range(0, _activePlayers.Count)].Lane;
            Instantiate(damagePickup, lane.transform.position + new Vector3(Random.Range(-9, 9), 0.5f), Quaternion.identity);
        }
    }

    IEnumerator SpawnDizzyPickup()
    {
        while (_gameIsRunning)
        {
            yield return new WaitForSeconds(Random.Range(6, 12));
            if (!_gameIsRunning || _activePlayers.Count == 0) yield break;
            var lane = _activePlayers[Random.Range(0, _activePlayers.Count)].Lane;
            Instantiate(dizzyPickup, lane.transform.position + new Vector3(Random.Range(-9, 9), 0.5f), Quaternion.identity);
        }
    }

    public void PlayerDied(PlayerManager player)
    {
        txt_commentator.text = player.PlayerName + " died! #" + _activePlayers.Count;
        _activePlayers.Remove(player);
        if (_activePlayers.Count == 0)
            GameOver(player);
    }

    void GameOver(PlayerManager winner)
    {
        _gameIsRunning = false;
        GameOverText.color = winner.PlayerColor;
        GameOverButton.SetActive(true);
        openStart.SetActive(true);
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
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
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
