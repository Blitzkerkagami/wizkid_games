using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "QuestionBankCatalog", menuName = "WizKid/Learning/Question Bank Catalog")]
public class QuestionBankCatalog : ScriptableObject
{
    public List<QuestionBank> banks = new List<QuestionBank>();

    public QuestionBank FindBank(LearningSkill skill, int level)
    {
        if (banks == null)
            return null;

        return banks.Find(bank => bank != null && bank.skill == skill && bank.level == level);
    }

    public bool IsValid(out List<string> problems)
    {
        problems = new List<string>();
        var bankIds = new HashSet<string>();
        var skillLevels = new HashSet<string>();
        var questionIds = new HashSet<string>();

        if (banks == null || banks.Count == 0)
        {
            problems.Add("The question bank catalog is empty.");
            return false;
        }

        foreach (QuestionBank bank in banks)
        {
            if (bank == null)
            {
                problems.Add("The catalog contains an empty bank reference.");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(bank.bankId) && !bankIds.Add(bank.bankId))
                problems.Add($"Duplicate bank ID '{bank.bankId}'.");

            string skillLevel = $"{bank.skill}-{bank.level}";
            if (!skillLevels.Add(skillLevel))
                problems.Add($"More than one bank is assigned to {bank.skill} level {bank.level}.");

            if (!bank.IsValid(out List<string> bankProblems))
                problems.AddRange(bankProblems);

            if (bank.questions == null)
                continue;

            foreach (LearningQuestion question in bank.questions)
            {
                if (question != null && !string.IsNullOrWhiteSpace(question.questionId) &&
                    !questionIds.Add(question.questionId))
                {
                    problems.Add($"Duplicate question ID '{question.questionId}' across the catalog.");
                }
            }
        }

        return problems.Count == 0;
    }
}
