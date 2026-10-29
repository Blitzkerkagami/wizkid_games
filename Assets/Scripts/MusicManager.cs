using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public static MusicManager instance;

    public AudioSource audioSource;
    public AudioClip backgroundMusic;
    public AudioClip buttonClickSound;
    public AudioClip gameSelectSound;
    public AudioClip backButtonSound;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        if (audioSource != null && backgroundMusic != null)
        {
            audioSource.clip = backgroundMusic;
            audioSource.loop = true;
            audioSource.volume = 0.4f;
            audioSource.Play();
        }
    }
    public void PlayButtonClick()
    {
        if (audioSource != null && buttonClickSound != null)
            audioSource.PlayOneShot(buttonClickSound);
    }

    public void PlayGameSelectSound()
    {
        if (audioSource != null && gameSelectSound != null)
            audioSource.PlayOneShot(gameSelectSound);
    }

    public void PlayBackButtonSound()
    {
        if (audioSource != null && backButtonSound != null)
            audioSource.PlayOneShot(backButtonSound);
    }
}