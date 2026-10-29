using System;
using System.Collections.Generic;
using System.Text;

namespace SamediRouge
{
    /// <summary>
    /// Plans de parcours (code C# pur, sans Unity : jeu, éditeur et ligne de commande).
    ///
    /// Chaque rue a 4 variantes à graine fixe (Équilibrée, Hordes, Obstacles, Pillage). Un plan est une suite de segments
    /// (bande, obstacle, recrues, magasin attaqué…) avec la vie exacte de chaque adversaire et l'emplacement des trousses.
    /// L'intensité de chaque variante (densité, taille des bandes, casqués/blindés) est calibrée par le modèle de difficulté
    /// déterministe (DifficultyModel) : même score effectif visé pour l'équipement de référence, et pas plus de séquences
    /// impossibles que toléré.
    /// </summary>
    public static class Balance
    {
        // ---------------------------------------------------------------- données de design (pas d'équilibrage)

        /// <summary>Boss de chaque rue (0 lanceur, 1 brute, 2 chef de bande, 3 double lancer, 4 charge, 5 mascotte, 6 grosse batte).</summary>
        public static readonly int[] StreetBoss = { 0, 1, 2, 4, 5, 3, 2, 1, 6, 3, 2, 4, 6, 1, 3, 2, 5, 4, 3, 2, 5, 6 };
        /// <summary>Quartier (= thème de décor) de chaque rue : 0 galerie, 1 rue pillée, 2 gare, 3 docks, 4 quartier chic.
        /// À partir du quartier 1 : matraque renforcée, alliés plus nombreux, magasins attaqués.</summary>
        public static readonly int[] StreetTheme = { 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 4 };
        /// <summary>Arme de chaque quartier : 0 armes de base, 1 matraque renforcée / barre de fer.
        /// Le niveau 2 se joue sans (il est dur) : on la gagne en le terminant, pour le niveau 3.</summary>
        public static readonly int[] MapWeaponTier = { 0, 0, 1, 1, 1 };
        public static bool HeavyAt(int street) => MapWeaponTier[Math.Min(MapWeaponTier.Length - 1, MapOf(street))] >= 1;
        /// <summary>Rue dont la première tentative se joue sans arme et sur la variante la plus dure ; l'arme est offerte ensuite.</summary>
        public const int FirstWeaponStreet = 1;
        /// <summary>Variante de la première tentative (les suivantes tournent à partir d'elle).</summary>
        public static int FirstVariant(int street) => street == FirstWeaponStreet ? 2 : 0;
        public static int MapOf(int street) => StreetTheme[Math.Max(0, Math.Min(StreetTheme.Length - 1, street))];
        /// <summary>Première rue où apparaît chaque obstacle.</summary>
        public const int IntroCarts = 0, IntroBoxes = 0, IntroSigns = 1, IntroShoppers = 1, IntroKiosks = 2, IntroThrowers = 2, IntroBarriers = 3;
        public const int Variants = 4;
        public static readonly string[] VariantNames = { "BALANCED", "HORDES", "GAUNTLET", "LOOTING" };

        static BalanceConfig C => BalanceConfig.I;

        // ---------------------------------------------------------------- plan de parcours

        public enum Kind { Pack, Barrier, Obstacle, Recruits, Shoppers, Thrower, Raid, Exit, Ambush, Stars, Gear }
        /// <summary>Obstacle : 0 caddies, 1 cartons, 2 panneau (glisser), 3 kiosque (2 coups).</summary>
        public enum Obs { Carts, Boxes, Sign, Kiosk }

        public class Seg
        {
            public Kind kind;
            public int n;               // adversaires du groupe principal
            public int side;            // adversaires d'une petite bande annexe
            public int recruits;
            public Obs obs;
            public int[] hp;            // vie de chaque adversaire (principal puis annexe)
            public int medkit;          // 0 aucune, 1 dans un couloir libre, 2 en position risquée
            public float spacing;       // mètres jusqu'au segment suivant
            public float z;             // position dans le parcours
        }

        public class Plan
        {
            public int street, variant;
            public float intensity, density = 1f, intrinsic, effectiveRef;
            public int impossibleRef;
            public readonly List<Seg> segs = new List<Seg>();
            /// <summary>Évaluations du modèle de difficulté déjà calculées (par équipement).</summary>
            public readonly Dictionary<int, object> evalCache = new Dictionary<int, object>();
            public int Medkits { get { int n = 0; foreach (Seg s in segs) if (s.medkit > 0) n++; return n; } }
        }

        static readonly Dictionary<int, Plan> cache = new Dictionary<int, Plan>();
        public static void ClearCache() { lock (cache) cache.Clear(); hashCache = null; }

        /// <summary>Plan calibré d'une rue (mis en cache ; sûr entre threads).</summary>
        public static Plan Get(int street, int variant)
        {
            int key = street * 16 + variant;
            lock (cache)
            {
                Plan p;
                if (cache.TryGetValue(key, out p)) return p;
                p = Baked(street, variant) ?? Calibrate(street, variant);
                cache[key] = p;
                return p;
            }
        }

        /// <summary>Calibre tous les plans en arrière-plan (≈0,1 s chacun), pour qu'aucune course n'attende au lancement.</summary>
        public static void PrecomputeAsync(int firstStreet)
        {
            var t = new System.Threading.Thread(() =>
            {
                int n = C.streetLength.Length;
                for (int k = 0; k < n; k++)
                {
                    int st = (firstStreet + k) % n;
                    for (int v = 0; v < Variants; v++) Get(st, v);
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        /// <summary>
        /// Plans pré-calibrés (intensité et densité trouvées hors jeu par Calibrate, voir BalanceConfig.bakedIntensity) :
        /// le calibrage complet prend plusieurs secondes sur téléphone. Utilisés seulement si la configuration n'a pas changé
        /// depuis (empreinte bakedHash) ; sinon on recalibre.
        /// </summary>
        static Plan Baked(int street, int variant)
        {
            int i = street * Variants + variant;
            if (C.bakedIntensity == null || C.bakedDensity == null || i >= C.bakedIntensity.Length || i >= C.bakedDensity.Length) return null;
            if (C.bakedHash != ConfigHash()) return null;
            Plan p = Generate(street, variant, C.bakedIntensity[i], C.bakedDensity[i]);
            DifficultyModel.PlaceMedkits(p);
            return p;
        }

        /// <summary>Recalibre tous les plans et renvoie les lignes à coller dans BalanceConfig (bakedHash, bakedIntensity, bakedDensity).</summary>
        public static string BakeText()
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var si = new StringBuilder();
            var sd = new StringBuilder();
            for (int st = 0; st < C.streetLength.Length; st++)
                for (int v = 0; v < Variants; v++)
                {
                    Plan p = Calibrate(st, v);
                    si.Append(p.intensity.ToString("0.###", ci)).Append("f, ");
                    sd.Append(p.density.ToString("0.###", ci)).Append("f, ");
                }
            return "public string bakedHash = \"" + ConfigHash() + "\";\npublic float[] bakedIntensity = { " + si.ToString().TrimEnd(' ', ',')
                 + " };\npublic float[] bakedDensity = { " + sd.ToString().TrimEnd(' ', ',') + " };\n";
        }

        static string hashCache;
        /// <summary>Empreinte de tout ce qui influence le calibrage (configuration hors plans pré-calibrés, boss et thèmes).</summary>
        public static string ConfigHash()
        {
            if (hashCache != null) return hashCache;
            var sb = new StringBuilder();
            Dump(sb, C);
            Dump(sb, C.coefs);
            foreach (int x in StreetBoss) sb.Append(x).Append(',');
            foreach (int x in StreetTheme) sb.Append(x).Append(',');
            foreach (int x in MapWeaponTier) sb.Append(x).Append(',');
            ulong h = 14695981039346656037UL;
            foreach (char ch in sb.ToString()) { h ^= ch; h *= 1099511628211UL; }
            hashCache = h.ToString("x16");
            return hashCache;
        }
        public static void ResetHash() { hashCache = null; }

        static string Fmt(object e, IFormatProvider ci) => e is float f ? ((long)Math.Round(f * 1000.0)).ToString(ci) : Convert.ToString(e, ci);   // entiers : même texte sur tous les runtimes

        static void Dump(StringBuilder sb, object o)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var fields = o.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            Array.Sort(fields, (a, b) => string.CompareOrdinal(a.Name, b.Name));
            foreach (var f in fields)
            {
                if (f.Name.StartsWith("baked") || f.Name == "coefs" || f.Name == "victoryBonus" || f.Name == "reportLoadouts") continue;
                object v = f.GetValue(o);
                sb.Append(f.Name).Append('=');
                if (v is Array arr) foreach (object e in arr) sb.Append(Fmt(e, ci)).Append(',');
                else sb.Append(Fmt(v, ci));
                sb.Append(';');
            }
        }

        /// <summary>Équipement de référence d'une rue (joueur moyen à son arrivée).</summary>
        public static Loadout RefLoadout(int street)
        {
            return Parse(street < C.refLoadout.Length ? C.refLoadout[street] : "0000");
        }

        /// <summary>
        /// Dichotomie sur l'intensité : score effectif (équipement de référence) le plus proche de la cible, sans dépasser
        /// le nombre de séquences impossibles toléré. Déterministe : un plan ne change pas d'une partie à l'autre.
        /// </summary>
        public static Plan Calibrate(int street, int variant)
        {
            float target = C.targetScore[street];
            int allowed = street < C.allowedImpossible.Length ? C.allowedImpossible[street] : 0;
            Loadout l = RefLoadout(street);
            Loadout acc = Parse(street < C.accessLoadout.Length ? C.accessLoadout[street] : "3333");
            bool gate = CostOf(acc) > CostOf(l);   // la rue doit exiger une amélioration de plus que l'équipement d'arrivée
            Plan best = null;
            float bestErr = float.MaxValue;
            // balayage de l'intensité (le score n'est pas forcément monotone : paliers quand une séquence devient impossible)
            for (int k = 0; k <= 56; k++)
            {
                float it = 0.2f + k * 0.05f;
                Plan p = Generate(street, variant, it);
                DifficultyModel.PlaceMedkits(p);
                DifficultyModel.Result r = DifficultyModel.Evaluate(p, l);
                DifficultyModel.Result ra = DifficultyModel.Evaluate(p, acc);
                bool accessOk = DifficultyModel.Accessible(ra);
                bool refAccessible = DifficultyModel.Accessible(r);
                float err = Math.Abs(r.score - target);
                if (r.impossible > allowed) err += 1000f;
                if (!accessOk) err += 500f;
                if (gate && refAccessible) err += 100f;
                if (err < bestErr) { bestErr = err; best = p; best.effectiveRef = r.score; best.impossibleRef = r.impossible; }
            }
            // réglage fin : le score avance par paliers (tailles de bandes entières) ; on resserre ou on desserre un peu les
            // rangées autour de la meilleure intensité pour viser la cible de plus près
            float it0 = best.intensity;
            for (int a = -2; a <= 2; a++)
                for (int b = 0; b < 6; b++)
                {
                    float it = Math.Max(0.2f, it0 + a * 0.025f), dm = 0.85f + b * 0.06f;
                    Plan p = Generate(street, variant, it, dm);
                    DifficultyModel.PlaceMedkits(p);
                    DifficultyModel.Result r = DifficultyModel.Evaluate(p, l);
                    DifficultyModel.Result ra = DifficultyModel.Evaluate(p, acc);
                    float err = Math.Abs(r.score - target);
                    if (r.impossible > allowed) err += 1000f;
                    if (!DifficultyModel.Accessible(ra)) err += 500f;
                    if (gate && DifficultyModel.Accessible(r)) err += 100f;
                    if (err < bestErr) { bestErr = err; best = p; best.effectiveRef = r.score; best.impossibleRef = r.impossible; }
                }
            best.intrinsic = DifficultyModel.Evaluate(best, new Loadout(0, 0, 0, 0)).score;
            return best;
        }

        /// <summary>Poids des segments selon la variante (identité du niveau conservée, situations différentes).</summary>
        static float[] Weights(int street, int variant)
        {
            bool loot = StreetTheme[street] >= 1;
            //                     Pack  Barr  Obst  Recr  Shop  Throw Raid  Exit  Amb   Stars
            float[] w = { 3.0f, 0.8f, 2.0f, 1.2f, 0.8f, 0.6f, 0f, 0f, 0f, 0.8f };
            if (loot) { w[6] = 1.2f; w[7] = 1.2f; w[8] = 0.6f; }
            switch (variant)
            {
                case 1: w[0] *= 1.7f; w[8] *= 2f; w[2] *= 0.6f; w[1] *= 0.6f; break;            // hordes
                case 2: w[1] *= 2.2f; w[2] *= 1.6f; w[5] *= 2f; w[0] *= 0.7f; break;            // parcours d'obstacles
                case 3: w[6] *= 2f; w[7] *= 2f; w[4] *= 1.5f; w[8] *= 1.5f; if (!loot) { w[5] *= 1.6f; w[4] *= 1.5f; } break; // pillage
            }
            if (street < IntroShoppers) w[4] = 0f;
            if (street < IntroThrowers) w[5] = 0f;
            if (street < IntroBarriers) w[1] *= 0.3f;
            return w;
        }

        static int Pick(Random r, float[] w)
        {
            float sum = 0f;
            foreach (float x in w) sum += x;
            float v = (float)r.NextDouble() * sum;
            for (int i = 0; i < w.Length; i++) { v -= w[i]; if (v <= 0f) return i; }
            return w.Length - 1;
        }

        /// <summary>Vie d'un adversaire : plus l'intensité est haute, plus il y a de casqués et de blindés.</summary>
        static int RollHp(Random r, int street, float intensity = 1f)
        {
            float k = Math.Max(0.5f, Math.Min(6f, intensity));
            float heavy = Math.Min(0.6f, C.heavyShare[street] * k), armored = Math.Min(0.95f - heavy, C.armoredShare[street] * k);
            double v = r.NextDouble();
            if (v < heavy) return C.hpHeavy[street];
            if (v < heavy + armored) return C.hpArmored[street];
            return C.hpNormal[street];
        }

        static int PackSize(Random r, int street, float intensity, float mult = 1f)
        {
            float b = C.packMin[street] + (float)r.NextDouble() * (C.packMax[street] - C.packMin[street]);
            int cap = (int)Math.Ceiling(C.packMax[street] * 1.4f);
            return Math.Max(1, Math.Min(cap, (int)Math.Round(b * (0.5f + 0.5f * intensity) * mult)));
        }

        public static Plan Generate(int street, int variant, float intensity, float densityMul = 1f)
        {
            var p = new Plan { street = street, variant = variant, intensity = intensity, density = densityMul };
            var r = new Random(street * 1009 + variant * 7919 + 4242);
            float[] w = Weights(street, variant);
            float len = C.streetLength[street];
            float z = 50f, nextGear = 55f;
            // la densité suit l'intensité (plus serré quand c'est plus intense), dans des limites jouables
            float spacingK = (float)Math.Max(0.5, Math.Min(2.0, Math.Pow(intensity, -0.6))) * densityMul;
            int idx = 0;
            while (z < len)
            {
                var s = new Seg { z = z };
                // tailles et vies tirées avec une graine propre au segment : changer l'intensité ne change pas la structure
                var q = new Random(street * 1009 + variant * 7919 + idx++ * 131 + 7);
                if (z >= nextGear)
                {
                    nextGear = z + C.gearGap[street];
                    s.kind = Kind.Gear;
                    s.side = q.NextDouble() < 0.7 ? PackSize(q, street, intensity, 0.6f) : 0;
                    s.spacing = 16f;
                }
                else
                {
                    s.kind = (Kind)Pick(r, w);
                    switch (s.kind)
                    {
                        case Kind.Pack: s.n = PackSize(q, street, intensity); s.spacing = 17f; break;
                        case Kind.Barrier: s.obs = street >= IntroSigns && q.NextDouble() < 0.45 ? Obs.Sign : Obs.Carts; s.spacing = 11f; break;
                        case Kind.Obstacle:
                        {
                            var pool = new List<Obs> { Obs.Carts, Obs.Boxes };
                            if (street >= IntroSigns) pool.Add(Obs.Sign);
                            if (street >= IntroKiosks) pool.Add(Obs.Kiosk);
                            s.obs = pool[q.Next(pool.Count)];
                            s.side = PackSize(q, street, intensity, 0.5f);
                            s.spacing = 15f;
                            break;
                        }
                        case Kind.Recruits: s.recruits = q.Next(1, 4); s.side = PackSize(q, street, intensity, 0.6f); s.spacing = 16f; break;
                        case Kind.Shoppers: s.spacing = 13f; break;
                        case Kind.Thrower: s.n = 1 + (intensity > 1.3f && q.NextDouble() < 0.5 ? 1 : 0); s.spacing = 15f; break;
                        case Kind.Raid: s.n = Math.Max(3, PackSize(q, street, intensity, 0.8f)); s.spacing = 17f; break;
                        case Kind.Exit: s.n = Math.Max(2, PackSize(q, street, intensity, 0.7f)); s.spacing = 16f; break;
                        case Kind.Ambush: s.n = Math.Max(2, Math.Min(5, PackSize(q, street, intensity, 0.55f))); s.spacing = 18f; break;
                        default: s.spacing = 13f; break;
                    }
                }
                int total = (s.kind == Kind.Ambush ? s.n * 3 : s.n) + s.side;
                s.hp = new int[total];
                for (int i = 0; i < total; i++) s.hp[i] = RollHp(q, street, intensity);
                s.spacing *= spacingK;
                p.segs.Add(s);
                z += s.spacing;
            }
            return p;
        }

        // ---------------------------------------------------------------- équipement

        public struct Loadout
        {
            public int helmet, vest, weapon, squad;
            public Loadout(int h, int v, int w, int s) { helmet = h; vest = v; weapon = w; squad = s; }
            public override string ToString() { return "Helmet " + helmet + " · Vest " + vest + " · Weapon " + weapon + " · Shield " + squad; }
            public string Short => "H" + helmet + " V" + vest + " W" + weapon + " S" + squad;
        }

        /// <summary>Coût total des améliorations d'un équipement (depuis zéro).</summary>
        public static int CostOf(Loadout l)
        {
            int c = 0;
            int[] lv = { l.helmet, l.vest, l.weapon, l.squad };
            for (int g = 0; g < 4; g++) for (int k = 0; k < lv[g]; k++) c += Math.Max(0, C.Cost(g, k));
            return c;
        }

        public static Loadout Parse(string t)
        {
            int[] v = new int[4];
            for (int i = 0; i < 4 && t != null && i < t.Length; i++) v[i] = Math.Max(0, Math.Min(3, t[i] - '0'));
            return new Loadout(v[0], v[1], v[2], v[3]);
        }
    }
}
