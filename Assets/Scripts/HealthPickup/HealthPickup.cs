using UnityEngine;
using System.Collections;

public class HealthPickup : MonoBehaviour {

    public int rotatorSpeed;
    public bool healthPickupIsPickedUp;

    // Audio
    public AudioSource snd_pickupHealth;

    void Update()
    {
        transform.Rotate(new Vector3(1, 1, 1) * Time.deltaTime * rotatorSpeed);
        if (healthPickupIsPickedUp)
        {
            DestroyPickup();
        }
    }

    void OnTriggerEnter(Collider player)
    {
        snd_pickupHealth.Play();
        healthPickupIsPickedUp = true;
        // Sends message to HealthBar.cs when health pickup is picked up
        player.gameObject.BroadcastMessage("HealthPickupPickedUp");
    }

    void DestroyPickup()
    {
        gameObject.SetActive(false);
    }
}
