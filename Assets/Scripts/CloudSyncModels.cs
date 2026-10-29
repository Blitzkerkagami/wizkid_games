using System;

[Serializable]
public sealed class CloudSyncBatch
{
    public CloudLearnerProfile[] profiles = Array.Empty<CloudLearnerProfile>();
    public CloudGameplaySession[] sessions = Array.Empty<CloudGameplaySession>();
    public CloudQuestionAttempt[] attempts = Array.Empty<CloudQuestionAttempt>();
    public CloudLevelResult[] results = Array.Empty<CloudLevelResult>();

    public bool IsEmpty =>
        profiles.Length == 0 && sessions.Length == 0 &&
        attempts.Length == 0 && results.Length == 0;

    public int Count => profiles.Length + sessions.Length + attempts.Length + results.Length;
}

[Serializable]
public sealed class CloudLearnerProfile
{
    public string player_id;
    public string display_name;
    public int age;
    public int wizard_index;
    public string created_at_utc;
    public string updated_at_utc;
    public long client_revision;
}

[Serializable]
public sealed class CloudGameplaySession
{
    public string session_id;
    public string player_id;
    public string activity;
    public string skill;
    public int level;
    public string started_at_utc;
    public string ended_at_utc;
    public int total_questions;
    public int attempted_questions;
    public int correct_answers;
    // -1 means SQL NULL while a session is still in progress.
    public int score_percent;
    // -1 means SQL NULL; otherwise 0=false and 1=true.
    public int passed;
    public int lives_remaining;
    public string completion_status;
    public string installation_id;
    public string app_version;
    public long client_revision;
}

[Serializable]
public sealed class CloudQuestionAttempt
{
    public string attempt_id;
    public string session_id;
    public string player_id;
    public string question_id;
    public string activity;
    public string skill;
    public int level;
    public int difficulty;
    public string competency;
    public int attempt_number;
    // -1 means SQL NULL (for example, a timeout).
    public int selected_answer_index;
    public int correct_answer_index;
    public bool is_correct;
    public bool timed_out;
    public long response_time_ms;
    public string attempted_at_utc;
    public long client_revision;
}

[Serializable]
public sealed class CloudLevelResult
{
    public string result_id;
    public string session_id;
    public string player_id;
    public string activity;
    public string skill;
    public int level;
    public int total_questions;
    public int attempted_questions;
    public int correct_answers;
    public int score_percent;
    public bool passed;
    public int lives_remaining;
    public string completed_at_utc;
    public long client_revision;
}
