using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class FreezePickup : MonoBehaviour
{

    // Freezes the player who is in lead for a while

    public int rotatorSpeed;
    public Text txt_commentator;

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
        {
            player.Freeze(2f);
            txt_commentator.text = player.PlayerName + " got freezed!";
        }
        Destroy(gameObject);
    }
}
