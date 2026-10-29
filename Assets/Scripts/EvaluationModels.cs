using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum EvaluationInstrument
{
    Uat,
    Iso25010
}

[Serializable]
public sealed class EvaluationQuestion
{
    public string id;
    public string category;
    public string statement;

    public EvaluationQuestion(string id, string category, string statement)
    {
        this.id = id;
        this.category = category;
        this.statement = statement;
    }
}

[Serializable]
public sealed class EvaluationRating
{
    public string question_id;
    public string category;
    public int rating;
}

[Serializable]
public sealed class EvaluationRatingCollection
{
    public EvaluationRating[] items = Array.Empty<EvaluationRating>();
}

[Serializable]
public sealed class EvaluationCategoryScore
{
    public string category;
    public float mean;
}

[Serializable]
public sealed class EvaluationCategoryScoreCollection
{
    public EvaluationCategoryScore[] items = Array.Empty<EvaluationCategoryScore>();
}

[Serializable]
public sealed class EvaluationResponseRecord
{
    public string response_id;
    public string instrument;
    public string respondent_role;
    public string ratings_json;
    public string category_scores_json;
    public float overall_mean;
    public string interpretation;
    public string comments;
    public string completed_at_utc;
    public string app_version;
    public string platform;
    public string sync_status;
    public string synced_at_utc;
}

public static class EvaluationScoring
{
    public static EvaluationResponseRecord CreateResponse(
        EvaluationInstrument instrument,
        string respondentRole,
        EvaluationQuestion[] questions,
        IReadOnlyList<int> ratings,
        string comments)
    {
        if (questions == null || ratings == null || questions.Length != ratings.Count || questions.Length == 0)
            throw new ArgumentException("Every evaluation question requires one rating.");

        EvaluationRating[] ratingRows = new EvaluationRating[questions.Length];
        for (int index = 0; index < questions.Length; index++)
        {
            int rating = Mathf.Clamp(ratings[index], 1, 5);
            ratingRows[index] = new EvaluationRating
            {
                question_id = questions[index].id,
                category = questions[index].category,
                rating = rating
            };
        }

        EvaluationCategoryScore[] categoryRows = ratingRows
            .GroupBy(row => row.category, StringComparer.OrdinalIgnoreCase)
            .Select(group => new EvaluationCategoryScore
            {
                category = group.Key,
                mean = (float)Math.Round(group.Average(row => row.rating), 2)
            })
            .ToArray();
        float overallMean = (float)Math.Round(ratingRows.Average(row => row.rating), 2);

        return new EvaluationResponseRecord
        {
            response_id = Guid.NewGuid().ToString("D"),
            instrument = instrument == EvaluationInstrument.Iso25010 ? "iso_25010" : "uat_tam",
            respondent_role = NormalizeRole(respondentRole),
            ratings_json = JsonUtility.ToJson(new EvaluationRatingCollection { items = ratingRows }),
            category_scores_json = JsonUtility.ToJson(new EvaluationCategoryScoreCollection { items = categoryRows }),
            overall_mean = overallMean,
            interpretation = Interpret(overallMean),
            comments = (comments ?? string.Empty).Trim(),
            completed_at_utc = DateTime.UtcNow.ToString("o"),
            app_version = Application.version,
            platform = Application.platform.ToString(),
            sync_status = "pending",
            synced_at_utc = string.Empty
        };
    }

    public static string Interpret(float mean)
    {
        if (mean >= 4.21f) return "Excellent";
        if (mean >= 3.41f) return "Very Good";
        if (mean >= 2.61f) return "Good";
        if (mean >= 1.81f) return "Fair";
        return "Poor";
    }

    private static string NormalizeRole(string role)
    {
        string normalized = (role ?? string.Empty).Trim().ToLowerInvariant().Replace(' ', '_');
        switch (normalized)
        {
            case "learner":
            case "parent":
            case "teacher":
            case "it_evaluator":
                return normalized;
            default:
                throw new ArgumentException("Unsupported evaluation respondent role.");
        }
    }
}

public static class EvaluationQuestionBank
{
    public static readonly EvaluationQuestion[] Uat =
    {
        new EvaluationQuestion("UAT-KQ-01", "Input and Feedback", "The game correctly understands and processes user inputs during gameplay."),
        new EvaluationQuestion("UAT-KQ-02", "Input and Feedback", "The answers and feedback provided are accurate and easy to understand."),
        new EvaluationQuestion("UAT-KQ-03", "Input and Feedback", "The game responds within an acceptable time of two to five seconds."),
        new EvaluationQuestion("UAT-KQ-04", "Input and Feedback", "The game can handle repeated or follow-up interactions smoothly."),
        new EvaluationQuestion("UAT-KQ-05", "Input and Feedback", "The content is appropriate for the learner's grade level."),

        new EvaluationQuestion("UAT-LA-01", "Learning Assistance", "The game presents lessons that match the learner's level."),
        new EvaluationQuestion("UAT-LA-02", "Learning Assistance", "The game provides helpful hints and guidance during gameplay."),
        new EvaluationQuestion("UAT-LA-03", "Learning Assistance", "The learning activities are engaging and interactive."),
        new EvaluationQuestion("UAT-LA-04", "Learning Assistance", "The game encourages continuous learning through rewards and feedback."),
        new EvaluationQuestion("UAT-LA-05", "Learning Assistance", "The game adjusts the recommended difficulty based on learner performance."),

        new EvaluationQuestion("UAT-UI-01", "Usability and Interface", "The interface is easy to navigate and visually appealing for young learners."),
        new EvaluationQuestion("UAT-UI-02", "Usability and Interface", "Buttons and controls are easy to understand and use."),
        new EvaluationQuestion("UAT-UI-03", "Usability and Interface", "The game works properly on the device used for testing."),
        new EvaluationQuestion("UAT-UI-04", "Usability and Interface", "The overall design is simple, clear, and child-friendly."),
        new EvaluationQuestion("UAT-UI-05", "Usability and Interface", "Users can complete tasks without confusion or unnecessary assistance."),

        new EvaluationQuestion("UAT-RP-01", "Reliability and Performance", "The game runs smoothly without crashes or serious bugs."),
        new EvaluationQuestion("UAT-RP-02", "Reliability and Performance", "Game progress and scores are saved correctly."),
        new EvaluationQuestion("UAT-RP-03", "Reliability and Performance", "The game loads and responds quickly during use."),
        new EvaluationQuestion("UAT-RP-04", "Reliability and Performance", "The game maintains consistent performance throughout gameplay."),
        new EvaluationQuestion("UAT-RP-05", "Reliability and Performance", "Overall, the game is reliable for educational use.")
    };

    public static readonly EvaluationQuestion[] Iso25010 =
    {
        new EvaluationQuestion("ISO-FS-01", "Functional Suitability", "WizKid provides the educational features needed by kindergarten learners."),
        new EvaluationQuestion("ISO-FS-02", "Functional Suitability", "The activities and lessons function correctly without errors."),
        new EvaluationQuestion("ISO-FS-03", "Functional Suitability", "The game achieves its intended educational objectives."),
        new EvaluationQuestion("ISO-FS-04", "Functional Suitability", "The game provides accurate outputs and responses."),

        new EvaluationQuestion("ISO-PE-01", "Performance Efficiency", "The game loads quickly and runs smoothly."),
        new EvaluationQuestion("ISO-PE-02", "Performance Efficiency", "The game responds quickly to user actions."),
        new EvaluationQuestion("ISO-PE-03", "Performance Efficiency", "The game performs well on a low-end supported Android device."),
        new EvaluationQuestion("ISO-PE-04", "Performance Efficiency", "The application does not frequently crash or freeze."),

        new EvaluationQuestion("ISO-CO-01", "Compatibility", "The game works properly on its supported devices."),
        new EvaluationQuestion("ISO-CO-02", "Compatibility", "WizKid can operate alongside other applications without issues."),
        new EvaluationQuestion("ISO-CO-03", "Compatibility", "The game maintains stable performance across supported devices."),

        new EvaluationQuestion("ISO-US-01", "Usability", "The interface is easy to understand and navigate."),
        new EvaluationQuestion("ISO-US-02", "Usability", "The controls and navigation are simple for young learners."),
        new EvaluationQuestion("ISO-US-03", "Usability", "The design, colors, and visuals are engaging for children."),
        new EvaluationQuestion("ISO-US-04", "Usability", "Instructions in the game are clear and understandable."),
        new EvaluationQuestion("ISO-US-05", "Usability", "Users can learn how to use the game quickly."),

        new EvaluationQuestion("ISO-RE-01", "Reliability", "The game works consistently during use."),
        new EvaluationQuestion("ISO-RE-02", "Reliability", "The game can recover properly after an unexpected error or restart."),
        new EvaluationQuestion("ISO-RE-03", "Reliability", "The application maintains stable performance over time."),

        new EvaluationQuestion("ISO-SE-01", "Security", "Learner data and progress are properly protected."),
        new EvaluationQuestion("ISO-SE-02", "Security", "The system prevents unauthorized access to evaluation and learning records."),
        new EvaluationQuestion("ISO-SE-03", "Security", "The application stores learner information safely."),

        new EvaluationQuestion("ISO-MA-01", "Maintainability", "The system can be updated or improved without affecting unrelated features."),
        new EvaluationQuestion("ISO-MA-02", "Maintainability", "Errors and bugs can be identified using the available diagnostics."),
        new EvaluationQuestion("ISO-MA-03", "Maintainability", "The application structure supports future enhancements."),

        new EvaluationQuestion("ISO-PO-01", "Portability", "The game can be installed easily on supported Android devices."),
        new EvaluationQuestion("ISO-PO-02", "Portability", "The project can be transferred to another supported build environment."),
        new EvaluationQuestion("ISO-PO-03", "Portability", "WizKid adapts well to different supported hardware configurations.")
    };
}
