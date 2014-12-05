using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DirectionController : MonoBehaviour {

    Animator anim;

    public bool naziDirectionRight = true;
    public bool gameIsRunning = false;
    public float minTimeTurn = .5f;
    public float maxTimeTurn = 5.0f;

    // Gui text
    public Text naziInfo;

    private int naziIdle = Animator.StringToHash("NaziIdle");
    private int naziLeft = Animator.StringToHash("NaziTurnLeft");
    private int naziRight = Animator.StringToHash("NaziTurnRight");
    private float flipTimer = 0.0f; 
    private float timeRange;

    // Audio
    public AudioSource snd_links;
    public AudioSource snd_rechts;

    void Start()
    {
        anim = GetComponent<Animator>();
        anim.Play(naziIdle);
        naziInfo.text = "CHOOSING DIRECTION";
        timeRange = Random.Range(minTimeTurn, maxTimeTurn);
        gameIsRunning = false;
    }

    void Update()
    {
        flipTimer += Time.deltaTime;
        if (flipTimer > timeRange)
        {
            if (gameIsRunning)
                Flip();
            else
                anim.Play(naziIdle);
            flipTimer = 0.0f;
            timeRange = Random.Range(minTimeTurn, maxTimeTurn);
        }
    }

    // Recieves message from GameController when game starts
    void ActivateDirections()
    {
        gameIsRunning = true;
    }

    void Flip()
    {
        if (naziDirectionRight)
        {
            anim.Play(naziLeft);
            //snd_links.Play();
            naziInfo.text = "LEFT";
            naziDirectionRight = false;
        }
        else
        {
            anim.Play(naziRight);
            //snd_rechts.Play();
            naziInfo.text = "RIGHT";
            naziDirectionRight = true;
        }
    }

}
