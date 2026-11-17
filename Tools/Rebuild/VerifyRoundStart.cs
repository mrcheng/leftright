using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;

if(!EditorApplication.isPlaying) throw new Exception("Enter Play Mode on Main first.");
var checks=new List<string>();
void Check(bool condition,string description)
{
 if(!condition) throw new Exception(description);
 checks.Add(description);
}
var start=UnityEngine.Object.FindAnyObjectByType<RebuildRoundStart>();
Check(start!=null && start.leadInSeconds==3,"Round start has a three-second lead-in");
var officer=start.officer;
var audio=start.audioOutput;
var march=start.march;
var lights=UnityEngine.Object.FindAnyObjectByType<DirectionFloodlights>();
float originalVolume=audio.music.volume;
audio.music.volume=.25f;
var cueTimes=new List<double>();
Action<bool> onCue=right=>cueTimes.Add(AudioSettings.dspTime);
officer.DirectionChanged+=onCue;
try
{
 start.BeginRound();
 double cancelledTime=start.ScheduledStartDspTime;
 await Task.Delay(1000);
 Check(start.IsCountingDown && !start.HasStarted,"Round waits during the lead-in");
 Check(!audio.MusicHasStarted && !officer.IsRunning && officer.IsIdling,"Music and pointing are held before the start");
 Check(Quaternion.Angle(march.leftHip.localRotation,Quaternion.identity)<.001f && Quaternion.Angle(march.rightHip.localRotation,Quaternion.identity)<.001f,
    "Officer remains still during the countdown");
 Check(!lights.leftLight.enabled && !lights.rightLight.enabled,"Both direction floodlights stay off during the countdown");
 var samples=new float[512];
 audio.music.GetOutputData(samples,0);
 Check(Array.TrueForAll(samples,s=>Mathf.Abs(s)<.000001f),"Scheduled music is silent during the lead-in");
 start.CancelStart();
 while(AudioSettings.dspTime<cancelledTime+.2) await Task.Delay(20);
 Check(!start.HasStarted && !audio.music.isPlaying && cueTimes.Count==0,"Cancelling prevents a pending start from firing later");
 double before=AudioSettings.dspTime;
 start.BeginRound();
 double expected=start.ScheduledStartDspTime;
 Check(Math.Abs(expected-before-3)<.08,"Music is scheduled three seconds ahead on the audio clock");
 while(!start.HasStarted && AudioSettings.dspTime<expected+2) await Task.Delay(10);
 Check(start.HasStarted && !start.IsCountingDown && audio.MusicHasStarted,"Music and round activate at the scheduled start");
 Check(cueTimes.Count==1 && Math.Abs(cueTimes[0]-expected)<.15,"First pointing cue starts on the scheduled opening beat");
 Check(!officer.DirectionRight && !officer.IsIdling && lights.leftLight.enabled && !lights.rightLight.enabled,
    "First cue points left with only the left tower lit");
 await Task.Delay(60);
 double beat=((double)audio.music.timeSamples/audio.music.clip.frequency-audio.palette.titleFirstBeatSeconds)*audio.palette.titleBeatsPerMinute/60;
 Check(beat>=-.05 && beat<.65,"Music begins at its measured first beat rather than playing the pre-beat pickup");
 Check(Math.Abs(march.BeatPosition-beat)<.2,"Marching shares the music's opening-beat position");
 Check(audio.voice.clip==audio.palette.leftCall,"First cue uses the original left voice call");
 await Task.Delay(200);
 Check(Quaternion.Angle(march.leftHip.localRotation,Quaternion.identity)>45,"First high-knee step rises immediately after the opening beat");
 double first=cueTimes[0];
 while(cueTimes.Count<2 && AudioSettings.dspTime<first+6) await Task.Delay(20);
 Check(cueTimes.Count==2 && cueTimes[1]-first>=2 && cueTimes[1]-first<5.2 && officer.DirectionRight,
    "Later direction changes retain the prototype's random two-to-five-second alternation");
 start.BeginRound();
 Check(!officer.IsRunning && officer.IsIdling && !lights.leftLight.enabled && !lights.rightLight.enabled,
    "Restart returns to the silent neutral lead-in");
}
finally
{
 officer.DirectionChanged-=onCue;
 audio.music.volume=originalVolume;
 start.BeginRound();
}
return new{checksPassed=checks.Count,checks};
