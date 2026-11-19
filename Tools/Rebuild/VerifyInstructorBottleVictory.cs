using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
var menu=UnityEngine.Object.FindAnyObjectByType<StartMenu>();menu.Open();var course=menu.course;var round=course.round;var director=menu.GetComponent<VictoryDirector>();
var pool=director.sequences;var sequence=pool.OfType<InstructorBottleVictory>().Single();var bottle=menu.GetComponentInChildren<OfficerVodkaBottle>(true);
var home=bottle.transform.position;var homeRotation=bottle.transform.rotation;var actor=round.officer.transform;var actorHome=actor.position;var actorRotation=actor.rotation;
float lead=round.leadInSeconds,delay=director.victoryDelaySeconds,reaction=course.reactionBeats;bool mines=menu.Obstacles.automaticSpawning;int count=menu.PlayerCount;
string key="Rebuild.SurvivalBestSeconds";bool had=PlayerPrefs.HasKey(key);float best=PlayerPrefs.GetFloat(key);
var born=typeof(VictoryDirector).GetField("startedAt",BindingFlags.Instance|BindingFlags.NonPublic);
var checks=new List<string>();void Check(bool c,string label){if(!c)throw new Exception(label);checks.Add(label);}
async Task Wait(Func<bool> condition,float timeout=4){float until=Time.realtimeSinceStartup+timeout;while(!condition()&&Time.realtimeSinceStartup<until)await Task.Delay(5);if(!condition())throw new Exception("Timed out");}
async Task<float> Win(){menu.Open();menu.SetPlayerCount(2);menu.Play();await Wait(()=>round.HasStarted);course.players[1].Eliminate();await Wait(()=>director.IsPlaying);return (float)born.GetValue(director);}
try
{
 Check(pool.Length==4&&pool.Distinct().Count()==4&&director.AvailableSequences().Length==4,"Four distinct available sequences retain equal one-in-four selection");
 Check(bottle!=null&&bottle.intactBody.activeSelf&&!bottle.IsHeld&&!bottle.IsBroken,"The instructor's bottle is visible before any game starts");
 Check(Mathf.Abs(home.y-actorHome.y)<.02f&&Vector3.Distance(home,actorHome)<1.1f,"Bottle stands on the command platform beside the instructor");
 var fill=bottle.intactBody.transform.Find("Half-full vodka").GetComponent<MeshFilter>().sharedMesh;
 var body=bottle.intactBody.transform.Find("Green glass body").GetComponent<MeshFilter>().sharedMesh;
 Check(fill.bounds.max.y<body.bounds.max.y*.6f&&fill.bounds.max.y>body.bounds.max.y*.4f,"Liquid visibly occupies only half the main bottle body");
 Check(sequence.glassHit!=null&&sequence.bloodMaterial!=null&&!sequence.PlayOpeningCheer,"Fourth asset owns glass audio and blood, with no cheer masking the smash");
 var samples=new float[sequence.glassHit.samples*sequence.glassHit.channels];Check(sequence.glassHit.GetData(samples,0)&&samples.Max(v=>Mathf.Abs(v))>.2f,"Supplied bottle hit contains audible audio");
 director.sequences=new VictorySequence[]{sequence};director.victoryDelaySeconds=0;round.leadInSeconds=.05f;course.reactionBeats=10000;menu.Obstacles.automaticSpawning=false;
 float birth=await Win();var winner=course.Winner;var winnerPosition=winner.transform.position;
 Check(bottle.intactBody.activeSelf&&!bottle.IsHeld,"The sequence uses the already standing scene bottle");
 Check(!round.officer.enabled&&!round.march.enabled&&!director.cheerSource.isPlaying,"Sequence owns the instructor's pose without overlapping cheer");
 await Wait(()=>Time.unscaledTime-birth>=sequence.grabAt-.1f);
 var all=actor.GetComponentsInChildren<Transform>();var hand=all.Single(t=>t.name=="Fist.R");var head=all.Single(t=>t.name=="Head");
 float reach=Vector3.Distance(hand.position,bottle.transform.TransformPoint(Vector3.up*bottle.gripHeight));
 Check(reach<.16f,"Reaching hand meets the neck before the floor bottle is picked up ("+reach+")");
 await Wait(()=>bottle.IsHeld);
 Check(Vector3.Distance(hand.position,bottle.transform.TransformPoint(Vector3.up*bottle.gripHeight))<.015f,"Picked-up bottle remains attached to the hand's grip");
 var grip=bottle.transform.localRotation;var lower=all.Single(t=>t.name=="LowerArm.R");
 Check(bottle.transform.parent==hand,"The bottle is actually parented to the gripping hand");
 Check(!bottle.IsBroken&&UnityEngine.Object.FindAnyObjectByType<BottleSmashEffects>()==null,"Bottle remains intact through pickup and wind-up");
 await Wait(()=>Time.unscaledTime-birth>=sequence.windupAt+.1f);var windupUp=bottle.transform.up;
 Check(Vector3.Dot(-bottle.transform.up,(hand.position-lower.position).normalized)>.995f,"Lifted bottle forms a rigid extension of the forearm");
 await Wait(()=>Time.unscaledTime-birth>=sequence.smashAt-.03f);
 var side=Vector3.ProjectOnPlane(all.Single(t=>t.name=="UpperArm.R").position-actor.position,Vector3.up).normalized;
 var temple=head.position+Vector3.up*.46f-round.officer.transform.forward*.4f+side*.14f;
 float contact=Vector3.Distance(bottle.transform.TransformPoint(Vector3.up*(bottle.gripHeight-.32f)),temple);
 Check(contact<.28f,"Bottle body reaches the front of the head at impact ("+contact+")");
 Check(Quaternion.Angle(grip,bottle.transform.localRotation)<.01f&&Vector3.Dot(-bottle.transform.up,(hand.position-lower.position).normalized)>.995f,"The bottle keeps its fixed hand grip and forearm alignment through the smash");
 Check(Vector3.SignedAngle(windupUp,bottle.transform.up,director.gameCamera.transform.forward)<-15,"Bottle swings clockwise in the frontal close-up");
 await Wait(()=>bottle.IsBroken);var effects=UnityEngine.Object.FindAnyObjectByType<BottleSmashEffects>();
 Check(!bottle.intactBody.activeSelf&&bottle.transform.Find("Bottle neck grip").gameObject.activeInHierarchy,"Smash removes the bottle body and leaves the neck in his grip");
 Check(GameObject.Find("Instructor bottle victory effects").GetComponent<AudioSource>().isPlaying,"Supplied glass sound plays on the smash");
 Check(effects!=null&&effects.GetComponentsInChildren<Transform>().Count(t=>t.name=="Flying bottle glass")==14&&effects.GetComponentsInChildren<Transform>().Count(t=>t.name=="Vodka splash droplet")==18,"Impact emits bounded glass shards and vodka droplets");
 Check(effects.Blood!=null&&head.Find("Bottle cut on instructor head")!=null,"Impact sprays blood from the head and leaves a small head cut");
 Check(course.Winner==winner&&!winner.IsEliminated&&Vector3.Distance(winner.transform.position,winnerPosition)<.001f,"The winning recruit remains alive and untouched");
 Check(Vector3.Distance(director.gameCamera.transform.position,actorHome+Vector3.up*1.55f)<4.5f,"Camera closes in on the instructor's bottle sequence");
 ScreenCapture.CaptureScreenshot("Temp/bottle-verified-impact.png");
 await Wait(()=>director.ShowRestartPrompt,6);
 Check(!bottle.IsHeld&&!bottle.IsBroken&&bottle.intactBody.activeSelf&&Vector3.Distance(bottle.transform.position,home)<.001f,"Sequence completion restores the same half-empty bottle to its home");
 Check(UnityEngine.Object.FindAnyObjectByType<BottleSmashEffects>()==null&&head.Find("Bottle cut on instructor head")==null,"Effects and temporary head cut are cleaned after zoom-out");
 Check(round.officer.enabled&&round.march.enabled&&Vector3.Distance(actor.position,actorHome)<.001f,"Normal instructor animation and standing pose are restored");
 foreach(float cancelAt in new[]{.4f,1.9f,2.9f})
 {
  birth=await Win();await Wait(()=>Time.unscaledTime-birth>=cancelAt);menu.Open();await Task.Delay(100);
  Check(!bottle.IsHeld&&!bottle.IsBroken&&Vector3.Distance(bottle.transform.position,home)<.001f&&Quaternion.Angle(bottle.transform.rotation,homeRotation)<.1f,"Cancellation at "+cancelAt+"s restores the standing bottle");
  Check(UnityEngine.Object.FindAnyObjectByType<BottleSmashEffects>()==null&&head.Find("Bottle cut on instructor head")==null&&round.officer.enabled&&round.march.enabled,"Cancellation at "+cancelAt+"s clears audio, liquids, glass and pose ownership");
 }
 Check(menu.GetComponentsInChildren<OfficerVodkaBottle>(true).Length==1,"Repeated wins and cancellations never duplicate the persistent bottle");
 menu.Open();var skipped=sequence.CreatePlayback(course.players[0],director.gameCamera);
 try
 {
  skipped.Tick(sequence.smashAt+.1f);
  Check(bottle.IsBroken&&bottle.transform.parent==hand,"A late playback sample still picks up and smashes the real bottle");
  Check(Vector3.Dot(-bottle.transform.up,(hand.position-lower.position).normalized)>.995f,"Skipped frames preserve the rigid forearm grip");
 }
 finally{skipped.Stop();}
 await Task.Delay(100);Check(!bottle.IsHeld&&!bottle.IsBroken&&UnityEngine.Object.FindAnyObjectByType<BottleSmashEffects>()==null,"Late playback cleanup restores the persistent bottle and removes effects");
 return new{checksPassed=checks.Count,checks};
}
finally
{
 menu.Open();menu.SetPlayerCount(count);director.sequences=pool;director.victoryDelaySeconds=delay;round.leadInSeconds=lead;course.reactionBeats=reaction;menu.Obstacles.automaticSpawning=mines;
 if(had)PlayerPrefs.SetFloat(key,best);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();
}
