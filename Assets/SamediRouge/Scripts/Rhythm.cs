using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace SamediRouge
{
    /// <summary>
    /// Seuils de l'analyse de rythme (brief « Level Difficulty Analyzer », §4). Modifiables sans code dans
    /// Resources/SamediRouge/rhythm.json. Ce sont des hypothèses à tester, pas des règles.
    /// </summary>
    [Serializable]
    public class RhythmConfig
    {
        /// <summary>Cible : une décision toutes les X à Y s.</summary>
        public float decisionMin = 2f, decisionMax = 3f;
        /// <summary>Cible : une récompense visible toutes les X à Y s.</summary>
        public float rewardMin = 4f, rewardMax = 6f;
        /// <summary>Cible : un moment spectaculaire toutes les X à Y s.</summary>
        public float spectacleMin = 8f, spectacleMax = 12f;
        /// <summary>Alerte : plus de X s sans décision.</summary>
        public float maxGap = 5f;
        /// <summary>Alerte : deux décisions séparées de moins de X s.</summary>
        public float minGap = 0.8f;
        /// <summary>Alerte : plus de X décisions dans une tranche de 10 s (surcharge).</summary>
        public int overload = 6;
        /// <summary>Alerte : moins de X s entre le moment où l'élément devient visible et le contact.</summary>
        public float minReaction = 1.2f;
        /// <summary>Distance (m) à laquelle un élément est considéré comme visible et lisible devant la bande.</summary>
        public float seenDistance = 40f;
        /// <summary>Pertes de membres de la bande en moins de 1 s à partir desquelles c'est un « moment fort ».</summary>
        public int bigLoss = 5;

        static RhythmConfig cached;
        public static RhythmConfig I
        {
            get
            {
                if (cached != null) return cached;
                cached = new RhythmConfig();
                TextAsset ta = Resources.Load<TextAsset>("SamediRouge/rhythm");
                if (ta != null) { try { JsonUtility.FromJsonOverwrite(ta.text, cached); } catch (Exception e) { Debug.LogWarning("rhythm.json illisible : " + e.Message); } }
                return cached;
            }
        }
        public static void Reload() { cached = null; }
    }

    /// <summary>
    /// Enregistreur de rythme et d'événements du mode arcade. Les niveaux arcade sont générés pendant la course :
    /// on mesure donc chaque vraie partie (pas une lecture statique de fichiers de niveau).
    ///
    /// - Décision : un choix du joueur (rangée de portes, groupe d'ennemis à affronter ou éviter, panneau d'arme, bonus).
    /// - Passif : ce qui arrive sans choix (pièces sur la trajectoire).
    /// - Récompense : gain visible (bonne porte, arme, bonus). Moment fort : fusion d'arme, boss, grosse perte, ×N.
    ///
    /// Sorties (éditeur : dossier Logs du projet ; téléphone : Application.persistentDataPath) :
    /// rhythm-events.jsonl (un événement par ligne, au format du futur contrat analytics), rhythm-runs.csv (une ligne par
    /// course), rhythm-report.md (rapport lisible de chaque course, le plus récent en haut).
    /// </summary>
    public static class Rhythm
    {
        /// <summary>Version du générateur de niveaux arcade : à incrémenter quand on change la génération (comparaisons).</summary>
        public const string LevelVersion = "arcade-2026.11.06";
        public static readonly string Session = Guid.NewGuid().ToString("N").Substring(0, 12);   // pseudonyme, change à chaque lancement

        public enum Kind { Decision, Passive, Reward, Spectacle, Loss }

        class Mark { public float z, seenT = -1f; public string what; public Kind kind; }
        public struct Ev { public float t; public string name, what, extra; public Kind kind; public float window; }

        static readonly List<Mark> marks = new List<Mark>();
        static readonly List<Ev> evs = new List<Ev>();
        static bool on;
        static float t, bossT;
        static int level, run, crewStart, lossBurst, losses, gatesGood, gatesBad;
        static float lossT = -9f;
        static string look;
        static float diff;

        public static bool Active => on;
        public static float T => t;

        public static void Begin(int lvl, int crew, float difficulty, string lookName)
        {
            if (on) Write("level_restart", "", "\"t\":" + F(t));   // course précédente interrompue
            marks.Clear();
            evs.Clear();
            on = true;
            t = 0f;
            bossT = -1f;
            level = lvl;
            crewStart = crew;
            diff = difficulty;
            look = lookName;
            losses = lossBurst = gatesGood = gatesBad = 0;
            lossT = -9f;
            run = PlayerPrefs.GetInt("sr_rhythm_run", 0) + 1;
            PlayerPrefs.SetInt("sr_rhythm_run", run);
            Write("level_start", "", "\"crew\":" + crew + ",\"difficulty\":" + F(difficulty) + ",\"look\":\"" + lookName + "\"");
        }

        /// <summary>Un élément vient d'être placé sur la piste à la profondeur z.</summary>
        public static void Place(float z, string what, Kind kind)
        {
            if (!on) return;
            marks.Add(new Mark { z = z, what = what, kind = kind });
        }

        /// <summary>À chaque image de jeu : avance le temps, note quand un élément devient visible et quand la bande l'atteint.</summary>
        public static void Tick(float dt, float pz)
        {
            if (!on) return;
            t += dt;
            RhythmConfig c = RhythmConfig.I;
            for (int i = marks.Count - 1; i >= 0; i--)
            {
                Mark m = marks[i];
                if (m.seenT < 0f && m.z - pz <= c.seenDistance) m.seenT = t;
                if (pz < m.z) continue;
                Add(m.kind, m.what, "", m.seenT >= 0f ? t - m.seenT : 0f);
                marks.RemoveAt(i);
            }
            if (lossBurst > 0 && t - lossT > 1f) FlushLoss();
        }

        public static void Event(Kind kind, string what, string extra = "")
        {
            if (!on) return;
            Add(kind, what, extra, -1f);
        }

        /// <summary>Choix d'une porte : options, couloir pris, bande avant et après.</summary>
        public static void Gate(string[] options, int lane, int before, int after, bool good)
        {
            if (!on) return;
            if (good) gatesGood++; else gatesBad++;
            string opts = "[\"" + string.Join("\",\"", options) + "\"]";
            Write("gate_choice", options[lane], "\"options\":" + opts + ",\"lane\":" + lane + ",\"crew_before\":" + before + ",\"crew_after\":" + after);
            if (good) Add(Kind.Reward, "gate " + options[lane], "", -1f, false);
            if (after - before >= 10) Add(Kind.Spectacle, "big gate " + options[lane], "", -1f, false);
        }

        /// <summary>Un membre de la bande est perdu (groupé par rafales d'une seconde).</summary>
        public static void Loss(string cause)
        {
            if (!on) return;
            losses++;
            if (lossBurst == 0) lossT = t;
            lossBurst++;
            if (cause != lastCause) { lastCause = cause; }
        }
        static string lastCause = "";

        static void FlushLoss()
        {
            int n = lossBurst;
            lossBurst = 0;
            Write("obstacle_hit", lastCause, "\"crew_lost\":" + n);
            Add(n >= RhythmConfig.I.bigLoss ? Kind.Spectacle : Kind.Loss, "lost " + n + " (" + lastCause + ")", "", -1f, false);
        }

        public static void Boss() { if (on) { bossT = t; Add(Kind.Spectacle, "boss", "", -1f); } }

        static int hpEnd;
        public static void End(bool won, int crew, int weaponTier, int hp = -1)
        {
            if (!on) return;
            if (lossBurst > 0) FlushLoss();
            on = false;
            hpEnd = hp;
            Write(won ? "level_complete" : "level_fail", "", "\"duration\":" + F(t) + ",\"crew\":" + crew + ",\"weapon\":" + weaponTier + ",\"hp\":" + hp);
            try { Report(won, crew, weaponTier); } catch (Exception e) { Debug.LogWarning("Rapport de rythme : " + e.Message); }
        }

        /// <summary>Le joueur relance ou quitte la course en cours.</summary>
        public static void Abort(string why)
        {
            if (!on) return;
            on = false;
            Write(why, "", "\"t\":" + F(t));
        }

        // ------------------------------------------------------------------ écriture

        static void Add(Kind kind, string what, string extra, float window, bool log = true)
        {
            evs.Add(new Ev { t = t, kind = kind, what = what, extra = extra, window = window });
            if (log && kind == Kind.Decision) Write("decision", what, "\"reaction_window\":" + F(window));
            else if (log && kind != Kind.Passive) Write(kind.ToString().ToLowerInvariant(), what, "");
        }

        /// <summary>Dans l'éditeur : dossier Logs du projet (à côté de balance, facile à ouvrir). Sur téléphone : données de l'appli.</summary>
        public static string Dir
        {
            get
            {
#if UNITY_EDITOR
                return Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs"));
#else
                return Application.persistentDataPath;
#endif
            }
        }

        static void Write(string ev, string what, string extra)
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append("{\"event\":\"").Append(ev).Append("\",\"level\":").Append(level)
                  .Append(",\"level_version\":\"").Append(LevelVersion).Append("\",\"session\":\"").Append(Session)
                  .Append("\",\"run\":").Append(run).Append(",\"t\":").Append(F(t))
                  .Append(",\"ts\":\"").Append(DateTime.UtcNow.ToString("o")).Append('"');
                if (!string.IsNullOrEmpty(what)) sb.Append(",\"what\":\"").Append(what.Replace("\"", "'")).Append('"');
                if (!string.IsNullOrEmpty(extra)) sb.Append(',').Append(extra);
                sb.Append('}');
                File.AppendAllText(Path.Combine(Dir, "rhythm-events.jsonl"), sb + "\n");
            }
            catch { /* jamais bloquant */ }
        }

        static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        static string S(float v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        // ------------------------------------------------------------------ rapport

        static void Report(bool won, int crew, int weaponTier)
        {
            RhythmConfig c = RhythmConfig.I;
            float runEnd = bossT > 0f ? bossT : t;   // le rythme se mesure sur le parcours, avant le boss
            var dec = evs.FindAll(e => e.kind == Kind.Decision && e.t <= runEnd);
            var rew = evs.FindAll(e => e.kind == Kind.Reward);
            var spe = evs.FindAll(e => e.kind == Kind.Spectacle);
            var gaps = Gaps(dec, runEnd);
            float mean = gaps.Count > 0 ? Avg(gaps) : 0f, med = Median(gaps), max = gaps.Count > 0 ? Max(gaps) : runEnd;
            var rg = Gaps(rew, t); var sg = Gaps(spe, t);
            var windows = new List<float>();
            foreach (Ev e in dec) if (e.window > 0f) windows.Add(e.window);

            // décisions par tranche de 10 s
            int slices = Mathf.Max(1, Mathf.CeilToInt(runEnd / 10f));
            int[] per10 = new int[slices];
            foreach (Ev e in dec) per10[Mathf.Clamp(Mathf.FloorToInt(e.t / 10f), 0, slices - 1)]++;

            // alertes
            var flags = new List<string>();
            float prev = 0f;
            foreach (Ev e in dec)
            {
                if (e.t - prev > c.maxGap) flags.Add("Temps mort : " + S(e.t - prev) + " s sans décision (de " + S(prev) + " à " + S(e.t) + " s)");
                else if (prev > 0f && e.t - prev < c.minGap) flags.Add("Décisions trop rapprochées : " + S(e.t - prev) + " s à " + S(e.t) + " s (" + e.what + ")");
                if (e.window > 0f && e.window < c.minReaction) flags.Add("Réaction courte : " + S(e.window) + " s pour « " + e.what + " » à " + S(e.t) + " s");
                prev = e.t;
            }
            if (runEnd - prev > c.maxGap) flags.Add("Temps mort final : " + S(runEnd - prev) + " s sans décision avant le boss");
            for (int i = 0; i < slices; i++) if (per10[i] > c.overload) flags.Add("Surcharge : " + per10[i] + " décisions entre " + i * 10 + " et " + (i + 1) * 10 + " s");
            if (rg.Count > 0 && Max(rg) > c.rewardMax * 2f) flags.Add("Récompenses : jusqu'à " + S(Max(rg)) + " s sans gain visible (cible " + S(c.rewardMin) + "–" + S(c.rewardMax) + " s)");
            if (sg.Count > 0 && Max(sg) > c.spectacleMax * 1.5f) flags.Add("Moments forts : jusqu'à " + S(Max(sg)) + " s sans moment fort (cible " + S(c.spectacleMin) + "–" + S(c.spectacleMax) + " s)");
            if (mean > c.decisionMax) flags.Add("Rythme lent : une décision toutes les " + S(mean) + " s en moyenne (cible " + S(c.decisionMin) + "–" + S(c.decisionMax) + " s)");
            else if (mean > 0f && mean < c.decisionMin * 0.6f) flags.Add("Rythme très dense : une décision toutes les " + S(mean) + " s en moyenne");

            // CSV (une ligne par course)
            string csv = Path.Combine(Dir, "rhythm-runs.csv");
            if (!File.Exists(csv)) File.WriteAllText(csv, "session,run,level,level_version,look,difficulty,won,duration,track_duration,decisions,mean_gap,median_gap,max_gap,min_reaction,median_reaction,rewards,mean_reward_gap,spectacles,mean_spectacle_gap,crew_start,crew_end,crew_lost,gates_good,gates_bad,weapon,flags\n");
            File.AppendAllText(csv, string.Join(",", new[]
            {
                Session, run.ToString(), level.ToString(), LevelVersion, look, F(diff), won ? "1" : "0", F(t), F(runEnd), dec.Count.ToString(),
                F(mean), F(med), F(max), windows.Count > 0 ? F(Min(windows)) : "", windows.Count > 0 ? F(Median(windows)) : "",
                rew.Count.ToString(), rg.Count > 0 ? F(Avg(rg)) : "", spe.Count.ToString(), sg.Count > 0 ? F(Avg(sg)) : "",
                crewStart.ToString(), crew.ToString(), losses.ToString(), gatesGood.ToString(), gatesBad.ToString(), weaponTier.ToString(), flags.Count.ToString()
            }) + "\n");

            // rapport lisible
            var sb = new StringBuilder();
            sb.AppendLine("## Niveau " + level + " — course " + run + " (" + (won ? "gagnée" : "perdue") + ", look " + look + ", " + DateTime.Now.ToString("dd/MM HH:mm") + ")");
            sb.AppendLine();
            sb.AppendLine("- Durée : " + S(t) + " s (parcours " + S(runEnd) + " s" + (bossT > 0f ? ", boss et finale " + S(t - bossT) + " s" : "") + ")");
            sb.AppendLine("- Décisions : " + dec.Count + " · intervalle moyen " + S(mean) + " s, médian " + S(med) + " s, maximal " + S(max) + " s (cible " + S(c.decisionMin) + "–" + S(c.decisionMax) + " s)");
            sb.AppendLine("- Décisions par tranche de 10 s : " + string.Join(" · ", Array.ConvertAll(per10, x => x.ToString())));
            if (windows.Count > 0) sb.AppendLine("- Temps de réaction disponible : min " + S(Min(windows)) + " s, médian " + S(Median(windows)) + " s");
            sb.AppendLine("- Récompenses : " + rew.Count + (rg.Count > 0 ? " · une toutes les " + S(Avg(rg)) + " s (max " + S(Max(rg)) + " s)" : "") + " (cible " + S(c.rewardMin) + "–" + S(c.rewardMax) + " s)");
            sb.AppendLine("- Moments forts : " + spe.Count + (sg.Count > 0 ? " · un toutes les " + S(Avg(sg)) + " s (max " + S(Max(sg)) + " s)" : "") + " (cible " + S(c.spectacleMin) + "–" + S(c.spectacleMax) + " s)");
            sb.AppendLine("- Bande : " + crewStart + " → " + crew + " · " + losses + " perdus · portes bonnes " + gatesGood + " / mauvaises " + gatesBad + " · arme niveau " + weaponTier + (hpEnd >= 0 ? " · vie restante " + hpEnd : ""));
            sb.AppendLine();
            sb.AppendLine(flags.Count == 0 ? "Aucune alerte." : "**Alertes**");
            foreach (string f in flags) sb.AppendLine("- " + f);
            sb.AppendLine();
            sb.AppendLine("<details><summary>Chronologie</summary>");
            sb.AppendLine();
            foreach (Ev e in evs) sb.AppendLine("- " + S(e.t) + " s · " + Label(e.kind) + " · " + e.what + (e.window > 0f ? " (réaction " + S(e.window) + " s)" : ""));
            sb.AppendLine();
            sb.AppendLine("</details>");
            sb.AppendLine();
            string md = Path.Combine(Dir, "rhythm-report.md");
            string old = File.Exists(md) ? File.ReadAllText(md) : "";
            const string head = "# Rythme des courses (mode arcade)\n\nUne entrée par course, la plus récente en haut. Seuils : Resources/SamediRouge/rhythm.json.\n\n";
            if (old.StartsWith(head)) old = old.Substring(head.Length);
            File.WriteAllText(md, head + sb + old);
            Debug.Log("[Rythme] niveau " + level + " : " + dec.Count + " décisions, une toutes les " + S(mean) + " s (max " + S(max) + " s), " + flags.Count + " alerte(s). Rapport : " + md);
        }

        static string Label(Kind k) => k == Kind.Decision ? "DÉCISION" : k == Kind.Reward ? "récompense" : k == Kind.Spectacle ? "MOMENT FORT" : k == Kind.Loss ? "perte" : "passif";

        static List<float> Gaps(List<Ev> l, float end)
        {
            var g = new List<float>();
            float p = 0f;
            foreach (Ev e in l) { g.Add(e.t - p); p = e.t; }
            if (l.Count > 0 && end - p > 0.5f) g.Add(end - p);
            return g;
        }
        static float Avg(List<float> l) { float s = 0f; foreach (float x in l) s += x; return l.Count > 0 ? s / l.Count : 0f; }
        static float Max(List<float> l) { float m = float.MinValue; foreach (float x in l) m = Mathf.Max(m, x); return l.Count > 0 ? m : 0f; }
        static float Min(List<float> l) { float m = float.MaxValue; foreach (float x in l) m = Mathf.Min(m, x); return l.Count > 0 ? m : 0f; }
        static float Median(List<float> l)
        {
            if (l.Count == 0) return 0f;
            var s = new List<float>(l);
            s.Sort();
            return s.Count % 2 == 1 ? s[s.Count / 2] : 0.5f * (s[s.Count / 2 - 1] + s[s.Count / 2]);
        }
    }
}
