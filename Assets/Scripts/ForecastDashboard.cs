using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Adds an accessible forecast dashboard to the Adventure Map without requiring
/// scene-specific object wiring. The panel renders cached data immediately and
/// requests a background refresh when opened.
/// </summary>
public sealed class ForecastDashboard : MonoBehaviour
{
    private static readonly Color Navy = new Color32(15, 36, 73, 255);
    private static readonly Color Blue = new Color32(55, 125, 207, 255);
    private static readonly Color Cream = new Color32(250, 247, 237, 255);
    private static readonly Color TextDark = new Color32(30, 42, 63, 255);
    private static readonly Color Muted = new Color32(91, 104, 124, 255);

    private GameObject overlay;
    private RectTransform cardsRoot;
    private TextMeshProUGUI statusText;
    private TMP_FontAsset fontAsset;
    private bool built;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        InstallIntoScene(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InstallIntoScene(scene);
    }

    private static void InstallIntoScene(Scene scene)
    {
        if (!scene.IsValid() || !string.Equals(scene.name, "AdventureMap", StringComparison.Ordinal))
            return;

        Canvas canvas = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            canvas = root.GetComponentInChildren<Canvas>(includeInactive: true);
            if (canvas != null)
                break;
        }

        if (canvas == null || canvas.transform.Find("ForecastDashboardRuntime") != null)
            return;

        GameObject host = new GameObject("ForecastDashboardRuntime", typeof(RectTransform));
        host.layer = 5;
        host.transform.SetParent(canvas.transform, false);
        ForecastDashboard dashboard = host.AddComponent<ForecastDashboard>();
        dashboard.Build(canvas);
    }

    private void OnEnable()
    {
        SupabaseForecastService.StatusChanged += HandleForecastStatusChanged;
        SupabaseForecastService.ForecastsChanged += HandleForecastStatusChanged;
    }

    private void OnDisable()
    {
        SupabaseForecastService.StatusChanged -= HandleForecastStatusChanged;
        SupabaseForecastService.ForecastsChanged -= HandleForecastStatusChanged;
    }

    private void Build(Canvas canvas)
    {
        if (built)
            return;
        built = true;

        TextMeshProUGUI existingText = canvas.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
        fontAsset = existingText != null ? existingText.font : TMP_Settings.defaultFontAsset;

        Button launcher = CreateButton(
            "ForecastButton",
            canvas.transform,
            "Learning Forecast",
            Blue,
            new Vector2(1f, 1f),
            new Vector2(-500f, -52f),
            new Vector2(280f, 58f));
        launcher.onClick.AddListener(Show);

        overlay = CreatePanel(
            "ForecastOverlay",
            canvas.transform,
            new Color(0.02f, 0.04f, 0.1f, 0.86f));
        Stretch(overlay.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        GameObject window = CreatePanel("ForecastWindow", overlay.transform, Cream);
        RectTransform windowRect = window.GetComponent<RectTransform>();
        SetCentered(windowRect, new Vector2(1500f, 860f));

        TextMeshProUGUI title = CreateText(
            "Title", window.transform, "Learning Forecast", 48f, FontStyles.Bold,
            TextAlignmentOptions.Center, Navy);
        Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(40f, -100f), new Vector2(-40f, -22f));

        statusText = CreateText(
            "Status", window.transform, "Loading cached forecast...", 24f,
            FontStyles.Normal, TextAlignmentOptions.Center, Muted);
        Anchor(statusText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(250f, -148f), new Vector2(-250f, -98f));

        Button refreshButton = CreateButton(
            "RefreshButton", window.transform, "Refresh", Blue,
            new Vector2(0f, 1f), new Vector2(125f, -74f), new Vector2(190f, 62f));
        refreshButton.onClick.AddListener(Refresh);

        Button closeButton = CreateButton(
            "CloseButton", window.transform, "Close", new Color32(222, 91, 91, 255),
            new Vector2(1f, 1f), new Vector2(-125f, -74f), new Vector2(190f, 62f));
        closeButton.onClick.AddListener(Hide);

        Button reportButton = CreateButton(
            "AccuracyReportButton", window.transform, "View Forecast Accuracy",
            new Color32(130, 83, 201, 255),
            new Vector2(0.5f, 0f), new Vector2(0f, 67f), new Vector2(390f, 70f));
        reportButton.onClick.AddListener(OpenAccuracyReport);

        GameObject viewport = CreatePanel("Viewport", window.transform, new Color(1f, 1f, 1f, 0.18f));
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        Anchor(viewportRect, Vector2.zero, Vector2.one,
            new Vector2(48f, 120f), new Vector2(-48f, -165f));
        Mask mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject content = new GameObject("Cards", typeof(RectTransform),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.layer = 5;
        content.transform.SetParent(viewport.transform, false);
        cardsRoot = content.GetComponent<RectTransform>();
        cardsRoot.anchorMin = new Vector2(0f, 1f);
        cardsRoot.anchorMax = new Vector2(1f, 1f);
        cardsRoot.pivot = new Vector2(0.5f, 1f);
        cardsRoot.anchoredPosition = Vector2.zero;
        cardsRoot.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 18, 18);
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = viewport.AddComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = cardsRoot;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 36f;

        overlay.SetActive(false);
        if (PlayerPrefs.GetInt("OpenForecastDashboard", 0) == 1)
        {
            PlayerPrefs.SetInt("OpenForecastDashboard", 0);
            PlayerPrefs.Save();
            Show();
        }
    }

    private void Show()
    {
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
        Render();
        SupabaseForecastService.RequestRefresh();
    }

    private void Hide()
    {
        overlay.SetActive(false);
    }

    private void Refresh()
    {
        SupabaseForecastService.RequestRefresh();
        Render();
    }

    private void OpenAccuracyReport()
    {
        MusicManager.instance?.PlayGameSelectSound();
        SceneManager.LoadScene("ForecastValidationReport");
    }

    private void HandleForecastStatusChanged()
    {
        if (overlay != null && overlay.activeSelf)
            Render();
    }

    private void Render()
    {
        if (cardsRoot == null || statusText == null)
            return;

        UpdateStatusText();
        for (int index = cardsRoot.childCount - 1; index >= 0; index--)
            Destroy(cardsRoot.GetChild(index).gameObject);

        string playerId = LocalDatabase.GetOrCreateCurrentPlayerId();
        LearnerForecastRecord[] forecasts = LocalDatabase.GetCachedForecasts(playerId);
        ForecastReadinessRecord[] readiness = LocalDatabase.GetCachedForecastReadiness(playerId);

        var forecastBySkill = new Dictionary<string, LearnerForecastRecord>(StringComparer.OrdinalIgnoreCase);
        var readinessBySkill = new Dictionary<string, ForecastReadinessRecord>(StringComparer.OrdinalIgnoreCase);
        var skills = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (LearnerForecastRecord row in forecasts)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.skill))
                continue;
            forecastBySkill[row.skill] = row;
            skills.Add(row.skill);
        }

        foreach (ForecastReadinessRecord row in readiness)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.skill))
                continue;
            readinessBySkill[row.skill] = row;
            skills.Add(row.skill);
        }

        if (skills.Count == 0)
        {
            CreateMessageCard(
                "No learning data is ready yet. Complete activities while online, " +
                "then return here after cloud sync.");
            return;
        }

        foreach (string skill in skills)
        {
            if (forecastBySkill.TryGetValue(skill, out LearnerForecastRecord forecast))
                CreateForecastCard(forecast);
            else if (readinessBySkill.TryGetValue(skill, out ForecastReadinessRecord progress))
                CreateReadinessCard(progress);
        }

        Canvas.ForceUpdateCanvases();
    }

    private void UpdateStatusText()
    {
        switch (SupabaseForecastService.State)
        {
            case ForecastSyncState.Authenticating:
                statusText.text = "Signing in securely...";
                break;
            case ForecastSyncState.Downloading:
                statusText.text = "Refreshing forecast...";
                break;
            case ForecastSyncState.Offline:
                statusText.text = "Offline — showing the latest saved forecast.";
                break;
            case ForecastSyncState.RetryScheduled:
                statusText.text = "Could not refresh yet — saved forecast remains available.";
                break;
            case ForecastSyncState.Ready:
                statusText.text = string.IsNullOrEmpty(SupabaseForecastService.LastSuccessfulRefreshUtc)
                    ? "Forecast is up to date."
                    : "Updated " + FormatLocalTime(SupabaseForecastService.LastSuccessfulRefreshUtc);
                break;
            case ForecastSyncState.Disabled:
                statusText.text = "Cloud forecast is unavailable. Check Supabase configuration.";
                break;
            default:
                statusText.text = "Loading forecast...";
                break;
        }
    }

    private void CreateForecastCard(LearnerForecastRecord forecast)
    {
        bool expired = DateTime.TryParse(forecast.valid_until, null,
            System.Globalization.DateTimeStyles.RoundtripKind, out DateTime validUntil) &&
            validUntil.ToUniversalTime() <= DateTime.UtcNow;

        Color accent = RiskColor(forecast.risk_level);
        StringBuilder body = new StringBuilder();
        body.Append("Model: ").Append(ModelDisplayName(forecast.model_version));
        body.Append("   Learning data: ").Append(forecast.source_attempt_count).Append(" attempts\n");
        body.Append("Expected score: ").Append(Mathf.RoundToInt(forecast.expected_score_percent)).Append("%   ");
        body.Append("Mastery: ").Append(Mathf.RoundToInt(forecast.mastery_probability * 100f)).Append("%   ");
        body.Append("Confidence: ").Append(Mathf.RoundToInt(forecast.confidence * 100f)).Append("%\n");
        body.Append("Trend: ").Append(ToDisplayName(forecast.trend_label));
        body.Append("   Risk: ").Append(ToDisplayName(forecast.risk_level));
        body.Append("   Recommended level: ").Append(forecast.recommended_level).Append("\n");
        body.Append(forecast.recommendation);
        if (expired)
            body.Append("\nThis saved forecast has expired; connect online to refresh it.");

        CreateCard(ToDisplayName(forecast.skill), body.ToString(), accent, 255f);
    }

    private void CreateReadinessCard(ForecastReadinessRecord readiness)
    {
        string message;
        if (readiness.is_forecast_eligible)
        {
            message = "Enough learning data is available. The forecast will appear after the next update.";
        }
        else if (readiness.attempts_needed > 0)
        {
            message = $"Complete {readiness.attempts_needed} more attempt" +
                      (readiness.attempts_needed == 1 ? string.Empty : "s") +
                      " to unlock forecasting.";
        }
        else if (readiness.sessions_needed > 0)
        {
            message = $"Complete {readiness.sessions_needed} more scored session" +
                      (readiness.sessions_needed == 1 ? string.Empty : "s") +
                      " to unlock ARIMA forecasting.";
        }
        else
        {
            message = "Keep practicing to unlock forecasting.";
        }

        string details = $"Attempts: {readiness.total_attempts}/10   " +
                         $"Scored sessions: {readiness.forecast_session_count}/5\n" +
                         message;
        CreateCard(ToDisplayName(readiness.skill), details,
            new Color32(235, 166, 54, 255), 180f);
    }

    private void CreateMessageCard(string message)
    {
        CreateCard("Forecast not available yet", message,
            new Color32(235, 166, 54, 255), 180f);
    }

    private static string ModelDisplayName(string modelVersion)
    {
        return string.Equals(modelVersion, "arima-session-v1", StringComparison.OrdinalIgnoreCase)
            ? "ARIMA"
            : ToDisplayName(modelVersion);
    }

    private void CreateCard(string heading, string body, Color accent, float height)
    {
        GameObject card = CreatePanel("ForecastCard", cardsRoot, Color.white);
        LayoutElement element = card.AddComponent<LayoutElement>();
        element.preferredHeight = height;
        element.minHeight = height;

        GameObject stripe = CreatePanel("Accent", card.transform, accent);
        Anchor(stripe.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 1f),
            Vector2.zero, new Vector2(12f, 0f));

        TextMeshProUGUI headingText = CreateText(
            "Heading", card.transform, heading, 32f, FontStyles.Bold,
            TextAlignmentOptions.TopLeft, TextDark);
        Anchor(headingText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(34f, -62f), new Vector2(-24f, -16f));

        TextMeshProUGUI bodyText = CreateText(
            "Details", card.transform, body, 25f, FontStyles.Normal,
            TextAlignmentOptions.TopLeft, Muted);
        bodyText.enableWordWrapping = true;
        bodyText.overflowMode = TextOverflowModes.Ellipsis;
        Anchor(bodyText.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(34f, 20f), new Vector2(-24f, -72f));
    }

    private Button CreateButton(
        string objectName,
        Transform parent,
        string label,
        Color color,
        Vector2 anchor,
        Vector2 position,
        Vector2 size)
    {
        GameObject buttonObject = CreatePanel(objectName, parent, color);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonObject.GetComponent<Image>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.15f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.15f);
        button.colors = colors;

        TextMeshProUGUI text = CreateText(
            "Label", buttonObject.transform, label, 27f, FontStyles.Bold,
            TextAlignmentOptions.Center, Color.white);
        Stretch(text.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(8f, 4f), new Vector2(-8f, -4f));
        return button;
    }

    private GameObject CreatePanel(string objectName, Transform parent, Color color)
    {
        GameObject panel = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.layer = 5;
        panel.transform.SetParent(parent, false);
        panel.GetComponent<Image>().color = color;
        return panel;
    }

    private TextMeshProUGUI CreateText(
        string objectName,
        Transform parent,
        string value,
        float fontSize,
        FontStyles style,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.layer = 5;
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null)
            text.font = fontAsset;
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void Anchor(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void SetCentered(RectTransform rect, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }

    private static Color RiskColor(string risk)
    {
        if (string.Equals(risk, "high", StringComparison.OrdinalIgnoreCase))
            return new Color32(222, 91, 91, 255);
        if (string.Equals(risk, "medium", StringComparison.OrdinalIgnoreCase))
            return new Color32(235, 166, 54, 255);
        return new Color32(72, 174, 120, 255);
    }

    private static string ToDisplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Learning skill";

        string normalized = value.Replace('_', ' ').Replace('-', ' ').Trim();
        StringBuilder result = new StringBuilder(normalized.Length + 8);
        for (int index = 0; index < normalized.Length; index++)
        {
            char character = normalized[index];
            if (index > 0 && char.IsUpper(character) && normalized[index - 1] != ' ')
                result.Append(' ');
            result.Append(character);
        }

        string text = result.ToString();
        return char.ToUpperInvariant(text[0]) + text.Substring(1);
    }

    private static string FormatLocalTime(string utcText)
    {
        if (!DateTime.TryParse(utcText, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out DateTime value))
        {
            return "recently";
        }

        return value.ToLocalTime().ToString("MMM d, h:mm tt");
    }
}

