using UnityEngine;
using System.Collections;

public class PlayerManager : MonoBehaviour
{
    public DirectionController Nazi;

    PlayerController _playerController;
    HealthBar _healthBar;
    bool _takingDamage;

    void Awake()
    {
        _playerController = GetComponentInChildren<PlayerController>();
        _healthBar = GetComponentInChildren<HealthBar>();
    }

    void Update()
    {
        if (_playerController.playerDirectionRight != Nazi.naziDirectionRight)
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
            _healthBar.InflictDamage();
    }

    void StartTakingDamage()
    {
        _healthBar.TakingDamage = true;
    }

    void StopTakingDamage()
    {
        _healthBar.TakingDamage = false;
    }
}
