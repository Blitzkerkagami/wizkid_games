using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public enum EvaluationSyncState
{
    Disabled,
    Offline,
    Authenticating,
    Uploading,
    UpToDate,
    RetryScheduled
}

/// <summary>
/// Uploads locally saved UAT and ISO 25010 responses using the authenticated
/// learner session. No respondent name, birthday, contact detail, or server key
/// is included in the payload.
/// </summary>
public sealed class EvaluationSyncService : MonoBehaviour
{
    private const float RetryDelaySeconds = 30f;
    private static EvaluationSyncService instance;

    public static EvaluationSyncState State { get; private set; } = EvaluationSyncState.Disabled;
    public static string LastError { get; private set; } = string.Empty;
    public static string LastSuccessfulSyncUtc { get; private set; } = string.Empty;
    public static int LastUploadedCount { get; private set; }
    public static event Action StatusChanged;

    private SupabaseConfig config;
    private SupabaseAuthClient auth;
    private bool syncRequested = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;
        GameObject host = new GameObject("Evaluation Response Sync");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<EvaluationSyncService>();
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
            SetState(EvaluationSyncState.Disabled, error);
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
            if (State == EvaluationSyncState.Offline || State == EvaluationSyncState.RetryScheduled)
            {
                yield return new WaitForSecondsRealtime(RetryDelaySeconds);
                syncRequested = true;
            }
        }
    }

    private IEnumerator SyncOnce()
    {
        EvaluationResponseRecord[] responses = LocalDatabase.GetPendingEvaluationResponses(config.batchSize);
        if (responses.Length == 0)
        {
            SetState(EvaluationSyncState.UpToDate);
            yield break;
        }

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            SetState(EvaluationSyncState.Offline,
                "Offline. Evaluation responses are saved safely and will sync later.");
            yield break;
        }

        SetState(EvaluationSyncState.Authenticating);
        bool authenticated = false;
        yield return auth.EnsureSession(success => authenticated = success);
        if (!authenticated)
        {
            SetState(EvaluationSyncState.RetryScheduled, auth.LastError);
            yield break;
        }

        SetState(EvaluationSyncState.Uploading);
        bool uploaded = false;
        long responseCode = 0;
        string error = string.Empty;
        yield return Upload(responses, (success, code, message) =>
        {
            uploaded = success;
            responseCode = code;
            error = message;
        });

        if (!uploaded && responseCode == 401)
        {
            auth.InvalidateAccessToken();
            authenticated = false;
            yield return auth.EnsureSession(success => authenticated = success);
            if (authenticated)
            {
                yield return Upload(responses, (success, code, message) =>
                {
                    uploaded = success;
                    responseCode = code;
                    error = message;
                });
            }
            else
            {
                error = auth.LastError;
            }
        }

        if (!uploaded)
        {
            SetState(EvaluationSyncState.RetryScheduled,
                $"Evaluation sync HTTP {responseCode}: {error}");
            yield break;
        }

        string syncedAt = DateTime.UtcNow.ToString("o");
        if (!LocalDatabase.MarkEvaluationResponsesSynced(responses, syncedAt))
        {
            SetState(EvaluationSyncState.RetryScheduled,
                "Responses were uploaded but their local sync status could not be saved.");
            yield break;
        }

        LastUploadedCount = responses.Length;
        LastSuccessfulSyncUtc = syncedAt;
        SetState(EvaluationSyncState.UpToDate);
        if (responses.Length >= config.batchSize)
            syncRequested = true;
    }

    private IEnumerator Upload(
        EvaluationResponseRecord[] responses,
        Action<bool, long, string> completed)
    {
        string payload = "[" + string.Join(",", responses.Select(ToCloudJson)) + "]";
        string url = config.projectUrl +
                     "/rest/v1/evaluation_responses?on_conflict=response_id";

        using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 30;
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Prefer", "resolution=ignore-duplicates,return=minimal");
            request.SetRequestHeader("apikey", config.publishableKey);
            request.SetRequestHeader("Authorization", "Bearer " + auth.AccessToken);

            yield return request.SendWebRequest();
            bool success = request.result == UnityWebRequest.Result.Success &&
                           request.responseCode >= 200 && request.responseCode < 300;
            string message = request.downloadHandler?.text;
            if (string.IsNullOrWhiteSpace(message))
                message = request.error;
            completed(success, request.responseCode, message ?? string.Empty);
        }
    }

    private static string ToCloudJson(EvaluationResponseRecord row)
    {
        return "{" +
               "\"response_id\":\"" + Escape(row.response_id) + "\"," +
               "\"instrument\":\"" + Escape(row.instrument) + "\"," +
               "\"respondent_role\":\"" + Escape(row.respondent_role) + "\"," +
               "\"ratings\":" + SafeJsonObject(row.ratings_json) + "," +
               "\"category_scores\":" + SafeJsonObject(row.category_scores_json) + "," +
               "\"overall_mean\":" + row.overall_mean.ToString("0.00", CultureInfo.InvariantCulture) + "," +
               "\"interpretation\":\"" + Escape(row.interpretation) + "\"," +
               "\"comments\":\"" + Escape(row.comments) + "\"," +
               "\"completed_at_utc\":\"" + Escape(row.completed_at_utc) + "\"," +
               "\"app_version\":\"" + Escape(row.app_version) + "\"," +
               "\"platform\":\"" + Escape(row.platform) + "\"" +
               "}";
    }

    private static string SafeJsonObject(string value)
    {
        string trimmed = (value ?? string.Empty).Trim();
        return trimmed.StartsWith("{", StringComparison.Ordinal) &&
               trimmed.EndsWith("}", StringComparison.Ordinal)
            ? trimmed
            : "{\"items\":[]}";
    }

    private static string Escape(string value)
    {
        return (value ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }

    private static void SetState(EvaluationSyncState state, string error = "")
    {
        State = state;
        LastError = error ?? string.Empty;
        StatusChanged?.Invoke();
        if (state == EvaluationSyncState.RetryScheduled)
            Debug.LogWarning("Evaluation responses will retry syncing. " + LastError);
    }
}
