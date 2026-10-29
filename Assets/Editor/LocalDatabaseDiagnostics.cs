using UnityEditor;
using UnityEngine;

public static class LocalDatabaseDiagnostics
{
    [MenuItem("WizKid/Phase 3/Initialize and Validate Local Database")]
    public static void InitializeAndValidate()
    {
        if (!LocalDatabase.Initialize())
        {
            Debug.LogError("Phase 3 validation failed. See the previous SQLite message for details.");
            return;
        }

        LocalDatabase.UpsertCurrentLearnerProfile();
        LocalDataSummary summary = LocalDatabase.GetSummary();
        Debug.Log($"WizKid Phase 3 local database is ready. {summary}. File: {summary.databasePath}");
    }

    [MenuItem("WizKid/Phase 3/Open Local Database Folder")]
    public static void OpenDatabaseFolder()
    {
        LocalDatabase.Initialize();
        EditorUtility.RevealInFinder(LocalDatabase.DatabasePath);
    }
}
