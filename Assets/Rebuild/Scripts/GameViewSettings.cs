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
        public float followSmoothSeconds = .85f;
        public float onlineDistance = 10f;
        public float onlineFieldOfView = 52f;
        public GameViewMode Mode { get; private set; }
        public bool CinematicActive { get; set; }
        public bool RoundSummaryActive { get; set; }
        public bool MenuActive { get; set; }
        public Vector3 CoursePosition => widePosition;
        public Quaternion CourseRotation => wideRotation;
        public float CourseFieldOfView => wideFieldOfView;
        Vector3 widePosition, velocity;
        Quaternion wideRotation;
        float wideFieldOfView, fieldVelocity;
        Texture2D gear;

        void Awake()
        {
            widePosition = gameCamera.transform.position;
            wideRotation = gameCamera.transform.rotation;
            wideFieldOfView = gameCamera.fieldOfView;
            Mode = GameViewMode.Local;
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
            if (close)
            {
                // Bias toward the local front recruit, retaining enough horizontal/vertical
                // field of view for the instructor at either end of the player's lane.
                Vector3 playerFeet = player.transform.position;
                Vector3 instructorFeet = course.round.officer.transform.position;
                Vector3 center = Vector3.Lerp(playerFeet, instructorFeet, .35f) + Vector3.up * 1.2f;
                float distance = onlineDistance;
                distance = FitPoint(playerFeet - Vector3.up * .1f, center, distance);
                distance = FitPoint(playerFeet + Vector3.up * 2.25f, center, distance);
                distance = FitPoint(instructorFeet - Vector3.up * .1f, center, distance);
                distance = FitPoint(instructorFeet + Vector3.up * 2.65f, center, distance);
                desired = center - (wideRotation * Vector3.forward) * distance;
            }
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

        float FitPoint(Vector3 point, Vector3 center, float distance)
        {
            Vector3 local = Quaternion.Inverse(wideRotation) * (point - center);
            float vertical = Mathf.Tan(onlineFieldOfView * Mathf.Deg2Rad * .5f) * .78f;
            float horizontal = vertical * Mathf.Max(.4f, gameCamera.aspect);
            return Mathf.Max(distance, Mathf.Abs(local.x) / horizontal - local.z,
                Mathf.Abs(local.y) / vertical - local.z);
        }

        void OnGUI()
        {
            if (MenuActive) return;
            const float margin = 14, button = 38;
            if (GUI.Button(new Rect(Screen.width - margin - button, margin, button, button), gear))
            {
                var menu = GetComponent<StartMenu>();
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
