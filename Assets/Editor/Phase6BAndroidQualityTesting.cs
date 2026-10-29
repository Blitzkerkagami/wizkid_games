using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class Phase6BAndroidQualityTesting
{
    private const string OutputDirectory = "Builds/Android/Phase6B";
    private const string RuntimeServicePath = "Assets/Scripts/AndroidQualityTestService.cs";

    [MenuItem("WizKid/Phase 6B/Validate Android Quality Test Setup")]
    public static void ValidateSetup()
    {
        List<string> problems = GetProblems();
        List<string> notes = GetNotes();
        string identifier = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);

        if (problems.Count == 0)
        {
            Debug.Log(
                "WizKid Phase 6B setup PASS. " +
                $"Package={identifier}, version={PlayerSettings.bundleVersion}, " +
                $"versionCode={PlayerSettings.Android.bundleVersionCode}." +
                (notes.Count > 0 ? "\nNotes:\n- " + string.Join("\n- ", notes) : string.Empty));
            return;
        }

        Debug.LogWarning(
            "WizKid Phase 6B setup needs attention:\n- " +
            string.Join("\n- ", problems) +
            (notes.Count > 0 ? "\nNotes:\n- " + string.Join("\n- ", notes) : string.Empty));
    }

    [MenuItem("WizKid/Phase 6B/Build Initial Quality Test APK")]
    public static void BuildInitialApk()
    {
        BuildQualityApk(incrementVersionCode: false);
    }

    [MenuItem("WizKid/Phase 6B/Build Update Quality Test APK")]
    public static void BuildUpdateApk()
    {
        BuildQualityApk(incrementVersionCode: true);
    }

    [MenuItem("WizKid/Phase 6B/Open Android Quality Checklist")]
    public static void OpenChecklist()
    {
        UnityEngine.Object checklist = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
            "Assets/Data/PHASE6B_ANDROID_QUALITY_TESTING.md");
        if (checklist == null)
        {
            Debug.LogError("Phase 6B checklist was not found.");
            return;
        }

        Selection.activeObject = checklist;
        EditorGUIUtility.PingObject(checklist);
    }

    private static void BuildQualityApk(bool incrementVersionCode)
    {
        List<string> problems = GetProblems();
        if (problems.Count > 0)
        {
            Debug.LogError("Phase 6B Android build blocked:\n- " + string.Join("\n- ", problems));
            return;
        }

        if (incrementVersionCode)
        {
            PlayerSettings.Android.bundleVersionCode++;
            AssetDatabase.SaveAssets();
        }

        Directory.CreateDirectory(OutputDirectory);
        int versionCode = PlayerSettings.Android.bundleVersionCode;
        string suffix = incrementVersionCode ? "Update" : "Initial";
        string outputPath = $"{OutputDirectory}/WizKid-Phase6B-{suffix}-v{versionCode}.apk";

        var options = new BuildPlayerOptions
        {
            scenes = GetEnabledScenes(),
            locationPathName = outputPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.Development
        };

        BuildReport buildReport = BuildPipeline.BuildPlayer(options);
        if (buildReport.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError("Phase 6B Android build failed: " + buildReport.summary.result);
            return;
        }

        var manifest = new AndroidQualityBuildManifest
        {
            builtAtUtc = DateTime.UtcNow.ToString("o"),
            packageName = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android),
            applicationVersion = PlayerSettings.bundleVersion,
            versionCode = versionCode,
            apkPath = Path.GetFullPath(outputPath),
            apkSizeBytes = buildReport.summary.totalSize
        };
        File.WriteAllText(
            Path.Combine(OutputDirectory, $"WizKid-Phase6B-{suffix}-v{versionCode}.json"),
            JsonUtility.ToJson(manifest, true));

        Debug.Log(
            $"WizKid Phase 6B {suffix.ToLowerInvariant()} APK PASS: {outputPath}. " +
            "Install the update APK over the initial APK without uninstalling the app.");
    }

    private static List<string> GetProblems()
    {
        var problems = new List<string>();
        if (!File.Exists(RuntimeServicePath))
            problems.Add("The Phase 6B Android runtime test service is missing.");
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            problems.Add("Unity Android Build Support is not installed.");
        if (PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android) != ScriptingImplementation.IL2CPP)
            problems.Add("Android scripting backend must be IL2CPP.");
        if (PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64)
            problems.Add("Android target architecture must be ARM64-only for this checkpoint.");
        if (EditorUserBuildSettings.androidBuildSystem != AndroidBuildSystem.Gradle)
            problems.Add("Android build system must be Gradle.");

        string identifier = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
        if (string.IsNullOrWhiteSpace(identifier) || identifier.Contains("DefaultCompany"))
            problems.Add("Set a permanent Android package name before update testing.");
        if (PlayerSettings.Android.bundleVersionCode < 1)
            problems.Add("Android version code must be at least 1.");
        if (GetEnabledScenes().Length == 0)
            problems.Add("No enabled scenes were found in Build Settings.");
        return problems;
    }

    private static List<string> GetNotes()
    {
        var notes = new List<string>();
        if ((int)PlayerSettings.Android.minSdkVersion < 23)
        {
            notes.Add(
                "Minimum Android API is below 23. Include at least one currently supported " +
                "Android phone in the HTTPS/Supabase connectivity test.");
        }
        if (!EditorUserBuildSettings.development)
            notes.Add("The Phase 6B build commands enable Development Build automatically.");
        return notes;
    }

    private static string[] GetEnabledScenes()
    {
        var scenes = new List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            if (scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                scenes.Add(scene.path);
        return scenes.ToArray();
    }

    [Serializable]
    private sealed class AndroidQualityBuildManifest
    {
        public string builtAtUtc;
        public string packageName;
        public string applicationVersion;
        public int versionCode;
        public string apkPath;
        public ulong apkSizeBytes;
    }
}
