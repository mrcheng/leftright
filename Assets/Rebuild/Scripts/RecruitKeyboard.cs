using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace LeftRight.Rebuild
{
    /// <summary>Each bank is left / jump / right. Ownership and bank are separate:
    /// online guests use bank zero even when the server assigns them lane four.</summary>
    public static class RecruitKeyboard
    {
        static readonly Key[][] banks={
            new[]{Key.A,Key.S,Key.D},new[]{Key.T,Key.Y,Key.U},
            new[]{Key.N,Key.M,Key.Comma},new[]{Key.O,Key.P,Key.LeftBracket}};
        public static KeyControl Control(Keyboard keyboard,int bank,int action)
        {
            bank=Mathf.Clamp(bank,0,3);action=Mathf.Clamp(action,0,2);
            // Swedish keyboards label the key beside P as Å. Resolve the actual
            // layout when available; its physical key is the fallback elsewhere.
            if(bank==3&&action==2)return keyboard.FindKeyOnCurrentKeyboardLayout("\u00e5")??keyboard[Key.LeftBracket];
            return keyboard[banks[bank][action]];
        }
    }
}
