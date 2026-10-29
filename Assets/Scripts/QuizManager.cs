using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class QuizManager : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI questionText;
    public Image questionImage;
    public Button[] answerButtons = new Button[4];
    public TextMeshProUGUI[] answerTexts = new TextMeshProUGUI[4];
    public TextMeshProUGUI timerText;
    public Image[] hearts = new Image[3];

    [Header("Feedback & Popups")]
    public TextMeshProUGUI feedbackText;
    public GameObject levelCompletePanel;
    public TextMeshProUGUI levelCompleteText;
    public Button nextLevelButton;
    public GameObject tryAgainPanel;
    public TextMeshProUGUI tryAgainText;

    [Header("Buttons")]
    public Button repeatButton;
    public Button backToLevelsButton;

    [Header("Settings")]
    public float timePerQuestion = 30f;

    [Range(0, 100)]
    public int passingScore = 60;

    [Header("Questions")]
    [Tooltip("Phase 2 question data. Legacy scene questions are used only when this is not assigned.")]
    public QuestionBankCatalog questionBankCatalog;
    public QuestionList[] countingQuestions;
    public QuestionList[] shapeQuestions;
    public QuestionList[] colorQuestions;
    public QuestionList[] mathQuestions;

    private int lives = 3;
    private float timeLeft;
    private bool answered = false;
    private int currentQuestionIndex = 0;
    private int correctAnswers = 0;
    private List<LearningQuestion> shuffledQuestions = new List<LearningQuestion>();
    private readonly List<Image> generatedAnswerImages = new List<Image>();
    private readonly int[] displayedAnswerSourceIndexes = new int[4];
    private int displayedCorrectAnswerIndex = -1;
    private string currentActivity;
    private LearningSkill currentSkill;
    private int currentLevel;
    private LocalGameplaySession localGameplaySession;

    public string CurrentQuestionId { get; private set; }

    [System.Serializable]
    public class Question
    {
        [TextArea(2, 4)]
        public string question;
        public Sprite image;
        public string[] answers = new string[4];
        public int correctIndex;
    }

    [System.Serializable]
    public class QuestionList
    {
        public Question[] questions = new Question[3];
    }

    void Start()
    {
        currentActivity = PlayerPrefs.GetString("CurrentGame", "CountingKingdom");
        string currentGame = currentActivity.ToLower();

        Debug.Log("Current Game Detected: " + currentGame);

        if (currentGame.Contains("counting"))
            LoadQuestions(LearningSkill.Counting, countingQuestions);
        else if (currentGame.Contains("shape"))
            LoadQuestions(LearningSkill.Shapes, shapeQuestions);
        else if (currentGame.Contains("color"))
            LoadQuestions(LearningSkill.Colors, colorQuestions);
        else if (currentGame.Contains("math"))
            LoadQuestions(LearningSkill.Math, mathQuestions);
        else if (currentGame.Contains("pattern"))
            LoadQuestions(LearningSkill.Patterns, null);
        else if (currentGame.Contains("match"))
            LoadQuestions(LearningSkill.ObjectMatching, null);
        else
            LoadQuestions(LearningSkill.Counting, countingQuestions);

        lives = 3;
        UpdateHearts();

        if (feedbackText != null) feedbackText.gameObject.SetActive(false);
        if (levelCompletePanel != null) levelCompletePanel.SetActive(false);
        if (tryAgainPanel != null) tryAgainPanel.SetActive(false);

        if (shuffledQuestions.Count > 0)
        {
            localGameplaySession = LocalGameplaySession.Begin(
                currentActivity,
                currentSkill,
                currentLevel,
                shuffledQuestions.Count);
            LoadNextQuestion();
        }
        else
            ShowQuestionDataError();
    }

    private void LoadQuestions(LearningSkill skill, QuestionList[] legacyLists)
    {
        int level = Mathf.Clamp(PlayerPrefs.GetInt("CurrentSubLevel", 1), 1, 5);
        currentSkill = skill;
        currentLevel = level;
        QuestionBank bank = questionBankCatalog != null
            ? questionBankCatalog.FindBank(skill, level)
            : null;

        if (bank != null && bank.questions != null && bank.questions.Count > 0)
        {
            shuffledQuestions = bank.questions.Where(question => question != null).ToList();
            ShuffleList(shuffledQuestions);
            return;
        }

        // Backward-compatible fallback for scenes that have not been migrated yet.
        int levelIndex = level - 1;
        if (legacyLists != null && levelIndex < legacyLists.Length && legacyLists[levelIndex] != null)
        {
            Question[] legacyQuestions = legacyLists[levelIndex].questions;
            if (legacyQuestions != null)
            {
                shuffledQuestions = legacyQuestions
                    .Where(question => question != null)
                    .Select((question, index) => ConvertLegacyQuestion(question, skill, level, index))
                    .ToList();
                ShuffleList(shuffledQuestions);
                return;
            }
        }

        Debug.LogError($"No question bank found for {skill} level {level}.");
        shuffledQuestions.Clear();
    }

    private static LearningQuestion ConvertLegacyQuestion(
        Question legacyQuestion,
        LearningSkill skill,
        int level,
        int index)
    {
        return new LearningQuestion
        {
            questionId = $"{skill.ToString().ToLowerInvariant()}-legacy-l{level:00}-q{index + 1:00}",
            prompt = legacyQuestion.question,
            image = legacyQuestion.image,
            answers = legacyQuestion.answers != null
                ? (string[])legacyQuestion.answers.Clone()
                : new string[4],
            answerImages = new Sprite[4],
            correctIndex = legacyQuestion.correctIndex,
            difficulty = level,
            competency = GetDefaultCompetency(skill)
        };
    }

    private static string GetDefaultCompetency(LearningSkill skill)
    {
        switch (skill)
        {
            case LearningSkill.Counting:
                return "Counts and identifies quantities";
            case LearningSkill.Shapes:
                return "Recognizes and names basic shapes";
            case LearningSkill.Colors:
                return "Recognizes and names colors";
            case LearningSkill.Math:
                return "Solves age-appropriate arithmetic";
            case LearningSkill.Patterns:
                return "Identifies and completes patterns";
            case LearningSkill.ObjectMatching:
                return "Matches related objects";
            case LearningSkill.Alphabet:
                return "Recognizes and orders letters";
            default:
                return skill.ToString();
        }
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int rand = Random.Range(i, list.Count);
            (list[i], list[rand]) = (list[rand], list[i]);
        }
    }

    private void LoadNextQuestion()
    {
        if (currentQuestionIndex >= shuffledQuestions.Count)
        {
            ShowLevelCompletePanel();
            return;
        }

        answered = false;
        timeLeft = timePerQuestion;

        LearningQuestion q = shuffledQuestions[currentQuestionIndex];
        CurrentQuestionId = q.questionId;

        if (questionText != null) questionText.text = q.prompt;
        if (questionImage != null)
        {
            questionImage.sprite = q.image;
            questionImage.type = Image.Type.Simple;
            questionImage.preserveAspect = true;
        }

        var shuffledAnswers = Enumerable.Range(0, 4)
            .Select(index => new AnswerOption(
                q.answers != null && index < q.answers.Length ? q.answers[index] : string.Empty,
                q.answerImages != null && index < q.answerImages.Length ? q.answerImages[index] : null,
                index == q.correctIndex,
                index))
            .ToList();
        ShuffleList(shuffledAnswers);
        displayedCorrectAnswerIndex = shuffledAnswers.FindIndex(answer => answer.isCorrect);

        for (int i = 0; i < 4; i++)
        {
            displayedAnswerSourceIndexes[i] = shuffledAnswers[i].sourceIndex;
            if (answerButtons[i] == null) continue;
            int index = i;
            if (i < answerTexts.Length && answerTexts[i] != null)
            {
                answerTexts[i].text = shuffledAnswers[i].image == null ? shuffledAnswers[i].text : string.Empty;
                answerTexts[i].gameObject.SetActive(shuffledAnswers[i].image == null);
            }

            Image answerImage = GetOrCreateAnswerImage(i);
            answerImage.sprite = shuffledAnswers[i].image;
            answerImage.gameObject.SetActive(shuffledAnswers[i].image != null);
            answerButtons[i].onClick.RemoveAllListeners();
            answerButtons[i].onClick.AddListener(() => OnAnswerSelected(index));
            answerButtons[i].interactable = true;
            answerButtons[i].image.color = Color.white;
        }

        if (feedbackText != null) feedbackText.gameObject.SetActive(false);
        localGameplaySession?.BeginQuestion();
    }

    public void OnAnswerSelected(int selectedIndex)
    {
        if (answered) return;
        answered = true;

        for (int i = 0; i < answerButtons.Length; i++)
            if (answerButtons[i] != null) answerButtons[i].interactable = false;

        bool isCorrect = selectedIndex == displayedCorrectAnswerIndex;
        LearningQuestion currentQuestion = shuffledQuestions[currentQuestionIndex];
        int? selectedOriginalIndex = selectedIndex >= 0 && selectedIndex < displayedAnswerSourceIndexes.Length
            ? displayedAnswerSourceIndexes[selectedIndex]
            : (int?)null;
        localGameplaySession?.RecordAnswer(currentQuestion, selectedOriginalIndex, isCorrect, false);

        if (isCorrect)
        {
            correctAnswers++;
            answerButtons[selectedIndex].image.color = Color.green;
            ShowFeedback("Very Good!", Color.green);
        }
        else
        {
            answerButtons[selectedIndex].image.color = Color.red;
            if (displayedCorrectAnswerIndex >= 0 &&
                displayedCorrectAnswerIndex < answerButtons.Length &&
                answerButtons[displayedCorrectAnswerIndex] != null)
                answerButtons[displayedCorrectAnswerIndex].image.color = Color.green;
            lives--;
            UpdateHearts();
            ShowFeedback("Try Again!", Color.red);
        }

        if (lives <= 0)
        {
            FinishLocalSession(false, "failed");
            ShowTryAgainPanel();
            return;
        }

        StartCoroutine(NextQuestionDelay());
    }

    private void ShowFeedback(string message, Color color)
    {
        if (feedbackText == null) return;
        feedbackText.text = message;
        feedbackText.color = color;
        feedbackText.gameObject.SetActive(true);
    }

    private void ShowLevelCompletePanel()
    {
        answered = true;

        int score = shuffledQuestions.Count > 0
            ? Mathf.RoundToInt((float)correctAnswers / shuffledQuestions.Count * 100f)
            : 0;
        bool passed = score >= passingScore;

        FinishLocalSession(passed, passed ? "completed" : "failed", score);

        if (!passed)
        {
            ShowTryAgainPanel($"Score: {score}%\nYou need {passingScore}% to pass.");
            return;
        }

        if (levelCompletePanel != null)
        {
            levelCompletePanel.SetActive(true);
            if (levelCompleteText != null)
                levelCompleteText.text = $"Level Complete!\nScore: {score}%";
        }

        string currentGame = PlayerPrefs.GetString("CurrentGame", "CountingKingdom");
        int currentLevel = PlayerPrefs.GetInt("CurrentSubLevel", 1);

        SaveSystem.SaveProgress(currentGame, currentLevel);
        SaveSystem.SaveBestScore(currentGame, score);

        if (nextLevelButton != null)
        {
            nextLevelButton.onClick.RemoveAllListeners();
            nextLevelButton.onClick.AddListener(GoToNextLevel);
        }
    }

    public void GoToNextLevel()
    {
        int currentLevel = PlayerPrefs.GetInt("CurrentSubLevel", 1);
        int nextLevel = currentLevel + 1;

        if (nextLevel <= 5)
        {
            PlayerPrefs.SetInt("CurrentSubLevel", nextLevel);
            SceneManager.LoadScene("Gameplay");
        }
        else
        {
            SceneManager.LoadScene("AdventureMap");
        }
    }

    private void ShowTryAgainPanel()
    {
        ShowTryAgainPanel("Try Again!");
    }

    private void ShowTryAgainPanel(string message)
    {
        if (tryAgainPanel != null)
        {
            tryAgainPanel.SetActive(true);
            if (tryAgainText != null)
                tryAgainText.text = message;
        }
    }

    private void ShowQuestionDataError()
    {
        answered = true;
        CurrentQuestionId = string.Empty;

        if (questionText != null)
            questionText.text = "Questions are not available for this level yet.";

        if (timerText != null)
            timerText.text = "--";

        for (int i = 0; i < answerButtons.Length; i++)
            if (answerButtons[i] != null)
                answerButtons[i].interactable = false;
    }

    private Image GetOrCreateAnswerImage(int buttonIndex)
    {
        while (generatedAnswerImages.Count <= buttonIndex)
            generatedAnswerImages.Add(null);

        if (generatedAnswerImages[buttonIndex] != null)
            return generatedAnswerImages[buttonIndex];

        Transform existing = answerButtons[buttonIndex].transform.Find("AnswerIcon");
        Image image = existing != null ? existing.GetComponent<Image>() : null;

        if (image == null)
        {
            var imageObject = new GameObject("AnswerIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(answerButtons[buttonIndex].transform, false);
            image = imageObject.GetComponent<Image>();

            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.18f, 0.16f);
            rect.anchorMax = new Vector2(0.82f, 0.84f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.raycastTarget = false;
        generatedAnswerImages[buttonIndex] = image;
        return image;
    }

    public void RepeatLevel()
    {
        SceneManager.LoadScene("Gameplay");
    }

    public void BackToLevelSelect()
    {
        SceneManager.LoadScene("AdventureMap");
    }

    private void UpdateHearts()
    {
        for (int i = 0; i < hearts.Length; i++)
            if (hearts[i] != null)
                hearts[i].enabled = i < lives;
    }

    IEnumerator NextQuestionDelay()
    {
        yield return new WaitForSeconds(1.8f);
        currentQuestionIndex++;
        LoadNextQuestion();
    }

    private readonly struct AnswerOption
    {
        public readonly string text;
        public readonly Sprite image;
        public readonly bool isCorrect;
        public readonly int sourceIndex;

        public AnswerOption(string text, Sprite image, bool isCorrect, int sourceIndex)
        {
            this.text = text;
            this.image = image;
            this.isCorrect = isCorrect;
            this.sourceIndex = sourceIndex;
        }
    }

    void Update()
    {
        if (!answered && timeLeft > 0)
        {
            timeLeft -= Time.deltaTime;
            if (timerText != null)
                timerText.text = Mathf.Ceil(timeLeft).ToString();

            if (timeLeft <= 0)
            {
                answered = true;

                for (int i = 0; i < answerButtons.Length; i++)
                    if (answerButtons[i] != null) answerButtons[i].interactable = false;

                lives--;
                LearningQuestion currentQuestion = shuffledQuestions[currentQuestionIndex];
                localGameplaySession?.RecordAnswer(currentQuestion, null, false, true);
                UpdateHearts();
                ShowFeedback("Try Again!", Color.red);

                if (lives > 0)
                    StartCoroutine(NextQuestionDelay());
                else
                {
                    FinishLocalSession(false, "failed");
                    ShowTryAgainPanel();
                }
            }
        }
    }

    private void FinishLocalSession(bool passed, string status, int? explicitScore = null)
    {
        int score = explicitScore ?? (shuffledQuestions.Count > 0
            ? Mathf.RoundToInt((float)correctAnswers / shuffledQuestions.Count * 100f)
            : 0);

        localGameplaySession?.Finish(correctAnswers, score, passed, lives, status);
    }

    private void OnDestroy()
    {
        localGameplaySession?.Abandon(correctAnswers, lives);
    }
}
