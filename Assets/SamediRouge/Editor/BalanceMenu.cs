using System.IO;
using UnityEditor;
using UnityEngine;

namespace SamediRouge.EditorTools
{
    /// <summary>
    /// Outils d'équilibrage (menu Samedi Rouge > Balance) :
    /// - exporter la configuration en JSON (Resources/SamediRouge/balance.json), à modifier sans toucher au code ;
    /// - générer le rapport de difficulté (Assets/SamediRouge/Balance/difficulty-report.md).
    /// </summary>
    public static class BalanceMenu
    {
        const string JsonPath = "Assets/SamediRouge/Resources/SamediRouge/balance.json";
        const string ReportPath = "Assets/SamediRouge/Balance/difficulty-report.md";

        static void Load()
        {
            BalanceConfig.I = new BalanceConfig();
            if (File.Exists(JsonPath)) JsonUtility.FromJsonOverwrite(File.ReadAllText(JsonPath), BalanceConfig.I);
            Balance.ClearCache();
        }

        [MenuItem("Samedi Rouge/Balance/Export balance.json")]
        public static void Export()
        {
            Load();
            Directory.CreateDirectory(Path.GetDirectoryName(JsonPath));
            File.WriteAllText(JsonPath, JsonUtility.ToJson(BalanceConfig.I, true));
            AssetDatabase.Refresh();
            Debug.Log("Balance config written to " + JsonPath + " (edit it, then run the difficulty report).");
            EditorUtility.RevealInFinder(JsonPath);
        }

        /// <summary>Après un changement d'équilibrage : recalibre tous les plans (long) et écrit les lignes à coller dans BalanceConfig.</summary>
        [MenuItem("Samedi Rouge/Balance/Bake calibrated plans")]
        public static void Bake()
        {
            Load();
            EditorUtility.DisplayProgressBar("Black Friday Rush", "Calibrating every street and variant...", 0.5f);
            try
            {
                const string path = "Logs/balance-bake.txt";
                File.WriteAllText(path, Balance.BakeText());
                Debug.Log("Calibrated plans written to " + path + " (paste them into BalanceConfig).");
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        [MenuItem("Samedi Rouge/Balance/Difficulty report")]
        public static void Report()
        {
            Load();
            EditorUtility.DisplayProgressBar("Black Friday Rush", "Scoring every street and variant...", 0.5f);
            try
            {
                string md = DifficultyModel.Report(Tuning.StreetNames, Tuning.MapNames, Tuning.MapFirst);
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                File.WriteAllText(ReportPath, md);
                AssetDatabase.Refresh();
                Debug.Log("Difficulty report written to " + ReportPath);
                EditorUtility.RevealInFinder(ReportPath);
            }
            finally { EditorUtility.ClearProgressBar(); }
        }
    }
}
