using UnityEngine;
using System.Collections;

public class FreezePickup : MonoBehaviour {

    public int rotatorSpeed;
    public bool freezePickupIsPickedUp;

    // Audio
    public AudioSource snd_pickupFreeze;

    // reference to player to recieve the freeze message
    //public GameObject player2;

    void Update()
    {
        transform.Rotate(new Vector3(1, 1, 1) * Time.deltaTime * rotatorSpeed);
        if (freezePickupIsPickedUp)
        {
            DestroyPickup();
        }
    }

    void OnTriggerEnter(Collider player)
    {
        snd_pickupFreeze.Play();
        freezePickupIsPickedUp = true;
        // Sends message to PlayerControllerP2.cs when freeze pickup is picked up
        //player2.SendMessage("FreezePickupPickedUp");
    }

    void DestroyPickup()
    {
        gameObject.SetActive(false);
    }
}
