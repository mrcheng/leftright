using UnityEngine;

namespace LeftRight.Rebuild
{
    /// <summary>One equally weighted opening. Playback samples the existing
    /// scheduled countdown; it cannot postpone the music or the first command.</summary>
    public abstract class StartSequence : ScriptableObject
    {
        public bool available=true;
        public abstract StartPlayback CreatePlayback(StartContext context);
    }
    public sealed class StartContext
    {
        public readonly StartMenu menu;
        public readonly float duration,initialElapsed;
        public Camera Camera=>menu.view.gameCamera;
        public RebuildRoundStart Round=>menu.course.round;
        public StartContext(StartMenu menu,float duration,float elapsed){this.menu=menu;this.duration=duration;initialElapsed=elapsed;}
    }
    public abstract class StartPlayback
    {
        public abstract void Tick(float elapsed);
        public virtual void Stop(){}
    }
}
