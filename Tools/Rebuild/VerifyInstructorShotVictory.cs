using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
var course=UnityEngine.Object.FindAnyObjectByType<SpikeCourse>();var director=course.GetComponent<VictoryDirector>();var round=course.round;
var lens=course.GetComponent<LensSplatter>();var savedPool=director.sequences;var shot=savedPool.OfType<InstructorShotVictory>().Single();
var checks=new List<string>();void Check(bool condition,string label){if(!condition)throw new Exception(label);checks.Add(label);}
async Task Wait(Func<bool> condition,float seconds=3){float until=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<until)await Task.Delay(5);if(!condition())throw new Exception("Timed out waiting for instructor-shot sequence");}
float lead=round.leadInSeconds,delay=director.victoryDelaySeconds;var speeds=course.players.Select(p=>p.walkSpeed).ToArray();
var started=typeof(VictoryDirector).GetField("startedAt",BindingFlags.Instance|BindingFlags.NonPublic);
try
{
 Check(savedPool.Length>=3&&savedPool.OfType<ArmsRaisedVictory>().Count()==1,"Saved pool retains the original sequences with equal 1/N selection");
 Check(shot.pistolPrefab!=null&&shot.gunshot!=null&&shot.bloodMaterial!=null,"Second sequence owns its pistol, sound and blood settings");
 round.leadInSeconds=.05f;director.victoryDelaySeconds=0;director.sequences=new VictorySequence[]{shot};
 foreach(var player in course.players){player.walkSpeed=0;player.InputOverride=0;}
 for(int lane=0;lane<4;lane++)
 {
  round.BeginRound();await Wait(()=>round.HasStarted);
  for(int other=0;other<4;other++)if(other!=lane)course.players[other].Eliminate();
  await Wait(()=>director.IsPlaying);
  var winner=course.players[lane];var march=winner.march;
  var scale=winner.transform.Find("Model orientation").localScale;
  float birth=(float)started.GetValue(director);
  Check(director.SelectedSequence==shot&&!round.officer.enabled&&!round.march.enabled,march.role+" hands officer pose control to its own sequence");
  await Wait(()=>Time.unscaledTime-birth>=1.65f);
  var pistol=GameObject.Find("Victory pistol");
  var hand=round.officer.GetComponentsInChildren<Transform>().Single(t=>t.name=="Fist.R");
  var head=winner.GetComponentsInChildren<Transform>().Single(t=>t.name=="Head");
  Check(Vector3.Distance(pistol.transform.position,hand.position)<.15f,march.role+" draw ends with the pistol attached to the firing hand");
  Check(Vector3.Dot(pistol.transform.forward,(head.position-pistol.transform.position).normalized)>.995f,march.role+" is correctly targeted from its own lane");
  Check(Vector3.Distance(director.gameCamera.transform.position,round.officer.transform.position+Vector3.up*1.55f)<4.5f,march.role+" sequence first zooms in on the instructor");
  int splats=lens.ActiveSplatCount;
  await Wait(()=>Time.unscaledTime-birth>=shot.fireAt+.04f);
  Check(GameObject.Find("Instructor shot victory props").GetComponent<AudioSource>().isPlaying,march.role+" gunshot sound plays when the pistol fires");
  Check(GameObject.Find("Victory bullet")!=null,march.role+" shot has a visible travelling bullet");
  await Wait(()=>UnityEngine.Object.FindAnyObjectByType<ExecutionAftermath>()!=null);
  var aftermath=UnityEngine.Object.FindAnyObjectByType<ExecutionAftermath>();
  Check(aftermath.Blood!=null&&lens.ActiveSplatCount>splats,march.role+" head impact creates blood spray, a puddle and lens splatter");
  Check(course.Winner==winner&&course.AliveCount==1&&!winner.IsEliminated,march.role+" cinematic death preserves the recorded match winner");
  await Wait(()=>aftermath.HasLanded,2);
  Check(Quaternion.Angle(winner.transform.rotation,Quaternion.identity)>70,march.role+" collapses visibly onto its side");
  Check(winner.transform.Find("Model orientation").localScale==scale&&!winner.ragdoll.IsActive,march.role+" corpse keeps normal proportions without spike flattening");
  Check(aftermath.Blood.transform.position.z==course.traps[lane].transform.position.z,march.role+" blood puddle stays on its own lane");
  if(lane==0)
  {
   await Wait(()=>director.ShowRestartPrompt,5);
   Check(UnityEngine.Object.FindAnyObjectByType<ExecutionAftermath>()!=null&&GameObject.Find("Victory pistol")==null,"Corpse and puddle persist through the replay prompt while the pistol is removed");
   Check(round.officer.enabled&&round.march.enabled,"Instructor animation ownership is restored after the sequence");
  }
  director.StartAgain();await Task.Delay(80);
  Check(UnityEngine.Object.FindAnyObjectByType<ExecutionAftermath>()==null&&GameObject.Find("Victory pistol")==null,march.role+" restart removes all sequence props and aftermath");
  Check(course.players.All(p=>!p.IsEliminated&&p.march.enabled)&&round.officer.enabled&&round.march.enabled,march.role+" restart restores every gameplay rig");
 }
 // Cancel during the draw, before any shot: no delayed projectile or blood can appear.
 await Wait(()=>round.HasStarted);for(int lane=1;lane<4;lane++)course.players[lane].Eliminate();await Wait(()=>director.IsPlaying);
 director.StartAgain();await Task.Delay(2500);
 Check(GameObject.Find("Victory pistol")==null&&UnityEngine.Object.FindAnyObjectByType<ExecutionAftermath>()==null,"Cancelling the draw cannot fire a delayed shot into the next round");
}
finally
{
 round.leadInSeconds=lead;director.victoryDelaySeconds=delay;director.sequences=savedPool;
 for(int i=0;i<course.players.Length;i++){course.players[i].walkSpeed=speeds[i];course.players[i].InputOverride=null;}
 round.BeginRound();
}
return new{checksPassed=checks.Count,checks};
