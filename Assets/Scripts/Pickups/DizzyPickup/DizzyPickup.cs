using UnityEngine;
using System.Collections;

public class DizzyPickup : MonoBehaviour {

    // Makes the player who is in lead dizzy for a while

    public int rotatorSpeed;

    // Audio
    public AudioSource snd_pickupDizzy;

    void Update()
    {
        transform.Rotate(new Vector3(1, 1, 1) * Time.deltaTime * rotatorSpeed);
    }

    void OnTriggerEnter(Collider playerCollider)
    {
        snd_pickupDizzy.Play();
        var player = GameManager.Instance.GetPlayerInLead();
        if (player == null)
            print("No player in lead?");
        else
            player.Dizzy(4f);
        Destroy(gameObject);
    }
}
