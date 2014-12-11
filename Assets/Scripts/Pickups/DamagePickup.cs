using UnityEngine;
using System.Collections;

public class DamagePickup : MonoBehaviour 
{
    // Inflicts Damage to player who pick it up

    public int rotatorSpeed;

    // Audio
    public AudioSource snd_pickupDamage;

    void Update()
    {
        transform.Rotate(new Vector3(1, 1, 1) * Time.deltaTime * rotatorSpeed);
    }

    void OnTriggerEnter(Collider playerCollider)
    {
        snd_pickupDamage.Play();
        var player = playerCollider.GetComponent<PlayerManager>();
        if (player == null)
            print("Collided with unexpected object: " + playerCollider.gameObject);
        else
            player.InflictDamage(1f);
        Destroy(gameObject);
    }
}
