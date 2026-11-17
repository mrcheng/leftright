using UnityEngine;

namespace LeftRight.Rebuild
{
    [DefaultExecutionOrder(50)]
    public sealed class LaneMine : MonoBehaviour
    {
        public int Id { get; private set; }
        public int LaneIndex { get; private set; }
        public double ArmsAtBeat { get; private set; }
        public double ExpiresAtBeat { get; private set; }
        public bool Detonated { get; private set; }
        public float triggerRadius=.32f,jumpClearance=.24f;
        public Transform Lamp;
        public bool IsArmed=>owner.course.round.masterBeat.BeatPosition>=ArmsAtBeat;
        ObstacleSpawner owner;
        float previousX,previousHeight;
        bool sampled;
        public void Initialize(ObstacleSpawner spawner,int id,int lane,double arms,double expires)
        {owner=spawner;Id=id;LaneIndex=lane;ArmsAtBeat=arms;ExpiresAtBeat=expires;}
        public bool CheckContact(RecruitController player)
        {
            if(Detonated||owner.course.NetworkReplica||!IsArmed||owner.course.IsGameOver||
                !owner.course.round.masterBeat.IsPlaying||player!=owner.course.players[LaneIndex]||player.IsPinned||player.IsEliminated)return false;
            float x=player.transform.position.x,height=player.JumpOffset;
            float from=sampled?previousX:x,fromHeight=sampled?previousHeight:height;
            previousX=x;previousHeight=height;sampled=true;
            float left=transform.position.x-triggerRadius,right=transform.position.x+triggerRadius;
            if(Mathf.Max(from,x)<left||Mathf.Min(from,x)>right)return false;
            // Sweep the feet between frames so fast movement cannot tunnel through it.
            // Only the portion overlapping the pressure plate needs jump clearance.
            float start=0,end=1,dx=x-from;
            if(Mathf.Abs(dx)>.00001f){float a=(left-from)/dx,b=(right-from)/dx;start=Mathf.Clamp01(Mathf.Min(a,b));end=Mathf.Clamp01(Mathf.Max(a,b));}
            if(Mathf.Min(Mathf.Lerp(fromHeight,height,start),Mathf.Lerp(fromHeight,height,end))>=jumpClearance)return false;
            Detonated=true;player.Explode(owner.BloodMaterial,owner.ExplosionClip);
            owner.course.traps[LaneIndex].RetractForVictory();Destroy(gameObject);return true;
        }
        void Update()
        {
            var course=owner.course;
            if(Lamp!=null)Lamp.gameObject.SetActive(IsArmed||((int)(course.round.masterBeat.BeatPosition*4)&1)==0);
            if(course.NetworkReplica)return;
            if(course.IsGameOver||course.players[LaneIndex].IsEliminated){Destroy(gameObject);return;}
            if(!course.round.masterBeat.IsPlaying)return;
            var trap=course.traps[LaneIndex];
            if(course.round.masterBeat.BeatPosition>=ExpiresAtBeat||transform.position.x<=trap.LeftTip+.35f||transform.position.x>=trap.RightTip-.35f)
            {Destroy(gameObject);return;}
            if(!IsArmed){sampled=false;return;}
            CheckContact(course.players[LaneIndex]);
        }
    }
}
