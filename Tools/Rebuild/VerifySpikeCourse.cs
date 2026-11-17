using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;

if (!EditorApplication.isPlaying) throw new Exception("Enter Play Mode on Main first.");
var checks = new List<string>();
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks.Add(message);
}
var course = UnityEngine.Object.FindAnyObjectByType<SpikeCourse>();
var round = course.round;
var clock = round.masterBeat;
var players = course.players;
var speeds = players.Select(p => p.walkSpeed).ToArray();
var traps = course.traps;
var starts = players.Select(p => p.transform.position).ToArray();
var scales = GameObject.Find("Player lanes - gameplay anchors").transform.Cast<Transform>()
    .Where(t => t.name.StartsWith("Player lane ")).Select(t => t.localScale).ToArray();
Action<bool> input = right => {
    players[0].InputOverride = 0;
    players[1].InputOverride = right ? -1 : 1;
    players[2].InputOverride = right ? 1 : -1;
    players[3].InputOverride = right ? 1 : -1;
};
round.officer.DirectionChanged += input;
try
{
    Check(traps.Length == 4 && traps.All(t => t.leftPillars.Length == 5 && t.rightPillars.Length == 5),
        "Four lanes have eight banks of five pillars: forty pillars total");
    Check(traps.All(t => {
        var left = t.leftPillars[0].GetComponentInChildren<MeshFilter>();
        var right = t.rightPillars[0].GetComponentInChildren<MeshFilter>();
        return left.sharedMesh.vertices.Max(v=>left.transform.TransformPoint(v).x) > t.leftBank.position.x + .6f &&
            right.sharedMesh.vertices.Min(v=>right.transform.TransformPoint(v).x) < t.rightBank.position.x - .6f;
    }),
        "Both spike faces aim inward across each lane");
    round.BeginRound();
    Check(course.AliveCount == 4 && !course.IsGameOver && players.All(p => !p.IsEliminated), "Starting restores all four recruits");
    Check(traps.All(t => t.leftPillars.All(p => p.localPosition.y < -2.0f)), "Pillars start concealed below the platforms");
    await Task.Delay(700);
    Check(traps.All(t => t.leftPillars[0].localPosition.y > -2.1f && t.leftPillars[0].localPosition.y < 0 && t.leftPillars[0].localPosition.y > t.leftPillars[4].localPosition.y),
        "Five posts rise in a staggered animation during the silent lead-in");
    Check(traps.All(t => t.MissedBeats == 0) && !clock.IsPlaying, "No penalties or music occur during the rise");
    while (!round.HasStarted) await Task.Delay(10);
    Check(traps.All(t => t.IsRaised && t.leftPillars.All(p => Mathf.Abs(p.localPosition.y) < .001f)), "All pillars reach recruit height before the opening beat");
    await Task.Delay(100);
    Check(traps.All(t => t.MissedBeats == 0), "The opening cue gives players time to respond");
    await Task.Delay(1400);
    Check(traps[0].MissedBeats >= 2 && traps[1].MissedBeats >= 2, "Standing still and walking the wrong way both advance the spikes on beats");
    Check(traps[2].MissedBeats == 0 && traps[3].MissedBeats == 0, "Moving in the correct direction keeps that lane's spikes still");
    Check(traps[0].leftBank.position.x > -9.72f && traps[0].rightBank.position.x < 9.72f,
        "Each mistake moves both banks closer to the recruit");
    Check(Math.Abs(traps[0].TargetTravel - traps[0].MissedBeats * traps[0].inwardPerMiss) < .001,
        "Each missed beat adds exactly one inward step");
    Check(Mathf.Abs(players[2].transform.position.x - starts[2].x) > .5f,
        "Player controls move recruits along their lanes");
    round.audioOutput.music.Pause();
    await Task.Delay(70);
    float held = traps[0].leftBank.position.x;
    int missed = traps[0].MissedBeats;
    var positions = players.Select(p => p.transform.position).ToArray();
    await Task.Delay(300);
    Check(traps[0].MissedBeats == missed && Mathf.Abs(traps[0].leftBank.position.x-held) < .001 && players.Select((p,i) => p.transform.position == positions[i]).All(v=>v),
        "Pausing music freezes input movement, spike movement and penalties");
    round.audioOutput.music.UnPause();
    round.CancelStart();
    Check(traps.All(t => t.MissedBeats == 0 && t.TargetTravel == 0) && players.All(p=>!p.IsEliminated),
        "Cancelling clears danger and player state");
    // Isolate contact from travel: recruits can now move during the lead-in too.
    foreach (var player in players) player.walkSpeed = 0;
    round.BeginRound();
    while (!round.HasStarted) await Task.Delay(10);
    // Keep player zero stationary and force only its trap to the contact threshold.
    for (int i=0;i<30;i++) traps[0].MissBeat();
    float until = Time.realtimeSinceStartup + 6;
    while (!players[0].IsEliminated && Time.realtimeSinceStartup < until) await Task.Delay(20);
    Check(players[0].IsEliminated && course.AliveCount == 3 && !course.IsGameOver,
        "Spike contact eliminates only that recruit and the round continues");
    round.officer.DirectionChanged -= input;
    foreach (var player in players) player.InputOverride = 0;
    for(int lane=1;lane<4;lane++) for(int i=0;i<30;i++) traps[lane].MissBeat();
    until = Time.realtimeSinceStartup + 6;
    while (!course.IsGameOver && Time.realtimeSinceStartup < until) await Task.Delay(20);
    Check(course.IsGameOver && course.AliveCount == 0 && !clock.IsPlaying && !round.officer.IsRunning,
        "Last recruit eliminated produces game over and stops music and cues");
    var currentScales = GameObject.Find("Player lanes - gameplay anchors").transform.Cast<Transform>()
        .Where(t=>t.name.StartsWith("Player lane ")).Select(t=>t.localScale).ToArray();
    Check(scales.SequenceEqual(currentScales), "Platforms retain their full size while the spikes close in");
    round.BeginRound();
    Check(!course.IsGameOver && course.AliveCount == 4 && players.All(p => !p.IsEliminated && p.march.enabled) && traps.All(t=>t.TargetTravel==0),
        "Restart resets all four recruits and forty pillars");
}
finally
{
    round.officer.DirectionChanged -= input;
    for (int i=0;i<players.Length;i++) { players[i].InputOverride = null; players[i].walkSpeed = speeds[i]; }
    round.BeginRound();
}
return new {checksPassed=checks.Count,checks};
