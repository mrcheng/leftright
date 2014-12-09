using UnityEngine;
using System.Collections;

public class AudioFootsteps : MonoBehaviour {

    // MAKE SURE THIS SCRIPT IS ATTATCHED TO THE GAME OBJECT AS FIRST IN HEARCHY ORTHERWIZE IT WILL NOT FUNCTION PROPERLY!

    public AudioClip[] footsteps_walk;
    public AudioClip footstep_land;
    public AudioClip[] footsteps_run;

    void PlayWalkFootsteps()
    {
        audio.clip = footsteps_walk[Random.Range(0, footsteps_walk.Length)];
        audio.Play();
    }

    void PlayLandingFootstep()
    {
        audio.clip = footstep_land;
        audio.Play();
    }

    void PlayRunFootsteps()
    {
        audio.clip = footsteps_run[Random.Range(0, footsteps_run.Length)];
        audio.volume = 1.0f;
        audio.Play();
    }

}
