using UnityEngine;
namespace LeftRight.Rebuild
{
    [DefaultExecutionOrder(50)]
    public sealed class LaneElectricWire : MonoBehaviour
    {
        public int Id {get;private set;}public int LaneIndex {get;private set;}
        public double ArmsAtBeat {get;private set;}public double ExpiresAtBeat {get;private set;}
        public bool Spent {get;private set;}
        public bool IsArmed=>owner.course.round.masterBeat.BeatPosition>=ArmsAtBeat;
        public Transform cable;ObstacleSpawner owner;float previousX,previousHeight;bool sampled;double burnedAt;
        public void Initialize(ObstacleSpawner spawner,int id,int lane,double arms,double expires)
        {owner=spawner;Id=id;LaneIndex=lane;ArmsAtBeat=arms;ExpiresAtBeat=expires;}
        public bool CheckContact(RecruitController player)
        {
            if(Spent||owner.course.NetworkReplica||!IsArmed||owner.course.IsGameOver||!owner.course.round.masterBeat.IsPlaying||player!=owner.course.players[LaneIndex]||player.IsPinned||player.IsEliminated||player.IsTrappedInHole||player.IsShocked)return false;
            float x=player.transform.position.x,height=player.JumpOffset,from=sampled?previousX:x,fromHeight=sampled?previousHeight:height;
            previousX=x;previousHeight=height;sampled=true;
            float left=transform.position.x-.20f,right=transform.position.x+.20f;
            if(Mathf.Max(from,x)<left||Mathf.Min(from,x)>right)return false;
            float start=0,end=1,dx=x-from;
            if(Mathf.Abs(dx)>.00001f){float a=(left-from)/dx,b=(right-from)/dx;start=Mathf.Clamp01(Mathf.Min(a,b));end=Mathf.Clamp01(Mathf.Max(a,b));}
            // Sweep only the wire's overlap interval, just like mine contact. This
            // remains jumpable at low frame rates and cannot be crossed by tunnelling.
            if(Mathf.Min(Mathf.Lerp(fromHeight,height,start),Mathf.Lerp(fromHeight,height,end))>=.38f)return false;
            double beat=owner.course.round.masterBeat.BeatPosition;player.ApplyShock(beat,beat+2);BurnOut();return true;
        }
        public void BurnOut(){if(Spent)return;Spent=true;burnedAt=owner.course.round.masterBeat.BeatPosition;if(cable!=null)cable.gameObject.SetActive(false);}
        void Update()
        {
            var course=owner.course;double beat=course.round.masterBeat.BeatPosition;
            if(cable!=null&&!Spent)cable.gameObject.SetActive(IsArmed||((int)(beat*4)&1)==0);
            if(course.NetworkReplica)return;
            var trap=course.traps[LaneIndex];
            if(course.IsGameOver||course.players[LaneIndex].IsEliminated||beat>=ExpiresAtBeat||(Spent&&beat>=burnedAt+2)||transform.position.x<=trap.LeftTip+.35f||transform.position.x>=trap.RightTip-.35f){Destroy(gameObject);return;}
            if(!course.round.masterBeat.IsPlaying)return;if(!IsArmed){sampled=false;return;}CheckContact(course.players[LaneIndex]);
        }
    }
}
