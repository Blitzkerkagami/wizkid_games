using System;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class Phase6EndToEndDiagnostics
{
    private const string ArimaModelVersion = "arima-session-v1";

    [MenuItem("WizKid/Phase 6A/Request Cloud and Forecast Refresh")]
    public static void RequestRefresh()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Phase 6A refresh requires Play Mode. Enter Play Mode, then run this menu again.");
            return;
        }

        SupabaseSyncService.RequestSync();
        SupabaseForecastService.RequestRefresh();
        Debug.Log(
            "Phase 6A refresh requested. Wait for cloud sync to become UpToDate and " +
            "forecast sync to become Ready, then run the end-to-end checkpoint.");
    }

    [MenuItem("WizKid/Phase 6A/Run End-to-End Forecast Check")]
    public static void RunEndToEndCheck()
    {
        var report = new StringBuilder();
        report.AppendLine("WizKid Phase 6A End-to-End Forecast Check");

        if (!SupabaseConfig.TryLoad(out SupabaseConfig config, out string configError))
        {
            Debug.LogError(report.AppendLine("[FAIL] Supabase config: " + configError).ToString());
            return;
        }
        report.AppendLine("[PASS] Supabase client configuration is available.");

        if (!LocalDatabase.Initialize())
        {
            Debug.LogError(report.AppendLine("[FAIL] Local SQLite database is unavailable.").ToString());
            return;
        }
        report.AppendLine("[PASS] Local SQLite database is available.");

        string playerId = LocalDatabase.GetOrCreateCurrentPlayerId();
        LearningPipelineSkillSummary[] skills =
            LocalDatabase.GetLearningPipelineSkillSummaries(playerId);
        long pendingRecords = LocalDatabase.GetPendingSyncRecordCount();
        LearnerForecastRecord[] forecasts = LocalDatabase.GetCachedForecasts(playerId);
        ForecastReadinessRecord[] readiness =
            LocalDatabase.GetCachedForecastReadiness(playerId);

        bool hasEligibleSkill = false;
        if (skills.Length == 0)
        {
            report.AppendLine("[WAIT] No local gameplay data exists for this learner yet.");
        }
        else
        {
            foreach (LearningPipelineSkillSummary skill in skills)
            {
                string status = skill.IsArimaEligible ? "PASS" : "WAIT";
                hasEligibleSkill |= skill.IsArimaEligible;
                report.AppendLine(
                    $"[{status}] {DisplayName(skill.skill)}: " +
                    $"{skill.attemptCount}/10 attempts, " +
                    $"{skill.scoredSessionCount}/5 scored sessions.");
            }
        }

        bool syncQueueClear = pendingRecords == 0;
        report.AppendLine(pendingRecords < 0
            ? "[FAIL] Could not inspect the local cloud-sync queue."
            : syncQueueClear
                ? "[PASS] Local cloud-sync queue is empty."
                : $"[WAIT] {pendingRecords} local record(s) are waiting for cloud sync.");

        bool hasArimaForecast = false;
        foreach (LearnerForecastRecord forecast in forecasts)
        {
            bool isArima = string.Equals(
                forecast.model_version, ArimaModelVersion, StringComparison.Ordinal);
            hasArimaForecast |= isArima;
            report.AppendLine(
                $"[{(isArima ? "PASS" : "WAIT")}] Cached {DisplayName(forecast.skill)} forecast: " +
                $"model={forecast.model_version}, generated={forecast.generated_at}.");
        }

        if (forecasts.Length == 0)
        {
            report.AppendLine("[WAIT] No forecast has been downloaded to SQLite yet.");
            if (hasEligibleSkill && syncQueueClear)
            {
                report.AppendLine(
                    "[NEXT] Run the protected forecasting job or wait for the Vercel cron, " +
                    "then request another forecast refresh.");
            }
        }
        report.AppendLine(
            $"[INFO] Cached readiness rows: {readiness.Length}; cached forecasts: {forecasts.Length}.");

        bool liveServicesReady = false;
        if (Application.isPlaying)
        {
            bool cloudReady = SupabaseSyncService.State == CloudSyncState.UpToDate;
            bool forecastReady = SupabaseForecastService.State == ForecastSyncState.Ready;
            liveServicesReady = cloudReady && forecastReady;
            report.AppendLine(
                $"[{(cloudReady ? "PASS" : "WAIT")}] Cloud sync: {SupabaseSyncService.State}.");
            report.AppendLine(
                $"[{(forecastReady ? "PASS" : "WAIT")}] Forecast sync: " +
                $"{SupabaseForecastService.State}.");
            if (!string.IsNullOrWhiteSpace(SupabaseSyncService.LastError))
                report.AppendLine("[INFO] Cloud sync message: " + SupabaseSyncService.LastError);
            if (!string.IsNullOrWhiteSpace(SupabaseForecastService.LastError))
                report.AppendLine("[INFO] Forecast sync message: " + SupabaseForecastService.LastError);
        }
        else
        {
            report.AppendLine(
                "[WAIT] Enter Play Mode to verify the live cloud and forecast services.");
        }

        bool passed = hasEligibleSkill && syncQueueClear && hasArimaForecast && liveServicesReady;
        report.AppendLine(passed
            ? "RESULT: PASS — gameplay → SQLite → Supabase → ARIMA → Unity cache is verified."
            : "RESULT: WAIT — complete the WAIT items, refresh, then run this check again.");

        if (passed)
            Debug.Log(report.ToString());
        else
            Debug.LogWarning(report.ToString());
    }

    [MenuItem("WizKid/Phase 6A/Validate Offline Forecast Cache")]
    public static void ValidateOfflineCache()
    {
        if (!LocalDatabase.Initialize())
        {
            Debug.LogError("Phase 6A offline-cache check FAIL: SQLite is unavailable.");
            return;
        }

        string playerId = LocalDatabase.GetOrCreateCurrentPlayerId();
        LearnerForecastRecord[] forecasts = LocalDatabase.GetCachedForecasts(playerId);
        int arimaForecasts = 0;
        foreach (LearnerForecastRecord forecast in forecasts)
            if (string.Equals(forecast.model_version, ArimaModelVersion, StringComparison.Ordinal))
                arimaForecasts++;

        if (arimaForecasts == 0)
        {
            Debug.LogWarning(
                "Phase 6A offline-cache check WAIT: download an ARIMA forecast while online first.");
            return;
        }

        Debug.Log(
            $"Phase 6A offline-cache read PASS: {arimaForecasts} ARIMA forecast(s) can be " +
            "read directly from SQLite. Final device test: enable airplane mode, restart the app, " +
            "and confirm the same forecast appears in AdventureMap.");
    }

    private static string DisplayName(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Unknown skill"
            : value.Replace('_', ' ').Replace('-', ' ');
    }
}
