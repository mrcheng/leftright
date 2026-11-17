using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using LeftRight.Rebuild;
var course=UnityEngine.Object.FindAnyObjectByType<SpikeCourse>();var director=course.GetComponent<VictoryDirector>();var round=course.round;
float lead=round.leadInSeconds,delay=director.victoryDelaySeconds;var checks=new List<string>();
async Task Wait(Func<bool> condition){float until=Time.realtimeSinceStartup+3;while(!condition()&&Time.realtimeSinceStartup<until)await Task.Delay(10);if(!condition())throw new Exception("Timed out");}
void Check(bool condition,string label){if(!condition)throw new Exception(label);checks.Add(label);}
try{
 round.leadInSeconds=.05f;director.victoryDelaySeconds=0;
 for(int lane=0;lane<4;lane++){
  round.BeginRound();await Wait(()=>round.HasStarted);
  for(int other=0;other<4;other++)if(other!=lane)course.players[other].Eliminate();
  await Wait(()=>director.IsPlaying);
  var winner=course.players[lane];var playback=(VictoryPlayback)typeof(VictoryDirector).GetField("playback",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(director);playback.Tick(5);
  Check(course.Winner==winner,winner.march.role+" can win from its own lane");
  var hands=winner.GetComponentsInChildren<Transform>().Where(t=>t.name=="Fist.L"||t.name=="Fist.R").ToArray();
  float shoulders=Mathf.Max(winner.march.leftArm.position.y,winner.march.rightArm.position.y);
  Check(hands.Length==2&&hands.All(h=>h.position.y>shoulders+.15f),winner.march.role+" raises both hands above shoulders");
  Vector3 toward=Vector3.ProjectOnPlane(director.gameCamera.transform.position-winner.transform.position,Vector3.up).normalized;
  Check(Vector3.Dot(-winner.transform.forward,toward)>.99f,winner.march.role+" faces its close-up camera");
  var viewport=director.gameCamera.WorldToViewportPoint(winner.transform.position+Vector3.up);
  Check(Mathf.Abs(viewport.x-.5f)<.05f&&viewport.y>.2f&&viewport.y<.8f,winner.march.role+" is centered in its close-up");
  var camera=director.gameCamera;var target=new RenderTexture(900,700,24);var texture=new Texture2D(900,700,TextureFormat.RGB24,false);var previous=RenderTexture.active;
  try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,900,700),0,0);texture.Apply();File.WriteAllBytes("Temp/victory-"+winner.march.role+".png",texture.EncodeToPNG());}
  finally{camera.targetTexture=null;RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(texture);}
 }
}finally{round.leadInSeconds=lead;director.victoryDelaySeconds=delay;round.BeginRound();}
return new{checksPassed=checks.Count,checks};
