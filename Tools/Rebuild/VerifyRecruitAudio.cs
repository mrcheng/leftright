using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying) throw new Exception("Enter Play Mode first.");
var checks=new List<string>();
void Check(bool c,string m){if(!c)throw new Exception(m);checks.Add(m);}
var course=UnityEngine.Object.FindAnyObjectByType<SpikeCourse>();
course.round.CancelStart();
var players=course.players;
try
{
 Check(players.Select(p=>p.hitAudio.scream).Distinct().Count()==4,"Each recruit has a different scream recording");
 Check(players.All(p=>p.hitAudio.output!=null && !p.hitAudio.output.playOnAwake && !p.hitAudio.output.loop),"Every recruit has an independent non-looping sound source");
 Check(players.All(p=>p.hitAudio.spikeImpact!=null && p.hitAudio.squeeze!=null && p.hitAudio.spikeImpact!=p.hitAudio.squeeze),"Penetration splash and squeeze use distinct clips");
 for(int i=0;i<players.Length;i++)
 {
  var p=players[i];var trap=course.traps[i];var a=p.hitAudio;
  p.PinTo(trap.leftBank,true,trap.spikeReach);
  await Task.Delay(80);
  Check(a.output.isPlaying && a.output.clip==a.scream,p.march.role+" plays its own scream on the first hit");
  float time=a.output.time;
  p.PinTo(trap.leftBank,true,trap.spikeReach);
  await Task.Delay(80);
  Check(a.output.time>time,p.march.role+" contact does not restart the scream every frame");
  p.Eliminate();
  await Task.Delay(50);
  Check(a.output.isPlaying,p.march.role+" squeeze leaves its impact audible");
 }
 Check(players.All(p=>p.hitAudio.output.isPlaying),"Simultaneous recruit effects coexist without cutting one another off");
 course.round.BeginRound();
 Check(players.All(p=>!p.hitAudio.output.isPlaying),"Restart stops old screams and impacts");
 players[0].PinTo(course.traps[0].leftBank,true,course.traps[0].spikeReach);
 await Task.Delay(60);
 Check(players[0].hitAudio.output.isPlaying,"A new round can trigger the same recruit's scream again");
}
finally {course.round.BeginRound();}
return new {checksPassed=checks.Count,checks};
