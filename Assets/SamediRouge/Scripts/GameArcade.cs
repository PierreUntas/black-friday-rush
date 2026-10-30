using System.Collections.Generic;
using UnityEngine;

namespace SamediRouge
{
    /// <summary>
    /// Mode arcade (version proposée à YSO Corp) : portes « +N / ×N / −N » qui font grossir la bande,
    /// bande qui tire toute seule, caisses d'armes qui fusionnent pour monter de niveau pendant la course.
    /// </summary>
    public partial class Game
    {
        /// <summary>Une rangée de trois portes (une par couloir) : la porte du couloir où l'on passe s'applique.</summary>
        class Gate
        {
            public float z;
            public readonly int[] val = new int[3];
            public readonly bool[] mul = new bool[3];
            public bool used;
            public GameObject go;
            public readonly Transform[] panel = new Transform[3];
        }

        readonly List<Gate> gates = new List<Gate>();
        float nextGateZ, nextCrateZ, mateVolleyT;
        /// <summary>Niveau d'arme de la course : 0 mains nues, 1 pistolet, 2 mitraillette, 3 mitraillette renforcée.</summary>
        int runTier;
        /// <summary>Caisses d'armes ramassées vers la prochaine fusion.</summary>
        int crates;
        const int ArcadeMaxSquad = 24;
        const int CratesPerMerge = 2;

        static readonly Color GateGood = new Color(0.2f, 0.62f, 1f, 0.5f), GateBad = new Color(1f, 0.18f, 0.15f, 0.5f);

        /// <summary>Place d'un membre de la bande derrière le héros (au-delà des 10 places fixes : rangées de 5).</summary>
        static Vector2 SlotAt(int i)
        {
            if (i < Slots.Length) return Slots[i];
            int k = i - Slots.Length;
            return new Vector2(((k % 5) - 2) * 0.85f, -3.3f - (k / 5) * 0.65f);
        }

        void ArcadeStart()
        {
            foreach (Gate g in gates) if (g.go != null) Destroy(g.go);
            gates.Clear();
            nextGateZ = pz + 48f;
            nextCrateZ = pz + 80f;
            crates = 0;
            mateVolleyT = 0.5f;
            runTier = gear[GSmg] ? 2 : gear[GPistol] ? 1 : 0;
        }

        /// <summary>Génération : une rangée de portes ou une caisse d'armes quand c'est leur tour. Renvoie l'espace pris (0 : rien de spécial).</summary>
        float ArcadeSpawn(float z)
        {
            if (!campaign || tutorialMode || demo) return 0f;
            if (z - stageStartZ > StageLen - 25f) return 0f;   // pas de portes dans l'arène du boss
            if (z >= nextGateZ)
            {
                SpawnGate(z + 4f);
                nextGateZ = z + Random.Range(55f, 72f);
                return 16f;
            }
            if (z >= nextCrateZ)
            {
                // caisse d'armes dans un couloir au hasard, puis la suite du parcours normalement
                Spawn(ET.Pistolet, Random.Range(0, 3), z + 3f);
                nextCrateZ = z + Random.Range(65f, 90f);
            }
            return 0f;
        }

        void SpawnGate(float z)
        {
            var g = new Gate { z = z };
            int n = squad.Count + 1;
            // trois choix : toujours au moins un bon et un mauvais
            var opts = new List<(int v, bool m)>
            {
                (Random.Range(2, 5) + n / 4, false),
                (Random.Range(4, 8) + n / 3, false),
                (n >= 3 ? (Random.value < 0.25f ? 3 : 2) : Random.Range(5, 9), n >= 3),
                (-(Random.Range(2, 4) + n / 4), false),
            };
            int bad = 3;
            int[] pick = { Random.Range(0, 3), bad, -1 };
            do pick[2] = Random.Range(0, 4); while (pick[2] == pick[0] || (pick[2] == bad && Random.value < 0.6f));
            if (pick[2] == bad) pick[2] = (pick[0] + 1) % 3;
            for (int i = 2; i > 0; i--) { int j = Random.Range(0, i + 1); int t = pick[i]; pick[i] = pick[j]; pick[j] = t; }
            for (int l = 0; l < 3; l++) { g.val[l] = opts[pick[l]].v; g.mul[l] = opts[pick[l]].m; }

            g.go = new GameObject("gate");
            g.go.transform.SetParent(world, false);
            g.go.transform.localPosition = new Vector3(0f, 0f, z);
            Material post = Art.Mat(new Color(0.92f, 0.92f, 0.95f), 0.6f, 0.3f);
            for (int l = 0; l < 3; l++)
            {
                float x = (l - 1) * Lane;
                bool good = g.mul[l] || g.val[l] > 0;
                Art.Part(g.go.transform, Art.Cylinder, post, new Vector3(x - 1.12f, 1.35f, 0f), new Vector3(0.14f, 1.35f, 0.14f), default, false);
                Art.Part(g.go.transform, Art.Cylinder, post, new Vector3(x + 1.12f, 1.35f, 0f), new Vector3(0.14f, 1.35f, 0.14f), default, false);
                Art.Part(g.go.transform, Art.Cube, post, new Vector3(x, 2.72f, 0f), new Vector3(2.4f, 0.14f, 0.16f), default, false);
                MeshRenderer pr = Art.Part(g.go.transform, Art.Cube, Art.Particle(null, good ? GateGood : GateBad), new Vector3(x, 1.35f, 0f), new Vector3(2.1f, 2.6f, 0.04f), default, false);
                g.panel[l] = pr.transform;
            }
            gates.Add(g);
        }

        static string GateText(Gate g, int l) => g.mul[l] ? "×" + g.val[l] : (g.val[l] > 0 ? "+" + g.val[l] : g.val[l].ToString());

        /// <summary>Portes franchies, tirs de la bande, nettoyage.</summary>
        void ArcadeTick(float dt)
        {
            for (int i = gates.Count - 1; i >= 0; i--)
            {
                Gate g = gates[i];
                if (!g.used && pz >= g.z)
                {
                    g.used = true;
                    int l = Mathf.Clamp(Mathf.RoundToInt(px / Lane) + 1, 0, 2);
                    ApplyGate(g, l);
                }
                if (g.z < pz - 12f) { if (g.go != null) Destroy(g.go); gates.RemoveAt(i); }
            }

            // la bande tire toute seule : quelques membres à la fois, sur les adversaires les plus proches
            mateVolleyT -= dt;
            if (mateVolleyT <= 0f && squad.Count > 0)
            {
                mateVolleyT = Mathf.Lerp(0.5f, 0.22f, squad.Count / (float)ArcadeMaxSquad);
                int shots = Mathf.Min(3, squad.Count);
                for (int s = 0; s < shots; s++)
                {
                    Mate m = squad[Random.Range(0, squad.Count)];
                    Ent best = null;
                    float bd = 1e9f;
                    foreach (Ent e in ents)
                    {
                        if (e.hit || e.dead) continue;
                        if (e.type != ET.Casseur && e.type != ET.Loup) continue;
                        float d = e.z - m.z;
                        if (d < 1f || d > 26f) continue;
                        if (Mathf.Abs(e.x - m.x) > 2.6f && e.type != ET.Loup) continue;
                        if (d < bd) { bd = d; best = e; }
                    }
                    if (best != null) Shoot(best, true, new Vector3(m.x, 1.2f, m.z + 0.4f));
                }
            }
        }

        void ApplyGate(Gate g, int l)
        {
            int before = squad.Count;
            if (g.mul[l]) AddMates((squad.Count + 1) * (g.val[l] - 1));
            else if (g.val[l] > 0) AddMates(g.val[l]);
            else for (int k = 0; k < -g.val[l] && squad.Count > 0; k++) LoseMate(squad.Count - 1, new Vector3(Random.Range(-3f, 3f), 4f, 2f));
            bool good = g.mul[l] || g.val[l] > 0;
            if (g.panel[l] != null) g.panel[l].gameObject.SetActive(false);
            Vector3 p = new Vector3((l - 1) * Lane, 1.4f, g.z);
            hud.FloatKey(p + Vector3.up, GateText(g, l), good ? Hud.Yellow : Hud.Bad, true);
            if (good)
            {
                Fx.Confetti(p);
                Snd.Play(Snd.I.pickup, 0.8f, 0f);
                P.Flash(Hud.StarGold, 0.15f);
            }
            else
            {
                Snd.Play(Snd.I.hurt, 0.6f);
                shake = Mathf.Max(shake, 0.25f);
            }
            if (squad.Count >= ArcadeMaxSquad && g.mul[l] && before < ArcadeMaxSquad) score += 200f;
        }

        /// <summary>Une caisse d'armes : deux caisses fusionnent en une arme du niveau au-dessus.</summary>
        void WeaponCrate(Vector3 p)
        {
            if (runTier >= 3) { score += 100f; hud.FloatKey(p + Vector3.up, "+100", Hud.Yellow, true); return; }
            crates++;
            if (crates < CratesPerMerge)
            {
                hud.FloatKey(p + Vector3.up, crates + "/" + CratesPerMerge, Hud.Yellow, true);
                return;
            }
            crates = 0;
            runTier++;
            gear[GPistol] = runTier >= 1;
            gear[GSmg] = runTier >= 2;
            ApplyGear();
            hud.Toast(L("MERGE! LV ", "FUSION ! NIV ") + (runTier + 1));
            Fx.Confetti(new Vector3(px, 1.6f, pz + 1f));
            P.Flash(Hud.StarGold, 0.3f);
            Snd.Play(Snd.I.pickup, 1f, 0f);
        }

        /// <summary>Étiquettes posées dans la scène : valeurs des portes et taille de la bande au-dessus du héros.</summary>
        void ArcadeTags()
        {
            foreach (Gate g in gates)
            {
                if (g.used) continue;
                float d = g.z - pz;
                if (d < 1f || d > 70f) continue;
                for (int l = 0; l < 3; l++)
                    hud.Tag(new Vector3((l - 1) * Lane, 1.5f, g.z), GateText(g, l), Color.white, 2.2f);
            }
            hud.Tag(new Vector3(px, 3.1f, pz), (squad.Count + 1).ToString(), Hud.Yellow, 1.3f);
        }
    }
}
