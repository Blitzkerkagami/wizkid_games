using System;
using UnityEngine;

public enum LearningSkill
{
    Counting,
    Shapes,
    Colors,
    Math,
    Patterns,
    ObjectMatching,
    Alphabet
}

[Serializable]
public class LearningQuestion
{
    [Tooltip("Stable identifier used by saved attempts, analytics, and forecasting.")]
    public string questionId;

    [TextArea(2, 4)]
    public string prompt;

    public Sprite image;
    public string[] answers = new string[4];

    [Tooltip("Optional visual choices. When present, these are shown instead of answer text.")]
    public Sprite[] answerImages = new Sprite[4];

    public int correctIndex;

    [Range(1, 5)]
    public int difficulty = 1;

    [Tooltip("The learning outcome measured by this question.")]
    public string competency;

    public bool IsValid(out string problem)
    {
        if (string.IsNullOrWhiteSpace(questionId))
        {
            problem = "Question ID is missing.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            problem = $"{questionId}: prompt is missing.";
            return false;
        }

        bool hasFourTextAnswers = answers != null && answers.Length == 4;
        bool hasFourImageAnswers = answerImages != null && answerImages.Length == 4;

        if (!hasFourTextAnswers && !hasFourImageAnswers)
        {
            problem = $"{questionId}: exactly four text or image answers are required.";
            return false;
        }

        for (int i = 0; i < 4; i++)
        {
            bool hasText = hasFourTextAnswers && !string.IsNullOrWhiteSpace(answers[i]);
            bool hasImage = hasFourImageAnswers && answerImages[i] != null;
            if (!hasText && !hasImage)
            {
                problem = $"{questionId}: answer {i + 1} has no text or image.";
                return false;
            }
        }

        if (correctIndex < 0 || correctIndex >= 4)
        {
            problem = $"{questionId}: correct answer index is outside the answer list.";
            return false;
        }

        problem = string.Empty;
        return true;
    }
}
