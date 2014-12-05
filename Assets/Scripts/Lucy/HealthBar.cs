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

            gameObject.renderer.material.color = new Color(1f, 0.1f, 0.25f, 0.5f);

            if (healthBarLength <= 0.0f)
            {
                PlayerDead();
            }
            else
            {
                InflictDamage();
            }
        }
        else
        {
            gameObject.renderer.material.color = new Color(0.25f, 1, 0.1f, 0.5f);
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


