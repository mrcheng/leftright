using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEditor;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
var menu=UnityEngine.Object.FindAnyObjectByType<StartMenu>();var course=menu.course;var p=course.players[0];
var obstacles=menu.Obstacles;var health=course.GetComponentInChildren<HealthCrateSpawner>();
float lead=course.round.leadInSeconds,reaction=course.reactionBeats,height=p.jumpHeight,duration=p.jumpDuration;
bool auto=obstacles.automaticSpawning,healthAuto=health.automaticSpawning;
var previous=Keyboard.current;var keyboard=InputSystem.AddDevice<Keyboard>("Mine jump playtest");
var results=new List<string>();
async Task<bool> Cross(float distance,int direction,float jumpHeight,float jumpDuration)
{
 InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Task.Delay(35);
 menu.Open();menu.Play();foreach(var other in course.players)other.InputOverride=other==p?null:(float?)0;
 p.jumpHeight=jumpHeight;p.jumpDuration=jumpDuration;await Task.Delay(180);
 var mine=obstacles.SpawnMine(0,1.8f);if(mine==null)throw new Exception("Mine setup failed");
 typeof(LaneMine).GetProperty("ArmsAtBeat").SetValue(mine,-1.0);
 var position=p.transform.position;position.x=1.8f-direction*distance;p.transform.position=position;
 float takeoff=position.x;
 var move=direction>0?Key.D:Key.A;
 InputSystem.QueueStateEvent(keyboard,new KeyboardState(move,Key.S));await Task.Delay(75);
 if(!p.IsJumping)throw new Exception("S input did not start jump");
 InputSystem.QueueStateEvent(keyboard,new KeyboardState(move));
 double until=Time.realtimeSinceStartupAsDouble+jumpDuration+.4;
 while(p.IsJumping&&!p.IsEliminated&&Time.realtimeSinceStartupAsDouble<until)await Task.Delay(15);
 bool clear=!p.IsEliminated&&!p.IsJumping&&(p.transform.position.x-1.8f)*direction>mine.triggerRadius;
 results.Add((direction>0?"RIGHT":"LEFT")+" takeoff="+distance.ToString("F2")+"m; duration="+jumpDuration.ToString("F2")+"s; distance="+Mathf.Abs(p.transform.position.x-takeoff).ToString("F2")+"m; "+(clear?"SAFE":"EXPLODED"));
 return clear;
}
try
{
 menu.Open();menu.SetPlayerCount(4);course.round.leadInSeconds=.05f;course.reactionBeats=10000;
 obstacles.automaticSpawning=health.automaticSpawning=false;
 // Reproduce the reported early-takeoff failure using real A/D + S device events.
 if(await Cross(.95f,1,.85f,.65f))throw new Exception("Old short-jump failure was not reproduced");
 var sparks=UnityEngine.Object.FindObjectsByType<Transform>().Where(t=>t.name=="Blast spark").ToArray();
 if(sparks.Any(t=>t.localScale.magnitude>.1f)||UnityEngine.Object.FindObjectsByType<Transform>().Any(t=>t.name=="Blast and smoke"))throw new Exception("Expanding blast geometry remains");
 foreach(int direction in new[]{1,-1})foreach(float distance in new[]{.5f,.65f,.8f,.95f,1.1f})
  if(!await Cross(distance,direction,height,duration))throw new Exception("New jump failed: "+results.Last());
 return "Old failure reproduced; 10 real-keyboard mine crossings passed; blast uses tiny shrinking sparks.\n"+string.Join("\n",results);
}
finally
{
 InputSystem.RemoveDevice(keyboard);if(previous!=null)previous.MakeCurrent();
 p.jumpHeight=height;p.jumpDuration=duration;course.round.leadInSeconds=lead;course.reactionBeats=reaction;
 obstacles.automaticSpawning=auto;health.automaticSpawning=healthAuto;menu.Open();
}
