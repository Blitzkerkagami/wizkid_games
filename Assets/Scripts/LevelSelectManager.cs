using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class LevelSelectManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI gameTitleText;
    public Button[] levelButtons;

    private string selectedGame;

    void Start()
    {
        selectedGame = PlayerPrefs.GetString("CurrentGame", PlayerPrefs.GetString("SelectedGame", "Shape Island"));

        if (gameTitleText != null)
        {
            string cleanCheck = selectedGame.Replace(" ", "").ToLower();

            if (cleanCheck == "shapeisland")
            {
                gameTitleText.text = "Shape Island";
                selectedGame = "ShapeIsland";
            }
            else if (cleanCheck == "colorforest")
            {
                gameTitleText.text = "Color Forest";
                selectedGame = "ColorForest";
            }
            else if (cleanCheck == "mathmountain")
            {
                gameTitleText.text = "Math Mountain";
                selectedGame = "MathMountain";
            }
            else if (cleanCheck == "countingkingdom")
            {
                gameTitleText.text = "Counting Kingdom";
                selectedGame = "CountingKingdom";
            }
            else
            {
                gameTitleText.text = selectedGame;
            }
        }

        SetupLevelButtons();
    }

    private void SetupLevelButtons()
    {
        int highestUnlockedLevel = SaveSystem.GetHighestUnlockedLevel(selectedGame);

        for (int i = 0; i < levelButtons.Length; i++)
        {
            if (levelButtons[i] == null)
                continue;

            int levelNumber = i + 1;
            levelButtons[i].onClick.RemoveAllListeners();
            levelButtons[i].onClick.AddListener(() => StartLevel(levelNumber));
            levelButtons[i].interactable = levelNumber <= highestUnlockedLevel;
        }
    }

    public void StartLevel(int levelNumber)
    {
        PlayerPrefs.SetString("CurrentGame", selectedGame.Replace(" ", ""));
        PlayerPrefs.SetInt("CurrentSubLevel", levelNumber);

        Debug.Log($"Starting {selectedGame} - Level {levelNumber}");
        SceneManager.LoadScene("Gameplay");
    }

    public void BackToAdventureMap()
    {
        SceneManager.LoadScene("AdventureMap");
    }
}
