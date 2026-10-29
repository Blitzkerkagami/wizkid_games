using System;
using System.Collections;
using UnityEngine;

public enum ForecastSyncState
{
    Disabled,
    Offline,
    Authenticating,
    Downloading,
    Ready,
    RetryScheduled
}

/// <summary>
/// Downloads forecasts after cloud sync and stores them in SQLite for offline display.
/// </summary>
public sealed class SupabaseForecastService : MonoBehaviour
{
    private const float RetryDelaySeconds = 30f;
    private static SupabaseForecastService instance;

    public static ForecastSyncState State { get; private set; } = ForecastSyncState.Disabled;
    public static string LastError { get; private set; } = string.Empty;
    public static string LastSuccessfulRefreshUtc { get; private set; } = string.Empty;
    public static int LastDownloadedForecastCount { get; private set; }
    public static int LastDownloadedValidationCount { get; private set; }
    public static string LastValidationError { get; private set; } = string.Empty;
    public static event Action StatusChanged;
    public static event Action ForecastsChanged;

    private SupabaseConfig config;
    private SupabaseAuthClient auth;
    private SupabaseForecastClient client;
    private string playerId = string.Empty;
    private bool refreshRequested = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject host = new GameObject("Supabase Forecast Sync");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<SupabaseForecastService>();
    }

    public static void RequestRefresh()
    {
        if (instance != null)
            instance.refreshRequested = true;
    }

    private void Awake()
    {
        if (!SupabaseConfig.TryLoad(out config, out string error))
        {
            SetState(ForecastSyncState.Disabled, error);
            enabled = false;
            return;
        }

        auth = new SupabaseAuthClient(config);
        client = new SupabaseForecastClient(config);
        LocalDatabase.Initialize();
        playerId = LocalDatabase.UpsertCurrentLearnerProfile();
        SupabaseSyncService.StatusChanged += HandleCloudSyncStatusChanged;
        StartCoroutine(RefreshLoop());
    }

    private void OnDestroy()
    {
        SupabaseSyncService.StatusChanged -= HandleCloudSyncStatusChanged;
        if (instance == this)
            instance = null;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            refreshRequested = true;
    }

    private void OnApplicationPause(bool isPaused)
    {
        if (!isPaused)
            refreshRequested = true;
    }

    private void HandleCloudSyncStatusChanged()
    {
        if (SupabaseSyncService.State == CloudSyncState.UpToDate)
            refreshRequested = true;
    }

    private IEnumerator RefreshLoop()
    {
        while (true)
        {
            if (!refreshRequested)
            {
                yield return new WaitForSecondsRealtime(1f);
                continue;
            }

            refreshRequested = false;
            yield return RefreshOnce();

            if (State == ForecastSyncState.Offline || State == ForecastSyncState.RetryScheduled)
            {
                yield return new WaitForSecondsRealtime(RetryDelaySeconds);
                refreshRequested = true;
            }
        }
    }

    private IEnumerator RefreshOnce()
    {
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            SetState(ForecastSyncState.Offline,
                "Offline. Showing the most recently downloaded forecast.");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(playerId))
            playerId = LocalDatabase.UpsertCurrentLearnerProfile();
        if (string.IsNullOrWhiteSpace(playerId))
        {
            SetState(ForecastSyncState.RetryScheduled,
                "The local learner profile is not available yet.");
            yield break;
        }

        SetState(ForecastSyncState.Authenticating);
        bool authenticated = false;
        yield return auth.EnsureSession(success => authenticated = success);
        if (!authenticated)
        {
            SetState(ForecastSyncState.RetryScheduled, auth.LastError);
            yield break;
        }

        bool downloaded = false;
        long responseCode = 0;
        string downloadError = string.Empty;
        LearnerForecastRecord[] forecasts = Array.Empty<LearnerForecastRecord>();
        ForecastReadinessRecord[] readiness = Array.Empty<ForecastReadinessRecord>();

        SetState(ForecastSyncState.Downloading);
        yield return DownloadAll((success, code, error, forecastRows, readinessRows) =>
        {
            downloaded = success;
            responseCode = code;
            downloadError = error;
            forecasts = forecastRows;
            readiness = readinessRows;
        });

        if (!downloaded && responseCode == 401)
        {
            auth.InvalidateAccessToken();
            authenticated = false;
            yield return auth.EnsureSession(success => authenticated = success);
            if (authenticated)
            {
                yield return DownloadAll((success, code, error, forecastRows, readinessRows) =>
                {
                    downloaded = success;
                    responseCode = code;
                    downloadError = error;
                    forecasts = forecastRows;
                    readiness = readinessRows;
                });
            }
            else
            {
                downloadError = auth.LastError;
            }
        }

        if (!downloaded)
        {
            SetState(ForecastSyncState.RetryScheduled,
                $"Forecast download HTTP {responseCode}: {downloadError}");
            yield break;
        }

        bool validationDownloaded = false;
        long validationCode = 0;
        string validationError = string.Empty;
        ForecastValidationRecord[] validationRows = Array.Empty<ForecastValidationRecord>();
        yield return client.FetchValidationResults(auth.AccessToken, playerId,
            (success, code, error, rows) =>
            {
                validationDownloaded = success;
                validationCode = code;
                validationError = error;
                validationRows = rows;
            });

        string cachedAtUtc = DateTime.UtcNow.ToString("o");
        if (!LocalDatabase.ReplaceForecastCache(playerId, forecasts, cachedAtUtc) ||
            !LocalDatabase.ReplaceForecastReadinessCache(playerId, readiness, cachedAtUtc))
        {
            SetState(ForecastSyncState.RetryScheduled,
                "Forecasts were downloaded but could not be cached locally.");
            yield break;
        }

        if (validationDownloaded)
        {
            if (LocalDatabase.ReplaceForecastValidationCache(playerId, validationRows, cachedAtUtc))
            {
                LastDownloadedValidationCount = validationRows.Length;
                LastValidationError = string.Empty;
            }
            else
            {
                LastValidationError = "The accuracy report was downloaded but could not be saved offline.";
            }
        }
        else
        {
            LastValidationError =
                $"Accuracy report HTTP {validationCode}: {validationError}";
            Debug.LogWarning(
                "Forecast validation data is not available yet. Existing offline report data was kept. " +
                LastValidationError);
        }

        LastDownloadedForecastCount = forecasts.Length;
        LastSuccessfulRefreshUtc = cachedAtUtc;
        SetState(ForecastSyncState.Ready);
        ForecastsChanged?.Invoke();
    }

    private IEnumerator DownloadAll(
        Action<bool, long, string, LearnerForecastRecord[], ForecastReadinessRecord[]> completed)
    {
        bool forecastsOk = false;
        long forecastsCode = 0;
        string forecastsError = string.Empty;
        LearnerForecastRecord[] forecasts = Array.Empty<LearnerForecastRecord>();

        yield return client.FetchForecasts(auth.AccessToken, playerId,
            (success, code, error, rows) =>
            {
                forecastsOk = success;
                forecastsCode = code;
                forecastsError = error;
                forecasts = rows;
            });

        if (!forecastsOk)
        {
            completed(false, forecastsCode, forecastsError, forecasts,
                Array.Empty<ForecastReadinessRecord>());
            yield break;
        }

        bool readinessOk = false;
        long readinessCode = 0;
        string readinessError = string.Empty;
        ForecastReadinessRecord[] readiness = Array.Empty<ForecastReadinessRecord>();
        yield return client.FetchReadiness(auth.AccessToken, playerId,
            (success, code, error, rows) =>
            {
                readinessOk = success;
                readinessCode = code;
                readinessError = error;
                readiness = rows;
            });

        completed(readinessOk, readinessCode, readinessError, forecasts, readiness);
    }

    private static void SetState(ForecastSyncState state, string error = "")
    {
        State = state;
        LastError = error ?? string.Empty;
        StatusChanged?.Invoke();

        if (state == ForecastSyncState.RetryScheduled)
            Debug.LogWarning("Forecast sync will retry. " + LastError);
    }
}

