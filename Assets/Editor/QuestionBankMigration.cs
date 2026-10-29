using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class QuestionBankMigration
{
    private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
    private const string DataFolder = "Assets/Data";
    private const string BankFolder = DataFolder + "/QuestionBanks";
    private const string CatalogPath = DataFolder + "/QuestionBankCatalog.asset";
    private const string SessionAttemptKey = "WizKid.Phase2.QuestionBankMigrationAttempted";

    static QuestionBankMigration()
    {
        EditorApplication.delayCall += TryAutomaticMigration;
    }

    [MenuItem("WizKid/Phase 2/Migrate Gameplay Questions")]
    public static void MigrateFromMenu()
    {
        MigrateQuestions(showSuccessDialog: true);
    }

    [MenuItem("WizKid/Phase 2/Validate Question Banks")]
    public static void ValidateFromMenu()
    {
        QuestionBankCatalog catalog = AssetDatabase.LoadAssetAtPath<QuestionBankCatalog>(CatalogPath);
        if (catalog == null)
        {
            EditorUtility.DisplayDialog("WizKid Question Banks", "QuestionBankCatalog.asset was not found.", "OK");
            return;
        }

        bool valid = ValidateCatalog(catalog, out string report);
        EditorUtility.DisplayDialog(
            "WizKid Question Banks",
            valid ? "Validation passed.\n\n" + report : "Validation failed.\n\n" + report,
            "OK");
    }

    private static void TryAutomaticMigration()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (SessionState.GetBool(SessionAttemptKey, false))
            return;

        SessionState.SetBool(SessionAttemptKey, true);

        if (AssetDatabase.LoadAssetAtPath<QuestionBankCatalog>(CatalogPath) != null)
            return;

        MigrateQuestions(showSuccessDialog: false);
    }

    private static void MigrateQuestions(bool showSuccessDialog)
    {
        Scene gameplayScene = default;
        bool openedForMigration = false;

        try
        {
            EnsureFolder(DataFolder);
            EnsureFolder(BankFolder);

            gameplayScene = SceneManager.GetSceneByPath(GameplayScenePath);
            if (!gameplayScene.IsValid() || !gameplayScene.isLoaded)
            {
                gameplayScene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Additive);
                openedForMigration = true;
            }

            QuizManager quizManager = FindQuizManager(gameplayScene);
            if (quizManager == null)
                throw new InvalidOperationException("QuizManager was not found in Gameplay.unity.");

            QuestionBankCatalog catalog = AssetDatabase.LoadAssetAtPath<QuestionBankCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<QuestionBankCatalog>();
                catalog.name = "QuestionBankCatalog";
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var migratedBanks = new List<QuestionBank>();
            MigrateSkill(migratedBanks, LearningSkill.Counting, quizManager.countingQuestions);
            MigrateSkill(migratedBanks, LearningSkill.Shapes, quizManager.shapeQuestions);
            MigrateSkill(migratedBanks, LearningSkill.Colors, quizManager.colorQuestions);
            MigrateSkill(migratedBanks, LearningSkill.Math, quizManager.mathQuestions);

            if (migratedBanks.Count > 0 && catalog.banks != null)
            {
                migratedBanks.AddRange(catalog.banks.Where(bank =>
                    bank != null &&
                    bank.skill != LearningSkill.Counting &&
                    bank.skill != LearningSkill.Shapes &&
                    bank.skill != LearningSkill.Colors &&
                    bank.skill != LearningSkill.Math));
            }

            if (migratedBanks.Count == 0 && catalog.banks != null && catalog.banks.Count > 0)
            {
                migratedBanks.AddRange(catalog.banks.Where(bank => bank != null));
            }

            catalog.banks = migratedBanks
                .OrderBy(bank => bank.skill)
                .ThenBy(bank => bank.level)
                .ToList();
            EditorUtility.SetDirty(catalog);

            if (!ValidateCatalog(catalog, out string report))
                throw new InvalidOperationException("Question bank validation failed:\n" + report);

            quizManager.questionBankCatalog = catalog;
            quizManager.passingScore = 60;

            // The questions now live in reusable assets. These arrays remain in the script only
            // as a compatibility fallback for older scenes.
            quizManager.countingQuestions = Array.Empty<QuizManager.QuestionList>();
            quizManager.shapeQuestions = Array.Empty<QuizManager.QuestionList>();
            quizManager.colorQuestions = Array.Empty<QuizManager.QuestionList>();
            quizManager.mathQuestions = Array.Empty<QuizManager.QuestionList>();
            EditorUtility.SetDirty(quizManager);

            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(gameplayScene))
                throw new InvalidOperationException("Unity could not save Gameplay.unity after migration.");

            Debug.Log("WizKid Phase 2: question-bank migration complete. " + report);

            if (showSuccessDialog)
                EditorUtility.DisplayDialog("WizKid Phase 2", "Question-bank migration complete.\n\n" + report, "OK");
        }
        catch (Exception exception)
        {
            Debug.LogError("WizKid Phase 2 migration failed: " + exception);
            if (showSuccessDialog)
                EditorUtility.DisplayDialog("WizKid Phase 2", "Migration failed. Check the Console for details.", "OK");
        }
        finally
        {
            if (openedForMigration && gameplayScene.IsValid() && gameplayScene.isLoaded)
                EditorSceneManager.CloseScene(gameplayScene, removeScene: true);
        }
    }

    private static void MigrateSkill(
        List<QuestionBank> migratedBanks,
        LearningSkill skill,
        QuizManager.QuestionList[] legacyLevels)
    {
        if (legacyLevels == null)
            return;

        for (int levelIndex = 0; levelIndex < legacyLevels.Length; levelIndex++)
        {
            QuizManager.QuestionList legacyLevel = legacyLevels[levelIndex];
            if (legacyLevel == null || legacyLevel.questions == null || legacyLevel.questions.Length == 0)
                continue;

            int level = levelIndex + 1;
            string skillName = skill.ToString();
            string bankId = $"{skillName.ToLowerInvariant()}-l{level:00}";
            string assetPath = $"{BankFolder}/{skillName}_Level_{level:00}.asset";
            QuestionBank bank = AssetDatabase.LoadAssetAtPath<QuestionBank>(assetPath);

            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<QuestionBank>();
                bank.name = $"{skillName} Level {level}";
                AssetDatabase.CreateAsset(bank, assetPath);
            }

            bank.bankId = bankId;
            bank.skill = skill;
            bank.level = level;
            bank.competency = GetCompetency(skill);
            bank.questions = legacyLevel.questions
                .Where(question => question != null)
                .Select((question, questionIndex) => ConvertQuestion(question, bankId, level, questionIndex, bank.competency))
                .ToList();

            EditorUtility.SetDirty(bank);
            migratedBanks.Add(bank);
        }
    }

    private static LearningQuestion ConvertQuestion(
        QuizManager.Question legacyQuestion,
        string bankId,
        int level,
        int questionIndex,
        string competency)
    {
        return new LearningQuestion
        {
            questionId = $"{bankId}-q{questionIndex + 1:00}",
            prompt = legacyQuestion.question != null ? legacyQuestion.question.Trim() : string.Empty,
            image = legacyQuestion.image,
            answers = legacyQuestion.answers != null
                ? legacyQuestion.answers.Select(answer => answer != null ? answer.Trim() : string.Empty).ToArray()
                : new string[4],
            answerImages = new Sprite[4],
            correctIndex = legacyQuestion.correctIndex,
            difficulty = level,
            competency = competency
        };
    }

    private static QuizManager FindQuizManager(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            QuizManager quizManager = root.GetComponentInChildren<QuizManager>(includeInactive: true);
            if (quizManager != null)
                return quizManager;
        }

        return null;
    }

    private static bool ValidateCatalog(QuestionBankCatalog catalog, out string report)
    {
        if (!catalog.IsValid(out List<string> problems))
        {
            report = string.Join("\n", problems);
            return false;
        }

        int bankCount = catalog.banks.Count(bank => bank != null);
        int questionCount = catalog.banks
            .Where(bank => bank != null && bank.questions != null)
            .Sum(bank => bank.questions.Count);
        report = $"{bankCount} banks and {questionCount} questions are valid.";
        return true;
    }

    private static string GetCompetency(LearningSkill skill)
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

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        string folderName = Path.GetFileName(folderPath);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folderName))
            throw new InvalidOperationException("Invalid asset folder path: " + folderPath);

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folderName);
    }
}
