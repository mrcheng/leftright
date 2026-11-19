using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
var menu=UnityEngine.Object.FindAnyObjectByType<StartMenu>();menu.Open();var round=menu.course.round;var starts=round.startScenes;var pool=starts.sequences;
var zoom=pool.OfType<CameraZoomStart>().Single();var drink=pool.OfType<InstructorDrinkStart>().Single();var bottle=menu.GetComponentInChildren<OfficerVodkaBottle>(true);var home=bottle.transform.position;var rotation=bottle.transform.rotation;
var actor=round.officer.transform;var actorHome=actor.position;float lead=round.leadInSeconds,reaction=menu.course.reactionBeats;bool mines=menu.Obstacles.automaticSpawning;int count=menu.PlayerCount;var rng=UnityEngine.Random.state;
var checks=new List<string>();void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(label);}
async Task Wait(Func<bool> condition,float seconds=6){float until=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<until)await Task.Delay(5);if(!condition())throw new Exception("Timed out");}
async Task At(float fraction){await Wait(()=>AudioSettings.dspTime>=round.ScheduledStartDspTime-round.IntroSeconds*(1-fraction));}
void Ready(string name)
{
 menu.view.GameplayPose(out var target,out var look,out float fov);
 Check(round.HasStarted&&!starts.IsPlaying&&!menu.view.CinematicActive&&round.audioOutput.MusicHasStarted,name+" releases camera and starts music on the scheduled first beat");
 Check(Vector3.Distance(target,menu.view.gameCamera.transform.position)<.05f&&Quaternion.Angle(look,menu.view.gameCamera.transform.rotation)<.1f&&Mathf.Abs(fov-menu.view.gameCamera.fieldOfView)<.05f,name+" ends at the exact gameplay camera with no second zoom");
 Check(round.officer.enabled&&round.march.enabled&&!bottle.IsHeld&&!bottle.IsBroken&&Vector3.Distance(home,bottle.transform.position)<.001f,name+" restores the instructor and unbroken floor bottle before gameplay");
}
try
{
 Check(pool.Length==2&&starts.AvailableSequences().Length==2,"Two separate opening assets give equal 50/50 selection");
 starts.sequences=new StartSequence[]{zoom};Check(Enumerable.Range(0,30).All(_=>starts.ChooseIndex()==0),"A single available opening is guaranteed");
 starts.sequences=new StartSequence[]{zoom,zoom,drink,null};Check(starts.AvailableSequences().Length==2&&Enumerable.Range(0,100).Select(_=>starts.ChooseIndex()).Distinct().OrderBy(i=>i).SequenceEqual(new[]{0,2}),"Duplicate/null entries cannot skew weights and both openings can be selected");
 bool available=drink.available;drink.available=false;Check(starts.AvailableSequences().Length==1,"Unavailable openings are excluded");drink.available=available;
 UnityEngine.Random.InitState(718);float expected=UnityEngine.Random.value;UnityEngine.Random.InitState(718);for(int i=0;i<50;i++)starts.ChooseIndex();Check(UnityEngine.Random.value==expected,"Opening selection does not consume instructor/gameplay randomness");
 Check(drink.drink!=null&&drink.burp!=null&&drink.bottleDown!=null&&drink.drink.length<=.71f&&drink.burp.length<=.33f&&drink.bottleDown.length<=.46f,"Supplied sip, burp and single set-down clips fit before the first beat");
 round.leadInSeconds=3;menu.course.reactionBeats=10000;menu.Obstacles.automaticSpawning=false;
 starts.sequences=new StartSequence[]{zoom};menu.PlaySurvival();var player=menu.course.players[0];player.InputOverride=0;
 Check(starts.IsPlaying&&starts.SelectedSequence==zoom&&!round.HasStarted,"Existing zoom is now an independently selected opening");
 var wide=menu.view.CoursePosition;await At(.4f);Check(Vector3.Distance(menu.view.gameCamera.transform.position,wide)>.5f&&menu.Scores.PersonalSeconds==0,"Zoom advances while gameplay/scoring wait for music");
 float before=player.transform.position.x;player.InputOverride=1;await Task.Delay(100);player.InputOverride=0;Check(player.transform.position.x>before+.05f,"Recruits can still move during the countdown");
 await Wait(()=>round.HasStarted);Ready("Zoom opening");Check(round.officer.IsRunning,"The first randomized direction starts with the first beat");menu.Open();
 starts.sequences=new StartSequence[]{drink};menu.PlaySurvival();player=menu.course.players[0];player.InputOverride=0;
 Check(starts.IsPlaying&&starts.SelectedSequence==drink&&!round.officer.enabled&&!round.march.enabled,"Drink opening owns the instructor pose only during the countdown");
 await At(.25f);Check(bottle.IsHeld&&GameObject.Find("Instructor opening drink sounds")!=null&&!GameObject.Find("Instructor opening drink sounds").GetComponents<AudioSource>().Any(a=>a.timeSamples>0),"Bottle is picked up before the sip sound");
 var hand=actor.GetComponentsInChildren<Transform>().Single(t=>t.name=="Fist.R");var lower=actor.GetComponentsInChildren<Transform>().Single(t=>t.name=="LowerArm.R");
 Check(bottle.transform.parent==hand&&Vector3.Dot(-bottle.transform.up,(hand.position-lower.position).normalized)>.995f,"Drinking keeps the approved rigid forearm/hand grip");
 await At(.44f);var head=actor.GetComponentsInChildren<Transform>().Single(t=>t.name=="Head");float mouth=Vector3.Distance(bottle.transform.TransformPoint(Vector3.up*.63f),head.position+Vector3.up*.15f-actor.forward*.42f);
 Check(mouth<.25f,"Bottle lip reaches the instructor's mouth ("+mouth+")");
 Check(GameObject.Find("Instructor opening drink sounds").GetComponents<AudioSource>().Any(a=>a.clip==drink.drink&&a.timeSamples>0)&&!round.audioOutput.MusicHasStarted,"Sip sound plays in close-up before music");ScreenCapture.CaptureScreenshot("Temp/start-drink-verified.png");
 await At(.64f);Check(GameObject.Find("Instructor opening drink sounds").GetComponents<AudioSource>().Any(a=>a.clip==drink.burp&&a.timeSamples>0),"Burp follows the sip");
 await At(.84f);Check(!bottle.IsHeld&&Vector3.Distance(home,bottle.transform.position)<.001f&&Quaternion.Angle(rotation,bottle.transform.rotation)<.1f,"Bottle is placed at its exact original location before pullback");
 Check(GameObject.Find("Instructor opening drink sounds").GetComponents<AudioSource>().Any(a=>a.clip==drink.bottleDown&&a.timeSamples>0),"One ground-glass impact plays on set-down");
 await At(.89f);Check(Vector3.Distance(menu.view.gameCamera.transform.position,player.transform.position+Vector3.up*1.1f)>1,"Fast pullback clears the front recruit");
 await Wait(()=>round.HasStarted);Ready("Drink opening");await Task.Delay(60);Check(GameObject.Find("Instructor opening drink sounds")==null,"Opening audio is removed before normal gameplay");ScreenCapture.CaptureScreenshot("Temp/start-gameplay-verified.png");await Task.Delay(60);menu.Open();
 foreach(float cancelAt in new[]{.3f,1.5f,2.6f})
 {
  menu.PlaySurvival();player=menu.course.players[0];player.InputOverride=0;await Task.Delay((int)(cancelAt*1000));menu.Open();await Task.Delay(50);
  Check(!starts.IsPlaying&&!menu.view.CinematicActive&&!bottle.IsHeld&&!bottle.IsBroken&&Vector3.Distance(home,bottle.transform.position)<.001f&&round.officer.enabled&&round.march.enabled&&GameObject.Find("Instructor opening drink sounds")==null,"Cancelling at "+cancelAt+"s clears intro sound/pose/prop ownership");
 }
 starts.sequences=pool;round.leadInSeconds=.05f;menu.PlaySurvival();player=menu.course.players[0];player.InputOverride=0;
 int drinkIndex=Array.IndexOf(pool,drink);round.BeginScheduled(AudioSettings.dspTime+3,true,drinkIndex,3);
 Check(starts.SelectedIndex==drinkIndex&&starts.SelectedSequence==drink,"Remote scheduled rounds use the supplied host index instead of rerolling");
 var capture=typeof(OnlineMatch).GetMethod("Capture",BindingFlags.Instance|BindingFlags.NonPublic);var state=(OnlineState)capture.Invoke(menu.online,null);var copy=JsonUtility.FromJson<OnlineState>(JsonUtility.ToJson(state));
 Check(copy.startSequence==drinkIndex&&Mathf.Abs(copy.introSeconds-3)<.001f,"Online snapshots carry the selected opening and shared duration");
 await Wait(()=>round.HasStarted);Check(!round.officer.IsRunning,"A remote intro does not invent a local instructor cue");round.officer.ApplyNetworkCue(false);await Task.Delay(80);Check(!round.officer.DirectionRight&&!round.officer.IsIdling,"Remote cue survives intro cleanup");menu.Open();
 round.leadInSeconds=9;menu.PlaySurvival();Check(round.IntroSeconds==5&&round.ScheduledStartDspTime-AudioSettings.dspTime<=5,"Production openings cannot postpone music beyond five seconds");menu.Open();
 starts.sequences=Array.Empty<StartSequence>();round.leadInSeconds=.05f;menu.PlaySurvival();await Wait(()=>round.HasStarted);Check(!starts.IsPlaying&&starts.SelectedSequence==null,"An empty pool safely skips the opening and still starts gameplay");
 return new{checksPassed=checks.Count,checks};
}
finally
{
 foreach(var player in menu.course.players)player.InputOverride=null;menu.Open();menu.SetPlayerCount(count);starts.sequences=pool;round.leadInSeconds=lead;menu.course.reactionBeats=reaction;menu.Obstacles.automaticSpawning=mines;UnityEngine.Random.state=rng;
}
