using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class HealthBarP2 : MonoBehaviour {

    public float healthBarP2Length = 10;
    private float healthBarHeight = 0.25f;
    private float healthBarThickness = 0.25f;

    // Gui text
    public Text txt_MaryLife;

    void Update()
    {
        if (GameObject.FindGameObjectWithTag("Player2").GetComponent<PlayerControllerP2>().isDamaged)
        {

            gameObject.renderer.material.color = new Color(1f, 0.1f, 0.25f, 0.5f);

            if (healthBarP2Length <= 0.0f)
            {
                Player2Dead();
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
        healthBarP2Length -= Time.deltaTime;
        transform.localScale = new Vector3(healthBarP2Length, healthBarHeight, healthBarThickness);
        txt_MaryLife.text = "Mary time left: " + healthBarP2Length;
    }

    public void Player2Dead()
    {
        // Sends message to Player2Controller.cs
        gameObject.SendMessageUpwards("Player2Died");
        txt_MaryLife.text = "MARY DEAD :(";
    }

    // Recieves message from HealthPickupP2.cs when health pickup is picked up
    public void HealthPickupPickedUpP2()
    {
        healthBarP2Length += 1; // Gives 1 second extra time when recieving health pickup
    }
}
