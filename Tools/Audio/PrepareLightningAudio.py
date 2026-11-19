"""Prepare the user-supplied thunder recordings for the one-shot lane hazard.

Usage: python Tools/Audio/PrepareLightningAudio.py <distant-thunder.wav> <clap.wav> <wood.wav>
Original recordings stay outside Unity; the game imports only these short edits.
"""
import subprocess
import sys
from pathlib import Path

output = Path(__file__).resolve().parents[2] / "Assets/Rebuild/Audio/Resources"
output.mkdir(parents=True, exist_ok=True)
rumble, clap, wood = sys.argv[1:4]
edits = [
    (rumble, "ThunderRumble", ["-ss", "5.5", "-t", "3"],
     "highpass=f=35,lowpass=f=1200,afade=t=in:d=0.12,afade=t=out:st=2.8:d=0.2,loudnorm=I=-21:TP=-2:LRA=7"),
    (clap, "LightningStrike", [],
     "silenceremove=start_periods=1:start_duration=0.002:start_threshold=-45dB,highpass=f=25,loudnorm=I=-17:TP=-1.5:LRA=9,afade=t=out:st=5.8:d=0.7"),
    (wood, "LightningWoodBreak", [],
     "silenceremove=start_periods=1:start_duration=0.002:start_threshold=-45dB,highpass=f=40,loudnorm=I=-18:TP=-2:LRA=9,afade=t=out:st=5.5:d=0.7"),
]
for source, name, trim, filters in edits:
    subprocess.run(["ffmpeg", "-v", "error", "-y", *trim, "-i", source,
                    "-af", filters, "-ac", "1", "-ar", "32000", "-c:a", "pcm_s16le",
                    str(output / (name + ".wav"))], check=True)
    print("Prepared " + name)
