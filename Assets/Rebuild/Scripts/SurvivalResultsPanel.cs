using System.Linq;
using UnityEngine;

namespace LeftRight.Rebuild
{
    /// <summary>Portrait-sized native/Editor results. The web build uses the same
    /// presenter with a scrollable HTML panel and a real phone keyboard.</summary>
    public static class SurvivalResultsPanel
    {
        public static void Draw(SurvivalResults results)
        {
            var menu=results.Menu;bool portrait=Screen.height>Screen.width,survival=menu.IsSurvival;
            float width=portrait?480:1000,height=portrait?(survival?980:860):(survival?860:720);
            float scale=Mathf.Min(Screen.width/width,Screen.height/height);
            var matrix=GUI.matrix;var color=GUI.color;var background=GUI.backgroundColor;bool enabled=GUI.enabled;
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-width*scale)*.5f,(Screen.height-height*scale)*.5f,0),Quaternion.identity,Vector3.one*scale);
            GUI.color=Color.white;
            GUIStyle Text(int size,TextAnchor alignment=TextAnchor.MiddleLeft)=>new GUIStyle(GUI.skin.label){font=menu.menuFont,fontSize=size,alignment=alignment,wordWrap=true,richText=false,normal={textColor=new Color(.96f,.91f,.73f)}};
            void Bar(Rect rect,Color value){var old=GUI.color;GUI.color=value;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;}
            bool Button(Rect rect,string text,bool primary=false)
            {
                GUI.backgroundColor=primary?new Color(.79f,.82f,.47f):new Color(.58f,.63f,.49f);
                var style=new GUIStyle(GUI.skin.button){font=menu.menuFont,fontSize=portrait?16:18,border=new RectOffset(12,12,12,12),normal={background=menu.buttonTexture,textColor=new Color(.12f,.15f,.1f)},active={background=menu.pressedButtonTexture,textColor=Color.black},hover={background=menu.buttonTexture,textColor=Color.black}};
                bool clicked=GUI.Button(rect,text,style);GUI.backgroundColor=background;return clicked;
            }
            try
            {
                Bar(new Rect(-2000,-2000,6000,6000),new Color(.035f,.055f,.04f,.97f));
                Bar(new Rect(0,0,width,6),new Color(.5f,.54f,.32f));
                Bar(new Rect(0,height-6,width,6),new Color(.5f,.54f,.32f));
                GUI.Label(new Rect(24,16,width-48,42),survival?"SURVIVAL RESULTS":"ROUND RESULTS",Text(portrait?24:30,TextAnchor.MiddleCenter));
                GUI.Label(new Rect(24,64,width-48,42),"YOU  "+SurvivalLeaderboardRules.Time(results.PersonalScore),Text(portrait?26:30,TextAnchor.MiddleCenter));
                GUI.Label(new Rect(24,111,width-48,28),(menu.Scores.NewBest?"NEW PERSONAL BEST  ":"PERSONAL BEST  ")+SurvivalScore.Format(menu.Scores.BestSeconds),Text(portrait?12:16,TextAnchor.MiddleCenter));
                var ranking=Enumerable.Range(0,menu.Scores.Times.Length).OrderByDescending(i=>menu.course.players[i]==menu.course.Winner).ThenByDescending(i=>menu.Scores.Times[i]).ToArray();
                string[] names={"GREEN","BLUE","YELLOW","RED"};
                int local=menu.online!=null&&menu.online.Active?menu.online.LocalSlot:0;
                for(int row=0;row<ranking.Length;row++)
                {
                    int i=ranking[row];float rowSize=survival?28:(portrait?54:48),y=154+row*rowSize;
                    bool winner=menu.course.players[i]==menu.course.Winner;
                    if(winner)Bar(new Rect(24,y,width-48,rowSize),new Color(.25f,.3f,.17f));
                    int size=survival?(portrait?14:18):(portrait?18:20);
                    GUI.Label(new Rect(32,y,width-205,rowSize),names[i]+(i==local?" (YOU)":"")+(winner?" *":""),Text(size));
                    GUI.Label(new Rect(width-170,y,138,rowSize),SurvivalScore.Format(menu.Scores.Times[i]),Text(size,TextAnchor.MiddleRight));
                }
                if(!survival)
                {
                    GUI.enabled=enabled&&results.CanReplay;
                    if(Button(new Rect(24,portrait?590:574,portrait?width-48:width*.5f-36,64),results.CanReplay?"PLAY AGAIN":"WAITING FOR HOST",true))results.PlayAgain();
                    GUI.enabled=enabled;
                    if(Button(new Rect(portrait?24:width*.5f+12,portrait?686:574,portrait?width-48:width*.5f-36,64),"MAIN MENU"))results.MainMenu();
                    return;
                }
                float titleY=portrait?280:290,tableY=portrait?366:386,rowHeight=portrait?28:25;
                GUI.Label(new Rect(24,titleY,width-48,58),"HONOR TO OUR GLORIOUS RECRUITS\nTHAT SURVIVED FOR THIS LONG",Text(portrait?13:18,TextAnchor.MiddleCenter));
                GUI.Label(new Rect(24,titleY+56,width-48,24),results.Scope+" TOP 10",Text(portrait?12:14,TextAnchor.MiddleCenter));
                var entries=results.Table?.entries;
                for(int i=0;i<SurvivalLeaderboardRules.Limit;i++)
                {
                    float y=tableY+i*rowHeight;var entry=entries!=null&&i<entries.Length?entries[i]:null;
                    if(entry!=null&&entry.runId==results.RunId)Bar(new Rect(24,y,width-48,rowHeight),new Color(.25f,.3f,.17f));
                    GUI.Label(new Rect(28,y,40,rowHeight),(i+1).ToString("00"),Text(portrait?16:18));
                    GUI.Label(new Rect(76,y,width-252,rowHeight),entry==null?"---":entry.name.ToUpperInvariant(),Text(portrait?16:18));
                    GUI.Label(new Rect(width-168,y,136,rowHeight),entry==null?"--:--.--":SurvivalLeaderboardRules.Time(entry.hundredths),Text(portrait?16:18,TextAnchor.MiddleRight));
                }
                float formY=portrait?698:708;
                if(results.Qualifies)
                {
                    GUI.Label(new Rect(24,formY-32,width-48,28),"TOP 10! ENTER YOUR NAME",Text(portrait?13:16,TextAnchor.MiddleCenter));
                    GUI.enabled=enabled&&!results.Submitting;
                    GUI.SetNextControlName("SurvivalScoreName");
                    results.DraftName=GUI.TextField(new Rect(24,formY,width-194,portrait?56:48),results.DraftName,SurvivalLeaderboardRules.NameLength,new GUIStyle(GUI.skin.textField){font=menu.menuFont,fontSize=portrait?18:20,alignment=TextAnchor.MiddleLeft,richText=false});
                    GUI.enabled=GUI.enabled&&SurvivalLeaderboardRules.Name(results.DraftName).Length>0;
                    if(Button(new Rect(width-154,formY,130,portrait?56:48),results.Submitting?"...":"SAVE",true))results.SubmitName(results.DraftName);
                    GUI.enabled=enabled;
                }
                else if(results.Table==null&&!results.Loading)
                {if(Button(new Rect(width*.5f-110,formY,220,48),"TRY AGAIN"))results.Refresh();}
                GUI.Label(new Rect(24,portrait?762:758,width-48,40),results.Message,Text(portrait?11:13,TextAnchor.MiddleCenter));
                GUI.enabled=enabled&&results.CanReplay;
                if(Button(new Rect(24,portrait?814:800,portrait?width-48:width*.5f-36,portrait?64:48),results.CanReplay?"PLAY AGAIN":"WAITING FOR HOST",true))results.PlayAgain();
                GUI.enabled=enabled;
                if(Button(new Rect(portrait?24:width*.5f+12,portrait?898:800,portrait?width-48:width*.5f-36,portrait?56:48),"MAIN MENU"))results.MainMenu();
            }
            finally{GUI.matrix=matrix;GUI.color=color;GUI.backgroundColor=background;GUI.enabled=enabled;}
        }
    }
}
