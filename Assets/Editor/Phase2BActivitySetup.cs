using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class Phase2BActivitySetup
{
    private const string CatalogPath = "Assets/Data/QuestionBankCatalog.asset";
    private const string BankFolder = "Assets/Data/QuestionBanks";
    private const string AdventureMapPath = "Assets/Scenes/AdventureMap.unity";
    private const string PatternScenePath = "Assets/Scenes/PatternPlains.unity";
    private const string MatchingScenePath = "Assets/Scenes/ObjectMatching.unity";
    private const string PatternButtonArtPath = "Assets/Sprites/Design/Pattern Plains.png";
    private const string MatchingButtonArtPath = "Assets/Sprites/Design/Match Maze.png";
    private const string PatternTitlePath = "Assets/Sprites/Design/Pattern.png";
    private const string MatchingTitlePath = "Assets/Sprites/Design/maze.png";
    private const string PatternSpriteFolder = "Assets/Sprites/Pattern/";
    private const string AnimalRoundFolder = "Assets/Sprite/kenney_animal-pack-redux/PNG/Round/";
    private const string AnimalSquareFolder = "Assets/Sprite/kenney_animal-pack-redux/PNG/Square/";

    [MenuItem("WizKid/Phase 2/Set Up Pattern and Matching Activities")]
    public static void SetupFromMenu()
    {
        RunSetup(showDialog: true);
    }

    private static void RunSetup(bool showDialog)
    {
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before running the Phase 2B setup.");

            QuestionBankCatalog catalog = AssetDatabase.LoadAssetAtPath<QuestionBankCatalog>(CatalogPath);
            if (catalog == null)
                throw new InvalidOperationException("QuestionBankCatalog.asset was not found. Complete the Phase 2 migration first.");

            CreatePatternBanks(catalog);
            CreateObjectMatchingBanks(catalog);
            CreateAndConfigureScenes();
            ConfigureAdventureMap();
            AddScenesToBuildSettings();

            catalog.banks = catalog.banks
                .Where(bank => bank != null)
                .GroupBy(bank => bank.bankId)
                .Select(group => group.First())
                .OrderBy(bank => bank.skill)
                .ThenBy(bank => bank.level)
                .ToList();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            if (!catalog.IsValid(out List<string> problems))
                throw new InvalidOperationException("Question-bank validation failed:\n" + string.Join("\n", problems));

            int questionCount = catalog.banks.Sum(bank => bank.questions != null ? bank.questions.Count : 0);
            string result = $"Phase 2B complete: {catalog.banks.Count} banks and {questionCount} questions are valid.";
            Debug.Log("WizKid " + result);

            if (showDialog)
                EditorUtility.DisplayDialog("WizKid Phase 2B", result, "OK");
        }
        catch (Exception exception)
        {
            Debug.LogError("WizKid Phase 2B setup failed: " + exception);
            if (showDialog)
                EditorUtility.DisplayDialog("WizKid Phase 2B", "Setup failed. Check the Console for details.", "OK");
        }
    }

    private static void CreatePatternBanks(QuestionBankCatalog catalog)
    {
        PatternSpec[][] levels =
        {
            new[]
            {
                P("What comes next in the pattern?", "l1p1.png", A("Blue", "Red", "Green", "Yellow"), 0),
                P("What comes next in the pattern?", "l1p2.png", A("Red Circle", "Blue Rectangle", "Green Circle", "Yellow Square"), 1),
                P("What comes next?", "12.png", A("1", "2", "3", "4"), 0),
                P("What shape comes next?", "tri.png", A("Square", "Circle", "Triangle", "Star"), 2),
                P("Which cat comes next?", "cat (1).png", A("Gray Cat", "Orange Cat", "White Cat", "Brown Cat"), 0)
            },
            new[]
            {
                P("What color comes next?", "COL.png", A("Red", "Yellow", "Green", "Blue"), 2),
                P("What letter comes next?", "abc.png", A("A", "B", "C", "D"), 2),
                P("What letter comes next?", "DA.png", A("A", "B", "C", "D"), 3),
                P("What size comes next?", "Big.png", A("Big", "Small", "Tall", "Short"), 0),
                P("What shape comes next?", "shep.png", A("Circle", "Triangle", "Square", "Star"), 2)
            },
            new[]
            {
                P("What number comes next?", "num.png", A("9", "10", "11", "12"), 1),
                P("What number comes next?", "w.png", A("8", "9", "10", "11"), 1),
                P("What comes next? 2, 3, 4, 5, ___", null, A("5", "6", "7", "8"), 1),
                P("What comes next? 10, 9, 8, 7, ___", null, A("4", "5", "6", "7"), 2),
                P("What comes next? 5, 10, 15, 20, ___", null, A("21", "22", "24", "25"), 3)
            },
            new[]
            {
                P("What comes next? 1, 1, 2, 1, 1, 2, ___", null, A("1", "2", "3", "4"), 0),
                P("Circle, Circle, Square, Circle, Circle, ___", null, A("Circle", "Square", "Triangle", "Star"), 1),
                P("Red, Blue, Blue, Red, Blue, Blue, ___", null, A("Red", "Blue", "Green", "Yellow"), 0),
                P("A, A, B, A, A, B, ___", null, A("A", "B", "C", "D"), 0),
                P("2, 4, 4, 2, 4, 4, ___", null, A("2", "3", "4", "5"), 0)
            },
            new[]
            {
                P("1, 2, 4, 1, 2, 4, ___", null, A("1", "2", "3", "4"), 0),
                P("A, B, C, D, A, B, C, ___", null, A("A", "B", "C", "D"), 3),
                P("2, 5, 8, 11, ___", null, A("12", "13", "14", "15"), 2),
                P("10, 8, 6, 4, ___", null, A("1", "2", "3", "4"), 1),
                P("Circle, Triangle, Square, Star, Circle, Triangle, Square, ___", null, A("Circle", "Triangle", "Square", "Star"), 3)
            }
        };

        for (int levelIndex = 0; levelIndex < levels.Length; levelIndex++)
        {
            int level = levelIndex + 1;
            string bankId = $"patterns-l{level:00}";
            var questions = new List<LearningQuestion>();

            for (int questionIndex = 0; questionIndex < levels[levelIndex].Length; questionIndex++)
            {
                PatternSpec spec = levels[levelIndex][questionIndex];
                questions.Add(new LearningQuestion
                {
                    questionId = $"{bankId}-q{questionIndex + 1:00}",
                    prompt = spec.prompt,
                    image = string.IsNullOrEmpty(spec.imageFile)
                        ? null
                        : LoadSprite(PatternSpriteFolder + spec.imageFile),
                    answers = spec.answers,
                    answerImages = new Sprite[4],
                    correctIndex = spec.correctIndex,
                    difficulty = level,
                    competency = "Identifies and completes visual, number, and symbol patterns"
                });
            }

            SaveBank(catalog, LearningSkill.Patterns, level, bankId,
                "Identifies and completes visual, number, and symbol patterns", questions);
        }
    }

    private static void CreateObjectMatchingBanks(QuestionBankCatalog catalog)
    {
        string[][] animalLevels =
        {
            new[] { "bear", "cow", "dog", "duck", "frog" },
            new[] { "elephant", "giraffe", "monkey", "panda", "rabbit" },
            new[] { "chick", "chicken", "goat", "horse", "pig" },
            new[] { "crocodile", "hippo", "rhino", "snake", "zebra" },
            new[] { "buffalo", "gorilla", "moose", "narwhal", "owl" }
        };

        for (int levelIndex = 0; levelIndex < animalLevels.Length; levelIndex++)
        {
            int level = levelIndex + 1;
            string bankId = $"objectmatching-l{level:00}";
            string[] group = animalLevels[levelIndex];
            var questions = new List<LearningQuestion>();

            for (int questionIndex = 0; questionIndex < group.Length; questionIndex++)
            {
                string animal = group[questionIndex];
                int correctIndex = (questionIndex + level) % 4;
                string[] choices = BuildAnimalChoices(group, questionIndex, correctIndex);

                questions.Add(new LearningQuestion
                {
                    questionId = $"{bankId}-q{questionIndex + 1:00}",
                    prompt = "Find the same animal.",
                    image = LoadSprite(AnimalRoundFolder + animal + ".png"),
                    answers = choices.Select(ToDisplayName).ToArray(),
                    answerImages = choices.Select(choice => LoadSprite(AnimalSquareFolder + choice + ".png")).ToArray(),
                    correctIndex = correctIndex,
                    difficulty = level,
                    competency = "Matches identical objects across visual styles"
                });
            }

            SaveBank(catalog, LearningSkill.ObjectMatching, level, bankId,
                "Matches identical objects across visual styles", questions);
        }
    }

    private static string[] BuildAnimalChoices(string[] group, int questionIndex, int correctIndex)
    {
        string correct = group[questionIndex];
        var distractors = new List<string>();
        for (int offset = 1; distractors.Count < 3; offset++)
        {
            string candidate = group[(questionIndex + offset) % group.Length];
            if (candidate != correct && !distractors.Contains(candidate))
                distractors.Add(candidate);
        }

        var choices = new string[4];
        int distractorIndex = 0;
        for (int i = 0; i < choices.Length; i++)
            choices[i] = i == correctIndex ? correct : distractors[distractorIndex++];
        return choices;
    }

    private static void SaveBank(
        QuestionBankCatalog catalog,
        LearningSkill skill,
        int level,
        string bankId,
        string competency,
        List<LearningQuestion> questions)
    {
        string assetPath = $"{BankFolder}/{skill}_Level_{level:00}.asset";
        QuestionBank bank = AssetDatabase.LoadAssetAtPath<QuestionBank>(assetPath);
        if (bank == null)
        {
            bank = ScriptableObject.CreateInstance<QuestionBank>();
            bank.name = $"{skill} Level {level}";
            AssetDatabase.CreateAsset(bank, assetPath);
        }

        bank.bankId = bankId;
        bank.skill = skill;
        bank.level = level;
        bank.competency = competency;
        bank.questions = questions;
        EditorUtility.SetDirty(bank);

        catalog.banks.RemoveAll(existing => existing == null || existing.bankId == bankId);
        catalog.banks.Add(bank);
    }

    private static void CreateAndConfigureScenes()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PatternScenePath) == null)
            throw new InvalidOperationException("PatternPlains.unity was not found.");

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MatchingScenePath) == null &&
            !AssetDatabase.CopyAsset(PatternScenePath, MatchingScenePath))
        {
            throw new InvalidOperationException("Unity could not create ObjectMatching.unity.");
        }

        AssetDatabase.SaveAssets();
        Scene matchingScene = EditorSceneManager.OpenScene(MatchingScenePath, OpenSceneMode.Additive);
        try
        {
            Sprite patternTitle = LoadSprite(PatternTitlePath);
            Sprite matchingTitle = LoadSprite(MatchingTitlePath);

            foreach (GameObject root in matchingScene.GetRootGameObjects())
            {
                foreach (Image image in root.GetComponentsInChildren<Image>(includeInactive: true))
                {
                    if (image.sprite == patternTitle)
                    {
                        image.sprite = matchingTitle;
                        EditorUtility.SetDirty(image);
                    }
                }
            }

            EditorSceneManager.SaveScene(matchingScene);
        }
        finally
        {
            EditorSceneManager.CloseScene(matchingScene, removeScene: true);
        }
    }

    private static void ConfigureAdventureMap()
    {
        Scene mapScene = EditorSceneManager.OpenScene(AdventureMapPath, OpenSceneMode.Additive);
        try
        {
            AdventureMapManager manager = FindComponentInScene<AdventureMapManager>(mapScene);
            if (manager == null || manager.alphabetOrderButton == null)
                throw new InvalidOperationException("AdventureMapManager or its button template is missing.");

            manager.patternPlainsButton = CreateOrUpdateActivityButton(
                manager,
                manager.patternPlainsButton,
                "patternPlainsButton",
                LoadSprite(PatternButtonArtPath),
                new Vector2(-771f, -271f),
                manager.OpenPatternPlains);

            manager.objectMatchingButton = CreateOrUpdateActivityButton(
                manager,
                manager.objectMatchingButton,
                "objectMatchingButton",
                LoadSprite(MatchingButtonArtPath),
                new Vector2(735f, -271f),
                manager.OpenObjectMatching);

            EditorUtility.SetDirty(manager);
            EditorSceneManager.SaveScene(mapScene);
        }
        finally
        {
            EditorSceneManager.CloseScene(mapScene, removeScene: true);
        }
    }

    private static Button CreateOrUpdateActivityButton(
        AdventureMapManager manager,
        Button currentButton,
        string objectName,
        Sprite artwork,
        Vector2 position,
        UnityEngine.Events.UnityAction action)
    {
        Button button = currentButton;
        Transform parent = manager.alphabetOrderButton.transform.parent;

        if (button == null)
        {
            Transform existing = parent.Find(objectName);
            button = existing != null ? existing.GetComponent<Button>() : null;
        }

        if (button == null)
        {
            GameObject clone = UnityEngine.Object.Instantiate(manager.alphabetOrderButton.gameObject, parent);
            clone.name = objectName;
            button = clone.GetComponent<Button>();
        }

        button.name = objectName;
        button.image.sprite = artwork;
        // Match the established Adventure Map cards, which fill the common button frame.
        button.image.preserveAspect = false;
        button.onClick = new Button.ButtonClickedEvent();
        UnityEventTools.AddPersistentListener(button.onClick, action);

        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(354.8729f, 315f);
        return button;
    }

    private static void AddScenesToBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        AddBuildScene(scenes, PatternScenePath);
        AddBuildScene(scenes, MatchingScenePath);
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void AddBuildScene(List<EditorBuildSettingsScene> scenes, string path)
    {
        EditorBuildSettingsScene existing = scenes.FirstOrDefault(scene => scene.path == path);
        if (existing != null)
        {
            existing.enabled = true;
            return;
        }

        scenes.Add(new EditorBuildSettingsScene(path, true));
    }

    private static T FindComponentInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(includeInactive: true);
            if (component != null)
                return component;
        }

        return null;
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            throw new InvalidOperationException("Sprite was not found or is not imported as a Sprite: " + path);
        return sprite;
    }

    private static string ToDisplayName(string value)
    {
        return string.IsNullOrEmpty(value) ? string.Empty : char.ToUpperInvariant(value[0]) + value.Substring(1);
    }

    private static string[] A(params string[] answers)
    {
        return answers;
    }

    private static PatternSpec P(string prompt, string imageFile, string[] answers, int correctIndex)
    {
        return new PatternSpec(prompt, imageFile, answers, correctIndex);
    }

    private readonly struct PatternSpec
    {
        public readonly string prompt;
        public readonly string imageFile;
        public readonly string[] answers;
        public readonly int correctIndex;

        public PatternSpec(string prompt, string imageFile, string[] answers, int correctIndex)
        {
            this.prompt = prompt;
            this.imageFile = imageFile;
            this.answers = answers;
            this.correctIndex = correctIndex;
        }
    }
}
