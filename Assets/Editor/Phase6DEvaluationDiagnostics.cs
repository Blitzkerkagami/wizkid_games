using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class Phase6DEvaluationDiagnostics
{
    [MenuItem("WizKid/Phase 6D/Validate Evaluation Setup")]
    public static void ValidateSetup()
    {
        bool databaseReady = LocalDatabase.Initialize();
        EvaluationQuestion[] allQuestions = EvaluationQuestionBank.Uat
            .Concat(EvaluationQuestionBank.Iso25010)
            .ToArray();
        string[] duplicateIds = allQuestions
            .GroupBy(question => question.id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        bool ratingsValid = allQuestions.All(question =>
            question != null && !string.IsNullOrWhiteSpace(question.id) &&
            !string.IsNullOrWhiteSpace(question.category) &&
            !string.IsNullOrWhiteSpace(question.statement));

        int[] perfectRatings = Enumerable.Repeat(5, EvaluationQuestionBank.Uat.Length).ToArray();
        EvaluationResponseRecord sample = EvaluationScoring.CreateResponse(
            EvaluationInstrument.Uat,
            "teacher",
            EvaluationQuestionBank.Uat,
            perfectRatings,
            string.Empty);
        bool scoringValid = Math.Abs(sample.overall_mean - 5f) < 0.001f &&
                            sample.interpretation == "Excellent";

        if (!databaseReady || duplicateIds.Length > 0 || !ratingsValid || !scoringValid ||
            EvaluationQuestionBank.Uat.Length != 20 ||
            EvaluationQuestionBank.Iso25010.Length != 28)
        {
            Debug.LogError(
                "Phase 6D validation failed. " +
                $"SQLite: {databaseReady}; UAT questions: {EvaluationQuestionBank.Uat.Length}; " +
                $"ISO questions: {EvaluationQuestionBank.Iso25010.Length}; " +
                $"duplicate IDs: {string.Join(", ", duplicateIds)}; " +
                $"question content valid: {ratingsValid}; scoring valid: {scoringValid}.");
            return;
        }

        Debug.Log(
            "WizKid Phase 6D setup PASS. " +
            $"UAT questions: 20; ISO 25010 questions: 28; " +
            $"saved responses on this device: {LocalDatabase.GetEvaluationResponseCount()}. " +
            "Enter Play Mode, open AdventureMap, and select Evaluate Game.");
    }

    [MenuItem("WizKid/Phase 6D/Request Evaluation Sync")]
    public static void RequestSync()
    {
        EvaluationSyncService.RequestSync();
        Debug.Log("Phase 6D evaluation synchronization requested. Pending responses remain safe in SQLite until upload succeeds.");
    }
}
