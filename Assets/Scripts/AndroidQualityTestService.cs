using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;

/// <summary>
/// Collects repeatable Phase 6B evidence on an actual Android device.
/// The compact QA overlay is included only in development builds.
/// </summary>
public sealed class AndroidQualityTestService : MonoBehaviour
{
    private const string PreferencePrefix = "WizKid.Phase6B.";
    private const float SnapshotIntervalSeconds = 5f;
    private static AndroidQualityTestService instance;

    public static string ReportPath => Path.Combine(
        Application.persistentDataPath,
        "phase6b_android_quality_report.json");

    private AndroidQualityReport report = new AndroidQualityReport();
    private bool overlayVisible;
    private Vector2 scrollPosition;
    private float elapsedSampleTime;
    private int sampledFrames;
    private float worstFrameMilliseconds;
    private long offlinePendingBaseline = -1;
    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private GUIStyle buttonStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (instance != null)
            return;

        var host = new GameObject("Phase 6B Android QA");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<AndroidQualityTestService>();
#endif
    }

    private void Awake()
    {
        Application.lowMemory += HandleLowMemory;
        Application.logMessageReceived += HandleLogMessage;
        SupabaseSyncService.StatusChanged += HandleCloudStateChanged;
        SupabaseForecastService.StatusChanged += HandleForecastStateChanged;

        LocalDatabase.Initialize();
        report.launchCount = LocalDatabase.RecordAndroidCheckpointLaunch();
        report.currentVersionCode = GetAndroidVersionCode();
        DetectAppUpdate();
        CaptureSnapshot();
        StartCoroutine(SnapshotLoop());
    }

    private void OnDestroy()
    {
        Application.lowMemory -= HandleLowMemory;
        Application.logMessageReceived -= HandleLogMessage;
        SupabaseSyncService.StatusChanged -= HandleCloudStateChanged;
        SupabaseForecastService.StatusChanged -= HandleForecastStateChanged;
        WriteReport();
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        float delta = Time.unscaledDeltaTime;
        if (delta <= 0f || delta > 1f)
            return;

        elapsedSampleTime += delta;
        sampledFrames++;
        worstFrameMilliseconds = Mathf.Max(worstFrameMilliseconds, delta * 1000f);
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            WriteReport();
        else
        {
            SupabaseSyncService.RequestSync();
            SupabaseForecastService.RequestRefresh();
            CaptureSnapshot();
        }
    }

    private void OnApplicationFocus(bool focused)
    {
        if (focused)
            CaptureSnapshot();
    }

    private IEnumerator SnapshotLoop()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(SnapshotIntervalSeconds);
            CaptureSnapshot();
        }
    }

    private void CaptureSnapshot()
    {
        LocalDataSummary summary = LocalDatabase.GetSummary();
        long pendingRecords = LocalDatabase.GetPendingSyncRecordCount();
        string playerId = LocalDatabase.GetOrCreateCurrentPlayerId();
        LearnerForecastRecord[] forecasts = LocalDatabase.GetCachedForecasts(playerId);
        bool offline = Application.internetReachability == NetworkReachability.NotReachable;

        report.recordedAtUtc = DateTime.UtcNow.ToString("o");
        report.applicationVersion = Application.version;
        report.buildGuid = Application.buildGUID;
        report.unityVersion = Application.unityVersion;
        report.deviceModel = SystemInfo.deviceModel;
        report.operatingSystem = SystemInfo.operatingSystem;
        report.processorType = SystemInfo.processorType;
        report.systemMemoryMb = SystemInfo.systemMemorySize;
        report.graphicsMemoryMb = SystemInfo.graphicsMemorySize;
        report.resolution = Screen.width + "x" + Screen.height;
        report.internetReachability = Application.internetReachability.ToString();
        report.sqliteAvailable = summary.isAvailable;
        report.learnerProfiles = summary.learnerProfiles;
        report.gameplaySessions = summary.gameplaySessions;
        report.questionAttempts = summary.questionAttempts;
        report.levelResults = summary.levelResults;
        report.pendingSyncRecords = pendingRecords;
        report.cachedForecasts = forecasts.Length;
        report.cloudSyncState = SupabaseSyncService.State.ToString();
        report.forecastSyncState = SupabaseForecastService.State.ToString();
        report.lastCloudSyncUtc = SupabaseSyncService.LastSuccessfulSyncUtc;
        report.lastForecastRefreshUtc = SupabaseForecastService.LastSuccessfulRefreshUtc;
        report.averageFps = elapsedSampleTime > 0f
            ? Mathf.Round(sampledFrames / elapsedSampleTime * 10f) / 10f
            : 0f;
        report.worstFrameMilliseconds = Mathf.Round(worstFrameMilliseconds * 10f) / 10f;
        report.totalAllocatedMemoryMb = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
        PlayerPrefs.SetString(
            PreferencePrefix + "RecordCount",
            TotalRecords(summary).ToString());

        if (offline)
        {
            if (!report.offlineModeObserved)
                offlinePendingBaseline = pendingRecords;
            report.offlineModeObserved = true;
            PlayerPrefs.SetInt(PreferencePrefix + "WasOffline", 1);

            if (report.launchCount > 1 &&
                PlayerPrefs.GetInt(PreferencePrefix + "OfflineAtPreviousPause", 0) == 1)
                report.offlineRestartObserved = true;

            if (pendingRecords > offlinePendingBaseline && offlinePendingBaseline >= 0)
                report.offlineProgressQueued = true;

            if (forecasts.Length > 0)
                report.offlineForecastCacheReadable = true;
        }
        else
        {
            bool wasOffline = PlayerPrefs.GetInt(PreferencePrefix + "WasOffline", 0) == 1;
            if (wasOffline && SupabaseSyncService.State == CloudSyncState.UpToDate && pendingRecords == 0)
                report.reconnectSyncRecovered = true;
            if (wasOffline && SupabaseForecastService.State == ForecastSyncState.Ready)
                report.forecastRefreshRecovered = true;
            PlayerPrefs.SetInt(PreferencePrefix + "WasOffline", 0);
        }

        if (SupabaseSyncService.State == CloudSyncState.WaitingForNetwork ||
            SupabaseSyncService.State == CloudSyncState.RetryScheduled ||
            SupabaseForecastService.State == ForecastSyncState.Offline ||
            SupabaseForecastService.State == ForecastSyncState.RetryScheduled)
            report.recoverableErrorObserved = true;

        PersistFlags();
        UpdateCompletion();
        WriteReport();
    }

    private void DetectAppUpdate()
    {
        int previousVersionCode = PlayerPrefs.GetInt(PreferencePrefix + "VersionCode", -1);
        long previousRecordCount = ParseLong(
            PlayerPrefs.GetString(PreferencePrefix + "RecordCount", "-1"));
        LocalDataSummary summary = LocalDatabase.GetSummary();
        long currentRecordCount = TotalRecords(summary);

        if (previousVersionCode >= 0 && report.currentVersionCode > previousVersionCode)
        {
            report.appUpdateObserved = true;
            report.dataPreservedAfterUpdate = summary.isAvailable &&
                                              previousRecordCount >= 0 &&
                                              currentRecordCount >= previousRecordCount;
        }

        PlayerPrefs.SetInt(PreferencePrefix + "VersionCode", report.currentVersionCode);
        PlayerPrefs.SetString(PreferencePrefix + "RecordCount", currentRecordCount.ToString());
        PlayerPrefs.Save();
    }

    private void HandleCloudStateChanged()
    {
        if (report.offlineModeObserved &&
            SupabaseSyncService.State == CloudSyncState.UpToDate &&
            LocalDatabase.GetPendingSyncRecordCount() == 0)
            report.reconnectSyncRecovered = true;
        CaptureSnapshot();
    }

    private void HandleForecastStateChanged()
    {
        if (report.offlineModeObserved && SupabaseForecastService.State == ForecastSyncState.Ready)
            report.forecastRefreshRecovered = true;
        CaptureSnapshot();
    }

    private void HandleLowMemory()
    {
        report.lowMemoryWarnings++;
        WriteReport();
    }

    private void HandleLogMessage(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            report.runtimeErrors++;
    }

    private void PersistFlags()
    {
        SaveFlag("OfflineMode", report.offlineModeObserved);
        SaveFlag("OfflineRestart", report.offlineRestartObserved);
        SaveFlag("OfflineProgress", report.offlineProgressQueued);
        SaveFlag("Reconnect", report.reconnectSyncRecovered);
        SaveFlag("ForecastCache", report.offlineForecastCacheReadable);
        SaveFlag("ForecastRecovery", report.forecastRefreshRecovered);
        SaveFlag("RecoverableError", report.recoverableErrorObserved);
        SaveFlag("AppUpdate", report.appUpdateObserved);
        SaveFlag("UpdateData", report.dataPreservedAfterUpdate);

        report.offlineModeObserved |= LoadFlag("OfflineMode");
        report.offlineRestartObserved |= LoadFlag("OfflineRestart");
        report.offlineProgressQueued |= LoadFlag("OfflineProgress");
        report.reconnectSyncRecovered |= LoadFlag("Reconnect");
        report.offlineForecastCacheReadable |= LoadFlag("ForecastCache");
        report.forecastRefreshRecovered |= LoadFlag("ForecastRecovery");
        report.recoverableErrorObserved |= LoadFlag("RecoverableError");
        report.appUpdateObserved |= LoadFlag("AppUpdate");
        report.dataPreservedAfterUpdate |= LoadFlag("UpdateData");
        PlayerPrefs.SetInt(
            PreferencePrefix + "OfflineAtPreviousPause",
            Application.internetReachability == NetworkReachability.NotReachable ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void UpdateCompletion()
    {
        bool performancePass = sampledFrames >= 120 && report.averageFps >= 30f;
        int passed = 0;
        passed += report.sqliteAvailable ? 1 : 0;
        passed += report.offlineModeObserved ? 1 : 0;
        passed += report.offlineRestartObserved ? 1 : 0;
        passed += report.offlineProgressQueued ? 1 : 0;
        passed += report.reconnectSyncRecovered ? 1 : 0;
        passed += report.offlineForecastCacheReadable ? 1 : 0;
        passed += report.appUpdateObserved && report.dataPreservedAfterUpdate ? 1 : 0;
        passed += performancePass ? 1 : 0;
        passed += report.lowMemoryWarnings == 0 && report.runtimeErrors == 0 ? 1 : 0;
        report.completedChecks = passed;
        report.totalChecks = 9;
        report.allRequiredChecksPassed = passed == report.totalChecks;
    }

    private void WriteReport()
    {
        try
        {
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Phase 6B could not write its QA report: " + exception.Message);
        }
    }

    private void ResetTestRun()
    {
        string[] keys =
        {
            "WasOffline", "OfflineAtPreviousPause", "OfflineMode", "OfflineRestart",
            "OfflineProgress", "Reconnect", "ForecastCache", "ForecastRecovery",
            "RecoverableError", "AppUpdate", "UpdateData"
        };
        foreach (string key in keys)
            PlayerPrefs.DeleteKey(PreferencePrefix + key);
        PlayerPrefs.Save();

        report = new AndroidQualityReport
        {
            launchCount = LocalDatabase.RecordAndroidCheckpointLaunch(),
            currentVersionCode = GetAndroidVersionCode()
        };
        elapsedSampleTime = 0f;
        sampledFrames = 0;
        worstFrameMilliseconds = 0f;
        offlinePendingBaseline = -1;
        CaptureSnapshot();
    }

    private void OnGUI()
    {
        if (!Debug.isDebugBuild)
            return;

        EnsureStyles();
        float scale = Mathf.Clamp(Screen.dpi > 0f ? Screen.dpi / 160f : 1.5f, 1f, 2.5f);
        Matrix4x4 oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float logicalWidth = Screen.width / scale;
        float logicalHeight = Screen.height / scale;

        if (!overlayVisible)
        {
            if (GUI.Button(new Rect(12f, logicalHeight - 62f, 110f, 50f), "QA 6B", buttonStyle))
                overlayVisible = true;
            GUI.matrix = oldMatrix;
            return;
        }

        Rect panel = new Rect(12f, 12f, Mathf.Min(620f, logicalWidth - 24f), logicalHeight - 24f);
        GUILayout.BeginArea(panel, GUI.skin.box);
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);
        GUILayout.Label("Phase 6B Android Quality Test", titleStyle);
        GUILayout.Label($"Progress: {report.completedChecks}/{report.totalChecks}", labelStyle);
        DrawCheck("SQLite database", report.sqliteAvailable);
        DrawCheck("Airplane/offline mode observed", report.offlineModeObserved);
        DrawCheck("Restarted while offline", report.offlineRestartObserved);
        DrawCheck("Offline gameplay queued", report.offlineProgressQueued);
        DrawCheck("Reconnect uploaded pending data", report.reconnectSyncRecovered);
        DrawCheck("Cached forecast readable offline", report.offlineForecastCacheReadable);
        DrawCheck("App update preserved data", report.appUpdateObserved && report.dataPreservedAfterUpdate);
        DrawCheck("Average performance is 30+ FPS", sampledFrames >= 120 && report.averageFps >= 30f);
        DrawCheck(
            "No low-memory warning or runtime error",
            report.lowMemoryWarnings == 0 && report.runtimeErrors == 0);
        GUILayout.Space(8f);
        GUILayout.Label(
            $"Network: {report.internetReachability}\n" +
            $"Cloud: {report.cloudSyncState} | Pending: {report.pendingSyncRecords}\n" +
            $"Forecast: {report.forecastSyncState} | Cached: {report.cachedForecasts}\n" +
            $"FPS: {report.averageFps:0.0} | Worst frame: {report.worstFrameMilliseconds:0.0} ms\n" +
            $"Sessions: {report.gameplaySessions} | Attempts: {report.questionAttempts}\n" +
            $"Version code: {report.currentVersionCode} | Launch: {report.launchCount}",
            labelStyle);

        GUILayout.Space(8f);
        if (GUILayout.Button("Refresh cloud now", buttonStyle))
        {
            SupabaseSyncService.RequestSync();
            SupabaseForecastService.RequestRefresh();
        }
        if (GUILayout.Button("Save and print report", buttonStyle))
        {
            CaptureSnapshot();
            Debug.Log("WizKid Phase 6B report:\n" + JsonUtility.ToJson(report, true));
            Debug.Log("WizKid Phase 6B report path: " + ReportPath);
        }
        if (GUILayout.Button("Reset QA flags", buttonStyle))
            ResetTestRun();
        if (GUILayout.Button("Close", buttonStyle))
            overlayVisible = false;
        GUILayout.EndScrollView();
        GUILayout.EndArea();
        GUI.matrix = oldMatrix;
    }

    private void DrawCheck(string label, bool passed)
    {
        Color previous = GUI.color;
        GUI.color = passed ? new Color(0.35f, 0.9f, 0.45f) : new Color(1f, 0.78f, 0.28f);
        GUILayout.Label((passed ? "PASS  " : "WAIT  ") + label, labelStyle);
        GUI.color = previous;
    }

    private void EnsureStyles()
    {
        if (titleStyle != null)
            return;
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
        labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, fixedHeight = 44f };
    }

    private static long TotalRecords(LocalDataSummary summary)
    {
        return summary.learnerProfiles + summary.gameplaySessions +
               summary.questionAttempts + summary.levelResults;
    }

    private static long ParseLong(string value)
    {
        return long.TryParse(value, out long result) ? result : -1;
    }

    private static void SaveFlag(string name, bool value)
    {
        if (value)
            PlayerPrefs.SetInt(PreferencePrefix + name, 1);
    }

    private static bool LoadFlag(string name)
    {
        return PlayerPrefs.GetInt(PreferencePrefix + name, 0) == 1;
    }

    private static int GetAndroidVersionCode()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject packageManager = activity.Call<AndroidJavaObject>("getPackageManager"))
            using (AndroidJavaObject packageInfo = packageManager.Call<AndroidJavaObject>(
                       "getPackageInfo", Application.identifier, 0))
            {
                try
                {
                    return checked((int)packageInfo.Call<long>("getLongVersionCode"));
                }
                catch
                {
                    return packageInfo.Get<int>("versionCode");
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Could not read Android version code: " + exception.Message);
        }
#endif
        return 0;
    }
}

[Serializable]
public sealed class AndroidQualityReport
{
    public string recordedAtUtc;
    public string applicationVersion;
    public int currentVersionCode;
    public string buildGuid;
    public string unityVersion;
    public string deviceModel;
    public string operatingSystem;
    public string processorType;
    public int systemMemoryMb;
    public int graphicsMemoryMb;
    public string resolution;
    public string internetReachability;
    public long launchCount;
    public bool sqliteAvailable;
    public long learnerProfiles;
    public long gameplaySessions;
    public long questionAttempts;
    public long levelResults;
    public long pendingSyncRecords;
    public int cachedForecasts;
    public string cloudSyncState;
    public string forecastSyncState;
    public string lastCloudSyncUtc;
    public string lastForecastRefreshUtc;
    public float averageFps;
    public float worstFrameMilliseconds;
    public long totalAllocatedMemoryMb;
    public int lowMemoryWarnings;
    public int runtimeErrors;
    public bool offlineModeObserved;
    public bool offlineRestartObserved;
    public bool offlineProgressQueued;
    public bool reconnectSyncRecovered;
    public bool offlineForecastCacheReadable;
    public bool forecastRefreshRecovered;
    public bool recoverableErrorObserved;
    public bool appUpdateObserved;
    public bool dataPreservedAfterUpdate;
    public int completedChecks;
    public int totalChecks;
    public bool allRequiredChecksPassed;
}
