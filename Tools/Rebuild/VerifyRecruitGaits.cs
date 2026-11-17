using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;

if (!EditorApplication.isPlaying) throw new Exception("Enter Play Mode on Main first.");
var checks=new List<string>();
void Check(bool condition,string message) { if(!condition) throw new Exception(message); checks.Add(message); }
var course=UnityEngine.Object.FindAnyObjectByType<SpikeCourse>();
var round=course.round;
var players=course.players;
round.CancelStart();
var hipRest=players.Select(p=>p.march.leftHip.localRotation).ToArray();
var kneeRest=players.Select(p=>p.march.leftKnee.localRotation).ToArray();
var rightRest=players.Select(p=>p.march.rightHip.localRotation).ToArray();
try
{
    foreach(var p in players) p.InputOverride=0;
    round.BeginRound();
    Check(players.All(p=>Mathf.Abs(p.transform.localPosition.x)<.001f),"All four recruits start at their lane's center");
    players[0].InputOverride=1;
    players[1].InputOverride=-1;
    await Task.Delay(450);
    Check(round.IsCountingDown && !round.masterBeat.IsPlaying,"Movement is tested before music or pointing begins");
    Check(players[0].transform.localPosition.x>.3f && players[1].transform.localPosition.x<-.3f,
        "Left and right input move recruits immediately during the lead-in");
    Check(Vector3.Dot(-players[0].transform.forward,Vector3.right)>.98f && Vector3.Dot(-players[1].transform.forward,Vector3.left)>.98f,
        "Recruits turn toward input before the first officer cue");
    bool highLeft=false,highRight=false,straight=true,planted=true,idleStill=true,walkingAnimated=false;
    var mesh=new Mesh();
    try
    {
        float until=Time.realtimeSinceStartup+1.3f;
        while(Time.realtimeSinceStartup<until)
        {
            await Task.Delay(20);
            var idle=players[2].march;
            idleStill&=Quaternion.Angle(hipRest[2],idle.leftHip.localRotation)<.01f &&
                Quaternion.Angle(rightRest[2],idle.rightHip.localRotation)<.01f && idle.BeatPosition==0;
            var moving=players[0].march;
            straight&=Quaternion.Angle(hipRest[0],moving.leftHip.localRotation)<=30.1f && Quaternion.Angle(kneeRest[0],moving.leftKnee.localRotation)<=5.1f;
            walkingAnimated|=Quaternion.Angle(hipRest[0],moving.leftHip.localRotation)>20;
            var skin=idle.GetComponentInChildren<SkinnedMeshRenderer>();skin.BakeMesh(mesh);
            float sole=mesh.vertices.Min(v=>(skin.transform.position+skin.transform.rotation*v).y);
            planted&=sole>.61f && sole<.72f;
        }
    }
    finally { UnityEngine.Object.DestroyImmediate(mesh); }
    Check(idleStill,"Idle recruits remain still throughout the silent lead-in");
    Check(straight&&walkingAnimated,"Moving recruits animate the normal walking gait before music starts");
    Check(planted,"High-knee marching keeps a supporting boot on the platform");
    Check(Mathf.Abs(players[2].transform.localPosition.x)<.001f && Mathf.Abs(players[3].transform.localPosition.x)<.001f,
        "Idle high knees do not move recruits away from center");
    Check(course.traps.All(t=>t.MissedBeats==0),"Pre-music marching and movement do not trigger penalties");
    players[1].InputOverride=0;
    await Task.Delay(80);
    Check(Quaternion.Angle(hipRest[1],players[1].march.leftHip.localRotation)<.01f,
        "Releasing movement before the music returns to standing still");
    while(!round.HasStarted) await Task.Delay(10);
    await Task.Delay(180);
    Check(players.All(p=>Math.Abs(p.march.BeatPosition-round.masterBeat.BeatPosition)<.2),
        "Both gaits join the master music beat at the opening downbeat");
    float idleUntil=Time.realtimeSinceStartup+1.2f;
    while(Time.realtimeSinceStartup<idleUntil)
    {
        await Task.Delay(20);
        highLeft|=Quaternion.Angle(hipRest[2],players[2].march.leftHip.localRotation)>75;
        highRight|=Quaternion.Angle(rightRest[2],players[2].march.rightHip.localRotation)>75;
    }
    Check(highLeft&&highRight,"Stationary recruits begin alternating high knees when the music starts");
    players[0].InputOverride=0;
    var held=players[0].transform.position;
    bool returnedHigh=false;
    float end=Time.realtimeSinceStartup+1.2f;
    while(Time.realtimeSinceStartup<end)
    {
        await Task.Delay(20);
        returnedHigh|=Quaternion.Angle(hipRest[0],players[0].march.leftHip.localRotation)>75;
    }
    Check(returnedHigh && Vector3.Distance(held,players[0].transform.position)<.08f,
        "Releasing input smoothly returns to high knees in place");
    round.audioOutput.music.Pause();await Task.Delay(60);
    var pose=players[0].march.leftHip.localRotation;held=players[1].transform.position;
    await Task.Delay(200);
    Check(Quaternion.Angle(pose,players[0].march.leftHip.localRotation)<.001f && held==players[1].transform.position,
        "Pausing audible music still freezes animation and movement");
    round.BeginRound();
    Check(players.All(p=>Mathf.Abs(p.transform.localPosition.x)<.001f),"Restart returns every recruit to center");
}
finally
{
    foreach(var p in players) p.InputOverride=null;
    round.BeginRound();
}
return new {checksPassed=checks.Count,checks};
