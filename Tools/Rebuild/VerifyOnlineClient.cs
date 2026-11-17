using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
var online=UnityEngine.Object.FindAnyObjectByType<OnlineMatch>();var menu=online.menu;var course=menu.course;
var checks=new List<string>();void Check(bool c,string m){if(!c)throw new Exception(m);checks.Add(m);}
async Task Wait(Func<bool> condition,float seconds=5){float until=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<until)await Task.Delay(10);if(!condition())throw new Exception("Timed out: "+online.Status);}
string report=Path.GetFullPath("Temp/peer-host.json"),input=Path.GetFullPath("Temp/peer-host-input.txt");
JObject Read(){try{return JObject.Parse(File.ReadAllText(report));}catch{return null;}}
Process host=null;
try
{
 if(File.Exists(report))File.Delete(report);File.WriteAllText(input,"0");online.port=7794;online.TestInput=0;
 host=Process.Start(new ProcessStartInfo{FileName=Path.GetFullPath("Builds/OnlineTest/LeftRight.exe"),Arguments="-batchmode -nographics -lr-test -lr-host 4 -lr-port 7794 -lr-autostart -lr-report \""+report+"\" -lr-input-file \""+input+"\" -logFile \""+Path.GetFullPath("Temp/peer-host.log")+"\"",UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});
 await Wait(()=>Read()!=null&&(bool)Read()["host"],15);
 Check(online.Join("127.0.0.1"),"Editor joins a standalone host through direct networking");
 await Wait(()=>online.RoundActive&&online.ReceivedSnapshots>5,15);
 Check(online.LocalSlot==1&&course.NetworkReplica,"Joining editor is a presentation client rather than a second authority");
 Check(course.players.All(p=>p.NetworkReplica&&!p.computerControlled),"Client does not run remote player simulation or AI");
 Check(course.players[1].isLocallyControlled&&!course.players[0].isLocallyControlled,"Input ownership belongs to the joining player's assigned recruit");
 Check(Math.Abs(course.players[1].transform.position.z+3.3f)<.01f&&course.players[0].transform.position.z>-2,"Joining player's recruit and lane are presented at the front");
 Check(menu.view.Mode==GameViewMode.Online&&!menu.IsOpen,"Online match enables follow view exclusively for the online session");
 await Wait(()=>course.round.HasStarted,5);
 online.TestInput=1;await Task.Delay(400);
 await Wait(()=>Read()!=null&&(float)((JArray)Read()["x"])[1]>.2f);
 Check((float)((JArray)Read()["x"])[1]>.2f&&Math.Abs((float)((JArray)Read()["x"])[0])<.05f,"Editor client inputs move its recruit on the standalone host without moving the host player");
 Check(course.players[1].transform.position.x>.15f,"Client renders the host-confirmed movement");
 Check(menu.view.gameCamera.transform.position.z>menu.view.CoursePosition.z+.2f,"Online follow camera actually eases toward the local recruit");
 host.Kill();host.WaitForExit();await Wait(()=>!online.Active,8);
 Check(menu.IsOpen&&!course.NetworkReplica&&course.players.All(p=>!p.NetworkReplica),"Losing the host closes the session and removes replica ownership");
 Check(course.players[0].isLocallyControlled&&Math.Abs(course.players[0].transform.position.z+3.3f)<.01f,"Leaving restores canonical lane order and local green ownership");
 menu.Play();await Task.Delay(400);
 Check(menu.view.Mode==GameViewMode.Local&&Vector3.Distance(menu.view.gameCamera.transform.position,menu.view.CoursePosition)<.01f,"Offline play after a disconnect returns to the full-course camera");
}
finally
{
 if(host!=null&&!host.HasExited){host.Kill();host.WaitForExit();}host?.Dispose();
 if(online.Active)online.Leave();online.TestInput=null;menu.Open();
}
return new{checksPassed=checks.Count,checks};
