using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Small, offline-first SQLite store for learner progress and question attempts.
/// It uses the SQLite library that ships with Windows/Unity, so no network or
/// external database package is required at runtime.
/// </summary>
public static class LocalDatabase
{
    private const string DatabaseFileName = "wizkid_learning.db";
    private const string PlayerIdKey = "LocalPlayerId";
    private const string InstallationIdKey = "LocalInstallationId";
    private const string PendingSyncStatus = "pending";
    private const int CurrentSchemaVersion = 6;

    private static readonly object SyncRoot = new object();
    private static bool initialized;
    private static bool disabledForSession;
    private static bool failureLogged;
    private static bool androidLaunchRecorded;
    private static long androidLaunchCountThisProcess;

    public static string DatabasePath => Path.Combine(Application.persistentDataPath, DatabaseFileName);
    public static bool IsAvailable => !disabledForSession;

    public static bool Initialize()
    {
        lock (SyncRoot)
            return EnsureReady();
    }

    public static string UpsertCurrentLearnerProfile()
    {
        lock (SyncRoot)
        {
            if (!EnsureReady())
                return string.Empty;

            string playerId = GetOrCreatePlayerPrefId(PlayerIdKey);
            string now = UtcNow();

            const string sql = @"
INSERT INTO learner_profiles (
    player_id, display_name, age, birthday_text, wizard_index,
    created_at_utc, updated_at_utc, sync_status
)
VALUES (?, ?, ?, ?, ?, ?, ?, ?)
ON CONFLICT(player_id) DO UPDATE SET
    display_name = excluded.display_name,
    age = excluded.age,
    birthday_text = excluded.birthday_text,
    wizard_index = excluded.wizard_index,
    updated_at_utc = excluded.updated_at_utc,
    sync_status = 'pending',
    synced_at_utc = NULL,
    sync_revision = learner_profiles.sync_revision + 1;";

            bool saved = ExecuteNonQuery(sql,
                playerId,
                PlayerPrefs.GetString("PlayerName", "Little Adventurer"),
                PlayerPrefs.GetInt("PlayerAge", 5),
                PlayerPrefs.GetString("PlayerBirthday", string.Empty),
                PlayerPrefs.GetInt("SelectedWizard", 0),
                now,
                now,
                PendingSyncStatus);

            return saved ? playerId : string.Empty;
        }
    }

    public static string StartGameplaySession(
        string playerId,
        string activity,
        LearningSkill skill,
        int level,
        int totalQuestions)
    {
        lock (SyncRoot)
        {
            if (!EnsureReady() || string.IsNullOrWhiteSpace(playerId))
                return string.Empty;

            string sessionId = Guid.NewGuid().ToString("N");
            string startedAt = UtcNow();

            const string sql = @"
INSERT INTO gameplay_sessions (
    session_id, player_id, activity, skill, level, started_at_utc,
    total_questions, attempted_questions, correct_answers, lives_remaining,
    completion_status, installation_id, app_version, sync_status
)
VALUES (?, ?, ?, ?, ?, ?, ?, 0, 0, 3, 'in_progress', ?, ?, ?);";

            bool saved = ExecuteNonQuery(sql,
                sessionId,
                playerId,
                activity,
                skill.ToString(),
                Mathf.Clamp(level, 1, 5),
                startedAt,
                Math.Max(0, totalQuestions),
                GetOrCreatePlayerPrefId(InstallationIdKey),
                Application.version,
                PendingSyncStatus);

            return saved ? sessionId : string.Empty;
        }
    }

    public static bool RecordQuestionAttempt(
        string sessionId,
        string playerId,
        string activity,
        LearningSkill skill,
        int level,
        LearningQuestion question,
        int attemptNumber,
        int? selectedAnswerIndex,
        bool isCorrect,
        bool timedOut,
        long responseTimeMilliseconds)
    {
        lock (SyncRoot)
        {
            if (!EnsureReady() || string.IsNullOrWhiteSpace(sessionId) || question == null)
                return false;

            const string sql = @"
INSERT INTO question_attempts (
    attempt_id, session_id, player_id, question_id, activity, skill, level,
    difficulty, competency, attempt_number, selected_answer_index,
    correct_answer_index, is_correct, timed_out, response_time_ms,
    attempted_at_utc, sync_status
)
VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);";

            return ExecuteNonQuery(sql,
                Guid.NewGuid().ToString("N"),
                sessionId,
                playerId,
                question.questionId,
                activity,
                skill.ToString(),
                Mathf.Clamp(level, 1, 5),
                Mathf.Clamp(question.difficulty, 1, 5),
                question.competency ?? string.Empty,
                Math.Max(1, attemptNumber),
                selectedAnswerIndex.HasValue ? (object)selectedAnswerIndex.Value : null,
                question.correctIndex,
                isCorrect,
                timedOut,
                Math.Max(0L, responseTimeMilliseconds),
                UtcNow(),
                PendingSyncStatus);
        }
    }

    public static bool FinishGameplaySession(
        string sessionId,
        string playerId,
        string activity,
        LearningSkill skill,
        int level,
        int totalQuestions,
        int attemptedQuestions,
        int correctAnswers,
        int scorePercent,
        bool passed,
        int livesRemaining,
        string completionStatus)
    {
        lock (SyncRoot)
        {
            if (!EnsureReady() || string.IsNullOrWhiteSpace(sessionId))
                return false;

            IntPtr database = IntPtr.Zero;
            try
            {
                database = OpenDatabase();
                ExecuteSql(database, "BEGIN IMMEDIATE;");

                const string updateSessionSql = @"
UPDATE gameplay_sessions SET
    ended_at_utc = ?, attempted_questions = ?, correct_answers = ?,
    score_percent = ?, passed = ?, lives_remaining = ?, completion_status = ?,
    sync_status = 'pending', synced_at_utc = NULL,
    sync_revision = sync_revision + 1
WHERE session_id = ? AND completion_status = 'in_progress';";

                int updated = ExecutePrepared(database, updateSessionSql,
                    UtcNow(),
                    Math.Max(0, attemptedQuestions),
                    Math.Max(0, correctAnswers),
                    Mathf.Clamp(scorePercent, 0, 100),
                    passed,
                    Mathf.Clamp(livesRemaining, 0, 3),
                    completionStatus,
                    sessionId);

                if (updated > 0 && completionStatus != "abandoned")
                {
                    const string insertResultSql = @"
INSERT INTO level_results (
    result_id, session_id, player_id, activity, skill, level,
    total_questions, attempted_questions, correct_answers, score_percent,
    passed, lives_remaining, completed_at_utc, sync_status
)
VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
ON CONFLICT(session_id) DO UPDATE SET
    attempted_questions = excluded.attempted_questions,
    correct_answers = excluded.correct_answers,
    score_percent = excluded.score_percent,
    passed = excluded.passed,
    lives_remaining = excluded.lives_remaining,
    completed_at_utc = excluded.completed_at_utc,
    sync_status = 'pending',
    synced_at_utc = NULL,
    sync_revision = level_results.sync_revision + 1;";

                    ExecutePrepared(database, insertResultSql,
                        Guid.NewGuid().ToString("N"),
                        sessionId,
                        playerId,
                        activity,
                        skill.ToString(),
                        Mathf.Clamp(level, 1, 5),
                        Math.Max(0, totalQuestions),
                        Math.Max(0, attemptedQuestions),
                        Math.Max(0, correctAnswers),
                        Mathf.Clamp(scorePercent, 0, 100),
                        passed,
                        Mathf.Clamp(livesRemaining, 0, 3),
                        UtcNow(),
                        PendingSyncStatus);
                }

                ExecuteSql(database, "COMMIT;");
                return updated > 0;
            }
            catch (Exception exception)
            {
                if (database != IntPtr.Zero)
                {
                    try { ExecuteSql(database, "ROLLBACK;"); }
                    catch { /* The original database error is more useful. */ }
                }

                HandleFailure("finish a gameplay session", exception);
                return false;
            }
            finally
            {
                CloseDatabase(database);
            }
        }
    }

    public static LocalDataSummary GetSummary()
    {
        lock (SyncRoot)
        {
            if (!EnsureReady())
                return new LocalDataSummary(DatabasePath, 0, 0, 0, 0, false);

            IntPtr database = IntPtr.Zero;
            try
            {
                database = OpenDatabase();
                return new LocalDataSummary(
                    DatabasePath,
                    QueryLong(database, "SELECT COUNT(*) FROM learner_profiles;"),
                    QueryLong(database, "SELECT COUNT(*) FROM gameplay_sessions;"),
                    QueryLong(database, "SELECT COUNT(*) FROM question_attempts;"),
                    QueryLong(database, "SELECT COUNT(*) FROM level_results;"),
                    true);
            }
            catch (Exception exception)
            {
                HandleFailure("read the local data summary", exception);
                return new LocalDataSummary(DatabasePath, 0, 0, 0, 0, false);
            }
            finally
            {
                CloseDatabase(database);
            }
        }
    }

    public static LearningPipelineSkillSummary[] GetLearningPipelineSkillSummaries(string playerId)
    {
        lock (SyncRoot)
        {
            if (!EnsureReady() || string.IsNullOrWhiteSpace(playerId))
                return Array.Empty<LearningPipelineSkillSummary>();

            IntPtr database = IntPtr.Zero;
            try
            {
                database = OpenDatabase();
                // Gameplay rows use the local PlayerPrefs identifier exactly as created.
                // UUID normalization is only required for records stored in the cloud cache.
                string localPlayerId = playerId.Trim();
                List<LearningPipelineSkillSummary> rows = ReadRowsWithValues(
                    database,
                    @"WITH target(player_id) AS (VALUES (?)),
                      skills AS (
                          SELECT DISTINCT qa.skill
                          FROM question_attempts AS qa, target
                          WHERE qa.player_id = target.player_id
                          UNION
                          SELECT DISTINCT gs.skill
                          FROM gameplay_sessions AS gs, target
                          WHERE gs.player_id = target.player_id
                      )
                      SELECT
                          skills.skill,
                          (SELECT COUNT(*) FROM question_attempts AS qa, target
                           WHERE qa.player_id = target.player_id
                             AND qa.skill = skills.skill) AS attempt_count,
                          (SELECT COUNT(*) FROM gameplay_sessions AS gs, target
                           WHERE gs.player_id = target.player_id
                             AND gs.skill = skills.skill
                             AND gs.completion_status IN ('completed', 'failed')
                             AND gs.ended_at_utc IS NOT NULL
                             AND gs.attempted_questions > 0
                             AND gs.score_percent IS NOT NULL) AS scored_session_count,
                          COALESCE((SELECT MAX(gs.ended_at_utc)
                           FROM gameplay_sessions AS gs, target
                           WHERE gs.player_id = target.player_id
                             AND gs.skill = skills.skill
                             AND gs.completion_status IN ('completed', 'failed')), '')
                              AS last_scored_session_utc
                      FROM skills
                      ORDER BY skills.skill;",
                    statement => new LearningPipelineSkillSummary(
                        ColumnText(statement, 0),
                        ColumnInt(statement, 1),
                        ColumnInt(statement, 2),
                        ColumnText(statement, 3)),
                    localPlayerId);
                return rows.ToArray();
            }
            catch (Exception exception)
            {
                HandleFailure("read Phase 6 learning-pipeline summary", exception);
                return Array.Empty<LearningPipelineSkillSummary>();
            }
            finally
            {
                CloseDatabase(database);
            }
        }
    }

    public static long GetPendingSyncRecordCount()
    {
        lock (SyncRoot)
        {
            if (!EnsureReady())
                return -1;

            IntPtr database = IntPtr.Zero;
            try
            {
                database = OpenDatabase();
                return QueryLong(database,
                    @"SELECT
                        (SELECT COUNT(*) FROM learner_profiles WHERE sync_status = 'pending') +
                        (SELECT COUNT(*) FROM gameplay_sessions WHERE sync_status = 'pending') +
                        (SELECT COUNT(*) FROM question_attempts WHERE sync_status = 'pending') +
                        (SELECT COUNT(*) FROM level_results WHERE sync_status = 'pending');");
            }
            catch (Exception exception)
            {
                HandleFailure("count pending cloud-sync records", exception);
                return -1;
            }
            finally
            {
                CloseDatabase(database);
            }
        }
    }

    public static CloudSyncBatch GetPendingCloudSyncBatch(int maximumRowsPerTable)
    {
        lock (SyncRoot)
        {
            if (!EnsureReady())
                return null;

            int limit = Mathf.Clamp(maximumRowsPerTable, 1, 100);
            IntPtr database = IntPtr.Zero;
            try
            {
                database = OpenDatabase();
                string limitSql = " LIMIT " + limit + ";";

                List<CloudLearnerProfile> profiles = ReadRows(
                    database,
                    @"SELECT player_id, display_name, age, wizard_index,
                             created_at_utc, updated_at_utc, sync_revision
                      FROM learner_profiles
                      WHERE sync_status = 'pending'
                      ORDER BY updated_at_utc" + limitSql,
                    statement => new CloudLearnerProfile
                    {
                        player_id = NormalizeUuid(ColumnText(statement, 0)),
                        display_name = ColumnText(statement, 1),
                        age = ColumnInt(statement, 2),
                        wizard_index = ColumnInt(statement, 3),
                        created_at_utc = ColumnText(statement, 4),
                        updated_at_utc = ColumnText(statement, 5),
                        client_revision = ColumnLong(statement, 6)
                    });

                List<CloudGameplaySession> sessions = ReadRows(
                    database,
                    @"SELECT session_id, player_id, activity, skill, level,
                             started_at_utc, ended_at_utc, total_questions,
                             attempted_questions, correct_answers, score_percent,
                             passed, lives_remaining, completion_status,
                             installation_id, app_version, sync_revision
                      FROM gameplay_sessions
                      WHERE sync_status = 'pending'
                      ORDER BY started_at_utc" + limitSql,
                    statement => new CloudGameplaySession
                    {
                        session_id = NormalizeUuid(ColumnText(statement, 0)),
                        player_id = NormalizeUuid(ColumnText(statement, 1)),
                        activity = ColumnText(statement, 2),
                        skill = ColumnText(statement, 3),
                        level = ColumnInt(statement, 4),
                        started_at_utc = ColumnText(statement, 5),
                        ended_at_utc = ColumnText(statement, 6),
                        total_questions = ColumnInt(statement, 7),
                        attempted_questions = ColumnInt(statement, 8),
                        correct_answers = ColumnInt(statement, 9),
                        score_percent = ColumnNullableInt(statement, 10, -1),
                        passed = ColumnNullableInt(statement, 11, -1),
                        lives_remaining = ColumnInt(statement, 12),
                        completion_status = ColumnText(statement, 13),
                        installation_id = NormalizeUuid(ColumnText(statement, 14)),
                        app_version = ColumnText(statement, 15),
                        client_revision = ColumnLong(statement, 16)
                    });

                List<CloudQuestionAttempt> attempts = ReadRows(
                    database,
                    @"SELECT attempt_id, session_id, player_id, question_id,
                             activity, skill, level, difficulty, competency,
                             attempt_number, selected_answer_index, correct_answer_index,
                             is_correct, timed_out, response_time_ms, attempted_at_utc,
                             sync_revision
                      FROM question_attempts
                      WHERE sync_status = 'pending'
                      ORDER BY attempted_at_utc" + limitSql,
                    statement => new CloudQuestionAttempt
                    {
                        attempt_id = NormalizeUuid(ColumnText(statement, 0)),
                        session_id = NormalizeUuid(ColumnText(statement, 1)),
                        player_id = NormalizeUuid(ColumnText(statement, 2)),
                        question_id = ColumnText(statement, 3),
                        activity = ColumnText(statement, 4),
                        skill = ColumnText(statement, 5),
                        level = ColumnInt(statement, 6),
                        difficulty = ColumnInt(statement, 7),
                        competency = ColumnText(statement, 8),
                        attempt_number = ColumnInt(statement, 9),
                        selected_answer_index = ColumnNullableInt(statement, 10, -1),
                        correct_answer_index = ColumnInt(statement, 11),
                        is_correct = ColumnInt(statement, 12) != 0,
                        timed_out = ColumnInt(statement, 13) != 0,
                        response_time_ms = ColumnLong(statement, 14),
                        attempted_at_utc = ColumnText(statement, 15),
                        client_revision = ColumnLong(statement, 16)
                    });

                List<CloudLevelResult> results = ReadRows(
                    database,
                    @"SELECT result_id, session_id, player_id, activity, skill,
                             level, total_questions, attempted_questions,
                             correct_answers, score_percent, passed, lives_remaining,
                             completed_at_utc, sync_revision
                      FROM level_results
                      WHERE sync_status = 'pending'
                      ORDER BY completed_at_utc" + limitSql,
                    statement => new CloudLevelResult
                    {
                        result_id = NormalizeUuid(ColumnText(statement, 0)),
                        session_id = NormalizeUuid(ColumnText(statement, 1)),
                        player_id = NormalizeUuid(ColumnText(statement, 2)),
                        activity = ColumnText(statement, 3),
                        skill = ColumnText(statement, 4),
                        level = ColumnInt(statement, 5),
                        total_questions = ColumnInt(statement, 6),
                        attempted_questions = ColumnInt(statement, 7),
                        correct_answers = ColumnInt(statement, 8),
                        score_percent = ColumnInt(statement, 9),
                        passed = ColumnInt(statement, 10) != 0,
                        lives_remaining = ColumnInt(statement, 11),
                        completed_at_utc = ColumnText(statement, 12),
                        client_revision = ColumnLong(statement, 13)
                    });

                return new CloudSyncBatch
                {
                    profiles = profiles.ToArray(),
                    sessions = sessions.ToArray(),
                    attempts = attempts.ToArray(),
                    results = results.ToArray()
                };
            }
            catch (Exception exception)
            {
                HandleFailure("read pending cloud-sync records", exception);
                return null;
            }
            finally
            {
                CloseDatabase(database);
            }
        }
    }

    public static bool MarkCloudSyncBatchSynced(CloudSyncBatch batch, string syncedAtUtc)
    {
        if (batch == null)
            return false;

        lock (SyncRoot)
        {
            if (!EnsureReady())
                return false;

            IntPtr database = IntPtr.Zero;
            try
            {
                database = OpenDatabase();
                ExecuteSql(database, "BEGIN IMMEDIATE;");

                foreach (CloudLearnerProfile row in batch.profiles)
                    MarkRowSynced(database, "learner_profiles", "player_id", row.player_id, row.client_revision, syncedAtUtc);
                foreach (CloudGameplaySession row in batch.sessions)
                    MarkRowSynced(database, "gameplay_sessions", "session_id", row.session_id, row.client_revision, syncedAtUtc);
                foreach (CloudQuestionAttempt row in batch.attempts)
                    MarkRowSynced(database, "question_attempts", "attempt_id", row.attempt_id, row.client_revision, syncedAtUtc);
                foreach (CloudLevelResult row in batch.results)
                    MarkRowSynced(database, "level_results", "result_id", row.result_id, row.client_revision, syncedAtUtc);

                ExecuteSql(database, "COMMIT;");
                return true;
            }
            catch (Exception exception)
            {
                if (database != IntPtr.Zero)
                {
                    try { ExecuteSql(database, "ROLLBACK;"); }
                    catch { /* Preserve the original error. */ }
                }

                HandleFailure("mark cloud-sync records as synced", exception);
                return false;
            }
            finally
            {
                CloseDatabase(database);
            }
        }
    }

    public static bool ReplaceForecastCache(
        string playerId,
        IReadOnlyList<LearnerForecastRecord> forecasts,
        string cachedAtUtc)
    {
        lock (SyncRoot)
        {
            if (!EnsureReady() || string.IsNullOrWhiteSpace(playerId))
                return false;

            string normalizedPlayerId = NormalizeUuid(playerId);
            IntPtr database = IntPtr.Zero;
            try
            {
                database = OpenDatabase();
                ExecuteSql(database, "BEGIN IMMEDIATE;");
                ExecutePrepared(database,
                    "DELETE FROM learner_forecast_cache WHERE player_id = ?;",
                    normalizedPlayerId);

                if (forecasts != null)
                {
                    foreach (LearnerForecastRecord row in forecasts)
                    {
                        if (row == null || string.IsNullOrWhiteSpace(row.forecast_id) ||
                            !string.Equals(NormalizeUuid(row.player_id), normalizedPlayerId,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        const string insertSql = @"
INSERT INTO learner_forecast_cache (
    forecast_id, player_id, skill, model_version, generated_at, valid_until,
    source_attempt_count, expected_score_percent, mastery_probability,
    confidence, trend_label, risk_level, recommended_level, recommendation,
    cached_at_utc
)
VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
ON CONFLICT(player_id, skill) DO UPDATE SET
    forecast_id = excluded.forecast_id,
    model_version = excluded.model_version,
    generated_at = excluded.generated_at,
    valid_until = excluded.valid_until,
    source_attempt_count = excluded.source_attempt_count,
    expected_score_percent = excluded.expected_score_percent,
    mastery_probability = excluded.mastery_probability,
    confidence = excluded.confidence,
    trend_label = excluded.trend_label,
    risk_level = excluded.risk_level,
    recommended_level = excluded.recommended_level,
    recommendation = excluded.recommendation,
    cached_at_utc = excluded.cached_at_utc;";

                        ExecutePrepared(database, insertSql,
                            row.forecast_id,
                            normalizedPlayerId,
                            row.skill ?? string.Empty,
                            row.model_version ?? string.Empty,
                            row.generated_at ?? string.Empty,
                            row.valid_until ?? string.Empty,
                            Math.Max(0, row.source_attempt_count),
                            Mathf.Clamp(row.expected_score_percent, 0f, 100f),
                            Mathf.Clamp01(row.mastery_probability),
                            Mathf.Clamp01(row.confidence),
                            row.trend_label ?? "stable",
                            row.risk_level ?? "medium",
                            Mathf.Clamp(row.recommended_level, 1, 5),
                            row.recommendation ?? string.Empty,
                            cachedAtUtc ?? UtcNow());
                    }
                }

                ExecuteSql(database, "COMMIT;");
                return true;
            }
            catch (Exception exception)
            {
                if (database != IntPtr.Zero)
                {
                    try { ExecuteSql(database, "ROLLBACK;"); }
                    catch { /* Preserve the original database error. */ }
                }

                HandleFailure("cache learner forecasts", exception);
                return false;
            }
            finally
            {
                CloseDatabase(database);
            }
        }
    }

    public static bool ReplaceForecastReadinessCache(
        string playerId,
        IReadOnlyList<ForecastReadinessRecord> readinessRows,
        string cachedAtUtc)
    {
        lock (SyncRoot)
        {
            if (!EnsureReady() || string.IsNullOrWhiteSpace(playerId))
                return false;

            string normalizedPlayerId = NormalizeUuid(playerId);
            IntPtr database = IntPtr.Zero;
            try
            {
                database = OpenDatabase();
                ExecuteSql(database, "BEGIN IMMEDIATE;");
                ExecutePrepared(database,
                    "DELETE FROM forecast_readiness_cache WHERE player_id = ?;",
                    normalizedPlayerId);

                if (readinessRows != null)
                {
                    foreach (ForecastReadinessRecord row in readinessRows)
                    {
                        if (row == null || string.IsNullOrWhiteSpace(row.skill) ||
                            !string.Equals(NormalizeUuid(row.player_id), normalizedPlayerId,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        const string insertSql = @"
INSERT INTO forecast_readiness_cache (
    player_id, skill, total_attempts, total_sessions, is_forecast_eligible,
    attempts_needed, data_quality_status, observed_trend,
    forecast_session_count, sessions_needed, cached_at_utc
)
VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
ON CONFLICT(player_id, skill) DO UPDATE SET
    total_attempts = excluded.total_attempts,
    total_sessions = excluded.total_sessions,
    is_forecast_eligible = excluded.is_forecast_eligible,
    attempts_needed = excluded.attempts_needed,
    data_quality_status = excluded.data_quality_status,
    observed_trend = excluded.observed_trend,
    forecast_session_count = excluded.forecast_session_count,
    sessions_needed = excluded.sessions_needed,
    cached_at_utc = excluded.cached_at_utc;";

                        ExecutePrepared(database, insertSql,
                            normalizedPlayerId,
                            row.skill,
                            Math.Max(0, row.total_attempts),
                            Math.Max(0, row.total_sessions),
                            row.is_forecast_eligible,
                            Math.Max(0, row.attempts_needed),
                            row.data_quality_status ?? string.Empty,
                            row.observed_trend ?? "insufficient_history",
                            Math.Max(0, row.forecast_session_count),
                            Math.Max(0, row.sessions_needed),
                            cachedAtUtc ?? UtcNow());
                    }
                }

                ExecuteSql(database, "COMMIT;");
                return true;
            }
            catch (Exception exception)
            {
                if (database != IntPtr.Zero)
                {
                    try { ExecuteSql(database, "ROLLBACK;"); }
                    catch { /* Preserve the original database error. */ }
                }

                HandleFailure("cache forecast readiness", exception);
                return false;
            }
            finally
            {
                CloseDatabase(database);
            }
        }
    }

    public static bool ReplaceForecastValidationCache(
        string playerId,
        IReadOnlyList<ForecastValidationRecord> validationRows,
        string cachedAtUtc)
    {
        lock (SyncRoot)
        {
            if (!EnsureReady() || string.IsNullOrWhiteSpace(playerId))
                return false;

            string normalizedPlayerId = NormalizeUuid(playerId);
            IntPtr database = IntPtr.Zero;
            try
            {
                database = OpenDatabase();
                ExecuteSql(database, "BEGIN IMMEDIATE;");
                ExecutePrepared(database,
                    "DELETE FROM forecast_validation_cache WHERE player_id = ?;",
                    normalizedPlayerId);

                if (validationRows != null)
                {
                    foreach (ForecastValidationRecord row in validationRows)
                    {
                        if (row == null || string.IsNullOrWhiteSpace(row.forecast_id) ||
                            !string.Equals(NormalizeUuid(row.player_id), normalizedPlayerId,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        const string insertSql = @"
INSERT INTO forecast_validation_cache (
    forecast_id, player_id, skill, model_version, generated_at,
    actual_session_id, actual_at, predicted_score_percent,
    actual_score_percent, signed_error, absolute_error, squared_error,
    baseline_score_percent, baseline_absolute_error, baseline_squared_error,
    has_baseline, cached_at_utc
)
VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
ON CONFLICT(forecast_id) DO UPDATE SET
    player_id = excluded.player_id,
    skill = excluded.skill,
    model_version = excluded.model_version,
    generated_at = excluded.generated_at,
    actual_session_id = excluded.actual_session_id,
    actual_at = excluded.actual_at,
    predicted_score_percent = excluded.predicted_score_percent,
    actual_score_percent = excluded.actual_score_percent,
    signed_error = excluded.signed_error,
    absolute_error = excluded.absolute_error,
    squared_error = excluded.squared_error,
    baseline_score_percent = excluded.baseline_score_percent,
    baseline_absolute_error = excluded.baseline_absolute_error,
    baseline_squared_error = excluded.baseline_squared_error,
    has_baseline = excluded.has_baseline,
    cached_at_utc = excluded.cached_at_utc;";

                        ExecutePrepared(database, insertSql,
                            row.forecast_id,
                            normalizedPlayerId,
                            row.skill ?? string.Empty,
                            row.model_version ?? string.Empty,
                            row.generated_at ?? string.Empty,
                            row.actual_session_id ?? string.Empty,
                            row.actual_at ?? string.Empty,
                            Mathf.Clamp(row.predicted_score_percent, 0f, 100f),
                            Mathf.Clamp(row.actual_score_percent, 0f, 100f),
                            row.signed_error,
                            Math.Max(0f, row.absolute_error),
                            Math.Max(0f, row.squared_error),
                            Mathf.Clamp(row.baseline_score_percent, 0f, 100f),
                            Math.Max(0f, row.baseline_absolute_error),
                            Math.Max(0f, row.baseline_squared_error),
                            row.has_baseline,
                            cachedAtUtc ?? UtcNow());
                    }
                }

                ExecuteSql(database, "COMMIT;");
                return true;
            }
            catch (Exception exception)
            {
                if (database != IntPtr.Zero)
                {
                    try { ExecuteSql(database, "ROLLBACK;"); }
                    catch { /* Preserve the original database error. */ }
                }

                HandleFailure("cache forecast validation results", exception);
                return false;
            }
            finally
            {
                CloseDatabase(database);
            }
        }
    }

    public static LearnerForecastRecord[] GetCachedForecasts(string playerId)
    {
        lock (SyncRoot)
        {
            if (!EnsureReady() || string.IsNullOrWhiteSpace(playerId))
                return Array.Empty<LearnerForecastRecord>();

            IntPtr database = IntPtr.Zero;
            try
            {
                database = OpenDatabase();
                string normalizedPlayerId = NormalizeUuid(playerId);
                List<LearnerForecastRecord> rows = ReadRowsWithValues(
                    database,
                    @"SELECT forecast_id, player_id, skill, model_version,
                             generated_at, valid_until, source_attempt_count,
                             expected_score_percent, mastery_probability, confidence,
                             trend_label, risk_level, recommended_level, recommendation,
                             cached_at_utc
                      FROM learner_forecast_cache
                      WHERE player_id = ?
                      ORDER BY skill;",
                    statement => new LearnerForecastRecord
                    {
                        forecast_id = ColumnText(statement, 0),
                        player_id = ColumnText(statement, 1),
                        skill = ColumnText(statement, 2),
                        model_version = ColumnText(statement, 3),
                        generated_at = ColumnText(statement, 4),
                        valid_until = ColumnText(statement, 5),
                        source_attempt_count = ColumnInt(statement, 6),
                        expected_score_percent = (float)ColumnDouble(statement, 7),
                        mastery_probability = (float)ColumnDouble(statement, 8),
                        confidence = (float)ColumnDouble(statement, 9),
                        trend_label = ColumnText(statement, 10),
                        risk_level = ColumnText(statement, 11),
                        recommended_level = ColumnInt(statement, 12),
                        recommendation = ColumnText(statement, 13),
                        cached_at_utc = ColumnText(statement, 14)
                    },
                    normalizedPlayerId);
                return rows.ToArray();
            }
            catch (Exception exception)
            {
                HandleFailure("read cached learner forecasts", exception);
                return Array.Empty<LearnerForecastRecord>();
            }
            finally
            {
                CloseDatabase(database);
            }
        }
    }

    public static ForecastReadinessRecord[] GetCachedForecastReadiness(string playerId)
    {
        lock (SyncRoot)
        {
            if (!EnsureReady() || string.IsNullOrWhiteSpace(playerId))
                return Array.Empty<ForecastReadinessRecord>();

            IntPtr database = IntPtr.Zero;
            try
            {
                database = OpenDatabase();
                string normalizedPlayerId = NormalizeUuid(playerId);
                List<ForecastReadinessRecord> rows = ReadRowsWithValues(
                    database,
                    @"SELECT player_id, skill, total_attempts, total_sessions,
                             is_forecast_eligible, attempts_needed,
                             data_quality_status, observed_trend,
                             forecast_session_count, sessions_needed, cached_at_utc
                      FROM forecast_readiness_cache
                      WHERE player_id = ?
                      ORDER BY skill;",
                    statement => new ForecastReadinessRecord
                    {
                        player_id = ColumnText(statement, 0),
                        skill = ColumnText(statement, 1),
                        total_attempts = ColumnInt(statement, 2),
                        total_sessions = ColumnInt(statement, 3),
                        is_forecast_eligible = ColumnInt(statement, 4) != 0,
                        attempts_needed = ColumnInt(statement, 5),
                        data_quality_status = ColumnText(statement, 6),
                        observed_trend = ColumnText(statement, 7),
                        forecast_session_count = ColumnInt(statement, 8),
                        sessions_needed = ColumnInt(statement, 9),
                        cached_at_utc = ColumnText(statement, 10)
                    },
                    normalizedPlayerId);
                return rows.ToArray();
            }
            catch (Exception exception)
            {
                HandleFailure("read cached forecast readiness", exception);
                return Array.Empty<ForecastReadinessRecord>();
            }
            finally
            {
                CloseDatabase(database);
            }
        }
    }

    public static ForecastValidationRecord[] GetCachedForecastValidation(string playerId)
    {
        lock (SyncRoot)
        {
            if (!EnsureReady() || string.IsNullOrWhiteSpace(playerId))
                return Array.Empty<ForecastValidationRecord>();

            IntPtr database = IntPtr.Zero;
            try
            {
                database = OpenDatabase();
                string normalizedPlayerId = NormalizeUuid(playerId);
                List<ForecastValidationRecord> rows = ReadRowsWithValues(
                    database,
                    @"SELECT forecast_id, player_id, skill, model_version,
                             generated_at, actual_session_id, actual_at,
                             predicted_score_percent, actual_score_percent,
                             signed_error, absolute_error, squared_error,
                             baseline_score_percent, baseline_absolute_error,
                             baseline_squared_error, has_baseline, cached_at_utc
                      FROM forecast_validation_cache
                      WHERE player_id = ?
                      ORDER BY actual_at ASC, forecast_id ASC;",
                    statement => new ForecastValidationRecord
                    {
                        forecast_id = ColumnText(statement, 0),
                        player_id = ColumnText(statement, 1),
                        skill = ColumnText(statement, 2),
                        model_version = ColumnText(statement, 3),
                        generated_at = ColumnText(statement, 4),
                        actual_session_id = ColumnText(statement, 5),
                        actual_at = ColumnText(statement, 6),
                        predicted_score_percent = (float)ColumnDouble(statement, 7),
                        actual_score_percent = (float)ColumnDouble(statement, 8),
                        signed_error = (float)ColumnDouble(statement, 9),
                        absolute_error = (float)ColumnDouble(statement, 10),
                        squared_error = (float)ColumnDouble(statement, 11),
                        baseline_score_percent = (float)ColumnDouble(statement, 12),
                        baseline_absolute_error = (float)ColumnDouble(statement, 13),
                        baseline_squared_error = (float)ColumnDouble(statement, 14),
                        has_baseline = ColumnInt(statement, 15) != 0,
                        cached_at_utc = ColumnText(statement, 16)
                    },
                    normalizedPlayerId);
                return rows.ToArray();
            }
            catch (Exception exception)
            {
                HandleFailure("read cached forecast validation results", exception);
                return Array.Empty<ForecastValidationRecord>();
            }
            finally
            {
                CloseDatabase(database);
            }
        }
    }

    public static bool SaveEvaluationResponse(EvaluationResponseRecord response)
    {
        lock (SyncRoot)
        {
            if (!EnsureReady() || response == null || string.IsNullOrWhiteSpace(response.response_id))
                return false;

            const string sql = @"
INSERT INTO evaluation_responses (
    response_id, instrument, respondent_role, ratings_json,
    category_scores_json, overall_mean, interpretation, comments,
    completed_at_utc, app_version, platform, sync_status, synced_at_utc
)
VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'pending', NULL)
ON CONFLICT(response_id) DO NOTHING;";

            return ExecuteNonQuery(sql,
                NormalizeUuid(response.response_id),
                response.instrument ?? string.Empty,
                response.respondent_role ?? string.Empty,
                response.ratings_json ?? "{\"items\":[]}",
                response.category_scores_json ?? "{\"items\":[]}",
                Mathf.Clamp(response.overall_mean, 1f, 5f),
                response.interpretation ?? string.Empty,
                response.comments ?? string.Empty,
                response.completed_at_utc ?? UtcNow(),
                response.app_version ?? Application.version,
                response.platform ?? Application.platform.ToString());
        }
    }

    public static EvaluationResponseRecord[] GetPendingEvaluationResponses(int maximumRows)
    {
        lock (SyncRoot)
        {
            if (!EnsureReady())
                return Array.Empty<EvaluationResponseRecord>();

            int limit = Mathf.Clamp(maximumRows, 1, 100);
            IntPtr database = IntPtr.Zero;
            try
            {
                database = OpenDatabase();
                List<EvaluationResponseRecord> rows = ReadRows(
                    database,
                    @"SELECT response_id, instrument, respondent_role, ratings_json,
                             category_scores_json, overall_mean, interpretation,
                             comments, completed_at_utc, app_version, platform,
                             sync_status, COALESCE(synced_at_utc, '')
                      FROM evaluation_responses
                      WHERE sync_status = 'pending'
                      ORDER BY completed_at_utc
                      LIMIT " + limit + ";",
                    statement => new EvaluationResponseRecord
                    {
                        response_id = ColumnText(statement, 0),
                        instrument = ColumnText(statement, 1),
                        respondent_role = ColumnText(statement, 2),
                        ratings_json = ColumnText(statement, 3),
                        category_scores_json = ColumnText(statement, 4),
                        overall_mean = (float)ColumnDouble(statement, 5),
                        interpretation = ColumnText(statement, 6),
                        comments = ColumnText(statement, 7),
                        completed_at_utc = ColumnText(statement, 8),
                        app_version = ColumnText(statement, 9),
                        platform = ColumnText(statement, 10),
                        sync_status = ColumnText(statement, 11),
                        synced_at_utc = ColumnText(statement, 12)
                    });
                return rows.ToArray();
            }
            catch (Exception exception)
            {
                HandleFailure("read pending evaluation responses", exception);
                return Array.Empty<EvaluationResponseRecord>();
            }
            finally
            {
                CloseDatabase(database);
            }
        }
    }

    public static bool MarkEvaluationResponsesSynced(
        IReadOnlyList<EvaluationResponseRecord> responses,
        string syncedAtUtc)
    {
        lock (SyncRoot)
        {
            if (!EnsureReady() || responses == null)
                return false;

            IntPtr database = IntPtr.Zero;
            try
            {
                database = OpenDatabase();
                ExecuteSql(database, "BEGIN IMMEDIATE;");
                foreach (EvaluationResponseRecord response in responses)
                {
                    if (response == null || string.IsNullOrWhiteSpace(response.response_id))
                        continue;
                    ExecutePrepared(database,
                        @"UPDATE evaluation_responses
                          SET sync_status = 'synced', synced_at_utc = ?
                          WHERE response_id = ? AND sync_status = 'pending';",
                        syncedAtUtc ?? UtcNow(), NormalizeUuid(response.response_id));
                }
                ExecuteSql(database, "COMMIT;");
                return true;
            }
            catch (Exception exception)
            {
                if (database != IntPtr.Zero)
                {
                    try { ExecuteSql(database, "ROLLBACK;"); }
                    catch { /* Preserve the original database error. */ }
                }
                HandleFailure("mark evaluation responses as synced", exception);
                return false;
            }
            finally
            {
                CloseDatabase(database);
            }
        }
    }

    public static long GetEvaluationResponseCount()
    {
        lock (SyncRoot)
        {
            if (!EnsureReady())
                return -1;

            IntPtr database = IntPtr.Zero;
            try
            {
                database = OpenDatabase();
                return QueryLong(database, "SELECT COUNT(*) FROM evaluation_responses;");
            }
            catch (Exception exception)
            {
                HandleFailure("count evaluation responses", exception);
                return -1;
            }
            finally
            {
                CloseDatabase(database);
            }
        }
    }

    public static string GetOrCreateCurrentPlayerId()
    {
        lock (SyncRoot)
            return GetOrCreatePlayerPrefId(PlayerIdKey);
    }

    public static long RecordAndroidCheckpointLaunch()
    {
        lock (SyncRoot)
        {
            // Both the original Android checkpoint and the Phase 6B QA service run
            // before the first scene. Count the process launch once regardless of
            // which bootstrap callback executes first.
            if (androidLaunchRecorded)
                return androidLaunchCountThisProcess;

            if (!EnsureReady())
                return 0;

            IntPtr database = IntPtr.Zero;
            try
            {
                database = OpenDatabase();
                const string updateSql = @"
INSERT INTO app_metadata(key, value)
VALUES ('android_checkpoint_launch_count', '1')
ON CONFLICT(key) DO UPDATE SET
    value = CAST(value AS INTEGER) + 1;";
                ExecutePrepared(database, updateSql);
                androidLaunchCountThisProcess = QueryLong(
                    database,
                    "SELECT CAST(value AS INTEGER) FROM app_metadata WHERE key = 'android_checkpoint_launch_count';");
                androidLaunchRecorded = true;
                return androidLaunchCountThisProcess;
            }
            catch (Exception exception)
            {
                HandleFailure("record the Android persistence checkpoint", exception);
                return 0;
            }
            finally
            {
                CloseDatabase(database);
            }
        }
    }

    private static bool EnsureReady()
    {
        if (initialized)
            return true;
        if (disabledForSession)
            return false;

        IntPtr database = IntPtr.Zero;
        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            database = OpenDatabase();
            ExecuteSql(database, "PRAGMA journal_mode=WAL;");
            ExecuteSql(database, "PRAGMA foreign_keys=ON;");
            ExecuteSql(database, "PRAGMA busy_timeout=5000;");
            CreateSchema(database);
            initialized = true;
            return true;
        }
        catch (Exception exception)
        {
            disabledForSession = true;
            HandleFailure("initialize the local database", exception);
            return false;
        }
        finally
        {
            CloseDatabase(database);
        }
    }

    private static void CreateSchema(IntPtr database)
    {
        const string schema = @"
BEGIN IMMEDIATE;

CREATE TABLE IF NOT EXISTS app_metadata (
    key TEXT PRIMARY KEY NOT NULL,
    value TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS learner_profiles (
    player_id TEXT PRIMARY KEY NOT NULL,
    display_name TEXT NOT NULL,
    age INTEGER NOT NULL DEFAULT 0,
    birthday_text TEXT NOT NULL DEFAULT '',
    wizard_index INTEGER NOT NULL DEFAULT 0,
    created_at_utc TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL,
    sync_status TEXT NOT NULL DEFAULT 'pending',
    synced_at_utc TEXT NULL,
    remote_id TEXT NULL,
    sync_revision INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS gameplay_sessions (
    session_id TEXT PRIMARY KEY NOT NULL,
    player_id TEXT NOT NULL,
    activity TEXT NOT NULL,
    skill TEXT NOT NULL,
    level INTEGER NOT NULL,
    started_at_utc TEXT NOT NULL,
    ended_at_utc TEXT NULL,
    total_questions INTEGER NOT NULL,
    attempted_questions INTEGER NOT NULL DEFAULT 0,
    correct_answers INTEGER NOT NULL DEFAULT 0,
    score_percent INTEGER NULL,
    passed INTEGER NULL,
    lives_remaining INTEGER NOT NULL DEFAULT 3,
    completion_status TEXT NOT NULL DEFAULT 'in_progress',
    installation_id TEXT NOT NULL,
    app_version TEXT NOT NULL,
    sync_status TEXT NOT NULL DEFAULT 'pending',
    synced_at_utc TEXT NULL,
    remote_id TEXT NULL,
    sync_revision INTEGER NOT NULL DEFAULT 1,
    FOREIGN KEY(player_id) REFERENCES learner_profiles(player_id)
);

CREATE TABLE IF NOT EXISTS question_attempts (
    attempt_id TEXT PRIMARY KEY NOT NULL,
    session_id TEXT NOT NULL,
    player_id TEXT NOT NULL,
    question_id TEXT NOT NULL,
    activity TEXT NOT NULL,
    skill TEXT NOT NULL,
    level INTEGER NOT NULL,
    difficulty INTEGER NOT NULL,
    competency TEXT NOT NULL DEFAULT '',
    attempt_number INTEGER NOT NULL,
    selected_answer_index INTEGER NULL,
    correct_answer_index INTEGER NOT NULL,
    is_correct INTEGER NOT NULL,
    timed_out INTEGER NOT NULL DEFAULT 0,
    response_time_ms INTEGER NOT NULL,
    attempted_at_utc TEXT NOT NULL,
    sync_status TEXT NOT NULL DEFAULT 'pending',
    synced_at_utc TEXT NULL,
    remote_id TEXT NULL,
    sync_revision INTEGER NOT NULL DEFAULT 1,
    FOREIGN KEY(session_id) REFERENCES gameplay_sessions(session_id) ON DELETE CASCADE,
    FOREIGN KEY(player_id) REFERENCES learner_profiles(player_id)
);

CREATE TABLE IF NOT EXISTS level_results (
    result_id TEXT PRIMARY KEY NOT NULL,
    session_id TEXT NOT NULL UNIQUE,
    player_id TEXT NOT NULL,
    activity TEXT NOT NULL,
    skill TEXT NOT NULL,
    level INTEGER NOT NULL,
    total_questions INTEGER NOT NULL,
    attempted_questions INTEGER NOT NULL,
    correct_answers INTEGER NOT NULL,
    score_percent INTEGER NOT NULL,
    passed INTEGER NOT NULL,
    lives_remaining INTEGER NOT NULL,
    completed_at_utc TEXT NOT NULL,
    sync_status TEXT NOT NULL DEFAULT 'pending',
    synced_at_utc TEXT NULL,
    remote_id TEXT NULL,
    sync_revision INTEGER NOT NULL DEFAULT 1,
    FOREIGN KEY(session_id) REFERENCES gameplay_sessions(session_id) ON DELETE CASCADE,
    FOREIGN KEY(player_id) REFERENCES learner_profiles(player_id)
);

CREATE TABLE IF NOT EXISTS learner_forecast_cache (
    forecast_id TEXT PRIMARY KEY NOT NULL,
    player_id TEXT NOT NULL,
    skill TEXT NOT NULL,
    model_version TEXT NOT NULL,
    generated_at TEXT NOT NULL,
    valid_until TEXT NOT NULL,
    source_attempt_count INTEGER NOT NULL,
    expected_score_percent REAL NOT NULL,
    mastery_probability REAL NOT NULL,
    confidence REAL NOT NULL,
    trend_label TEXT NOT NULL,
    risk_level TEXT NOT NULL,
    recommended_level INTEGER NOT NULL,
    recommendation TEXT NOT NULL,
    cached_at_utc TEXT NOT NULL,
    UNIQUE(player_id, skill)
);

CREATE TABLE IF NOT EXISTS forecast_readiness_cache (
    player_id TEXT NOT NULL,
    skill TEXT NOT NULL,
    total_attempts INTEGER NOT NULL,
    total_sessions INTEGER NOT NULL,
    is_forecast_eligible INTEGER NOT NULL,
    attempts_needed INTEGER NOT NULL,
    data_quality_status TEXT NOT NULL,
    observed_trend TEXT NOT NULL,
    forecast_session_count INTEGER NOT NULL DEFAULT 0,
    sessions_needed INTEGER NOT NULL DEFAULT 5,
    cached_at_utc TEXT NOT NULL,
    PRIMARY KEY(player_id, skill)
);

CREATE TABLE IF NOT EXISTS forecast_validation_cache (
    forecast_id TEXT PRIMARY KEY NOT NULL,
    player_id TEXT NOT NULL,
    skill TEXT NOT NULL,
    model_version TEXT NOT NULL,
    generated_at TEXT NOT NULL,
    actual_session_id TEXT NOT NULL,
    actual_at TEXT NOT NULL,
    predicted_score_percent REAL NOT NULL,
    actual_score_percent REAL NOT NULL,
    signed_error REAL NOT NULL,
    absolute_error REAL NOT NULL,
    squared_error REAL NOT NULL,
    baseline_score_percent REAL NOT NULL DEFAULT 0,
    baseline_absolute_error REAL NOT NULL DEFAULT 0,
    baseline_squared_error REAL NOT NULL DEFAULT 0,
    has_baseline INTEGER NOT NULL DEFAULT 0,
    cached_at_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS evaluation_responses (
    response_id TEXT PRIMARY KEY NOT NULL,
    instrument TEXT NOT NULL,
    respondent_role TEXT NOT NULL,
    ratings_json TEXT NOT NULL,
    category_scores_json TEXT NOT NULL,
    overall_mean REAL NOT NULL,
    interpretation TEXT NOT NULL,
    comments TEXT NOT NULL DEFAULT '',
    completed_at_utc TEXT NOT NULL,
    app_version TEXT NOT NULL,
    platform TEXT NOT NULL,
    sync_status TEXT NOT NULL DEFAULT 'pending',
    synced_at_utc TEXT NULL
);

CREATE INDEX IF NOT EXISTS idx_sessions_player_started
    ON gameplay_sessions(player_id, started_at_utc);
CREATE INDEX IF NOT EXISTS idx_attempts_player_time
    ON question_attempts(player_id, attempted_at_utc);
CREATE INDEX IF NOT EXISTS idx_attempts_skill_level
    ON question_attempts(skill, level, attempted_at_utc);
CREATE INDEX IF NOT EXISTS idx_attempts_question
    ON question_attempts(question_id, attempted_at_utc);
CREATE INDEX IF NOT EXISTS idx_results_player_time
    ON level_results(player_id, completed_at_utc);
CREATE INDEX IF NOT EXISTS idx_profiles_sync
    ON learner_profiles(sync_status);
CREATE INDEX IF NOT EXISTS idx_sessions_sync
    ON gameplay_sessions(sync_status);
CREATE INDEX IF NOT EXISTS idx_attempts_sync
    ON question_attempts(sync_status);
CREATE INDEX IF NOT EXISTS idx_results_sync
    ON level_results(sync_status);
CREATE INDEX IF NOT EXISTS idx_forecasts_player_skill
    ON learner_forecast_cache(player_id, skill);
CREATE INDEX IF NOT EXISTS idx_forecast_readiness_player_skill
    ON forecast_readiness_cache(player_id, skill);
CREATE INDEX IF NOT EXISTS idx_forecast_validation_player_time
    ON forecast_validation_cache(player_id, actual_at);
CREATE INDEX IF NOT EXISTS idx_evaluation_responses_sync
    ON evaluation_responses(sync_status, completed_at_utc);

COMMIT;";

        ExecuteSql(database, schema);
        EnsureColumn(database, "learner_profiles", "sync_revision", "INTEGER NOT NULL DEFAULT 1");
        EnsureColumn(database, "gameplay_sessions", "sync_revision", "INTEGER NOT NULL DEFAULT 1");
        EnsureColumn(database, "question_attempts", "sync_revision", "INTEGER NOT NULL DEFAULT 1");
        EnsureColumn(database, "level_results", "sync_revision", "INTEGER NOT NULL DEFAULT 1");
        EnsureColumn(database, "forecast_readiness_cache", "forecast_session_count", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(database, "forecast_readiness_cache", "sessions_needed", "INTEGER NOT NULL DEFAULT 5");
        ExecutePrepared(
            database,
            @"INSERT INTO app_metadata(key, value)
              VALUES ('schema_version', ?)
              ON CONFLICT(key) DO UPDATE SET value = excluded.value;",
            CurrentSchemaVersion);
    }

    private static bool ExecuteNonQuery(string sql, params object[] values)
    {
        IntPtr database = IntPtr.Zero;
        try
        {
            database = OpenDatabase();
            ExecutePrepared(database, sql, values);
            return true;
        }
        catch (Exception exception)
        {
            HandleFailure("write local learning data", exception);
            return false;
        }
        finally
        {
            CloseDatabase(database);
        }
    }

    private static IntPtr OpenDatabase()
    {
        byte[] path = ToUtf8(DatabasePath);
        int result = NativeSqlite.sqlite3_open_v2(
            path,
            out IntPtr database,
            NativeSqlite.OpenReadWrite | NativeSqlite.OpenCreate | NativeSqlite.OpenFullMutex,
            IntPtr.Zero);

        if (result != NativeSqlite.Ok)
        {
            string message = database == IntPtr.Zero ? "Unknown SQLite open error." : GetError(database);
            CloseDatabase(database);
            throw new InvalidOperationException(message);
        }

        NativeSqlite.sqlite3_busy_timeout(database, 5000);
        ExecuteSql(database, "PRAGMA foreign_keys=ON;");
        return database;
    }

    private static void CloseDatabase(IntPtr database)
    {
        if (database != IntPtr.Zero)
            NativeSqlite.sqlite3_close_v2(database);
    }

    private static void ExecuteSql(IntPtr database, string sql)
    {
        int result = NativeSqlite.sqlite3_exec(database, ToUtf8(sql), IntPtr.Zero, IntPtr.Zero, out IntPtr error);
        if (result == NativeSqlite.Ok)
            return;

        string message = error == IntPtr.Zero ? GetError(database) : PtrToUtf8(error);
        if (error != IntPtr.Zero)
            NativeSqlite.sqlite3_free(error);
        throw new InvalidOperationException(message);
    }

    private static int ExecutePrepared(IntPtr database, string sql, params object[] values)
    {
        IntPtr statement = Prepare(database, sql);
        try
        {
            BindValues(database, statement, values);
            int result = NativeSqlite.sqlite3_step(statement);
            if (result != NativeSqlite.Done)
                throw new InvalidOperationException(GetError(database));
            return NativeSqlite.sqlite3_changes(database);
        }
        finally
        {
            NativeSqlite.sqlite3_finalize(statement);
        }
    }

    private static long QueryLong(IntPtr database, string sql)
    {
        IntPtr statement = Prepare(database, sql);
        try
        {
            int result = NativeSqlite.sqlite3_step(statement);
            if (result != NativeSqlite.Row)
                throw new InvalidOperationException(GetError(database));
            return NativeSqlite.sqlite3_column_int64(statement, 0);
        }
        finally
        {
            NativeSqlite.sqlite3_finalize(statement);
        }
    }

    private static List<T> ReadRows<T>(IntPtr database, string sql, Func<IntPtr, T> map)
    {
        List<T> rows = new List<T>();
        IntPtr statement = Prepare(database, sql);
        try
        {
            while (true)
            {
                int result = NativeSqlite.sqlite3_step(statement);
                if (result == NativeSqlite.Done)
                    return rows;
                if (result != NativeSqlite.Row)
                    throw new InvalidOperationException(GetError(database));
                rows.Add(map(statement));
            }
        }
        finally
        {
            NativeSqlite.sqlite3_finalize(statement);
        }
    }

    private static List<T> ReadRowsWithValues<T>(
        IntPtr database,
        string sql,
        Func<IntPtr, T> map,
        params object[] values)
    {
        List<T> rows = new List<T>();
        IntPtr statement = Prepare(database, sql);
        try
        {
            BindValues(database, statement, values);
            while (true)
            {
                int result = NativeSqlite.sqlite3_step(statement);
                if (result == NativeSqlite.Done)
                    return rows;
                if (result != NativeSqlite.Row)
                    throw new InvalidOperationException(GetError(database));
                rows.Add(map(statement));
            }
        }
        finally
        {
            NativeSqlite.sqlite3_finalize(statement);
        }
    }

    private static void MarkRowSynced(
        IntPtr database,
        string table,
        string idColumn,
        string normalizedId,
        long revision,
        string syncedAtUtc)
    {
        // The table and column names are constants supplied only by this class.
        string sql = $@"UPDATE {table}
                        SET sync_status = 'synced', synced_at_utc = ?, remote_id = ?
                        WHERE {idColumn} = ? AND sync_revision = ? AND sync_status = 'pending';";
        string localId = CompactUuid(normalizedId);
        ExecutePrepared(database, sql, syncedAtUtc, normalizedId, localId, revision);
    }

    private static void EnsureColumn(IntPtr database, string table, string column, string definition)
    {
        IntPtr statement = Prepare(database, "PRAGMA table_info(" + table + ");");
        try
        {
            while (NativeSqlite.sqlite3_step(statement) == NativeSqlite.Row)
            {
                if (string.Equals(ColumnText(statement, 1), column, StringComparison.OrdinalIgnoreCase))
                    return;
            }
        }
        finally
        {
            NativeSqlite.sqlite3_finalize(statement);
        }

        ExecuteSql(database, $"ALTER TABLE {table} ADD COLUMN {column} {definition};");
    }

    private static string ColumnText(IntPtr statement, int index)
    {
        if (NativeSqlite.sqlite3_column_type(statement, index) == NativeSqlite.Null)
            return string.Empty;
        return PtrToUtf8(NativeSqlite.sqlite3_column_text(statement, index));
    }

    private static int ColumnInt(IntPtr statement, int index)
    {
        return NativeSqlite.sqlite3_column_int(statement, index);
    }

    private static int ColumnNullableInt(IntPtr statement, int index, int nullValue)
    {
        return NativeSqlite.sqlite3_column_type(statement, index) == NativeSqlite.Null
            ? nullValue
            : NativeSqlite.sqlite3_column_int(statement, index);
    }

    private static long ColumnLong(IntPtr statement, int index)
    {
        return NativeSqlite.sqlite3_column_int64(statement, index);
    }

    private static double ColumnDouble(IntPtr statement, int index)
    {
        return NativeSqlite.sqlite3_column_double(statement, index);
    }

    private static string NormalizeUuid(string value)
    {
        return Guid.TryParse(value, out Guid id) ? id.ToString("D") : value;
    }

    private static string CompactUuid(string value)
    {
        return Guid.TryParse(value, out Guid id) ? id.ToString("N") : value;
    }

    private static IntPtr Prepare(IntPtr database, string sql)
    {
        int result = NativeSqlite.sqlite3_prepare_v2(database, ToUtf8(sql), -1, out IntPtr statement, IntPtr.Zero);
        if (result != NativeSqlite.Ok)
            throw new InvalidOperationException(GetError(database));
        return statement;
    }

    private static void BindValues(IntPtr database, IntPtr statement, IReadOnlyList<object> values)
    {
        for (int index = 0; index < values.Count; index++)
        {
            int parameter = index + 1;
            object value = values[index];
            int result;

            if (value == null)
                result = NativeSqlite.sqlite3_bind_null(statement, parameter);
            else if (value is bool boolean)
                result = NativeSqlite.sqlite3_bind_int(statement, parameter, boolean ? 1 : 0);
            else if (value is int integer)
                result = NativeSqlite.sqlite3_bind_int(statement, parameter, integer);
            else if (value is long longInteger)
                result = NativeSqlite.sqlite3_bind_int64(statement, parameter, longInteger);
            else if (value is float single)
                result = NativeSqlite.sqlite3_bind_double(statement, parameter, single);
            else if (value is double doubleValue)
                result = NativeSqlite.sqlite3_bind_double(statement, parameter, doubleValue);
            else if (value is decimal decimalValue)
                result = NativeSqlite.sqlite3_bind_double(statement, parameter, (double)decimalValue);
            else
                result = NativeSqlite.sqlite3_bind_text(
                    statement,
                    parameter,
                    ToUtf8(Convert.ToString(value)),
                    -1,
                    NativeSqlite.Transient);

            if (result != NativeSqlite.Ok)
                throw new InvalidOperationException(GetError(database));
        }
    }

    private static string GetOrCreatePlayerPrefId(string key)
    {
        string id = PlayerPrefs.GetString(key, string.Empty);
        if (!string.IsNullOrWhiteSpace(id))
            return id;

        id = Guid.NewGuid().ToString("N");
        PlayerPrefs.SetString(key, id);
        PlayerPrefs.Save();
        return id;
    }

    private static string GetError(IntPtr database)
    {
        return database == IntPtr.Zero ? "SQLite database is unavailable." : PtrToUtf8(NativeSqlite.sqlite3_errmsg(database));
    }

    private static byte[] ToUtf8(string value)
    {
        byte[] text = System.Text.Encoding.UTF8.GetBytes((value ?? string.Empty) + "\0");
        return text;
    }

    private static string PtrToUtf8(IntPtr pointer)
    {
        if (pointer == IntPtr.Zero)
            return string.Empty;

        int length = 0;
        while (Marshal.ReadByte(pointer, length) != 0)
            length++;

        byte[] bytes = new byte[length];
        Marshal.Copy(pointer, bytes, 0, length);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    private static string UtcNow()
    {
        return DateTime.UtcNow.ToString("o");
    }

    private static void HandleFailure(string operation, Exception exception)
    {
        if (failureLogged)
            return;

        failureLogged = true;
        Debug.LogWarning($"Local SQLite could not {operation}. Gameplay will continue without analytics. {exception.Message}");
    }

    private static class NativeSqlite
    {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        private const string Library = "__Internal";
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private const string Library = "winsqlite3";
#else
        private const string Library = "sqlite3";
#endif

        internal const int Ok = 0;
        internal const int Row = 100;
        internal const int Done = 101;
        internal const int Null = 5;
        internal const int OpenReadWrite = 0x00000002;
        internal const int OpenCreate = 0x00000004;
        internal const int OpenFullMutex = 0x00010000;
        internal static readonly IntPtr Transient = new IntPtr(-1);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_open_v2(byte[] filename, out IntPtr database, int flags, IntPtr vfs);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_close_v2(IntPtr database);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_busy_timeout(IntPtr database, int milliseconds);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_exec(
            IntPtr database,
            byte[] sql,
            IntPtr callback,
            IntPtr callbackArgument,
            out IntPtr errorMessage);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void sqlite3_free(IntPtr memory);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr sqlite3_errmsg(IntPtr database);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_prepare_v2(
            IntPtr database,
            byte[] sql,
            int byteCount,
            out IntPtr statement,
            IntPtr tail);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_step(IntPtr statement);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_finalize(IntPtr statement);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_bind_null(IntPtr statement, int parameterIndex);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_bind_int(IntPtr statement, int parameterIndex, int value);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_bind_int64(IntPtr statement, int parameterIndex, long value);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_bind_double(IntPtr statement, int parameterIndex, double value);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_bind_text(
            IntPtr statement,
            int parameterIndex,
            byte[] value,
            int byteCount,
            IntPtr destructor);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern long sqlite3_column_int64(IntPtr statement, int columnIndex);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern double sqlite3_column_double(IntPtr statement, int columnIndex);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_column_int(IntPtr statement, int columnIndex);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr sqlite3_column_text(IntPtr statement, int columnIndex);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_column_type(IntPtr statement, int columnIndex);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sqlite3_changes(IntPtr database);
    }
}

public readonly struct LocalDataSummary
{
    public readonly string databasePath;
    public readonly long learnerProfiles;
    public readonly long gameplaySessions;
    public readonly long questionAttempts;
    public readonly long levelResults;
    public readonly bool isAvailable;

    public LocalDataSummary(
        string databasePath,
        long learnerProfiles,
        long gameplaySessions,
        long questionAttempts,
        long levelResults,
        bool isAvailable)
    {
        this.databasePath = databasePath;
        this.learnerProfiles = learnerProfiles;
        this.gameplaySessions = gameplaySessions;
        this.questionAttempts = questionAttempts;
        this.levelResults = levelResults;
        this.isAvailable = isAvailable;
    }

    public override string ToString()
    {
        return isAvailable
            ? $"Profiles: {learnerProfiles}, Sessions: {gameplaySessions}, Attempts: {questionAttempts}, Results: {levelResults}"
            : "Local database unavailable";
    }
}

public readonly struct LearningPipelineSkillSummary
{
    public readonly string skill;
    public readonly int attemptCount;
    public readonly int scoredSessionCount;
    public readonly string lastScoredSessionUtc;

    public bool IsArimaEligible => attemptCount >= 10 && scoredSessionCount >= 5;

    public LearningPipelineSkillSummary(
        string skill,
        int attemptCount,
        int scoredSessionCount,
        string lastScoredSessionUtc)
    {
        this.skill = skill;
        this.attemptCount = attemptCount;
        this.scoredSessionCount = scoredSessionCount;
        this.lastScoredSessionUtc = lastScoredSessionUtc;
    }
}
