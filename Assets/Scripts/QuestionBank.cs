using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "QuestionBank", menuName = "WizKid/Learning/Question Bank")]
public class QuestionBank : ScriptableObject
{
    [Tooltip("Stable identifier for this skill and level, for example counting-l01.")]
    public string bankId;

    public LearningSkill skill;

    [Range(1, 5)]
    public int level = 1;

    [TextArea(2, 4)]
    public string competency;

    public List<LearningQuestion> questions = new List<LearningQuestion>();

    public bool IsValid(out List<string> problems)
    {
        problems = new List<string>();

        if (string.IsNullOrWhiteSpace(bankId))
            problems.Add($"{name}: bank ID is missing.");

        if (level < 1 || level > 5)
            problems.Add($"{name}: level must be from 1 to 5.");

        if (questions == null || questions.Count == 0)
        {
            problems.Add($"{name}: the bank has no questions.");
            return false;
        }

        var ids = new HashSet<string>();
        foreach (LearningQuestion question in questions)
        {
            if (question == null)
            {
                problems.Add($"{name}: a question entry is empty.");
                continue;
            }

            if (!question.IsValid(out string problem))
                problems.Add(problem);

            if (!string.IsNullOrWhiteSpace(question.questionId) && !ids.Add(question.questionId))
                problems.Add($"{name}: duplicate question ID '{question.questionId}'.");
        }

        return problems.Count == 0;
    }
}
