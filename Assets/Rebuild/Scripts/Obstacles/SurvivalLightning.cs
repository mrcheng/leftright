using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LeftRight.Rebuild
{
    /// <summary>One lightning strike per Survival lane per round. Randomness chooses
    /// the deadline and two adjacent boards, never a repeating strike schedule.</summary>
    [DefaultExecutionOrder(60)]
    public sealed class SurvivalLightning : MonoBehaviour
    {
        public Vector2 strikeSeconds=new Vector2(30,40);
        [Min(.1f)] public float warningSeconds=3;
        public AudioClip rumbleClip,strikeClip,woodClip;
        public bool HasStruck {get;private set;}
        public bool IsWarning {get;private set;}
        public double StrikeAt {get;private set;}
        public double Elapsed {get;private set;}
        public int BoardCount=>deck?.Boards.Count??0;
        public int BrokenCount=>deck?.Boards.Count(b=>b.broken)??0;
        public float TargetX {get;private set;}
        public float HoleLeft {get;private set;}
        public float HoleRight {get;private set;}
        StartMenu menu;SpikeCourse course;BreakableLane deck;
        readonly System.Random random=new System.Random();
        BreakableLane.Board first,second;
        SharedLightningAudio stormAudio;double previousDsp;
        Material boltMaterial;GameObject bolt;float flashAge;Light flash;
        float lastX,lastHeight;bool sampled;
        static readonly int BaseColor=Shader.PropertyToID("_BaseColor");
        public void Initialize(StartMenu owner)
        {
            menu=owner;course=owner.course;
            if(rumbleClip==null)rumbleClip=Resources.Load<AudioClip>("ThunderRumble");
            if(strikeClip==null)strikeClip=Resources.Load<AudioClip>("LightningStrike");
            if(woodClip==null)woodClip=Resources.Load<AudioClip>("LightningWoodBreak");
            stormAudio=SharedLightningAudio.ForCourse(course);
            course.round.Preparing+=Prepare;course.round.Cancelled+=Clear;
            // A referenced material keeps the unlit shader in stripped web builds.
            boltMaterial=new Material(Resources.Load<Material>("LightningBolt")){name="Lightning bolt"};
        }
        void Prepare()
        {
            Clear();
            if(!menu.IsSurvival||course.NetworkReplica)return;
            // This handler runs after roster reset/repositioning. Rebuild only the
            // live single lane; leave the original meshes untouched in other modes.
            var lane=menu.transform.Find("Player lanes - gameplay anchors/Player lane 1");
            deck=new BreakableLane(lane);
            double minimum=Math.Max(warningSeconds+.1f,Math.Min(strikeSeconds.x,strikeSeconds.y));
            StrikeAt=minimum+random.NextDouble()*Math.Max(0,Math.Max(strikeSeconds.x,strikeSeconds.y)-minimum);
            previousDsp=AudioSettings.dspTime;
        }
        void Update()
        {
            UpdateBolt();
            double now=AudioSettings.dspTime,delta=Math.Max(0,now-previousDsp);previousDsp=now;
            if(deck==null||!menu.IsSurvival||course.NetworkReplica)return;
            if(course.IsGameOver)
            {
                if(IsWarning){first.renderer.SetPropertyBlock(null);second.renderer.SetPropertyBlock(null);IsWarning=false;}
                return;
            }
            if(!course.round.HasStarted||!course.round.masterBeat.IsPlaying)
                return;
            // The audio clock excludes the silent count-in. Accumulating only while
            // music runs also freezes the warning if playback pauses, across loops.
            Elapsed+=delta;
            if(!HasStruck&&!IsWarning&&Elapsed>=StrikeAt-warningSeconds)Warn();
            if(IsWarning)
            {
                var tint=new MaterialPropertyBlock();tint.SetColor(BaseColor,Color.Lerp(Color.white,new Color(1,.55f,.1f),.55f+.4f*Mathf.Sin((float)Elapsed*12)));
                first.renderer.SetPropertyBlock(tint);second.renderer.SetPropertyBlock(tint);
                if(Elapsed>=StrikeAt)Strike();
            }
            if(HasStruck)CheckHole();
        }
        void Warn()
        {
            var trap=course.traps[0];var player=course.players[0];
            var candidates=new List<int>();
            for(int i=0;i<deck.Boards.Count-1;i++)
            {
                var a=deck.Boards[i];var b=deck.Boards[i+1];
                float x=(a.bounds.center.x+b.bounds.center.x)*.5f;
                if(menu.Obstacles.BlocksHealth(0,x)||course.GetComponentsInChildren<HealthCratePickup>().Any(crate=>!crate.IsCollected&&Mathf.Abs(crate.transform.position.x-x)<.8f))continue;
                if(a.bounds.min.x>trap.LeftTip+.4f&&b.bounds.max.x<trap.RightTip-.4f&&Mathf.Abs(x-player.transform.position.x)<4)candidates.Add(i);
            }
            // Once the spikes have almost closed there may be no safe complete pair.
            // Skip the strike rather than hitting a bank or scheduling a second one.
            if(candidates.Count==0){HasStruck=true;return;}
            int index=candidates[random.Next(candidates.Count)];first=deck.Boards[index];second=deck.Boards[index+1];
            TargetX=(first.bounds.center.x+second.bounds.center.x)*.5f;
            IsWarning=true;stormAudio.PlayWarning(rumbleClip);
            // Warning starts when the selected patch is visible, even after a hitch;
            // always allow the full three seconds before it can disappear.
            StrikeAt=Elapsed+warningSeconds;
        }
        public bool BlocksSpawn(float x)=>first!=null&&(IsWarning||first.broken)&&x>=first.bounds.min.x-.5f&&x<=second.bounds.max.x+.5f;
        void Strike()
        {
            IsWarning=false;HasStruck=true;sampled=false;
            HoleLeft=first.bounds.min.x;HoleRight=second.bounds.max.x;
            Vector3 hit=new Vector3(TargetX,first.bounds.max.y,course.players[0].transform.position.z);
            foreach(var board in new[]{first,second})
            {board.renderer.SetPropertyBlock(null);board.broken=true;board.transform.gameObject.AddComponent<FallingLanePlank>().Initialize(random);}
            stormAudio.PlayStrike(strikeClip,woodClip);MakeBolt(hit);
        }
        void CheckHole()
        {
            if(first==null||!first.broken)return;
            var player=course.players[0];float x=player.transform.position.x,height=player.JumpOffset;
            if(player.IsEliminated||player.IsPinned){sampled=false;return;}
            float from=sampled?lastX:x,fromHeight=sampled?lastHeight:height;
            lastX=x;lastHeight=height;sampled=true;
            // Sweep both position and jump clearance so a low-frame-rate crossing
            // cannot step over the hole. A proper jump remains the way across it.
            float left=HoleLeft+.08f,right=HoleRight-.08f;
            if(Mathf.Max(from,x)<left||Mathf.Min(from,x)>right)return;
            float start=0,end=1,dx=x-from;
            if(Mathf.Abs(dx)>.00001f){float a=(left-from)/dx,b=(right-from)/dx;start=Mathf.Clamp01(Mathf.Min(a,b));end=Mathf.Clamp01(Mathf.Max(a,b));}
            if(Mathf.Min(Mathf.Lerp(fromHeight,height,start),Mathf.Lerp(fromHeight,height,end))>=.15f)return;
            player.FallThroughLane();course.traps[0].RetractForVictory();
        }
        void MakeBolt(Vector3 hit)
        {
            bolt=new GameObject("Lightning strike");bolt.transform.SetParent(transform,false);flashAge=0;
            var line=bolt.AddComponent<LineRenderer>();line.sharedMaterial=boltMaterial;line.positionCount=9;line.startWidth=.09f;line.endWidth=.035f;line.useWorldSpace=true;
            for(int i=0;i<9;i++){float t=i/8f;line.SetPosition(i,hit+Vector3.up*(13*(1-t))+new Vector3(i==8?0:(float)(random.NextDouble()-.5)*1.5f,0,0));}
            flash=bolt.AddComponent<Light>();flash.type=LightType.Point;flash.transform.position=hit+Vector3.up*2;flash.range=14;flash.color=new Color(.75f,.85f,1);flash.intensity=5;
        }
        void UpdateBolt()
        {
            if(bolt==null)return;flashAge+=Time.unscaledDeltaTime;
            flash.intensity=5*Mathf.Clamp01(1-flashAge/.2f);
            if(flashAge>.18f){Destroy(bolt);bolt=null;}
        }
        void Clear()
        {
            if(bolt!=null)Destroy(bolt);bolt=null;
            deck?.Dispose();deck=null;first=second=null;HasStruck=IsWarning=sampled=false;Elapsed=StrikeAt=0;
            previousDsp=AudioSettings.dspTime;
        }
        void OnDestroy()
        {if(course!=null){course.round.Preparing-=Prepare;course.round.Cancelled-=Clear;}Clear();if(boltMaterial!=null)Destroy(boltMaterial);}
    }
}
