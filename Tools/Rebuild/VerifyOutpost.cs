using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;

if (!EditorApplication.isPlaying) throw new Exception("Open Main and enter Play Mode first.");
var checks = new List<string>();
void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
    checks.Add(description);
}
await Task.Delay(250);
var roundStart = UnityEngine.Object.FindAnyObjectByType<RebuildRoundStart>();
float startTimeout = Time.realtimeSinceStartup + 8;
while (roundStart != null && roundStart.IsCountingDown && Time.realtimeSinceStartup < startTimeout) await Task.Delay(20);
Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == "Assets/Rebuild/Scenes/Main.unity", "Rebuild scene opens in Play Mode");
Check(UnityEngine.Object.FindObjectsByType<Camera>().Length == 1 && Camera.main != null, "Exactly one game camera is active");
Check(UnityEngine.Object.FindObjectsByType<AudioListener>().Length == 1, "Exactly one audio listener is active");
Check(UnityEngine.Object.FindObjectsByType<MonoBehaviour>().All(b => b.GetType().Assembly.GetName().Name != "LeftRight.Prototype"),
    "Rebuild has no prototype gameplay components");
var root = GameObject.Find("Outpost Environment");
Check(root != null, "Generated outpost hierarchy exists");
var renderers = root.GetComponentsInChildren<Renderer>();
Check(renderers.Length > 100, "Camp and forest dressing are present");
Check(renderers.All(r => r.sharedMaterials.All(m => m != null && m.shader != null && m.shader.isSupported && m.shader.name != "Hidden/InternalErrorShader")),
    "All visible objects have supported materials");
Check(root.GetComponentsInChildren<MeshFilter>().All(f => f.sharedMesh != null), "All static meshes are assigned");
var lanes = root.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Player lane ")).ToArray();
Check(lanes.Length == 4, "Four lane anchors exist");
foreach (var lane in lanes)
{
    var mesh = lane.GetComponentInChildren<Renderer>();
    var collider = lane.GetComponent<BoxCollider>();
    Check(mesh.bounds.size.x > 19.9f && mesh.bounds.size.x < 20.1f && mesh.bounds.size.y < 1f,
        lane.name + ": imported geometry has the correct axis and metre scale");
    Check(collider != null && Mathf.Abs(collider.bounds.size.x - mesh.bounds.size.x) < .01f,
        lane.name + ": collider matches the walkway width");
}
var officer = root.GetComponentsInChildren<Transform>().Single(t => t.name == "Officer - animated direction cues");
var officerBounds = new Bounds(officer.position, Vector3.zero);
foreach (var renderer in officer.GetComponentsInChildren<Renderer>()) officerBounds.Encapsulate(renderer.bounds);
Check(officerBounds.size.y > 2f, "Multipart officer imports at human scale");
var ground = root.GetComponentsInChildren<Transform>().First(t => t.name == "Terrain").GetComponentInChildren<Renderer>();
Check(ground.bounds.size.x > 70 && ground.bounds.size.z > 70 && ground.bounds.size.y < 15,
    "Terrain imports upright at full scene scale");
var audio = UnityEngine.Object.FindAnyObjectByType<OutpostAudio>();
var palette = audio.palette;
Check(palette != null && palette.walking.Length == 10 && palette.running.Length == 10,
    "All original walking and running variations are retained");
foreach (var clip in palette.walking.Concat(palette.running).Concat(new[]{palette.titleMusic,palette.roundMusic,palette.leftCall,palette.rightCall,palette.landing,palette.healthPickup}))
    Check(clip != null && AssetDatabase.GetAssetPath(clip).StartsWith("Assets/Prototype/Sounds/"), "Original audio reused: " + (clip != null ? clip.name : "missing"));
Check(audio.music.clip == palette.titleMusic && audio.MusicHasStarted, "Original title music loops after the start lead-in");
audio.PlayRoundMusic();
Check(audio.music.clip == palette.roundMusic && audio.music.isPlaying, "Original round music can be selected");
audio.PlayDirection(false);
Check(audio.voice.isPlaying, "Left direction call plays");
audio.voice.Stop();
audio.PlayDirection(true);
Check(audio.voice.isPlaying, "Right direction call plays");
audio.PlayFootstep(false);
Check(audio.effects.isPlaying, "Walking footstep plays");
audio.PlayFootstep(true);
audio.PlayLanding();
audio.PlayHealthPickup();
Check(audio.effects.isPlaying, "Running, landing, and pickup audio outputs are connected");
audio.effects.Stop();
audio.voice.Stop();
audio.PlayTitleMusic();
return new { checksPassed = checks.Count, renderers = renderers.Length, checks };
