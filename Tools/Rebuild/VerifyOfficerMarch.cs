using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;

if(!EditorApplication.isPlaying) throw new Exception("Enter Play Mode on Main first.");
UnityEngine.Object.FindAnyObjectByType<RebuildRoundStart>().BeginRound();
var checks=new List<string>();
void Check(bool condition,string description)
{
 if(!condition) throw new Exception(description);
 checks.Add(description);
}
await Task.Delay(300);
var march=UnityEngine.Object.FindAnyObjectByType<OfficerMarch>();
Check(march!=null && march.leftHip!=null && march.rightHip!=null && march.leftKnee!=null && march.rightKnee!=null,
    "Officer has independent hip and knee joints");
var stand=GameObject.Find("Command post").transform.Find("CommandStand").GetComponentInChildren<Renderer>();
float stageHeight=march.transform.position.y;
Check(stand.bounds.size.y<.8f && stand.bounds.max.y<.5f,"Lectern is removed, leaving only the command platform");
var skin=march.GetComponentInChildren<SkinnedMeshRenderer>();
float FootMin(bool left)
{
 var knee=left?march.leftKnee:march.rightKnee;
 if(skin==null) return knee.GetComponentInChildren<Renderer>().bounds.min.y;
 var mesh=new Mesh();
 try
 {
  skin.BakeMesh(mesh);
  var vertices=mesh.vertices;var weights=skin.sharedMesh.boneWeights;
  string foot=left?"Foot.L":"Foot.R";
  float min=float.MaxValue;
  for(int i=0;i<vertices.Length;i++)
  {
   var w=weights[i];
   if(skin.bones[w.boneIndex0].name==foot)
    min=Mathf.Min(min,(skin.transform.position+skin.transform.rotation*vertices[i]).y);
  }
  return min;
 }
 finally {UnityEngine.Object.DestroyImmediate(mesh);}
}
var rootPosition=march.transform.position;
var audio=march.audioOutput;
var source=audio.music;
float startTimeout=Time.realtimeSinceStartup+8;
while(!audio.MusicHasStarted && Time.realtimeSinceStartup<startTimeout) await Task.Delay(20);
Check(source.isPlaying && source.clip==audio.palette.titleMusic,"Marching starts with the original title music");
float oldTimeScale=Time.timeScale;
try
{
 bool leftHigh=false,rightHigh=false,groundedOnBeat=false,footsteps=false;
 bool neverBothHigh=true,neverBelowStage=true;
 float until=Time.realtimeSinceStartup+3.0f;
 while(Time.realtimeSinceStartup<until)
 {
  await Task.Delay(15);
  double beat=((double)source.timeSamples/source.clip.frequency-audio.palette.titleFirstBeatSeconds)*audio.palette.titleBeatsPerMinute/60;
  double fraction=beat-Math.Floor(beat);
  long step=(long)Math.Floor(beat);
  float leftAngle=Quaternion.Angle(march.leftHip.localRotation,Quaternion.identity);
  float rightAngle=Quaternion.Angle(march.rightHip.localRotation,Quaternion.identity);
  if(fraction>.42 && fraction<.58)
  {
   if((step%2+2)%2==0 && leftAngle>75 && rightAngle<1 && march.leftHip.position.y-march.leftKnee.position.y<.16f) leftHigh=true;
   if((step%2+2)%2==1 && rightAngle>75 && leftAngle<1 && march.rightHip.position.y-march.rightKnee.position.y<.16f) rightHigh=true;
  }
  if((fraction<.05 || fraction>.95) && FootMin(true)<stageHeight+.04f && FootMin(false)<stageHeight+.04f)
   groundedOnBeat=true;
  if(leftAngle>5 && rightAngle>5) neverBothHigh=false;
  if(Mathf.Min(FootMin(true),FootMin(false))<stageHeight-.02f) neverBelowStage=false;
  if(audio.effects.isPlaying) footsteps=true;
 }
 Check(leftHigh,"Left knee rises near hip height between music beats");
 Check(rightHigh,"Right knee rises near hip height on the alternate step");
 Check(groundedOnBeat,"Boots return to the platform on music beats");
 Check(neverBothHigh,"One leg supports the officer while the other lifts");
 Check(neverBelowStage,"Boots do not sink through the stage");
 Check(Vector3.Distance(rootPosition,march.transform.position)<.00001f,"Officer marches in place without moving across the platform");
 Check(footsteps,"Original boot sounds accompany the footfalls");
 source.Pause();
 var leftPose=march.leftHip.localRotation;
 var rightPose=march.rightHip.localRotation;
 double pausedBeat=march.BeatPosition;
 await Task.Delay(200);
 Check(Quaternion.Angle(leftPose,march.leftHip.localRotation)<.001f && Quaternion.Angle(rightPose,march.rightHip.localRotation)<.001f
    && Math.Abs(pausedBeat-march.BeatPosition)<.00001,"March freezes with paused music");
 source.UnPause();
 await Task.Delay(250);
 Check(march.BeatPosition>pausedBeat+.1,"March resumes from the music position");
 Time.timeScale=.5f;
 double before=march.BeatPosition;
 await Task.Delay(650);
 double elapsed=march.BeatPosition-before;
 Check(elapsed>.85 && elapsed<1.7,"Half-speed game time does not pull marching off the audio beat");
 Time.timeScale=oldTimeScale;
 source.timeSamples=Mathf.RoundToInt((audio.palette.titleFirstBeatSeconds+12.5f*60/audio.palette.titleBeatsPerMinute)*source.clip.frequency);
 await Task.Delay(50);
 double actual=((double)source.timeSamples/source.clip.frequency-audio.palette.titleFirstBeatSeconds)*audio.palette.titleBeatsPerMinute/60;
 Check(Math.Abs(actual-march.BeatPosition)<.2,"Seeking music immediately re-synchronizes the march");
 audio.PlayRoundMusic();
 await Task.Delay(350);
 actual=((double)source.timeSamples/source.clip.frequency-audio.palette.roundFirstBeatSeconds)*audio.palette.roundBeatsPerMinute/60;
 Check(source.clip==audio.palette.roundMusic && Math.Abs(actual-march.BeatPosition)<.2,"Round music selects its own measured beat timing");
}
finally
{
 Time.timeScale=oldTimeScale;
 audio.PlayTitleMusic();
}
return new{checksPassed=checks.Count,checks};
