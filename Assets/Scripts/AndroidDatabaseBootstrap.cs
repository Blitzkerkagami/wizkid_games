using UnityEngine;

public static class AndroidDatabaseBootstrap
{
#if UNITY_ANDROID && !UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ValidateLocalDatabaseOnLaunch()
    {
        bool initialized = LocalDatabase.Initialize();
        long launchCount = initialized ? LocalDatabase.RecordAndroidCheckpointLaunch() : 0;
        LocalDataSummary summary = LocalDatabase.GetSummary();

        if (initialized && summary.isAvailable && launchCount > 0)
        {
            Debug.Log(
                $"WizKid Android SQLite checkpoint PASS. " +
                $"Persistent launch #{launchCount}. {summary}. Path: {summary.databasePath}");
        }
        else
        {
            Debug.LogError("WizKid Android SQLite checkpoint FAILED. Local offline data is unavailable.");
        }
    }
#endif
}
