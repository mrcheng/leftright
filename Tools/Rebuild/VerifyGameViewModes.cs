using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
var menu=UnityEngine.Object.FindAnyObjectByType<StartMenu>();var settings=menu.view;var course=menu.course;
var camera=settings.gameCamera;float aspect=camera.aspect;bool enabled=course.enabled;
var checks=new List<string>();void Check(bool c,string m){if(!c)throw new Exception(m);checks.Add(m);}
var speeds=course.players.Select(p=>p.walkSpeed).ToArray();
var positions=course.players.Select(p=>p.transform.position).ToArray();
try
{
 menu.Open();await Task.Delay(100);
 Vector3 wide=settings.CoursePosition;
 Check(Vector3.Distance(camera.transform.position,wide)<.01f,"Menu retains the saved course framing");
 menu.Play();course.enabled=false;
 foreach(var p in course.players){p.InputOverride=0;p.walkSpeed=0;}
 await Task.Delay(2400);
 Check(settings.Mode==GameViewMode.Local&&Vector3.Distance(camera.transform.position,wide)>1,"Local match follows its recruit during countdown");
 var player=settings.LocalPlayer;var pos=player.transform.position;
 float before=camera.transform.position.x;pos.x=5;player.transform.position=pos;
 await Task.Delay(70);float early=camera.transform.position.x;await Task.Delay(1800);
 Check(early>before&&early<camera.transform.position.x-.5f,"Local follow retains slow inertia");
 bool Visible(Vector3 point){var v=camera.WorldToViewportPoint(point);return v.z>0&&v.x>.02f&&v.x<.98f&&v.y>.02f&&v.y<.98f;}
 foreach(var mode in new[]{GameViewMode.Local,GameViewMode.Online})
 {
  settings.SetMode(mode);
  foreach(float ratio in new[]{16f/9,390f/844})
  {
   camera.aspect=ratio;
   foreach(float x in new[]{-8.5f,8.5f})
   {
    pos=player.transform.position;pos.x=x;player.transform.position=pos;await Task.Delay(3000);
    Check(Visible(player.transform.position)&&Visible(player.transform.position+Vector3.up*2.2f)&&Visible(course.round.officer.transform.position)&&Visible(course.round.officer.transform.position+Vector3.up*2.6f),mode+" keeps player and instructor visible at x="+x+", aspect="+ratio);
   }
  }
 }
 Check(Mathf.Abs(Mathf.DeltaAngle(camera.transform.eulerAngles.z,0))<.001f,"Follow stays horizontally level");
 menu.Open();await Task.Delay(150);
 Check(Vector3.Distance(camera.transform.position,wide)<.01f,"Opening menu restores wide framing");
}
finally
{
 camera.aspect=aspect;course.enabled=enabled;
 for(int i=0;i<course.players.Length;i++){course.players[i].walkSpeed=speeds[i];course.players[i].InputOverride=null;course.players[i].transform.position=positions[i];}
 menu.Open();
}
return new{checksPassed=checks.Count,checks};
