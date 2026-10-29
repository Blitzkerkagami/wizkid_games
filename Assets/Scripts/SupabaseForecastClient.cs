using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Reads only the signed-in learner's forecast data through Supabase RLS.
/// The Unity client uses the publishable key and user access token, never a server secret.
/// </summary>
public sealed class SupabaseForecastClient
{
    private const string ForecastColumns =
        "forecast_id,player_id,skill,model_version,generated_at,valid_until," +
        "source_attempt_count,expected_score_percent,mastery_probability,confidence," +
        "trend_label,risk_level,recommended_level,recommendation";

    private const string ReadinessColumns =
        "player_id,skill,total_attempts,total_sessions,is_forecast_eligible," +
        "attempts_needed,data_quality_status,observed_trend," +
        "forecast_session_count,sessions_needed";

    private const string ValidationColumns =
        "forecast_id,player_id,skill,model_version,generated_at," +
        "actual_session_id,actual_at,predicted_score_percent,actual_score_percent," +
        "signed_error,absolute_error,squared_error,baseline_score_percent," +
        "baseline_absolute_error,baseline_squared_error,has_baseline";

    private readonly SupabaseConfig config;

    public SupabaseForecastClient(SupabaseConfig config)
    {
        this.config = config;
    }

    public IEnumerator FetchForecasts(
        string accessToken,
        string playerId,
        Action<bool, long, string, LearnerForecastRecord[]> completed)
    {
        string normalizedPlayerId = NormalizeUuid(playerId);
        string url = config.projectUrl +
                     "/rest/v1/current_learner_forecasts?select=" + ForecastColumns +
                     "&player_id=eq." + Uri.EscapeDataString(normalizedPlayerId) +
                     "&order=skill.asc";

        using (UnityWebRequest request = CreateGet(url, accessToken))
        {
            yield return request.SendWebRequest();
            if (!IsSuccess(request))
            {
                completed(false, request.responseCode, ReadableResponse(request), Array.Empty<LearnerForecastRecord>());
                yield break;
            }

            try
            {
                string wrappedJson = "{\"items\":" + request.downloadHandler.text + "}";
                LearnerForecastArray response = JsonUtility.FromJson<LearnerForecastArray>(wrappedJson);
                completed(true, request.responseCode, string.Empty,
                    response?.items ?? Array.Empty<LearnerForecastRecord>());
            }
            catch (Exception exception)
            {
                completed(false, request.responseCode,
                    "Could not read forecast data: " + exception.Message,
                    Array.Empty<LearnerForecastRecord>());
            }
        }
    }

    public IEnumerator FetchReadiness(
        string accessToken,
        string playerId,
        Action<bool, long, string, ForecastReadinessRecord[]> completed)
    {
        string normalizedPlayerId = NormalizeUuid(playerId);
        string url = config.projectUrl +
                     "/rest/v1/learner_skill_features?select=" + ReadinessColumns +
                     "&player_id=eq." + Uri.EscapeDataString(normalizedPlayerId) +
                     "&order=skill.asc";

        using (UnityWebRequest request = CreateGet(url, accessToken))
        {
            yield return request.SendWebRequest();
            if (!IsSuccess(request))
            {
                completed(false, request.responseCode, ReadableResponse(request), Array.Empty<ForecastReadinessRecord>());
                yield break;
            }

            try
            {
                string wrappedJson = "{\"items\":" + request.downloadHandler.text + "}";
                ForecastReadinessArray response = JsonUtility.FromJson<ForecastReadinessArray>(wrappedJson);
                completed(true, request.responseCode, string.Empty,
                    response?.items ?? Array.Empty<ForecastReadinessRecord>());
            }
            catch (Exception exception)
            {
                completed(false, request.responseCode,
                    "Could not read forecast readiness: " + exception.Message,
                    Array.Empty<ForecastReadinessRecord>());
            }
        }
    }

    public IEnumerator FetchValidationResults(
        string accessToken,
        string playerId,
        Action<bool, long, string, ForecastValidationRecord[]> completed)
    {
        string normalizedPlayerId = NormalizeUuid(playerId);
        string url = config.projectUrl +
                     "/rest/v1/forecast_validation_details?select=" + ValidationColumns +
                     "&player_id=eq." + Uri.EscapeDataString(normalizedPlayerId) +
                     "&order=actual_at.asc&limit=30";

        using (UnityWebRequest request = CreateGet(url, accessToken))
        {
            yield return request.SendWebRequest();
            if (!IsSuccess(request))
            {
                completed(false, request.responseCode, ReadableResponse(request),
                    Array.Empty<ForecastValidationRecord>());
                yield break;
            }

            try
            {
                string wrappedJson = "{\"items\":" + request.downloadHandler.text + "}";
                ForecastValidationArray response = JsonUtility.FromJson<ForecastValidationArray>(wrappedJson);
                completed(true, request.responseCode, string.Empty,
                    response?.items ?? Array.Empty<ForecastValidationRecord>());
            }
            catch (Exception exception)
            {
                completed(false, request.responseCode,
                    "Could not read forecast validation data: " + exception.Message,
                    Array.Empty<ForecastValidationRecord>());
            }
        }
    }

    private UnityWebRequest CreateGet(string url, string accessToken)
    {
        UnityWebRequest request = UnityWebRequest.Get(url);
        request.timeout = 30;
        request.SetRequestHeader("Accept", "application/json");
        request.SetRequestHeader("apikey", config.publishableKey);
        request.SetRequestHeader("Authorization", "Bearer " + accessToken);
        return request;
    }

    private static bool IsSuccess(UnityWebRequest request)
    {
        return request.result == UnityWebRequest.Result.Success &&
               request.responseCode >= 200 && request.responseCode < 300;
    }

    private static string ReadableResponse(UnityWebRequest request)
    {
        string response = request.downloadHandler?.text;
        return string.IsNullOrWhiteSpace(response) ? request.error : response;
    }

    private static string NormalizeUuid(string value)
    {
        return Guid.TryParse(value, out Guid id) ? id.ToString("D") : value;
    }
}

