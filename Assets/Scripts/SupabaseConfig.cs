using System;
using UnityEngine;

[Serializable]
public sealed class SupabaseConfig
{
    public bool enableSync;
    public string projectUrl;
    public string publishableKey;
    public int syncIntervalSeconds = 30;
    public int batchSize = 100;

    public static bool TryLoad(out SupabaseConfig config, out string error)
    {
        config = null;
        error = string.Empty;

        TextAsset asset = Resources.Load<TextAsset>("SupabaseConfig");
        if (asset == null)
        {
            error = "Resources/SupabaseConfig.json was not found.";
            return false;
        }

        try
        {
            config = JsonUtility.FromJson<SupabaseConfig>(asset.text);
        }
        catch (Exception exception)
        {
            error = $"SupabaseConfig.json is invalid JSON: {exception.Message}";
            return false;
        }

        if (config == null)
        {
            error = "SupabaseConfig.json is empty.";
            return false;
        }

        config.projectUrl = (config.projectUrl ?? string.Empty).Trim().TrimEnd('/');
        config.publishableKey = (config.publishableKey ?? string.Empty).Trim();
        config.syncIntervalSeconds = Mathf.Clamp(config.syncIntervalSeconds, 10, 300);
        config.batchSize = Mathf.Clamp(config.batchSize, 1, 100);

        if (!config.enableSync)
        {
            error = "Cloud sync is disabled in SupabaseConfig.json.";
            return false;
        }

        if (!Uri.TryCreate(config.projectUrl, UriKind.Absolute, out Uri uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && !IsLocalDevelopmentUri(uri)))
        {
            error = "projectUrl must be an HTTPS Supabase URL (HTTP is allowed only for localhost).";
            return false;
        }

        if (!config.publishableKey.StartsWith("sb_publishable_", StringComparison.Ordinal))
        {
            error = "Use the Supabase publishable key (sb_publishable_...), never a secret/service-role key.";
            return false;
        }

        return true;
    }

    private static bool IsLocalDevelopmentUri(Uri uri)
    {
        return uri.Scheme == Uri.UriSchemeHttp &&
               (uri.Host == "localhost" || uri.Host == "127.0.0.1");
    }
}
