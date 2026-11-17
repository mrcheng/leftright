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
var savedPool=director.sequences;var self=savedPool.OfType<InstructorSelfShotVictory>().Single();
var checks=new List<string>();void Check(bool condition,string label){if(!condition)throw new Exception(label);checks.Add(label);}
async Task Wait(Func<bool> condition,float seconds=3){float until=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<until)await Task.Delay(5);if(!condition())throw new Exception("Timed out waiting for instructor self-shot");}
round.CancelStart();
var actor=round.officer.transform;var actorStart=actor.position;var actorRotation=actor.rotation;
float lead=round.leadInSeconds,delay=director.victoryDelaySeconds;
var started=typeof(VictoryDirector).GetField("startedAt",BindingFlags.Instance|BindingFlags.NonPublic);
async Task<float> Win(){round.BeginRound();await Wait(()=>round.HasStarted);for(int lane=1;lane<4;lane++)course.players[lane].Eliminate();await Wait(()=>director.IsPlaying);return (float)started.GetValue(director);}
try
{
 Check(savedPool.Length==3&&savedPool.Distinct().Count()==3,"Three distinct saved assets get equal one-in-three selection chances");
 Check(self.pistolPrefab!=null&&self.shoutClip!=null&&self.gunshot!=null&&self.bloodMaterial!=null,"Third asset owns its dialogue, pistol, gunshot and blood settings");
 Check(!self.PlayOpeningCheer&&savedPool.Where(s=>s!=self).All(s=>s.PlayOpeningCheer),"Only the dialogue-led sequence suppresses the opening cheer");
 var samples=new float[self.shoutClip.samples*self.shoutClip.channels];
 Check(self.shoutClip.GetData(samples,0)&&samples.Max(v=>Mathf.Abs(v))>.2f&&self.shoutClip.length>.7f,"Generated voice line contains audible speech");
 Check(self.shoutAt+self.shoutClip.length<self.fireAt,"The shout completes before the pistol fires");
 round.leadInSeconds=.05f;director.victoryDelaySeconds=0;director.sequences=new VictorySequence[]{self};
 float birth=await Win();var winner=course.Winner;var winnerPosition=winner.transform.position;
 Check(director.SelectedSequence==self&&!director.cheerSource.isPlaying,"Third sequence starts with no overlapping cheer");
 Check(!round.officer.enabled&&!round.march.enabled,"Sequence owns the instructor pose");
 await Wait(()=>Time.unscaledTime-birth>=self.shoutAt+.05f);
 var props=GameObject.Find("Instructor self-shot props");
 Check(props.GetComponent<AudioSource>().isPlaying,"The instructor's shout plays during his close-up");
 Check(Vector3.Distance(director.gameCamera.transform.position,actorStart+Vector3.up*1.55f)<4.5f,"Camera zooms in on the instructor for the shout");
 await Wait(()=>Time.unscaledTime-birth>=self.fireAt-.15f);
 var transforms=actor.GetComponentsInChildren<Transform>();var head=transforms.Single(t=>t.name=="Head");
 var upper=transforms.Single(t=>t.name=="UpperArm.R");var hand=transforms.Single(t=>t.name=="Fist.R");
 var side=Vector3.ProjectOnPlane(upper.position-actor.position,Vector3.up).normalized;
 var pistol=GameObject.Find("Self-shot pistol");var temple=head.position+Vector3.up*.15f+side*.22f;
 Check(Vector3.Distance(pistol.transform.position,hand.position)<.08f,"Draw ends with the pistol held in his hand");
 Check(Vector3.Distance(pistol.transform.TransformPoint(new Vector3(0,0,.43f)),temple)<.12f,"Pistol muzzle reaches the side of his head");
 Check(Vector3.Dot(pistol.transform.forward,-side)>.99f,"Pistol points inward toward his own head");
 await Wait(()=>UnityEngine.Object.FindAnyObjectByType<InstructorDeathAftermath>()!=null);
 var aftermath=UnityEngine.Object.FindAnyObjectByType<InstructorDeathAftermath>();
 Check(props.GetComponent<AudioSource>().isPlaying,"Gunshot plays on the head impact");
 Check(aftermath.Blood!=null&&aftermath.transform.Find("Instructor blood catch surface")!=null,"Head impact creates blood and a temporary platform collision surface");
 Check(course.Winner==winner&&!winner.IsEliminated&&Vector3.Distance(winner.transform.position,winnerPosition)<.001f,"Winning recruit stays alive and untouched");
 await Wait(()=>aftermath.HasLanded,2);
 Check(Quaternion.Angle(actor.rotation,actorRotation)>80,"Instructor collapses onto his side");
 var mesh=new Mesh();float bottom=float.PositiveInfinity;
 foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>()){skin.BakeMesh(mesh,true);foreach(var vertex in mesh.vertices)bottom=Mathf.Min(bottom,(skin.transform.position+skin.transform.rotation*vertex).y);}
 foreach(var renderer in actor.GetComponentsInChildren<Renderer>())if(!(renderer is SkinnedMeshRenderer))bottom=Mathf.Min(bottom,renderer.bounds.min.y);
 UnityEngine.Object.Destroy(mesh);
 Check(bottom>=actorStart.y-.005f&&bottom<actorStart.y+.05f,"Visible corpse rests on the platform surface");
 await Wait(()=>Time.unscaledTime-birth>=4.6f);
 Check(aftermath.Blood.transform.Find("Spreading puddle").localScale.x>.1f,"Blood lands on the platform and grows into a puddle");
 await Wait(()=>director.ShowRestartPrompt,3);
 Check(UnityEngine.Object.FindAnyObjectByType<InstructorDeathAftermath>()!=null&&!round.officer.enabled&&!round.march.enabled,"Instructor stays dead through the replay prompt");
 Check(GameObject.Find("Self-shot pistol")==null&&GameObject.Find("Instructor self-shot props")==null,"Transient pistol, flash and audio are cleaned after playback");
 director.StartAgain();await Task.Delay(80);
 Check(UnityEngine.Object.FindAnyObjectByType<InstructorDeathAftermath>()==null&&round.officer.enabled&&round.march.enabled,"Restart removes the corpse effects and restores officer animation");
 Check(Vector3.Distance(actor.position,actorStart)<.001f&&Quaternion.Angle(actor.rotation,actorRotation)<.1f,"Restart restores the instructor's standing position and rotation");
 // Cancel before speech, with the weapon raised, and during the fall.
 foreach(float cancelAt in new[]{.3f,2.9f,3.3f})
 {
  birth=await Win();await Wait(()=>Time.unscaledTime-birth>=cancelAt,4);
  director.StartAgain();await Task.Delay(80);
  Check(GameObject.Find("Self-shot pistol")==null&&UnityEngine.Object.FindAnyObjectByType<InstructorDeathAftermath>()==null&&round.officer.enabled&&round.march.enabled,"Cancellation at "+cancelAt+" seconds clears audio, props and death ownership");
  Check(Vector3.Distance(actor.position,actorStart)<.001f&&Quaternion.Angle(actor.rotation,actorRotation)<.1f,"Cancellation at "+cancelAt+" seconds restores the standing instructor");
 }
}
finally{round.leadInSeconds=lead;director.victoryDelaySeconds=delay;director.sequences=savedPool;round.BeginRound();}
return new{checksPassed=checks.Count,checks};
