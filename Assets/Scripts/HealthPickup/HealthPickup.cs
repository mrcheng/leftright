using UnityEngine;
using System.Collections;

public class HealthPickup : MonoBehaviour {

    public int rotatorSpeed;
    public bool healthPickupIsPickedUp;

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
        healthPickupIsPickedUp = true;
        // Sends message to HealthBar.cs when health pickup is picked up
        player.gameObject.BroadcastMessage("HealthPickupPickedUp");
    }

    void DestroyPickup()
    {
        gameObject.SetActive(false);
    }
}
