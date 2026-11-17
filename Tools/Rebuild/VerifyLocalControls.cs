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
var checks=new List<string>();
void Check(bool c,string m){if(!c)throw new Exception(m);checks.Add(m);}
var course=UnityEngine.Object.FindAnyObjectByType<SpikeCourse>();var players=course.players;
var originalKeyboard=Keyboard.current;var keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();
try
{
 course.round.BeginRound();
 Check(players.Count(p=>p.isLocallyControlled)==1&&players[0].isLocallyControlled&&players[0].march.role==RecruitRole.Rifle,
    "Only the green recruit owns this client's hardware input");
 Check(players[0].transform.position.z==players.Min(p=>p.transform.position.z),"Local recruit occupies the front lane");
 var positions=players.Select(p=>p.transform.position).ToArray();
 InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.A)); InputSystem.Update(); await Task.Delay(250);
 Check(course.round.IsCountingDown&&players[0].transform.position.x<positions[0].x-.1f,$"A moves green left immediately during the lead-in (x={players[0].transform.position.x}, key={keyboard.aKey.isPressed}, axis={players[0].Horizontal})");
 float x=players[0].transform.position.x;
 InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D)); InputSystem.Update(); await Task.Delay(250);
 Check(players[0].transform.position.x>x+.1f,"D moves green right");
 Check(players.Skip(1).Select((p,i)=>p.transform.position==positions[i+1]).All(v=>v),"A/D never move the other three recruits");
 InputSystem.QueueStateEvent(keyboard,new KeyboardState()); InputSystem.Update(); await Task.Delay(250);x=players[0].transform.position.x;
 InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftArrow,Key.I,Key.R)); InputSystem.Update(); await Task.Delay(250);
 Check(Mathf.Abs(players[0].transform.position.x-x)<.001f&&!players[0].IsMoving,"Old split-keyboard controls no longer move green");
 Check(players.Skip(1).All(p=>!p.IsMoving),"Other recruits ignore the old local multiplayer keys");
 InputSystem.QueueStateEvent(keyboard,new KeyboardState()); InputSystem.Update(); await Task.Delay(250);
 course.round.BeginRound();
 Check(players[0].isLocallyControlled&&players.Skip(1).All(p=>!p.isLocallyControlled),"Restart preserves local input ownership");
}
finally
{
 InputSystem.RemoveDevice(keyboard);if(originalKeyboard!=null)originalKeyboard.MakeCurrent();
 course.round.BeginRound();
}
return new{checksPassed=checks.Count,checks};
