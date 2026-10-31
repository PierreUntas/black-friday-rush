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
            public bool done;
            public Transform bar, panel;
        }

        readonly List<Craft> crafts = new List<Craft>();

        readonly List<Gate> gates = new List<Gate>();
        float nextGateZ, nextCrateZ, mateVolleyT;
        /// <summary>Niveau d'arme de la course : 0 mains nues, 1 pistolet, 2 mitraillette, 3 mitraillette renforcée.</summary>
        int runTier;
        static bool devCenter;   // dev (éditeur) : caisses dans le couloir du milieu, pour tester la fusion sans diriger
        /// <summary>Caisses d'armes ramassées vers la prochaine fusion.</summary>
        int crates;
        const int ArcadeMaxSquad = 24;
        /// <summary>Arcade : la rue est plus longue (environ une minute de course avant le boss).</summary>
        const float ArcadeLengthMult = 3.4f;
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
                var l = new List<Vector2>();
                for (int j = 0; l.Count < 40; j++)
                {
                    float r = 0.6f * Mathf.Sqrt(j + 1.2f), a = j * 2.39996f + 1.2f;
                    var v = new Vector2(Mathf.Clamp(Mathf.Cos(a) * r * 1.1f, -2.9f, 2.9f), -0.5f + Mathf.Sin(a) * r * 0.65f);
                    if (v.sqrMagnitude < 0.45f || (Mathf.Abs(v.x) < 0.55f && v.y > 0f)) continue;   // pas sur le héros ni juste devant lui
                    l.Add(v);
                }
                blob = l.ToArray();
            }
            return blob[Mathf.Min(i, blob.Length - 1)];
        }

        void ArcadeStart()
        {
            if (store != null) Destroy(store);
            finaleZ = -1f;
            foreach (Gate g in gates) if (g.go != null) Destroy(g.go);
            gates.Clear();
            nextGateZ = pz + 48f;
            nextCrateZ = pz + 80f;
            crates = 0;
            ClearMergeIcons();
            foreach (Craft c in crafts) if (c.go != null) Destroy(c.go);
            crafts.Clear();
            mateVolleyT = 0.5f;
            runTier = gear[GSmg] ? 2 : gear[GPistol] ? 1 : 0;
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
            if (z >= nextCrateZ)
            {
                // panneau de fusion dans un couloir au hasard : il faut s'y placer et tirer dessus pour le remplir
                if (runTier < 3) { SpawnCraft(z + 6f, devCenter ? 1 : Random.Range(0, 3)); nextCrateZ = z + Random.Range(80f, 105f); return 14f; }
                nextCrateZ = z + Random.Range(80f, 105f);
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
            FinaleTick();
            if (st != St.Play) return;
            MergeTick(dt);
            PropsTick(dt);
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
        void ThrowBat(Ent target)
        {
            P.throwT = 0f;
            Snd.PlayPitch(Snd.I.whoosh, 0.35f, Random.Range(1.1f, 1.3f));
            Transform tr = Art.Node(world, "bat", new Vector3(px + 0.3f, 1.5f, pz + 0.5f));
            var spinner = new GameObject("spin").transform;
            spinner.SetParent(tr, false);
            if (FitModel(spinner, "arme_voleur_niv1", 1.35f) == null)
                Art.Part(spinner, Art.Cylinder, Art.Mat(new Color(0.62f, 0.4f, 0.2f), 0.4f), Vector3.zero, new Vector3(0.13f, 0.6f, 0.13f), default, false);
            TrailRenderer trl = Trail(tr);   // traînée : la batte se suit des yeux
            trl.emitting = true;
            bolts.Add(new Bolt { tr = tr, target = target, smg = false, bat = spinner });
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

        Vector3 IconBase => new Vector3(px, 4.4f, pz + 0.5f);

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

        void UpgradeWeapon()
        {
            runTier++;
            gear[GPistol] = runTier >= 1;
            gear[GSmg] = runTier >= 2;
            ApplyGear();
            if (newIcon != null) Destroy(newIcon.gameObject);
            newIcon = WeaponIcon(runTier, IconBase);
            popT = 0f;
            Fx.Confetti(IconBase);
            P.Flash(Hud.StarGold, 0.3f);
            Snd.Play(Snd.I.pickup, 1f, 0f);
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
                int n = Mathf.Clamp(2 + crowd / 4 + Mathf.RoundToInt(prog * 4f) + Diff / 3, 2, 10);
                SpawnPack(lanes[0], z, n);
                for (int i = 0; i < 3; i++) Spawn(ET.Piece, lanes[2], z + 2f + i * 2.4f);
                return 20f;
            }
            if (r < 0.85f)
            {
                // barrage : deux groupes côte à côte, un couloir libre
                int n = Mathf.Clamp(2 + crowd / 6 + Mathf.RoundToInt(prog * 3f), 2, 7);
                SpawnPack(lanes[0], z, n);
                SpawnPack(lanes[1], z + 1.5f, n);
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
            finaleZ = pz + 34f;
            nextSpawnZ = 1e9f;
            // les vigiles restants s'enfuient
            foreach (Ent e in ents)
                if (!e.dead && !e.hit && e.type == ET.Casseur && e.rig != null)
                {
                    e.hit = true;
                    e.rig.KO(new Vector3(Random.Range(-4f, 4f), 6f, 6f), new Vector3(Random.Range(-5f, 5f), 0f, Random.Range(-5f, 5f)));
                }
            BuildStore(finaleZ);
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
            lane = 1;
            speed = Mathf.Max(speed, 15f);
            foreach (Mate m in squad)
                if (m.rig != null && m.z > finaleZ + 0.5f && m.rig.root.gameObject.activeSelf) m.rig.root.gameObject.SetActive(false);
            if (pz > finaleZ + 0.6f) P.SetVisible(false);
            if (pz >= finaleZ + 4.5f)
            {
                finaleZ = -1f;
                Fx.Confetti(new Vector3(0f, 3f, pz + 2f));
                Fx.Confetti(new Vector3(-2f, 3f, pz + 1f));
                Fx.Confetti(new Vector3(2f, 3f, pz + 1f));
                RunWon();
            }
        }

        void SpawnCraft(float z, int lane)
        {
            var c = new Craft { z = z, lane = lane, need = runTier == 0 ? 3 : runTier == 1 ? 6 : 12 };
            Perf.Note("panneau de fusion couloir " + lane + " à " + z.ToString("0") + " (pz " + pz.ToString("0") + ")");
            c.go = new GameObject("craft");
            c.go.transform.SetParent(world, false);
            c.go.transform.localPosition = new Vector3((lane - 1) * Lane, 0f, z);
            Transform t = c.go.transform;
            Material post = Art.Mat(new Color(0.92f, 0.92f, 0.95f), 0.6f, 0.3f);
            Art.Part(t, Art.Cylinder, post, new Vector3(-1.12f, 1.35f, 0f), new Vector3(0.14f, 1.35f, 0.14f), default, false);
            Art.Part(t, Art.Cylinder, post, new Vector3(1.12f, 1.35f, 0f), new Vector3(0.14f, 1.35f, 0.14f), default, false);
            Art.Part(t, Art.Cube, post, new Vector3(0f, 2.72f, 0f), new Vector3(2.4f, 0.14f, 0.16f), default, false);
            c.panel = Art.Part(t, Art.Cube, Art.Particle(null, new Color(1f, 0.78f, 0.2f, 0.45f)), new Vector3(0f, 1.35f, 0f), new Vector3(2.1f, 2.6f, 0.04f), default, false).transform;
            // jauge : fond sombre, remplissage vert qui grandit depuis la gauche
            Art.Part(t, Art.Cube, Art.Mat(new Color(0.12f, 0.12f, 0.15f), 0.3f), new Vector3(0f, 2.35f, -0.06f), new Vector3(1.8f, 0.24f, 0.05f), default, false);
            c.bar = Art.Node(t, "bar", new Vector3(-0.88f, 2.35f, -0.1f));
            Art.Part(c.bar, Art.Cube, Art.Glow(new Color(0.3f, 0.95f, 0.35f), new Color(0.3f, 1.2f, 0.35f), 0.5f), new Vector3(0.5f, 0f, 0f), new Vector3(1f, 0.18f, 0.05f), default, false);
            c.bar.localScale = new Vector3(0.001f, 1f, 1f);
            // l'arme qu'on va obtenir, au-dessus du panneau
            var ic = new GameObject("next").transform;
            ic.SetParent(t, false);
            ic.localPosition = new Vector3(0f, 3.5f, 0f);
            ic.localRotation = Quaternion.Euler(30f, 0f, 0f);
            Art.Part(ic, Art.Sphere, Art.Particle(null, new Color(1f, 0.85f, 0.3f, 0.3f)), Vector3.zero, Vector3.one * 1.3f, default, false);
            if (FitModel(ic, TierModel(runTier + 1), 1.3f) == null)
                Art.Part(ic, Art.Cube, Art.Glow(new Color(1f, 0.8f, 0.3f), new Color(0.6f, 0.4f, 0.05f), 0.85f), Vector3.zero, new Vector3(0.9f, 0.26f, 0.18f));
            crafts.Add(c);
        }

        /// <summary>Tir automatique sans ennemi en vue : il vise le panneau de fusion, sinon la porte de son couloir (plus on tire, meilleure elle devient).</summary>
        bool ArcadeShootProps()
        {
            int l = Mathf.Clamp(Mathf.RoundToInt(px / Lane) + 1, 0, 2);
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

        /// <summary>Une porte touchée : +1 (les portes négatives remontent vers le positif), et ×N+1 tous les 4 coups.</summary>
        void GateHit(Gate g, int l)
        {
            if (g.used) return;
            if (g.mul[l]) { if (++g.hits[l] % 4 == 0) g.val[l] = Mathf.Min(g.val[l] + 1, 5); }
            else
            {
                g.val[l] = Mathf.Min(g.val[l] + 1, 12);
                if (g.val[l] == 0) g.val[l] = 1;   // pas de porte « 0 »
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
            if (runTier == 0) { UpgradeWeapon(); return; }
            if (mergeA != null) Destroy(mergeA.gameObject);
            if (mergeB != null) Destroy(mergeB.gameObject);
            mergeA = WeaponIcon(runTier, IconBase + new Vector3(-0.85f, 0f, 0f));
            mergeB = WeaponIcon(runTier, IconBase + new Vector3(0.85f, 0f, 0f));
            mergeT = 0f;
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
            hud.Tag(new Vector3(px, 3.1f, pz), (squad.Count + 1).ToString(), Hud.Yellow, 1.3f);
        }
    }
}
