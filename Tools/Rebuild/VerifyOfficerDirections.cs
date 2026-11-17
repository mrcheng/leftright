using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using LeftRight.Rebuild;

if (!UnityEditor.EditorApplication.isPlaying) throw new Exception("Enter Play Mode on Main first.");
var checks = new List<string>();
void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
    checks.Add(description);
}
var officer = UnityEngine.Object.FindAnyObjectByType<OfficerDirections>();
var roundStart = UnityEngine.Object.FindAnyObjectByType<RebuildRoundStart>();
if (roundStart != null) roundStart.CancelStart();
Check(officer != null && officer.leftShoulder != null && officer.rightShoulder != null, "Officer has two animated shoulder pivots");
var floodlights = UnityEngine.Object.FindAnyObjectByType<DirectionFloodlights>();
Check(floodlights != null && floodlights.officer == officer, "Tower floodlights follow this officer's direction sequence");
string prototype = File.ReadAllText("Assets/Prototype/Scenes/Prototype.unity");
float Original(string field) => float.Parse(Regex.Match(prototype, "  " + field + ": ([0-9.]+)").Groups[1].Value, CultureInfo.InvariantCulture);
Check(officer.minTimeTurn == Original("minTimeTurn") && officer.maxTimeTurn == Original("maxTimeTurn"),
    "Random wait range matches the prototype scene: two to five seconds");
var oldRandom = UnityEngine.Random.state;
var seen = new List<(bool right, float time)>();
Action<bool> onCue = right => seen.Add((right, Time.time));
officer.DirectionChanged += onCue;
try
{
    officer.StopDirections();
    UnityEngine.Random.InitState(719);
    var expected = new float[4];
    expected[0] = UnityEngine.Random.Range(2f, 5f);
    bool firstRight = UnityEngine.Random.Range(0, 2) == 1;
    for(int i=1;i<4;i++) expected[i] = UnityEngine.Random.Range(2f, 5f);
    UnityEngine.Random.InitState(719);
    officer.ActivateDirections();
    Check(officer.IsIdling, "Initial state is neutral before the random first cue");
    Check(!floodlights.leftLight.enabled && !floodlights.rightLight.enabled, "Both tower lights are off before the first direction cue");
    Check(Quaternion.Angle(officer.leftShoulder.localRotation, Quaternion.identity) < .001f && Quaternion.Angle(officer.rightShoulder.localRotation, Quaternion.identity) < .001f,
        "Both arms rest down before the first cue");
    Check(Mathf.Abs(officer.CurrentInterval - expected[0]) < .0001f, "First random wait follows the prototype's uniform random draw");
    float lastCueTime = Time.time;
    for (int cue = 0; cue < 3; cue++)
    {
        float timeout = Time.realtimeSinceStartup + 12f;
        while (seen.Count <= cue && Time.realtimeSinceStartup < timeout) await Task.Delay(20);
        Check(seen.Count == cue + 1, "Cue " + (cue + 1) + " arrives automatically");
        bool right = cue % 2 == 0 ? firstRight : !firstRight;
        Check(seen[cue].right == right && officer.DirectionRight == right && !officer.IsIdling,
            "Cue " + (cue + 1) + ": direction alternates " + (right ? "right" : "left"));
        Check(floodlights.leftLight.enabled == !right && floodlights.rightLight.enabled == right,
            "Cue " + (cue + 1) + ": only the matching tower spotlight is lit");
        var leftLens = new MaterialPropertyBlock();
        var rightLens = new MaterialPropertyBlock();
        floodlights.leftLamp.GetPropertyBlock(leftLens, floodlights.leftLampMaterialIndex);
        floodlights.rightLamp.GetPropertyBlock(rightLens, floodlights.rightLampMaterialIndex);
        Check((leftLens.GetColor("_EmissionColor").maxColorComponent > 1f) == !right
            && (rightLens.GetColor("_EmissionColor").maxColorComponent > 1f) == right,
            "Cue " + (cue + 1) + ": only the matching lamp panel glows");
        Check(Mathf.Abs(seen[cue].time - lastCueTime - expected[cue]) < .35f,
            "Cue " + (cue + 1) + ": elapsed game time matches the sampled random wait");
        lastCueTime = seen[cue].time;
        Check(Mathf.Abs(officer.CurrentInterval - expected[cue + 1]) < .0001f,
            "Cue " + (cue + 1) + ": the next wait is sampled again");
        Check(officer.audioOutput.voice.clip == (right ? officer.audioOutput.palette.rightCall : officer.audioOutput.palette.leftCall),
            "Cue " + (cue + 1) + ": original direction voice matches the pointing arm");
        await Task.Delay(100);
        var moving = right ? officer.rightShoulder : officer.leftShoulder;
        float targetZ = right ? 90 : -90;
        Check(Quaternion.Angle(moving.localRotation, Quaternion.Euler(0, 0, targetZ)) > .01f,
            "Cue " + (cue + 1) + ": arm swings through an intermediate pose");
        await Task.Delay(350);
        Check(Quaternion.Angle(moving.localRotation, Quaternion.Euler(0, 0, targetZ)) < .001f,
            "Cue " + (cue + 1) + ": active arm settles exactly horizontal");
        var resting = right ? officer.leftShoulder : officer.rightShoulder;
        Check(Quaternion.Angle(resting.localRotation, Quaternion.identity) < .001f,
            "Cue " + (cue + 1) + ": unused arm lowers to the side");
        var skin = officer.GetComponentInChildren<SkinnedMeshRenderer>();
        Vector3 meshOffset = skin != null
            ? officer.GetComponentsInChildren<Transform>().Single(t => t.name == (right ? "Fist.R" : "Fist.L")).position - moving.position
            : moving.GetComponentInChildren<Renderer>().bounds.center - moving.position;
        Check(Vector3.Dot(meshOffset.normalized, right ? Vector3.right : Vector3.left) > .98f,
            "Cue " + (cue + 1) + ": visible mesh points in the required world direction");
        Quaternion held = moving.localRotation;
        await Task.Delay(250);
        Check(Quaternion.Angle(moving.localRotation, held) < .001f && seen.Count == cue + 1,
            "Cue " + (cue + 1) + ": pointing pose holds between cues");
    }
    officer.StopDirections();
    int count = seen.Count;
    await Task.Delay(500);
    Check(seen.Count == count, "Direction sequence can stop for a future round-end event");
    Check(!floodlights.leftLight.enabled && !floodlights.rightLight.enabled, "Stopping the sequence turns both tower lights off");
    officer.ActivateDirections();
    Check(officer.IsIdling && officer.DirectionRight && Quaternion.Angle(officer.leftShoulder.localRotation, Quaternion.identity) < .001f,
        "Restart resets to neutral before sampling a fresh opening direction");
    Check(!floodlights.leftLight.enabled && !floodlights.rightLight.enabled, "Restart resets both tower lights to neutral/off");
}
finally
{
    officer.DirectionChanged -= onCue;
    UnityEngine.Random.state = oldRandom;
    if (roundStart != null) roundStart.BeginRound();
    else officer.ActivateDirections();
}
return new { checksPassed = checks.Count, checks };
