using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
var menu=UnityEngine.Object.FindAnyObjectByType<StartMenu>();menu.Open();var course=menu.course;var round=course.round;
var lightning=menu.GetComponent<SurvivalLightning>();var rain=menu.GetComponent<SurvivalRain>();
var strike=lightning.strikeSeconds;var delay=rain.delaySeconds;var duration=rain.durationSeconds;float dry=rain.dryingSeconds,lead=round.leadInSeconds,reaction=course.reactionBeats;bool mines=menu.Obstacles.automaticSpawning;
var checks=new List<string>();void Check(bool c,string m){if(!c)throw new Exception(m);checks.Add(m);}
async Task Wait(Func<bool> condition,float seconds=7){float until=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<until)await Task.Delay(15);if(!condition())throw new Exception("Timed out");}
var sampled=typeof(SurvivalLightning).GetField("sampled",BindingFlags.Instance|BindingFlags.NonPublic);
void Place(float x){var player=course.players[0];var p=player.transform.position;p.x=x;player.transform.position=p;sampled.SetValue(lightning,false);}
try
{
 Check(delay.x==5&&delay.y==10&&duration.x==8&&duration.y==12,"Production shower waits 5-10 seconds and lasts a random 8-12 seconds");
 Check(Resources.Load<Material>("RainWater")!=null&&Resources.Load<Material>("RainDrop")!=null,"Water and rain materials are included for web stripping");
 lightning.strikeSeconds=new Vector2(3.4f,3.4f);rain.delaySeconds=new Vector2(.4f,.4f);rain.durationSeconds=new Vector2(6,6);rain.dryingSeconds=1.2f;
 round.leadInSeconds=.15f;course.reactionBeats=10000;menu.Obstacles.automaticSpawning=false;
 menu.PlaySurvival();var player=course.players[0];player.InputOverride=0;
 Check(!rain.Scheduled&&!rain.IsRaining&&rain.PuddleCount==0,"No rain or wetness during the countdown");
 await Wait(()=>round.HasStarted);player.InputOverride=1;await Task.Delay(150);player.InputOverride=0;await Task.Delay(60);float dryStop=player.transform.position.x;await Task.Delay(100);
 Check(Mathf.Abs(player.transform.position.x-dryStop)<.005f,"Dry ground keeps instant stopping");
 await Wait(()=>lightning.IsWarning);Place(lightning.TargetX-1.5f);Check(!rain.Scheduled,"Rumble alone does not schedule rain");
 await Wait(()=>lightning.HasStruck);
 Check(rain.Scheduled&&!rain.IsRaining&&rain.StartsAt-lightning.Elapsed>.3,"Actual strike schedules one delayed shower");
 await Wait(()=>rain.IsRaining);
 Check(rain.PuddleCount>0&&rain.PuddleCount<=rain.puddlesPerLane,"Bounded puddles appear on the active lane");
 var puddles=menu.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("Slippery puddle")).ToArray();
 Check(puddles.All(p=>!lightning.BlocksSpawn(p.bounds.center.x)),"Puddles do not cover the lightning gap");
 var systems=menu.GetComponentsInChildren<ParticleSystem>();Check(systems.Count(p=>p.name=="Rain shower")==1&&systems.Count(p=>p.name=="Lane edge drips")==1,"One shower and one shared drip pool serve the course");
 Place(puddles[0].transform.position.x-.2f);await Wait(()=>rain.WetnessAt(player)>.95f);
 Check(puddles[0].transform.localScale.x>.95f,"Water patches grow while rain falls");
 ScreenCapture.CaptureScreenshot("Temp/rain-puddles.png");
 player.InputOverride=1;await Task.Delay(280);player.InputOverride=0;float wetStop=player.transform.position.x;await Task.Delay(150);
 Check(player.transform.position.x>wetStop+.03f,"Wet ground coasts after input is released");
 player.InputOverride=1;await Task.Delay(180);float beforeReverse=player.transform.position.x;player.InputOverride=-1;await Task.Delay(60);
 Check(player.transform.position.x>beforeReverse,"Wet movement keeps momentum briefly on reversal");player.InputOverride=0;
 double pausedClock=lightning.Elapsed;float wetScale=puddles[0].transform.localScale.x;round.audioOutput.music.Pause();await Task.Delay(250);
 Check(Math.Abs(lightning.Elapsed-pausedClock)<.06&&Mathf.Abs(puddles[0].transform.localScale.x-wetScale)<.02f,"Music pause freezes rain timing and puddle growth/drying");round.audioOutput.music.UnPause();
 await Wait(()=>!rain.IsRaining);Check(rain.PuddleCount>0,"Puddles remain temporarily after the shower");
 await Wait(()=>rain.PuddleCount==0);Check(rain.WetnessAt(player)==0,"Puddles dry and restore grip");
 Check(menu.GetComponentsInChildren<ParticleSystem>().Count(p=>p.name=="Rain shower")==1,"One strike cannot create repeated rain systems");
 menu.Open();Check(!rain.Scheduled&&!rain.IsRaining&&rain.PuddleCount==0,"Menu clears weather, puddles and scheduling");
 menu.PlaySurvival();Check(!rain.Scheduled&&rain.WetnessAt(course.players[0])==0,"Replay starts dry with no old slide state");menu.Open();menu.SetPlayerCount(2);menu.Play();await Task.Delay(150);
 Check(!rain.Scheduled&&rain.PuddleCount==0,"Other modes retain their existing dry movement");
 return new{checksPassed=checks.Count,checks};
}
finally
{
 foreach(var player in course.players)player.InputOverride=null;menu.Open();menu.SetPlayerCount(4);
 lightning.strikeSeconds=strike;rain.delaySeconds=delay;rain.durationSeconds=duration;rain.dryingSeconds=dry;
 round.leadInSeconds=lead;course.reactionBeats=reaction;menu.Obstacles.automaticSpawning=mines;
}
