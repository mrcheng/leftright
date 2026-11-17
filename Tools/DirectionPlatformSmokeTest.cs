using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

if (!UnityEditor.EditorApplication.isPlaying) throw new Exception("Enter Play Mode on Scene1 first.");
var checks = new List<string>();
void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
    checks.Add(description);
}
var manager = GameManager.Instance;
bool hadScore = PlayerPrefs.HasKey("HighScore");
float previousScore = PlayerPrefs.GetFloat("HighScore");
manager.Reset();
await Task.Delay(750);
manager = GameManager.Instance;
manager.Nazi.enabled = false;
manager.numberOfPlayerSlider.SetValueWithoutNotify(4);
manager.StartGame();
manager.StopAllCoroutines(); // Keep random pickups from interfering with these checks.
await Task.Delay(1200);
try
{
    Check(!UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
        .Any(t => t.name.Contains("MasterHealth")), "Separate platform overlays are removed");
    var players = UnityEngine.Object.FindObjectsByType<PlayerManager>(FindObjectsSortMode.None)
        .Where(p => p.name.Contains("Clone")).OrderBy(p => p.GetComponent<PlayerController>().PlayerIndex).ToArray();
    Check(players.Length == 4, "Four players spawn on their platforms");
    foreach (var player in players)
    {
        var lane = player.Lane;
        var collider = lane.GetComponent<Collider>();
        Vector3 scale = lane.transform.localScale;
        Vector3 center = lane.transform.position;
        player.InflictDamage(player.HealthInSeconds / 2f);
        Physics.SyncTransforms();
        Check(Mathf.Abs(lane.transform.localScale.x - scale.x / 2f) < 0.001f,
            player.PlayerName + ": damage halves the solid platform width");
        Check(lane.transform.position == center && lane.transform.localScale.y == scale.y && lane.transform.localScale.z == scale.z,
            player.PlayerName + ": shrinking keeps the center, thickness, and depth");
        Check(Mathf.Abs(collider.bounds.size.x - lane.GetComponent<Renderer>().bounds.size.x) < 0.001f,
            player.PlayerName + ": visible platform and collider have matching widths");
        var edge = new Ray(center + new Vector3(scale.x * 0.45f, 2f, 0f), Vector3.down);
        Check(!collider.Raycast(edge, out _, 4f), player.PlayerName + ": removed edge has no collision");
        player.GiveHealth(player.HealthInSeconds / 2f);
        Check(Mathf.Abs(lane.transform.localScale.x - scale.x) < 0.001f,
            player.PlayerName + ": healing immediately restores the platform");
        player.GiveHealth(player.HealthInSeconds);
        Check(player.Health == 1f && lane.transform.localScale == scale, player.PlayerName + ": healing caps at the original platform width");
    }
    for (int cue = 0; cue < 2; cue++)
    {
        manager.Nazi.SendMessage("Flip");
        await Task.Delay(250);
        Vector3 expected = manager.Nazi.naziDirectionRight ? Vector3.right : Vector3.left;
        Check(Vector3.Dot(manager.Nazi.transform.up, expected) > 0.99999f,
            "Arrow stays exactly " + (manager.Nazi.naziDirectionRight ? "right" : "left") + " after animation updates");
    }
    var first = players[0];
    float before = first.Health;
    float beforeWidth = first.Lane.transform.localScale.x;
    await Task.Delay(200);
    Check(first.Health < before && first.Lane.transform.localScale.x < beforeWidth,
        "Wrong-direction penalty shrinks the platform during gameplay");
    first.transform.position = first.Lane.transform.position + new Vector3(8f, 1f, 0f);
    first.InflictDamage(first.HealthInSeconds / 2f);
    float beforeFall = first.transform.position.y;
    await Task.Delay(700);
    Check(first.transform.position.y < beforeFall - 1f, "Player falls through the removed section of platform");
    foreach (var player in players) player.InflictDamage(1000f);
    Check(manager.Lanes.All(lane => lane.transform.localScale.x == 0f && !lane.GetComponent<Collider>().enabled),
        "Zero health leaves no platform or collision");
    Check(manager.GameOverButton.activeSelf, "All players dying still shows game over");
}
finally
{
    if (hadScore) PlayerPrefs.SetFloat("HighScore", previousScore); else PlayerPrefs.DeleteKey("HighScore");
    manager.Reset();
    await Task.Delay(750);
}
Check(GameManager.Instance.Lanes.All(lane => lane.transform.localScale.x == 20f && lane.GetComponent<Collider>().enabled),
    "Restart restores full platform widths and collision");
return new { checksPassed = checks.Count, checks };
