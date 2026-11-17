using UnityEngine;

namespace LeftRight.Rebuild
{
    public sealed class RecruitAudio : MonoBehaviour
    {
        public AudioSource output;
        public AudioClip scream;
        public AudioClip spikeImpact;
        public AudioClip squeeze;
        bool spiked, squeezed;

        void Awake() => output.volume *= Mathf.Clamp01(PlayerPrefs.GetFloat("AudioLevel", 1f));

        public void PlaySpiked()
        {
            if (spiked) return;
            spiked = true;
            output.clip = scream;
            output.Play();
            output.PlayOneShot(spikeImpact, .75f);
        }

        public void PlaySqueezed()
        {
            if (squeezed) return;
            squeezed = true;
            output.PlayOneShot(squeeze, .85f);
        }

        public void ResetAudio()
        {
            output.Stop();
            spiked = squeezed = false;
        }
    }
}
