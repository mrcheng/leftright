using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
var checks=new List<string>();
void Check(bool c,string m){if(!c)throw new Exception(m);checks.Add(m);}
var spawner=UnityEngine.Object.FindAnyObjectByType<HealthCrateSpawner>();
var course=spawner.course;var round=course.round;var trap=course.traps[0];var player=course.players[0];
bool wasEnabled=course.enabled;int oldLifetime=spawner.lifetimeBeats;
try
{
 Check(spawner.cratePrefab!=null && spawner.cratePrefab.visual.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterials.All(m=>m!=null)),"Wooden medic crate prefab has valid models and materials");
 round.BeginRound();
 Check(spawner.SpawnCrate(0,2)==null,"Crates cannot spawn during the silent lead-in");
 while(!round.HasStarted)await Task.Delay(10);
 course.enabled=false; // Isolate health recovery from beat penalties while keeping the music clock running.
 foreach(var laneTrap in course.traps) for(int i=0;i<2;i++)laneTrap.MissBeat();
 float until=Time.realtimeSinceStartup+6;
 while(spawner.GetComponentsInChildren<HealthCratePickup>().Length==0 && Time.realtimeSinceStartup<until)await Task.Delay(20);
 Check(spawner.GetComponentsInChildren<HealthCratePickup>().Length==4,"All four eligible lanes get their first health crate on the master beat");
 spawner.automaticSpawning=false;
 foreach(var crate in spawner.GetComponentsInChildren<HealthCratePickup>())UnityEngine.Object.DestroyImmediate(crate.gameObject);
 Check(spawner.SpawnCrate(0,trap.LeftTip)==null,"Spawner rejects unsafe positions inside the spikes");
 for(int i=0;i<4;i++)trap.MissBeat();trap.Animate(2);
 float before=trap.TargetTravel;float leftBefore=trap.leftBank.position.x;float rightBefore=trap.rightBank.position.x;
 var pickup=spawner.SpawnCrate(0,2);
 Check(pickup!=null && spawner.SpawnCrate(0,3)==null,"Only one crate can exist on a lane");
 Check(!pickup.TryCollect(course.players[1]),"A recruit cannot collect another lane's crate");
 Check(!pickup.TryCollect(player),"Crate requires the recruit to reach it");
 var pos=player.transform.position;pos.x=2;player.transform.position=pos;
 Check(pickup.TryCollect(player),"Touching the crate collects it");
 Check(trap.PendingRecoverySteps==3&&Math.Abs(trap.TravelAfterRecovery-(before-3*trap.inwardPerMiss))<.001,"Health queues exactly three missed beats of recovery");
 Check(!pickup.TryCollect(player),"A crate can heal only once");
 Check(round.audioOutput.effects.isPlaying,"Collecting uses the prototype health pickup sound");
 long recoveryBeat=round.masterBeat.BeatIndex+1;
 for(int step=0;step<3;step++)trap.ProcessRecoveryBeat(recoveryBeat+step);
 Check(trap.leftBank.position.x<leftBefore && trap.rightBank.position.x>rightBefore,"Both spike sets retreat visibly after collection");
 trap.RecoverBeats(100);
 Check(trap.TravelAfterRecovery==0,"Healing clamps at the original lane width");
 player.ResetPlayer();
 spawner.lifetimeBeats=1;
 var expiring=spawner.SpawnCrate(0,3);
 round.audioOutput.music.Pause();await Task.Delay(80);
 float y=expiring.visual.localPosition.y;await Task.Delay(700);
 Check(expiring!=null && expiring.visual.localPosition.y==y,"Music pause freezes crate bob and lifetime");
 round.audioOutput.music.UnPause();await Task.Delay(800);
 Check(expiring==null,"Uncollected crates expire in musical beats");
 var pinnedCrate=spawner.SpawnCrate(0,0);
 player.PinTo(trap.leftBank,true,trap.spikeReach);
 Check(!pinnedCrate.TryCollect(player),"Pinned recruits cannot collect health or escape the spikes");
 round.BeginRound();await Task.Delay(60);
 Check(spawner.GetComponentsInChildren<HealthCratePickup>().Length==0,"Restart removes all remaining crates");
}
finally
{
 course.enabled=wasEnabled;spawner.automaticSpawning=true;spawner.lifetimeBeats=oldLifetime;
 round.BeginRound();
}
return new{checksPassed=checks.Count,checks};
