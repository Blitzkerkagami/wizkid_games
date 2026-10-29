using System;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Adds the Phase 6D UAT and ISO 25010 questionnaire to AdventureMap as a
/// full-screen, mobile-friendly flow. One required rating is shown per page.
/// </summary>
public sealed class EvaluationCenter : MonoBehaviour
{
    private static readonly Color Navy = new Color32(24, 29, 77, 255);
    private static readonly Color Purple = new Color32(118, 75, 209, 255);
    private static readonly Color Blue = new Color32(55, 156, 226, 255);
    private static readonly Color Gold = new Color32(251, 179, 43, 255);
    private static readonly Color Green = new Color32(54, 181, 119, 255);
    private static readonly Color Coral = new Color32(239, 100, 101, 255);
    private static readonly Color Paper = new Color32(255, 252, 243, 255);
    private static readonly Color Ink = new Color32(40, 42, 73, 255);
    private static readonly Color Muted = new Color32(94, 96, 125, 255);

    private static Sprite roundedSprite;
    private GameObject overlay;
    private RectTransform contentRoot;
    private TextMeshProUGUI headerTitle;
    private TextMeshProUGUI syncText;
    private TMP_FontAsset font;
    private EvaluationInstrument instrument;
    private string respondentRole = string.Empty;
    private EvaluationQuestion[] questions = Array.Empty<EvaluationQuestion>();
    private int[] ratings = Array.Empty<int>();
    private int currentQuestion;
    private string draftComments = string.Empty;
    private View currentView = View.RoleSelection;
    private bool built;

    private enum View
    {
        RoleSelection,
        Consent,
        Question,
        Review,
        Complete
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        Install(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Install(scene);
    }

    private static void Install(Scene scene)
    {
        if (!scene.IsValid() || !string.Equals(scene.name, "AdventureMap", StringComparison.Ordinal))
            return;

        Canvas canvas = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            canvas = root.GetComponentInChildren<Canvas>(true);
            if (canvas != null)
                break;
        }

        if (canvas == null || canvas.transform.Find("EvaluationCenterRuntime") != null)
            return;

        GameObject host = new GameObject("EvaluationCenterRuntime", typeof(RectTransform));
        host.layer = 5;
        host.transform.SetParent(canvas.transform, false);
        EvaluationCenter center = host.AddComponent<EvaluationCenter>();
        center.Build(canvas);
    }

    private void OnEnable()
    {
        EvaluationSyncService.StatusChanged += UpdateSyncText;
    }

    private void OnDisable()
    {
        EvaluationSyncService.StatusChanged -= UpdateSyncText;
    }

    private void Build(Canvas canvas)
    {
        if (built)
            return;
        built = true;
        TextMeshProUGUI existingText = canvas.GetComponentInChildren<TextMeshProUGUI>(true);
        font = existingText != null ? existingText.font : TMP_Settings.defaultFontAsset;

        Button launcher = CreateButton("EvaluationButton", canvas.transform,
            "Evaluate Game", Purple, 25f);
        Place(launcher.GetComponent<RectTransform>(), new Vector2(1f, 1f),
            new Vector2(-500f, -121f), new Vector2(280f, 58f));
        launcher.onClick.AddListener(Show);

        overlay = CreateImage("EvaluationOverlay", canvas.transform,
            new Color32(22, 24, 72, 255), false).gameObject;
        Stretch(overlay.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero);

        Image topGlow = CreateImage("Top Glow", overlay.transform,
            new Color(0.34f, 0.18f, 0.65f, 0.55f), false);
        Anchor(topGlow.rectTransform, new Vector2(0f, 0.72f), Vector2.one,
            Vector2.zero, Vector2.zero);
        AddStars(overlay.transform);

        Image header = CreateImage("Header", overlay.transform,
            new Color(0.05f, 0.06f, 0.20f, 0.92f), false);
        Anchor(header.rectTransform, new Vector2(0f, 1f), Vector2.one,
            new Vector2(0f, -145f), Vector2.zero);

        Button back = CreateButton("Back", overlay.transform, "Back",
            new Color32(247, 139, 34, 255), 27f);
        Place(back.GetComponent<RectTransform>(), new Vector2(0f, 1f),
            new Vector2(120f, -72f), new Vector2(185f, 72f));
        back.onClick.AddListener(GoBack);

        Button close = CreateButton("Close", overlay.transform, "Close", Coral, 27f);
        Place(close.GetComponent<RectTransform>(), new Vector2(1f, 1f),
            new Vector2(-120f, -72f), new Vector2(185f, 72f));
        close.onClick.AddListener(Hide);

        headerTitle = CreateText("Header Title", overlay.transform,
            "WizKid Evaluation", 47f, FontStyles.Bold,
            TextAlignmentOptions.Center, Color.white);
        Anchor(headerTitle.rectTransform, new Vector2(0f, 1f), Vector2.one,
            new Vector2(260f, -91f), new Vector2(-260f, -24f));

        syncText = CreateText("Sync Status", overlay.transform,
            string.Empty, 19f, FontStyles.Normal,
            TextAlignmentOptions.Center, new Color32(219, 220, 255, 255));
        Anchor(syncText.rectTransform, new Vector2(0f, 1f), Vector2.one,
            new Vector2(360f, -132f), new Vector2(-360f, -96f));

        Image window = CreateImage("Questionnaire Window", overlay.transform, Paper, true);
        Anchor(window.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(80f, 55f), new Vector2(-80f, -175f));

        GameObject content = new GameObject("Evaluation Content", typeof(RectTransform));
        content.layer = 5;
        content.transform.SetParent(window.transform, false);
        contentRoot = content.GetComponent<RectTransform>();
        Stretch(contentRoot, Vector2.zero, Vector2.one,
            new Vector2(44f, 34f), new Vector2(-44f, -34f));

        overlay.SetActive(false);
    }

    private void Show()
    {
        MusicManager.instance?.PlayGameSelectSound();
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
        ShowRoleSelection();
        UpdateSyncText();
    }

    private void Hide()
    {
        MusicManager.instance?.PlayBackButtonSound();
        overlay.SetActive(false);
    }

    private void ShowRoleSelection()
    {
        currentView = View.RoleSelection;
        headerTitle.text = "WizKid Evaluation";
        respondentRole = string.Empty;
        questions = Array.Empty<EvaluationQuestion>();
        ratings = Array.Empty<int>();
        currentQuestion = 0;
        draftComments = string.Empty;
        ClearContent();

        TextMeshProUGUI title = CreateText("Title", contentRoot,
            "Who is evaluating the game?", 43f, FontStyles.Bold,
            TextAlignmentOptions.Center, Ink);
        Anchor(title.rectTransform, new Vector2(0f, 1f), Vector2.one,
            new Vector2(30f, -80f), new Vector2(-30f, -20f));

        TextMeshProUGUI subtitle = CreateText("Subtitle", contentRoot,
            "Choose one role. Learners should answer with help from an adult.",
            25f, FontStyles.Normal, TextAlignmentOptions.Center, Muted);
        Anchor(subtitle.rectTransform, new Vector2(0f, 1f), Vector2.one,
            new Vector2(100f, -132f), new Vector2(-100f, -83f));

        CreateRoleCard(0, "Learner + Adult", "UAT", "learner", Blue,
            "Share how the game feels while learning.");
        CreateRoleCard(1, "Parent", "UAT", "parent", Gold,
            "Evaluate learning support and child-friendly use.");
        CreateRoleCard(2, "Teacher", "UAT", "teacher", Green,
            "Evaluate classroom value and learner experience.");
        CreateRoleCard(3, "IT Evaluator", "ISO 25010", "it_evaluator", Purple,
            "Evaluate technical software quality.");

        TextMeshProUGUI note = CreateText("Privacy Note", contentRoot,
            "No name, birthday, email address, or phone number will be collected.",
            21f, FontStyles.Bold, TextAlignmentOptions.Center, Purple);
        Anchor(note.rectTransform, Vector2.zero, new Vector2(1f, 0f),
            new Vector2(80f, 18f), new Vector2(-80f, 57f));
    }

    private void CreateRoleCard(
        int index,
        string title,
        string badge,
        string role,
        Color color,
        string description)
    {
        float width = 0.235f;
        float gap = 0.02f;
        float left = index * (width + gap);
        Image card = CreateImage(title, contentRoot, new Color32(245, 247, 255, 255), true);
        Anchor(card.rectTransform, new Vector2(left, 0.18f),
            new Vector2(left + width, 0.75f), new Vector2(4f, 0f), new Vector2(-4f, 0f));

        Image badgePanel = CreateImage("Badge", card.transform, color, true);
        Anchor(badgePanel.rectTransform, new Vector2(0.18f, 0.71f),
            new Vector2(0.82f, 0.91f), Vector2.zero, Vector2.zero);
        TextMeshProUGUI badgeText = CreateText("Badge Label", badgePanel.transform,
            badge, 22f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        Stretch(badgeText.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(8f, 3f), new Vector2(-8f, -3f));

        TextMeshProUGUI roleTitle = CreateText("Role", card.transform, title,
            28f, FontStyles.Bold, TextAlignmentOptions.Center, Ink);
        Anchor(roleTitle.rectTransform, new Vector2(0f, 0.49f), new Vector2(1f, 0.72f),
            new Vector2(15f, 0f), new Vector2(-15f, 0f));

        TextMeshProUGUI details = CreateText("Description", card.transform, description,
            20f, FontStyles.Normal, TextAlignmentOptions.Center, Muted);
        details.enableWordWrapping = true;
        Anchor(details.rectTransform, new Vector2(0f, 0.23f), new Vector2(1f, 0.51f),
            new Vector2(22f, 0f), new Vector2(-22f, 0f));

        Button select = CreateButton("Select", card.transform, "Select", color, 24f);
        Anchor(select.GetComponent<RectTransform>(), new Vector2(0.16f, 0.05f),
            new Vector2(0.84f, 0.22f), Vector2.zero, Vector2.zero);
        select.onClick.AddListener(() => SelectRole(role));
    }

    private void SelectRole(string role)
    {
        respondentRole = role;
        instrument = role == "it_evaluator"
            ? EvaluationInstrument.Iso25010
            : EvaluationInstrument.Uat;
        questions = instrument == EvaluationInstrument.Iso25010
            ? EvaluationQuestionBank.Iso25010
            : EvaluationQuestionBank.Uat;
        ratings = new int[questions.Length];
        ShowConsent();
    }

    private void ShowConsent()
    {
        currentView = View.Consent;
        headerTitle.text = instrument == EvaluationInstrument.Iso25010
            ? "ISO 25010 Evaluation"
            : "User Acceptance Test";
        ClearContent();

        TextMeshProUGUI title = CreateText("Title", contentRoot,
            "Before You Begin", 44f, FontStyles.Bold,
            TextAlignmentOptions.Center, Purple);
        Anchor(title.rectTransform, new Vector2(0f, 0.82f), Vector2.one,
            new Vector2(50f, 0f), new Vector2(-50f, -15f));

        Image info = CreateImage("Information", contentRoot,
            new Color32(241, 245, 255, 255), true);
        Anchor(info.rectTransform, new Vector2(0.16f, 0.30f), new Vector2(0.84f, 0.80f),
            Vector2.zero, Vector2.zero);
        TextMeshProUGUI copy = CreateText("Information Text", info.transform,
            "Please test the game before answering. All questions use a 1 to 5 rating. " +
            "Every rating is required, while the final comment is optional.\n\n" +
            "The response does not ask for a name, birthday, contact detail, or password. " +
            "It is saved on this device first and securely synchronized when internet is available.\n\n" +
            (respondentRole == "learner"
                ? "A parent, teacher, or researcher should read each question aloud and let the learner choose the answer."
                : "Submit only one response for this testing session."),
            27f, FontStyles.Normal, TextAlignmentOptions.Center, Ink);
        copy.enableWordWrapping = true;
        copy.lineSpacing = 7f;
        Stretch(copy.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(55f, 35f), new Vector2(-55f, -35f));

        Button start = CreateButton("Start", contentRoot, "I Agree and Start", Green, 28f);
        Anchor(start.GetComponent<RectTransform>(), new Vector2(0.34f, 0.10f),
            new Vector2(0.66f, 0.23f), Vector2.zero, Vector2.zero);
        start.onClick.AddListener(BeginQuestions);
    }

    private void BeginQuestions()
    {
        currentQuestion = 0;
        ShowQuestion();
    }

    private void ShowQuestion()
    {
        currentView = View.Question;
        ClearContent();
        EvaluationQuestion question = questions[currentQuestion];

        TextMeshProUGUI category = CreateText("Category", contentRoot,
            question.category, 24f, FontStyles.Bold,
            TextAlignmentOptions.Center, Purple);
        Anchor(category.rectTransform, new Vector2(0.28f, 0.89f),
            new Vector2(0.72f, 0.98f), Vector2.zero, Vector2.zero);

        TextMeshProUGUI progress = CreateText("Progress", contentRoot,
            $"Question {currentQuestion + 1} of {questions.Length}", 22f,
            FontStyles.Bold, TextAlignmentOptions.Center, Muted);
        Anchor(progress.rectTransform, new Vector2(0f, 0.82f),
            new Vector2(1f, 0.90f), Vector2.zero, Vector2.zero);

        Image progressTrack = CreateImage("Progress Track", contentRoot,
            new Color32(220, 224, 240, 255), true);
        Anchor(progressTrack.rectTransform, new Vector2(0.14f, 0.79f),
            new Vector2(0.86f, 0.825f), Vector2.zero, Vector2.zero);
        Image progressFill = CreateImage("Progress Fill", progressTrack.transform, Purple, true);
        Anchor(progressFill.rectTransform, Vector2.zero,
            new Vector2((currentQuestion + 1f) / questions.Length, 1f),
            Vector2.zero, Vector2.zero);

        Image questionCard = CreateImage("Question Card", contentRoot,
            new Color32(245, 248, 255, 255), true);
        Anchor(questionCard.rectTransform, new Vector2(0.10f, 0.45f),
            new Vector2(0.90f, 0.75f), Vector2.zero, Vector2.zero);
        TextMeshProUGUI statement = CreateText("Statement", questionCard.transform,
            question.statement, 33f, FontStyles.Bold,
            TextAlignmentOptions.Center, Ink);
        statement.enableWordWrapping = true;
        Stretch(statement.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(60f, 24f), new Vector2(-60f, -24f));

        TextMeshProUGUI instruction = CreateText("Instruction", contentRoot,
            "Choose one answer", 22f, FontStyles.Bold,
            TextAlignmentOptions.Center, Muted);
        Anchor(instruction.rectTransform, new Vector2(0f, 0.39f),
            new Vector2(1f, 0.45f), Vector2.zero, Vector2.zero);

        string[] labels = { "Poor", "Fair", "Good", "Very Good", "Excellent" };
        Color[] colors = { Coral, new Color32(239, 143, 65, 255), Gold, Blue, Green };
        for (int index = 0; index < 5; index++)
        {
            int value = index + 1;
            float minX = 0.08f + index * 0.174f;
            bool selected = ratings[currentQuestion] == value;
            Color color = selected ? colors[index] : Color.Lerp(colors[index], Color.white, 0.44f);
            Button rating = CreateButton("Rating " + value, contentRoot,
                value + "\n" + labels[index], color, selected ? 25f : 22f);
            Anchor(rating.GetComponent<RectTransform>(), new Vector2(minX, 0.18f),
                new Vector2(minX + 0.15f, 0.36f), Vector2.zero, Vector2.zero);
            rating.onClick.AddListener(() => SelectRating(value));
        }

        Button previous = CreateButton("Previous", contentRoot, "Previous",
            new Color32(128, 134, 158, 255), 25f);
        Anchor(previous.GetComponent<RectTransform>(), new Vector2(0.10f, 0.02f),
            new Vector2(0.28f, 0.13f), Vector2.zero, Vector2.zero);
        previous.onClick.AddListener(PreviousQuestion);

        Button next = CreateButton("Next", contentRoot,
            currentQuestion == questions.Length - 1 ? "Review Answers" : "Next",
            ratings[currentQuestion] > 0 ? Purple : new Color32(181, 182, 195, 255), 25f);
        Anchor(next.GetComponent<RectTransform>(), new Vector2(0.72f, 0.02f),
            new Vector2(0.90f, 0.13f), Vector2.zero, Vector2.zero);
        next.interactable = ratings[currentQuestion] > 0;
        next.onClick.AddListener(NextQuestion);
    }

    private void SelectRating(int value)
    {
        ratings[currentQuestion] = Mathf.Clamp(value, 1, 5);
        ShowQuestion();
    }

    private void PreviousQuestion()
    {
        if (currentQuestion <= 0)
            ShowConsent();
        else
        {
            currentQuestion--;
            ShowQuestion();
        }
    }

    private void NextQuestion()
    {
        if (ratings[currentQuestion] <= 0)
            return;
        if (currentQuestion < questions.Length - 1)
        {
            currentQuestion++;
            ShowQuestion();
        }
        else
        {
            ShowReview();
        }
    }

    private void ShowReview()
    {
        currentView = View.Review;
        ClearContent();
        float mean = ratings.Length == 0 ? 0f : (float)ratings.Average();

        TextMeshProUGUI title = CreateText("Title", contentRoot,
            "Review and Submit", 42f, FontStyles.Bold,
            TextAlignmentOptions.Center, Ink);
        Anchor(title.rectTransform, new Vector2(0f, 0.88f), Vector2.one,
            new Vector2(40f, 0f), new Vector2(-40f, 0f));

        Image scoreCard = CreateImage("Score Preview", contentRoot,
            new Color32(239, 245, 255, 255), true);
        Anchor(scoreCard.rectTransform, new Vector2(0.12f, 0.62f),
            new Vector2(0.88f, 0.86f), Vector2.zero, Vector2.zero);
        TextMeshProUGUI score = CreateText("Mean", scoreCard.transform,
            $"Current Mean: {mean:0.00} / 5.00\n{EvaluationScoring.Interpret(mean)}",
            34f, FontStyles.Bold, TextAlignmentOptions.Center, Purple);
        Stretch(score.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(20f, 15f), new Vector2(-20f, -15f));

        TextMeshProUGUI label = CreateText("Comment Label", contentRoot,
            "Optional comment or suggestion", 25f, FontStyles.Bold,
            TextAlignmentOptions.Left, Ink);
        Anchor(label.rectTransform, new Vector2(0.16f, 0.54f),
            new Vector2(0.84f, 0.61f), Vector2.zero, Vector2.zero);

        TMP_InputField input = CreateInputField(contentRoot, draftComments);
        Anchor(input.GetComponent<RectTransform>(), new Vector2(0.16f, 0.25f),
            new Vector2(0.84f, 0.54f), Vector2.zero, Vector2.zero);
        input.onValueChanged.AddListener(value => draftComments = value);

        Button edit = CreateButton("Edit", contentRoot, "Back to Questions",
            new Color32(128, 134, 158, 255), 25f);
        Anchor(edit.GetComponent<RectTransform>(), new Vector2(0.16f, 0.07f),
            new Vector2(0.36f, 0.19f), Vector2.zero, Vector2.zero);
        edit.onClick.AddListener(() =>
        {
            currentQuestion = questions.Length - 1;
            ShowQuestion();
        });

        Button submit = CreateButton("Submit", contentRoot, "Submit Evaluation", Green, 27f);
        Anchor(submit.GetComponent<RectTransform>(), new Vector2(0.64f, 0.07f),
            new Vector2(0.84f, 0.19f), Vector2.zero, Vector2.zero);
        submit.onClick.AddListener(Submit);
    }

    private void Submit()
    {
        if (ratings.Any(rating => rating < 1 || rating > 5))
        {
            currentQuestion = Math.Max(0, Array.FindIndex(ratings, rating => rating == 0));
            ShowQuestion();
            return;
        }

        EvaluationResponseRecord response = EvaluationScoring.CreateResponse(
            instrument, respondentRole, questions, ratings, draftComments);
        if (!LocalDatabase.SaveEvaluationResponse(response))
        {
            ShowSaveError();
            return;
        }

        EvaluationSyncService.RequestSync();
        ShowComplete(response);
    }

    private void ShowComplete(EvaluationResponseRecord response)
    {
        currentView = View.Complete;
        ClearContent();
        TextMeshProUGUI star = CreateText("Star", contentRoot, "★", 92f,
            FontStyles.Bold, TextAlignmentOptions.Center, Gold);
        Anchor(star.rectTransform, new Vector2(0f, 0.72f),
            new Vector2(1f, 0.94f), Vector2.zero, Vector2.zero);

        TextMeshProUGUI title = CreateText("Title", contentRoot,
            "Evaluation Saved!", 47f, FontStyles.Bold,
            TextAlignmentOptions.Center, Green);
        Anchor(title.rectTransform, new Vector2(0f, 0.63f),
            new Vector2(1f, 0.75f), Vector2.zero, Vector2.zero);

        TextMeshProUGUI score = CreateText("Score", contentRoot,
            $"Overall Mean: {response.overall_mean:0.00} / 5.00\n{response.interpretation}",
            35f, FontStyles.Bold, TextAlignmentOptions.Center, Ink);
        Anchor(score.rectTransform, new Vector2(0.15f, 0.40f),
            new Vector2(0.85f, 0.62f), Vector2.zero, Vector2.zero);

        TextMeshProUGUI note = CreateText("Note", contentRoot,
            "The response is stored safely on this device. If the internet is unavailable, it will synchronize automatically later.",
            25f, FontStyles.Normal, TextAlignmentOptions.Center, Muted);
        note.enableWordWrapping = true;
        Anchor(note.rectTransform, new Vector2(0.18f, 0.25f),
            new Vector2(0.82f, 0.40f), Vector2.zero, Vector2.zero);

        Button newEvaluation = CreateButton("New Evaluation", contentRoot,
            "New Evaluation", Purple, 26f);
        Anchor(newEvaluation.GetComponent<RectTransform>(), new Vector2(0.22f, 0.07f),
            new Vector2(0.43f, 0.20f), Vector2.zero, Vector2.zero);
        newEvaluation.onClick.AddListener(ShowRoleSelection);

        Button returnButton = CreateButton("Return", contentRoot,
            "Return to Map", Blue, 26f);
        Anchor(returnButton.GetComponent<RectTransform>(), new Vector2(0.57f, 0.07f),
            new Vector2(0.78f, 0.20f), Vector2.zero, Vector2.zero);
        returnButton.onClick.AddListener(Hide);
    }

    private void ShowSaveError()
    {
        currentView = View.Review;
        ClearContent();
        TextMeshProUGUI error = CreateText("Save Error", contentRoot,
            "The evaluation could not be saved to this device. Please check the local database and try again.",
            34f, FontStyles.Bold, TextAlignmentOptions.Center, Coral);
        error.enableWordWrapping = true;
        Anchor(error.rectTransform, new Vector2(0.15f, 0.35f),
            new Vector2(0.85f, 0.70f), Vector2.zero, Vector2.zero);
        Button retry = CreateButton("Retry", contentRoot, "Try Again", Purple, 27f);
        Anchor(retry.GetComponent<RectTransform>(), new Vector2(0.38f, 0.17f),
            new Vector2(0.62f, 0.30f), Vector2.zero, Vector2.zero);
        retry.onClick.AddListener(ShowReview);
    }

    private void GoBack()
    {
        MusicManager.instance?.PlayBackButtonSound();
        switch (currentView)
        {
            case View.RoleSelection:
                Hide();
                break;
            case View.Consent:
                ShowRoleSelection();
                break;
            case View.Question:
                PreviousQuestion();
                break;
            case View.Review:
                currentQuestion = Math.Max(0, questions.Length - 1);
                ShowQuestion();
                break;
            default:
                Hide();
                break;
        }
    }

    private void UpdateSyncText()
    {
        if (syncText == null)
            return;
        switch (EvaluationSyncService.State)
        {
            case EvaluationSyncState.Uploading:
            case EvaluationSyncState.Authenticating:
                syncText.text = "Saving evaluation securely...";
                break;
            case EvaluationSyncState.Offline:
                syncText.text = "Offline — responses stay saved and will sync later.";
                break;
            case EvaluationSyncState.RetryScheduled:
                syncText.text = "Saved locally — cloud sync will retry.";
                break;
            case EvaluationSyncState.UpToDate:
                syncText.text = "Evaluation responses are up to date.";
                break;
            default:
                syncText.text = "Responses are saved on this device first.";
                break;
        }
    }

    private TMP_InputField CreateInputField(Transform parent, string value)
    {
        Image background = CreateImage("Comments", parent,
            new Color32(247, 248, 252, 255), true);
        TMP_InputField input = background.gameObject.AddComponent<TMP_InputField>();
        input.lineType = TMP_InputField.LineType.MultiLineNewline;
        input.characterLimit = 1000;

        TextMeshProUGUI text = CreateText("Text", background.transform, value,
            24f, FontStyles.Normal, TextAlignmentOptions.TopLeft, Ink);
        text.enableWordWrapping = true;
        Stretch(text.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(24f, 18f), new Vector2(-24f, -18f));

        TextMeshProUGUI placeholder = CreateText("Placeholder", background.transform,
            "Tell us what worked well or what should be improved...",
            23f, FontStyles.Italic, TextAlignmentOptions.TopLeft,
            new Color32(145, 147, 165, 255));
        placeholder.enableWordWrapping = true;
        Stretch(placeholder.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(24f, 18f), new Vector2(-24f, -18f));

        input.textComponent = text;
        input.placeholder = placeholder;
        input.textViewport = background.rectTransform;
        input.text = value;
        return input;
    }

    private void AddStars(Transform parent)
    {
        Vector2[] points =
        {
            new Vector2(0.07f, 0.74f), new Vector2(0.18f, 0.36f),
            new Vector2(0.32f, 0.89f), new Vector2(0.67f, 0.86f),
            new Vector2(0.83f, 0.40f), new Vector2(0.94f, 0.71f)
        };
        for (int index = 0; index < points.Length; index++)
        {
            TextMeshProUGUI star = CreateText("Star " + index, parent, "✦",
                index % 2 == 0 ? 27f : 20f, FontStyles.Bold,
                TextAlignmentOptions.Center, new Color(1f, 0.87f, 0.3f, 0.72f));
            Place(star.rectTransform, points[index], Vector2.zero, new Vector2(42f, 42f));
        }
    }

    private void ClearContent()
    {
        for (int index = contentRoot.childCount - 1; index >= 0; index--)
            Destroy(contentRoot.GetChild(index).gameObject);
    }

    private Button CreateButton(string name, Transform parent, string label, Color color, float fontSize)
    {
        Image image = CreateImage(name, parent, color, true);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
        colors.disabledColor = new Color32(177, 178, 190, 255);
        button.colors = colors;

        TextMeshProUGUI text = CreateText("Label", image.transform, label,
            fontSize, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        Stretch(text.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(8f, 4f), new Vector2(-8f, -4f));
        return button;
    }

    private Image CreateImage(string name, Transform parent, Color color, bool rounded)
    {
        GameObject gameObject = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        gameObject.layer = 5;
        gameObject.transform.SetParent(parent, false);
        Image image = gameObject.GetComponent<Image>();
        image.color = color;
        if (rounded)
        {
            image.sprite = GetRoundedSprite();
            image.type = Image.Type.Sliced;
        }
        return image;
    }

    private TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        string value,
        float size,
        FontStyles style,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject gameObject = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        gameObject.layer = 5;
        gameObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>();
        if (font != null)
            text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static Sprite GetRoundedSprite()
    {
        if (roundedSprite != null)
            return roundedSprite;
        const int size = 64;
        const float radius = 15f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Evaluation Rounded UI",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nearX = Mathf.Clamp(x, radius, size - 1f - radius);
                float nearY = Mathf.Clamp(y, radius, size - 1f - radius);
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(nearX, nearY));
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(radius + 0.5f - distance) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        roundedSprite = Sprite.Create(texture, new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
            new Vector4(16f, 16f, 16f, 16f));
        return roundedSprite;
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
