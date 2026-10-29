using UnityEditor;
using UnityEngine;

public static class ForecastIntegrationDiagnostics
{
    [MenuItem("WizKid/Phase 5/Validate Forecast Integration")]
    public static void ValidateForecastIntegration()
    {
        if (!SupabaseConfig.TryLoad(out SupabaseConfig config, out string configError))
        {
            Debug.LogError("Phase 5C validation failed: " + configError);
            return;
        }

        if (!LocalDatabase.Initialize())
        {
            Debug.LogError("Phase 5C validation failed: local SQLite is unavailable.");
            return;
        }

        string playerId = LocalDatabase.GetOrCreateCurrentPlayerId();
        LearnerForecastRecord[] forecasts = LocalDatabase.GetCachedForecasts(playerId);
        ForecastReadinessRecord[] readiness = LocalDatabase.GetCachedForecastReadiness(playerId);

        Debug.Log(
            "WizKid Phase 5C integration is ready. " +
            $"Supabase: {config.projectUrl}; cached forecasts: {forecasts.Length}; " +
            $"cached readiness rows: {readiness.Length}. " +
            "Enter Play Mode and open AdventureMap to use the Learning Forecast button.");
    }
}

