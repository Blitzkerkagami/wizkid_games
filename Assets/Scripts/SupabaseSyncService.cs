using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public enum CloudSyncState
{
    Disabled,
    WaitingForNetwork,
    Authenticating,
    Uploading,
    UpToDate,
    RetryScheduled
}

/// <summary>
/// Persistent offline-first uploader. Local SQLite remains the source of truth;
/// only rows marked pending are sent, and they are marked synced after the RPC
/// transaction succeeds.
/// </summary>
public sealed class SupabaseSyncService : MonoBehaviour
{
    private const float MaximumRetrySeconds = 300f;
    private static SupabaseSyncService instance;

    public static CloudSyncState State { get; private set; } = CloudSyncState.Disabled;
    public static string LastError { get; private set; } = string.Empty;
    public static string LastSuccessfulSyncUtc { get; private set; } = string.Empty;
    public static int LastUploadedRecordCount { get; private set; }
    public static event Action StatusChanged;

    private SupabaseConfig config;
    private SupabaseAuthClient auth;
    private float retrySeconds = 5f;
    private bool syncRequested = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject host = new GameObject("Supabase Cloud Sync");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<SupabaseSyncService>();
    }

    public static void RequestSync()
    {
        if (instance != null)
            instance.syncRequested = true;
    }

    private void Awake()
    {
        if (!SupabaseConfig.TryLoad(out config, out string error))
        {
            SetState(CloudSyncState.Disabled, error);
            enabled = false;
            return;
        }

        auth = new SupabaseAuthClient(config);
        LocalDatabase.Initialize();
        StartCoroutine(SyncLoop());
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            syncRequested = true;
    }

    private void OnApplicationPause(bool isPaused)
    {
        if (!isPaused)
            syncRequested = true;
    }

    private IEnumerator SyncLoop()
    {
        while (true)
        {
            if (!syncRequested)
            {
                yield return new WaitForSecondsRealtime(1f);
                continue;
            }

            syncRequested = false;
            yield return SyncOnce();

            float delay = State == CloudSyncState.RetryScheduled
                ? retrySeconds
                : config.syncIntervalSeconds;

            if (State == CloudSyncState.RetryScheduled)
                retrySeconds = Mathf.Min(retrySeconds * 2f, MaximumRetrySeconds);
            else
                retrySeconds = 5f;

            yield return new WaitForSecondsRealtime(delay);
            syncRequested = true;
        }
    }

    private IEnumerator SyncOnce()
    {
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            SetState(CloudSyncState.WaitingForNetwork, "No internet connection. Local progress is safe and will retry later.");
            yield break;
        }

        SetState(CloudSyncState.Authenticating);
        bool authenticated = false;
        yield return auth.EnsureSession(result => authenticated = result);
        if (!authenticated)
        {
            ScheduleRetry(auth.LastError);
            yield break;
        }

        CloudSyncBatch batch = LocalDatabase.GetPendingCloudSyncBatch(config.batchSize);
        if (batch == null)
        {
            ScheduleRetry("Could not read pending records from local SQLite.");
            yield break;
        }

        if (batch.IsEmpty)
        {
            SetState(CloudSyncState.UpToDate);
            yield break;
        }

        SetState(CloudSyncState.Uploading);
        bool uploaded = false;
        long responseCode = 0;
        string uploadError = string.Empty;
        yield return UploadBatch(batch, (success, code, error) =>
        {
            uploaded = success;
            responseCode = code;
            uploadError = error;
        });

        if (!uploaded && responseCode == 401)
        {
            auth.InvalidateAccessToken();
            bool refreshed = false;
            yield return auth.EnsureSession(result => refreshed = result);
            if (refreshed)
            {
                yield return UploadBatch(batch, (success, code, error) =>
                {
                    uploaded = success;
                    responseCode = code;
                    uploadError = error;
                });
            }
            else
            {
                uploadError = auth.LastError;
            }
        }

        if (!uploaded)
        {
            ScheduleRetry($"Cloud sync HTTP {responseCode}: {uploadError}");
            yield break;
        }

        if (!LocalDatabase.MarkCloudSyncBatchSynced(batch, DateTime.UtcNow.ToString("o")))
        {
            // Safe to retry: the server RPC is idempotent and uses client revisions.
            ScheduleRetry("Upload succeeded, but local sync markers could not be saved.");
            yield break;
        }

        LastUploadedRecordCount = batch.Count;
        LastSuccessfulSyncUtc = DateTime.UtcNow.ToString("o");
        SetState(CloudSyncState.UpToDate);

        // Drain another batch immediately instead of waiting for the interval.
        if (batch.Count >= config.batchSize)
            syncRequested = true;
    }

    private IEnumerator UploadBatch(CloudSyncBatch batch, Action<bool, long, string> completed)
    {
        string envelope = "{\"payload\":" + JsonUtility.ToJson(batch) + "}";
        byte[] body = Encoding.UTF8.GetBytes(envelope);
        string url = config.projectUrl + "/rest/v1/rpc/sync_learning_batch";

        using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 30;
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("apikey", config.publishableKey);
            request.SetRequestHeader("Authorization", "Bearer " + auth.AccessToken);

            yield return request.SendWebRequest();

            bool success = request.result == UnityWebRequest.Result.Success &&
                           request.responseCode >= 200 && request.responseCode < 300;
            string error = request.downloadHandler?.text;
            if (string.IsNullOrWhiteSpace(error))
                error = request.error;
            completed(success, request.responseCode, error ?? string.Empty);
        }
    }

    private static void ScheduleRetry(string error)
    {
        SetState(CloudSyncState.RetryScheduled, error);
    }

    private static void SetState(CloudSyncState state, string error = "")
    {
        State = state;
        LastError = error ?? string.Empty;
        StatusChanged?.Invoke();

        if (state == CloudSyncState.RetryScheduled)
            Debug.LogWarning("Supabase sync will retry. " + LastError);
    }
}
