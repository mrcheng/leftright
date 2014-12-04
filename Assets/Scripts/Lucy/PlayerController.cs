using UnityEngine;
using System.Collections;

[RequireComponent(typeof(PlayerPhysics))]
public class PlayerController : MonoBehaviour {

    // Player Handling
    public float gravity = 20;
    public float walkSpeed = 8;
    public float runSpeed = 12;
    public float acceleration = 30;
    public float jumpHeight = 12;

    // Health
    public bool isDamaged = false;

    // System
    private float animationSpeed;
    private float currentSpeed;
    private float targetSpeed;
    private Vector2 amountToMove;

    // States
    private bool jumping;
    public bool playerDirectionRight = false;
    public bool playerStandingStill = true;

    // Components
    private PlayerPhysics playerPhysics;
    private Animator animator;
    public GameObject Player1Lane; // to be able to send message to lane when player dies
    public GameObject gameManager;

    //Reference to Nazi
    public DirectionController nazi;

	void Start() 
    {
        playerPhysics = GetComponent<PlayerPhysics>();
        animator = GetComponent<Animator>();
        
        animator.SetBool("Jumping", true);
        animator.SetLayerWeight(1, 1); // headband layer (both layer zero and layer headband has 100%)
        playerDirectionRight = false;
        playerStandingStill = true;
	}

	void Update() 
    {
        // reset acceleration upon collision
        if (playerPhysics.movementStopped)
        {
            targetSpeed = 0;
            currentSpeed = 0;
        }

        // inflict damage if player facing wrong direction
        isDamaged = playerDirectionRight != nazi.naziDirectionRight;

        // If player is touching ground
        if (playerPhysics.grounded)
        {   
            amountToMove.y = 0; //reset gravity when grounded
            animator.SetBool("Jumping", false);

            if (jumping)
            {
                jumping = false;
                animator.SetBool("Jumping", false);
            }

            // jump input
            if (Input.GetButtonDown("JumpP1"))
            {
                amountToMove.y = jumpHeight;
                jumping = true;
                playerStandingStill = true;
                animator.SetBool("Jumping", true);
            }
        }

        // Set animator parameters
        animationSpeed = IncrementTowards(animationSpeed, Mathf.Abs(targetSpeed), acceleration);
        animator.SetFloat("Speed", animationSpeed);

        // Input
        float speed = (Input.GetButton("RunP1")) ? runSpeed : walkSpeed;
        targetSpeed = Input.GetAxisRaw("HorizontalP1") * speed;
        currentSpeed = IncrementTowards(currentSpeed, targetSpeed, acceleration);
            
        // face direction
        float moveDir = Input.GetAxisRaw("HorizontalP1");
        if (moveDir != 0)
        {
            transform.eulerAngles = (moveDir > 0) ? Vector3.up * 180 : Vector3.zero;
            playerDirectionRight = moveDir > 0;
            playerStandingStill = false;
        }
        else
        {
            playerStandingStill = true;
            isDamaged = true; // inflict damage when standing still
        }

        // Set amount to move
        amountToMove.x = currentSpeed;
        amountToMove.y -= gravity * Time.deltaTime;
        playerPhysics.Move(amountToMove * Time.deltaTime);
 
	}

    // Increase n towards target by speed
    private float IncrementTowards(float n, float target, float a)
    {
        if (n == target)
        {
            return n;
        }
        else
        {
            float dir = Mathf.Sign(target - n); // must n be increased or decreased to get closer to target
            n += a * Time.deltaTime * dir;
            return (dir == Mathf.Sign(target - n)) ? n : target;
        }
    }

    // Recieves message from HealthBar.cs
    void PlayerDied()
    {
        // Sends message to LaneController.cs to remove lane on player death
        Player1Lane.SendMessage("RemoveLane");
        // Sends message to GameManager.cs to remove lane on player death
        gameManager.SendMessage("LucyDead");
    }

}