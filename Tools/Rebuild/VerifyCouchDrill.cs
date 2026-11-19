using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first");
var menu=UnityEngine.Object.FindAnyObjectByType<StartMenu>();menu.Open();var course=menu.course;var round=course.round;float lead=round.leadInSeconds,reaction=course.reactionBeats;int count=menu.PlayerCount,checks=0;bool auto=menu.Obstacles.automaticSpawning;
var keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();
void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;}
async Task Keys(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));await Task.Delay(90);}
async Task Started(){float timeout=Time.realtimeSinceStartup+4;while(!round.HasStarted&&Time.realtimeSinceStartup<timeout)await Task.Delay(5);Check(round.HasStarted,"Round starts");}
try{
 round.leadInSeconds=.1f;course.reactionBeats=10000;menu.Obstacles.automaticSpawning=false;
 menu.ShowCreateOnline();Check(menu.Page==StartMenu.MenuPage.CreateOnline,"Separate create screen");menu.OpenOnlineLobby();Check(menu.Page==StartMenu.MenuPage.CreateOnline,"Host setup retains create screen");menu.Back();menu.ShowJoinOnline();Check(menu.Page==StartMenu.MenuPage.Online,"Separate join screen");menu.Back();
 foreach(int number in new[]{2,3,4}){
  menu.SetPlayerCount(number);menu.ShowCouch();Check(menu.Page==StartMenu.MenuPage.Couch,"Couch setup has own page");menu.PlayCouch();await Started();
  Check(menu.IsCouch&&course.players.Length==number&&course.players.All(p=>p.isLocallyControlled&&!p.computerControlled),"Couch roster has only human owners");Check(course.players.Select(p=>p.keyboardSlot).SequenceEqual(Enumerable.Range(0,number)),"Unique keyboard banks");
  var before=course.players.Select(p=>p.transform.position.x).ToArray();await Keys(Key.A,Key.T,Key.N,Key.O);
  Check(course.players.Select((p,i)=>p.Horizontal<0&&p.transform.position.x<before[i]).All(x=>x),"All humans move left simultaneously");
  await Keys();await Keys(Key.D,Key.U,Key.Comma,RecruitKeyboard.Control(keyboard,3,2).keyCode);Check(course.players.All(p=>p.Horizontal>0),"All humans move right simultaneously, including comma and Swedish key position");
  await Keys();await Keys(Key.S,Key.Y,Key.M,Key.P);Check(course.players.All(p=>p.IsJumping),"All humans jump simultaneously on middle keys");await Keys();await Task.Delay(1150);
  await Keys(Key.Space);Check(course.players.All(p=>!p.IsJumping),"Space no longer jumps");await Keys();await Keys(Key.A);Check(course.players[0].Horizontal<0&&course.players.Skip(1).All(p=>p.Horizontal==0),"P1 input cannot move other humans");await Keys();
  menu.view.SnapToGameplay();Check(course.players.All(p=>{var a=menu.view.gameCamera.WorldToViewportPoint(p.transform.position);var b=menu.view.gameCamera.WorldToViewportPoint(p.transform.position+Vector3.up*2.2f);return a.z>0&&a.x>.01f&&a.x<.99f&&a.y>.01f&&a.y<.99f&&b.y>.01f&&b.y<.99f;}),"Shared camera frames every recruit");
  round.BeginRound();await Started();Check(menu.IsCouch&&course.players.All(p=>!p.computerControlled)&&course.players.Select(p=>p.keyboardSlot).SequenceEqual(Enumerable.Range(0,number)),"Couch rematch retains human banks");menu.Open();Check(!menu.IsCouch,"Menu clears couch mode");
 }
 menu.SetPlayerCount(4);menu.Play();await Started();Check(!menu.IsCouch&&course.players[0].keyboardSlot==0&&course.players.Skip(1).All(p=>p.computerControlled&&!p.isLocallyControlled),"YOU vs Computer restores AI ownership");await Keys(Key.S);Check(course.players[0].IsJumping,"S jumps in AI drill");await Keys();menu.Open();menu.PlaySurvival();await Started();Check(!menu.IsCouch&&course.players.Length==1&&course.players[0].keyboardSlot==0,"Survival restores P1 bank");await Keys(Key.S);Check(course.players[0].IsJumping,"S jumps in Survival");await Keys();menu.Open();
 menu.PrepareOnlineMatch(4,3);Check(!menu.IsCouch&&course.players[3].isLocallyControlled&&course.players[3].keyboardSlot==0,"Online guest in lane four still uses A/S/D");await Keys(Key.D);Check(course.players[3].ReadNetworkInput()==1&&course.players.Take(3).All(p=>p.ReadNetworkInput()==0),"Online input reads only local guest bank");
 return new{checksPassed=checks};
}finally{InputSystem.RemoveDevice(keyboard);menu.Open();menu.RestoreLaneOrder();menu.SetPlayerCount(count);round.leadInSeconds=lead;course.reactionBeats=reaction;menu.Obstacles.automaticSpawning=auto;}
