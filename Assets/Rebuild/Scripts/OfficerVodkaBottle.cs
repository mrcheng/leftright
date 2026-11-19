using UnityEngine;

namespace LeftRight.Rebuild
{
    /// <summary>Persistent half-filled scene prop. The victory borrows it instead
    /// of spawning a replacement; cancellation and replay restore the same bottle.</summary>
    public sealed class OfficerVodkaBottle : MonoBehaviour
    {
        public GameObject intactBody;
        public Material glassMaterial,liquidMaterial;
        public float gripHeight=.55f;
        public bool IsHeld {get;private set;}
        public bool IsBroken {get;private set;}
        Transform homeParent;Vector3 homePosition,homeScale;Quaternion homeRotation;
        RebuildRoundStart round;
        public void Initialize(RebuildRoundStart owner)
        {
            if(round!=null)return;round=owner;
            homeParent=transform.parent;homePosition=transform.localPosition;homeRotation=transform.localRotation;homeScale=transform.localScale;
            round.Cancelled+=Restore;
        }
        public void AttachToHand(Transform hand,Quaternion rotation)
        {
            IsHeld=true;transform.SetPositionAndRotation(hand.position-rotation*Vector3.up*gripHeight,rotation);
            // Preserve world size, then let the skeleton carry the bottle through
            // one fixed local grip. There is no independent bottle rotation track.
            transform.SetParent(hand,true);
        }
        public void Break()
        {if(IsBroken)return;IsBroken=true;intactBody.SetActive(false);}
        public void Restore()
        {
            if(round==null)return;
            transform.SetParent(homeParent,false);transform.localPosition=homePosition;transform.localRotation=homeRotation;transform.localScale=homeScale;
            intactBody.SetActive(true);IsHeld=IsBroken=false;
        }
        void OnDestroy(){if(round!=null)round.Cancelled-=Restore;}
    }
}
