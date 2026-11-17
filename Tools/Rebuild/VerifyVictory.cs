using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
var course=UnityEngine.Object.FindAnyObjectByType<SpikeCourse>();var round=course.round;
var director=course.GetComponent<VictoryDirector>();var view=course.GetComponent<GameViewSettings>();
var checks=new List<string>();void Check(bool condition,string label){if(!condition)throw new Exception(label);checks.Add(label);}
async Task Wait(Func<bool> condition,float seconds){float end=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<end)await Task.Delay(10);if(!condition())throw new Exception("Timed out waiting for victory state");}
var speeds=course.players.Select(p=>p.walkSpeed).ToArray();var pool=director.sequences;float delay=director.victoryDelaySeconds;
var extra=ScriptableObject.CreateInstance<ArmsRaisedVictory>();
try
{
 Check(director!=null&&pool.Length==1&&pool[0] is ArmsRaisedVictory,"Saved scene installs the first victory asset");
 Check(Mathf.Approximately(delay,1),"Default victory delay is an editable one second");
 foreach(var player in course.players){player.walkSpeed=0;player.InputOverride=0;}
 round.BeginRound();await Wait(()=>round.HasStarted,5);
 for(int i=0;i<4;i++)course.traps[0].MissBeat();
 for(int lane=1;lane<4;lane++)for(int i=0;i<30;i++)course.traps[lane].MissBeat();
 await Wait(()=>course.IsGameOver,9);
 float ended=Time.unscaledTime;
 var winner=course.players[0];var startPosition=winner.transform.position;
 Check(course.Winner==winner&&course.AliveCount==1,"Last surviving recruit wins");
 Check(director.IsPending&&!director.IsPlaying&&director.Winner==winner,"Victory waits after the last opponent dies");
 Check(!winner.march.enabled&&!winner.IsMoving,"Winner stops marching and accepting movement");
 Check(!round.masterBeat.IsPlaying&&!round.officer.IsRunning,"Gameplay music and direction cues stop at round end");
 float cameraDistance=Vector3.Distance(director.gameCamera.transform.position,winner.transform.position);
 await Task.Delay(350);Check(!director.IsPlaying,"Victory does not run before its configured delay");
 await Wait(()=>director.IsPlaying,3);
 Check(Time.unscaledTime-ended>=.9f,"Playback begins after the one-second delay");
 Check(director.SelectedSequence==pool[0]&&view.CinematicActive,"Single sequence is guaranteed and owns the camera");
 await Task.Delay(4500);
 Check(course.traps.All(t=>t.HasReturned),"Dead lanes finish crushing and all spike banks retract during victory");
 Check(!winner.IsEliminated&&Vector3.Distance(winner.transform.position,startPosition)<.001f,"Winner remains alive and stationary throughout celebration");
 Check(Vector3.Distance(director.gameCamera.transform.position,winner.transform.position)<cameraDistance*.8f,"Camera slowly zooms toward the winner");
 Vector3 towardCamera=Vector3.ProjectOnPlane(director.gameCamera.transform.position-winner.transform.position,Vector3.up).normalized;
 Check(Vector3.Dot(-winner.transform.forward,towardCamera)>.99f,"Winner turns to face the camera");
 var hands=winner.GetComponentsInChildren<Transform>().Where(t=>t.name=="Fist.L"||t.name=="Fist.R").ToArray();
 float shoulderHeight=Mathf.Max(winner.march.leftArm.position.y,winner.march.rightArm.position.y);
 Check(hands.Length==2&&hands.All(h=>h.position.y>shoulderHeight+.15f),"Both hands raise above the shoulders");
 var feet=director.gameCamera.WorldToViewportPoint(winner.transform.position);
 var head=director.gameCamera.WorldToViewportPoint(winner.transform.position+Vector3.up*2.3f);
 Check(feet.z>0&&feet.y>.02f&&head.y<.98f,"Close-up keeps the winner's feet and head in frame");
 // Selection test uses the same director path with zero delay and distinct assets.
 var complete=typeof(VictoryDirector).GetMethod("OnCompleted",BindingFlags.Instance|BindingFlags.NonPublic);
 var tick=typeof(VictoryDirector).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
 director.sequences=new VictorySequence[]{pool[0],extra,null,pool[0]};director.victoryDelaySeconds=0;
 Check(director.AvailableSequences().Length==2,"Duplicate and null entries cannot skew equal selection chances");
 var selected=new HashSet<VictorySequence>();
 for(int trial=0;trial<100;trial++){complete.Invoke(director,new object[]{winner});tick.Invoke(director,null);selected.Add(director.SelectedSequence);}
 Check(selected.Count==2,"Both available sequences can be randomly selected");
 extra.available=false;Check(director.AvailableSequences().Length==1,"Unavailable sequences are excluded");
 director.sequences=Array.Empty<VictorySequence>();complete.Invoke(director,new object[]{winner});tick.Invoke(director,null);
 Check(!director.IsPlaying&&!view.CinematicActive,"An empty pool safely skips playback");
 director.sequences=pool;director.victoryDelaySeconds=delay;
 round.BeginRound();await Task.Delay(100);
 Check(!director.IsPlaying&&!director.IsPending&&!view.CinematicActive&&course.Winner==null&&!course.IsGameOver,"Restart clears victory and returns camera control");
 Check(course.players.All(p=>p.march.enabled&&!p.IsEliminated),"Restart restores recruits and their marching rigs");
 await Wait(()=>round.HasStarted,5);
 foreach(var player in course.players)player.Eliminate();
 await Wait(()=>course.IsGameOver,2);
 Check(course.Winner==null&&!director.IsPending&&!director.IsPlaying,"Simultaneous all-dead finish has no victory sequence");
 round.BeginRound();await Wait(()=>round.HasStarted,5);
 for(int lane=1;lane<4;lane++)course.players[lane].Eliminate();
 await Wait(()=>director.IsPending,2);round.BeginRound();await Task.Delay(1200);
 Check(!director.IsPlaying&&!director.IsPending,"Restart during the delay cancels the pending celebration");
}
finally
{
 director.sequences=pool;director.victoryDelaySeconds=delay;
 for(int i=0;i<course.players.Length;i++){course.players[i].walkSpeed=speeds[i];course.players[i].InputOverride=null;}
 round.BeginRound();UnityEngine.Object.Destroy(extra);
}
return new{checksPassed=checks.Count,checks};
