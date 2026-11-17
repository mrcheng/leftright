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
        public Font menuFont;
        public int PlayerCount { get; private set; } = 4;
        public bool IsOpen { get; private set; }
        RecruitController[] allPlayers;
        LaneSpikeTrap[] allTraps;
        Transform[] lanePlatforms;
        GUIStyle title, label, small, button;

        void Awake()
        {
            // The menu must win startup ordering over the round's automatic Start.
            course.round.playOnStart = false;
            allPlayers = course.players.ToArray(); allTraps = course.traps.ToArray();
            var group = transform.Find("Player lanes - gameplay anchors");
            lanePlatforms = Enumerable.Range(1, allPlayers.Length).Select(i => group.Find("Player lane " + i)).ToArray();
        }
        void Start() => Open();

        public void Open()
        {
            course.round.CancelStart(); IsOpen = true;
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
            IsOpen = false; view.RoundSummaryActive = false;
            course.round.BeginRound();
        }

        void Update()
        {
            var keys = Keyboard.current;
            if (keys == null) return;
            if (keys.escapeKey.wasPressedThisFrame) { if (IsOpen) Play(); else Open(); }
            else if (IsOpen && keys.enterKey.wasPressedThisFrame) Play();
        }

        void Styles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { font = menuFont, fontSize = 38, fontStyle = FontStyle.Bold, normal = { textColor = new Color(.96f,.91f,.73f) } };
            label = new GUIStyle(GUI.skin.label) { font = menuFont, fontSize = 20, normal = { textColor = new Color(.94f,.91f,.8f) } };
            small = new GUIStyle(label) { fontSize = 14, wordWrap = true, normal = { textColor = new Color(.71f,.75f,.64f) } };
            button = new GUIStyle(GUI.skin.button) { font = menuFont, fontSize = 22, border = new RectOffset(10,10,10,12),
                normal = { background = buttonTexture, textColor = new Color(.12f,.15f,.1f) },
                hover = { background = buttonTexture, textColor = Color.black },
                active = { background = buttonTexture, textColor = Color.black } };
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
            var oldMatrix = GUI.matrix; var oldColor = GUI.color;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)*.5f,(Screen.height-720*scale)*.5f,0),Quaternion.identity,Vector3.one*scale);
            GUI.color = new Color(.025f,.04f,.025f,.58f); GUI.DrawTexture(new Rect(-1000,-1000,4000,3000),Texture2D.whiteTexture);
            GUI.color = new Color(.09f,.12f,.085f,.97f); GUI.DrawTexture(new Rect(80,55,520,610),Texture2D.whiteTexture);
            GUI.color = new Color(.63f,.66f,.4f); GUI.DrawTexture(new Rect(80,55,7,610),Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(118,89,450,32),"OUTPOST DRILL",small);
            GUI.Label(new Rect(113,122,460,88),"LEFT / RIGHT",title);
            GUI.Label(new Rect(118,218,425,55),"Follow the orders.\nBe the last recruit standing.",label);
            GUI.Label(new Rect(118,301,420,32),"RECRUITS",small);
            for(int count=2;count<=4;count++)
            {
                GUI.backgroundColor = count==PlayerCount ? new Color(.76f,.8f,.42f) : new Color(.44f,.5f,.37f);
                if(GUI.Button(new Rect(118+(count-2)*140,341,126,58),count.ToString(),button))SetPlayerCount(count);
            }
            GUI.backgroundColor = Color.white;
            GUI.Label(new Rect(118,418,430,32),"YOU + " + (PlayerCount-1) + (PlayerCount==2?" COMPUTER":" COMPUTERS"),label);
            GUI.Label(new Rect(118,455,420,48),"Your green recruit leads the line.\nA / D to march. Escape opens this menu.",small);
            GUI.backgroundColor = new Color(.79f,.82f,.47f);
            if(GUI.Button(new Rect(118,536,420,66),"START DRILL",button))Play();
            GUI.backgroundColor = Color.white;
            GUI.Label(new Rect(118,618,420,30),"2-4 recruits  /  solo vs computers",small);
            GUI.Label(new Rect(690,300,490,50),PlayerCount+" RECRUITS. ONE SURVIVOR.",label);
            GUI.Label(new Rect(690,350,440,92),"Watch the instructor. Match the beat.\nWrong steps bring the spikes closer.\nGrab a medic crate to win back space.",small);
            GUI.color=oldColor;GUI.matrix=oldMatrix;
        }
    }
}
