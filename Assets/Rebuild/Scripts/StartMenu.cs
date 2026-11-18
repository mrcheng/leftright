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
        public bool IsSurvival { get; private set; }
        int localPlayerCount = 4;
        public bool IsOpen { get; private set; }
        public enum MenuPage { Main, LocalGame, Settings, Online }
        public MenuPage Page { get; private set; }
        public OnlineMatch online;
        public bool OnlineAvailable => online != null;
        public PublicDrillLobby PublicDrills { get; private set; }
        int drillPage;
        [System.Serializable] sealed class BrowserDrills
        {
            public bool isPublic, listed, refreshing, joining;
            public string status;
            public PublicDrillRoom[] rooms;
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        float nextWebStatus;
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void LeftRight_UpdateLobby(string code,string status,int humans,int host,int playing,float x,int jumping,
            int menu,int over,float beat,float travel,string receiver);
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void LeftRight_UpdateDrills(string json);
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void LeftRight_UpdateResults(string json);
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
            if(PublicDrills!=null)LeftRight_UpdateDrills(JsonUtility.ToJson(new BrowserDrills{
                isPublic=PublicDrills.IsPublic,listed=PublicDrills.IsListed,refreshing=PublicDrills.Refreshing,
                joining=PublicDrills.Joining,status=online.IsHost?PublicDrills.HostStatus:PublicDrills.BrowserStatus,rooms=PublicDrills.Rooms}));
            if(Results!=null)LeftRight_UpdateResults(JsonUtility.ToJson(Results.State()));
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
        public void SubmitSurvivalName(string name)=>Results.SubmitName(name);
        public void RetrySurvivalScores(string unused)=>Results.Refresh();
        public void ReplayFromResults(string unused)=>Results.PlayAgain();
        public void MenuFromResults(string unused)=>Results.MainMenu();
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
        public SurvivalResults Results { get; private set; }
        public ObstacleSpawner Obstacles { get; private set; }

        void Awake()
        {
            victory = GetComponent<VictoryDirector>();
            Scores = gameObject.AddComponent<SurvivalScore>();
            Scores.Initialize(course,this);
            Results=gameObject.AddComponent<SurvivalResults>();
            Results.Initialize(this,victory,new UnitySurvivalLeaderboard());
            Obstacles=course.GetComponent<ObstacleSpawner>();
            if(Obstacles==null)Obstacles=course.gameObject.AddComponent<ObstacleSpawner>();
            Obstacles.Initialize(course);
            PublicDrills=GetComponent<PublicDrillLobby>();
            if(PublicDrills==null)PublicDrills=gameObject.AddComponent<PublicDrillLobby>();
            PublicDrills.Initialize(this,online);
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
            course.round.Preparing+=ArrangeSurvivalLane;
        }
        void Start() => Open();

        public void Open()
        {
            inMatchSettings=false;
            if(online!=null&&(online.Active||online.Busy)){online.Leave();return;}
            course.round.CancelStart(); IsOpen = true;
            Page = MenuPage.Main; view.MenuActive = true;
            view.RoundSummaryActive = true;
            if(IsSurvival)RestoreLaneOrder();
            IsSurvival=false;SetPlayerCount(localPlayerCount);
        }

        public void SetPlayerCount(int count)
        {
            if (!IsOpen) return;
            IsSurvival=false;localPlayerCount=Mathf.Clamp(count,2,4);
            ApplyRoster(localPlayerCount);
        }
        void ApplyRoster(int count)
        {
            course.round.CancelStart();
            PlayerCount = count;
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
            IsSurvival=false;
            // Session mode chooses ownership/network behavior; both modes follow
            // the locally controlled recruit once the match begins.
            view.SetMode(GameViewMode.Local);
            IsOpen = false; view.RoundSummaryActive = false;
            view.MenuActive = false;
            course.round.BeginRound();
        }

        public void PlaySurvival()
        {
            if(!IsOpen||(online!=null&&(online.Active||online.Busy)))return;
            RestoreLaneOrder();IsSurvival=true;ApplyRoster(1);
            view.SetMode(GameViewMode.Local);IsOpen=false;view.MenuActive=false;view.RoundSummaryActive=false;
            course.round.BeginRound();
        }
        void ArrangeSurvivalLane()
        {
            if(!IsSurvival)return;
            // Keep the lone walkway beside the instructor, without the empty gap
            // left by hidden multiplayer lanes. Preparing runs AFTER player reset,
            // so replay cannot put the recruit back on its old, now-hidden lane.
            float destination=laneZ[lanePlatforms[allPlayers.Length-1]],source=laneZ[allPlayers[0].transform];
            foreach(var target in new[]{allPlayers[0].transform,allTraps[0].transform,lanePlatforms[0]})
            {var p=target.localPosition;p.z=destination;target.localPosition=p;}
            foreach(Transform child in lanePlatforms[0].parent)
                if((child.name=="Walkway footing"||child.name=="Bank retaining board")&&Mathf.Abs(laneZ[child]-source)<.01f)
                {var p=child.localPosition;p.z=destination;child.localPosition=p;}
        }

        public void OpenOnlineLobby()
        {
            course.round.CancelStart();IsOpen=true;Page=MenuPage.Online;
            if(IsSurvival){RestoreLaneOrder();IsSurvival=false;SetPlayerCount(localPlayerCount);}
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
            if (Screen.height > Screen.width) { DrawPortraitMenu(); return; }
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
                if(MenuButton(new Rect(410,512,460,56),"SURVIVAL",true))PlaySurvival();
                if(MenuButton(new Rect(410,584,460,52),"SETTINGS"))ShowSettings();
                DrawPublicDrills(new Rect(914,304,342,320),4);
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
                if(!online.Active||online.IsHost)
                {
                    bool old=GUI.enabled;GUI.enabled=old&&!online.Busy;
                    DrawPublicOption(new Rect(914,430,342,44));GUI.enabled=old;
                    GUI.Label(new Rect(914,488,330,108),online.Active?PublicDrills.HostStatus:"Public rooms appear on the start screen. Private rooms use a shared code.",new GUIStyle(small){alignment=TextAnchor.UpperLeft});
                }
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

        void DrawPublicOption(Rect rect)
        {
            var style=new GUIStyle(GUI.skin.toggle){font=menuFont,fontSize=14,padding=new RectOffset(26,0,0,0)};
            style.normal.textColor=style.onNormal.textColor=new Color(.94f,.91f,.8f);
            bool value=GUI.Toggle(rect,PublicDrills.IsPublic,"PUBLIC ROOM",style);
            if(value!=PublicDrills.IsPublic)PublicDrills.SetPublic(value);
        }

        void DrawPublicDrills(Rect rect,int perPage)
        {
            if(PublicDrills==null)return;
            GUI.Label(new Rect(rect.x,rect.y,rect.width-96,44),"DRILLS SOON ABOUT\nTO TAKE PLACE",new GUIStyle(small){fontSize=11});
            bool enabled=GUI.enabled;GUI.enabled=enabled&&PublicDrills.CanRefresh;
            if(GUI.Button(new Rect(rect.xMax-88,rect.y,88,44),PublicDrills.Refreshing?"...":"REFRESH",new GUIStyle(button){fontSize=10}))_=PublicDrills.RefreshAsync();
            GUI.enabled=enabled;
            var rooms=PublicDrills.Rooms;
            if(rooms.Length==0)
            {
                GUI.Label(new Rect(rect.x,rect.y+66,rect.width,100),PublicDrills.BrowserStatus,new GUIStyle(small){fontSize=12});return;
            }
            int pages=(rooms.Length+perPage-1)/perPage;drillPage=Mathf.Clamp(drillPage,0,pages-1);
            float height=(rect.height-92)/perPage;
            for(int row=0;row<perPage;row++)
            {
                int index=drillPage*perPage+row;if(index>=rooms.Length)break;
                var room=rooms[index];float y=rect.y+48+row*height;
                Bar(new Rect(rect.x,y,rect.width,height-4),new Color(.12f,.16f,.1f,.85f));
                string name=room.Name??"Drill";if(name.Length>18)name=name.Substring(0,15)+"...";
                GUI.Label(new Rect(rect.x+8,y+4,rect.width-104,24),name.ToUpperInvariant(),new GUIStyle(small){fontSize=12});
                GUI.Label(new Rect(rect.x+8,y+29,rect.width-104,24),room.Players+" / "+room.Capacity+" PLAYERS",new GUIStyle(small){fontSize=11});
                GUI.enabled=enabled&&OnlineAvailable&&!online.Active&&!online.Busy&&!PublicDrills.Joining;
                if(GUI.Button(new Rect(rect.xMax-88,y+2,88,perPage==2?56:44),PublicDrills.Joining?"...":"JOIN",new GUIStyle(button){fontSize=12}))_=PublicDrills.JoinAsync(room);
            }
            GUI.enabled=enabled;
            if(pages>1)
            {
                float y=rect.yMax-44;
                if(MenuButton(new Rect(rect.x,y,56,44),"<"))drillPage=(drillPage+pages-1)%pages;
                string status=string.IsNullOrEmpty(PublicDrills.BrowserStatus)?(drillPage+1)+" / "+pages:PublicDrills.BrowserStatus;
                GUI.Label(new Rect(rect.x+64,y,rect.width-128,44),status,new GUIStyle(small){alignment=TextAnchor.MiddleCenter,fontSize=11});
                if(MenuButton(new Rect(rect.xMax-56,y,56,44),">"))drillPage=(drillPage+1)%pages;
            }
            else if(!string.IsNullOrEmpty(PublicDrills.BrowserStatus))
                GUI.Label(new Rect(rect.x,rect.yMax-44,rect.width,44),PublicDrills.BrowserStatus,new GUIStyle(small){alignment=TextAnchor.MiddleCenter,fontSize=11});
        }

        void DrawPortraitMenu()
        {
            // A separate 480x860 layout keeps phone targets at thumb size instead
            // of shrinking the entire 1280-wide desktop menu. Browser room controls
            // use this same centered reference rectangle in index.template.html.
            float scale=Mathf.Min(Screen.width/480f,Screen.height/860f);
            var matrix=GUI.matrix;var color=GUI.color;var background=GUI.backgroundColor;
            bool enabled=GUI.enabled;
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-480*scale)*.5f,(Screen.height-860*scale)*.5f,0),Quaternion.identity,Vector3.one*scale);
            Bar(new Rect(-2000,-2000,6000,6000),new Color(.035f,.055f,.04f,.95f));
            Bar(new Rect(-2000,0,6000,6),new Color(.5f,.54f,.32f));
            Bar(new Rect(-2000,854,6000,6),new Color(.5f,.54f,.32f));
            GUI.Label(new Rect(24,80,432,64),"LEFT | RIGHT",new GUIStyle(title){fontSize=32});
            GUI.Label(new Rect(36,156,408,54),"FOLLOW THE ORDERS.\nBE THE LAST ONE STANDING.",new GUIStyle(small){alignment=TextAnchor.MiddleCenter,fontSize=13});
            if(Page==MenuPage.Main)
            {
                if(MenuButton(new Rect(24,230,432,60),"START LOCAL GAME",true))ShowLocalGame();
                GUI.enabled=enabled&&OnlineAvailable;
                if(MenuButton(new Rect(24,306,432,60),"ONLINE MATCH"))Page=MenuPage.Online;
                GUI.enabled=enabled;
                GUI.Label(new Rect(24,370,432,24),"HOST / JOIN",new GUIStyle(small){alignment=TextAnchor.MiddleCenter});
                if(MenuButton(new Rect(24,404,432,60),"SURVIVAL",true))PlaySurvival();
                if(MenuButton(new Rect(24,480,432,52),"SETTINGS"))ShowSettings();
                DrawPublicDrills(new Rect(24,550,432,244),2);
            }
            else if(Page==MenuPage.LocalGame)
            {
                GUI.Label(new Rect(24,238,432,40),"LOCAL GAME",centered);
                GUI.Label(new Rect(24,294,432,32),"NUMBER OF RECRUITS",new GUIStyle(small){alignment=TextAnchor.MiddleCenter});
                for(int count=2;count<=4;count++)
                    if(MenuButton(new Rect(24+(count-2)*150,342,132,64),count.ToString(),count==PlayerCount))SetPlayerCount(count);
                GUI.Label(new Rect(24,434,432,64),"YOU + "+(PlayerCount-1)+" COMPUTER"+(PlayerCount==2?"":"S"),centered);
                if(MenuButton(new Rect(24,530,432,64),"START GAME",true))Play();
                if(MenuButton(new Rect(24,690,432,56),"BACK"))Back();
            }
            else if(Page==MenuPage.Online)
            {
                GUI.Label(new Rect(24,224,432,40),"PLAY WITH A FRIEND",centered);
                if(!online.Active)
                {
                    GUI.enabled=enabled&&!online.Busy;
                    for(int count=2;count<=4;count++)
                        if(MenuButton(new Rect(24+(count-2)*150,270,132,56),count.ToString(),count==PlayerCount))SetPlayerCount(count);
                    DrawPublicOption(new Rect(24,328,432,40));
                    if(MenuButton(new Rect(24,370,432,54),online.Busy?"CONNECTING...":"HOST ONLINE",true))online.HostInternetFromMenu(PlayerCount);
                    joinCode=GUI.TextField(new Rect(24,436,270,56),joinCode,12,new GUIStyle(GUI.skin.textField){font=menuFont,fontSize=20,alignment=TextAnchor.MiddleCenter}).ToUpperInvariant();
                    if(MenuButton(new Rect(310,436,146,56),"JOIN"))online.JoinInternetFromMenu(joinCode);
                    GUI.enabled=enabled;
                    GUI.Label(new Rect(24,494,432,60),online.Busy?"Please wait for online services...":"Enter your friend's room code.",new GUIStyle(small){alignment=TextAnchor.MiddleCenter});
                    GUI.Label(new Rect(24,565,432,96),online.Status,new GUIStyle(small){alignment=TextAnchor.MiddleCenter});
                }
                else
                {
                    GUI.Label(new Rect(24,270,432,56),online.Status,new GUIStyle(small){alignment=TextAnchor.MiddleCenter});
                    if(online.IsHost&&online.UsesRelay)
                    {
                        GUI.Label(new Rect(24,330,270,48),online.JoinCode,new GUIStyle(centered){fontSize=28});
                        if(MenuButton(new Rect(316,330,140,48),"COPY"))GUIUtility.systemCopyBuffer=online.JoinCode;
                    }
                    GUI.Label(new Rect(24,410,432,40),online.HumanCount+" / "+online.Capacity+" PLAYERS",centered);
                    GUI.enabled=enabled&&online.CanStart;
                    if(MenuButton(new Rect(24,490,432,64),online.IsHost?"START MATCH":"WAITING FOR HOST",true))online.RequestRound();
                    GUI.enabled=enabled;
                    GUI.Label(new Rect(24,574,432,32),"Empty slots use computers.",new GUIStyle(small){alignment=TextAnchor.MiddleCenter,fontSize=11});
                    if(online.IsHost)
                    {
                        DrawPublicOption(new Rect(24,610,432,44));
                        GUI.Label(new Rect(24,654,432,30),PublicDrills.HostStatus,new GUIStyle(small){alignment=TextAnchor.MiddleCenter,fontSize=10});
                    }
                }
                if(MenuButton(new Rect(24,690,432,56),online.Active?"DISCONNECT":online.Busy?"CANCEL":"BACK"))Back();
            }
            else
            {
                GUI.Label(new Rect(24,250,432,40),"SETTINGS",centered);
                GUI.Label(new Rect(24,330,432,40),"VOLUME  "+Mathf.RoundToInt(masterVolume*100)+"%",centered);
                if(MenuButton(new Rect(24,395,64,64),"-"))SetMasterVolume(masterVolume-.1f);
                for(int step=0;step<10;step++)Bar(new Rect(104+step*27,415,20,24),step<Mathf.RoundToInt(masterVolume*10)?new Color(.72f,.75f,.44f):new Color(.18f,.24f,.17f));
                if(MenuButton(new Rect(392,395,64,64),"+"))SetMasterVolume(masterVolume+.1f);
                if(MenuButton(new Rect(24,690,432,56),"BACK"))Back();
            }
            GUI.Label(new Rect(24,798,432,32),"OUTPOST DRILL / PROTOTYPE",new GUIStyle(small){alignment=TextAnchor.MiddleCenter,fontSize=11});
            GUI.matrix=matrix;GUI.color=color;GUI.backgroundColor=background;GUI.enabled=enabled;
        }

        void DrawRoundResult()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            SurvivalResultsPanel.Draw(Results);
#endif
        }

        void OnDestroy()
        {
            if(course!=null&&course.round!=null)course.round.Preparing-=ArrangeSurvivalLane;
        }

        void DrawSurvivalTimer()
        {
            Styles();
            bool portraitSolo=IsSurvival&&Screen.height>Screen.width;
            float scale=portraitSolo?Screen.width/480f:Mathf.Min(Screen.width/1280f,Screen.height/720f);
            var oldMatrix=GUI.matrix;var oldColor=GUI.color;
            GUI.matrix=Matrix4x4.TRS(portraitSolo?Vector3.zero:new Vector3((Screen.width-1280*scale)*.5f,(Screen.height-720*scale)*.5f,0),Quaternion.identity,Vector3.one*scale);
            GUI.color=Color.white;
            Bar(new Rect(16,16,238,78),new Color(.035f,.055f,.04f,.9f));
            Bar(new Rect(16,16,4,78),new Color(.72f,.75f,.44f));
            GUI.Label(new Rect(32,25,208,22),"SURVIVAL",small);
            GUI.Label(new Rect(32,52,208,30),SurvivalScore.Format(Scores.PersonalSeconds),new GUIStyle(label){fontSize=22});
            GUI.matrix=oldMatrix;GUI.color=oldColor;
        }
    }
}
