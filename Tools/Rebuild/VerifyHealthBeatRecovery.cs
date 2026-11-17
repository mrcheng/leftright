using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
var checks=new List<string>();void Check(bool c,string m){if(!c)throw new Exception(m);checks.Add(m);}
var course=UnityEngine.Object.FindAnyObjectByType<SpikeCourse>();var round=course.round;var trap=course.traps[0];
var spawner=UnityEngine.Object.FindAnyObjectByType<HealthCrateSpawner>();bool automatic=spawner.automaticSpawning;
var speeds=course.players.Select(p=>p.walkSpeed).ToArray();
try
{
 spawner.automaticSpawning=false;
 foreach(var p in course.players){p.InputOverride=0;p.walkSpeed=0;}
 round.BeginRound();while(!round.HasStarted)await Task.Delay(10);
 // Freeze music to isolate initial damage, then collect between two actual musical beats.
 round.audioOutput.music.Pause();for(int i=0;i<8;i++)trap.MissBeat();trap.Animate(3);
 round.audioOutput.music.UnPause();
 while(round.masterBeat.BeatPhase<.3||round.masterBeat.BeatPhase>.55)await Task.Delay(10);
 var player=course.players[0];var crate=spawner.SpawnCrate(0,1.5f);
 var position=player.transform.position;position.x=1.5f;player.transform.position=position;
 float before=trap.TargetTravel,left=trap.leftBank.position.x,right=trap.rightBank.position.x;
 long beat=round.masterBeat.BeatIndex;
 Check(crate.TryCollect(player)&&trap.PendingRecoverySteps==3,"Health pickup queues three individual recovery steps");
 await Task.Delay(40);
 Check(Mathf.Abs(trap.leftBank.position.x-left)<.001f&&Mathf.Abs(trap.rightBank.position.x-right)<.001f,
    "Collecting between beats does not start a continuous retreat");
 for(int step=1;step<=3;step++)
 {
  float until=Time.realtimeSinceStartup+2;
  while(trap.PendingRecoverySteps>3-step&&Time.realtimeSinceStartup<until)await Task.Delay(5);
  Check(round.masterBeat.BeatIndex==beat+step&&round.masterBeat.BeatPhase<.25,
    "Recovery step "+step+" occurs on the next master beat");
  Check(Mathf.Abs(trap.TargetTravel-(before-step*trap.inwardPerMiss))<.001f&&
      Mathf.Abs(trap.leftBank.position.x-(left-step*trap.inwardPerMiss))<.001f&&
      Mathf.Abs(trap.rightBank.position.x-(right+step*trap.inwardPerMiss))<.001f,
      "Step "+step+" moves both banks outward by exactly one increment despite idle input");
  float held=trap.leftBank.position.x;await Task.Delay(70);
  Check(Mathf.Abs(trap.leftBank.position.x-held)<.001f,"Spike banks hold still between recovery beats "+step);
 }
 Check(trap.PendingRecoverySteps==0,"Exactly three steps consume the health reward");
 trap.RecoverBeats(3);round.audioOutput.music.Pause();await Task.Delay(60);
 int pending=trap.PendingRecoverySteps;left=trap.leftBank.position.x;await Task.Delay(650);
 Check(trap.PendingRecoverySteps==pending&&Mathf.Abs(trap.leftBank.position.x-left)<.001f,"Music pause also pauses queued recovery");
 round.BeginRound();
 Check(trap.PendingRecoverySteps==0&&trap.TargetTravel==0,"Restart discards queued recovery");
}
finally
{
 spawner.automaticSpawning=automatic;
 for(int i=0;i<course.players.Length;i++){course.players[i].walkSpeed=speeds[i];course.players[i].InputOverride=null;}
 round.BeginRound();
}
return new{checksPassed=checks.Count,checks};
