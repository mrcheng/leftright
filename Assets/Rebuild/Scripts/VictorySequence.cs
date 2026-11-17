using UnityEngine;

namespace LeftRight.Rebuild
{
    /// <summary>One equally likely entry in the victory pool. Assets contain settings;
    /// a fresh playback object owns each run's state, so assets are safe to reuse.</summary>
    public abstract class VictorySequence : ScriptableObject
    {
        public bool available = true;
        public virtual bool PlayOpeningCheer => true;
        public abstract VictoryPlayback CreatePlayback(RecruitController winner, Camera camera);
    }

    public abstract class VictoryPlayback
    {
        public abstract void Tick(float elapsedSeconds);
        public virtual void Stop() { }
    }
}
