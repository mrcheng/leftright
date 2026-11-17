using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
var course=UnityEngine.Object.FindAnyObjectByType<SpikeCourse>();var round=course.round;var director=course.GetComponent<VictoryDirector>();
var checks=new List<string>();void Check(bool condition,string label){if(!condition)throw new Exception(label);checks.Add(label);}
async Task Wait(Func<bool> condition){float until=Time.realtimeSinceStartup+3;while(!condition()&&Time.realtimeSinceStartup<until)await Task.Delay(5);if(!condition())throw new Exception("Timed out resolving winner");}
float lead=round.leadInSeconds,delay=director.victoryDelaySeconds;var speeds=course.players.Select(p=>p.walkSpeed).ToArray();
void FatalContact(int lane){var trap=course.traps[lane];for(int i=0;i<30;i++)trap.MissBeat();trap.Animate(100);course.players[lane].PinTo(trap.leftBank,true,trap.spikeReach);}
try
{
 round.leadInSeconds=.05f;director.victoryDelaySeconds=1;
 foreach(var p in course.players){p.walkSpeed=0;p.InputOverride=0;}
 var winners=new HashSet<int>();
 for(int trial=0;trial<16;trial++)
 {
  round.BeginRound();await Wait(()=>round.HasStarted);
  for(int lane=0;lane<4;lane++)FatalContact(lane);
  Check(course.traps.Select((trap,lane)=>trap.WouldEliminate(course.players[lane])).All(value=>value),"Tie trial "+trial+" presents four simultaneous fatal contacts");
  await Wait(()=>course.IsGameOver);
  var winner=course.Winner;int index=Array.IndexOf(course.players,winner);winners.Add(index);
  Check(winner!=null&&!winner.IsEliminated&&course.AliveCount==1&&course.players.Count(p=>!p.IsEliminated)==1,"Tie trial "+trial+" reserves exactly one live winner before applying deaths");
  Check(!winner.IsPinned&&!winner.ragdoll.IsActive&&course.traps[index].TargetTravel==0,"Tie trial "+trial+" immediately detaches winner and retracts their spikes");
  Check(director.IsPending&&director.SelectedSequence!=null&&!director.IsPlaying,"Tie trial "+trial+" chooses a sequence immediately while retaining the playback delay");
 }
 Check(winners.Count>1,"Repeated ties choose different lanes rather than using array order");
 // Earlier casualties cannot be picked when the final two die together.
 round.BeginRound();await Wait(()=>round.HasStarted);
 course.players[0].Eliminate();course.players[1].Eliminate();await Task.Delay(40);
 FatalContact(2);FatalContact(3);await Wait(()=>course.IsGameOver);
 Check(course.Winner==course.players[2]||course.Winner==course.players[3],"Final two-way tie excludes recruits who died earlier");
 // Last opponent dies just before the already-pinned survivor's fatal contact.
 round.BeginRound();await Wait(()=>round.HasStarted);
 for(int lane=1;lane<4;lane++)course.players[lane].Eliminate();
 FatalContact(0);float before=course.traps[0].leftBank.position.x;
 await Wait(()=>course.IsGameOver);
 Check(course.Winner==course.players[0]&&!course.players[0].IsEliminated,"Near-death last survivor is protected before contact resolution");
 Check(course.traps[0].TargetTravel==0&&!course.players[0].IsPinned,"Winner's pending squeeze is cancelled immediately");
 await Task.Delay(150);
 Check(course.traps[0].leftBank.position.x<before-.1f&&director.IsPending&&!director.IsPlaying,"Spikes visibly retract during the victory delay");
 await Task.Delay(1300);
 Check(!course.players[0].IsEliminated&&!course.players[0].ragdoll.IsActive&&director.IsPlaying,"Near-death winner safely reaches the celebration");
 round.BeginRound();await Wait(()=>round.HasStarted);
 course.players[0].Eliminate();course.players[1].Eliminate();await Task.Delay(40);
 course.players[2].Eliminate();course.players[3].Eliminate();await Wait(()=>course.IsGameOver);
 Check(course.Winner==course.players[2]||course.Winner==course.players[3],"Already-applied external death batch also chooses only its final contenders");
 round.BeginRound();await Task.Delay(60);
 Check(course.Winner==null&&!course.IsGameOver&&!director.IsPending&&course.players.All(p=>!p.IsEliminated),"Restart clears tie history and restores every recruit");
}
finally{round.leadInSeconds=lead;director.victoryDelaySeconds=delay;for(int i=0;i<course.players.Length;i++){course.players[i].walkSpeed=speeds[i];course.players[i].InputOverride=null;}round.BeginRound();}
return new{checksPassed=checks.Count,checks};
