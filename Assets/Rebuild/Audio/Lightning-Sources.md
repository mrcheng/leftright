# Lightning audio

User-supplied Epidemic Sound recordings, provided on 2026-11-19:

- Warning: `ES_Weather, Thunder, Tropical, Distant Thunder, Cracking Thunder 02 - Epidemic Sound.wav`. Uses seconds 5.5–8.5, filtered to a low rumble with short edge fades, matching the three-second warning.
- Strike: `ES_Weather, Thunder, Thunder Clap, Heavy Impact, Explosive, Bright 03 - Epidemic Sound.wav`. Uses the clap and decay, with leading silence removed and a short tail fade.

- Wood: `ES_Wood, Impact, Big Planks Fall, Bounce - Epidemic Sound.wav`. Plays the break/fall/bounce recording with the strike, with leading silence removed and a short tail fade.

The game uses the mono 32 kHz edits `Resources/ThunderRumble.wav`, `Resources/LightningStrike.wav` and `Resources/LightningWoodBreak.wav`. All are one-shot effects; none loops as ambience. `Tools/Audio/PrepareLightningAudio.py` reproduces these edits from the supplied originals, which remain in the user's Downloads folder. These recordings are separate from the CC0 environment assets.
