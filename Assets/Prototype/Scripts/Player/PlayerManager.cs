using UnityEngine;
using System.Collections;

public class PlayerManager : MonoBehaviour
{
    public DirectionController Nazi;
    public LaneController Lane;

    // Texts
    public UnityEngine.UI.Text txt_commentator;
    public string PlayerName;

    // Sets the player color for game over text
    public Color PlayerColor;
    public GameObject meshRenderer; // To be able to get the color from mesh to do crazy color effects ;)
    public Material BodyMaterial;

    public float HealthInSeconds = 10f;

    PlayerController _playerController;
    Collider _playerCollider;
    float _fallDeathHeight;
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
        _health = 1f;
    }

    void Start()
    {
        _playerCollider = _playerController.GetComponent<Collider>();
        _fallDeathHeight = Lane.GetComponent<Collider>().bounds.min.y;
        if (BodyMaterial != null) meshRenderer.GetComponent<Renderer>().sharedMaterial = BodyMaterial;
        UpdateHealth();
    }

    void Update()
    {
        if (_isDead)
            return;

        // Once the whole player is below the platform, they cannot land on it.
        // Use its starting height so a falling lane cannot move this boundary.
        if (_playerCollider.bounds.max.y < _fallDeathHeight)
        {
            ChangeHealth(-_health);
            return;
        }

        _takingDamage = !Nazi.IsIdling &&
            (_playerController.playerDirectionRight != Nazi.naziDirectionRight ||
                _playerController.playerStandingStill);

        if (_takingDamage)
        {
            ChangeHealth(-Time.deltaTime / HealthInSeconds);
        }
    }

    void UpdateHealth()
    {
        Lane.SetHealth(_health);
    }

    void ChangeHealth(float amount)
    {
        if (_isDead) return;
        _health = Mathf.Clamp01(_health + amount);
        UpdateHealth();
        if (_health <= 0f) Died();
    }

    void Died()
    {
        _isDead = true;
        Lane.RemoveLane();
        GameManager.Instance.PlayerDied(this);
    }

    public void GiveHealth(float seconds)
    {
        txt_commentator.text = PlayerName + " recieved health!";
        ChangeHealth(seconds / HealthInSeconds);
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
        txt_commentator.text = PlayerName + " got damaged!";
        ChangeHealth(-seconds / HealthInSeconds);
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
