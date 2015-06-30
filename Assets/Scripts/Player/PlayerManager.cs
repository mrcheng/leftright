using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class PlayerManager : MonoBehaviour
{
    public DirectionController Nazi;
    public LaneController Lane;

    // Master health bars
    public Transform HealthBar;

    // Texts
    public Text LifeText;
    public Text txt_commentator;
    public string PlayerName;

    // Sets the player color for game over text
    public Color PlayerColor;
    public GameObject meshRenderer; // To be able to get the color from mesh to do crazy color effects ;)

    public float HealthInSeconds = 10f;

    PlayerController _playerController;
    HealthBar _healthBar;
    float _health;

    public float Health
    {
        get { return _health; }
    }
    bool _takingDamage;
    bool _isDead;

    Vector3 _healthBarOriginalScale;

    void Awake()
    {
        _playerController = GetComponentInChildren<PlayerController>();
        _healthBar = GetComponentInChildren<HealthBar>();
        _health = 1f;
    }

    void Start()
    {
        _healthBarOriginalScale = HealthBar.localScale;
    }

    void Update()
    {
        if (_isDead)
            return;

        if (!Nazi.IsIdling &&
            (_playerController.playerDirectionRight != Nazi.naziDirectionRight ||
                _playerController.playerStandingStill))
        {
            if (!_takingDamage)
            {
                _takingDamage = true;
                StartTakingDamage();
            }
        }
        else
        {
            if (_takingDamage)
            {
                _takingDamage = false;
                StopTakingDamage();
            }
        }

        if (_takingDamage)
        {
            _health -= Time.deltaTime / HealthInSeconds;
			UpdateHealth();
			if (_health < 0f)
            {
                _health = 0f;
                Died();
            }
        }
    }

    void UpdateHealth()
    {
        LifeText.text = PlayerName + " time left: " + System.Math.Round(_health * HealthInSeconds, 2);
        _healthBar.Health = _health;
        HealthBar.localScale = new Vector3(_healthBarOriginalScale.x * _health, _healthBarOriginalScale.y, _healthBarOriginalScale.z);
    }

    void StartTakingDamage()
    {
        _healthBar.TakingDamage = true;
    }

    void StopTakingDamage()
    {
        _healthBar.TakingDamage = false;
    }

    void Died()
    {
        _isDead = true;
        Lane.RemoveLane();
		LifeText.text = PlayerName.ToUpper() + " DEAD :(";
        GameManager.Instance.PlayerDied(this);
    }

    public void GiveHealth(float seconds)
    {
        _health += seconds / HealthInSeconds;
        txt_commentator.text = PlayerName + " recieved health!";
    }

    public void Freeze(float seconds)
    {
        StartCoroutine(FreezeCoroutine(seconds));
    }

    public void Speed(float seconds)
    {
        StartCoroutine(SpeedCoroutine(seconds));
    }

    public void InflictDamage(float seconds)
    {
        _health -= seconds / HealthInSeconds;
        txt_commentator.text = PlayerName + " got damaged!";
    }

    public void Dizzy(float seconds)
    {
        StartCoroutine(DizzyCoroutine(seconds));
    }

    IEnumerator FreezeCoroutine(float seconds)
    {
        float oldWalkSpeed = _playerController.walkSpeed;
        float oldRunSpeed = _playerController.runSpeed;
        _playerController.walkSpeed = 0f;
        _playerController.runSpeed = 0f;
        yield return new WaitForSeconds(seconds);
        _playerController.walkSpeed = oldWalkSpeed;
        _playerController.runSpeed = oldRunSpeed;
    }

    IEnumerator SpeedCoroutine(float seconds)
    {
        float oldWalkSpeed = _playerController.walkSpeed;
        float oldRunSpeed = _playerController.runSpeed;
        _playerController.walkSpeed = 6f;
        _playerController.runSpeed = 12f;
        yield return new WaitForSeconds(seconds);
        _playerController.walkSpeed = oldWalkSpeed;
        _playerController.runSpeed = oldRunSpeed;
    }

    IEnumerator DizzyCoroutine(float seconds)
    {
        // THIS IS NOT YET IMPLEMENTED!
        print(PlayerName + " is dizzy!!!");

        meshRenderer.GetComponent<Renderer>().material.color = new Color(1, 1, 1, 0); // Player white when dizzy
        yield return new WaitForSeconds(seconds);
        meshRenderer.GetComponent<Renderer>().material.color = PlayerColor; // Player normal color
    }
}
