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
var course=spawner.course;var round=course.round;bool enabled=course.enabled;
HealthCratePickup[] Crates()=>spawner.GetComponentsInChildren<HealthCratePickup>();
async Task Seek(double beat)
{
 var audio=round.audioOutput;
 audio.music.timeSamples=(int)((audio.palette.titleFirstBeatSeconds+beat*60/audio.palette.titleBeatsPerMinute)*audio.music.clip.frequency);
 await Task.Delay(100);
}
try
{
 Check(spawner.firstSpawnBeat==8&&spawner.intervalBeats==16&&spawner.lifetimeBeats==12&&spawner.restoredBeats==3,
    "Saved rules use beat 8, sixteen-beat opportunities, twelve-beat expiry and three-beat recovery");
 round.BeginRound();while(!round.HasStarted)await Task.Delay(10);
 course.enabled=false; // Keep the real music scheduler; isolate it from movement penalties.
 for(int i=0;i<4;i++)course.traps[0].MissBeat();
 for(int i=0;i<2;i++){course.traps[1].MissBeat();course.traps[3].MissBeat();}
 course.players[3].PinTo(course.traps[3].leftBank,true,course.traps[3].spikeReach);
 await Seek(6);
 Check(Crates().Length==0,"No crates appear before the first eight beats");
 await Seek(8.1);
 Check(Crates().Length==2&&Crates().Any(c=>c.LaneIndex==0)&&Crates().Any(c=>c.LaneIndex==1),
    "Every eligible lane receives a guaranteed opportunity rather than competing for a random slot");
 Check(Crates().All(c=>c.LaneIndex!=2&&c.LaneIndex!=3),"Full-width and pinned lanes receive no crate");
 Check(Crates().All(c=>{
  float distance=Mathf.Abs(c.transform.position.x-course.players[c.LaneIndex].transform.position.x);
  var trap=course.traps[c.LaneIndex];
  return distance>=1.5f&&distance<=2.01f&&c.transform.position.x>=trap.LeftTip+.79f&&c.transform.position.x<=trap.RightTip-.79f;
 }),"Crates are 1.5–2 metres away and safely clear of both spike sets");
 var crate=Crates().Single(c=>c.LaneIndex==0);
 var player=course.players[0];var pos=player.transform.position;pos.x=crate.transform.position.x;player.transform.position=pos;
 float before=course.traps[0].TargetTravel;
 Check(crate.TryCollect(player)&&course.traps[0].PendingRecoverySteps==3&&Mathf.Abs(course.traps[0].TravelAfterRecovery-(before-3*course.traps[0].inwardPerMiss))<.001f,
    "A crate reserves three outward steps on upcoming beats");
 for(int i=0;i<2;i++)course.traps[0].MissBeat();
 await Seek(12);
 Check(Crates().All(c=>c.LaneIndex!=0),"Collecting does not reset the lane timer or trigger immediate replacement");
 course.traps[2].MissBeat();
 await Seek(24.1);
 Check(Crates().Count(c=>c.LaneIndex==0)==1,"A damaged lane receives its next opportunity at beat 24");
 Check(Crates().All(c=>c.LaneIndex!=2),"A lane with only one missed beat does not qualify for health");
 Check(Crates().GroupBy(c=>c.LaneIndex).All(g=>g.Count()==1),"Every lane stays capped at one crate");
 foreach(var c in Crates())UnityEngine.Object.DestroyImmediate(c.gameObject);
 player.ResetPlayer();for(int i=0;i<30;i++)course.traps[0].MissBeat();course.traps[0].Animate(20);
 await Seek(40.1);
 Check(Crates().All(c=>c.LaneIndex!=0),"An opportunity is skipped when no safe 1.5–2 metre placement exists");
 round.BeginRound();await Task.Delay(50);
 Check(Crates().Length==0,"Restart clears crates and resets all lane schedules");
}
finally{course.enabled=enabled;round.BeginRound();}
return new{checksPassed=checks.Count,checks};
