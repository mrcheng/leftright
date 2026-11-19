using UnityEngine;

namespace LeftRight.Rebuild
{
    public sealed class RecruitAudio : MonoBehaviour
    {
        public AudioSource output;
        public AudioClip scream;
        public AudioClip spikeImpact;
        public AudioClip squeeze;
        bool spiked, squeezed, falling;

        void Awake() => output.volume *= Mathf.Clamp01(PlayerPrefs.GetFloat("AudioLevel", 1f));

        public void PlaySpiked(bool playScream=true)
        {
            if (spiked) return;
            spiked = true;
            if(playScream)
            {
                output.clip = scream;
                output.Play();
            }
            output.PlayOneShot(spikeImpact, .75f);
        }

        public void PlaySqueezed()
        {
            if (squeezed) return;
            squeezed = true;
            output.PlayOneShot(squeeze, .85f);
        }

        public void PlayFalling()
        {
            if (falling) return;
            falling = true;
            // Reuse this recruit's voice without the spike penetration sound.
            output.clip = scream;
            output.Play();
        }

        public void ResetAudio()
        {
            output.Stop();
            spiked = squeezed = falling = false;
        }
    }
}
