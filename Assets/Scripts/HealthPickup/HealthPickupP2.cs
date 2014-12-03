using UnityEngine;
using System.Collections;

public class HealthPickupP2 : MonoBehaviour {

    public int rotatorSpeed;
    public bool healthPickupIsPickedUpP2;

    void Update()
    {
        transform.Rotate(new Vector3(1, 1, 1) * Time.deltaTime * rotatorSpeed);
        if (healthPickupIsPickedUpP2)
        {
            DestroyPickup();
        }
    }

    void OnTriggerEnter(Collider player2)
    {
        healthPickupIsPickedUpP2 = true;
        // Sends message to HealthBarP2.cs when health pickup is picked up
        player2.gameObject.BroadcastMessage("HealthPickupPickedUpP2");
    }

    void DestroyPickup()
    {
        gameObject.SetActive(false);
    }
}
