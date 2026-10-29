using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds the Phase 6C report as a child-friendly game screen. All values come
/// from the locally cached Supabase validation view, so the report also works
/// offline and never needs a server key or an exported file.
/// </summary>
public sealed class ForecastValidationReport : MonoBehaviour
{
    private const string SceneName = "ForecastValidationReport";
    private static readonly Color Ink = new Color32(37, 38, 74, 255);
    private static readonly Color Muted = new Color32(92, 94, 126, 255);
    private static readonly Color Purple = new Color32(117, 72, 211, 255);
    private static readonly Color Blue = new Color32(55, 162, 232, 255);
    private static readonly Color Gold = new Color32(255, 190, 51, 255);
    private static readonly Color Green = new Color32(61, 190, 126, 255);
    private static readonly Color Coral = new Color32(244, 105, 102, 255);
    private static readonly Color Paper = new Color32(255, 252, 242, 255);

    private static Sprite roundedSprite;
    private static Texture2D backgroundTexture;

    private TMP_FontAsset font;
    private TextMeshProUGUI statusText;
    private TextMeshProUGUI sampleValue;
    private TextMeshProUGUI maeValue;
    private TextMeshProUGUI accuracyValue;
    private TextMeshProUGUI progressValue;
    private TextMeshProUGUI progressCaption;
    private TextMeshProUGUI measurementText;
    private TextMeshProUGUI chartTitle;
    private RectTransform chartContent;
    private bool built;

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
        if (!scene.IsValid() || !string.Equals(scene.name, SceneName, StringComparison.Ordinal))
            return;
        if (FindObjectOfType<ForecastValidationReport>() != null)
            return;

        EnsureCamera();
        EnsureEventSystem();
        GameObject host = new GameObject("ForecastValidationReportRuntime");
        host.AddComponent<ForecastValidationReport>().Build();
    }

    private static void EnsureCamera()
    {
        if (Camera.main != null)
            return;

        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(18, 20, 69, 255);
        camera.orthographic = true;
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
            return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private void OnEnable()
    {
        SupabaseForecastService.StatusChanged += HandleDataChanged;
        SupabaseForecastService.ForecastsChanged += HandleDataChanged;
    }

    private void OnDisable()
    {
        SupabaseForecastService.StatusChanged -= HandleDataChanged;
        SupabaseForecastService.ForecastsChanged -= HandleDataChanged;
    }

    private void Build()
    {
        if (built)
            return;
        built = true;
        font = TMP_Settings.defaultFontAsset;

        GameObject canvasObject = new GameObject(
            "Forecast Report Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RawImage background = CreateRawImage("Starlight Background", canvasObject.transform, CreateBackgroundTexture());
        Stretch(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        Image glow = CreateImage("Soft Glow", canvasObject.transform, new Color(0.24f, 0.13f, 0.55f, 0.26f), false);
        Stretch(glow.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        AddTwinklingStars(canvasObject.transform);

        Image header = CreateImage("Header", canvasObject.transform, new Color(0.07f, 0.08f, 0.25f, 0.92f), false);
        Anchor(header.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -158f), Vector2.zero);

        Button back = CreateButton("Back", canvasObject.transform, "Back", new Color32(247, 139, 34, 255));
        Place(back.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(125f, -78f), new Vector2(190f, 78f));
        back.onClick.AddListener(BackToForecast);

        Button refresh = CreateButton("Refresh", canvasObject.transform, "Refresh", Blue);
        Place(refresh.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(-125f, -78f), new Vector2(190f, 78f));
        refresh.onClick.AddListener(Refresh);

        TextMeshProUGUI title = CreateText("Title", canvasObject.transform,
            "Forecast Adventure Report", 50f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(260f, -88f), new Vector2(-260f, -22f));

        statusText = CreateText("Status", canvasObject.transform,
            "Loading your learning journey...", 23f, FontStyles.Normal,
            TextAlignmentOptions.Center, new Color32(224, 224, 255, 255));
        Anchor(statusText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(350f, -137f), new Vector2(-350f, -96f));

        CreateMetricCard(canvasObject.transform, "Predictions Checked", "star", Purple,
            0, out sampleValue, out _);
        CreateMetricCard(canvasObject.transform, "Average Score Difference", "target", Coral,
            1, out maeValue, out _);
        CreateMetricCard(canvasObject.transform, "Forecast Accuracy", "sparkle", Blue,
            2, out accuracyValue, out _);
        CreateMetricCard(canvasObject.transform, "Learning Journey", "wand", Green,
            3, out progressValue, out progressCaption);

        Image chartPanel = CreateCard("Prediction Journey Panel", canvasObject.transform, Paper);
        Anchor(chartPanel.rectTransform, new Vector2(0f, 0f), new Vector2(0.68f, 1f),
            new Vector2(42f, 42f), new Vector2(-15f, -350f));

        chartTitle = CreateText("Chart Title", chartPanel.transform,
            "Prediction Journey", 33f, FontStyles.Bold, TextAlignmentOptions.TopLeft, Ink);
        Anchor(chartTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(34f, -60f), new Vector2(-34f, -16f));

        CreateLegend(chartPanel.transform, "Forecast", Gold, new Vector2(0f, 1f), new Vector2(42f, -92f));
        CreateLegend(chartPanel.transform, "Actual Score", Blue, new Vector2(0f, 1f), new Vector2(220f, -92f));

        GameObject chartObject = new GameObject("Chart Content", typeof(RectTransform));
        chartObject.layer = 5;
        chartObject.transform.SetParent(chartPanel.transform, false);
        chartContent = chartObject.GetComponent<RectTransform>();
        Anchor(chartContent, Vector2.zero, Vector2.one,
            new Vector2(28f, 30f), new Vector2(-28f, -118f));

        Image detailsPanel = CreateCard("How It Is Measured", canvasObject.transform, new Color32(247, 241, 255, 255));
        Anchor(detailsPanel.rectTransform, new Vector2(0.68f, 0f), Vector2.one,
            new Vector2(15f, 42f), new Vector2(-42f, -350f));

        TextMeshProUGUI detailsTitle = CreateText("Details Title", detailsPanel.transform,
            "How It Is Measured", 32f, FontStyles.Bold, TextAlignmentOptions.TopLeft, Purple);
        Anchor(detailsTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(34f, -62f), new Vector2(-28f, -18f));

        measurementText = CreateText("Details", detailsPanel.transform, string.Empty,
            24f, FontStyles.Normal, TextAlignmentOptions.TopLeft, Ink);
        measurementText.enableWordWrapping = true;
        measurementText.lineSpacing = 9f;
        Anchor(measurementText.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(34f, 30f), new Vector2(-28f, -82f));

        Render();
        SupabaseForecastService.RequestRefresh();
    }

    private void CreateMetricCard(
        Transform parent,
        string label,
        string icon,
        Color accent,
        int index,
        out TextMeshProUGUI valueText,
        out TextMeshProUGUI captionText)
    {
        float left = 42f + index * 459f;
        Image shadow = CreateCard(label + " Shadow", parent, new Color(0f, 0f, 0f, 0.18f));
        Anchor(shadow.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(left + 7f, -333f), new Vector2(left + 429f, -181f));

        Image card = CreateCard(label, parent, Color.white);
        Anchor(card.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(left, -325f), new Vector2(left + 422f, -173f));

        Image badge = CreateImage("Icon Badge", card.transform, accent, true);
        Place(badge.rectTransform, new Vector2(0f, 0.5f), new Vector2(68f, 0f), new Vector2(92f, 92f));
        TextMeshProUGUI iconText = CreateText("Icon", badge.transform, IconGlyph(icon), 42f,
            FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        Stretch(iconText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        TextMeshProUGUI labelText = CreateText("Label", card.transform, label, 21f,
            FontStyles.Bold, TextAlignmentOptions.TopLeft, Muted);
        Anchor(labelText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(128f, -58f), new Vector2(-18f, -19f));

        valueText = CreateText("Value", card.transform, "--", 40f,
            FontStyles.Bold, TextAlignmentOptions.TopLeft, accent);
        Anchor(valueText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(128f, 28f), new Vector2(-18f, -62f));

        captionText = CreateText("Caption", card.transform, string.Empty, 17f,
            FontStyles.Normal, TextAlignmentOptions.BottomLeft, Muted);
        Anchor(captionText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(128f, 12f), new Vector2(-18f, 38f));
    }

    private void Render()
    {
        if (!built || chartContent == null)
            return;

        string playerId = LocalDatabase.GetOrCreateCurrentPlayerId();
        ForecastValidationRecord[] rows = LocalDatabase.GetCachedForecastValidation(playerId);
        ForecastValidationSummary summary = ForecastValidationCalculator.Calculate(rows);

        sampleValue.text = summary.sample_count.ToString(CultureInfo.InvariantCulture);
        maeValue.text = summary.sample_count == 0 ? "--" : $"{summary.mae:0.0} pts";
        accuracyValue.text = summary.sample_count == 0 ? "--" : $"{summary.accuracy_percent:0}%";
        progressValue.text = ProgressDisplay(summary.progress_label);
        progressCaption.text = ProgressCaption(summary.progress_label);
        UpdateStatus(rows.Length);
        RenderChart(rows);
        RenderMeasurementSummary(summary);
    }

    private void UpdateStatus(int count)
    {
        if (SupabaseForecastService.State == ForecastSyncState.Downloading ||
            SupabaseForecastService.State == ForecastSyncState.Authenticating)
        {
            statusText.text = "Checking for new learning results...";
        }
        else if (count == 0 && !string.IsNullOrWhiteSpace(SupabaseForecastService.LastValidationError))
        {
            statusText.text = "The report is waiting for its secure cloud setup.";
        }
        else if (SupabaseForecastService.State == ForecastSyncState.Offline)
        {
            statusText.text = "Offline — showing the latest saved adventure report.";
        }
        else if (count == 0)
        {
            statusText.text = "Complete a scored activity after a forecast to unlock this report.";
        }
        else
        {
            statusText.text = "Great work! Your forecast has real scores to compare.";
        }
    }

    private void RenderChart(ForecastValidationRecord[] allRows)
    {
        for (int index = chartContent.childCount - 1; index >= 0; index--)
            Destroy(chartContent.GetChild(index).gameObject);

        ForecastValidationRecord[] rows = allRows
            .Skip(Math.Max(0, allRows.Length - 6))
            .ToArray();
        if (rows.Length == 0)
        {
            chartTitle.text = "Prediction Journey";
            Image messageCard = CreateCard("Empty Report", chartContent, new Color32(239, 245, 255, 255));
            Anchor(messageCard.rectTransform, new Vector2(0.12f, 0.18f), new Vector2(0.88f, 0.82f),
                Vector2.zero, Vector2.zero);
            TextMeshProUGUI star = CreateText("Star", messageCard.transform, "★", 76f,
                FontStyles.Bold, TextAlignmentOptions.Center, Gold);
            Anchor(star.rectTransform, new Vector2(0f, 0.53f), new Vector2(1f, 1f),
                new Vector2(0f, 0f), new Vector2(0f, -6f));
            TextMeshProUGUI message = CreateText("Message", messageCard.transform,
                "No validated forecast samples yet\nPlay one scored activity after a forecast, then come back!",
                27f, FontStyles.Bold, TextAlignmentOptions.Center, Ink);
            message.enableWordWrapping = true;
            Anchor(message.rectTransform, Vector2.zero, new Vector2(1f, 0.6f),
                new Vector2(30f, 20f), new Vector2(-30f, 0f));
            return;
        }

        chartTitle.text = rows.Length < allRows.Length
            ? "Prediction Journey — Latest 6"
            : "Prediction Journey";

        float groupWidth = 1f / rows.Length;
        for (int index = 0; index < rows.Length; index++)
        {
            ForecastValidationRecord row = rows[index];
            float center = groupWidth * (index + 0.5f);
            RectTransform group = CreateRect("Sample " + index, chartContent);
            group.anchorMin = new Vector2(center, 0f);
            group.anchorMax = new Vector2(center, 1f);
            group.pivot = new Vector2(0.5f, 0f);
            group.anchoredPosition = Vector2.zero;
            group.sizeDelta = new Vector2(Mathf.Min(150f, 900f / rows.Length), 0f);

            CreateBar(group, "Forecast", -29f, row.predicted_score_percent, Gold);
            CreateBar(group, "Actual", 29f, row.actual_score_percent, Blue);

            TextMeshProUGUI skill = CreateText("Skill", group, ShortSkill(row.skill), 17f,
                FontStyles.Bold, TextAlignmentOptions.Center, Ink);
            Anchor(skill.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(-10f, 9f), new Vector2(10f, 40f));

            TextMeshProUGUI date = CreateText("Date", group, ShortDate(row.actual_at), 14f,
                FontStyles.Normal, TextAlignmentOptions.Center, Muted);
            Anchor(date.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(-10f, 38f), new Vector2(10f, 64f));
        }
    }

    private void CreateBar(RectTransform parent, string name, float x, float value, Color color)
    {
        float clamped = Mathf.Clamp(value, 0f, 100f);
        float height = 40f + clamped * 2.55f;
        Image bar = CreateImage(name + " Bar", parent, color, true);
        RectTransform rect = bar.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(x, 72f);
        rect.sizeDelta = new Vector2(48f, height);

        TextMeshProUGUI score = CreateText(name + " Score", parent, $"{clamped:0}", 16f,
            FontStyles.Bold, TextAlignmentOptions.Center, Ink);
        RectTransform scoreRect = score.rectTransform;
        scoreRect.anchorMin = new Vector2(0.5f, 0f);
        scoreRect.anchorMax = new Vector2(0.5f, 0f);
        scoreRect.pivot = new Vector2(0.5f, 0f);
        scoreRect.anchoredPosition = new Vector2(x, 78f + height);
        scoreRect.sizeDelta = new Vector2(58f, 26f);
    }

    private void RenderMeasurementSummary(ForecastValidationSummary summary)
    {
        if (summary.sample_count == 0)
        {
            measurementText.text =
                "MAE\nAverage score difference\n\n" +
                "RMSE\nHighlights bigger misses\n\n" +
                "Samples\n0 predictions checked\n\n" +
                "Model\nARIMA\n\n" +
                "A forecast needs a later real score before it can join this report.";
            return;
        }

        string baseline;
        if (summary.baseline_sample_count == 0)
        {
            baseline = "Baseline\nWaiting for enough previous scores";
        }
        else
        {
            float difference = summary.baseline_mae - summary.mae;
            baseline = difference > 0.05f
                ? $"Baseline\nARIMA is {difference:0.0} points closer"
                : difference < -0.05f
                    ? $"Baseline\nLast score is {-difference:0.0} points closer"
                    : "Baseline\nARIMA and last score are tied";
        }

        measurementText.text =
            $"MAE\n{summary.mae:0.00} average difference\n\n" +
            $"RMSE\n{summary.rmse:0.00} big-miss check\n\n" +
            $"Samples\n{summary.sample_count} predictions checked\n\n" +
            $"Model\n{ModelDisplay(summary.model_version)}\n\n" +
            baseline;
    }

    private void Refresh()
    {
        MusicManager.instance?.PlayGameSelectSound();
        SupabaseForecastService.RequestRefresh();
        Render();
    }

    private void BackToForecast()
    {
        MusicManager.instance?.PlayBackButtonSound();
        PlayerPrefs.SetInt("OpenForecastDashboard", 1);
        PlayerPrefs.Save();
        SceneManager.LoadScene("AdventureMap");
    }

    private void HandleDataChanged()
    {
        if (this != null && isActiveAndEnabled)
            Render();
    }

    private void CreateLegend(Transform parent, string label, Color color, Vector2 anchor, Vector2 position)
    {
        Image swatch = CreateImage(label + " Swatch", parent, color, true);
        Place(swatch.rectTransform, anchor, position, new Vector2(25f, 25f));
        TextMeshProUGUI text = CreateText(label + " Legend", parent, label, 19f,
            FontStyles.Bold, TextAlignmentOptions.Left, Muted);
        RectTransform rect = text.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = position + new Vector2(22f, 0f);
        rect.sizeDelta = new Vector2(155f, 32f);
    }

    private void AddTwinklingStars(Transform parent)
    {
        Vector2[] positions =
        {
            new Vector2(0.08f, 0.76f), new Vector2(0.18f, 0.42f),
            new Vector2(0.32f, 0.91f), new Vector2(0.49f, 0.56f),
            new Vector2(0.66f, 0.86f), new Vector2(0.79f, 0.47f),
            new Vector2(0.91f, 0.72f), new Vector2(0.96f, 0.24f)
        };

        for (int index = 0; index < positions.Length; index++)
        {
            TextMeshProUGUI star = CreateText("Twinkle " + index, parent, "✦",
                index % 2 == 0 ? 29f : 20f, FontStyles.Bold,
                TextAlignmentOptions.Center, new Color(1f, 0.88f, 0.35f, 0.72f));
            Place(star.rectTransform, positions[index], Vector2.zero, new Vector2(42f, 42f));
            ForecastReportTwinkle twinkle = star.gameObject.AddComponent<ForecastReportTwinkle>();
            twinkle.phase = index * 0.63f;
        }
    }

    private Button CreateButton(string name, Transform parent, string label, Color color)
    {
        Image image = CreateImage(name, parent, color, true);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
        button.colors = colors;

        TextMeshProUGUI text = CreateText("Label", image.transform, label, 27f,
            FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 5f), new Vector2(-10f, -5f));
        return button;
    }

    private Image CreateCard(string name, Transform parent, Color color)
    {
        Image image = CreateImage(name, parent, color, true);
        return image;
    }

    private Image CreateImage(string name, Transform parent, Color color, bool rounded)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
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

    private RawImage CreateRawImage(string name, Transform parent, Texture texture)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        gameObject.layer = 5;
        gameObject.transform.SetParent(parent, false);
        RawImage image = gameObject.GetComponent<RawImage>();
        image.texture = texture;
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
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
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

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.layer = 5;
        gameObject.transform.SetParent(parent, false);
        return gameObject.GetComponent<RectTransform>();
    }

    private static Sprite GetRoundedSprite()
    {
        if (roundedSprite != null)
            return roundedSprite;

        const int size = 64;
        const float radius = 15f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Runtime Rounded UI",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nearestX = Mathf.Clamp(x, radius, size - 1f - radius);
                float nearestY = Mathf.Clamp(y, radius, size - 1f - radius);
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(nearestX, nearestY));
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(radius + 0.5f - distance) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        roundedSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(16f, 16f, 16f, 16f));
        roundedSprite.name = "Runtime Rounded UI Sprite";
        return roundedSprite;
    }

    private static Texture2D CreateBackgroundTexture()
    {
        if (backgroundTexture != null)
            return backgroundTexture;

        const int width = 96;
        const int height = 54;
        backgroundTexture = new Texture2D(width, height, TextureFormat.RGB24, false)
        {
            name = "Forecast Adventure Sky",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        Color top = new Color32(31, 25, 102, 255);
        Color bottom = new Color32(13, 50, 103, 255);
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            Color row = Color.Lerp(bottom, top, y / (height - 1f));
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = row;
        }

        System.Random random = new System.Random(617);
        for (int index = 0; index < 80; index++)
        {
            int x = random.Next(1, width - 1);
            int y = random.Next(1, height - 1);
            float glow = (float)(0.6 + random.NextDouble() * 0.4);
            pixels[y * width + x] = Color.Lerp(pixels[y * width + x], Color.white, glow);
        }
        backgroundTexture.SetPixels(pixels);
        backgroundTexture.Apply(false, true);
        return backgroundTexture;
    }

    private static string IconGlyph(string icon)
    {
        switch (icon)
        {
            case "target": return "◎";
            case "sparkle": return "✦";
            case "wand": return "↗";
            default: return "★";
        }
    }

    private static string ProgressDisplay(string value)
    {
        if (string.Equals(value, "improving", StringComparison.OrdinalIgnoreCase))
            return "Improving!";
        if (string.Equals(value, "steady", StringComparison.OrdinalIgnoreCase))
            return "Steady";
        return "Keep Practicing";
    }

    private static string ProgressCaption(string value)
    {
        if (string.Equals(value, "improving", StringComparison.OrdinalIgnoreCase))
            return "Forecasts are getting closer";
        if (string.Equals(value, "steady", StringComparison.OrdinalIgnoreCase))
            return "Results are staying consistent";
        return "More adventures make it smarter";
    }

    private static string ModelDisplay(string value)
    {
        return string.Equals(value, "arima-session-v1", StringComparison.OrdinalIgnoreCase)
            ? "ARIMA session model v1"
            : string.IsNullOrWhiteSpace(value) ? "ARIMA" : value;
    }

    private static string ShortSkill(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Skill";
        string normalized = value.Replace('_', ' ').Replace('-', ' ');
        return normalized.Length <= 13 ? normalized : normalized.Substring(0, 12) + "…";
    }

    private static string ShortDate(string value)
    {
        return DateTime.TryParse(value, null, DateTimeStyles.RoundtripKind, out DateTime date)
            ? date.ToLocalTime().ToString("MMM d")
            : string.Empty;
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

internal sealed class ForecastReportTwinkle : MonoBehaviour
{
    public float phase;
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void Update()
    {
        float wave = (Mathf.Sin(Time.unscaledTime * 1.7f + phase) + 1f) * 0.5f;
        canvasGroup.alpha = Mathf.Lerp(0.38f, 0.92f, wave);
        transform.localScale = Vector3.one * Mathf.Lerp(0.86f, 1.08f, wave);
    }
}
