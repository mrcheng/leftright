using System;
using System.Linq;
using System.Collections.Generic;
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
var recruits = UnityEngine.Object.FindObjectsByType<RecruitMarch>().OrderBy(r => r.role).ToArray();
var start = UnityEngine.Object.FindAnyObjectByType<RebuildRoundStart>();
start.CancelStart();
var players = recruits.Select(r => r.player).ToArray();
var speeds = players.Select(p => p != null ? p.walkSpeed : 0).ToArray();
Action<bool> follow = right => {
    foreach (var player in players) if (player != null) player.InputOverride = right ? 1 : -1;
};
foreach (var player in players) if (player != null) player.walkSpeed = 0;
start.officer.DirectionChanged += follow;
Check(recruits.Length == 4 && UnityEngine.Object.FindObjectsByType<OfficerDirections>().Length == 1,
    "Four recruits plus one officer occupy the course");
Check(recruits.Count(r => r.female) == 2 && recruits.Select(r => r.role).Distinct().Count() == 4,
    "Two women and two men have four distinct roles");
Check(recruits.Select(r => r.transform.position.z).Distinct().Count() == 4, "Each recruit occupies a different lane");
var roots = recruits.Select(r => r.transform.position).ToArray();
var hips = recruits.Select(r => r.leftHip.localRotation).ToArray();
var knees = recruits.Select(r => r.leftKnee.localRotation).ToArray();
// Capture the imported neutral pose rather than the current marching pose.
foreach (var r in recruits) r.ResetMarch();
hips = recruits.Select(r => r.leftHip.localRotation).ToArray();
knees = recruits.Select(r => r.leftKnee.localRotation).ToArray();
try
{
    foreach (var r in recruits)
    {
        Check(r.masterBeat == start.masterBeat && r.directions == start.officer, r.role + " shares the course clock and direction cues");
        var skin = r.GetComponentInChildren<SkinnedMeshRenderer>();
        Check(skin != null && skin.sharedMesh.triangles.Length / 3 < 8000 && skin.bones.All(b => b != null),
            r.role + " has a complete low-poly skinned rig");
        Check(r.GetComponentsInChildren<Renderer>().All(s => s.sharedMaterials.All(m => m != null && m.shader.name == "Universal Render Pipeline/Lit")),
            r.role + " has valid scene materials");
    }
    start.BeginRound();
    await Task.Delay(500);
    Check(recruits.All(r => r.BeatPosition == 0 && Quaternion.Angle(r.transform.localRotation, Quaternion.identity) < .01),
        "Idle recruits stand still during the silent countdown");
    while (!start.HasStarted) await Task.Delay(10);
    await Task.Delay(180); // Let the high-knee idle gait blend into the moving drill gait.
    bool[] lifted = new bool[4], planted = Enumerable.Repeat(true,4).ToArray();
    bool straight = true, shared = true;
    float until = Time.realtimeSinceStartup + 2.5f;
    var mesh = new Mesh();
    try
    {
        while (Time.realtimeSinceStartup < until)
        {
            await Task.Delay(25);
            for (int i = 0; i < recruits.Length; i++)
            {
                var r = recruits[i];
                lifted[i] |= Quaternion.Angle(hips[i], r.leftHip.localRotation) > 25;
                straight &= Quaternion.Angle(knees[i], r.leftKnee.localRotation) <= 5.1f;
                shared &= Math.Abs(r.BeatPosition - start.masterBeat.BeatPosition) < .2;
                var skin = r.GetComponentInChildren<SkinnedMeshRenderer>();
                skin.BakeMesh(mesh);
                // Unity's baked vertices include the imported renderer scale.
                float sole = mesh.vertices.Min(v => (skin.transform.position + skin.transform.rotation * v).y);
                planted[i] &= sole > .61f && sole < .72f;
            }
        }
    }
    finally { UnityEngine.Object.DestroyImmediate(mesh); }
    Check(lifted.All(v => v), "All four recruits lift their legs to march");
    Check(straight, "Recruit knees stay nearly straight instead of using the officer's high-knee bend");
    Check(shared, "Every recruit follows the master beat throughout the march");
    Check(planted.All(v => v), "A supporting boot stays on the platform while the other leg lifts");
    for (int i = 0; i < recruits.Length; i++)
        Check(Vector3.Distance(roots[i], recruits[i].transform.position) < .00001f, recruits[i].role + " animation does not drift when movement speed is zero");
    bool sawLeft = false, sawRight = false;
    until = Time.realtimeSinceStartup + 6;
    while ((!sawLeft || !sawRight) && Time.realtimeSinceStartup < until)
    {
        await Task.Delay(50);
        var expected = start.officer.DirectionRight ? Vector3.right : Vector3.left;
        if (!recruits.All(r => Vector3.Dot(-r.transform.forward, expected) > .98f)) continue;
        if (start.officer.DirectionRight) sawRight = true; else sawLeft = true;
    }
    Check(sawLeft && sawRight, "All recruits follow both left and right cues");
    start.audioOutput.music.Pause();
    await Task.Delay(60);
    var paused = recruits.Select(r => r.leftHip.localRotation).ToArray();
    await Task.Delay(200);
    Check(recruits.Select((r,i) => Quaternion.Angle(paused[i],r.leftHip.localRotation) < .01f).All(v => v),
        "Paused music holds every recruit's pose");
    start.CancelStart();
    await Task.Delay(60);
    Check(recruits.All(r => r.BeatPosition == 0 && Quaternion.Angle(r.transform.localRotation,Quaternion.identity) < .01),
        "Cancelled rounds return all recruits to attention");
}
finally
{
    start.officer.DirectionChanged -= follow;
    for (int i=0;i<players.Length;i++) if (players[i] != null) { players[i].walkSpeed = speeds[i]; players[i].InputOverride = null; }
    start.BeginRound();
}
return new { checksPassed = checks.Count, checks };
