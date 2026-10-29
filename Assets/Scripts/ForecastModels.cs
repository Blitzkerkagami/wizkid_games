using System;

[Serializable]
public sealed class LearnerForecastRecord
{
    public string forecast_id;
    public string player_id;
    public string skill;
    public string model_version;
    public string generated_at;
    public string valid_until;
    public int source_attempt_count;
    public float expected_score_percent;
    public float mastery_probability;
    public float confidence;
    public string trend_label;
    public string risk_level;
    public int recommended_level;
    public string recommendation;
    public string cached_at_utc;
}

[Serializable]
public sealed class ForecastReadinessRecord
{
    public string player_id;
    public string skill;
    public int total_attempts;
    public int total_sessions;
    public bool is_forecast_eligible;
    public int attempts_needed;
    public string data_quality_status;
    public string observed_trend;
    public int forecast_session_count;
    public int sessions_needed;
    public string cached_at_utc;
}

[Serializable]
public sealed class ForecastValidationRecord
{
    public string forecast_id;
    public string player_id;
    public string skill;
    public string model_version;
    public string generated_at;
    public string actual_session_id;
    public string actual_at;
    public float predicted_score_percent;
    public float actual_score_percent;
    public float signed_error;
    public float absolute_error;
    public float squared_error;
    public float baseline_score_percent;
    public float baseline_absolute_error;
    public float baseline_squared_error;
    public bool has_baseline;
    public string cached_at_utc;
}

public sealed class ForecastValidationSummary
{
    public int sample_count;
    public float mae;
    public float rmse;
    public float accuracy_percent;
    public int baseline_sample_count;
    public float baseline_mae;
    public float baseline_rmse;
    public string progress_label;
    public string model_version;
}

public static class ForecastValidationCalculator
{
    public static ForecastValidationSummary Calculate(ForecastValidationRecord[] rows)
    {
        ForecastValidationRecord[] safeRows = rows ?? Array.Empty<ForecastValidationRecord>();
        ForecastValidationSummary result = new ForecastValidationSummary
        {
            sample_count = safeRows.Length,
            progress_label = "needs_more_practice",
            model_version = safeRows.Length > 0 ? safeRows[0].model_version : "arima-session-v1"
        };

        if (safeRows.Length == 0)
            return result;

        double absoluteTotal = 0d;
        double squaredTotal = 0d;
        double baselineAbsoluteTotal = 0d;
        double baselineSquaredTotal = 0d;
        int baselineCount = 0;

        foreach (ForecastValidationRecord row in safeRows)
        {
            absoluteTotal += Math.Max(0d, row.absolute_error);
            squaredTotal += Math.Max(0d, row.squared_error);
            if (row.has_baseline)
            {
                baselineAbsoluteTotal += Math.Max(0d, row.baseline_absolute_error);
                baselineSquaredTotal += Math.Max(0d, row.baseline_squared_error);
                baselineCount++;
            }
        }

        result.mae = (float)(absoluteTotal / safeRows.Length);
        result.rmse = (float)Math.Sqrt(squaredTotal / safeRows.Length);
        result.accuracy_percent = Math.Max(0f, 100f - result.mae);
        result.baseline_sample_count = baselineCount;
        if (baselineCount > 0)
        {
            result.baseline_mae = (float)(baselineAbsoluteTotal / baselineCount);
            result.baseline_rmse = (float)Math.Sqrt(baselineSquaredTotal / baselineCount);
        }

        if (safeRows.Length < 3 || result.mae > 20f)
        {
            result.progress_label = "needs_more_practice";
            return result;
        }

        int recentCount = Math.Min(3, safeRows.Length / 2);
        if (recentCount == 0)
        {
            result.progress_label = "steady";
            return result;
        }

        double olderTotal = 0d;
        int olderCount = safeRows.Length - recentCount;
        for (int index = 0; index < olderCount; index++)
            olderTotal += safeRows[index].absolute_error;

        double recentTotal = 0d;
        for (int index = olderCount; index < safeRows.Length; index++)
            recentTotal += safeRows[index].absolute_error;

        double olderMae = olderCount > 0 ? olderTotal / olderCount : result.mae;
        double recentMae = recentTotal / recentCount;
        result.progress_label = recentMae <= olderMae - 2d ? "improving" : "steady";
        return result;
    }
}

[Serializable]
internal sealed class LearnerForecastArray
{
    public LearnerForecastRecord[] items = Array.Empty<LearnerForecastRecord>();
}

[Serializable]
internal sealed class ForecastReadinessArray
{
    public ForecastReadinessRecord[] items = Array.Empty<ForecastReadinessRecord>();
}

[Serializable]
internal sealed class ForecastValidationArray
{
    public ForecastValidationRecord[] items = Array.Empty<ForecastValidationRecord>();
}

