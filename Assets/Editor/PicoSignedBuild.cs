using System;
using System.IO;
using ByteDance.PICO.XR;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public static class PicoSignedBuild
{
    private const string RequestPath = ".superpowers/diagnostics/signed-build.request";
    private const string ResultPath = ".superpowers/diagnostics/signed-build-result.txt";

    [Serializable]
    private sealed class Credentials
    {
        public string keystorePath;
        public string alias;
        public string storePassword;
        public string keyPassword;
    }

    static PicoSignedBuild() { EditorApplication.delayCall += Run; }

    private static void Run()
    {
        if (!File.Exists(RequestPath)) return;
        File.Delete(RequestPath);
        BuildSignedApk();
    }

    [MenuItem("Tools/PICO/Build Signed APK")]
    public static void BuildSignedApk()
    {
        File.Delete(ResultPath);
        try
        {
            PXR_ProjectSetting.GetProjectConfig().handTracking = true;
            PXR_ProjectSetting.GetProjectConfig().handTrackingSupportType = HandTrackingSupport.HandsOnly;
            PXR_ProjectSetting.SaveAssets();
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".pico-keystore/trailblazer/credentials.json");
            var credentials = JsonUtility.FromJson<Credentials>(File.ReadAllText(path));
            PlayerSettings.Android.keystoreName = credentials.keystorePath;
            PlayerSettings.Android.keystorePass = credentials.storePassword;
            PlayerSettings.Android.keyaliasName = credentials.alias;
            PlayerSettings.Android.keyaliasPass = credentials.keyPassword;
            PlayerSettings.Android.useCustomKeystore = true;
            AssetDatabase.SaveAssets();
            var outputPath = Path.GetFullPath("Build/PICO/Trailblazer.apk");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/BikeScene.unity" },
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            });
            File.WriteAllText(ResultPath, $"Result: {report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nOutput: {report.summary.outputPath}");
        }
        catch (Exception exception)
        {
            File.WriteAllText(ResultPath, exception.ToString());
            Debug.LogException(exception);
        }
        finally
        {
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.Android.keystoreName = string.Empty;
            PlayerSettings.Android.keystorePass = string.Empty;
            PlayerSettings.Android.keyaliasName = string.Empty;
            PlayerSettings.Android.keyaliasPass = string.Empty;
            AssetDatabase.SaveAssets();
        }
    }
}
