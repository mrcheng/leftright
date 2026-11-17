using System;
using System.Collections.Generic;
using UnityEngine;

namespace LeftRight.Rebuild
{
    /// <summary>Beat-grid obstacle opportunities, separate from health rewards.
    /// Every active lane gets the same schedule; only safe placement is randomized.</summary>
    public sealed class ObstacleSpawner : MonoBehaviour
    {
        public SpikeCourse course;
        public int firstSpawnBeat=12,intervalBeats=16,lifetimeBeats=12,armingBeats=2;
        public float minimumSpawnDistance=1.5f,maximumSpawnDistance=2f,spikeMargin=.8f;
        public bool automaticSpawning=true;
        public Material BloodMaterial => course.traps[0].bloodMaterial;
        public AudioClip ExplosionClip { get; private set; }
        public IReadOnlyList<LaneMine> ActiveMines => active;
        readonly List<LaneMine> active=new List<LaneMine>();
        readonly System.Random random=new System.Random();
        long[] nextOpportunity;
        int nextId;
        Material metal,ring,pressure,lamp;

        public void Initialize(SpikeCourse owner)
        {
            course=owner;
            metal=Material(new Color(.19f,.23f,.12f));ring=Material(new Color(.65f,.54f,.18f));
            pressure=Material(new Color(.08f,.10f,.06f));lamp=Material(new Color(.9f,.08f,.025f));
            ExplosionClip=Resources.Load<AudioClip>("MineExplosion");
            course.round.Cancelled+=Clear;course.round.masterBeat.Beat+=OnBeat;ResetSchedule();
        }
        Material Material(Color color)=>new Material(Shader.Find("Universal Render Pipeline/Lit")){color=color};
        void ResetSchedule()
        {
            nextOpportunity=new long[course.players.Length];
            for(int i=0;i<nextOpportunity.Length;i++)nextOpportunity[i]=Mathf.Max(0,firstSpawnBeat);
        }
        void OnBeat(long beat)
        {
            if(!automaticSpawning||course.NetworkReplica||!course.round.HasStarted||course.IsGameOver)return;
            if(nextOpportunity.Length!=course.players.Length)ResetSchedule();
            for(int lane=0;lane<course.players.Length;lane++)
            {
                if(beat<nextOpportunity[lane])continue;
                // Consume skipped opportunities too: expiry/death/collection must never
                // produce catch-up waves or let one lane farm additional spawn rolls.
                long first=Mathf.Max(0,firstSpawnBeat),interval=Mathf.Max(1,intervalBeats);
                nextOpportunity[lane]=first+((beat-first)/interval+1)*interval;
                var p=course.players[lane];
                if(p.IsPinned||p.IsEliminated||HasMine(lane))continue;
                if(LaneSpawnPlacement.TryChoose(p,course.traps[lane],minimumSpawnDistance,
                    Mathf.Max(minimumSpawnDistance,maximumSpawnDistance),spikeMargin,random,
                    x=>ClearOfCrates(lane,x),out float x))SpawnMine(lane,x);
            }
        }
        public bool HasMine(int lane)
        {active.RemoveAll(m=>m==null||m.Detonated);return active.Exists(m=>m.LaneIndex==lane);}
        public bool BlocksHealth(int lane,float x)=>active.Exists(m=>m!=null&&!m.Detonated&&m.LaneIndex==lane&&Mathf.Abs(m.transform.position.x-x)<.9f);
        bool ClearOfCrates(int lane,float x)
        {
            foreach(var crate in course.GetComponentsInChildren<HealthCratePickup>())
                if(!crate.IsCollected&&crate.LaneIndex==lane&&Mathf.Abs(crate.transform.position.x-x)<.9f)return false;
            return true;
        }
        public LaneMine SpawnMine(int lane,float x)
        {
            if(lane<0||lane>=course.players.Length||HasMine(lane)||!course.round.HasStarted||course.IsGameOver)return null;
            var p=course.players[lane];var trap=course.traps[lane];
            if(p.IsPinned||p.IsEliminated||x<=trap.LeftTip+spikeMargin||x>=trap.RightTip-spikeMargin||!ClearOfCrates(lane,x))return null;
            double beat=course.round.masterBeat.BeatPosition;
            return Create(++nextId,lane,x,beat+Mathf.Max(1,armingBeats),beat+Mathf.Max(armingBeats+1,lifetimeBeats));
        }
        LaneMine Create(int id,int lane,float x,double arms,double expires)
        {
            var root=new GameObject("Pressure mine - lane "+(lane+1));root.transform.SetParent(transform,false);
            root.transform.position=new Vector3(x,.65f,course.players[lane].transform.position.z);
            var mine=root.AddComponent<LaneMine>();mine.Initialize(this,id,lane,arms,expires);
            Part(root.transform,"Steel casing",new Vector3(0,.065f,0),new Vector3(.54f,.065f,.54f),metal);
            Part(root.transform,"Warning rim",new Vector3(0,.12f,0),new Vector3(.45f,.018f,.45f),ring);
            Part(root.transform,"Pressure plate",new Vector3(0,.15f,0),new Vector3(.33f,.018f,.33f),pressure);
            mine.Lamp=Part(root.transform,"Arming indicator",new Vector3(.17f,.16f,0),new Vector3(.065f,.018f,.065f),lamp);
            active.Add(mine);return mine;
        }
        static Transform Part(Transform parent,string name,Vector3 position,Vector3 size,Material material)
        {
            var part=GameObject.CreatePrimitive(PrimitiveType.Cylinder);part.name=name;part.transform.SetParent(parent,false);
            part.transform.localPosition=position;part.transform.localScale=size;
            var collider=part.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            part.GetComponent<Renderer>().sharedMaterial=material;return part.transform;
        }
        public void ApplyNetwork(OnlineMineState[] values)
        {
            var ids=new HashSet<int>();
            foreach(var value in values??Array.Empty<OnlineMineState>())
            {
                if(value.lane<0||value.lane>=course.players.Length)continue;
                ids.Add(value.id);var mine=active.Find(m=>m!=null&&m.Id==value.id);
                if(mine==null)Create(value.id,value.lane,value.x,value.arms,value.expires);
                else mine.transform.position=new Vector3(value.x,.65f,course.players[value.lane].transform.position.z);
            }
            foreach(var mine in active)if(mine!=null&&!ids.Contains(mine.Id))Destroy(mine.gameObject);
            active.RemoveAll(m=>m==null||!ids.Contains(m.Id));
        }
        void Update(){if(course!=null&&course.IsGameOver)ClearMines();}
        void ClearMines(){foreach(var mine in active)if(mine!=null)Destroy(mine.gameObject);active.Clear();}
        void Clear(){ClearMines();nextId=0;ResetSchedule();}
        void OnDestroy()
        {
            if(course!=null){course.round.Cancelled-=Clear;course.round.masterBeat.Beat-=OnBeat;}
            ClearMines();foreach(var material in new[]{metal,ring,pressure,lamp})if(material!=null)Destroy(material);
        }
    }
}
