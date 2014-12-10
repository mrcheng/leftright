using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DirectionController : MonoBehaviour
{
    Animator anim;

    public bool naziDirectionRight = true;
    public float minTimeTurn = .5f;
    public float maxTimeTurn = 5.0f;

    // Text
    public Text txt_commentator;

    // Audio
    public AudioSource snd_links;
    public AudioSource snd_rechts;

    bool _isRunning;
    int naziIdle = Animator.StringToHash("NaziIdle");
    int naziLeft = Animator.StringToHash("NaziTurnLeft");
    int naziRight = Animator.StringToHash("NaziTurnRight");
    float flipTimer;
    float timeRange;
    bool _isIdling;
    public bool IsIdling
    {
        get { return _isIdling; }
    }

    void Awake()
    {
        anim = GetComponent<Animator>();
    }

    void Start()
    {
        _isIdling = true;
        anim.Play(naziIdle);
        timeRange = Random.Range(minTimeTurn, maxTimeTurn);
    }

    void Update()
    {
        flipTimer += Time.deltaTime;
        if (flipTimer > timeRange)
        {
            if (_isRunning) 
            {
                txt_commentator.text = "";
                Flip();
            } 
            else
                anim.Play(naziIdle);
            flipTimer = 0.0f;
            timeRange = Random.Range(minTimeTurn, maxTimeTurn);
        }
    }

    // Recieves message from GameController when game starts
    public void ActivateDirections()
    {
        _isRunning = true;
    }

    void Flip()
    {
        _isIdling = false;

        if (naziDirectionRight)
        {
            anim.Play(naziLeft);
            snd_links.Play();
            naziDirectionRight = false;
        }
        else
        {
            anim.Play(naziRight);
            snd_rechts.Play();
            naziDirectionRight = true;
        }
    }
}
