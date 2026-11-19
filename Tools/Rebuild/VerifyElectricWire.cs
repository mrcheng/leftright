using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
var menu=UnityEngine.Object.FindAnyObjectByType<StartMenu>();menu.Open();var course=menu.course;var round=course.round;var obstacles=menu.Obstacles;var health=menu.GetComponentInChildren<HealthCrateSpawner>();
float lead=round.leadInSeconds,reaction=course.reactionBeats,chance=obstacles.electricWireChance;bool automatic=obstacles.automaticSpawning,healthAuto=health.automaticSpawning;int count=menu.PlayerCount,checks=0;
void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;}
async Task Wait(Func<bool> test,float timeout=5){float end=Time.realtimeSinceStartup+timeout;while(!test()&&Time.realtimeSinceStartup<end)await Task.Delay(5);if(!test())throw new Exception("Timed out");}
void Arm(LaneElectricWire w)=>typeof(LaneElectricWire).GetProperty("ArmsAtBeat").SetValue(w,-1d);
try{
 menu.SetPlayerCount(4);round.leadInSeconds=.1f;course.reactionBeats=10000;obstacles.automaticSpawning=false;health.automaticSpawning=false;menu.Play();foreach(var p in course.players)p.InputOverride=0;
 Check(obstacles.SpawnWire(0,1.7f)==null,"No wire during countdown");await Wait(()=>round.HasStarted);
 Check(Resources.Load<AudioClip>("Electrocution")!=null,"Supplied electrical loop loads");
 obstacles.electricWireChance=1;obstacles.automaticSpawning=true;typeof(ObstacleSpawner).GetMethod("OnBeat",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(obstacles,new object[]{(long)obstacles.firstSpawnBeat});obstacles.automaticSpawning=false;
 Check(obstacles.ActiveWires.Count==4&&obstacles.ActiveMines.Count==0,"All four active lanes can receive wires through the shared schedule");
 var wire=obstacles.ActiveWires[0];wire.enabled=false;var player=course.players[wire.LaneIndex];
 Check(wire.GetComponentsInChildren<Renderer>().Length>=12,"Two poles, warning symbols and cable are built");
 Check(obstacles.SpawnMine(wire.LaneIndex,-wire.transform.position.x)==null,"One obstacle slot shared by wire and mine");Check(health.SpawnCrate(wire.LaneIndex,wire.transform.position.x)==null,"Health cannot overlap a wire");
 Check(!wire.CheckContact(player)&&!wire.IsArmed,"Wire warning period is harmless");Arm(wire);Check(!wire.CheckContact(course.players[1]),"Other lanes cannot trigger wire");
 var pos=player.transform.position;pos.x=wire.transform.position.x;player.transform.position=pos;
 Check(player.TryJump(),"Normal jump available");await Task.Delay(300);Check(player.JumpOffset>.38f&&!wire.CheckContact(player),"Jump clears the wire");
 await Wait(()=>!player.IsJumping);Check(wire.CheckContact(player)&&player.IsShocked&&wire.Spent,"Ground contact shocks once and burns wire out");
 double start=player.ShockStartedBeat,endBeat=player.ShockEndsBeat;Check(Math.Abs(endBeat-start-2)<.0001,"Lock uses exactly two masterbeats");
 player.InputOverride=1;float x=player.transform.position.x;Check(!player.TryJump(),"Shock blocks jump");await Task.Delay(40);Check(Mathf.Abs(player.transform.position.x-x)<.001f&&!player.IsMoving,"Shock blocks movement");
 var effect=player.GetComponent<RecruitShock>();Check(effect.GetComponents<AudioSource>().Any(a=>a.clip!=null&&a.clip.name=="Electrocution"&&a.isPlaying),"Shock sound plays");
 Check(!wire.CheckContact(player),"Burned wire cannot repeatedly shock");
 bool skeletonSeen=false,skinSeen=false;while(player.IsShocked){skeletonSeen|=effect.SkeletonVisible;skinSeen|=!effect.SkeletonVisible;await Task.Delay(5);}
 Check(skeletonSeen&&skinSeen,"Skeleton alternates with original mesh");Check(round.masterBeat.BeatPosition>=endBeat&&round.masterBeat.BeatPosition<endBeat+.2,"Recovery follows beat clock");
 await Task.Delay(60);Check(!effect.IsActive&&player.march.enabled,"Normal march restored");Check(player.transform.position.x>x,"Held input resumes after recovery");player.InputOverride=0;
 foreach(var p in course.players){double b=round.masterBeat.BeatPosition;p.ApplyShock(b,b+2);Check(p.IsShocked,"Each recruit model supports shock");p.ClearShock();}
 double now=round.masterBeat.BeatPosition;player.ApplyShock(now,now+2);await Task.Delay(30);round.audioOutput.music.Pause();double paused=round.masterBeat.BeatPosition;await Task.Delay(450);Check(player.IsShocked&&Math.Abs(paused-round.masterBeat.BeatPosition)<.01,"Pausing music pauses shock duration");round.audioOutput.music.UnPause();player.ClearShock();
 now=round.masterBeat.BeatPosition;player.ApplyShock(now,now+2);await Task.Delay(30);player.PinTo(course.traps[0].leftBank,true,course.traps[0].spikeReach);Check(!player.IsShocked&&!effect.IsActive,"Spike pin safely cancels shock before mesh capture");
 menu.Open();await Task.Delay(60);Check(obstacles.ActiveWires.Count==0&&course.players.All(p=>!p.IsShocked)&&menu.GetComponentsInChildren<RecruitShock>().All(s=>!s.IsActive),"Menu clears all wires and shocks");
 menu.PlaySurvival();await Wait(()=>round.HasStarted);player=course.players[0];player.InputOverride=0;now=round.masterBeat.BeatPosition;player.ApplyShock(now,now+2);course.reactionBeats=0;float travel=course.traps[0].TargetTravel;await Task.Delay(120);Check(course.traps[0].TargetTravel==travel,"Forced shock idle does not create wrong-direction penalties");
 var payload=new OnlinePlayerState{shockStart=now,shockEnd=now+2};var copied=JsonUtility.FromJson<OnlinePlayerState>(JsonUtility.ToJson(payload));Check(copied.shockEnd==payload.shockEnd,"Network snapshot retains exact shock bounds");
 player.ClearShock();course.NetworkReplica=true;obstacles.ApplyNetworkWires(new[]{new OnlineWireState{id=900,lane=0,x=2,arms=0,expires=100,spent=true}});Check(obstacles.ActiveWires.Count==1&&obstacles.ActiveWires[0].Spent,"Remote wire reproduces burned state");Check(!obstacles.ActiveWires[0].CheckContact(player),"Remote wire has no independent collision authority");course.NetworkReplica=false;
 return new{checksPassed=checks};
}finally{course.NetworkReplica=false;foreach(var p in menu.GetComponentsInChildren<RecruitController>(true))p.InputOverride=null;menu.Open();menu.SetPlayerCount(count);round.leadInSeconds=lead;course.reactionBeats=reaction;obstacles.electricWireChance=chance;obstacles.automaticSpawning=automatic;health.automaticSpawning=healthAuto;}

