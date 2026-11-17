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
        public enum MenuPage { Main, LocalGame, Settings }
        public MenuPage Page { get; private set; }
        public bool OnlineAvailable => false;
        float masterVolume;
        RecruitController[] allPlayers;
        LaneSpikeTrap[] allTraps;
        Transform[] lanePlatforms;
        GUIStyle title, label, small, button, centered;

        void Awake()
        {
            // The menu must win startup ordering over the round's automatic Start.
            course.round.playOnStart = false;
            masterVolume = PlayerPrefs.GetFloat("Rebuild.MasterVolume", 1);
            AudioListener.volume = masterVolume;
            allPlayers = course.players.ToArray(); allTraps = course.traps.ToArray();
            var group = transform.Find("Player lanes - gameplay anchors");
            lanePlatforms = Enumerable.Range(1, allPlayers.Length).Select(i => group.Find("Player lane " + i)).ToArray();
        }
        void Start() => Open();

        public void Open()
        {
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
        public void Back() { if (IsOpen) Page = MenuPage.Main; }
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
                if (course.IsGameOver && GUI.Button(new Rect(14, Screen.height - 52, 140, 38), "Main menu")) Open();
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
                bool enabled=GUI.enabled;GUI.enabled=false;
                GUI.color=new Color(.6f,.62f,.59f);
                MenuButton(new Rect(410,398,460,66),"ONLINE MATCH");GUI.enabled=enabled;GUI.color=Color.white;
                GUI.Label(new Rect(410,466,460,28),"COMING SOON",new GUIStyle(small){alignment=TextAnchor.MiddleCenter});
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
    }
}
