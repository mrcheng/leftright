using System;
using UnityEngine;

namespace LeftRight.Rebuild
{
    /// <summary>Shared safe placement for pickups and obstacles. Never clamp a random
    /// point under the player: intersect the requested distance with the open lane.</summary>
    public static class LaneSpawnPlacement
    {
        public static bool TryChoose(RecruitController player,LaneSpikeTrap trap,float near,float far,
            float margin,System.Random random,Func<float,bool> available,out float x)
        {
            float center=player.transform.position.x;
            float leftMin=Mathf.Max(trap.LeftTip+margin,center-far),leftMax=Mathf.Min(trap.RightTip-margin,center-near);
            float rightMin=Mathf.Max(trap.LeftTip+margin,center+near),rightMax=Mathf.Min(trap.RightTip-margin,center+far);
            bool leftValid=leftMin<=leftMax,rightValid=rightMin<=rightMax;
            for(int attempt=0;attempt<12&&(leftValid||rightValid);attempt++)
            {
                bool left=leftValid&&(!rightValid||random.Next(2)==0);
                x=Mathf.Lerp(left?leftMin:rightMin,left?leftMax:rightMax,(float)random.NextDouble());
                if(available==null||available(x))return true;
            }
            x=0;return false;
        }
    }
}
