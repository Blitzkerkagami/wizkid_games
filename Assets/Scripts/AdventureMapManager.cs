using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class AdventureMapManager : MonoBehaviour
{
    [Header("Player Info Display")]
    public Image PlayerIcon;
    public TextMeshProUGUI PlayerNameText;
    public TextMeshProUGUI PlayerAgeText;
    public TextMeshProUGUI PlayerBirthdayText;

    [Header("All Character Sprites (Same order as Character Select)")]
    public Sprite[] wizardSprites;

    [Header("Game Buttons")]
    public Button countingKingdomButton;
    public Button shapeIslandButton;
    public Button colorForestButton;
    public Button mathMountainButton;
    public Button alphabetOrderButton;
    public Button patternPlainsButton;
    public Button objectMatchingButton;

    [Header("Other Buttons")]
    public Button backButton;

    [Header("UI Score Displays")]
    public TextMeshProUGUI countingKingdomScoreText;
    public TextMeshProUGUI shapeIslandScoreText;
    public TextMeshProUGUI colorForestScoreText;
    public TextMeshProUGUI mathMountainScoreText;
    public TextMeshProUGUI alphabetOrderScoreText;
    public TextMeshProUGUI patternPlainsScoreText;
    public TextMeshProUGUI objectMatchingScoreText;

    void Start()
    {
        LoadPlayerInfo();
        SetupButtons();       // Fixed: Your original code had this method but forgot to call it here!
        UpdateScoresDisplay(); // Added: Updates all game scores when the map loads
    }

    private void LoadPlayerInfo()
    {
        string playerName = PlayerPrefs.GetString("PlayerName", "Young Wizard");
        int playerAge = PlayerPrefs.GetInt("PlayerAge", 5);
        string playerBirthday = PlayerPrefs.GetString("PlayerBirthday", "01/01/2020");
        int selectedWizard = PlayerPrefs.GetInt("SelectedWizard", 0);

        if (PlayerNameText != null)
            PlayerNameText.text = playerName;

        if (PlayerAgeText != null)
            PlayerAgeText.text = "Age: " + playerAge.ToString();

        if (PlayerBirthdayText != null)
            PlayerBirthdayText.text = "Birthday: " + playerBirthday;

        if (PlayerIcon != null && wizardSprites != null && selectedWizard < wizardSprites.Length)
        {
            PlayerIcon.sprite = wizardSprites[selectedWizard];
        }
        else
        {
            Debug.LogWarning("Player icon or sprites not assigned properly!");
        }
    }

    private void UpdateScoresDisplay()
    {
        // 1. Counting Kingdom
        int countingScore = PlayerPrefs.GetInt("CountingKingdom_Score", 0);
        if (countingKingdomScoreText != null) countingKingdomScoreText.text = "Score: " + countingScore;

        // 2. Shape Island
        int shapeScore = PlayerPrefs.GetInt("ShapeIsland_Score", 0);
        if (shapeIslandScoreText != null) shapeIslandScoreText.text = "Score: " + shapeScore;

        // 3. Color Forest
        int colorScore = PlayerPrefs.GetInt("ColorForest_Score", 0);
        if (colorForestScoreText != null) colorForestScoreText.text = "Score: " + colorScore;

        // 4. Math Mountain
        int mathScore = PlayerPrefs.GetInt("MathMountain_Score", 0);
        if (mathMountainScoreText != null) mathMountainScoreText.text = "Score: " + mathScore;

        // 5. Alphabet Order (Shows out of 26 max points)
        int alphabetScore = PlayerPrefs.GetInt("AlphabetOrder_Score", 0);
        if (alphabetOrderScoreText != null) alphabetOrderScoreText.text = $"Score: {alphabetScore}";

        int patternScore = SaveSystem.GetBestScore("PatternPlains");
        if (patternPlainsScoreText != null) patternPlainsScoreText.text = $"Score: {patternScore}";

        int matchingScore = SaveSystem.GetBestScore("ObjectMatching");
        if (objectMatchingScoreText != null) objectMatchingScoreText.text = $"Score: {matchingScore}";
    }

    private void SetupButtons()
    {
        AddListenerWhenMissing(countingKingdomButton, OpenCountingKingdom);
        AddListenerWhenMissing(shapeIslandButton, OpenShapeIsland);
        AddListenerWhenMissing(colorForestButton, OpenColorForest);
        AddListenerWhenMissing(mathMountainButton, OpenMathMountain);
        AddListenerWhenMissing(alphabetOrderButton, OpenAlphabetOrder);
        AddListenerWhenMissing(patternPlainsButton, OpenPatternPlains);
        AddListenerWhenMissing(objectMatchingButton, OpenObjectMatching);

        if (backButton != null && backButton.onClick.GetPersistentEventCount() == 0)
            backButton.onClick.AddListener(() => {
                MusicManager.instance?.PlayBackButtonSound();
                SceneManager.LoadScene("TitleScreen");
            });
    }

    private static void AddListenerWhenMissing(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null && button.onClick.GetPersistentEventCount() == 0)
            button.onClick.AddListener(action);
    }

    public void OpenCountingKingdom()
    {
        PlayerPrefs.SetString("CurrentGame", "CountingKingdom");
        MusicManager.instance?.PlayGameSelectSound();
        SceneManager.LoadScene("CountingKingdom");
    }

    public void OpenShapeIsland()
    {
        PlayerPrefs.SetString("CurrentGame", "ShapeIsland");
        MusicManager.instance?.PlayGameSelectSound();
        SceneManager.LoadScene("ShapeIsland");
    }

    public void OpenColorForest()
    {
        PlayerPrefs.SetString("CurrentGame", "ColorForest");
        MusicManager.instance?.PlayGameSelectSound();
        SceneManager.LoadScene("ColorForest");
    }

    public void OpenMathMountain()
    {
        PlayerPrefs.SetString("CurrentGame", "MathMountain");
        MusicManager.instance?.PlayGameSelectSound();
        SceneManager.LoadScene("MathMountain");
    }

    public void OpenAlphabetOrder()
    {
        PlayerPrefs.SetString("CurrentGame", "AlphabetOrder");
        MusicManager.instance?.PlayGameSelectSound();
        SceneManager.LoadScene("AlphabetOrder");
    }

    public void OpenPatternPlains()
    {
        PlayerPrefs.SetString("CurrentGame", "PatternPlains");
        MusicManager.instance?.PlayGameSelectSound();
        SceneManager.LoadScene("PatternPlains");
    }

    public void OpenObjectMatching()
    {
        PlayerPrefs.SetString("CurrentGame", "ObjectMatching");
        MusicManager.instance?.PlayGameSelectSound();
        SceneManager.LoadScene("ObjectMatching");
    }

    public void BackToTitleOrPrevious()
    {
        MusicManager.instance?.PlayBackButtonSound();
        SceneManager.LoadScene("TitleScreen");
    }
}
