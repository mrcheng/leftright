using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class HealthBar : MonoBehaviour {

    public float healthBarLength = 10;
    private float healthBarHeight = 0.25f;
    private float healthBarThickness = 0.25f;

    // Gui text
    public Text txt_LucyLife;

    void Update()
    {
        if (GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerController>().isDamaged)
        {
            if (healthBarLength <= 0.0f)
            {
                PlayerDead();
            }
            else
            {
                InflictDamage();
            }
        }
    }

    public void InflictDamage()
    {
        healthBarLength -= Time.deltaTime;
        transform.localScale = new Vector3(healthBarLength, healthBarHeight, healthBarThickness);
        txt_LucyLife.text = "Lucy time left: " + healthBarLength;
    }

    public void PlayerDead()
    {
        // Sends message to PlayerController.cs
        gameObject.SendMessageUpwards("PlayerDied");
        txt_LucyLife.text = "LUCY DEAD :(";
    }

    // Recieves message from HealthPickup.cs when health pickup is picked up
    public void HealthPickupPickedUp()
    {
        healthBarLength += 1; // Gives 1 second extra time when recieving health pickup
    }
}


