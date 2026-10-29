using UnityEngine;

public class SaveSystem : MonoBehaviour
{
    private const string ScoreSuffix = "_Score";
    private const string UnlockedLevelSuffix = "_UnlockedLevel";

    public static void SaveProgress(string gameName, int levelNumber)
    {
        string normalizedGameName = NormalizeGameName(gameName);
        int completedLevel = Mathf.Clamp(levelNumber, 1, 5);
        int unlockedLevel = Mathf.Min(completedLevel + 1, 5);

        PlayerPrefs.SetString("LastGame", gameName);
        PlayerPrefs.SetInt("LastLevel", completedLevel);
        PlayerPrefs.SetInt("HasProgress", 1);

        if (!string.IsNullOrEmpty(normalizedGameName))
        {
            string unlockKey = normalizedGameName + UnlockedLevelSuffix;
            int currentUnlockedLevel = PlayerPrefs.GetInt(unlockKey, 1);
            if (unlockedLevel > currentUnlockedLevel)
                PlayerPrefs.SetInt(unlockKey, unlockedLevel);
        }

        PlayerPrefs.Save();
    }

    public static bool HasSavedProgress()
    {
        return PlayerPrefs.GetInt("HasProgress", 0) == 1;
    }

    public static string GetLastGame()
    {
        return PlayerPrefs.GetString("LastGame", "");
    }

    public static int GetLastLevel()
    {
        return PlayerPrefs.GetInt("LastLevel", 1);
    }

    public static void ClearProgress()
    {
        PlayerPrefs.DeleteKey("LastGame");
        PlayerPrefs.DeleteKey("LastLevel");
        PlayerPrefs.DeleteKey("HasProgress");
        PlayerPrefs.Save();
    }

    public static void SaveBestScore(string gameName, int score)
    {
        if (string.IsNullOrWhiteSpace(gameName))
        {
            Debug.LogWarning("Cannot save a score without a game name.");
            return;
        }

        string key = NormalizeGameName(gameName) + ScoreSuffix;
        int clampedScore = Mathf.Clamp(score, 0, 100);
        int currentBest = PlayerPrefs.GetInt(key, 0);

        if (clampedScore > currentBest)
        {
            PlayerPrefs.SetInt(key, clampedScore);
            PlayerPrefs.Save();
        }
    }

    public static int GetBestScore(string gameName)
    {
        if (string.IsNullOrWhiteSpace(gameName))
            return 0;

        string key = NormalizeGameName(gameName) + ScoreSuffix;
        return PlayerPrefs.GetInt(key, 0);
    }

    public static int GetHighestUnlockedLevel(string gameName)
    {
        string normalizedGameName = NormalizeGameName(gameName);
        if (string.IsNullOrEmpty(normalizedGameName))
            return 1;

        string unlockKey = normalizedGameName + UnlockedLevelSuffix;
        int unlockedLevel = PlayerPrefs.GetInt(unlockKey, 1);

        // Preserve progress made before the per-game unlock keys were introduced.
        if (GetLastGame().Replace(" ", "") == normalizedGameName)
            unlockedLevel = Mathf.Max(unlockedLevel, Mathf.Min(GetLastLevel() + 1, 5));

        return Mathf.Clamp(unlockedLevel, 1, 5);
    }

    private static string NormalizeGameName(string gameName)
    {
        return string.IsNullOrWhiteSpace(gameName) ? string.Empty : gameName.Replace(" ", "");
    }
}
