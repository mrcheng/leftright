using UnityEngine;
using System.Collections;

public class AudioFootsteps : MonoBehaviour {

    // MAKE SURE THIS SCRIPT IS ATTATCHED TO THE GAME OBJECT AS FIRST IN HEARCHY ORTHERWIZE IT WILL NOT FUNCTION PROPERLY!

    public AudioClip[] footsteps_walk;
    public AudioClip footstep_land;
    public AudioClip[] footsteps_run;

    void PlayWalkFootsteps()
    {
        GetComponent<AudioSource>().clip = footsteps_walk[Random.Range(0, footsteps_walk.Length)];
        GetComponent<AudioSource>().Play();
    }

    void PlayLandingFootstep()
    {
        GetComponent<AudioSource>().clip = footstep_land;
        GetComponent<AudioSource>().Play();
    }

    void PlayRunFootsteps()
    {
        GetComponent<AudioSource>().clip = footsteps_run[Random.Range(0, footsteps_run.Length)];
        GetComponent<AudioSource>().volume = 1.0f;
        GetComponent<AudioSource>().Play();
    }

}
