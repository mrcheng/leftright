using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
var menu=UnityEngine.Object.FindAnyObjectByType<StartMenu>();var course=menu.course;var round=course.round;
var spawner=UnityEngine.Object.FindAnyObjectByType<HealthCrateSpawner>();
var all=course.GetComponentsInChildren<RecruitController>(true);var speeds=all.Select(p=>p.walkSpeed).ToArray();
float lead=round.leadInSeconds;bool auto=spawner.automaticSpawning;int count=menu.PlayerCount;
var checks=new List<string>();void Check(bool c,string m){if(!c)throw new Exception(m);checks.Add(m);}
async Task Wait(Func<bool> condition,float seconds=3){float until=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<until)await Task.Delay(5);if(!condition())throw new Exception("Timed out waiting for health beat");}
async Task BetweenBeats(){await Wait(()=>round.masterBeat.BeatPhase>.3&&round.masterBeat.BeatPhase<.5);}
async Task Collect(LaneSpikeTrap trap,string label,int expectedPending)
{
 await BetweenBeats();
 var player=course.players[0];var crate=spawner.SpawnCrate(0,1.5f);
 Check(crate!=null,label+" spawns another real health crate on the same lane");
 var position=player.transform.position;position.x=1.5f;player.transform.position=position;
 float left=trap.leftBank.position.x,right=trap.rightBank.position.x;
 Check(crate.TryCollect(player)&&trap.PendingRecoverySteps==expectedPending,label+" queues three more recovery steps");
 Check(!crate.TryCollect(player),label+" cannot collect the same crate twice");
 await Task.Delay(30);
 Check(Mathf.Abs(trap.leftBank.position.x-left)<.001f&&Mathf.Abs(trap.rightBank.position.x-right)<.001f,label+" holds both banks still until the next beat");
}
async Task Recover(LaneSpikeTrap trap,int steps,string label)
{
 float before=trap.TargetTravel,left=trap.leftBank.position.x,right=trap.rightBank.position.x;
 long beat=round.masterBeat.BeatIndex;int start=trap.PendingRecoverySteps;
 for(int step=1;step<=steps;step++)
 {
  await Wait(()=>trap.PendingRecoverySteps<=start-step);
  Check(round.masterBeat.BeatIndex==beat+step,label+" step "+step+" is on the next musical beat");
  Check(Mathf.Abs(trap.TargetTravel-(before-step*trap.inwardPerMiss))<.001f&&
   Mathf.Abs(trap.leftBank.position.x-(left-step*trap.inwardPerMiss))<.001f&&
   Mathf.Abs(trap.rightBank.position.x-(right+step*trap.inwardPerMiss))<.001f,
   label+" step "+step+" moves both banks outward exactly 0.45m despite idle penalties");
 }
}
try
{
 round.leadInSeconds=.1f;spawner.automaticSpawning=false;
 foreach(var p in all){p.InputOverride=0;p.walkSpeed=0;}
 foreach(int roster in new[]{2,4})
 {
  menu.Open();menu.SetPlayerCount(roster);menu.Play();await Wait(()=>round.HasStarted);
  var trap=course.traps[0];round.audioOutput.music.Pause();for(int i=0;i<12;i++)trap.MissBeat();trap.Animate(5);round.audioOutput.music.UnPause();
  await Collect(trap,roster+" players, first pickup",3);await Recover(trap,3,"First pickup");
  Check(trap.PendingRecoverySteps==0,"First pickup finishes all three steps");
  await Collect(trap,roster+" players, second pickup",3);await Recover(trap,3,"Second pickup");
  Check(trap.PendingRecoverySteps==0,"Second pickup finishes all three steps independently");
  // Damage again, then take a second crate while recovery from the first is active.
  round.audioOutput.music.Pause();for(int i=0;i<8;i++)trap.MissBeat();trap.Animate(5);round.audioOutput.music.UnPause();
  await Collect(trap,roster+" players, stacked first pickup",3);await Recover(trap,1,"Stacked first pickup");
  await Collect(trap,roster+" players, stacked second pickup",5);await Recover(trap,5,"Stacked second pickup");
  Check(trap.PendingRecoverySteps==0,"Stacked pickups deliver six total recovery beats without dropping the second reward");
  // More health than remaining damage must stop at the original open position.
  round.audioOutput.music.Pause();trap.RecoverBeats(100);
  long recoveryBeat=round.masterBeat.BeatIndex+1;int pending=trap.PendingRecoverySteps;
  for(int i=0;i<pending;i++)trap.ProcessRecoveryBeat(recoveryBeat+i);
  Check(trap.TargetTravel==0&&trap.PendingRecoverySteps==0,"Repeated health clamps at the fully open lane");
 }
}
finally
{
 round.leadInSeconds=lead;spawner.automaticSpawning=auto;
 for(int i=0;i<all.Length;i++){all[i].walkSpeed=speeds[i];all[i].InputOverride=null;}
 menu.Open();menu.SetPlayerCount(count);
}
return new{checksPassed=checks.Count,checks};
