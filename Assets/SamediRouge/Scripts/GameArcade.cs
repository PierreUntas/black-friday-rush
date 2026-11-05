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
            public readonly int[] hits = new int[3];
            public readonly float[] punch = new float[3];
        }

        /// <summary>Panneau de fusion (à la Hell Escape) : on le remplit en tirant dessus, l'arme suivante sort quand la jauge est pleine.</summary>
        class Craft
        {
            public GameObject go;
            public int lane, hits, need;
            public float z, punch;
            public bool done, armor;
            public Transform bar, panel;
        }

        readonly List<Craft> crafts = new List<Craft>();

        /// <summary>Finale : cartons de butin numérotés devant le MEGA STORE, à casser en tirant (comme les caisses de Hell Escape).</summary>
        class LootBox
        {
            public Transform tr;
            public int lane, hp, max;
            public float z, punch;
            public bool broken, dodged;
            public int id;     // place dans le champ (rangée × 3 + couloir), pour s'en souvenir d'une course à l'autre
            public int pend;   // dégâts déjà en vol vers ce carton (évite que toute la bande vise le même)
        }

        /// <summary>Champ de cartons de la finale en cours : la bande se resserre derrière le héros pour passer par la même brèche.</summary>
        bool InCrateField => finaleZ >= 0f && pz < lootEndZ + 3f && loot.Count > 0;

        readonly List<LootBox> loot = new List<LootBox>();
        float lootEndZ = -1f;

        readonly List<Gate> gates = new List<Gate>();
        float nextGateZ, nextCrateZ, mateVolleyT;
        /// <summary>Niveau d'arme de la course : 0 mains nues, 1 pistolet, 2 mitraillette, 3 mitraillette renforcée.</summary>
        int runTier;
        /// <summary>Niveaux d'atelier gagnés au-delà de la mitraillette : chaque niveau accélère les tirs de toute la bande.</summary>
        int craftBoost;
        /// <summary>Niveau d'arme affiché (comme la carte « EQUIPPED » de Hell Escape) : batte 1, pistolet 2, mitraillette 3, puis +1 par atelier.</summary>
        int RunWeaponLv => runTier + 1 + craftBoost;
        /// <summary>Cadence : chaque niveau d'atelier au-delà de la mitraillette fait tirer 15 % plus vite.</summary>
        float RateK => 1f / (1f + 0.15f * craftBoost);

        /// <summary>Bonus à ramasser (comme le « God's Help » de Hell Escape) : 0 attaque +20 %, 1 défense +20 %.</summary>
        class Boon
        {
            public GameObject go;
            public int lane, kind;
            public float z;
            public bool done;
        }

        readonly List<Boon> boons = new List<Boon>();
        int dmgBoon, defBoon, lastBoonKind = -1;
        /// <summary>Armure de la course (atelier d'armure) : 0 rien, 1 sweat, 2 blouson, 3 gilet volé, puis +1 par atelier.</summary>
        int armorLv;
        int craftCount;
        /// <summary>Chance qu'un voleur touché reste debout (bonus de défense +20 % chacun, armure +15 % par niveau).</summary>
        float DefChance { get { float d = 0.2f * defBoon + 0.15f * armorLv; return d / (1f + d); } }
        bool Defended() => Tuning.Arcade && Random.value < DefChance;
        /// <summary>Cartons de la finale déjà cassés à une tentative précédente (gardés d'une course à l'autre, comme Hell Escape).</summary>
        string LootKey => "sr_loot_" + streetIdx;
        float nextBoonZ;
        /// <summary>Multiplicateur de dégâts des bonus ramassés.</summary>
        float DmgK => 1f + 0.2f * dmgBoon;

        /// <summary>Dégâts d'un tir avec le bonus de dégâts (partie fractionnaire tirée au sort : +20 % en moyenne).</summary>
        int BoostDmg(int d)
        {
            if (!Tuning.Arcade || dmgBoon == 0) return d;
            float f = d * DmgK;
            int i = Mathf.FloorToInt(f);
            return i + (Random.value < f - i ? 1 : 0);
        }
        static bool devCenter;   // dev (éditeur) : caisses dans le couloir du milieu, pour tester la fusion sans diriger
        /// <summary>Caisses d'armes ramassées vers la prochaine fusion.</summary>
        int crates;
        const int ArcadeMaxSquad = 99;   // une vraie foule : 99 voleurs + le héros = 100 (retour : 18 c'était trop peu)
        /// <summary>Arcade : vitesse de course réduite, taille des personnages de ta bande (alignée sur les adversaires).</summary>
        const float ArcadeSpeed = 0.38f, CrewScale = 0.65f, MateScale = 0.48f, EnemyScale = 1f, FreeMax = 3.3f;
        float freeX;

        /// <summary>
        /// Difficulté arcade du niveau en cours (0 = très facile). Courbe en dents de scie par cycles de 5 niveaux
        /// (facile, moyen, moyen+, dur, respiration) sur une base qui monte doucement ; les 3 premiers niveaux restent
        /// presque imperdables. Rattrapage : chaque échec récent sur ce niveau le rend un peu plus facile.
        /// </summary>
        float arcD;
        static readonly float[] Saw = { 0f, 0.15f, 0.3f, 0.5f, -0.1f };

        float ArcadeDifficulty()
        {
            int lv = streetIdx;
            float d = lv == 0 ? 0f : 0.2f * lv + Saw[lv % 5];   // niveau 1 : découverte ; ensuite ça monte vite, en dents de scie
            int fails = Progress.Conquered(thief, lv) ? 0 : Mathf.Max(0, Progress.Tries(thief, lv) - 1);
            d -= 0.25f * Mathf.Min(fails, 2);
            return Mathf.Clamp(d, 0f, 3f);
        }
        bool FreeMove => Tuning.Arcade && campaign && !demo && !tutorialMode;
        /// <summary>Arcade : la rue est plus longue (environ une minute de course avant le boss).</summary>
        const float ArcadeLengthMult = 2.4f;
        float finaleZ = -1f;
        GameObject store;
        const int CratesPerMerge = 2;

        static readonly Color GateGood = new Color(0.2f, 0.62f, 1f, 0.5f), GateBad = new Color(1f, 0.18f, 0.15f, 0.5f);

        /// <summary>Place d'un membre de la bande derrière le héros (au-delà des 10 places fixes : rangées de 5).</summary>
        static Vector2[] blob;
        static Vector2 SlotAt(int i)
        {
            if (!Tuning.Arcade)
            {
                if (i < Slots.Length) return Slots[i];
                int k = i - Slots.Length;
                return new Vector2(((k % 5) - 2) * 0.85f, -3.3f - (k / 5) * 0.65f);
            }
            // arcade : une foule compacte en tournesol autour et juste derrière le héros (lisible, ne cache pas l'écran)
            if (blob == null)
            {
                // foule compacte et régulière (maillage en quinconce) : les places les plus proches du héros d'abord,
                // la foule s'élargit jusqu'aux bords de la piste puis s'allonge vers l'arrière (jusqu'à 100 voleurs)
                var l = new List<Vector2>();
                const float sx = 0.46f, sy = 0.4f;
                for (int iy = -40; iy <= 4; iy++)
                    for (int ix = -7; ix <= 7; ix++)
                    {
                        float x = ix * sx + ((iy & 1) != 0 ? sx * 0.5f : 0f), y = iy * sy - 0.3f;
                        if (Mathf.Abs(x) > 2.95f) continue;
                        if (Mathf.Abs(x) < 0.45f && y > -0.5f) continue;   // pas sur le héros ni juste devant lui
                        if (y > 0.9f) continue;
                        l.Add(new Vector2(x, y));
                    }
                l.Sort((p1, p2) => (p1.x * p1.x * 0.55f + (p1.y + 0.5f) * (p1.y + 0.5f)).CompareTo(p2.x * p2.x * 0.55f + (p2.y + 0.5f) * (p2.y + 0.5f)));
                if (l.Count > ArcadeMaxSquad + 4) l.RemoveRange(ArcadeMaxSquad + 4, l.Count - ArcadeMaxSquad - 4);
                blob = l.ToArray();
            }
            return blob[Mathf.Min(i, blob.Length - 1)];
        }

        void ArcadeStart()
        {
            blocked = false;
            for (int i = 0; i < kindSeen.Length; i++) kindSeen[i] = false;
            tagCrowd = -1;
            if (store != null) Destroy(store);
            finaleZ = -1f;
            foreach (Gate g in gates) if (g.go != null) Destroy(g.go);
            gates.Clear();
            nextGateZ = pz + 48f;
            nextCrateZ = pz + 80f;
            crates = 0;
            craftBoost = 0;
            dmgBoon = defBoon = 0;
            armorLv = 0;
            craftCount = 0;
            lastBoonKind = -1;
            nextBoonZ = pz + 125f;
            foreach (Boon bo in boons) if (bo.go != null) Destroy(bo.go);
            boons.Clear();
            hud.HideWeaponCard();
            hud.SetBoons(0, 0);
            ClearMergeIcons();
            foreach (Craft c in crafts) if (c.go != null) Destroy(c.go);
            crafts.Clear();
            foreach (LootBox b in loot) if (b.tr != null) Destroy(b.tr.gameObject);
            loot.Clear();
            lootEndZ = -1f;
            mateVolleyT = 0.5f;
            runTier = gear[GSmg] ? 2 : gear[GPistol] ? 1 : 0;
            freeX = 0f;
            arcD = campaign && !demo ? ArcadeDifficulty() : 0f;
            Perf.Note("niveau " + (streetIdx + 1) + " : difficulté " + arcD.ToString("0.00"));
            if (campaign && !demo && StartCrew > 0) AddMates(StartCrew);   // amélioration « Bande » de l'accueil
            ArmCrew();
            if (campaign && !demo && streetIdx == 0) hud.Toast(L("← SWIPE →", "← GLISSE →"));
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
            if (z >= nextBoonZ)
            {
                SpawnBoon(z + 5f);
                nextBoonZ = z + Random.Range(95f, 125f);
                return 10f;
            }
            if (z >= nextCrateZ)
            {
                // panneau de fusion dans un couloir au hasard : il faut s'y placer et tirer dessus pour le remplir
                // l'atelier revient toute la course (plus de plafond à la mitraillette : ensuite il augmente la cadence)
                SpawnCraft(z + 6f, devCenter ? 1 : Random.Range(0, 3), craftCount++ % 2 == 1);   // une fois l'arme, une fois l'armure
                nextCrateZ = z + Random.Range(80f, 105f);
                return 14f;
            }
            return 0f;
        }

        /// <summary>
        /// Arcade : les adversaires s'endurcissent avec le niveau et au fil de la course. Niveau 1 : un coup suffit,
        /// quelques costauds à 2 en fin de parcours ; ensuite casques puis gilets, qu'il faut une meilleure arme
        /// (ou une plus grosse bande) pour abattre avant qu'ils n'arrivent sur toi.
        /// </summary>
#if UNITY_EDITOR
        /// <summary>Outil de dev (vidéo, équilibrage) : pilote auto qui prend la meilleure porte, puis le panneau d'armes.</summary>
        bool devAuto;
        float AutoLaneX()
        {
            int crowd = squad.Count + 1;
            foreach (Gate g in gates)
            {
                if (g.used || g.z < pz + 2f || g.z > pz + 30f) continue;
                int best = 1;
                float bv = -1e9f;
                for (int l = 0; l < 3; l++)
                {
                    float v = g.mul[l] ? crowd * (g.val[l] - 1) : g.val[l];
                    if (v > bv) { bv = v; best = l; }
                }
                return (best - 1) * Lane;
            }
            foreach (Craft c in crafts) if (!c.done && c.z > pz && c.z < pz + 32f) return (c.lane - 1) * Lane;
            foreach (Boon bo in boons) if (!bo.done && bo.z > pz && bo.z < pz + 30f) return (bo.lane - 1) * Lane;
            return 0f;
        }
#endif

        int ArcadeEnemyHp()
        {
            if (arcPackHp > 0f)
            {
                // points de vie du groupe répartis : partie entière + arrondi au hasard, et parfois un costaud
                float h = arcPackHp * (Random.value < 0.2f ? 1.8f : Random.Range(0.8f, 1.15f));
                int hp = Mathf.FloorToInt(h) + (Random.value < h - Mathf.Floor(h) ? 1 : 0);
                return Mathf.Clamp(hp, 1, 45);   // grosse foule : des vigiles plus coriaces
            }
            float prog = Mathf.Clamp01((pz + 35f - stageStartZ) / Mathf.Max(1f, StageLen));
            float tough = arcD * 0.9f + prog * (0.9f + 0.7f * arcD);
            int b = 1 + Mathf.FloorToInt(tough * 0.6f);
            if (Random.value < Mathf.Clamp(0.12f + 0.2f * tough, 0f, 0.55f)) b += 1 + Mathf.FloorToInt(tough * 0.5f);
            return Mathf.Clamp(b, 1, 8);
        }

        float arcPackHp;

        /// <summary>Coups par seconde que la bande peut envoyer (héros + volées des membres).</summary>
        const float MateRate = 0.95f;   // cadence de tir d'un membre de la bande (s)

        float ArcadePower()
        {
            float dmg = runTier >= 3 ? 2f : 1f;
            float hero = dmg / (gear[GSmg] ? 0.11f : gear[GPistol] ? Tuning.AutoPistolRate : Tuning.AutoBareRate);
            float mates = dmg * squad.Count / MateRate;   // chacun tire à son rythme
            return hero + mates;
        }

        /// <summary>
        /// Difficulté arcade : chaque groupe d'ennemis reçoit un total de points de vie calé sur ce que ta bande peut
        /// abattre avant le contact, multiplié par la « pression » du niveau. Pression &lt; 1 : tout tombe avant
        /// d'arriver ; &gt; 1 : une partie du groupe arrive sur la bande et chaque ennemi emporte un membre.
        /// Elle monte au fil du parcours et d'un niveau à l'autre (avec les creux de la courbe en dents de scie).
        /// </summary>
        void SetPackHp(float z, int n, float share)
        {
            float prog = Mathf.Clamp01((z - stageStartZ) / Mathf.Max(1f, StageLen));
            float k = Mathf.Min(2.6f, 0.95f + 0.5f * arcD + 0.7f * prog);   // bande plus petite qu'avant (portes moins généreuses) : début plus doux
            // grosse foule : pression un peu moindre (sinon chaque groupe la décimait : 97 → 34 en une rencontre)
            k *= Mathf.Lerp(1f, 0.8f, Mathf.Clamp01((squad.Count - 20) / 79f));
            float t = 26f / Mathf.Max(6f, speed + 6f * Tuning.EnemyRush);   // temps entre l'entrée à portée et le contact
            float budget = k * t * ArcadePower() * share;
            arcPackHp = Mathf.Max(1f, budget / Mathf.Max(1, n));
            Perf.Note("bande " + (squad.Count + 1) + " · groupe " + n + " ennemis, pression " + k.ToString("0.00") + ", pv " + arcPackHp.ToString("0.0"));
        }

        void SpawnGate(float z)
        {
            var g = new Gate { z = z };
            int n = squad.Count + 1;
            bool x2 = n >= 12 && n < 40 && Random.value < 0.15f;
            // trois choix : toujours au moins un bon et un mauvais
            var opts = new List<(int v, bool m)>
            {
                // peu généreuses : +1..+2 au début, les ×2 n'arrivent qu'avec une bande déjà formée
                // (retour : on finissait toujours à 100) — valeurs de départ plus basses
                (1 + n / 20, false),
                (Random.Range(1, 3) + n / 16, false),
                (x2 ? 2 : Random.Range(2, 4) + n / 16, x2),
                (-Mathf.RoundToInt((Random.Range(2, 5) + n / 3) * (1f + 0.5f * arcD)), false),
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

        class Swoosh { public Transform t; public Material m; public float life; public bool flip; }
        readonly List<Swoosh> swooshes = new List<Swoosh>();
        const float SwooshT = 0.28f;

        /// <summary>Arc jaune d'un coup de batte, face caméra : grandit et s'efface en 0,3 s.</summary>
        void SpawnSwoosh(Vector3 at, bool flip)
        {
            Swoosh w = null;
            foreach (Swoosh x in swooshes) if (x.life <= 0f && x.t != null) { w = x; break; }
            if (w == null)
            {
                if (swooshes.Count >= 10) return;
                Material m = Art.Swoosh();
                MeshRenderer r = Art.Part(world, Art.Quad, m, at, Vector3.one, default, false);
                w = new Swoosh { t = r.transform, m = m };
                swooshes.Add(w);
            }
            w.life = SwooshT;
            w.flip = flip;
            w.t.localPosition = at;
            w.t.gameObject.SetActive(true);
        }

        void SwooshTick(float dt)
        {
            foreach (Swoosh w in swooshes)
            {
                if (w.t == null || w.life <= 0f) continue;
                w.life -= dt;
                if (w.life <= 0f) { w.t.gameObject.SetActive(false); continue; }
                float u = 1f - w.life / SwooshT;
                if (cam != null) w.t.rotation = Quaternion.LookRotation(w.t.position - cam.transform.position) * Quaternion.Euler(0f, 0f, (w.flip ? 1f : -1f) * (40f - 110f * u));
                w.t.localScale = Vector3.one * (1.1f + 1.1f * u);
                w.m.color = new Color(1f, 0.9f, 0.3f, 1f - u * u);
            }
        }

        /// <summary>Portes franchies, tirs de la bande, nettoyage.</summary>
        void ArcadeTick(float dt)
        {
            SwooshTick(dt);
            FinaleTick();
            if (st != St.Play) return;
            MergeTick(dt);
            PropsTick(dt);
            BoonTick();
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

            // la bande tire toute seule : CHAQUE membre tire à son rythme sur l'adversaire le plus proche devant lui (retour YSO)
            // grosse foule : chacun tire moins souvent mais plus fort (même puissance de feu, beaucoup moins de projectiles à l'écran)
            int salvo = Mathf.Max(1, Mathf.CeilToInt(squad.Count / 24f));
            foreach (Mate m in squad)
            {
                m.shotT -= dt;
                if (m.shotT > 0f) continue;
                Ent best = null;
                float bd = 1e9f;
                foreach (Ent e in ents)
                {
                    if (e.hit || e.dead) continue;
                    if (e.type != ET.Casseur && e.type != ET.Loup) continue;
                    float d = e.z - m.z;
                    if (d < 1f || d > (e.type == ET.Loup ? 45f : 26f)) continue;   // le boss se tient loin : toute la bande le vise
                    if (Mathf.Abs(e.x - m.x) > 2.6f && e.type != ET.Loup) continue;
                    if (d < bd) { bd = d; best = e; }
                }
                if (best != null)
                {
                    int nb = bolts.Count;
                    Shoot(best, true, new Vector3(m.x, 1.3f, m.z + 0.4f));
                    if (bolts.Count > nb) bolts[bolts.Count - 1].mult = salvo;
                    m.shotT = MateRate * salvo * RateK * Random.Range(0.85f, 1.15f);
                }
                else if (loot.Count > 0)
                {
                    int ml = Mathf.Clamp(Mathf.RoundToInt(m.x / Lane) + 1, 0, 2);
                    LootBox lb = NearestLoot(ml, m.z, 24f) ?? NearestLoot(-1, m.z, 24f);
                    if (lb != null)
                    {
                        Shoot(null, true, new Vector3(m.x, 1.3f, m.z + 0.4f));
                        bolts[bolts.Count - 1].loot = lb;
                        bolts[bolts.Count - 1].mult = salvo;
                        lb.pend += LootDmg * salvo;
                        m.shotT = MateRate * salvo * RateK * Random.Range(0.85f, 1.15f);
                    }
                    else m.shotT = 0.15f;
                }
                else m.shotT = 0.15f;   // rien à viser : il regarde de nouveau un peu plus tard
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
            Fx.Glass(p);   // le panneau vole en éclats
            if (good)
            {
                Fx.Confetti(p);
                Snd.Play(Snd.I.pickup, 0.8f, 0f);
                P.Flash(Hud.StarGold, 0.15f);
                fovKick = Mathf.Max(fovKick, 5f);   // un seul coup de zoom au passage, pas à chaque tir
                foreach (Mate m in squad) m.rig.Land(0.35f);
                Haptics.Pulse(12);
            }
            else
            {
                Snd.Play(Snd.I.hurt, 0.6f);
                shake = Mathf.Max(shake, 0.25f);
            }
            if (squad.Count >= ArcadeMaxSquad && g.mul[l] && before < ArcadeMaxSquad) score += 200f;
        }

        /// <summary>Une caisse d'armes : deux caisses fusionnent en une arme du niveau au-dessus.</summary>
        // ---- fusion d'armes visible : les armes ramassées flottent au-dessus de la bande, deux pareilles se rejoignent
        Transform heldIcon, mergeA, mergeB, newIcon;
        float mergeT = -1f, popT = -1f;

        /// <summary>Modèle 3D (GLB) centré sous parent et mis à la taille voulue (plus grande dimension), sans ombre. Null s'il manque.</summary>
        static Transform FitModel(Transform parent, string name, float size)
        {
            if (!GlbModel.Exists(name)) return null;
            Transform m = GlbModel.Spawn(name, parent);
            if (m == null) return null;
            m.localPosition = Vector3.zero;
            m.localRotation = Quaternion.Euler(0f, 180f, 0f);
            m.localScale = Vector3.one;
            Renderer[] rs = m.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                Bounds bb = rs[0].bounds;
                foreach (Renderer r in rs) bb.Encapsulate(r.bounds);
                float k = size / Mathf.Max(0.001f, Mathf.Max(bb.size.x, Mathf.Max(bb.size.y, bb.size.z)));
                m.localScale = Vector3.one * k;
                m.localPosition = -parent.InverseTransformVector(bb.center - parent.position) * k;
            }
            foreach (Renderer r in rs) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return m;
        }

        /// <summary>Arcade, à mains nues : le héros lance sa batte (une nouvelle à chaque fois), qui tournoie jusqu'à l'ennemi.</summary>
        void ThrowBat(Ent target, Vector3? from = null)
        {
            bool mate = from.HasValue;
            if (!mate)
            {
                P.throwT = 0f;
                Snd.PlayPitch(Snd.I.whoosh, 0.35f, Random.Range(1.1f, 1.3f));
            }
            Vector3 at = mate ? from.Value : new Vector3(px + 0.3f, 1.5f, pz + 0.5f);
            string key = mate ? "batM" : "batH";
            Transform tr = TakeBolt(key, at);
            Transform spinner;
            if (tr != null) spinner = tr.GetChild(0);
            else
            {
                tr = Art.Node(world, "bat", at);
                spinner = new GameObject("spin").transform;
                spinner.SetParent(tr, false);
                // la bande : batte simple (2 cylindres, léger) ; le héros : le modèle 3D de la batte
                if (mate || FitModel(spinner, "arme_voleur_niv1", 1.25f) == null)
                {
                    Material wood = Art.Mat(new Color(0.72f, 0.52f, 0.3f), 0.45f);
                    Art.Part(spinner, Art.Cylinder, wood, new Vector3(0f, -0.2f, 0f), new Vector3(0.06f, 0.22f, 0.06f), default, false);
                    Art.Part(spinner, Art.Cylinder, wood, new Vector3(0f, 0.18f, 0f), new Vector3(0.11f, 0.2f, 0.11f), default, false);
                }
                if (!mate)
                {
                    TrailRenderer trl = Trail(tr);   // traînée : la batte se suit des yeux
                    trl.emitting = true;
                }
            }
            bolts.Add(new Bolt { tr = tr, target = target, smg = mate, bat = spinner, spin = Random.Range(0f, 360f), pool = key });
        }

        static string TierModel(int tier) => tier >= 2 ? "arme_voleur_niv3" : "arme_voleur_niv2";

        Transform WeaponIcon(int tier, Vector3 at)
        {
            var h = new GameObject("weapon_icon").transform;
            h.SetParent(world, false);
            h.localPosition = at;
            // modèle de l'arme mis à la taille voulue d'après sa vraie boîte englobante (rendu), centré sur la bulle
            Transform m = FitModel(h, TierModel(tier), 1.1f);
            if (m == null)
                Art.Part(h, Art.Cube, Art.Glow(new Color(1f, 0.8f, 0.3f), new Color(0.6f, 0.4f, 0.05f), 0.85f), Vector3.zero, new Vector3(0.9f, 0.26f, 0.18f));
            // bulle dorée translucide autour de l'arme : lisible au-dessus de la foule
            Art.Part(h, Art.Sphere, Art.Particle(null, new Color(1f, 0.85f, 0.3f, 0.35f)), Vector3.zero, Vector3.one * 1.35f, default, false);
            Perf.Note("icône arme " + tier + " en " + h.position);
            if (tier >= 3)
            {
                // arme maximale : halo doré
                Material gold = Art.Glow(new Color(1f, 0.8f, 0.2f), new Color(1.6f, 1.1f, 0.2f), 0.5f);
                Art.Part(h, Art.Cylinder, gold, new Vector3(0f, -0.35f, 0f), new Vector3(0.9f, 0.02f, 0.9f), default, false);
            }
            return h;
        }

        void ClearMergeIcons()
        {
            foreach (Transform t in new[] { heldIcon, mergeA, mergeB, newIcon }) if (t != null) Destroy(t.gameObject);
            heldIcon = mergeA = mergeB = newIcon = null;
            mergeT = popT = -1f;
        }

        Vector3 IconBase => new Vector3(px, 3.8f, pz + 0.5f);

        void WeaponCrate(Vector3 p)
        {
            Perf.Note("caisse : niveau " + runTier + ", caisses " + crates);
            if (runTier >= 3) { score += 100f; return; }
            Snd.Play(Snd.I.pickup, 0.8f, 0f);
            if (runTier == 0)
            {
                // à mains nues : la première caisse donne directement le pistolet
                UpgradeWeapon();
                return;
            }
            crates++;
            if (crates < CratesPerMerge)
            {
                // première arme : elle reste en l'air au-dessus de la bande, en attendant sa jumelle
                if (heldIcon != null) Destroy(heldIcon.gameObject);
                heldIcon = WeaponIcon(runTier, IconBase + new Vector3(-0.85f, 0f, 0f));
                Fx.Pickup(heldIcon.position);
                return;
            }
            crates = 0;
            // deuxième arme : les deux se rejoignent puis fusionnent
            if (mergeA != null) Destroy(mergeA.gameObject);
            if (mergeB != null) Destroy(mergeB.gameObject);
            mergeA = heldIcon != null ? heldIcon : WeaponIcon(runTier, IconBase + new Vector3(-0.85f, 0f, 0f));
            heldIcon = null;
            mergeB = WeaponIcon(runTier, IconBase + new Vector3(0.85f, 0f, 0f));
            mergeT = 0f;
        }

        /// <summary>La bande porte la même arme que le héros (batte, pistolet, mitraillette).</summary>
        void ArmCrew()
        {
            foreach (Mate m in squad) if (m.rig != null) m.rig.CrewWeapon(runTier);
        }

        void UpgradeWeapon()
        {
            runTier++;
            gear[GPistol] = runTier >= 1;
            gear[GSmg] = runTier >= 2;
            ApplyGear();
            ArmCrew();
            if (newIcon != null) Destroy(newIcon.gameObject);
            newIcon = WeaponIcon(runTier, IconBase);
            popT = 0f;
            Fx.Confetti(IconBase);
            P.Flash(Hud.StarGold, 0.3f);
            Snd.Play(Snd.I.pickup, 1f, 0f);
            Snd.Play(Snd.I.perfect, 0.7f, 0f);
            hud.Flash(0.35f);
            fovKick = Mathf.Max(fovKick, 7f);
            hud.Toast(runTier >= 3 ? L("SUPER SMG!", "SUPER MITRAILLETTE !") : runTier >= 2 ? L("SMG!", "MITRAILLETTE !") : L("PISTOL!", "PISTOLET !"));
            foreach (Mate m in squad) { m.rig.Land(0.4f); m.rig.Flash(Hud.StarGold, 0.2f); }
            Haptics.Pulse(30, true);
        }

        /// <summary>Animation des armes au-dessus de la bande (attente, rapprochement, fusion, nouvelle arme qui descend dans la main).</summary>
        void MergeTick(float dt)
        {
            Vector3 b = IconBase;
            // l'arme se présente de profil, inclinée vers la caméra, et se balance doucement (une rotation complète la montrait par la tranche)
            float sway = Mathf.Sin(Time.time * 2.5f) * 18f;
            Quaternion face = Quaternion.Euler(35f, sway, 0f);
            if (heldIcon != null)
            {
                heldIcon.localPosition = b + new Vector3(-0.85f, Mathf.Sin(Time.time * 3f) * 0.08f, 0f);
                heldIcon.localRotation = face;
            }
            if (mergeT >= 0f)
            {
                mergeT += dt;
                float k = Mathf.Clamp01(mergeT / 0.45f);
                float e = k * k;
                if (mergeA != null) { mergeA.localPosition = b + new Vector3(-0.85f * (1f - e), 0.15f * Mathf.Sin(k * Mathf.PI), 0f); mergeA.localRotation = face * Quaternion.Euler(0f, 0f, 25f * Mathf.Sin(k * Mathf.PI)); }
                if (mergeB != null) { mergeB.localPosition = b + new Vector3(0.85f * (1f - e), 0.15f * Mathf.Sin(k * Mathf.PI), 0f); mergeB.localRotation = face * Quaternion.Euler(0f, 0f, -25f * Mathf.Sin(k * Mathf.PI)); }
                if (k >= 1f)
                {
                    mergeT = -1f;
                    if (mergeA != null) Destroy(mergeA.gameObject);
                    if (mergeB != null) Destroy(mergeB.gameObject);
                    mergeA = mergeB = null;
                    hud.Flash(0.25f);
                    UpgradeWeapon();
                }
            }
            if (popT >= 0f && newIcon != null)
            {
                popT += dt;
                // pop (gros puis normal), tient un instant, puis descend dans la main du héros en rétrécissant
                float s = popT < 0.2f ? Mathf.Lerp(0.3f, 1.5f, popT / 0.2f) : popT < 0.4f ? Mathf.Lerp(1.5f, 1.1f, (popT - 0.2f) / 0.2f) : popT < 1.0f ? 1.1f : Mathf.Lerp(1.1f, 0.2f, (popT - 1f) / 0.3f);
                float y = popT < 1.0f ? 0f : Mathf.Lerp(0f, -2.6f, (popT - 1f) / 0.3f);
                newIcon.localPosition = b + new Vector3(0f, y, 0f);
                newIcon.localScale = Vector3.one * s;
                newIcon.localRotation = face;
                if (popT >= 1.3f)
                {
                    Destroy(newIcon.gameObject);
                    newIcon = null;
                    popT = -1f;
                    Fx.Sparkle(new Vector3(px, 1.3f, pz));
                }
            }
        }

        /// <summary>
        /// Arcade : une « mesure » de parcours, lisible en une seconde. Les bandes de vigiles grossissent avec ta bande
        /// et la distance ; obstacles d'un seul couloir (ils fauchent les membres de la bande qui passent dedans),
        /// complices à recruter, pièces en guide.
        /// </summary>
        float ArcadeRow(float z, int[] lanes)
        {
            // piste épurée (retours YSO) : des groupes d'ennemis, quelques pièces, et rien d'autre
            // (ni obstacles, ni trousses, ni recrues : la bande ne grossit que par les portes)
            float prog = Mathf.Clamp01((z - stageStartZ) / Mathf.Max(1f, StageLen));
            int crowd = squad.Count + 1;
            float r = Random.value;
            if (r < 0.6f)
            {
                int n = Mathf.Clamp(3 + Mathf.Min(crowd, 30) / 3 + Mathf.Max(0, crowd - 30) / 8 + Mathf.RoundToInt(prog * (3f + 2f * arcD) + arcD * 2f), 3, 22);
                SetPackHp(z, n, 1f);
                SpawnPack(lanes[0], z, n);
                arcPackHp = 0f;
                for (int i = 0; i < 3; i++) Spawn(ET.Piece, lanes[2], z + 2f + i * 2.4f);
                return 20f;
            }
            if (r < 0.85f)
            {
                // barrage : deux groupes côte à côte, un couloir libre
                int n = Mathf.Clamp(2 + Mathf.Min(crowd, 30) / 4 + Mathf.Max(0, crowd - 30) / 10 + Mathf.RoundToInt(prog * (2f + 2f * arcD) + arcD), 2, 16);
                SetPackHp(z, n, 0.65f);   // deux groupes : on n'en affronte qu'un si on choisit bien son couloir
                SpawnPack(lanes[0], z, n);
                SpawnPack(lanes[1], z + 1.5f, n);
                arcPackHp = 0f;
                for (int i = 0; i < 3; i++) Spawn(ET.Piece, lanes[2], z + 2f + i * 2.4f);
                return 22f;
            }
            // respiration : une ligne de pièces
            for (int i = 0; i < 4; i++) Spawn(ET.Piece, lanes[1], z + i * 2.2f);
            return 14f;
        }

        /// <summary>Boss à terre : la bande fonce dans le MEGA STORE, puis l'écran de victoire.</summary>
        bool ArcadeFinale()
        {
            if (!campaign || tutorialMode || demo) return false;
            // un champ de cartons de butin, puis le magasin
            const int rows = 7;
            float z0 = pz + 18f, gap = 6.5f;
            lootEndZ = z0 + (rows - 1) * gap;
            finaleZ = lootEndZ + 22f;
            nextSpawnZ = 1e9f;
            hud.Toast(L("SMASH THE CRATES!", "CASSE LES CARTONS !"));   // ils barrent la route jusqu'au MEGA STORE
            foreach (LootBox b in loot) if (b.tr != null) Destroy(b.tr.gameObject);
            loot.Clear();
            Material card = Art.Mat(new Color(0.78f, 0.56f, 0.32f), 0.25f);
            Material tape = Art.Mat(new Color(0.55f, 0.36f, 0.18f), 0.3f);
            Material gold = Art.Glow(new Color(1f, 0.8f, 0.25f), new Color(1.3f, 0.9f, 0.2f), 0.6f);
            // résistance calée sur la puissance de feu réelle (arme, ateliers, taille de la bande) et le temps de traversée :
            // en restant dans un couloir, la bande peut abattre ~2 fois la résistance de ce couloir (petite ou grosse bande)
            float heroRate = (gear[GSmg] ? 0.11f : gear[GPistol] ? Tuning.AutoPistolRate : Tuning.AutoBareRate) * RateK;
            float lootDps = LootDmg * DmgK * (1f / heroRate + squad.Count / (MateRate * RateK));
            float crossT = (lootEndZ - pz) / Mathf.Max(4f, speed);
            // retour : il faut s'y reprendre à deux fois (comme Hell Escape) → à la première tentative on en casse environ la moitié,
            // les cartons cassés restent cassés, la deuxième passe
            float laneHp = (0.62f + 0.08f * arcD) * lootDps * crossT;
            Perf.Note("cartons : " + lootDps.ToString("0.0") + " dégâts/s, traversée " + crossT.ToString("0.0") + " s, couloir " + laneHp.ToString("0"));
            int mask = PlayerPrefs.GetInt(LootKey, 0);
            for (int r = 0; r < rows; r++)
                for (int l = 0; l < 3; l++)
                {
                    if ((mask & (1 << (r * 3 + l))) != 0) continue;   // cassé à une tentative précédente : il ne revient pas
                    // premières rangées plus faciles, les dernières plus solides (moyenne = résistance du couloir / 7)
                    int hp = Mathf.Max(3, Mathf.RoundToInt(laneHp / rows * (0.7f + 0.1f * r) * Random.Range(0.9f, 1.1f)));
                    var b = new LootBox { lane = l, z = z0 + r * gap + Random.Range(-0.6f, 0.6f), hp = hp, max = hp, id = r * 3 + l };
                    b.tr = Art.Node(world, "loot", new Vector3((l - 1) * Lane, 0f, b.z));
                    float k = 0.8f + 0.06f * Mathf.Min(hp, 8);   // les cartons solides sont plus gros
                    Art.Part(b.tr, Art.Cube, card, new Vector3(0f, 0.6f * k, 0f), new Vector3(1.4f, 1.2f, 1.1f) * k);
                    Art.Part(b.tr, Art.Cube, tape, new Vector3(0f, 1.2f * k + 0.01f, 0f), new Vector3(0.3f, 0.02f, 1.12f) * k, default, false);
                    Art.Part(b.tr, Art.Cylinder, gold, new Vector3(0f, 1.2f * k + 0.08f, 0f), new Vector3(0.45f, 0.06f, 0.45f) * k, default, false);
                    loot.Add(b);
                }
            // les vigiles restants s'enfuient
            foreach (Ent e in ents)
                if (!e.dead && !e.hit && e.type == ET.Casseur && e.rig != null)
                {
                    e.hit = true;
                    e.rig.KO(new Vector3(Random.Range(-4f, 4f), 6f, 6f), new Vector3(Random.Range(-5f, 5f), 0f, Random.Range(-5f, 5f)));
                }
            BuildStore(finaleZ);
            Perf.Note("finale : " + loot.Count + " cartons jusqu'à " + lootEndZ.ToString("0") + ", magasin à " + finaleZ.ToString("0"));
            hud.Toast(L("RAID THE MEGA STORE!", "PILLE LE MEGA STORE !"));
            return true;
        }

        void BuildStore(float z)
        {
            if (store != null) Destroy(store);
            store = new GameObject("megastore");
            store.transform.SetParent(world, false);
            store.transform.localPosition = new Vector3(0f, 0f, z);
            Transform t = store.transform;
            float w = Street.ShopX * 2f + 1f;
            Material wall = Art.Mat(new Color(0.16f, 0.15f, 0.2f), 0.4f);
            Material gold = Art.Glow(new Color(1f, 0.8f, 0.2f), new Color(1.4f, 1.0f, 0.25f), 0.5f);
            Material glass = Art.Glow(new Color(1f, 0.93f, 0.7f), new Color(1.6f, 1.4f, 0.9f), 0.8f);
            // façade : murs de part et d'autre de la grande entrée, linteau, enseigne lumineuse
            Art.Part(t, Art.Cube, wall, new Vector3(-(w * 0.5f + 4.2f) * 0.5f - 0.1f, 4.5f, 0.6f), new Vector3(w * 0.5f - 4.2f, 9f, 1.2f));
            Art.Part(t, Art.Cube, wall, new Vector3((w * 0.5f + 4.2f) * 0.5f + 0.1f, 4.5f, 0.6f), new Vector3(w * 0.5f - 4.2f, 9f, 1.2f));
            Art.Part(t, Art.Cube, wall, new Vector3(0f, 7.6f, 0.6f), new Vector3(8.6f, 2.8f, 1.2f));
            Art.Part(t, Art.Cube, gold, new Vector3(0f, 6.15f, 0.2f), new Vector3(8.6f, 0.18f, 0.3f), default, false);
            Art.Part(t, Art.Cube, gold, new Vector3(-4.3f, 3.1f, 0.2f), new Vector3(0.18f, 6.2f, 0.3f), default, false);
            Art.Part(t, Art.Cube, gold, new Vector3(4.3f, 3.1f, 0.2f), new Vector3(0.18f, 6.2f, 0.3f), default, false);
            // intérieur éclairé : on voit la lumière par les portes ouvertes
            Art.Part(t, Art.Cube, glass, new Vector3(0f, 3f, 6f), new Vector3(8.4f, 6f, 0.2f), default, false);
            Art.Part(t, Art.Cube, glass, new Vector3(-3.2f, 3f, 1.4f), new Vector3(1.8f, 5.6f, 0.08f), new Vector3(0f, -60f, 0f), false);
            Art.Part(t, Art.Cube, glass, new Vector3(3.2f, 3f, 1.4f), new Vector3(1.8f, 5.6f, 0.08f), new Vector3(0f, 60f, 0f), false);
            Texture2D sign = Resources.Load<Texture2D>("SamediRouge/Art/mega_store");
            if (sign != null)
                Art.Part(t, Art.Cube, Art.Tex(sign, Vector2.one, 0.4f, sign, new Color(0.9f, 0.9f, 0.9f)), new Vector3(0f, 7.7f, -0.05f), new Vector3(10f, 2.5f, 0.12f), new Vector3(0f, 180f, 0f), false);   // tourné : la face vue par la caméra n'est pas en miroir
            Texture2D sale = Resources.Load<Texture2D>("SamediRouge/Art/sale_70");
            if (sale != null)
            {
                Material sm = Art.Particle(sale, Color.white);
                Art.Part(t, Art.Cube, sm, new Vector3(-6.4f, 5.4f, -0.05f), new Vector3(2.2f, 2.2f, 0.05f), new Vector3(0f, 180f, -12f), false);
                Art.Part(t, Art.Cube, sm, new Vector3(6.4f, 5.4f, -0.05f), new Vector3(2.2f, 2.2f, 0.05f), new Vector3(0f, 180f, 12f), false);
            }
        }

        /// <summary>Course vers le magasin : la bande s'engouffre par les portes, puis la victoire.</summary>
        void FinaleTick()
        {
            if (finaleZ < 0f) return;
            LootTick();
            if (pz < lootEndZ + 3f) speed = 8.5f;   // champ de cartons : on garde le contrôle, un peu plus lentement
            else
            {
                lane = 1;
                freeX = 0f;
                speed = Mathf.Max(speed, 15f);
            }
            foreach (Mate m in squad)
                if (m.rig != null && m.z > finaleZ + 0.5f && m.rig.root.gameObject.activeSelf) m.rig.root.gameObject.SetActive(false);
            if (pz > finaleZ + 0.6f) P.SetVisible(false);
            if (pz >= finaleZ + 4.5f)
            {
                finaleZ = -1f;
                Fx.Confetti(new Vector3(0f, 3f, pz + 2f));
                Fx.Confetti(new Vector3(-2f, 3f, pz + 1f));
                Fx.Confetti(new Vector3(2f, 3f, pz + 1f));
                PlayerPrefs.DeleteKey(LootKey);   // magasin pillé : la prochaine fois, un champ de cartons tout neuf
                RunWon();
            }
        }

        void SpawnCraft(float z, int lane, bool armor = false)
        {
            int step = armor ? armorLv : runTier + craftBoost;
            var c = new Craft { z = z, lane = lane, armor = armor, need = Mathf.RoundToInt((step == 0 ? 3 : step == 1 ? 5 : step == 2 ? 8 : 10 + 3 * (step - 3)) * (1f + 0.25f * arcD)) };
            Perf.Note("panneau de fusion couloir " + lane + " à " + z.ToString("0") + " (pz " + pz.ToString("0") + ")");
            c.go = new GameObject("craft");
            c.go.transform.SetParent(world, false);
            c.go.transform.localPosition = new Vector3((lane - 1) * Lane, 0f, z);
            Transform t = c.go.transform;
            // portique vert pour l'arme, bleu pour l'armure, bien visible comme le « Craft » de Hell Escape
            Material post = armor ? Art.Mat(new Color(0.18f, 0.38f, 0.75f), 0.6f, 0.3f) : Art.Mat(new Color(0.18f, 0.62f, 0.25f), 0.6f, 0.3f);
            Art.Part(t, Art.Cylinder, post, new Vector3(-1.12f, 1.35f, 0f), new Vector3(0.14f, 1.35f, 0.14f), default, false);
            Art.Part(t, Art.Cylinder, post, new Vector3(1.12f, 1.35f, 0f), new Vector3(0.14f, 1.35f, 0.14f), default, false);
            Art.Part(t, Art.Cube, post, new Vector3(0f, 2.72f, 0f), new Vector3(2.4f, 0.14f, 0.16f), default, false);
            c.panel = Art.Part(t, Art.Cube, Art.Particle(null, armor ? new Color(0.3f, 0.55f, 1f, 0.7f) : new Color(0.25f, 0.9f, 0.35f, 0.7f)), new Vector3(0f, 1.35f, 0f), new Vector3(2.1f, 2.6f, 0.04f), default, false).transform;
            // jauge : fond sombre, remplissage vert qui grandit depuis la gauche
            Art.Part(t, Art.Cube, Art.Mat(new Color(0.12f, 0.12f, 0.15f), 0.3f), new Vector3(0f, 2.35f, -0.06f), new Vector3(1.8f, 0.24f, 0.05f), default, false);
            c.bar = Art.Node(t, "bar", new Vector3(-0.88f, 2.35f, -0.1f));
            Art.Part(c.bar, Art.Cube, Art.Glow(new Color(0.3f, 0.95f, 0.35f), new Color(0.3f, 1.2f, 0.35f), 0.5f), new Vector3(0.5f, 0f, 0f), new Vector3(1f, 0.18f, 0.05f), default, false);
            c.bar.localScale = new Vector3(0.001f, 1f, 1f);
            // l'arme qu'on va obtenir, au-dessus du panneau
            var ic = new GameObject("next").transform;
            ic.SetParent(t, false);
            // l'arme à gagner est affichée dans le portique, sous la jauge, face à la caméra
            ic.localPosition = new Vector3(0f, 1.2f, -0.15f);
            ic.localRotation = Quaternion.Euler(20f, 0f, 0f);
            if (armor)
            {
                // l'armure à gagner : icône du vêtement (sweat, blouson, gilet volé)
                Texture2D tex = GearIcons.Upgrade(1, true, Mathf.Clamp(armorLv + 1, 1, 3)).texture;
                tex.name = "armor" + Mathf.Clamp(armorLv + 1, 1, 3);
                ic.localRotation = Quaternion.identity;
                Art.Part(ic, Art.Quad, Art.Particle(tex, Color.white), new Vector3(0f, 0.1f, 0f), new Vector3(1.3f, 1.3f, 1f), default, false);
            }
            else if (FitModel(ic, TierModel(Mathf.Min(runTier + 1, 3)), 1.6f) == null)
                Art.Part(ic, Art.Cube, Art.Glow(new Color(1f, 0.8f, 0.3f), new Color(0.6f, 0.4f, 0.05f), 0.85f), Vector3.zero, new Vector3(0.9f, 0.26f, 0.18f));
            crafts.Add(c);
        }

        /// <summary>Tir automatique sans ennemi en vue : il vise le panneau de fusion, sinon la porte de son couloir (plus on tire, meilleure elle devient).</summary>
        bool ArcadeShootProps()
        {
            int l = Mathf.Clamp(Mathf.RoundToInt(px / Lane) + 1, 0, 2);
            LootBox lb = NearestLoot(l, pz, 30f);
            if (lb != null)
            {
                Shoot(null, gear[GSmg]);
                bolts[bolts.Count - 1].loot = lb;
                lb.pend += LootDmg;
                return true;
            }
            foreach (Craft c in crafts)
            {
                float d = c.z - pz;
                if (c.done || c.lane != l || d < 2f || d > 32f) continue;
                Shoot(null, gear[GSmg]);
                bolts[bolts.Count - 1].craft = c;
                return true;
            }
            foreach (Gate g in gates)
            {
                float d = g.z - pz;
                if (g.used || d < 3f || d > 32f) continue;
                Shoot(null, gear[GSmg]);
                Bolt b = bolts[bolts.Count - 1];
                b.gate = g;
                b.lane = l;
                return true;
            }
            return false;
        }

        LootBox NearestLoot(int lane, float fromZ, float range)
        {
            LootBox best = null;
            foreach (LootBox b in loot)
            {
                if (b.broken || b.pend >= b.hp || (lane >= 0 && b.lane != lane)) continue;   // déjà condamné par les tirs en vol : on vise le suivant
                float d = b.z - fromZ;
                if (d < 1f || d > range) continue;
                if (best == null || b.z < best.z) best = b;
            }
            return best;
        }

        /// <summary>Pas de carton intact dans ce couloir à hauteur de z (même rangée).</summary>
        bool LaneOpen(int lane, float z)
        {
            foreach (LootBox o in loot)
                if (!o.broken && o.lane == lane && Mathf.Abs(o.z - z) < 1.6f) return false;
            return true;
        }

        int lootStreak;
        float lootStreakT;
        int tagCrowd = -1;
        float crowdPop, crowdHurt;

        /// <summary>Dégâts d'un tir sur un carton : dépendent de l'arme ACHETÉE (batte 1, pistolet 2, mitraillette 3),
        /// pas de l'arme gagnée en course — c'est l'amélioration de l'accueil qui ouvre le MEGA STORE.</summary>
        int LootDmg => Mathf.Clamp(Progress.GearLevel(thief, Tuning.Weapon), 1, 3);
        bool blocked;

        /// <summary>
        /// La bande s'écrase sur un carton pas cassé : choc bien visible (ralenti, flash rouge, la bande rebondit en arrière,
        /// le carton tremble, « BLOQUÉ ! »), puis l'écran de défaite un peu plus tard pour qu'on comprenne ce qui s'est passé.
        /// </summary>
        void Blocked(LootBox b)
        {
            blocked = true;
            cause = L("Blocked by the crates", "Bloqué par les cartons");
            Vector3 at = b.tr.position + Vector3.up * 0.8f;
            Fx.Debris(at);
            Fx.Hit(at);
            b.punch = 2.5f;
            shake = Mathf.Max(shake, 0.9f);
            fovKick = Mathf.Max(fovKick, 10f);
            hud.Damage(1f);
            hud.Flash(0.35f);
            Snd.Play(Snd.I.crash, 1f);
            Snd.Play(Snd.I.hurt, 0.8f);
            Haptics.Heavy();
            hud.Toast(L("BLOCKED!", "BLOQUÉ !"));
            hud.FloatKey(new Vector3(px, 0.2f, pz - 1.5f), L("BREAK THE CRATES!", "CASSE LES CARTONS !"), Hud.Bad, true);   // sous le « BLOQUÉ ! », pas dessus
            // la bande rebondit en arrière contre le mur de cartons
            foreach (Mate m in squad)
                if (m.rig != null) m.rig.KO(new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(3f, 5f), -Random.Range(4f, 7f)), new Vector3(Random.Range(-6f, 6f), 0f, Random.Range(-4f, 4f)));
            EndGame();
        }

        void LootHit(LootBox b, int mult = 1)
        {
            if (b.broken) return;
            b.hp -= BoostDmg(LootDmg * mult);
            b.pend = Mathf.Max(0, b.pend - LootDmg * mult);
            b.punch = 1f;
            if (b.hp > 0) return;
            b.broken = true;
            PlayerPrefs.SetInt(LootKey, PlayerPrefs.GetInt(LootKey, 0) | (1 << b.id));
            int gain = b.max;
            coins += gain;
            Vector3 p = b.tr.position + Vector3.up * 0.8f;
            Fx.Debris(p);
            Fx.Coin(p);
            hud.FloatKey(p + Vector3.up * 0.6f, "+" + gain, Hud.StarGold, false);
            // cartons cassés à la suite : la note monte
            lootStreak = Time.time - lootStreakT < 1.2f ? lootStreak + 1 : 0;
            lootStreakT = Time.time;
            Snd.PlayPitch(Snd.I.coin, 0.6f, 1f + 0.06f * Mathf.Min(lootStreak, 12));
            Haptics.Pulse(8);
            Destroy(b.tr.gameObject);
        }

        /// <summary>Cartons : rebond quand on les touche ; celui qu'on percute sans l'avoir cassé s'écrase sans rien rapporter.</summary>
        void LootTick()
        {
            int l = Mathf.Clamp(Mathf.RoundToInt(px / Lane) + 1, 0, 2);
            for (int i = loot.Count - 1; i >= 0; i--)
            {
                LootBox b = loot[i];
                if (b.broken || b.tr == null) { loot.RemoveAt(i); continue; }
                if (b.punch > 0f)
                {
                    b.punch = Mathf.Max(0f, b.punch - Time.deltaTime * 6f);
                    b.tr.localScale = Vector3.one * (1f + 0.15f * b.punch);
                }
                if (b.lane == l && !b.dodged && pz >= b.z - 0.9f && st == St.Play)
                {
                    // le héros est au bord de son couloir et le couloir voisin est dégagé : il se faufile par la brèche au lieu de s'écraser
                    int n = px < (l - 1) * Lane ? l - 1 : l + 1;
                    if (n >= 0 && n <= 2 && Mathf.Abs(px - (n - 1) * Lane) < 1.9f && LaneOpen(n, b.z))
                    {
                        b.dodged = true;
                        freeX = (n - 1) * Lane;
                        continue;
                    }
                    // un carton pas cassé barre la route : la bande s'y écrase, partie perdue
                    Blocked(b);
                    return;
                }
                if (b.z < pz - 4f) { Destroy(b.tr.gameObject); loot.RemoveAt(i); }
            }
        }

        /// <summary>
        /// Une porte touchée — règle simple et visible (retour YSO) : +1 tous les 2 tirs, sans plafond
        /// (les portes négatives remontent vers le positif). Une porte ×N gagne +1 au multiplicateur tous les 6 tirs.
        /// </summary>
        void GateHit(Gate g, int l)
        {
            if (g.used) return;
            Vector3 at = new Vector3((l - 1) * Lane, 2.3f, g.z);
            if (g.mul[l])
            {
                if (++g.hits[l] >= 8 && g.val[l] < 4)   // multiplicateur : +1 tous les 8 tirs, jusqu'à ×4
                {
                    g.hits[l] = 0;
                    g.val[l]++;
                    hud.FloatKey(at, "×" + g.val[l] + "!", Hud.Yellow, true);
                    Snd.PlayPitch(Snd.I.pickup, 0.5f, 1.3f);
                }
            }
            else if (++g.hits[l] >= 3 + Mathf.Max(0, g.val[l]) / 5)   // +1 tous les 3 tirs, de plus en plus dur (retour : on finissait toujours à 100)
            {
                g.hits[l] = 0;
                g.val[l]++;
                if (g.val[l] == 0) g.val[l] = 1;   // pas de porte « 0 »
                hud.FloatKey(at, "+1", Hud.Yellow, false);
                Snd.PlayPitch(Snd.I.tick, 0.35f, 1f + 0.05f * Mathf.Clamp(g.val[l], -5, 12));
            }
            bool good = g.mul[l] || g.val[l] > 0;
            if (g.panel[l] != null)
            {
                MeshRenderer mr = g.panel[l].GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = Art.Particle(null, good ? GateGood : GateBad);
            }
            g.punch[l] = 1f;
            Fx.Sparkle(new Vector3((l - 1) * Lane, 1.6f, g.z));
        }

        void CraftHit(Craft c)
        {
            if (c.done) return;
            c.hits++;
            c.punch = 1f;
            c.bar.localScale = new Vector3(Mathf.Max(0.001f, 1.76f * Mathf.Clamp01(c.hits / (float)c.need)), 1f, 1f);
            Fx.Sparkle(c.go.transform.position + new Vector3(0f, 1.6f, 0f));
            if (c.hits < c.need) return;
            // jauge pleine : le panneau s'efface, les deux armes fusionnent au-dessus de la bande
            c.done = true;
            Perf.Note("fusion : panneau rempli (" + c.need + " coups), niveau " + runTier);
            Fx.Confetti(c.go.transform.position + Vector3.up * 1.6f);
            Snd.Play(Snd.I.pickup, 0.9f, 0f);
            Destroy(c.go, 0.05f);
            if (c.armor) { UpgradeArmor(); return; }
            if (runTier == 0) { UpgradeWeapon(); return; }
            if (runTier >= 3) { BoostWeapon(); return; }
            if (mergeA != null) Destroy(mergeA.gameObject);
            if (mergeB != null) Destroy(mergeB.gameObject);
            mergeA = WeaponIcon(runTier, IconBase + new Vector3(-0.85f, 0f, 0f));
            mergeB = WeaponIcon(runTier, IconBase + new Vector3(0.85f, 0f, 0f));
            mergeT = 0f;
        }

        /// <summary>Atelier d'armure rempli : nouvelle tenue (sweat → blouson → gilet volé), puis +1 niveau ; chaque niveau protège la bande.</summary>
        void UpgradeArmor()
        {
            armorLv++;
            Perf.Note("armure niveau " + armorLv + ", défense " + (DefChance * 100f).ToString("0") + " %");
            int vest = RunForm;
            P.SetLevels(helmetLv, vest, weaponLv);
            Fx.Confetti(IconBase);
            P.Flash(new Color(0.4f, 0.7f, 1f), 0.3f);
            Snd.Play(Snd.I.pickup, 1f, 0f);
            Snd.Play(Snd.I.perfect, 0.7f, 0f);
            hud.Flash(0.3f);
            fovKick = Mathf.Max(fovKick, 6f);
            hud.Toast(armorLv == 1 ? L("HOODIE!", "SWEAT !") : armorLv == 2 ? L("JACKET!", "BLOUSON !") : armorLv == 3 ? L("STOLEN VEST!", "GILET VOLÉ !") : L("ARMOR +15%", "ARMURE +15 %"));
            foreach (Mate m in squad) { m.rig.Land(0.4f); m.rig.Flash(new Color(0.4f, 0.7f, 1f), 0.2f); }
            Haptics.Pulse(30, true);
        }

        /// <summary>Un bonus debout dans un couloir : carte dorée avec son icône ; on le prend en passant dedans.</summary>
        void SpawnBoon(float z)
        {
            int kind = lastBoonKind < 0 ? Random.Range(0, 2) : 1 - lastBoonKind;   // attaque et défense en alternance
            lastBoonKind = kind;
            var bo = new Boon { z = z, lane = Random.Range(0, 3), kind = kind };
            bo.go = new GameObject("boon");
            bo.go.transform.SetParent(world, false);
            bo.go.transform.localPosition = new Vector3((bo.lane - 1) * Lane, 0f, z);
            Transform t = Art.Node(bo.go.transform, "card", Vector3.zero);
            Color face = kind == 0 ? new Color(0.85f, 0.2f, 0.2f) : new Color(0.16f, 0.45f, 0.95f);
            Art.Part(t, Art.Cube, Art.Glow(new Color(1f, 0.82f, 0.25f), new Color(1.2f, 0.9f, 0.2f), 0.5f), new Vector3(0f, 1.55f, 0f), new Vector3(1.5f, 1.9f, 0.12f), default, false);
            Art.Part(t, Art.Cube, Art.Glow(face, face * 0.5f, 0.3f), new Vector3(0f, 1.55f, -0.07f), new Vector3(1.3f, 1.7f, 0.02f), default, false);
            Sprite sp = GearIcons.Get(kind == 0 ? "atk" : "def");
            Texture2D tex = sp.texture;
            tex.name = "boon" + kind;   // la clé du matériau dépend du nom de la texture
            Art.Part(t, Art.Quad, Art.Particle(tex, Color.white), new Vector3(0f, 1.6f, -0.09f), new Vector3(1.15f, 1.15f, 1f), default, false);
            boons.Add(bo);
            Perf.Note("bonus " + kind + " couloir " + bo.lane + " à " + z.ToString("0"));
        }

        /// <summary>Bonus : léger flottement, ramassé quand le héros passe dans son couloir.</summary>
        void BoonTick()
        {
            int l = Mathf.Clamp(Mathf.RoundToInt(px / Lane) + 1, 0, 2);
            for (int i = boons.Count - 1; i >= 0; i--)
            {
                Boon bo = boons[i];
                if (bo.go == null || bo.z < pz - 6f) { if (bo.go != null) Destroy(bo.go); boons.RemoveAt(i); continue; }
                if (bo.done) continue;
                Transform card = bo.go.transform.GetChild(0);
                card.localPosition = new Vector3(0f, 0.12f * Mathf.Sin(Time.time * 3f + bo.z), 0f);
                card.localRotation = Quaternion.Euler(0f, 10f * Mathf.Sin(Time.time * 2f + bo.z), 0f);
                if (pz < bo.z - 0.3f) continue;
                bo.done = true;
                if (bo.lane != l) continue;   // passé à côté
                TakeBoon(bo);
                Destroy(bo.go);
            }
        }

        void TakeBoon(Boon bo)
        {
            Vector3 p = new Vector3((bo.lane - 1) * Lane, 1.6f, bo.z);
            Fx.Confetti(p);
            Snd.Play(Snd.I.pickup, 1f, 0f);
            Snd.Play(Snd.I.perfect, 0.6f, 0f);
            P.Flash(Hud.StarGold, 0.25f);
            hud.Flash(0.2f);
            Haptics.Pulse(20, true);
            if (bo.kind == 0) { dmgBoon++; hud.Toast(L("ATTACK +20%", "ATTAQUE +20 %")); }
            else { defBoon++; hud.Toast(L("DEFENSE +20%", "DÉFENSE +20 %")); }
            hud.SetBoons(dmgBoon, defBoon);
            Perf.Note("bonus pris : " + bo.kind + " (attaque " + dmgBoon + ", défense " + defBoon + ")");
        }

        /// <summary>Atelier rempli avec la meilleure arme : l'arme monte d'un niveau, toute la bande tire plus vite.</summary>
        void BoostWeapon()
        {
            craftBoost++;
            Perf.Note("atelier : arme niveau " + RunWeaponLv + ", cadence x" + (1f / RateK).ToString("0.00"));
            if (newIcon != null) Destroy(newIcon.gameObject);
            newIcon = WeaponIcon(runTier, IconBase);
            popT = 0f;
            Fx.Confetti(IconBase);
            P.Flash(Hud.StarGold, 0.3f);
            Snd.Play(Snd.I.pickup, 1f, 0f);
            Snd.Play(Snd.I.perfect, 0.7f, 0f);
            hud.Flash(0.3f);
            fovKick = Mathf.Max(fovKick, 6f);
            hud.Toast(L("FIRE RATE +15%", "CADENCE +15 %"));   // la carte d'arme à droite saute et affiche le nouveau niveau
            foreach (Mate m in squad) { m.rig.Land(0.4f); m.rig.Flash(Hud.StarGold, 0.2f); }
            Haptics.Pulse(30, true);
        }

        /// <summary>Petits coups de « boing » sur les panneaux touchés, et nettoyage des panneaux dépassés.</summary>
        void PropsTick(float dt)
        {
            foreach (Gate g in gates)
                for (int l = 0; l < 3; l++)
                {
                    if (g.punch[l] <= 0f || g.panel[l] == null) continue;
                    g.punch[l] = Mathf.Max(0f, g.punch[l] - dt * 6f);
                    float k = 1f + 0.12f * g.punch[l];
                    g.panel[l].localScale = new Vector3(2.1f * k, 2.6f * k, 0.04f);
                }
            for (int i = crafts.Count - 1; i >= 0; i--)
            {
                Craft c = crafts[i];
                if (c.go == null || c.z < pz - 6f) { if (c.go != null) Destroy(c.go); crafts.RemoveAt(i); continue; }
                if (c.punch > 0f && c.panel != null)
                {
                    c.punch = Mathf.Max(0f, c.punch - dt * 6f);
                    float k = 1f + 0.12f * c.punch;
                    c.panel.localScale = new Vector3(2.1f * k, 2.6f * k, 0.04f);
                }
            }
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
            // carte d'arme fixe sur le bord droit (pas de texte dans la scène, comme Hell Escape)
            hud.SetWeaponCard(runTier, RunWeaponLv);
            hud.SetArmorCard(armorLv);
            hud.SetBoons(dmgBoon, defBoon);
            int myLane = Mathf.Clamp(Mathf.RoundToInt(px / Lane) + 1, 0, 2);
            foreach (LootBox b in loot)
            {
                if (b.broken || b.z - pz >= 60f) continue;
                // carton de ton couloir tout proche : chiffre rouge qui clignote (il va te bloquer)
                bool danger = b.lane == myLane && b.z - pz < 12f && b.z > pz;
                float pulse = danger ? 1f + 0.25f * Mathf.Abs(Mathf.Sin(Time.time * 12f)) : 1f;
                hud.Tag(new Vector3((b.lane - 1) * Lane, 1.9f, b.z), b.hp.ToString(), danger ? Hud.Bad : Color.white, 1.4f * pulse);
            }
            // compteur de la bande : il gonfle quand elle grossit, rougit quand elle perd du monde
            int crowd = squad.Count + 1 + pendingMates;
            if (tagCrowd >= 0 && crowd > tagCrowd) crowdPop = 1f;
            if (tagCrowd >= 0 && crowd < tagCrowd) crowdHurt = 1f;
            tagCrowd = crowd;
            crowdPop = Mathf.MoveTowards(crowdPop, 0f, Time.deltaTime * 3f);
            crowdHurt = Mathf.MoveTowards(crowdHurt, 0f, Time.deltaTime * 2.5f);
            Color cc = Color.Lerp(Hud.Yellow, Hud.Bad, crowdHurt);
            if (finaleZ < 0f || pz < lootEndZ + 3f)
            {
                hud.Tag(new Vector3(px, 2.6f, pz), crowd.ToString(), cc, 1.3f * (1f + 0.6f * crowdPop + 0.3f * crowdHurt));
            }
        }
    }
}
