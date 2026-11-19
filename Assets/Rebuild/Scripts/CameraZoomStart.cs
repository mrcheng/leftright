using UnityEngine;

namespace LeftRight.Rebuild
{
    [CreateAssetMenu(menuName="LeftRight/Start/Camera zoom")]
    public sealed class CameraZoomStart : StartSequence
    {
        public override StartPlayback CreatePlayback(StartContext context)=>new Playback(context);
        sealed class Playback : StartPlayback
        {
            readonly StartContext context;readonly Vector3 from;readonly Quaternion rotation;readonly float fov;
            public Playback(StartContext context){this.context=context;from=context.Camera.transform.position;rotation=context.Camera.transform.rotation;fov=context.Camera.fieldOfView;}
            public override void Tick(float elapsed)
            {
                float t=Mathf.SmoothStep(0,1,elapsed/context.duration);
                context.menu.view.GameplayPose(out var target,out var targetRotation,out float targetFov);
                context.Camera.transform.SetPositionAndRotation(Vector3.Lerp(from,target,t),Quaternion.Slerp(rotation,targetRotation,t));context.Camera.fieldOfView=Mathf.Lerp(fov,targetFov,t);
            }
        }
    }
}
