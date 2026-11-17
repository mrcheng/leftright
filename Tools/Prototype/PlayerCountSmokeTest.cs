using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

if (!UnityEditor.EditorApplication.isPlaying) throw new Exception("Enter Play Mode on Scene1 first.");
var checks = new List<string>();
void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
    checks.Add(description);
}
bool hadCount = PlayerPrefs.HasKey("NumberOfPlayers");
float oldCount = PlayerPrefs.GetFloat("NumberOfPlayers");
bool hadScore = PlayerPrefs.HasKey("HighScore");
float oldScore = PlayerPrefs.GetFloat("HighScore");
try
{
    for (int count = 1; count <= 4; count++)
    {
        GameManager.Instance.Reset();
        await Task.Delay(500);
        var manager = GameManager.Instance;
        manager.Nazi.enabled = false;
        Check(manager.numberOfPlayerSlider.maxValue == 4 && manager.numberOfPlayerSlider.wholeNumbers,
            count + " players: slider supports whole numbers from one to four");
        manager.numberOfPlayerSlider.value = count;
        manager.SetNumbersOfPlayers();
        Check(manager.Lanes.Count(l => l.gameObject.activeSelf) == count,
            count + " players: settings preview hides unused lanes");
        Check(manager.txt_numberOfPlayers.text == count.ToString() && PlayerPrefs.GetFloat("NumberOfPlayers") == count,
            count + " players: selection label and saved setting agree");
        Check(!UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Any(t => t.name.EndsWith("DebugInfo")),
            count + " players: redundant top info boxes are removed");
        manager.StartGame();
        manager.StopAllCoroutines();
        manager.StartGame();
        await Task.Delay(1000);
        var players = UnityEngine.Object.FindObjectsByType<PlayerManager>(FindObjectsSortMode.None)
            .Where(p => p.name.Contains("Clone")).OrderBy(p => p.GetComponent<PlayerController>().PlayerIndex).ToArray();
        Check(players.Length == count, count + " players: exactly the selected number spawn, including repeated start");
        Check(players.All(p => p.GetComponentsInChildren<Transform>(true).All(t => t.name != "HealthBar")),
            count + " players: no overhead health bars are spawned");
        Check(players.Select((p, i) => p.GetComponent<PlayerController>().PlayerIndex == i && p.Lane == manager.Lanes[i]
            && p.GetComponent<PlayerPhysics>().grounded).All(v => v), count + " players: each input slot lands on its own active lane");
        foreach (string method in new[] { "SpawnHealthPickup", "SpawnFreezePickup", "SpawnSpeedPickup", "SpawnDamagedPickup", "SpawnDizzyPickup" })
        {
            var routine = (IEnumerator)typeof(GameManager).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null);
            routine.MoveNext(); // Wait interval.
            routine.MoveNext(); // Spawn one pickup.
        }
        var pickups = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .Where(t => t.name.EndsWith("(Clone)") && t.name.Contains("Pickup")).ToArray();
        Check(pickups.Length == 5 && pickups.All(p => manager.Lanes.Take(count).Any(l => Mathf.Abs(l.transform.position.z - p.position.z) < 0.001f)),
            count + " players: all five pickup types spawn only on participating lanes");
        if (count > 1)
        {
            players[count - 1].InflictDamage(1000f);
            Check(!manager.GameOverButton.activeSelf, count + " players: one death keeps remaining players in the round");
        }
        foreach (var player in players.Take(count > 1 ? count - 1 : count)) player.InflictDamage(1000f);
        Check(manager.GameOverButton.activeSelf, count + " players: round ends when all selected players die");
        manager.Reset();
        await Task.Delay(500);
        Check(GameManager.Instance.Lanes.Count(l => l.gameObject.activeSelf) == count && GameManager.Instance.numberOfPlayerSlider.value == count,
            count + " players: restart restores the saved player count and lane visibility");
    }
}
finally
{
    if (hadCount) PlayerPrefs.SetFloat("NumberOfPlayers", oldCount); else PlayerPrefs.DeleteKey("NumberOfPlayers");
    if (hadScore) PlayerPrefs.SetFloat("HighScore", oldScore); else PlayerPrefs.DeleteKey("HighScore");
    GameManager.Instance.Reset();
    await Task.Delay(500);
}
return new { checksPassed = checks.Count, checks };
