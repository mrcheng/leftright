using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

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
bool hadCount = PlayerPrefs.HasKey("NumberOfPlayers");
float previousCount = PlayerPrefs.GetFloat("NumberOfPlayers");
Check(manager != null, "Game manager loads");
var keyboard = InputSystem.AddDevice<Keyboard>();
var pads = new List<Gamepad>();
var oldDirectionEnabled = manager.Nazi.enabled;
var uiInput = UnityEngine.Object.FindAnyObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
if (uiInput != null) uiInput.enabled = false;
try
{
    keyboard.MakeCurrent();
    var layouts = new[] {
        new[] { Key.LeftArrow, Key.RightArrow, Key.Delete, Key.RightShift },
        new[] { Key.A, Key.D, Key.LeftCtrl, Key.LeftShift },
        new[] { Key.I, Key.P, Key.Digit8, Key.Digit9 },
        new[] { Key.R, Key.Y, Key.Digit4, Key.Digit5 }
    };
    for (int player = 0; player < 4; player++)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(layouts[player][0]));
        InputSystem.Update();
        Check(LocalPlayerInput.Horizontal(player) == -1f, $"Player {player + 1}: original left binding");
        InputSystem.RemoveDevice(keyboard);
        keyboard = InputSystem.AddDevice<Keyboard>();
        keyboard.MakeCurrent();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(layouts[player][1], layouts[player][2], layouts[player][3]));
        InputSystem.Update();
        Check(LocalPlayerInput.Horizontal(player) == 1f && LocalPlayerInput.JumpPressed(player) && LocalPlayerInput.RunHeld(player),
            $"Player {player + 1}: original right, jump, and run bindings");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        InputSystem.Update();
    }
    for (int player = 0; player < 4; player++)
    {
        var pad = InputSystem.AddDevice<Gamepad>();
        pads.Add(pad);
        int slot = Array.IndexOf(Gamepad.all.ToArray(), pad);
        InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.right }.WithButton(GamepadButton.South).WithButton(GamepadButton.East));
        InputSystem.Update();
        Check(LocalPlayerInput.Horizontal(slot) > 0.9f && LocalPlayerInput.JumpPressed(slot) && LocalPlayerInput.RunHeld(slot),
            $"Gamepad {player + 1}: stick, jump, and run controls");
        InputSystem.QueueStateEvent(pad, new GamepadState());
        InputSystem.Update();
    }
    foreach (var pad in pads) InputSystem.RemoveDevice(pad);
    pads.Clear();

    manager.Nazi.enabled = false;
    manager.numberOfPlayerSlider.value = 4;
    manager.StartGame();
    await Task.Delay(1200);
    var controllers = UnityEngine.Object.FindObjectsByType<PlayerController>()
        .Where(player => player.name.Contains("Clone")).OrderBy(player => player.PlayerIndex).ToArray();
    Check(controllers.Length == 4, "Game starts four local players");
    Check(controllers.Select(p => p.PlayerIndex).Distinct().Count() == 4, "Each player has a separate input slot");
    foreach (var player in controllers)
    {
        var animator = player.GetComponent<Animator>();
        Check(animator != null && animator.runtimeAnimatorController != null && animator.runtimeAnimatorController.animationClips.Length > 0,
            $"Player {player.PlayerIndex + 1}: animation controller and clips survive import");
        Check(player.GetComponentInChildren<HealthBar>() != null, $"Player {player.PlayerIndex + 1}: overhead health bar survives the rig rebuild");
        var skin = player.GetComponentInChildren<SkinnedMeshRenderer>();
        Check(skin.sharedMaterial == player.GetComponent<PlayerManager>().BodyMaterial,
            $"Player {player.PlayerIndex + 1}: original body material is applied");
        Check(skin != null && skin.sharedMesh != null && skin.bones.Length > 0 && skin.bones.All(bone => bone != null),
            $"Player {player.PlayerIndex + 1}: character mesh and bones survive import");
        Check(skin.bones.Length == skin.sharedMesh.bindposes.Length, $"Player {player.PlayerIndex + 1}: skin binding count matches the mesh");
        var baked = new Mesh();
        skin.BakeMesh(baked);
        Check(baked.vertices.All(v => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z)),
            $"Player {player.PlayerIndex + 1}: animation produces finite skinned vertices");
        UnityEngine.Object.Destroy(baked);
        Check(skin.bounds.size.y > 0.5f && skin.bounds.size.y < 4f, $"Player {player.PlayerIndex + 1}: visible character has the correct scale");
        Check(player.GetComponent<PlayerPhysics>().grounded, $"Player {player.PlayerIndex + 1}: lands on its lane");
    }
    var first = controllers[0];
    var start = first.transform.position;
    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow));
    await Task.Delay(250);
    Check(first.transform.position.x > start.x + 0.1f, "Keyboard input moves a real player");
    float beforeJump = first.transform.position.y;
    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Delete));
    await Task.Delay(120);
    Check(first.transform.position.y > beforeJump + 0.1f, "Jump input lifts a grounded player");
    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
    var health = first.GetComponent<PlayerManager>();
    float before = health.Health;
    health.InflictDamage(1f);
    Check(health.Health < before, "Damage changes player health");
    health.GiveHealth(1f);
    Check(Mathf.Abs(health.Health - before) < 0.001f, "Health pickup effect restores health");
    float walk = first.walkSpeed;
    health.Freeze(0.1f);
    Check(first.walkSpeed == 0f, "Freeze temporarily stops movement");
    await Task.Delay(200);
    Check(Mathf.Abs(first.walkSpeed - walk) < 0.001f, "Freeze restores movement speed");
    await Task.Delay(100);
    Check(manager.score > 0f, "Score timer advances during play");
    manager.Nazi.SendMessage("Flip");
    before = health.Health;
    await Task.Delay(150);
    Check(health.Health < before, "Following the wrong direction damages the player and updates both health bars");
    foreach (var player in controllers) player.GetComponent<PlayerManager>().InflictDamage(1000f);
    await Task.Delay(250);
    Check(manager.GameOverButton.activeSelf, "All players dying shows the game-over/restart UI");
    Check(manager.Lanes.All(lane => !lane.GetComponent<Rigidbody>().isKinematic), "Defeated players release their lanes");
}
finally
{
    if (keyboard.added) InputSystem.RemoveDevice(keyboard);
    foreach (var pad in pads) if (pad.added) InputSystem.RemoveDevice(pad);
    manager.Nazi.enabled = oldDirectionEnabled;
    if (uiInput != null) uiInput.enabled = true;
    if (hadScore) PlayerPrefs.SetFloat("HighScore", previousScore); else PlayerPrefs.DeleteKey("HighScore");
    if (hadCount) PlayerPrefs.SetFloat("NumberOfPlayers", previousCount); else PlayerPrefs.DeleteKey("NumberOfPlayers");
}
manager.Reset();
await Task.Delay(750);
Check(GameManager.Instance != null && GameManager.Instance != manager, "Restart reloads a fresh game manager");
Check(GameManager.Instance.startGameButton.activeSelf && !GameManager.Instance.GameOverButton.activeSelf, "Restart restores the start UI");
Check(GameManager.Instance.Lanes.All(lane => lane.GetComponent<Rigidbody>().isKinematic), "Restart restores all lanes");
return new { checksPassed = checks.Count, checks };
