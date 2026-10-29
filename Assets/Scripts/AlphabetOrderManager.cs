using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;

public class AlphabetOrderManager : MonoBehaviour
{
    public Transform letterPoolParent;
    public Transform slotsParent;
    public Button checkButton;
    public Button resetButton;
    public Button backButton;
    public TextMeshProUGUI feedbackText;
    public AudioSource audioSource;
    public AudioClip buttonClickSound;
    public AudioClip correctSound;
    public AudioClip wrongSound;
    public GameObject tutorialPanel;
    public TextMeshProUGUI tutorialText;
    public Button tutorialNextButton;
    private int tutorialStep = 0;
    public bool isDragging = false;

    [Header("Score System")]
    public TextMeshProUGUI scoreText;
    private int currentScore = 0;

    private float idleTimer = 0f;
    private const float HINT_DELAY = 10f;
    private Image highlightedLetterImage = null;
    private Color originalLetterColor;
    private Coroutine feedbackCoroutine;
    private Coroutine blinkCoroutine;
    private LocalGameplaySession localGameplaySession;
    private LearningQuestion alphabetOrderQuestion;

    void Start()
    {
        InitScore();

        // Fix: Explicitly hook up the button to use the true parameter via code lambda
        if (checkButton != null)
        {
            checkButton.onClick.RemoveAllListeners(); // Clear old broken links
            checkButton.onClick.AddListener(() => { PlayButtonClick(); CheckOrder(true); });
        }

        if (resetButton != null)
            resetButton.onClick.AddListener(() => { PlayButtonClick(); ResetGame(); });

        if (backButton != null)
            backButton.onClick.AddListener(() => { PlayButtonClick(); GoBack(); });

        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(true);
            ShowTutorialStep(0);
        }

        if (tutorialNextButton != null)
            tutorialNextButton.onClick.AddListener(() => { PlayButtonClick(); NextTutorialStep(); });

        if (feedbackText != null) feedbackText.text = "";

        GenerateAndShuffleLetters();
        BeginLocalTracking();
    }

    private void InitScore()
    {
        currentScore = 0;
        if (scoreText != null) scoreText.text = "Score: " + currentScore;
    }

    private void Update()
    {
        if (tutorialPanel != null && tutorialPanel.activeSelf) return;
        if (isDragging) return;

        idleTimer += Time.deltaTime;

        if (idleTimer >= HINT_DELAY)
        {
            ShowHint();
            idleTimer = 0f;
        }
    }

    public void ResetIdleTimer(bool clearCurrentHint = false)
    {
        idleTimer = 0f;
        if (clearCurrentHint)
        {
            RemoveHintHighlight();
        }
    }

    private void ShowHint()
    {
        if (highlightedLetterImage != null) return;

        string correctAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        for (int i = 0; i < slotsParent.childCount; i++)
        {
            DropSlot slot = slotsParent.GetChild(i).GetComponent<DropSlot>();
            if (slot != null && string.IsNullOrEmpty(slot.GetLetter()))
            {
                if (i < correctAlphabet.Length)
                {
                    char targetLetter = correctAlphabet[i];

                    foreach (Transform child in letterPoolParent)
                    {
                        DraggableLetter draggable = child.GetComponent<DraggableLetter>();
                        if (draggable != null && draggable.letterText.text.Trim().ToUpper() == targetLetter.ToString())
                        {
                            Image img = child.GetComponent<Image>();
                            if (img != null)
                            {
                                highlightedLetterImage = img;
                                originalLetterColor = img.color;
                                blinkCoroutine = StartCoroutine(BlinkHintRoutine());
                            }
                            return;
                        }
                    }
                }
            }
        }
    }

    private IEnumerator BlinkHintRoutine()
    {
        while (highlightedLetterImage != null)
        {
            highlightedLetterImage.color = Color.yellow;
            yield return new WaitForSeconds(0.5f);

            if (highlightedLetterImage == null) yield break;

            highlightedLetterImage.color = originalLetterColor;
            yield return new WaitForSeconds(0.5f);
        }
    }

    public void RemoveHintHighlight(bool forceResetColor = false)
    {
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }

        if (highlightedLetterImage != null)
        {
            if (forceResetColor)
            {
                highlightedLetterImage.color = originalLetterColor;
            }
            highlightedLetterImage = null;
        }
    }

    public void ShowTimedFeedback(string text, Color color)
    {
        if (feedbackText == null) return;

        if (feedbackCoroutine != null)
            StopCoroutine(feedbackCoroutine);

        feedbackText.text = text;
        feedbackText.color = color;

        feedbackCoroutine = StartCoroutine(ClearFeedbackAfterTime(3f));
    }

    private IEnumerator ClearFeedbackAfterTime(float duration)
    {
        yield return new WaitForSeconds(duration);
        feedbackText.text = "";
    }

    private void PlayButtonClick()
    {
        if (audioSource != null && buttonClickSound != null)
            audioSource.PlayOneShot(buttonClickSound);
    }

    private void ShowTutorialStep(int step)
    {
        tutorialStep = step;
        if (tutorialText == null) return;

        switch (step)
        {
            case 0:
                tutorialText.text = "Welcome to Alphabet Order!";
                tutorialText.fontSize = 90;
                tutorialText.alignment = TextAlignmentOptions.Center;
                break;
            case 1:
                tutorialText.text = "Drag the letters to arrange them A to Z from left to right.";
                tutorialText.fontSize = 72;
                break;
            case 2:
                tutorialText.text = "If you put the wrong letter, it will automatically go back down.";
                tutorialText.fontSize = 72;
                break;
            case 3:
                tutorialText.text = "When all letters are correct, click the Done button!";
                tutorialText.fontSize = 72;
                break;
            default:
                tutorialPanel.SetActive(false);
                ResetIdleTimer(false);
                return;
        }
    }

    private void NextTutorialStep()
    {
        ShowTutorialStep(tutorialStep + 1);
    }

    private void GenerateAndShuffleLetters()
    {
        if (letterPoolParent == null) return;

        foreach (Transform child in letterPoolParent)
            Destroy(child.gameObject);

        string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        for (int i = 0; i < alphabet.Length; i++)
        {
            char c = alphabet[i];
            GameObject letterObj = new GameObject("Letter_" + c);

            RectTransform rect = letterObj.AddComponent<RectTransform>();
            Image img = letterObj.AddComponent<Image>();
            Button btn = letterObj.AddComponent<Button>();
            CanvasGroup cg = letterObj.AddComponent<CanvasGroup>();
            LayoutElement le = letterObj.AddComponent<LayoutElement>();
            DraggableLetter draggable = letterObj.AddComponent<DraggableLetter>();

            GameObject textObj = new GameObject("Text");
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            TextMeshProUGUI txt = textObj.AddComponent<TextMeshProUGUI>();

            textObj.transform.SetParent(letterObj.transform);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            txt.text = c.ToString();
            txt.fontSize = 55;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = Color.white;

            letterObj.transform.SetParent(letterPoolParent);
            letterObj.transform.localScale = Vector3.one;

            Color buttonColor = Random.ColorHSV(0.5f, 1f, 0.8f, 1f, 0.9f, 1f);
            buttonColor.a = 1f;
            img.color = buttonColor;
            cg.alpha = 1f;

            draggable.letterText = txt;
            btn.interactable = false;

            ColorBlock colors = btn.colors;
            colors.disabledColor = new Color(1f, 1f, 1f, 1f);
            btn.colors = colors;
        }
        ShuffleChildren(letterPoolParent);
    }

    private void ShuffleChildren(Transform parent)
    {
        int childCount = parent.childCount;
        for (int i = 0; i < childCount; i++)
        {
            int randomIndex = Random.Range(0, childCount);
            parent.GetChild(i).SetSiblingIndex(randomIndex);
        }
    }

    public void GoBack()
    {
        MusicManager.instance?.PlayBackButtonSound();
        SceneManager.LoadScene("AdventureMap");
    }

    public void CheckOrder()
    {
        CheckOrder(false); // Called mid-game when a letter drops
    }

    public void CheckOrder(bool isManualCheck)
    {
        if (slotsParent == null) return;

        string formedWord = "";
        int filledCount = 0;

        DraggableLetter[] allLetters = FindObjectsByType<DraggableLetter>(FindObjectsSortMode.None);
        string correctAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        int calculatedPoints = 0;

        for (int i = 0; i < slotsParent.childCount; i++)
        {
            Transform slotTransform = slotsParent.GetChild(i);
            RectTransform slotRect = slotTransform.GetComponent<RectTransform>();
            string foundLetterAtSlot = "";

            if (slotRect != null)
            {
                float closestDistance = 50f;

                foreach (DraggableLetter letterComp in allLetters)
                {
                    RectTransform letterRect = letterComp.GetComponent<RectTransform>();
                    if (letterRect != null)
                    {
                        float distance = Vector2.Distance(slotRect.position, letterRect.position);
                        if (distance < closestDistance)
                        {
                            TextMeshProUGUI textComp = letterComp.GetComponentInChildren<TextMeshProUGUI>();
                            if (textComp != null)
                            {
                                foundLetterAtSlot = textComp.text.Replace(" ", "").Trim().ToUpper();
                                closestDistance = distance;
                            }
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(foundLetterAtSlot))
            {
                formedWord += foundLetterAtSlot;
                filledCount++;

                if (i < correctAlphabet.Length && foundLetterAtSlot == correctAlphabet[i].ToString())
                {
                    calculatedPoints++;
                }
            }
        }

        currentScore = calculatedPoints;
        if (scoreText != null) scoreText.text = "Score: " + currentScore;

        // --- MANUAL SUBMIT CHECK (When pressing Done Button) ---
        if (isManualCheck)
        {
            bool isPerfectOrder = formedWord == correctAlphabet;
            localGameplaySession?.RecordAnswer(alphabetOrderQuestion, null, isPerfectOrder, false);

            // 1. If everything is full and correct -> WIN!
            if (isPerfectOrder)
            {
                if (audioSource != null && correctSound != null)
                    audioSource.PlayOneShot(correctSound);

                if (feedbackText != null)
                {
                    if (feedbackCoroutine != null) StopCoroutine(feedbackCoroutine);
                    feedbackText.text = "Excellent! Perfect Order!";
                    feedbackText.color = Color.green;
                }

                PlayerPrefs.SetInt("AlphabetOrder_Score", currentScore);
                PlayerPrefs.Save();
                localGameplaySession?.Finish(1, 100, true, 3, "completed");

                Invoke("GoToAdventureMap", 5.0f);
            }
            // 2. If the slots are NOT completely full yet -> Keep Going!
            else if (filledCount < 26)
            {
                if (audioSource != null && wrongSound != null)
                    audioSource.PlayOneShot(wrongSound);

                ShowTimedFeedback("Keep going! Fill all the slots.", Color.red);
                localGameplaySession?.BeginQuestion();
            }
            // 3. If they filled all 26 but some are mixed up
            else
            {
                if (audioSource != null && wrongSound != null)
                    audioSource.PlayOneShot(wrongSound);

                ShowTimedFeedback("Wrong! Check your order and try again!", Color.red);
                localGameplaySession?.BeginQuestion();
            }
        }
    }

    private void GoToAdventureMap()
    {
        SceneManager.LoadScene("AdventureMap");
    }

    public void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    public void ResetGame()
    {
        if (slotsParent == null) return;

        foreach (Transform slot in slotsParent)
        {
            DropSlot dropSlot = slot.GetComponent<DropSlot>();
            if (dropSlot != null)
            {
                DraggableLetter letter = dropSlot.RemoveLetter();
                if (letter != null)
                {
                    letter.transform.SetParent(letterPoolParent);
                    letter.transform.localScale = Vector3.one;

                    RectTransform letterRect = letter.GetComponent<RectTransform>();
                    if (letterRect != null)
                    {
                        letterRect.localPosition = Vector3.zero;
                        letterRect.anchoredPosition = Vector2.zero;
                    }
                }
            }
        }

        ShowTimedFeedback("Game Reset!", Color.white);
        ShuffleChildren(letterPoolParent);
        InitScore();
        ResetIdleTimer();
    }

    private void BeginLocalTracking()
    {
        alphabetOrderQuestion = new LearningQuestion
        {
            questionId = "alphabet-order-a-to-z",
            prompt = "Arrange the letters from A to Z.",
            answers = new[] { "A to Z", "Z to A", "Mixed", "Incomplete" },
            answerImages = new Sprite[4],
            correctIndex = 0,
            difficulty = 1,
            competency = "Recognizes and orders letters"
        };

        localGameplaySession = LocalGameplaySession.Begin(
            "AlphabetOrder",
            LearningSkill.Alphabet,
            1,
            1);
        localGameplaySession.BeginQuestion();
    }

    private void OnDestroy()
    {
        int partialScore = Mathf.RoundToInt((float)currentScore / 26f * 100f);
        localGameplaySession?.Abandon(0, 3, partialScore);
    }
}
