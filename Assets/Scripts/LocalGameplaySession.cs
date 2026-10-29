using System;
using UnityEngine;

/// <summary>
/// Tracks one quiz run and forwards durable events to LocalDatabase.
/// Database errors never interrupt the player's game.
/// </summary>
public sealed class LocalGameplaySession
{
    private readonly string sessionId;
    private readonly string playerId;
    private readonly string activity;
    private readonly LearningSkill skill;
    private readonly int level;
    private readonly int totalQuestions;

    private float questionStartedAt;
    private int attemptedQuestions;
    private bool finished;

    public bool IsActive => !finished && !string.IsNullOrEmpty(sessionId);

    private LocalGameplaySession(
        string sessionId,
        string playerId,
        string activity,
        LearningSkill skill,
        int level,
        int totalQuestions)
    {
        this.sessionId = sessionId;
        this.playerId = playerId;
        this.activity = activity;
        this.skill = skill;
        this.level = level;
        this.totalQuestions = totalQuestions;
    }

    public static LocalGameplaySession Begin(
        string activity,
        LearningSkill skill,
        int level,
        int totalQuestions)
    {
        string playerId = LocalDatabase.UpsertCurrentLearnerProfile();
        string sessionId = LocalDatabase.StartGameplaySession(playerId, activity, skill, level, totalQuestions);
        return new LocalGameplaySession(sessionId, playerId, activity, skill, level, totalQuestions);
    }

    public void BeginQuestion()
    {
        questionStartedAt = Time.realtimeSinceStartup;
    }

    public void RecordAnswer(
        LearningQuestion question,
        int? selectedOriginalAnswerIndex,
        bool isCorrect,
        bool timedOut)
    {
        if (!IsActive || question == null)
            return;

        attemptedQuestions++;
        long responseTime = (long)Math.Round(Math.Max(0f, Time.realtimeSinceStartup - questionStartedAt) * 1000f);

        LocalDatabase.RecordQuestionAttempt(
            sessionId,
            playerId,
            activity,
            skill,
            level,
            question,
            attemptedQuestions,
            selectedOriginalAnswerIndex,
            isCorrect,
            timedOut,
            responseTime);
    }

    public void Finish(int correctAnswers, int scorePercent, bool passed, int livesRemaining, string status)
    {
        if (!IsActive)
            return;

        finished = true;
        LocalDatabase.FinishGameplaySession(
            sessionId,
            playerId,
            activity,
            skill,
            level,
            totalQuestions,
            attemptedQuestions,
            correctAnswers,
            scorePercent,
            passed,
            livesRemaining,
            status);
    }

    public void Abandon(int correctAnswers, int livesRemaining, int? explicitScore = null)
    {
        int score = explicitScore ?? (totalQuestions > 0
            ? Mathf.RoundToInt((float)correctAnswers / totalQuestions * 100f)
            : 0);
        Finish(correctAnswers, score, false, livesRemaining, "abandoned");
    }
}
