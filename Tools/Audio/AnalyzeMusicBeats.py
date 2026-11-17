from pathlib import Path
import wave, json, subprocess
import numpy as np
root=Path(__file__).resolve().parents[2]
results={}
for name, filename in [('title', 'Warmongers Title.mp3'), ('round', 'defcon_one_stereo.mp3')]:
    (root/'Temp').mkdir(exist_ok=True)
    subprocess.run(['ffmpeg','-hide_banner','-loglevel','error','-y','-i',
                    str(root/'Assets/Prototype/Sounds/Music'/filename),'-ac','1','-ar','11025',
                    str(root/'Temp'/f'{name}-beats.wav')], check=True)
    with wave.open(str(root/'Temp'/f'{name}-beats.wav')) as f:
        sample_rate=f.getframerate()
        samples=np.frombuffer(f.readframes(f.getnframes()),dtype=np.int16).astype(float)/32768
    hop=128;n=1024
    frames=np.lib.stride_tricks.sliding_window_view(samples,n)[::hop]
    spectrum=np.abs(np.fft.rfft(frames*np.hanning(n),axis=1))
    log=np.log1p(spectrum*8)
    flux=np.maximum(0,np.diff(log,axis=0)).mean(axis=1)
    env=np.concatenate(([0.0],flux))
    env=np.maximum(0,env-np.convolve(env,np.ones(17)/17,mode='same'))
    env/=max(env.max(),1e-8)
    fps=sample_rate/hop
    ac=np.correlate(env,env,mode='full')[len(env)-1:]
    ac/=ac[0]
    candidates=[]
    for lag in range(int(fps*60/180),int(fps*60/70)):
        if ac[lag]>ac[lag-1] and ac[lag]>=ac[lag+1]:
            candidates.append((round(60*fps/lag,2),round(float(ac[lag]),4)))
    candidates.sort(key=lambda item:item[1],reverse=True)
    print(name,'autocorrelation candidates',candidates[:12])
    best=(-1,None,None)
    # Refine recurring beat grids over the entire recording, rather than using rounded FFT lags.
    for bpm in np.arange(75,165,.05):
        period=fps*60/bpm
        for phase in np.arange(0,period,period/48):
            positions=np.arange(phase,len(env)-3,period)
            positions=positions[(positions>fps*2)&(positions<len(env)-fps*2)]
            # Permit a small onset width around each predicted beat.
            pulse=sum(np.interp(positions+d,np.arange(len(env)),env) for d in [-1,0,1])/3
            score=float(np.mean(pulse))
            if score>best[0]: best=(score,float(bpm),float(phase/fps))
    results[name]={'beatsPerMinute':round(best[1],2),'firstBeatSeconds':round(best[2],4),'confidenceScore':best[0],'candidates':candidates[:8]}
print(json.dumps(results,indent=2))
(root/'SourceArt/Audio/PrototypeMusicBeats.json').write_text(json.dumps(results,indent=2)+'\n')
