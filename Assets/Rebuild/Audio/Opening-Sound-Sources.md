# Opening scene audio

The user supplied these Epidemic Sound recordings on 2026-11-19:

- `ES_Food & Drink, Drinking, Human, Drink, Slurp From Cup - Epidemic Sound.wav`: the sip.
- `ES_Human, Burp, Forced - Epidemic Sound.wav`: the burp after drinking.
- `ES_Glass, Impact, Bottle, Glass, Hit, Ground 02 - Epidemic Sound.wav`: the first set-down impact only. Later repeated hits in the recording are excluded.

The short mono 32 kHz edits are `Resources/OpeningDrink.wav`, `OpeningBurp.wav` and `OpeningBottleDown.wav`. Leading silence is removed and loudness controlled; the bottle impact is capped at 0.45 seconds so its tail fits before the first music beat. `Tools/Audio/PrepareOpeningAudio.py` recreates them from the supplied originals, which remain in the user's Downloads folder. These recordings are separate from the CC0 environment assets.
