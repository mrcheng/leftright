using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

if (!UnityEditor.EditorApplication.isPlaying) throw new Exception("Enter Play Mode on Scene1 first.");
var checks = new List<string>();
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks.Add(message);
}
bool hadScore = PlayerPrefs.HasKey("HighScore");
float oldScore = PlayerPrefs.GetFloat("HighScore");
async Task<PlayerManager[]> StartRound(int count)
{
    GameManager.Instance.Reset();
    await Task.Delay(500);
    var manager = GameManager.Instance;
    manager.Nazi.enabled = false; // Fall detection must work even without wrong-direction damage.
    manager.numberOfPlayerSlider.SetValueWithoutNotify(count);
    manager.StartGame();
    manager.StopAllCoroutines();
    await Task.Delay(1200);
    return UnityEngine.Object.FindObjectsByType<PlayerManager>()
        .Where(p => p.name.Contains("Clone")).OrderBy(p => p.GetComponent<PlayerController>().PlayerIndex).ToArray();
}
async Task WaitForDeath(PlayerManager player)
{
    for (int i = 0; i < 160 && player.Health > 0; i++) await Task.Delay(50);
    Check(player.Health == 0f, "Falling eliminates " + player.PlayerName + " (position " + player.transform.position + ", health " + player.Health + ")");
}
try
{
    foreach (int direction in new[] { -1, 1 })
    {
        var players = await StartRound(1);
        var manager = GameManager.Instance;
        Check(players.Length == 1 && players[0].GetComponent<PlayerPhysics>().grounded, "Solo player lands safely on spawn");
        players[0].transform.position += Vector3.up * 2f;
        await Task.Delay(150);
        Check(players[0].Health == 1f && !manager.GameOverButton.activeSelf, "Being airborne above the platform does not eliminate the player");
        await Task.Delay(1500);
        Check(players[0].GetComponent<PlayerPhysics>().grounded && players[0].Health == 1f, "Player lands safely after falling onto the platform");
        players[0].transform.position += Vector3.right * direction * 15f;
        await WaitForDeath(players[0]);
        Check(manager.GameOverButton.activeSelf, "Falling past the " + (direction < 0 ? "left" : "right") + " edge ends a solo game");
        Check(!players[0].Lane.GetComponent<Rigidbody>().isKinematic, "Falling releases the eliminated player's lane");
        await Task.Delay(100);
        float score = manager.score;
        await Task.Delay(150);
        Check(manager.score == score, "Score stops advancing after falling ends the game");
    }
    {
        var players = await StartRound(1);
        var player = players[0];
        player.transform.position = player.Lane.transform.position + new Vector3(8f, 1f, 0f);
        await Task.Delay(500);
        Check(player.GetComponent<PlayerPhysics>().grounded, "Player stands on a full-width platform before it shrinks");
        player.InflictDamage(player.HealthInSeconds / 2f);
        await WaitForDeath(player);
        Check(GameManager.Instance.GameOverButton.activeSelf, "Falling from a shrinking platform ends a solo game");
    }
    {
        var players = await StartRound(2);
        players[0].transform.position += Vector3.right * 15f;
        await WaitForDeath(players[0]);
        Check(!GameManager.Instance.GameOverButton.activeSelf && players[1].Health == 1f,
            "One player falling leaves the other multiplayer opponent in the game");
        Check(GameManager.Instance.GetPlayerInLead() == players[1], "Fallen player is removed from active players");
        players[1].transform.position += Vector3.left * 15f;
        await WaitForDeath(players[1]);
        Check(GameManager.Instance.GameOverButton.activeSelf, "Last multiplayer player falling ends the game");
    }
}
finally
{
    if (hadScore) PlayerPrefs.SetFloat("HighScore", oldScore); else PlayerPrefs.DeleteKey("HighScore");
    GameManager.Instance.Reset();
    await Task.Delay(500);
}
Check(GameManager.Instance.startGameButton.activeSelf && !GameManager.Instance.GameOverButton.activeSelf,
    "Restart returns to a fresh start screen");
return new { checksPassed = checks.Count, checks };

