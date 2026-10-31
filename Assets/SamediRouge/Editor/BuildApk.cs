using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SamediRouge.EditorTools
{
    /// <summary>Construit l'APK Android sans exiger de téléphone branché (Build And Run s'arrête sinon).</summary>
    public static class BuildApk
    {
        const string Path = "Builds/Android/BlackFridayRush.apk";

        [MenuItem("Samedi Rouge/Build APK")]
        public static void Build()
        {
            var scenes = new System.Collections.Generic.List<string>();
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes) if (s.enabled) scenes.Add(s.path);
            var opts = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = Path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };
            BuildReport r = BuildPipeline.BuildPlayer(opts);
            string msg = "Samedi Rouge APK: " + r.summary.result + " (" + Mathf.RoundToInt((float)r.summary.totalTime.TotalSeconds) + " s, " + (r.summary.totalSize / (1024 * 1024)) + " Mo)";
            Debug.Log(msg);
            try { System.IO.File.WriteAllText("Logs/sr-build.txt", msg + "\n"); } catch (System.Exception) { }
        }
    }
}
