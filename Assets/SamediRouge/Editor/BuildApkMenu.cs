using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SamediRouge.EditorTools
{
    /// <summary>Construit l'APK Android sans avoir besoin d'un téléphone branché (Build And Run échoue sinon).</summary>
    public static class BuildApkMenu
    {
        [MenuItem("Samedi Rouge/Build APK (sans téléphone)")]
        public static void BuildApk()
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var opts = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = "Builds/Android/BlackFridayRush.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None
            };
            BuildReport r = BuildPipeline.BuildPlayer(opts);
            Debug.Log("[BuildApk] " + r.summary.result + " — " + r.summary.totalSize / (1024 * 1024) + " Mo");
        }
    }
}
