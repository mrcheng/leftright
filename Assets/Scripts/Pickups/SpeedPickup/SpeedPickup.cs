using UnityEngine;
using System.Collections;

public class SpeedPickup : MonoBehaviour 
{

    // The player who picks up gets increased speed

    public int rotatorSpeed;

    // Audio
    public AudioSource snd_pickupSpeed;

    void Update()
    {
        transform.Rotate(new Vector3(1, 1, 1) * Time.deltaTime * rotatorSpeed);
    }

    void OnTriggerEnter(Collider playerCollider)
    {
        snd_pickupSpeed.Play();
        var player = playerCollider.GetComponent<PlayerManager>();
        if (player == null)
            print("Collided with unexpected object: " + playerCollider.gameObject);
        else
            player.Speed(3f);
        Destroy(gameObject);
    }
}
