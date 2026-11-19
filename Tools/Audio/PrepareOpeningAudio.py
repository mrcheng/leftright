"""Prepare supplied drink, burp and bottle set-down WAVs for the three-second intro."""
import subprocess
import sys
from pathlib import Path

root = Path(__file__).resolve().parents[2] / "Assets/Rebuild/Audio/Resources"
for source, name, duration, loudness in zip(sys.argv[1:4], ["OpeningDrink", "OpeningBurp", "OpeningBottleDown"], [.7, .32, .45], [-20, -18, -20]):
    # The ground recording has several spaced hits. Only its first impact belongs
    # to setting this bottle down; exclude the later repeats and leading silence.
    filters = f"silenceremove=start_periods=1:start_duration=0.002:start_threshold=-40dB,atrim=duration={duration},highpass=f=45,loudnorm=I={loudness}:TP=-2:LRA=8"
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", source, "-af", filters,
                    "-ac", "1", "-ar", "32000", "-c:a", "pcm_s16le", str(root / (name + ".wav"))], check=True)
