using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
var menu=UnityEngine.Object.FindAnyObjectByType<StartMenu>();menu.Open();
var lightning=menu.GetComponent<SurvivalLightning>();var course=menu.course;var round=course.round;
var timing=lightning.strikeSeconds;float warning=lightning.warningSeconds,lead=round.leadInSeconds,reaction=course.reactionBeats;
bool mines=menu.Obstacles.automaticSpawning;int count=menu.PlayerCount;
string key="Rebuild.SurvivalSoloBestSeconds";bool had=PlayerPrefs.HasKey(key);float best=PlayerPrefs.GetFloat(key);
var checks=new List<string>();void Check(bool value,string message){if(!value)throw new Exception(message);checks.Add(message);}
async Task Wait(Func<bool> value,float timeout=6){float end=Time.realtimeSinceStartup+timeout;while(!value()&&Time.realtimeSinceStartup<end)await Task.Delay(15);if(!value())throw new Exception("Timed out");}
void Place(float x){var p=course.players[0].transform.position;p.x=x;course.players[0].transform.position=p;}
try
{
 Check(timing.x>=30&&timing.y>timing.x&&warning==3,"Production deadline is randomized after 30s with a three-second warning");
 Check(lightning.rumbleClip!=null&&lightning.strikeClip!=null&&lightning.woodClip!=null&&lightning.rumbleClip.length>=2.9f,"Supplied rumble, thunderclap and wood clips are available");
 lightning.strikeSeconds=new Vector2(3.4f,3.4f);round.leadInSeconds=.15f;course.reactionBeats=10000;menu.Obstacles.automaticSpawning=false;
 menu.PlaySurvival();var player=course.players[0];player.InputOverride=0;
 var storm=SharedLightningAudio.ForCourse(course);int voices=course.GetComponents<AudioSource>().Length;
 Check(Enumerable.Range(0,4).All(_=>SharedLightningAudio.ForCourse(course)==storm)&&course.GetComponents<AudioSource>().Length==voices,"Four lane callers share the same storm audio without adding voices");
 Check(lightning.BoardCount==80,"The actual 80 walkway planks are extracted with their original geometry");
 Check(lightning.Elapsed==0&&!lightning.IsWarning,"Countdown does not advance the hazard");
 await Wait(()=>lightning.IsWarning);
 Check(lightning.BrokenCount==0,"Warning keeps both planks in place");
 Check(storm.WarningPlayed&&Enumerable.Range(0,4).All(_=>!storm.PlayWarning(lightning.rumbleClip)),"Repeated lane warning requests cannot stack or restart the rumble");
 Check(lightning.BlocksSpawn(lightning.TargetX)&&menu.Obstacles.SpawnMine(0,lightning.TargetX)==null&&course.GetComponentInChildren<HealthCrateSpawner>().SpawnCrate(0,lightning.TargetX)==null,"Mines and health cannot spawn on the threatened patch");
 Check(lightning.StrikeAt-lightning.Elapsed>=2.9,"Selected patch gets the full three seconds to react");
 double paused=lightning.Elapsed;round.audioOutput.music.Pause();await Task.Delay(300);
 Check(Math.Abs(lightning.Elapsed-paused)<.06&&!lightning.HasStruck,"Paused music freezes the warning clock");round.audioOutput.music.UnPause();
 Place(lightning.TargetX-1.5f);
 await Wait(()=>lightning.HasStruck);
 Check(lightning.BrokenCount==2,"Exactly two adjacent planks break on the strike");
 Check(storm.StrikePlayed&&Enumerable.Range(0,4).All(_=>!storm.PlayStrike(lightning.strikeClip,lightning.woodClip)),"Repeated lane strike requests cannot stack thunderclap or wood sounds");
 Check(lightning.HoleRight-lightning.HoleLeft>.4f&&lightning.HoleRight-lightning.HoleLeft<.7f,"Missing boards form a jumpable physical-width gap");
 Check(!player.IsEliminated,"A player outside the struck patch survives");
 var debris=UnityEngine.Object.FindObjectsByType<FallingLanePlank>();float before=debris.Average(p=>p.GetComponent<Renderer>().bounds.center.y);
 await Task.Delay(550);Check(debris.Length==2&&debris.Average(p=>p.GetComponent<Renderer>().bounds.center.y)<before-.1f,"Both real planks fall down as debris");
 Place(lightning.HoleLeft-.4f);player.InputOverride=1;Check(player.TryJump(),"Ordinary jump can start before the gap");
 await Wait(()=>player.transform.position.x>lightning.HoleRight+.3f);
 player.InputOverride=0;await Wait(()=>!player.IsJumping);
 Check(!player.IsEliminated,"The normal jump crosses the missing boards safely");
 await Task.Delay(3500);Check(lightning.HasStruck&&lightning.BrokenCount==2&&!lightning.IsWarning,"The lane never schedules a second strike");
 Place((lightning.HoleLeft+lightning.HoleRight)*.5f);await Wait(()=>player.IsEliminated);
 Check(player.IsFallingThrough,"Walking onto the gap eliminates with a downward fall");
 Check(player.hitAudio.output.isPlaying&&player.hitAudio.output.clip==player.hitAudio.scream,"Falling through the gap plays the recruit's scream");
 await Task.Delay(450);Check(player.transform.position.y<.1f,"The recruit falls onto the ground below the lane");
 await Wait(()=>menu.Results.Visible);Check(menu.Scores.Finished&&menu.GetComponent<VictoryDirector>().SelectedSequence==null,"Gap death ends Survival without a winner animation");
 menu.Results.PlayAgain();player=course.players[0];player.InputOverride=0;
 Check(lightning.BrokenCount==0&&!lightning.HasStruck&&!player.IsFallingThrough,"Replay restores boards, strike allowance and recruit");
 Check(!storm.WarningPlayed&&!storm.StrikePlayed,"Replay resets the shared audio allowance for the new round");
 menu.Open();Check(!lightning.BlocksSpawn(lightning.TargetX),"Menu clears missing-ground spawn restrictions");Check(lightning.BoardCount==0&&!UnityEngine.Object.FindObjectsByType<FallingLanePlank>().Any(p=>p.gameObject.activeInHierarchy),"Menu clears the split deck and all falling debris");
 menu.SetPlayerCount(2);menu.Play();await Task.Delay(400);
 Check(lightning.BoardCount==0&&lightning.Elapsed==0&&!lightning.IsWarning,"Local multiplayer never starts the Survival lightning hazard");
 return new{checksPassed=checks.Count,checks};
}
finally
{
 foreach(var player in course.players)player.InputOverride=null;
 menu.Open();menu.SetPlayerCount(count);lightning.strikeSeconds=timing;lightning.warningSeconds=warning;
 round.leadInSeconds=lead;course.reactionBeats=reaction;menu.Obstacles.automaticSpawning=mines;
 if(had)PlayerPrefs.SetFloat(key,best);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();
}


