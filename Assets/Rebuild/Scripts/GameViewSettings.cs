using UnityEngine;

namespace LeftRight.Rebuild
{
    public enum GameViewMode { Local, Online }

    /// <summary>Follow the locally controlled recruit in every match. Menus,
    /// results and victory cinematics retain their own camera framing.</summary>
    public sealed class GameViewSettings : MonoBehaviour
    {
        public SpikeCourse course;
        public Camera gameCamera;
        public float followSmoothSeconds = 1.5f;
        public float startPullback = 3f;
        public float startCampWidth = 26f;
        public float onlineDistance = 9f;
        [Min(1), Tooltip("Horizontal world width at the recruit: the 20-unit lane almost fills the screen.")]
        public float followLaneWidth = 22f;
        public float onlineFieldOfView = 60f;
        public GameViewMode Mode { get; private set; }
        public bool CinematicActive { get; set; }
        public bool RoundSummaryActive { get; set; }
        public bool MenuActive { get; set; }
        public Vector3 CoursePosition => widePosition;
        public Quaternion CourseRotation => wideRotation;
        public float CourseFieldOfView => wideFieldOfView;
        public Vector3 GameplayFocus=>menu!=null&&menu.IsCouch?CouchCenter:LocalPlayer!=null?Vector3.Lerp(LocalPlayer.transform.position,course.round.officer.transform.position,.35f)+Vector3.up*1.2f:widePosition+wideRotation*Vector3.forward*20;
        Vector3 CouchCenter
        {
            get
            {
                var bounds=new Bounds(course.round.officer.transform.position+Vector3.up*1.3f,Vector3.zero);
                foreach(var p in course.players)bounds.Encapsulate(p.transform.position+Vector3.up*1.1f);
                return bounds.center;
            }
        }
        Vector3 widePosition, savedWidePosition, velocity;
        Quaternion wideRotation;
        float wideFieldOfView, fieldVelocity;
        Texture2D gear;
        StartMenu menu;

        void Awake()
        {
            savedWidePosition = gameCamera.transform.position;
            wideRotation = gameCamera.transform.rotation;
            wideFieldOfView = gameCamera.fieldOfView;
            UpdateWideFraming();
            Mode = GameViewMode.Local;
            menu=GetComponent<StartMenu>();
            gear = MakeGear();
        }

        public void SetMode(GameViewMode mode)
        {
            Mode = mode;
            if(mode==GameViewMode.Local)
            {
                // A new local session begins its follow transition from the menu pose,
                // without carrying momentum from the previous online recruit.
                velocity=Vector3.zero;fieldVelocity=0;
                gameCamera.transform.SetPositionAndRotation(widePosition,wideRotation);
                gameCamera.fieldOfView=wideFieldOfView;
            }
        }

        public RecruitController LocalPlayer
        {
            get
            {
                // Ownership chooses the tracked recruit, not character/color or array index.
                foreach (var player in course.players)
                    if (player != null && player.isLocallyControlled) return player;
                return null;
            }
        }

        void LateUpdate()
        {
            UpdateWideFraming();
            if (CinematicActive) return;
            if (RoundSummaryActive)
            {
                // Pullback already reached this pose. Discard old follow momentum so
                // it cannot nudge the wide camera after the replay prompt appears.
                velocity = Vector3.zero; fieldVelocity = 0;
                gameCamera.transform.SetPositionAndRotation(widePosition, wideRotation);
                gameCamera.fieldOfView = wideFieldOfView;
                return;
            }
            var player = LocalPlayer;
            bool close = !RoundSummaryActive && player != null &&
                (course.round.IsCountingDown || course.round.HasStarted);
            Vector3 desired = widePosition;
            float fov = close ? onlineFieldOfView : wideFieldOfView;
            if (close) GameplayPose(out desired,out _,out _);
            float delta = Time.unscaledDeltaTime;
            gameCamera.transform.position = Vector3.SmoothDamp(gameCamera.transform.position, desired,
                ref velocity, Mathf.Max(.05f, followSmoothSeconds), Mathf.Infinity, delta);
            // No roll or horizontal orbit: keep the left/right course visually level.
            gameCamera.transform.rotation = wideRotation;
            gameCamera.fieldOfView = Mathf.SmoothDamp(gameCamera.fieldOfView, fov, ref fieldVelocity,
                Mathf.Max(.05f, followSmoothSeconds), Mathf.Infinity, delta);
            if (!close && Vector3.Distance(gameCamera.transform.position, widePosition) < .001f &&
                Mathf.Abs(gameCamera.fieldOfView - wideFieldOfView) < .001f)
            {
                gameCamera.transform.position = widePosition; gameCamera.fieldOfView = wideFieldOfView;
            }
        }

        public void GameplayPose(out Vector3 position,out Quaternion rotation,out float fov)
        {
            UpdateWideFraming();rotation=wideRotation;fov=onlineFieldOfView;
            if(LocalPlayer==null){position=widePosition;fov=wideFieldOfView;return;}
            if(menu!=null&&menu.IsCouch)
            {
                // Shared-screen humans must all remain visible, including after P1
                // dies. Keep the saved level angle and fit every recruit's full height.
                Vector3 focus=CouchCenter;float couchDistance=onlineDistance;
                foreach(var p in course.players){couchDistance=FitPoint(p.transform.position-Vector3.up*.1f,focus,couchDistance);couchDistance=FitPoint(p.transform.position+Vector3.up*2.25f,focus,couchDistance);}
                var instructor=course.round.officer.transform.position;
                couchDistance=FitPoint(instructor,focus,couchDistance);couchDistance=FitPoint(instructor+Vector3.up*2.65f,focus,couchDistance);
                position=focus-wideRotation*Vector3.forward*couchDistance;return;
            }
            // Bias toward the local front recruit, retaining enough horizontal/vertical
            // field of view for the instructor at either end of the player's lane.
            Vector3 playerFeet = LocalPlayer.transform.position;
            Vector3 instructorFeet = course.round.officer.transform.position;
            Vector3 center = GameplayFocus;
            // Frame the front lane consistently across phone/desktop aspect ratios.
            // Measure width at the recruit's depth, rather than at the aim point
            // behind them. The instructor visibility checks can still pull back
            // when the player walks toward either end of the lane.
            float horizontal = Mathf.Tan(onlineFieldOfView*Mathf.Deg2Rad*.5f)*Mathf.Max(.4f,gameCamera.aspect);
            float playerDepth=(Quaternion.Inverse(wideRotation)*(playerFeet-center)).z;
            float distance = Mathf.Max(onlineDistance,followLaneWidth*.5f/horizontal-playerDepth);
            distance = FitPoint(playerFeet - Vector3.up * .1f, center, distance);
            distance = FitPoint(playerFeet + Vector3.up * 2.25f, center, distance);
            distance = FitPoint(instructorFeet - Vector3.up * .1f, center, distance);
            distance = FitPoint(instructorFeet + Vector3.up * 2.65f, center, distance);
            position = center - (wideRotation * Vector3.forward) * distance;
        }

        public void SnapToGameplay()
        {GameplayPose(out var position,out var rotation,out float fov);velocity=Vector3.zero;fieldVelocity=0;gameCamera.transform.SetPositionAndRotation(position,rotation);gameCamera.fieldOfView=fov;}
        public void SnapToCourse()
        {UpdateWideFraming();velocity=Vector3.zero;fieldVelocity=0;gameCamera.transform.SetPositionAndRotation(widePosition,wideRotation);gameCamera.fieldOfView=wideFieldOfView;}

        float FitPoint(Vector3 point, Vector3 center, float distance)
        {
            Vector3 local = Quaternion.Inverse(wideRotation) * (point - center);
            float vertical = Mathf.Tan(onlineFieldOfView * Mathf.Deg2Rad * .5f) * .78f;
            float horizontal = vertical * Mathf.Max(.4f, gameCamera.aspect);
            return Mathf.Max(distance, Mathf.Abs(local.x) / horizontal - local.z,
                Mathf.Abs(local.y) / vertical - local.z);
        }

        void UpdateWideFraming()
        {
            // Fit the camp's width rather than cropping a landscape introduction
            // into a narrow phone screen. Keep the saved aim point and level angle;
            // recompute from the saved pose on rotation, never accumulate offsets.
            const float savedAimDistance = 20f;
            float horizontal = Mathf.Tan(wideFieldOfView * Mathf.Deg2Rad * .5f) * Mathf.Max(.2f, gameCamera.aspect);
            float pullback = Mathf.Max(startPullback, startCampWidth * .5f / horizontal - savedAimDistance);
            widePosition = savedWidePosition - (wideRotation * Vector3.forward) * Mathf.Max(0, pullback);
        }

        void OnGUI()
        {
            if (MenuActive||menu?.Results?.Visible==true) return;
            const float margin = 14, button = 38;
            if (GUI.Button(new Rect(Screen.width - margin - button, margin, button, button), gear))
            {
                if (menu != null) menu.OpenMatchSettings();
            }
        }

        static Texture2D MakeGear()
        {
            const int size = 48;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - 23.5f, dy = y - 23.5f;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx);
                    float outer = Mathf.Cos(angle * 8) > .2f ? 19 : 15;
                    pixels[y * size + x] = radius > 6 && radius < outer ? new Color(.9f, .9f, .85f) : Color.clear;
                }
            texture.SetPixels(pixels); texture.Apply(); return texture;
        }

        void OnDisable()
        {
            if (gameCamera == null) return;
            gameCamera.transform.SetPositionAndRotation(widePosition, wideRotation);
            gameCamera.fieldOfView = wideFieldOfView;
            velocity = Vector3.zero; fieldVelocity = 0;
        }

        void OnDestroy() { if (gear != null) Destroy(gear); }
    }
}
