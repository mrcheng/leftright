using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
var course=UnityEngine.Object.FindAnyObjectByType<SpikeCourse>();var director=course.GetComponent<VictoryDirector>();
var round=course.round;var view=course.GetComponent<GameViewSettings>();
var checks=new List<string>();void Check(bool condition,string label){if(!condition)throw new Exception(label);checks.Add(label);}
async Task Wait(Func<bool> condition,float seconds){float end=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<end)await Task.Delay(10);if(!condition())throw new Exception("Timed out waiting for victory lifecycle");}
float lead=round.leadInSeconds,delay=director.victoryDelaySeconds;
try
{
 Check(director.cheerSource!=null&&director.cheerClip!=null,"Saved victory setup includes the cheer clip and a dedicated source");
 Check(Mathf.Approximately(director.victoryDurationSeconds,5)&&Mathf.Approximately(director.zoomOutSeconds,1.5f),"Celebration lasts five seconds followed by a 1.5-second pullback");
 Check(!director.cheerSource.playOnAwake&&!director.cheerSource.loop&&director.cheerSource.spatialBlend==0,"Cheer is a non-looping 2D sound rather than distant world audio");
 var samples=new float[director.cheerClip.samples*director.cheerClip.channels];
 Check(director.cheerClip.GetData(samples,0)&&samples.Max(v=>Mathf.Abs(v))>.2f,"Imported cheer contains audible non-silent samples");
 round.leadInSeconds=.05f;director.victoryDelaySeconds=0;
 round.BeginRound();await Wait(()=>round.HasStarted,2);
 for(int lane=1;lane<4;lane++)course.players[lane].Eliminate();
 await Wait(()=>director.IsPlaying,2);float start=Time.unscaledTime;
 Check(director.cheerSource.isPlaying&&director.cheerSource.clip==director.cheerClip,"Cheer starts with the victory playback");
 Check(!director.ShowRestartPrompt&&!director.IsReturning,"No replay prompt during the celebration");
 float audioTime=director.cheerSource.time;await Task.Delay(150);
 Check(director.cheerSource.time>audioTime,"Cheer audio playback advances");
 while(Time.unscaledTime-start<4.75f)await Task.Delay(10);
 Check(director.IsPlaying&&!director.IsReturning&&!director.ShowRestartPrompt,"Celebration continues until approximately five seconds");
 await Wait(()=>director.IsReturning,1);float returnStart=Time.unscaledTime;
 Check(returnStart-start>=4.9f&&returnStart-start<5.3f,"Camera pullback starts at the five-second limit");
 Check(!director.IsPlaying&&!director.cheerSource.isPlaying&&!director.ShowRestartPrompt,"Cheer and celebration finish before replay prompt");
 var close=director.gameCamera.transform.position;await Task.Delay(500);
 Check(Vector3.Distance(close,director.gameCamera.transform.position)>.15f&&director.IsReturning,"Camera visibly eases outward rather than cutting");
 await Wait(()=>director.ShowRestartPrompt,2);
 Check(!director.IsReturning&&!director.IsPlaying&&view.RoundSummaryActive&&!view.CinematicActive,"Replay prompt takes over after the pullback finishes");
 Check(Vector3.Distance(director.gameCamera.transform.position,view.CoursePosition)<.01f&&Quaternion.Angle(director.gameCamera.transform.rotation,view.CourseRotation)<.1f,"Camera finishes at the full course framing");
 await Task.Delay(250);
 Check(Vector3.Distance(director.gameCamera.transform.position,view.CoursePosition)<.01f,"Online follow does not zoom back in behind the prompt");
 director.StartAgain();await Task.Delay(80);
 Check(!director.ShowRestartPrompt&&!director.IsReturning&&!director.IsPlaying&&!view.RoundSummaryActive&&!view.CinematicActive&&!course.IsGameOver,"Start again clears the prompt, cinematic and completed round");
 await Wait(()=>round.HasStarted,2);
 for(int lane=1;lane<4;lane++)course.players[lane].Eliminate();
 await Wait(()=>director.IsPlaying,2);director.StartAgain();await Task.Delay(80);
 Check(!director.cheerSource.isPlaying&&!director.IsPlaying&&!director.ShowRestartPrompt,"Restart during celebration stops the cheer and cancels the sequence");
}
finally{round.leadInSeconds=lead;director.victoryDelaySeconds=delay;round.BeginRound();}
return new{checksPassed=checks.Count,checks};
