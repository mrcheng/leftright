using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
var menu=UnityEngine.Object.FindAnyObjectByType<StartMenu>();menu.Open();var course=menu.course;var round=course.round;
float lead=round.leadInSeconds,reaction=course.reactionBeats;bool enabled=course.enabled,mines=menu.Obstacles.automaticSpawning;int count=menu.PlayerCount;
string key="Rebuild.SurvivalSoloBestSeconds";bool had=PlayerPrefs.HasKey(key);float best=PlayerPrefs.GetFloat(key);
var voice=course.players[0].hitAudio;var originalScream=voice.scream;var originalClip=voice.output.clip;
// A silent long voice lets the audio playhead prove that later impacts never
// restart the fall scream, independently of the supplied recording's duration.
var screamProbe=AudioClip.Create("Hole scream timing probe",16000*12,1,16000,false);voice.scream=screamProbe;
var checks=new List<string>();void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(label);}
async Task Wait(Func<bool> condition,float timeout=6){float until=Time.realtimeSinceStartup+timeout;while(!condition()&&Time.realtimeSinceStartup<until)await Task.Delay(15);if(!condition())throw new Exception("Timed out");}
try
{
 round.leadInSeconds=.15f;course.reactionBeats=10000;menu.Obstacles.automaticSpawning=false;
 menu.PlaySurvival();var player=course.players[0];var trap=course.traps[0];player.InputOverride=0;await Wait(()=>round.HasStarted);course.enabled=false;
 var position=player.transform.position;position.x=-3;player.transform.position=position;float fullHeight=player.transform.position.y;
 player.FallThroughLane();var hole=player.GetComponent<RecruitHoleTrap>();
 Check(player.IsTrappedInHole&&!player.IsEliminated&&!course.IsGameOver,"Hole entry wedges a living recruit instead of ending the round");
 Check(player.hitAudio.output.isPlaying&&player.hitAudio.output.clip==player.hitAudio.scream,"Hole entry plays the recruit's scream");
 player.InputOverride=1;Check(!player.TryJump(),"A wedged recruit cannot jump out");
 await Task.Delay(700);Check(player.transform.position.y<fullHeight-.4f&&player.transform.position.y>-.2f,"The slide stops halfway through at waist height");
 float x=player.transform.position.x;await Task.Delay(150);Check(Mathf.Abs(player.transform.position.x-x)<.001f&&!player.IsMoving,"Movement input cannot shift a wedged recruit");
 var knee=player.march.leftKnee.localRotation;await Task.Delay(140);Check(Quaternion.Angle(knee,player.march.leftKnee.localRotation)>2,"Legs kick beneath the lane while the torso stays wedged");
 float beforeScore=menu.Scores.PersonalSeconds;await Task.Delay(100);Check(menu.Scores.PersonalSeconds>beforeScore,"The recruit remains alive and scoring until the squeeze");
 float travelBefore=trap.TargetTravel;course.reactionBeats=0;course.enabled=true;await Wait(()=>trap.TargetTravel>travelBefore,3);course.enabled=false;course.reactionBeats=10000;
 Check(trap.TargetTravel>travelBefore,"Real missed music beats keep the spikes advancing toward a wedged recruit");
 Check(menu.Obstacles.SpawnMine(0,x+2)==null&&course.GetComponentInChildren<HealthCrateSpawner>().SpawnCrate(0,x+2)==null,"Trapped recruits do not receive mines or health pickups");
 int screamSample=voice.output.timeSamples;
 var bank=trap.leftBank;bank.position+=Vector3.right*(player.transform.position.x-player.collisionRadius-trap.LeftTip+.03f);trap.ResolveContact(player);
 Check(screamSample>0&&voice.output.timeSamples>=screamSample,"Spike capture does not restart the single fall scream");
 Check(player.IsPinned&&!player.IsTrappedInHole&&hole.IsSeparated&&!player.IsEliminated,"First spike contact captures only the upper body without early elimination");
 var upper=player.transform.Find("Model orientation/Carried head and torso");var lower=course.transform.Find("Hole victim lower half and blood/Detached kicking legs");
 Check(upper!=null&&lower!=null&&upper.GetComponentsInChildren<MeshRenderer>().Length>0&&lower.GetComponentsInChildren<MeshRenderer>().Length>0,"The actual recruit mesh is split into independent upper and lower snapshots");
 Check(player.GetComponentsInChildren<SkinnedMeshRenderer>().All(r=>!r.enabled),"The intact original skin is hidden after separation");
 float lowerX=lower.position.x,upperX=player.transform.position.x;bank.position+=Vector3.right*.5f;player.FollowPinnedBank();
 Check(Mathf.Abs(player.transform.position.x-upperX-.5f)<.01f&&Mathf.Abs(lower.position.x-lowerX)<.001f,"Upper body travels with the bank while detached legs remain independent");
 Check(course.transform.Find("Hole victim lower half and blood").GetComponentsInChildren<BloodSplash>().Length==1,"Separation creates a ground blood spray and pool");
 await Wait(()=>hole.LowerSettled);Check(lower.GetComponentsInChildren<Renderer>().Min(r=>r.bounds.min.y)>=.02f,"Lower half falls and settles on the ground rather than sinking through it");
 trap.ApplyNetworkPose(trap.MaximumTravel-.6f,trap.MaximumTravel-.6f,0,0);trap.FinishSqueeze();course.enabled=true;
 await Wait(()=>player.IsEliminated);Check(course.IsGameOver&&menu.Scores.Finished,"The second spike set finally eliminates the upper body and finishes Survival");
 Check(voice.output.clip==screamProbe&&voice.output.timeSamples>=screamSample,"The final squeeze adds no second scream");
 await Wait(()=>trap.HasFinishedSqueeze&&player.ragdoll.IsActive);
 Check(hole.IsSeparated&&lower.gameObject.activeInHierarchy,"Upper body is squeezed and released while the lower corpse remains on the ground");
 await Wait(()=>trap.HasReturned);Check(trap.TargetTravel==0,"Both spike sets retract after the upper-body squeeze");
 float final=menu.Scores.PersonalSeconds;await Task.Delay(150);Check(Mathf.Abs(final-menu.Scores.PersonalSeconds)<.001f,"Watching the body effects cannot inflate the finished score");
 await Wait(()=>menu.Results.Visible);Check(menu.GetComponent<VictoryDirector>().SelectedSequence==null,"Survival results still skip winner sequences");
 menu.Results.PlayAgain();Check(!player.IsTrappedInHole&&!hole.IsSeparated&&!player.IsEliminated&&player.GetComponentsInChildren<SkinnedMeshRenderer>().All(r=>r.enabled),"Replay restores the complete recruit and clears every hole state");
 menu.Open();menu.SetPlayerCount(4);menu.Play();course.enabled=false;await Wait(()=>round.HasStarted);
 foreach(var recruit in course.players)
 {
  recruit.InputOverride=0;recruit.FallThroughLane();await Task.Delay(600);var lane=course.traps[Array.IndexOf(course.players,recruit)];
  recruit.PinTo(lane.leftBank,true,lane.spikeReach);var state=recruit.GetComponent<RecruitHoleTrap>();
  Check(state.IsSeparated&&state.UpperWidth>.1f&&recruit.GetComponentsInChildren<MeshRenderer>().Any(r=>r.enabled),recruit.march.role+" supports waist clipping with its own mesh and materials");
  recruit.ResetPlayer();Check(!state.IsActive&&recruit.GetComponentsInChildren<SkinnedMeshRenderer>().All(r=>r.enabled),recruit.march.role+" restores its original skin and leg pose on reset");
 }
 return new{checksPassed=checks.Count,checks};
}
finally
{
 course.enabled=enabled;foreach(var recruit in course.players)recruit.InputOverride=null;menu.Open();menu.SetPlayerCount(count);
 round.leadInSeconds=lead;course.reactionBeats=reaction;menu.Obstacles.automaticSpawning=mines;
 voice.scream=originalScream;voice.output.clip=originalClip;UnityEngine.Object.Destroy(screamProbe);
 if(had)PlayerPrefs.SetFloat(key,best);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();
}
