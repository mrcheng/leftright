using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class DizzyPickup : MonoBehaviour 
{
    // Makes the player who is in lead dizzy for a while

    public int rotatorSpeed;
    public Text txt_commentator;

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
        {
            player.Dizzy(4f);
            txt_commentator.text = player.PlayerName + " got freezed!";
        }
        Destroy(gameObject);
    }
}
