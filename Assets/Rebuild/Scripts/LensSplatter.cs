using System;
using UnityEngine;
using UnityEngine.UI;

namespace LeftRight.Rebuild
{
    /// <summary>Client-local death presentation. No gameplay RNG or shared material state.</summary>
    public sealed class LensSplatter : MonoBehaviour
    {
        public SpikeCourse course;
        public Camera gameCamera;
        public Material splatterMaterial;
        public float lifetimeSeconds = 11;
        public float splatSize = .10f;
        public int ActiveSplatCount { get; private set; }
        public RawImage Overlay { get; private set; }
        public Material RuntimeMaterial { get; private set; }
        readonly Vector4[] splats = new Vector4[8];
        readonly Vector4[] seeds = new Vector4[8];
        readonly System.Random random = new System.Random();
        GameObject canvasRoot;
        RecruitController[] observedPlayers;

        void Awake()
        {
            // Overlay canvases are runtime root objects: they stay fixed to the screen,
            // independent of world framing and the environment prefab's transform.
            canvasRoot = new GameObject("Lens splatter - local camera", typeof(RectTransform), typeof(Canvas));
            var canvas=canvasRoot.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=50;
            var graphic=new GameObject("Blood on lens",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));
            graphic.transform.SetParent(canvasRoot.transform,false);
            var rect=graphic.GetComponent<RectTransform>(); rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one;
            rect.offsetMin=rect.offsetMax=Vector2.zero;
            Overlay=graphic.GetComponent<RawImage>(); Overlay.raycastTarget=false;
            RuntimeMaterial=new Material(splatterMaterial); Overlay.material=RuntimeMaterial;
            Clear();
        }

        void OnEnable()
        {
            course.round.Cancelled+=Clear;
            observedPlayers = course.GetComponentsInChildren<RecruitController>(true);
            foreach(var player in observedPlayers) { player.Eliminated+=OnDeath; player.Spiked+=OnSpikeHit; }
            if(canvasRoot!=null)canvasRoot.SetActive(true);
        }

        void OnDisable()
        {
            course.round.Cancelled-=Clear;
            foreach(var player in observedPlayers)if(player!=null) { player.Eliminated-=OnDeath; player.Spiked-=OnSpikeHit; }
            Clear(); if(canvasRoot!=null)canvasRoot.SetActive(false);
        }

        void OnDeath(RecruitController player) => AddSplatter(player.transform.position+Vector3.up);
        void OnSpikeHit(RecruitController player) => AddSplatter(player.transform.position+Vector3.up, .55f, .65f);

        public void AddSplatter(Vector3 worldPosition, float sizeMultiplier = 1, float opacityMultiplier = 1)
        {
            Compact();
            // Bounded buffer: a later hit replaces the oldest if all eight slots are occupied.
            if(ActiveSplatCount==splats.Length)
            {
                Array.Copy(splats,1,splats,0,splats.Length-1); Array.Copy(seeds,1,seeds,0,seeds.Length-1);
                ActiveSplatCount--;
            }
            Vector3 projected=gameCamera.WorldToViewportPoint(worldPosition);
            // Centered lanes should not pile every death on the same side of the lens.
            bool left=Mathf.Abs(projected.x-.5f)<.15f ? random.Next(2)==0 : projected.x<.5f;
            float side=left ? .25f : .75f;
            float x=Mathf.Clamp(side+((float)random.NextDouble()-.5f)*.22f,.08f,.92f);
            float y=Mathf.Clamp(projected.y+.12f+((float)random.NextDouble()-.5f)*.25f,.35f,.85f);
            int index=ActiveSplatCount++;
            splats[index]=new Vector4(x,y,Time.unscaledTime,splatSize*Mathf.Clamp(sizeMultiplier,.1f,1)*(.8f+(float)random.NextDouble()*.35f));
            seeds[index]=new Vector4((float)random.NextDouble()*100,lifetimeSeconds,.018f+(float)random.NextDouble()*.012f,Mathf.Clamp01(opacityMultiplier));
            Upload();
        }

        void Compact()
        {
            for(int i=ActiveSplatCount-1;i>=0;i--)
                if(Time.unscaledTime-splats[i].z>=seeds[i].y)
                {
                    Array.Copy(splats,i+1,splats,i,ActiveSplatCount-i-1);
                    Array.Copy(seeds,i+1,seeds,i,ActiveSplatCount-i-1); ActiveSplatCount--;
                }
        }

        void Update() { Compact(); Upload(); }

        void Upload()
        {
            if(RuntimeMaterial==null)return;
            Overlay.enabled=ActiveSplatCount>0;
            RuntimeMaterial.SetInt("_SplatCount",ActiveSplatCount);
            RuntimeMaterial.SetVectorArray("_Splats",splats);RuntimeMaterial.SetVectorArray("_Seeds",seeds);
            RuntimeMaterial.SetFloat("_Now",Time.unscaledTime);
            RuntimeMaterial.SetFloat("_Aspect",(float)Screen.width/Mathf.Max(1,Screen.height));
        }

        public void Clear() { ActiveSplatCount=0; Upload(); }

        void OnDestroy()
        {
            if(canvasRoot!=null)Destroy(canvasRoot);
            if(RuntimeMaterial!=null)Destroy(RuntimeMaterial);
        }
    }
}
