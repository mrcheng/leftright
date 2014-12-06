using UnityEngine;
using System.Collections;

[RequireComponent (typeof(BoxCollider))]
public class PlayerPhysics : MonoBehaviour {

    public LayerMask collisionMask;

    private BoxCollider _collider;
    private Vector3 s; //size
    private Vector3 c; //centre

    private Vector3 originalSize;
    private Vector3 originalCentre;
    private float colliderScale;

    private int collitionDivisionsX = 6; //How many rays is needed to make an accurate collitions
    private int collitionDivisionsY = 10;

    private float skin = .005f; // creates a little space between for the collider to work properly

    [HideInInspector]
    public bool grounded;
    [HideInInspector]
    public bool movementStopped;

    Ray ray;
    RaycastHit hit;

    void Start()
    {
        _collider = GetComponent<BoxCollider>();
        colliderScale = transform.localScale.x;

        originalSize = _collider.size;
        originalCentre = _collider.center;
        SetCollider(originalSize, originalCentre);
    }

    public void Move(Vector2 moveAmount)
    {
        float deltaY = moveAmount.y;
        float deltaX = moveAmount.x;
        Vector3 p = transform.position;

        // Check collitions above and below
        grounded = false;

        for (int i = 0; i < collitionDivisionsX; i++)
        {
            float dir = Mathf.Sign(deltaY);
            float x = (p.x + c.x - s.x / 4) + s.x / (collitionDivisionsX -1) * i; //left,centre and rightmost point of collider (change /4 dependent of size of player)
            float y = p.y + c.y + s.y/2 * dir; //bottom of collider

            ray = new Ray(new Vector3(x, y, p.z), new Vector3(0, dir, 0));
            Debug.DrawRay(ray.origin, ray.direction);
            if (Physics.Raycast(ray, out hit, Mathf.Abs(deltaY) + skin, collisionMask))
            {
                //get distance between player and ground
                float dst = Vector3.Distance(ray.origin, hit.point);
                //Stop players downward movement after coming within skin width of active collider
                if (dst > skin)
                {
                    deltaY = dst * dir - skin * dir;
                }
                else
                {
                    deltaY = 0;
                }
                grounded = true;
                break;
            }
        }

        // Check collitions left and right
        movementStopped = false;
        for (int i = 0; i < collitionDivisionsY; i++)
        {
            float dir = Mathf.Sign(deltaX);
            float x = p.x + c.x + s.x / 4 * dir; //left,centre and rightmost point of collider (change /4 dependent of size of player)
            float y = p.y + c.y - s.y / 2 + s.y / (collitionDivisionsY - 1) * i; //bottom of collider

            ray = new Ray(new Vector3(x, y, p.z), new Vector3(dir, 0, 0));
            //Debug.DrawRay(ray.origin, ray.direction);
            if (Physics.Raycast(ray, out hit, Mathf.Abs(deltaX) + skin, collisionMask))
            {
                //get distance between player and ground
                float dst = Vector3.Distance(ray.origin, hit.point);
                //Stop players downward movement after coming within skin width of active collider
                if (dst > skin)
                {
                    deltaX = dst * dir - skin * dir;
                }
                else
                {
                    deltaX = 0;
                }
                movementStopped = true;
                break;
            }
        }

        /*if (!grounded && !movementStopped)
        {
            Vector3 playerDir = new Vector3(deltaX, deltaY);
            Vector3 o = new Vector3(p.x + c.x + s.x / 4 * Mathf.Sign(deltaX), p.y + c.y + s.y / 4 * Mathf.Sign(deltaY));
            ray = new Ray(o, playerDir.normalized);

            if (Physics.Raycast(ray, Mathf.Sqrt(deltaX * deltaX + deltaY * deltaY), collisionMask))
            {
                grounded = true;
                deltaY = 0;
            }
            //Debug.DrawRay(o, playerDir.normalized);
        }*/

        Vector2 finalTransform = new Vector2(deltaX, deltaY);

        transform.Translate(finalTransform, Space.World);

    }

    // Set collider
    public void SetCollider (Vector3 size, Vector3 centre) 
    {
        _collider.size = size;
        _collider.center = centre;

        s = size * colliderScale;
        c = centre * colliderScale;
    }
}
