using UnityEngine;

namespace LeftRight.Rebuild
{
    /// <summary>Host confirmation and persistent guest notice. Opening this dialog
    /// never pauses an online simulation or closes its transport.</summary>
    public sealed class OnlineSessionDialog : MonoBehaviour
    {
        public bool Visible=>confirm||!string.IsNullOrEmpty(notice);
        public bool Confirming=>confirm;
        public string Notice=>notice;
        public const string HostEnded="The host left the drill.\nEveryone got kicked in the butt - back to the menu.";
        public const string HostLost="The host left or lost connection.\nEveryone is back at the menu.";
        StartMenu menu;bool confirm;string notice;
        public void Initialize(StartMenu owner)=>menu=owner;
        public void RequestHostLeave(){if(menu.online.IsHost&&!menu.online.Closing){notice=null;confirm=true;}}
        public void Dismiss(){confirm=false;notice=null;}
        public void Notify(string message){confirm=false;notice=message;}
        public void ConfirmLeave()
        {if(!confirm)return;confirm=false;menu.online.EndHostedSession();}
        void OnGUI()
        {
            if(!Visible||menu==null)return;
            GUI.depth=-100;
            var old=GUI.color;GUI.color=new Color(.02f,.035f,.025f,.94f);
            GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=old;
            bool portrait=Screen.height>Screen.width;float width=portrait?440:760,height=portrait?420:350;
            float scale=Mathf.Min(1,Screen.width/(width+32),Screen.height/(height+32));
            var matrix=GUI.matrix;GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-width*scale)/2,(Screen.height-height*scale)/2,0),Quaternion.identity,Vector3.one*scale);
            GUI.color=new Color(.51f,.56f,.33f);GUI.DrawTexture(new Rect(0,0,width,4),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(0,height-4,width,4),Texture2D.whiteTexture);GUI.color=old;
            var heading=new GUIStyle(GUI.skin.label){font=menu.menuFont,fontSize=portrait?22:26,alignment=TextAnchor.MiddleCenter,wordWrap=true};heading.normal.textColor=new Color(.96f,.91f,.73f);
            var body=new GUIStyle(heading){fontSize=portrait?16:18};
            GUI.Label(new Rect(20,25,width-40,65),confirm?"LEAVE THE DRILL?":"DRILL ENDED",heading);
            GUI.Label(new Rect(30,100,width-60,height-215),confirm?"Leaving ends this drill for everyone.\nAll joined recruits get kicked in the butt - back to the menu.":notice,body);
            var button=new GUIStyle(GUI.skin.button){font=menu.menuFont,fontSize=portrait?15:18,border=new RectOffset(12,12,12,12)};
            button.normal.background=menu.buttonTexture;button.hover.background=menu.buttonTexture;button.active.background=menu.pressedButtonTexture;
            button.normal.textColor=button.hover.textColor=new Color(.10f,.15f,.08f);
            float y=height-95;
            if(confirm)
            {
                if(GUI.Button(new Rect(24,y,width/2-36,64),"STAY",button))Dismiss();
                if(GUI.Button(new Rect(width/2+12,y,width/2-36,64),"LEAVE & KICK",button))ConfirmLeave();
            }
            else if(GUI.Button(new Rect(40,y,width-80,64),"BACK TO MENU",button))Dismiss();
            GUI.matrix=matrix;
        }
    }
}
