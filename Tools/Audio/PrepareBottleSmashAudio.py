"""Trim the supplied glass hit to start with the bottle impact. Pass its WAV path."""
import subprocess
import sys
from pathlib import Path

output = Path(__file__).resolve().parents[2] / "Assets/Rebuild/Audio/Resources/VodkaBottleSmash.wav"
subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", sys.argv[1], "-af",
                "silenceremove=start_periods=1:start_duration=0.002:start_threshold=-40dB,highpass=f=45,loudnorm=I=-17:TP=-1.5:LRA=8",
                "-ac", "1", "-ar", "32000", "-c:a", "pcm_s16le", str(output)], check=True)
