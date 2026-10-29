using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Phase6CForecastValidation
{
    private const string ReportScenePath = "Assets/Scenes/ForecastValidationReport.unity";

    [MenuItem("WizKid/Phase 6C/Validate Forecast Report Setup")]
    public static void ValidateSetup()
    {
        bool sceneExists = AssetDatabase.LoadAssetAtPath<SceneAsset>(ReportScenePath) != null;
        bool sceneEnabled = EditorBuildSettings.scenes.Any(scene =>
            scene.enabled && string.Equals(scene.path, ReportScenePath, StringComparison.OrdinalIgnoreCase));
        bool databaseReady = LocalDatabase.Initialize();
        string playerId = LocalDatabase.GetOrCreateCurrentPlayerId();
        ForecastValidationRecord[] rows = databaseReady
            ? LocalDatabase.GetCachedForecastValidation(playerId)
            : Array.Empty<ForecastValidationRecord>();
        ForecastValidationSummary summary = ForecastValidationCalculator.Calculate(rows);

        if (!sceneExists || !sceneEnabled || !databaseReady)
        {
            Debug.LogError(
                "Phase 6C setup failed. " +
                $"Scene exists: {sceneExists}; build scene enabled: {sceneEnabled}; " +
                $"SQLite ready: {databaseReady}.");
            return;
        }

        string cloudNote = string.IsNullOrWhiteSpace(SupabaseForecastService.LastValidationError)
            ? "No validation download error is currently recorded."
            : "Latest validation download note: " + SupabaseForecastService.LastValidationError;
        Debug.Log(
            "WizKid Phase 6C report setup PASS. " +
            $"Cached validated samples: {summary.sample_count}; " +
            $"MAE: {summary.mae:0.00}; RMSE: {summary.rmse:0.00}. " + cloudNote + " " +
            "Open AdventureMap > Learning Forecast > View Forecast Accuracy.");
    }

    [MenuItem("WizKid/Phase 6C/Open Forecast Report Scene")]
    public static void OpenReportScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        EditorSceneManager.OpenScene(ReportScenePath);
    }
}
