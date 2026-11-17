using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
var menu=UnityEngine.Object.FindAnyObjectByType<StartMenu>();var course=menu.course;var round=course.round;
var players=course.GetComponentsInChildren<RecruitController>(true).OrderBy(p=>p.playerIndex).ToArray();
var checks=new List<string>();void Check(bool condition,string label){if(!condition)throw new Exception(label);checks.Add(label);}
async Task Wait(Func<bool> condition,float seconds=3){float until=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<until)await Task.Delay(10);if(!condition())throw new Exception("Timed out");}
float lead=round.leadInSeconds;
try
{
 menu.Open();
 Check(menu.IsOpen&&!round.HasStarted&&!round.IsCountingDown&&!round.masterBeat.IsPlaying,"Scene starts at a silent menu without auto-starting gameplay");
 Check(menu.buttonTexture!=null&&menu.menuFont!=null,"Menu uses imported Kenney UI button art and font");
 round.leadInSeconds=.15f;
 foreach(int count in new[]{2,3,4,2,4})
 {
  menu.SetPlayerCount(count);
  Check(course.players.Length==count&&course.traps.Length==count&&course.AliveCount==count,count+" recruits form the actual simulation roster");
  Check(players.Count(p=>p.gameObject.activeSelf)==count,count+" recruit models are visible");
  var laneGroup=course.transform.Find("Player lanes - gameplay anchors");
  Check(Enumerable.Range(1,4).Count(i=>laneGroup.Find("Player lane "+i).gameObject.activeSelf)==count,count+" walkways are visible");
  Check(course.GetComponentsInChildren<LaneSpikeTrap>().Length==count,count+" spike lanes are active");
  Check(course.players[0].isLocallyControlled&&!course.players[0].computerControlled&&course.players.Skip(1).All(p=>p.computerControlled&&!p.isLocallyControlled),"One human and "+(count-1)+" computer opponents own separate inputs");
  menu.view.SetMode(GameViewMode.Online); // Stale/previous session must never change local framing.
  menu.Play();
  Check(menu.view.Mode==GameViewMode.Local,"Start Local Game always selects full-course camera behavior");
  Check(!menu.IsOpen&&round.IsCountingDown,"Start drill enters the normal music countdown");
  await Wait(()=>round.HasStarted);
  await Task.Delay(900);
  Check(Vector3.Distance(menu.view.gameCamera.transform.position,menu.view.CoursePosition)<.01f&&Mathf.Abs(menu.view.gameCamera.fieldOfView-menu.view.CourseFieldOfView)<.01f,"Local gameplay stays in the full-course view");
  Check(course.players.Skip(1).All(p=>Mathf.Abs(p.Horizontal)>.2f),"Computers march without keyboard input");
  menu.Open();
  Check(menu.IsOpen&&!round.HasStarted&&!course.IsGameOver&&players.Where(p=>p.gameObject.activeSelf).All(p=>!p.IsEliminated),"Opening menu cancels and resets the running match");
 }
 menu.SetPlayerCount(2);menu.Play();await Wait(()=>round.HasStarted);
 var bot=course.players[1].GetComponent<RecruitComputer>();float min=bot.reactionMinSeconds,max=bot.reactionMaxSeconds,chance=bot.mistakeChance;
 try
 {
  bot.reactionMinSeconds=bot.reactionMaxSeconds=0;bot.mistakeChance=0;
  typeof(RecruitComputer).GetField("lastRunning",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(bot,false);
  await Task.Delay(100);
  Check(bot.player.DirectionRight==round.officer.DirectionRight,"A correct computer reaction follows the visible instructor cue");
  bot.mistakeChance=1;typeof(RecruitComputer).GetField("lastRunning",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(bot,false);await Task.Delay(100);
  Check(bot.player.DirectionRight!=round.officer.DirectionRight,"Computer mistakes use genuine wrong-direction movement");
  bot.player.InputOverride=0;await Task.Delay(60);
  Check(!bot.player.IsMoving,"Simulation input overrides remain independent of bot input");
  bot.player.InputOverride=null;round.audioOutput.music.Pause();await Task.Delay(80);
  Check(bot.player.ComputerInput==0&&!bot.player.IsMoving,"Paused music stops computer movement");
  round.audioOutput.music.UnPause();
 }
 finally{bot.reactionMinSeconds=min;bot.reactionMaxSeconds=max;bot.mistakeChance=chance;bot.player.InputOverride=null;}
 // Selecting two players must not let hidden recruits prevent match completion.
 course.players[1].Eliminate();await Wait(()=>course.IsGameOver);
 Check(course.Winner==course.players[0]&&course.AliveCount==1,"Hidden recruits cannot block a two-player win");
 course.GetComponent<VictoryDirector>().StartAgain();await Task.Delay(60);
 Check(course.players.Length==2&&course.players[1].computerControlled&&!course.IsGameOver,"Play again preserves the selected count and computer ownership");
 menu.Open();menu.SetPlayerCount(4);
 Check(players.All(p=>p.gameObject.activeSelf&&p.march.enabled&&!p.IsEliminated),"Returning to four restores all characters cleanly");
}
finally{round.leadInSeconds=lead;menu.Open();menu.SetPlayerCount(4);}
return new{checksPassed=checks.Count,checks};
