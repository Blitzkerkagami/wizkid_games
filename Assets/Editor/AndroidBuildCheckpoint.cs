using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class AndroidBuildCheckpoint
{
    private const string SqliteSourcePath = "Assets/Plugins/Android/SQLite/sqlite3.c";
    private const string SqliteHeaderPath = "Assets/Plugins/Android/SQLite/sqlite3.h";

    [MenuItem("WizKid/Phase 3A/Configure Android IL2CPP + ARM64")]
    public static void ConfigureAndroid()
    {
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
        AssetDatabase.SaveAssets();

        Debug.Log(
            "WizKid Android settings configured: IL2CPP, ARM64, and Gradle. " +
            "Run WizKid > Phase 3A > Validate Android Checkpoint next.");
    }

    [MenuItem("WizKid/Phase 3A/Validate Android Checkpoint")]
    public static void ValidateAndroidCheckpoint()
    {
        List<string> problems = GetProblems();
        string identifier = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);

        if (problems.Count == 0)
        {
            Debug.Log(
                "WizKid Phase 3A project checkpoint PASS: Android SQLite source, IL2CPP, ARM64, " +
                $"Gradle, Android module, and package name '{identifier}' are ready.");
            return;
        }

        Debug.LogWarning("WizKid Phase 3A checkpoint needs attention:\n- " + string.Join("\n- ", problems));
    }

    [MenuItem("WizKid/Phase 3A/Build Development APK")]
    public static void BuildDevelopmentApk()
    {
        BuildAndroid(false);
    }

    [MenuItem("WizKid/Phase 3A/Build and Run on Connected Device")]
    public static void BuildAndRunOnDevice()
    {
        BuildAndroid(true);
    }

    private static void BuildAndroid(bool runOnDevice)
    {
        List<string> problems = GetProblems();
        if (problems.Count > 0)
        {
            Debug.LogError("Android build blocked:\n- " + string.Join("\n- ", problems));
            return;
        }

        const string outputDirectory = "Builds/Android";
        const string outputPath = outputDirectory + "/WizKid-Development.apk";
        Directory.CreateDirectory(outputDirectory);

        var options = new BuildPlayerOptions
        {
            scenes = GetEnabledScenes(),
            locationPathName = outputPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.Development |
                (runOnDevice ? BuildOptions.AutoRunPlayer : BuildOptions.None)
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log(
                $"WizKid Android development build PASS: {outputPath} " +
                $"({report.summary.totalSize} bytes)." +
                (runOnDevice ? " Install/run was requested for the connected device." : string.Empty));
        }
        else
        {
            Debug.LogError($"WizKid Android development build failed: {report.summary.result}.");
        }
    }

    private static List<string> GetProblems()
    {
        var problems = new List<string>();

        if (!File.Exists(SqliteSourcePath))
            problems.Add($"Missing {SqliteSourcePath}");
        if (!File.Exists(SqliteHeaderPath))
            problems.Add($"Missing {SqliteHeaderPath}");
        if (PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android) != ScriptingImplementation.IL2CPP)
            problems.Add("Android scripting backend is not IL2CPP.");
        if (PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64)
            problems.Add("Android target architecture is not ARM64-only.");
        if (EditorUserBuildSettings.androidBuildSystem != AndroidBuildSystem.Gradle)
            problems.Add("Android build system is not Gradle.");
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            problems.Add("Unity Android Build Support is not installed for this Editor version.");

        string identifier = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
        if (string.IsNullOrWhiteSpace(identifier) || identifier.Contains("DefaultCompany"))
            problems.Add("Set a permanent Android package name before device/update testing.");

        if (GetEnabledScenes().Length == 0)
            problems.Add("No enabled scenes were found in Build Settings.");

        return problems;
    }

    private static string[] GetEnabledScenes()
    {
        var scenes = new List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            if (scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                scenes.Add(scene.path);
        return scenes.ToArray();
    }
}
