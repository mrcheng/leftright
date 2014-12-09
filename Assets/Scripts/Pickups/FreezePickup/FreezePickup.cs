using UnityEngine;
using System.Collections;

public class FreezePickup : MonoBehaviour
{

    // Freezes the player who is in lead for a second

    public int rotatorSpeed;

    // Audio
    public AudioSource snd_pickupFreeze;

    void Update()
    {
        transform.Rotate(new Vector3(1, 1, 1) * Time.deltaTime * rotatorSpeed);
    }

    void OnTriggerEnter(Collider playerCollider)
    {
        snd_pickupFreeze.Play();
        var player = GameManager.Instance.GetPlayerInLead();
        if (player == null)
            print("No player in lead?");
        else
            player.Freeze(1f);
        Destroy(gameObject);
    }
}
