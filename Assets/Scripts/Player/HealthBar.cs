using UnityEngine;
using System.Collections;

public class HealthBar : MonoBehaviour
{
    private float healthBarHeight = 0.25f;
    private float healthBarThickness = 0.25f;

    public bool TakingDamage
    {
        set
        {
            if (value)
                gameObject.renderer.material.color = new Color(1f, 0.1f, 0.25f, 0.5f);
            else
                gameObject.renderer.material.color = new Color(0.25f, 1, 0.1f, 0.5f);
        }
    }

    public float Health
    {
        set { transform.localScale = new Vector3(value * 10f, healthBarHeight, healthBarThickness); }
    }
}
