using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleManager : MonoBehaviour
{
    public Button PlayButton;
    public Button ContinueButton;


    void Start()
    {

        if (ContinueButton != null)
            ContinueButton.gameObject.SetActive(SaveSystem.HasSavedProgress());

        if (PlayButton != null)
            PlayButton.onClick.AddListener(OnPlayClicked);

        if (ContinueButton != null)
            ContinueButton.onClick.AddListener(OnContinueClicked);
        
    }
    public void OnPlayClicked()
    {
        MusicManager.instance?.PlayBackButtonSound();
        SceneManager.LoadScene("CharacterSelect");
    }

    public void OnContinueClicked()
    {
        string lastGame = SaveSystem.GetLastGame();
        int lastLevel = SaveSystem.GetLastLevel();

        if (!string.IsNullOrEmpty(lastGame))
        {
            PlayerPrefs.SetString("CurrentGame", lastGame);
            PlayerPrefs.SetInt("CurrentSubLevel", lastLevel);
            SceneManager.LoadScene("Gameplay");
        }
        else
        {
            MusicManager.instance?.PlayBackButtonSound();
            SceneManager.LoadScene("CharacterSelect");
        }
    }
}
