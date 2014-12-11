using UnityEngine;
using System.Collections;

public class HealthPickup : MonoBehaviour
{
    // Gives the player who picks up more time

    public int rotatorSpeed;

    // Audio
    public AudioSource snd_pickupHealth;

    void Update()
    {
        transform.Rotate(new Vector3(1, 1, 1) * Time.deltaTime * rotatorSpeed);
    }

    void OnTriggerEnter(Collider playerCollider)
    {
        snd_pickupHealth.Play();
        var player = playerCollider.GetComponent<PlayerManager>();
        if (player == null)
            print("Collided with unexpected object: " + playerCollider.gameObject);
        else
            player.GiveHealth(1f);
        Destroy(gameObject);
    }
}
