using UnityEngine;
namespace LeftRight.Rebuild
{
    [DefaultExecutionOrder(90)]
    public sealed class ShockBone : MonoBehaviour
    {
        public Transform anchor;public Vector3 offset,size;
        void LateUpdate(){if(anchor==null)return;transform.SetPositionAndRotation(anchor.position+anchor.rotation*offset,anchor.rotation);transform.localScale=size;}
    }
}
