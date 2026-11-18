using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LeftRight.Rebuild
{
    [DefaultExecutionOrder(-150)]
    public sealed class StartMenu : MonoBehaviour
    {
        public SpikeCourse course;
        public GameViewSettings view;
        public Texture2D buttonTexture;
        public Texture2D pressedButtonTexture;
        public Font menuFont;
        public int PlayerCount { get; private set; } = 4;
        public bool IsOpen { get; private set; }
        public enum MenuPage { Main, LocalGame, Settings, Online }
        public MenuPage Page { get; private set; }
        public OnlineMatch online;
        public bool OnlineAvailable => online != null;
#if UNITY_WEBGL && !UNITY_EDITOR
        float nextWebStatus;
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void LeftRight_UpdateLobby(string code,string status,int humans,int host,int playing,float x,int jumping,
            int menu,int over,float beat,float travel,string receiver);
        void LateUpdate()
        {
            if(online==null||Time.unscaledTime<nextWebStatus)return;
            nextWebStatus=Time.unscaledTime+.25f;
            // Public presentation state also enables the browser's touch controls in
            // local games. Never send service credentials to the page.
            int slot=online.Active?online.LocalSlot:0;
            var local=slot>=0&&slot<course.players.Length?course.players[slot]:null;
            bool playing=online.RoundActive||course.round.IsCountingDown||course.round.HasStarted;
            LeftRight_UpdateLobby(online.JoinCode,online.Status,online.HumanCount,online.IsHost?1:0,playing?1:0,
                local!=null?local.transform.position.x:0,local!=null&&local.IsJumping?1:0,
                IsOpen?(int)Page+1:0,course.IsGameOver?1:0,(float)course.round.masterBeat.BeatPosition,
                slot>=0&&slot<course.traps.Length?course.traps[slot].CurrentTravel:0,gameObject.name);
        }
#endif
        // A real HTML input supplies the mobile keyboard; IMGUI's code field remains
        // available to desktop players. Resolve this receiver from the live scene.
        public void JoinBrowserRoom(string code)
        {
            if(online==null||online.Active||online.Busy)return;
            OpenOnlineLobby();
            online.JoinInternetFromMenu(code.Trim().ToUpperInvariant());
        }
        public void SetBrowserTextFocus(string focused)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.captureAllKeyboardInput=focused!="1";
#endif
        }
        string joinCode = "";
        readonly System.Collections.Generic.Dictionary<Transform,float> laneZ = new System.Collections.Generic.Dictionary<Transform,float>();
        float masterVolume;
        bool inMatchSettings;
        RecruitController[] allPlayers;
        LaneSpikeTrap[] allTraps;
        Transform[] lanePlatforms;
        GUIStyle title, label, small, button, centered;
        VictoryDirector victory;
        public SurvivalScore Scores { get; private set; }
        public ObstacleSpawner Obstacles { get; private set; }

        void Awake()
        {
            victory = GetComponent<VictoryDirector>();
            Scores = gameObject.AddComponent<SurvivalScore>();
            Scores.Initialize(course,this);
            Obstacles=course.GetComponent<ObstacleSpawner>();
            if(Obstacles==null)Obstacles=course.gameObject.AddComponent<ObstacleSpawner>();
            Obstacles.Initialize(course);
            // The menu must win startup ordering over the round's automatic Start.
            course.round.playOnStart = false;
            masterVolume = PlayerPrefs.GetFloat("Rebuild.MasterVolume", 1);
            AudioListener.volume = masterVolume;
            allPlayers = course.players.ToArray(); allTraps = course.traps.ToArray();
            var group = transform.Find("Player lanes - gameplay anchors");
            lanePlatforms = Enumerable.Range(1, allPlayers.Length).Select(i => group.Find("Player lane " + i)).ToArray();
            foreach(var player in allPlayers)laneZ[player.transform]=player.transform.localPosition.z;
            foreach(var trap in allTraps)laneZ[trap.transform]=trap.transform.localPosition.z;
            foreach(Transform child in group)laneZ[child]=child.localPosition.z;
        }
        void Start() => Open();

        public void Open()
        {
            inMatchSettings=false;
            if(online!=null&&(online.Active||online.Busy)){online.Leave();return;}
            course.round.CancelStart(); IsOpen = true;
            Page = MenuPage.Main; view.MenuActive = true;
            view.RoundSummaryActive = true;
            SetPlayerCount(PlayerCount);
        }

        public void SetPlayerCount(int count)
        {
            if (!IsOpen) return;
            course.round.CancelStart();
            PlayerCount = Mathf.Clamp(count, 2, 4);
            for (int i = 0; i < allPlayers.Length; i++)
            {
                bool active = i < PlayerCount;
                allPlayers[i].gameObject.SetActive(active);
                allTraps[i].gameObject.SetActive(active);
                lanePlatforms[i].gameObject.SetActive(active);
                allPlayers[i].isLocallyControlled = i == 0;
                allPlayers[i].computerControlled = i > 0 && active;
                allPlayers[i].ComputerInput = 0;
            }
            var laneGroup = lanePlatforms[0].parent;
            foreach (Transform child in laneGroup)
            {
                if (child.name != "Walkway footing" && child.name != "Bank retaining board") continue;
                // The original builder places end supports beside, rather than under,
                // each walkway. Hide those too when their corresponding lane is unused.
                int lane = System.Array.FindIndex(lanePlatforms, platform => Mathf.Abs(platform.localPosition.z - child.localPosition.z) < .01f);
                if (lane >= 0) child.gameObject.SetActive(lane < PlayerCount);
            }
            course.SetRoster(allPlayers.Take(PlayerCount).ToArray(), allTraps.Take(PlayerCount).ToArray());
            view.RoundSummaryActive = true;
        }

        public void Play()
        {
            if (!IsOpen) return;
            // This entry point starts a local match. Follow-camera belongs exclusively
            // to a future online session, never to a user camera preference.
            view.SetMode(GameViewMode.Local);
            IsOpen = false; view.RoundSummaryActive = false;
            view.MenuActive = false;
            course.round.BeginRound();
        }

        public void OpenOnlineLobby()
        {
            course.round.CancelStart();IsOpen=true;Page=MenuPage.Online;
            view.MenuActive=true;view.RoundSummaryActive=true;
        }

        public void PrepareOnlineMatch(int count,int localSlot)
        {
            inMatchSettings=false;
            RestoreLaneOrder();IsOpen=true;SetPlayerCount(count);
            IsOpen=false;view.MenuActive=false;view.RoundSummaryActive=false;view.SetMode(GameViewMode.Online);
            for(int i=0;i<course.players.Length;i++)course.players[i].isLocallyControlled=i==localSlot;
        }

        public void RestoreLaneOrder()
        {
            foreach(var entry in laneZ)
            {if(entry.Key==null)continue;var position=entry.Key.localPosition;position.z=entry.Value;entry.Key.localPosition=position;}
        }

        public void ArrangeOnlineFront(int localSlot)
        {
            for(int i=0;i<PlayerCount;i++)
            {
                int display=i==localSlot?0:i<localSlot?i+1:i;
                float destination=laneZ[allPlayers[display].transform];float source=laneZ[allPlayers[i].transform];
                foreach(var target in new[]{allPlayers[i].transform,allTraps[i].transform,lanePlatforms[i]})
                {var position=target.localPosition;position.z=destination;target.localPosition=position;}
                foreach(Transform child in lanePlatforms[0].parent)
                {
                    if(child.name!="Walkway footing"&&child.name!="Bank retaining board")continue;
                    if(Mathf.Abs(laneZ[child]-source)>.01f)continue;
                    var position=child.localPosition;position.z=destination;child.localPosition=position;
                }
            }
        }

        void Update()
        {
            var keys = Keyboard.current;
            if (keys == null) return;
            if (keys.escapeKey.wasPressedThisFrame) { if (IsOpen) Back(); else Open(); }
            else if (IsOpen && keys.enterKey.wasPressedThisFrame)
            { if (Page == MenuPage.LocalGame) Play(); else if (Page == MenuPage.Main) ShowLocalGame(); }
        }

        public void ShowLocalGame() { if (IsOpen) Page = MenuPage.LocalGame; }
        public void ShowSettings() { if (IsOpen) Page = MenuPage.Settings; }
        public void OpenMatchSettings()
        {
            if(online!=null&&online.Active)
            {inMatchSettings=true;IsOpen=true;Page=MenuPage.Settings;view.MenuActive=true;}
            else {Open();ShowSettings();}
        }
        public void Back()
        {
            if(!IsOpen)return;
            if(inMatchSettings){inMatchSettings=false;IsOpen=false;view.MenuActive=false;return;}
            if(online!=null&&(online.Active||online.Busy))online.Leave();Page=MenuPage.Main;
        }
        public void SetMasterVolume(float value)
        {
            masterVolume = Mathf.Clamp01(value); AudioListener.volume = masterVolume;
            PlayerPrefs.SetFloat("Rebuild.MasterVolume", masterVolume);
        }

        void Styles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { font = menuFont, fontSize = 64, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(.96f,.91f,.73f) } };
            label = new GUIStyle(GUI.skin.label) { font = menuFont, fontSize = 16, normal = { textColor = new Color(.94f,.91f,.8f) } };
            small = new GUIStyle(label) { fontSize = 12, wordWrap = true, normal = { textColor = new Color(.71f,.75f,.64f) } };
            button = new GUIStyle(GUI.skin.button) { font = menuFont, fontSize = 16, border = new RectOffset(12,12,12,12),
                normal = { background = buttonTexture, textColor = new Color(.12f,.15f,.1f) },
                hover = { background = buttonTexture, textColor = Color.black },
                active = { background = pressedButtonTexture, textColor = Color.black } };
            centered = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
        }

        bool MenuButton(Rect rect, string text, bool primary = false)
        {
            GUI.backgroundColor = primary ? new Color(.79f,.82f,.47f) : new Color(.58f,.63f,.49f);
            bool clicked = GUI.Button(rect, text, button);
            GUI.backgroundColor = Color.white; return clicked;
        }

        static void Bar(Rect rect, Color color)
        {
            var before = GUI.color; GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = before;
        }

        static void Arrow(Vector2 center, bool right)
        {
            var color = new Color(.72f,.75f,.44f);
            Bar(new Rect(center.x-32,center.y-4,64,8),color);
            // Stair-step pixels instead of rotated rectangles: all logo edges stay hard
            // and the arrowheads remain attached at every screen scale.
            for(int step=0;step<4;step++)
            {
                float x = center.x + (right ? 24-step*8 : -32+step*8);
                Bar(new Rect(x,center.y-4-step*8,8,8),color);
                Bar(new Rect(x,center.y-4+step*8,8,8),color);
            }
        }

        void OnGUI()
        {
            if (!IsOpen)
            {
                if (victory != null && victory.ShowRestartPrompt) DrawRoundResult();
                else if(Scores.HasRound)DrawSurvivalTimer();
                return;
            }
            Styles();
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            var oldMatrix = GUI.matrix; var oldColor = GUI.color; var oldBackground = GUI.backgroundColor;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)*.5f,(Screen.height-720*scale)*.5f,0),Quaternion.identity,Vector3.one*scale);
            // A full-screen title treatment, with one centered menu hierarchy. It scales
            // uniformly and extends the background beyond the reference aspect ratio.
            Bar(new Rect(-2000,-2000,6000,6000),new Color(.035f,.055f,.04f,.95f));
            Bar(new Rect(-2000,0,6000,9),new Color(.5f,.54f,.32f));
            Bar(new Rect(-2000,711,6000,9),new Color(.5f,.54f,.32f));
            GUI.Label(new Rect(230,81,390,103),"LEFT",title);
            GUI.Label(new Rect(660,81,390,103),"RIGHT",title);
            Arrow(new Vector2(195,137),false);Arrow(new Vector2(1085,137),true);
            Bar(new Rect(638,105,4,68),new Color(.44f,.49f,.3f));
            GUI.Label(new Rect(190,198,900,42),"FOLLOW THE ORDERS. BE THE LAST ONE STANDING.",centered);
            Bar(new Rect(490,260,300,2),new Color(.32f,.38f,.25f));
            if (Page == MenuPage.Main)
            {
                if(MenuButton(new Rect(410,310,460,66),"START LOCAL GAME",true))ShowLocalGame();
                bool enabled=GUI.enabled;GUI.enabled=OnlineAvailable;
                if(MenuButton(new Rect(410,398,460,66),"ONLINE MATCH"))Page=MenuPage.Online;
                GUI.enabled=enabled;
                GUI.Label(new Rect(410,466,460,28),"HOST / JOIN",new GUIStyle(small){alignment=TextAnchor.MiddleCenter});
                if(MenuButton(new Rect(410,517,460,62),"SETTINGS"))ShowSettings();
            }
            else if(Page==MenuPage.LocalGame)
            {
                GUI.Label(new Rect(380,283,520,45),"LOCAL GAME",centered);
                GUI.Label(new Rect(380,333,520,32),"NUMBER OF RECRUITS",new GUIStyle(small){alignment=TextAnchor.MiddleCenter});
                for(int count=2;count<=4;count++)
                    if(MenuButton(new Rect(410+(count-2)*160,380,140,58),count.ToString(),count==PlayerCount))SetPlayerCount(count);
                GUI.Label(new Rect(260,451,760,40),"YOU + "+(PlayerCount-1)+(PlayerCount==2?" COMPUTER OPPONENT":" COMPUTER OPPONENTS"),centered);
                if(MenuButton(new Rect(410,514,460,64),"START GAME",true))Play();
                if(MenuButton(new Rect(510,597,260,48),"BACK"))Back();
            }
            else if(Page==MenuPage.Online)
            {
                GUI.Label(new Rect(250,280,780,40),"PLAY WITH A FRIEND",centered);
                if(!online.Active)
                {
                    bool enabled=GUI.enabled;GUI.enabled=!online.Busy;
                    GUI.Label(new Rect(280,330,720,28),"MATCH SIZE",new GUIStyle(small){alignment=TextAnchor.MiddleCenter});
                    for(int count=2;count<=4;count++)if(MenuButton(new Rect(410+(count-2)*160,365,140,48),count.ToString(),count==PlayerCount))SetPlayerCount(count);
                    if(MenuButton(new Rect(410,430,460,54),online.Busy?"CONNECTING...":"HOST ONLINE",true))online.HostInternetFromMenu(PlayerCount);
                    joinCode=GUI.TextField(new Rect(410,503,285,40),joinCode,12,new GUIStyle(GUI.skin.textField){font=menuFont,fontSize=18,alignment=TextAnchor.MiddleCenter}).ToUpperInvariant();
                    if(MenuButton(new Rect(710,498,160,48),"JOIN"))online.JoinInternetFromMenu(joinCode);
                    GUI.enabled=enabled;
                    GUI.Label(new Rect(240,548,800,24),online.Busy?"Please wait for online services...":"Enter the join code your friend shared.",new GUIStyle(small){alignment=TextAnchor.MiddleCenter});
                    GUI.Label(new Rect(100,571,1080,24),online.Status,new GUIStyle(small){alignment=TextAnchor.MiddleCenter,fontSize=12});
                }
                else
                {
                    GUI.Label(new Rect(140,325,1000,36),online.Status,new GUIStyle(small){alignment=TextAnchor.MiddleCenter});
                    if(online.IsHost&&online.UsesRelay)
                    {
                        GUI.Label(new Rect(390,363,340,38),online.JoinCode,new GUIStyle(centered){fontSize=28});
                        if(MenuButton(new Rect(750,360,130,40),"COPY"))GUIUtility.systemCopyBuffer=online.JoinCode;
                    }
                    GUI.Label(new Rect(260,397,760,40),online.HumanCount+" / "+online.Capacity+" PLAYERS",centered);
                    bool enabled=GUI.enabled;GUI.enabled=online.CanStart;
                    if(MenuButton(new Rect(410,470,460,64),online.IsHost?"START MATCH":"WAITING FOR HOST",true))online.RequestRound();
                    GUI.enabled=enabled;
                    GUI.Label(new Rect(220,550,840,28),"Empty slots are filled with computers.",new GUIStyle(small){alignment=TextAnchor.MiddleCenter});
                }
                if(MenuButton(new Rect(510,597,260,48),online.Active?"DISCONNECT":online.Busy?"CANCEL":"BACK"))Back();
            }
            else
            {
                GUI.Label(new Rect(380,283,520,45),"SETTINGS",centered);
                GUI.Label(new Rect(410,350,460,38),"VOLUME  "+Mathf.RoundToInt(masterVolume*100)+"%",centered);
                if(MenuButton(new Rect(350,395,62,46),"-"))SetMasterVolume(masterVolume-.1f);
                for(int step=0;step<10;step++)Bar(new Rect(438+step*38,403,28,28),step<Mathf.RoundToInt(masterVolume*10)?new Color(.72f,.75f,.44f):new Color(.18f,.24f,.17f));
                if(MenuButton(new Rect(860,395,62,46),"+"))SetMasterVolume(masterVolume+.1f);
                if(MenuButton(new Rect(510,597,260,48),"BACK"))Back();
            }
            GUI.Label(new Rect(240,663,800,32),"OUTPOST DRILL  /  PROTOTYPE",new GUIStyle(small){alignment=TextAnchor.MiddleCenter});
            GUI.color=oldColor;GUI.backgroundColor=oldBackground;GUI.matrix=oldMatrix;
        }

        void DrawRoundResult()
        {
            // Reuse the title menu's pixel font, nine-slice buttons and reference
            // resolution. Keep the actions below the winner instead of dimming the scene.
            Styles();
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            var oldMatrix = GUI.matrix; var oldColor = GUI.color; var oldBackground = GUI.backgroundColor;
            bool oldEnabled = GUI.enabled;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)*.5f,(Screen.height-720*scale)*.5f,0),Quaternion.identity,Vector3.one*scale);
            GUI.color = Color.white;
            Bar(new Rect(256,356,768,340),new Color(.035f,.055f,.04f,.96f));
            Bar(new Rect(256,356,768,4),new Color(.5f,.54f,.32f));
            Bar(new Rect(256,692,768,4),new Color(.5f,.54f,.32f));
            GUI.Label(new Rect(280,374,720,28),"SURVIVAL RESULTS",centered);
            GUI.Label(new Rect(280,409,720,24),(Scores.NewBest?"NEW PERSONAL BEST  ":"PERSONAL BEST  ")+SurvivalScore.Format(Scores.BestSeconds),new GUIStyle(small){alignment=TextAnchor.MiddleCenter});
            var ranking=Enumerable.Range(0,Scores.Times.Length)
                .OrderByDescending(i=>course.players[i]==course.Winner).ThenByDescending(i=>Scores.Times[i]).ToArray();
            string[] names={"GREEN","BLUE","YELLOW","RED"};
            for(int rank=0;rank<ranking.Length;rank++)
            {
                int i=ranking[rank];float y=449+rank*34;
                if(course.players[i]==course.Winner)Bar(new Rect(286,y-2,708,30),new Color(.25f,.3f,.17f));
                GUI.Label(new Rect(304,y,400,28),(rank+1)+". "+names[i]+(course.players[i]==course.Winner?"  WINNER":""),label);
                GUI.Label(new Rect(774,y,202,28),SurvivalScore.Format(Scores.Times[i]),new GUIStyle(label){alignment=TextAnchor.MiddleRight});
            }
            bool waiting = online != null && online.Active && !online.IsHost;
            GUI.enabled = oldEnabled && !waiting;
            if(MenuButton(new Rect(286,610,344,58),waiting?"WAITING FOR HOST":"PLAY AGAIN",true))victory.StartAgain();
            GUI.enabled = oldEnabled;
            if(MenuButton(new Rect(650,610,344,58),"MAIN MENU"))Open();
            GUI.matrix=oldMatrix;GUI.color=oldColor;GUI.backgroundColor=oldBackground;GUI.enabled=oldEnabled;
        }

        void DrawSurvivalTimer()
        {
            Styles();
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
            var oldMatrix=GUI.matrix;var oldColor=GUI.color;
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)*.5f,(Screen.height-720*scale)*.5f,0),Quaternion.identity,Vector3.one*scale);
            GUI.color=Color.white;
            Bar(new Rect(16,16,238,78),new Color(.035f,.055f,.04f,.9f));
            Bar(new Rect(16,16,4,78),new Color(.72f,.75f,.44f));
            GUI.Label(new Rect(32,25,208,22),"SURVIVAL",small);
            GUI.Label(new Rect(32,52,208,30),SurvivalScore.Format(Scores.PersonalSeconds),new GUIStyle(label){fontSize=22});
            GUI.matrix=oldMatrix;GUI.color=oldColor;
        }
    }
}
