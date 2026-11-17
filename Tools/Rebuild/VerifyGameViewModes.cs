using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
var checks=new List<string>();void Check(bool c,string m){if(!c)throw new Exception(m);checks.Add(m);}
var settings=UnityEngine.Object.FindAnyObjectByType<GameViewSettings>();var course=settings.course;var round=course.round;
var camera=settings.gameCamera;var oldMode=settings.Mode;bool enabled=course.enabled;float aspect=camera.aspect;
var speeds=course.players.Select(p=>p.walkSpeed).ToArray();
try
{
 settings.SetMode(GameViewMode.Local);round.CancelStart();await Task.Delay(1800);
 Vector3 wide=camera.transform.position;float wideFov=camera.fieldOfView;
 Check(Vector3.Distance(wide,new Vector3(0,7.567722f,-11.445186f))<.01f,"Local mode retains the saved wide course camera");
 Check(settings.LocalPlayer==course.players[0],"Online target is the locally owned front recruit");
 settings.SetMode(GameViewMode.Online);await Task.Delay(250);
 Check(Vector3.Distance(camera.transform.position,wide)<.01f,"Online selection waits for round start before moving closer");
 foreach(var p in course.players){p.InputOverride=0;p.walkSpeed=0;}
 round.BeginRound();course.enabled=false;
 await Task.Delay(2400);
 Check(Vector3.Distance(camera.transform.position,settings.LocalPlayer.transform.position)<Vector3.Distance(wide,settings.LocalPlayer.transform.position)-1,
    "Online round start eases the camera closer to the local recruit");
 Check(camera.fieldOfView<wideFov,"Online mode uses tighter framing");
 var player=settings.LocalPlayer;var pos=player.transform.position;pos.x=5;player.transform.position=pos;
 float before=camera.transform.position.x;await Task.Delay(70);float early=camera.transform.position.x;
 await Task.Delay(1300);
 Check(early>before&&early<camera.transform.position.x-1,"Following uses gradual inertia instead of snapping to player movement");
 bool Visible(Vector3 point){var v=camera.WorldToViewportPoint(point);return v.z>0&&v.x>.03f&&v.x<.97f&&v.y>.03f&&v.y<.97f;}
 foreach(float ratio in new[]{16f/9,4f/3})
 {
  camera.aspect=ratio;
  foreach(float x in new[]{-8.5f,8.5f})
  {
   pos=player.transform.position;pos.x=x;player.transform.position=pos;await Task.Delay(2400);
   Check(Visible(player.transform.position)&&Visible(player.transform.position+Vector3.up*2.2f)&&
      Visible(round.officer.transform.position)&&Visible(round.officer.transform.position+Vector3.up*2.6f),
      "Online framing keeps recruit and instructor visible at x="+x+" and aspect="+ratio);
  }
 }
 Check(Mathf.Abs(Mathf.DeltaAngle(camera.transform.eulerAngles.z,0))<.001f,"Follow camera stays horizontally level");
 settings.SetMode(GameViewMode.Local);await Task.Delay(4200);
 Check(Vector3.Distance(camera.transform.position,wide)<.03f&&Mathf.Abs(camera.fieldOfView-wideFov)<.03f,
    "Switching back to Local restores the wide view");
 settings.SetMode(GameViewMode.Online);
 Check(settings.Mode==GameViewMode.Online,"Future online sessions can select follow framing programmatically");
 round.BeginRound();await Task.Delay(1600);
 Check(camera.transform.position.z>wide.z+.5f,"Restart re-enters the selected Online framing");
}
finally
{
 settings.SetMode(oldMode);camera.aspect=aspect;course.enabled=enabled;
 for(int i=0;i<course.players.Length;i++){course.players[i].walkSpeed=speeds[i];course.players[i].InputOverride=null;}
 round.BeginRound();
}
return new{checksPassed=checks.Count,checks};
