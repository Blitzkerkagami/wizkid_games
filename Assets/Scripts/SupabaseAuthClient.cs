using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Minimal Supabase Auth REST client. It creates an anonymous authenticated user
/// and refreshes the session without putting a server secret inside the game.
/// </summary>
public sealed class SupabaseAuthClient
{
    private const string AccessTokenKey = "SupabaseAccessToken";
    private const string RefreshTokenKey = "SupabaseRefreshToken";
    private const string UserIdKey = "SupabaseUserId";
    private const string ExpiresAtKey = "SupabaseExpiresAtUtc";

    private static string sharedAccessToken = string.Empty;
    private static string sharedUserId = string.Empty;
    private static string sharedLastError = string.Empty;
    private static bool sessionRequestInProgress;

    private readonly SupabaseConfig config;

    public string AccessToken => sharedAccessToken;
    public string UserId => sharedUserId;
    public string LastError => sharedLastError;

    public SupabaseAuthClient(SupabaseConfig config)
    {
        this.config = config;
        if (string.IsNullOrEmpty(sharedAccessToken))
            sharedAccessToken = PlayerPrefs.GetString(AccessTokenKey, string.Empty);
        if (string.IsNullOrEmpty(sharedUserId))
            sharedUserId = PlayerPrefs.GetString(UserIdKey, string.Empty);
    }

    public IEnumerator EnsureSession(Action<bool> completed)
    {
        sharedLastError = string.Empty;
        long expiresAt = ReadLong(ExpiresAtKey);
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (!string.IsNullOrEmpty(AccessToken) && !string.IsNullOrEmpty(UserId) && expiresAt > now + 60)
        {
            completed(true);
            yield break;
        }

        if (sessionRequestInProgress)
        {
            while (sessionRequestInProgress)
                yield return null;

            expiresAt = ReadLong(ExpiresAtKey);
            now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            completed(!string.IsNullOrEmpty(AccessToken) && !string.IsNullOrEmpty(UserId) &&
                      expiresAt > now + 60);
            yield break;
        }

        sessionRequestInProgress = true;

        string refreshToken = PlayerPrefs.GetString(RefreshTokenKey, string.Empty);
        if (!string.IsNullOrEmpty(refreshToken))
        {
            string refreshJson = "{\"refresh_token\":\"" + EscapeJson(refreshToken) + "\"}";
            yield return SendAuthRequest(
                "/auth/v1/token?grant_type=refresh_token",
                refreshJson,
                response => SaveSession(response));

            if (!string.IsNullOrEmpty(AccessToken))
            {
                sessionRequestInProgress = false;
                completed(true);
                yield break;
            }

            ClearSession();
        }

        // With Anonymous Sign-Ins enabled, an empty signup body creates a user and session.
        yield return SendAuthRequest("/auth/v1/signup", "{}", response => SaveSession(response));
        sessionRequestInProgress = false;
        completed(!string.IsNullOrEmpty(AccessToken) && !string.IsNullOrEmpty(UserId));
    }

    public void InvalidateAccessToken()
    {
        sharedAccessToken = string.Empty;
        PlayerPrefs.DeleteKey(AccessTokenKey);
        PlayerPrefs.SetString(ExpiresAtKey, "0");
        PlayerPrefs.Save();
    }

    private IEnumerator SendAuthRequest(string path, string json, Action<SupabaseAuthResponse> onSuccess)
    {
        sharedAccessToken = string.Empty;
        byte[] body = Encoding.UTF8.GetBytes(json);
        using (UnityWebRequest request = new UnityWebRequest(config.projectUrl + path, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 20;
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("apikey", config.publishableKey);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                sharedLastError = $"Auth HTTP {request.responseCode}: {ReadableResponse(request)}";
                yield break;
            }

            SupabaseAuthResponse response;
            try
            {
                response = JsonUtility.FromJson<SupabaseAuthResponse>(request.downloadHandler.text);
            }
            catch (Exception exception)
            {
                sharedLastError = "Could not read the Supabase Auth response: " + exception.Message;
                yield break;
            }

            if (response == null || string.IsNullOrEmpty(response.access_token) ||
                string.IsNullOrEmpty(response.refresh_token) || response.user == null ||
                string.IsNullOrEmpty(response.user.id))
            {
                sharedLastError = "Supabase Auth returned an incomplete session. Check that Anonymous Sign-Ins is enabled.";
                yield break;
            }

            onSuccess(response);
        }
    }

    private void SaveSession(SupabaseAuthResponse response)
    {
        sharedAccessToken = response.access_token;
        sharedUserId = response.user.id;
        long lifetime = Math.Max(60L, response.expires_in);
        long expiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + lifetime;

        PlayerPrefs.SetString(AccessTokenKey, response.access_token);
        PlayerPrefs.SetString(RefreshTokenKey, response.refresh_token);
        PlayerPrefs.SetString(UserIdKey, response.user.id);
        PlayerPrefs.SetString(ExpiresAtKey, expiresAt.ToString());
        PlayerPrefs.Save();
        sharedLastError = string.Empty;
    }

    private void ClearSession()
    {
        sharedAccessToken = string.Empty;
        sharedUserId = string.Empty;
        PlayerPrefs.DeleteKey(AccessTokenKey);
        PlayerPrefs.DeleteKey(RefreshTokenKey);
        PlayerPrefs.DeleteKey(UserIdKey);
        PlayerPrefs.DeleteKey(ExpiresAtKey);
        PlayerPrefs.Save();
    }

    private static long ReadLong(string key)
    {
        return long.TryParse(PlayerPrefs.GetString(key, "0"), out long value) ? value : 0L;
    }

    private static string EscapeJson(string value)
    {
        return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string ReadableResponse(UnityWebRequest request)
    {
        string text = request.downloadHandler?.text;
        return string.IsNullOrWhiteSpace(text) ? request.error : text;
    }

    [Serializable]
    private sealed class SupabaseAuthResponse
    {
        public string access_token = string.Empty;
        public string refresh_token = string.Empty;
        public long expires_in = 0;
        public SupabaseAuthUser user = null;
    }

    [Serializable]
    private sealed class SupabaseAuthUser
    {
        public string id = string.Empty;
    }
}
