# Electrocution sound

The user supplied `ES_Electricity, Arc, Current, Beam, Surge, Aggressive, Harsh, Perfect Loop - Epidemic Sound.wav` from Epidemic Sound on 2026-11-19. The original remains in Downloads. `Resources/Electrocution.wav` preserves the full loop duration, converted to mono 32 kHz PCM with an 80 Hz high-pass and controlled loudness (-22 LUFS, -2 dBTP). The playback source loops only while the two-masterbeat shock is active, fades over its final 0.12 beat, and pauses with music. It is separate from the CC0 environment assets.

Recreate with FFmpeg: `-af "highpass=f=80,loudnorm=I=-22:TP=-2:LRA=8" -ac 1 -ar 32000 -c:a pcm_s16le`.
