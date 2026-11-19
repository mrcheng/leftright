using System;
using System.Collections.Generic;
using UnityEngine;

namespace LeftRight.Rebuild
{
    /// <summary>One course-wide shower after lightning, with lane-local puddles.
    /// Timing shares the lightning's paused audio clock, never per-lane timers.
    /// Particle counts are capped; puddles dry after the shower instead of stacking.</summary>
    [DefaultExecutionOrder(70)]
    public sealed class SurvivalRain : MonoBehaviour
    {
        public Vector2 delaySeconds=new Vector2(5,10),durationSeconds=new Vector2(8,12);
        [Min(1)] public float dryingSeconds=12;
        [Range(1,6)] public int puddlesPerLane=3;
        public bool Scheduled {get;private set;}
        public bool IsRaining {get;private set;}
        public double StartsAt {get;private set;}
        public double EndsAt {get;private set;}
        public int PuddleCount=>puddles.Count;
        sealed class Puddle
        {public int lane;public float radius,amount;public Transform shape;public Mesh mesh;}
        readonly List<Puddle> puddles=new List<Puddle>();
        readonly System.Random random=new System.Random();
        StartMenu menu;SurvivalLightning lightning;
        GameObject effects;ParticleSystem rain,drips;Mesh droplet;
        Material water,dropMaterial;double lastClock;float dripTimer;bool finished,paused;
        float Roll(Vector2 range)=>Mathf.Lerp(Mathf.Max(0,Mathf.Min(range.x,range.y)),Mathf.Max(0,Mathf.Max(range.x,range.y)),(float)random.NextDouble());
        public void Initialize(StartMenu owner,SurvivalLightning storm)
        {
            menu=owner;lightning=storm;lightning.Struck+=Schedule;
            owner.course.round.Preparing+=Clear;owner.course.round.Cancelled+=Clear;
            water=Resources.Load<Material>("RainWater");dropMaterial=Resources.Load<Material>("RainDrop");
        }
        void Schedule()
        {
            if(Scheduled||!menu.IsSurvival||menu.course.NetworkReplica)return;
            Scheduled=true;StartsAt=lightning.Elapsed+Roll(delaySeconds);EndsAt=StartsAt+Roll(durationSeconds);lastClock=lightning.Elapsed;
        }
        void Update()
        {
            if(!Scheduled||finished)return;
            var course=menu.course;
            if(course.IsGameOver){StopParticles();return;}
            bool running=course.round.HasStarted&&course.round.masterBeat.IsPlaying;
            if(!running)
            {if(!paused){rain?.Pause();drips?.Pause();paused=true;}return;}
            if(paused){if(IsRaining)rain?.Play();drips?.Play();paused=false;}
            double clock=lightning.Elapsed;float dt=(float)Math.Max(0,clock-lastClock);lastClock=clock;
            if(!IsRaining&&clock>=StartsAt&&clock<EndsAt)BeginRain();
            if(IsRaining&&clock>=EndsAt)
            {IsRaining=false;rain.Stop(true,ParticleSystemStopBehavior.StopEmitting);}
            for(int i=puddles.Count-1;i>=0;i--)
            {
                var p=puddles[i];p.amount=Mathf.MoveTowards(p.amount,IsRaining?1:0,dt/(IsRaining?3:Mathf.Max(1,dryingSeconds)));
                p.shape.localScale=new Vector3(p.amount,1,p.amount);
                if(!IsRaining&&p.amount<=0){Destroy(p.shape.gameObject);Destroy(p.mesh);puddles.RemoveAt(i);}
            }
            // Spilled water drips over the front/back ledge while the puddles remain.
            // Emit into one shared particle pool, rather than a system per puddle.
            dripTimer-=dt;
            if(dripTimer<=0&&puddles.Count>0)
            {
                dripTimer=.09f;var p=puddles[random.Next(puddles.Count)];float side=random.Next(2)==0?-1:1;
                var emit=new ParticleSystem.EmitParams{position=p.shape.position+new Vector3(((float)random.NextDouble()-.5f)*p.radius*p.amount,0,side*.57f),velocity=new Vector3(0,-.3f,side*.15f)};
                drips.Emit(emit,1);
            }
            if(clock>=EndsAt&&puddles.Count==0){finished=true;StopParticles();}
        }
        void BeginRain()
        {
            IsRaining=true;effects=new GameObject("Survival rain and puddles");effects.transform.SetParent(transform,false);
            // Keep rain above the camp, beyond the following camera. Drops passing
            // its near plane otherwise stretch into huge distracting screen ribbons.
            rain=Particles("Rain shower",new Vector3(0,12,menu.course.players[0].transform.position.z+5));
            rain.transform.rotation=Quaternion.Euler(90,0,0);
            var main=rain.main;main.maxParticles=800;main.startLifetime=1.1f;main.startSpeed=13;main.startSize=.018f;
            var shape=rain.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(26,12,.1f);
            var emission=rain.emission;emission.rateOverTime=450;
            var renderer=rain.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=2;renderer.velocityScale=.015f;
            rain.Play();
            drips=Particles("Lane edge drips",Vector3.zero);main=drips.main;main.maxParticles=64;main.startLifetime=.38f;main.startSize=.045f;main.startSpeed=0;main.gravityModifier=1;
            emission=drips.emission;emission.rateOverTime=0;
            droplet=new Mesh{name="Low poly water drop",vertices=new[]{Vector3.up,Vector3.down,Vector3.left,Vector3.right,Vector3.forward,Vector3.back},triangles=new[]{0,4,3,0,3,5,0,5,2,0,2,4,1,3,4,1,5,3,1,2,5,1,4,2}};droplet.RecalculateNormals();
            renderer=drips.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=droplet;drips.Play();
            for(int lane=0;lane<menu.course.players.Length;lane++)CreatePuddles(lane);
        }
        ParticleSystem Particles(string name,Vector3 position)
        {
            var go=new GameObject(name);go.transform.SetParent(effects.transform,false);go.transform.position=position;
            var system=go.AddComponent<ParticleSystem>();system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=system.main;main.playOnAwake=false;main.loop=true;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var renderer=system.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=dropMaterial;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;return system;
        }
        void CreatePuddles(int lane)
        {
            var course=menu.course;var player=course.players[lane];var trap=course.traps[lane];
            if(player.IsEliminated||player.IsPinned)return;
            for(int count=0;count<puddlesPerLane;count++)
            {
                float radius=Mathf.Lerp(.65f,1.05f,(float)random.NextDouble()),x=0;bool found=false;
                for(int attempt=0;attempt<24;attempt++)
                {
                    float min=trap.LeftTip+radius+.25f,max=trap.RightTip-radius-.25f;if(max<=min)break;
                    x=Mathf.Lerp(min,max,(float)random.NextDouble());
                    if(lightning.BlocksSpawn(x)||lightning.BlocksSpawn(x-radius)||lightning.BlocksSpawn(x+radius))continue;
                    if(puddles.Exists(p=>p.lane==lane&&Mathf.Abs(p.shape.position.x-x)<p.radius+radius+.2f))continue;
                    found=true;break;
                }
                if(!found)continue;
                const int sides=12;var vertices=new Vector3[sides+1];var triangles=new int[sides*3];
                for(int i=0;i<sides;i++)
                {
                    float angle=i*Mathf.PI*2/sides,jitter=Mathf.Lerp(.82f,1,(float)random.NextDouble());
                    vertices[i+1]=new Vector3(Mathf.Cos(angle)*radius*jitter,0,Mathf.Sin(angle)*.56f*jitter);
                    triangles[i*3]=0;triangles[i*3+1]=(i+1)%sides+1;triangles[i*3+2]=i+1;
                }
                var mesh=new Mesh{name="Irregular rain puddle",vertices=vertices,triangles=triangles};mesh.RecalculateNormals();
                var go=new GameObject("Slippery puddle - lane "+(lane+1));go.transform.SetParent(effects.transform,false);go.transform.position=new Vector3(x,.675f,player.transform.position.z);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=water;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                go.transform.localScale=new Vector3(0,1,0);puddles.Add(new Puddle{lane=lane,radius=radius,shape=go.transform,mesh=mesh});
            }
        }
        public float WetnessAt(RecruitController player)
        {
            if(player.IsEliminated||player.IsPinned||player.IsTrappedInHole||player.IsJumping||player.NetworkReplica||!menu.IsSurvival||menu.course.IsGameOver)return 0;
            float wetness=0;
            foreach(var p in puddles)
            {
                if(menu.course.players[p.lane]!=player||p.amount<.15f)continue;
                var relative=player.transform.position-p.shape.position;
                float a=relative.x/(p.radius*p.amount),b=relative.z/(.56f*p.amount);
                if(a*a+b*b<=.8f)wetness=Mathf.Max(wetness,p.amount);
            }
            return wetness;
        }
        void StopParticles()
        {IsRaining=false;if(rain!=null)rain.Stop(true,ParticleSystemStopBehavior.StopEmitting);if(drips!=null)drips.Stop(true,ParticleSystemStopBehavior.StopEmitting);}
        void Clear()
        {
            StopParticles();if(effects!=null){effects.SetActive(false);Destroy(effects);}effects=null;rain=drips=null;
            foreach(var p in puddles)if(p.mesh!=null)Destroy(p.mesh);puddles.Clear();if(droplet!=null)Destroy(droplet);
            Scheduled=IsRaining=finished=paused=false;StartsAt=EndsAt=lastClock=0;dripTimer=0;
        }
        void OnDestroy()
        {if(lightning!=null)lightning.Struck-=Schedule;if(menu!=null){menu.course.round.Preparing-=Clear;menu.course.round.Cancelled-=Clear;}Clear();}
    }
}
