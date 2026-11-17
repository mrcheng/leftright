# Left Right

Unity 6.6 project (`6000.6.9f1`) containing the playable prototype and a separate starting point for its rebuild.

## Prototype snapshot

The annotated Git tag `prototype-v1` preserves the full working project before cleanup, including scripts, assets, package versions, settings, and smoke checks. A scene copy alone would still share those dependencies.

For an independent reference checkout, run `git worktree add --detach ../leftright-prototype prototype-v1` and open that directory as a separate Unity project. That snapshot uses the original scene path, `Assets/Scenes/Scene1.unity`. Keep the tag unchanged. The tag and snapshot commit are local until pushed.

The prototype remains playable at `Assets/Prototype/Scenes/Prototype.unity`. Use **LeftRight > Open prototype reference**, then Play. Its assets and runtime code live under `Assets/Prototype`; treat them as reference material. Git preserves the exact original even if these working files change later.

## Rebuild

Open `Assets/Rebuild/Scenes/Main.unity`, or use **LeftRight > Open rebuild**. It contains only the default camera and light. Put new scripts in `Assets/Rebuild/Scripts`, using the `LeftRight.Rebuild` namespace. Its assembly has no dependency on the prototype assembly.

Rebuild the smallest complete gameplay loop first: one player, one platform, left/right cues, shrinking, falling, game over, and restart. Then add local multiplayer, presentation, and pickups separately. Use prefabs for repeated players and lanes, and keep movement, round rules, and UI separate from one another.

Avoid editing prototype assets to serve the new game. Copy a needed asset into the rebuild through Unity so it gets its own GUID. Keep render pipeline settings shared until the rebuild needs different rendering.

The prototype remains the enabled build scene while the rebuild is empty. Main is listed but disabled. Switch the build entry point to Main and disable the prototype once the new loop is playable. **LeftRight > Build prototype for Windows** always builds only the prototype.

## Cleanup

`Archive` retains 23 assets not referenced by the prototype or rendering settings: migration candidates, an old import package, unused animation clips, materials, images, and audio. Their original metadata is retained. `Archive/asset-moves.json` maps the old paths to their archive paths. The completed project-upgrade script is archived too, so it cannot rewrite the rebuilt project.

All prototype runtime scripts remain necessary. `Perlin` and `LocalPlayerInput` are called from code even though they have no scene component references. URP, Input System, uGUI, and Unity Pipeline remain in use; the latter supports editor automation. Redundant direct declarations for UI, IMGUI, and JSON serialization modules were removed; their owning packages still bring them in as dependencies.

`Library`, `Temp`, `Logs`, `UserSettings`, and builds are generated and ignored. `SourceArt` and `Misc` remain source/reference material outside Unity imports.

## Checks

Use **LeftRight > Validate scenes** to check both scenes for missing scripts. Prototype checks live in `Tools/Prototype` and run through the installed Unity Pipeline CLI, with the prototype open in Play Mode:

```powershell
unity command script eval --file Tools/Prototype/PlayerCountSmokeTest.cs --timeout 60
unity command script eval --file Tools/Prototype/DirectionPlatformSmokeTest.cs --timeout 60
unity command script eval --file Tools/Prototype/FallDeathSmokeTest.cs --timeout 60
```

These scripts reload scenes and simulate gameplay; run them in a disposable Play session. `SmokeTest.cs` also retains broader legacy input, animation, and pickup checks. It previously stopped at a Player 3 body-material identity check, so it is not a fully passing baseline.

After cleanup, all 99 focused checks passed (43 player-count/UI checks, 33 direction/platform checks, and 23 fall/death checks). All 135 original asset GUIDs were retained, and the moved asset contents match the snapshot.
