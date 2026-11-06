using UnityEditor;
using UnityEngine;

namespace SamediRouge
{
    /// <summary>Menu Samedi Rouge > Rhythm : accès aux rapports de rythme écrits pendant les parties (mode arcade).</summary>
    public static class RhythmMenu
    {
        [MenuItem("Samedi Rouge/Rhythm/Open report (last runs)")]
        static void OpenReport()
        {
            string p = System.IO.Path.Combine(Rhythm.Dir, "rhythm-report.md");
            if (System.IO.File.Exists(p)) EditorUtility.OpenWithDefaultApp(p);
            else Debug.Log("Pas encore de rapport : joue une course en mode Play (" + p + ")");
        }

        [MenuItem("Samedi Rouge/Rhythm/Show data folder")]
        static void Reveal() => EditorUtility.RevealInFinder(Rhythm.Dir + "/");

        [MenuItem("Samedi Rouge/Rhythm/Reload thresholds (rhythm.json)")]
        static void Reload() { RhythmConfig.Reload(); Debug.Log("Seuils de rythme rechargés"); }

        [MenuItem("Samedi Rouge/Rhythm/Clear logs")]
        static void Clear()
        {
            foreach (string f in new[] { "rhythm-report.md", "rhythm-runs.csv", "rhythm-events.jsonl" })
            {
                string p = System.IO.Path.Combine(Rhythm.Dir, f);
                if (System.IO.File.Exists(p)) System.IO.File.Delete(p);
            }
            Debug.Log("Journaux de rythme effacés");
        }
    }
}
