using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace SamediRouge
{
    /// <summary>
    /// Outil de dev (éditeur) : temps passé dans chaque partie du code par image. Les images anormalement
    /// longues sont écrites avec leur détail dans Logs/sr-perf.txt, pour trouver ce qui fait saccader le jeu.
    /// </summary>
    public static class Perf
    {
        public static bool On;
        static readonly Stopwatch sw = new Stopwatch();
        static readonly Dictionary<string, double> acc = new Dictionary<string, double>();
        static readonly Dictionary<string, int> cnt = new Dictionary<string, int>();
        static double last, avg = 4.0;
        static readonly System.Text.StringBuilder log = new System.Text.StringBuilder();
        static float flushT;

        [Conditional("UNITY_EDITOR")]
        public static void Frame()
        {
            if (!On) return;
            double total = sw.Elapsed.TotalMilliseconds;
            if (total > 0.0)
            {
                Add("unity(rendu,physique,vsync)", total - last);
                if (total > avg * 2.0 + 1.5)
                {
                    var l = new List<KeyValuePair<string, double>>(acc);
                    l.Sort((a, b) => b.Value.CompareTo(a.Value));
                    log.Append(Time.realtimeSinceStartup.ToString("0.00")).Append("s  total ").Append(total.ToString("0.0")).Append("ms (moy ").Append(avg.ToString("0.0")).Append(") :");
                    for (int i = 0; i < l.Count && i < 6; i++)
                        log.Append("  ").Append(l[i].Key).Append('=').Append(l[i].Value.ToString("0.0")).Append(cnt.TryGetValue(l[i].Key, out int n) && n > 1 ? "x" + n : "");
                    log.Append('\n');
                }
                avg = avg * 0.97 + total * 0.03;
            }
            acc.Clear(); cnt.Clear();
            sw.Restart();
            last = 0.0;
            if (Time.realtimeSinceStartup > flushT && log.Length > 0)
            {
                flushT = Time.realtimeSinceStartup + 3f;
                try { System.IO.File.AppendAllText("Logs/sr-perf.txt", log.ToString()); } catch (System.Exception) { }
                log.Length = 0;
            }
        }

        /// <summary>Événement de jeu noté dans le journal (pour caler les captures d'écran).</summary>
        [Conditional("UNITY_EDITOR")]
        public static void Note(string what)
        {
            if (!On) return;
            log.Append(Time.realtimeSinceStartup.ToString("0.00")).Append("s  >> ").Append(what).Append('\n');
        }

        /// <summary>Attribue le temps écoulé depuis la dernière marque à « name ».</summary>
        [Conditional("UNITY_EDITOR")]
        public static void Mark(string name)
        {
            if (!On) return;
            double t = sw.Elapsed.TotalMilliseconds;
            Add(name, t - last);
            last = t;
        }

        /// <summary>Mesure d'un bloc imbriqué : renvoie l'instant de départ, à passer à End.</summary>
        public static double Begin() => On ? sw.Elapsed.TotalMilliseconds : 0.0;

        [Conditional("UNITY_EDITOR")]
        public static void End(string name, double t0)
        {
            if (!On) return;
            Add(name, sw.Elapsed.TotalMilliseconds - t0);
        }

        static void Add(string name, double ms)
        {
            acc.TryGetValue(name, out double v); acc[name] = v + ms;
            cnt.TryGetValue(name, out int n); cnt[name] = n + 1;
        }
    }
}
