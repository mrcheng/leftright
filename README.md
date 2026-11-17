# Left Right

Unity 6.6 project (`6000.6.9f1`) containing the playable prototype and a separate starting point for its rebuild.

## Prototype snapshot

The annotated Git tag `prototype-v1` preserves the full working project before cleanup, including scripts, assets, package versions, settings, and smoke checks. A scene copy alone would still share those dependencies.

For an independent reference checkout, run `git worktree add --detach ../leftright-prototype prototype-v1` and open that directory as a separate Unity project. That snapshot uses the original scene path, `Assets/Scenes/Scene1.unity`. Keep the tag unchanged. The tag and snapshot commit are local until pushed.

The prototype remains playable at `Assets/Prototype/Scenes/Prototype.unity`. Use **LeftRight > Open prototype reference**, then Play. Its assets and runtime code live under `Assets/Prototype`; treat them as reference material. Git preserves the exact original even if these working files change later.

## Rebuild

Open `Assets/Rebuild/Scenes/Main.unity`, or use **LeftRight > Open rebuild**. It contains a low-poly military outpost: four raised walkways, a command post, barracks, tents, sandbags, wire, watchtowers, and forest scenery. Put new scripts in `Assets/Rebuild/Scripts`, using the `LeftRight.Rebuild` namespace. Its assembly has no dependency on the prototype assembly. The officer is an animated blockout character; the player gameplay loop is not connected yet.

Rebuild the smallest complete gameplay loop first: one player, one platform, left/right cues, shrinking, falling, game over, and restart. Then add local multiplayer, presentation, and pickups separately. Use prefabs for repeated players and lanes, and keep movement, round rules, and UI separate from one another.

Avoid editing prototype assets to serve the new game. Copy a needed visual asset into the rebuild through Unity so it gets its own GUID. Keep render pipeline settings shared until the rebuild needs different rendering. Audio is intentionally shared: `Assets/Rebuild/Settings/PrototypeAudio.asset` references the exact original title/round music, direction calls, footsteps, landing, and health-pickup clips. Play Mode waits three seconds, then starts the original title music on its opening beat; `OutpostAudio` exposes the other cues for the future game flow.

The prototype remains the enabled build scene while the rebuild has no gameplay. Main is listed but disabled. Switch the build entry point to Main and disable the prototype once the new loop is playable. **LeftRight > Build prototype for Windows** always builds only the prototype.

## Environment assets

The starter models are from [Kenney Survival Kit 2.0](https://kenney.nl/assets/survival-kit), released under CC0. This is a general survival/camp kit, dressed here as a fictional WWII-inspired outpost. The selected FBX files, palette texture, original license, and provenance live in `Assets/Rebuild/Art/KenneySurvivalKit`. The full original download is retained outside Unity imports in `SourceArt/Environment/KenneySurvivalKit`.

Additional military props were made in Blender: `SourceArt/Environment/OutpostProps.blend` is the editable prop library, and `build_outpost_props.py` regenerates its FBX exports. Run `blender --background --python SourceArt/Environment/build_outpost_props.py`, let Unity finish importing, then use **LeftRight > Rebuild > Build outpost environment** to regenerate the arrangement. This replaces the generated `Outpost Environment` hierarchy; keep manual scene additions outside it. Material tweaks belong in the builder/palette if they need to survive regeneration. The saved prefab is `Assets/Rebuild/Prefabs/OutpostEnvironment.prefab`.

See [the environment preview](SourceArt/Environment/OutpostPreview.png). The original prototype scene and art have not been changed for this environment work.

With Main open in Play Mode, run `unity command script eval --file Tools/Rebuild/VerifyOutpost.cs --timeout 60` for the environment and audio checks. All 52 checks passed after setup, including model scale, walkway colliders, materials, camera/listener counts, and the original audio palette/playback. These checks exercise the audio outputs, so expect music and short sound cues.

`RebuildRoundStart` owns startup: three seconds of silence with the officer still and both tower floodlights off, then music, marching, the first left cue, and its floodlight start together. Music is scheduled on the DSP clock from the measured opening beat, skipping the short pre-beat pickup. Subsequent direction cues alternate right/left with a fresh random 2–5 second wait, matching the prototype's ongoing pattern. The active arm swings out over 0.25 seconds and holds exactly horizontally while the other arm lowers. The original German voice call plays when each direction changes. Future start-screen logic can call `BeginRound`/`CancelStart` and switch off `RebuildRoundStart.playOnStart`. `OfficerDirections` also exposes `ActivateDirections`, `StopDirections`, `DirectionRight`, `IsIdling`, and `DirectionChanged` for the round rules. **LeftRight > Rebuild > Update round start timing** installs the startup coordination without rebuilding the environment. `Tools/Rebuild/VerifyRoundStart.cs` passed 17 checks, including silence during the lead-in, first-beat alignment, cancellation, and restart; `VerifyOfficerDirections.cs` checks ongoing cues and floodlights independently.

The officer wears the reference's darker coat/cap and light collar, with the lectern removed. `OfficerMarch` alternates high knees in place, bends the knees to keep the boots level, and swings the free arm while preserving the pointing arm. Footfalls follow the music sample clock, with quiet original boot sounds. Title/round beat estimates (115/137 BPM with separate onset offsets) live in `PrototypeAudio.asset`; the analysis is recorded in `SourceArt/Audio/PrototypeMusicBeats.json`. Pause, seeking, track changes, and game time scale follow the audio rather than a separate animation timer. **LeftRight > Rebuild > Update marching officer** updates the rig without rebuilding the environment. `Tools/Rebuild/VerifyOfficerMarch.cs` passed 15 focused checks. To repeat the onset analysis using Blender's bundled NumPy and the installed FFmpeg, run `blender --background --python Tools/Audio/AnalyzeMusicBeats.py`.

Both flags use flexible fabric meshes with their pole edges pinned. `WindFlag` adds gentle sag, traveling ripples, and wind gusts. Both flags match the right flag's direction and phase, so the fabric extends leftward and ripples together. The fabric is visible from both sides, and each flag animates its own mesh without modifying the shared source asset. This is lightweight procedural wind deformation, without a cloth-physics package. `Tools/Rebuild/VerifyWindFlags.cs` checks motion, shared wind direction, pinned edges, rigid poles, mesh ownership, and re-enabling.

The rebuild uses a procedural stormy evening sky, with drifting cloud banks, a faint warm horizon, cooler ambient light, and damp distance haze. It adds no external texture pack or weather package. Adjust `Assets/Rebuild/Settings/StormEveningSky.mat` for cloud cover, drift, colors, and sky brightness. **LeftRight > Rebuild > Apply stormy evening** applies the matching camp lighting without changing camera framing, and the scene builder uses the same settings. `Tools/Rebuild/VerifyStormSky.cs` passed 11 checks, including shader compilation, visible cloud variation, evening brightness, and retained framing.

Each watchtower has one roof-mounted floodlight, with an emissive gridded lamp housing and a warm-white spotlight aimed across the lanes and command post. `DirectionFloodlights` follows the officer: a left cue turns on only the left tower's beam and glowing panel; a right cue turns on only the right. Both are off during the initial neutral wait and when the sequence stops or resets. The lights aim toward their corresponding halves of the stage. **LeftRight > Rebuild > Install tower floodlights** updates the fixtures without rebuilding the scene, and the environment builder includes them automatically. The mounting and housing models are in the editable Blender prop library. The spots use the existing URP pipeline's lighting capabilities; additional-light shadows are disabled in that shared pipeline.

The four streetlamps remain on independently of the direction cues. Their point lights sit just below the actual lamp heads, with warm pools reaching the ground and nearby structures. **LeftRight > Rebuild > Update streetlamp lighting** refreshes their brightness, range, bulb emission, and attachment to the fixtures. The update tools rebind imported materials by source name, including the floodlight lens slots, to repair material assignments after FBX re-exports.

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
