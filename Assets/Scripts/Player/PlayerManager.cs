using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class PlayerManager : MonoBehaviour
{
    public DirectionController Nazi;
    public LaneController Lane;
    // Master health bars
    public RectTransform HealthBar;
    public Text LifeText;
    public string PlayerName;

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

    void Awake()
    {
        _playerController = GetComponentInChildren<PlayerController>();
        _healthBar = GetComponentInChildren<HealthBar>();
        _health = 1f;
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
            if (_health < 0f)
            {
                _health = 0f;
                Died();
            }

            UpdateHealth();
        }
    }

    void UpdateHealth()
    {
        LifeText.text = PlayerName + " time left: " + System.Math.Round(_health * HealthInSeconds, 2);
        _healthBar.Health = _health;
        HealthBar.localScale = new Vector3(_health, 1f, 1f);
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
    }

    public void Freeze(float seconds)
    {
        StartCoroutine(FreezeCoroutine(seconds));
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
}
