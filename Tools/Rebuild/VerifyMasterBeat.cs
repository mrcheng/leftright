using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;

if (!EditorApplication.isPlaying) throw new Exception("Enter Play Mode on Main first.");
var checks = new List<string>();
void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
    checks.Add(description);
}
var start = UnityEngine.Object.FindAnyObjectByType<RebuildRoundStart>();
var clock = start.masterBeat;
var audio = start.audioOutput;
var source = audio.music;
var events = new List<long>();
Action<long> listener = beat => events.Add(beat);
float oldPitch = source.pitch;
float oldScale = Time.timeScale;
clock.Beat += listener;
try
{
    Check(UnityEngine.Object.FindObjectsByType<MasterBeat>().Length == 1 && start.march.masterBeat == clock,
        "One course clock drives the march");
    start.BeginRound();
    await Task.Delay(500);
    Check(!clock.IsPlaying && clock.BeatPosition == 0 && events.Count == 0,
        "Countdown neither advances the beat nor fires events");
    while (!start.HasStarted) await Task.Delay(10);
    await Task.Delay(100);
    Check(clock.IsPlaying && events.Count == 1 && events[0] == 0, "Scheduled music starts with exactly one beat-zero event");
    Check(clock.BeatsPerMinute == audio.palette.titleBeatsPerMinute && Math.Abs(clock.SecondsPerBeat - 60.0 / 115) < .001,
        "Clock uses the title track's measured tempo");
    await Task.Delay(1600);
    bool sequential = events.Count >= 3;
    for (int i = 1; i < events.Count; i++) sequential &= events[i] == events[i - 1] + 1;
    Check(sequential, "Beat events advance once per musical beat");
    Check(Math.Abs(clock.BeatPosition - start.march.BeatPosition) < .15 && clock.BeatPhase >= 0 && clock.BeatPhase < 1,
        "March and continuous beat phase share the same clock");
    source.Pause();
    await Task.Delay(60);
    double held = clock.BeatPosition;
    int count = events.Count;
    await Task.Delay(300);
    Check(!clock.IsPlaying && clock.BeatPosition == held && events.Count == count, "Pausing music freezes phase and events");
    source.UnPause();
    await Task.Delay(150);
    Check(clock.IsPlaying && clock.BeatPosition > held, "Resuming continues the music clock");
    Time.timeScale = 0;
    double before = clock.BeatPosition;
    await Task.Delay(650);
    Check(clock.BeatPosition - before > .9, "Game time scale does not change the musical clock");
    Time.timeScale = oldScale;
    source.pitch = 1.5f;
    await Task.Delay(100);
    before = clock.BeatPosition;
    await Task.Delay(650);
    Check(clock.BeatPosition - before > 1.5 && clock.BeatPosition - before < 2.3, "Playback pitch changes beat rate with the music");
    source.pitch = oldPitch;
    count = events.Count;
    source.timeSamples = Mathf.RoundToInt((audio.palette.titleFirstBeatSeconds + 50.3f * 60 / 115) * source.clip.frequency);
    await Task.Delay(80);
    double actual = ((double)source.timeSamples / source.clip.frequency - audio.palette.titleFirstBeatSeconds) * 115 / 60;
    Check(Math.Abs(clock.BeatPosition - actual) < .2 && events.Count - count <= 1,
        "Seeking re-synchronizes immediately without replaying skipped beat events");
    audio.PlayRoundMusic();
    await Task.Delay(750);
    actual = ((double)source.timeSamples / source.clip.frequency - audio.palette.roundFirstBeatSeconds) * audio.palette.roundBeatsPerMinute / 60;
    Check(clock.BeatsPerMinute == audio.palette.roundBeatsPerMinute && Math.Abs(clock.BeatPosition - actual) < .2,
        "Changing tracks switches tempo and opening offset");
    start.CancelStart();
    await Task.Delay(100);
    Check(!clock.IsPlaying && clock.BeatPosition == 0, "Stopping resets the shared clock");
    events.Clear();
    start.BeginRound();
    while (!start.HasStarted) await Task.Delay(10);
    await Task.Delay(100);
    Check(events.Count == 1 && events[0] == 0, "Restarting starts a fresh beat-zero event");
}
finally
{
    clock.Beat -= listener;
    source.pitch = oldPitch;
    Time.timeScale = oldScale;
    start.BeginRound();
}
return new { checksPassed = checks.Count, checks };
