using System;
using System.Collections.Generic;
using System.Text;

namespace SamediRouge
{
    /// <summary>
    /// Modèle de difficulté déterministe (V1), code C# pur.
    ///
    /// 1. Ligne de temps : chaque élément du plan est converti en instant de contact avec la vraie vitesse du jeu
    ///    (v = min(vmax, v0 + d/60)) et la vitesse de rapprochement des adversaires qui chargent.
    /// 2. Demandes : chaque élément exige une action (frapper, sauter, glisser, changer de couloir) d'une durée τ.
    ///    Seule la durée d'un combat dépend de la puissance : coups nécessaires (vie / dégâts, rang par rang), moins la part
    ///    éliminée par les alliés.
    /// 3. Enchaînement : on suit l'occupation du joueur. Pression = (réaction + τ) / temps disponible depuis la fin de
    ///    l'action précédente. Pression > 1 pour toutes les options = séquence impossible avec cette puissance.
    /// 4. Score d'élément : d = b × (1 + k × pression^γ) (+ pénalité si impossible). Bonus : b négatif × accessibilité α.
    /// 5. Agrégation : fenêtres de W s, charge(w) = Σd + λ × charge(w−1), D = (Σ charge^p)^(1/p),
    ///    score = 100 × (1 − e^(−D/D0)). p = 1 serait la somme simple ; p grand ne retient que le pire passage.
    ///
    /// Score intrinsèque = équipement de base ; score effectif = équipement donné. La vie restante n'intervient jamais
    /// dans le score (seule la vie maximale sert à dire si les séquences impossibles bloquent le joueur).
    /// </summary>
    public static class DifficultyModel
    {
        static BalanceConfig C => BalanceConfig.I;
        static DifficultyCoefs K => BalanceConfig.I.coefs;

        public class Demand
        {
            public float t;              // instant de contact (s depuis le départ)
            public string what;          // description lisible
            public int seg = -1;         // segment du plan (-1 : boss)
            public float b;              // score de base
            public float pressure;       // pression de l'option retenue
            public float score;          // score final de l'élément
            public bool impossible, bonus, optional;
            public string action = "";
            // options : durée avant contact, fenêtre d'action, durée totale d'occupation
            public float fightTau = -1f, fightWindow;
            public int n;                // adversaires (combat)
            public float fightPressure = 99f;
            public bool canLane, canJump, canSlide;
        }

        public class Result
        {
            public float score, D, duration, bossStart;
            public int impossible, hitBudget, kills, stars;
            public readonly List<Demand> demands = new List<Demand>();
            public float[] windows;
            public bool Blocked => impossible >= hitBudget;
        }

        // ---------------------------------------------------------------- ligne de temps

        /// <summary>Temps (s) pour parcourir d mètres avec v = min(vmax, v0 + d/60).</summary>
        public static float TimeAt(int street, float d)
        {
            double v0 = C.startSpeed[street], vmax = C.maxSpeed[street];
            double dStar = (vmax - v0) * 60.0;
            if (d <= dStar) return (float)(60.0 * Math.Log((v0 + d / 60.0) / v0));
            return (float)(60.0 * Math.Log(vmax / v0) + (d - dStar) / vmax);
        }

        public static float SpeedAt(int street, float d) => Math.Min(C.maxSpeed[street], C.startSpeed[street] + d / 60f);

        static int Dmg(int street, Balance.Loadout l) => Balance.HeavyAt(street) ? C.heavyDamage[l.weapon] : C.weaponDamage[l.weapon];
        static float Reach(int street, Balance.Loadout l) => Balance.HeavyAt(street) ? K.reachHeavy : l.weapon >= 1 ? K.reachBaton : K.reachFists;

        /// <summary>Points de vie retirés à distance avant le contact : pistolet (niv. 2, tir au tap) ou mitraillette (niv. 3, automatique).
        /// Le tir ne vise que le couloir du joueur : seule une part des tirs compte (gunEfficiency).</summary>
        static float GunShots(int weapon, float v, float charge, float reach)
        {
            if (weapon < 2) return 0f;
            float t = Math.Max(0f, K.gunRange - reach) / Math.Max(1f, v + charge);
            return t / (weapon >= 3 ? K.smgRate : K.pistolRate) * K.gunEfficiency;
        }

        /// <summary>Coups de matraque nécessaires pour une bande (rangs de 3, le coup balaie le rang), moins la part des alliés.</summary>
        static float Swings(int[] hp, int from, int count, int dmg, int mates, int mateDmg, float shots, out string detail)
        {
            float total = 0f, hpSum = 0f;
            int armored = 0;
            // tirs à distance : ils usent les premiers adversaires avant le contact
            var left = new float[count];
            for (int j = 0; j < count; j++) { float take = Math.Min(hp[from + j], shots); left[j] = hp[from + j] - take; shots -= take; }
            for (int r = 0; r < count; r += 3)
            {
                float worst = 0f;
                for (int j = r; j < Math.Min(count, r + 3); j++) { worst = Math.Max(worst, left[j]); hpSum += left[j]; }
                total += (float)Math.Ceiling(worst / dmg - 0.001);
            }
            int minHp = int.MaxValue;
            for (int j = 0; j < count; j++) minHp = Math.Min(minHp, hp[from + j]);
            for (int j = 0; j < count; j++) if (hp[from + j] > minHp) armored++;
            float allyShare = hpSum > 0f ? Math.Min(K.allyShareMax, mates * mateDmg / hpSum) : 0f;
            detail = count + (count > 1 ? " enemies" : " enemy") + (armored > 0 ? " (" + armored + " tough)" : "");
            return total * (1f - allyShare);
        }

        // ---------------------------------------------------------------- évaluation

        /// <summary>
        /// Score effectif d'un équipement. Un joueur mieux équipé peut toujours jouer comme s'il l'était moins
        /// (même choix d'actions) : on garde le meilleur résultat parmi les équipements inférieurs ou égaux, ce qui rend
        /// le score monotone (une amélioration ne rend jamais un parcours plus dur). Résultats mis en cache par plan.
        /// </summary>
        public static Result Evaluate(Balance.Plan p, Balance.Loadout l)
        {
            Result best = null;
            for (int h = 0; h <= l.helmet; h++) for (int v = 0; v <= l.vest; v++) for (int w = 0; w <= l.weapon; w++) for (int s = 0; s <= l.squad; s++)
            {
                Result r = Raw(p, new Balance.Loadout(h, v, w, s));
                if (best == null || r.impossible * 1000f + r.score < best.impossible * 1000f + best.score) best = r;
            }
            // la vie maximale est celle de l'équipement réel
            int budget = Raw(p, l).hitBudget;
            if (best.hitBudget != budget) { best = Clone(best); best.hitBudget = budget; }
            return best;
        }

        static Result Clone(Result r)
        {
            var c = new Result { score = r.score, D = r.D, duration = r.duration, bossStart = r.bossStart, impossible = r.impossible, hitBudget = r.hitBudget, kills = r.kills, stars = r.stars, windows = r.windows };
            c.demands.AddRange(r.demands);
            return c;
        }

        static Result Raw(Balance.Plan p, Balance.Loadout l)
        {
            int key = ((l.helmet * 4 + l.vest) * 4 + l.weapon) * 4 + l.squad;
            lock (p.evalCache)
            {
                object o;
                if (p.evalCache.TryGetValue(key, out o)) return (Result)o;
                Result r = EvaluateRaw(p, l);
                p.evalCache[key] = r;
                return r;
            }
        }

        static Result EvaluateRaw(Balance.Plan p, Balance.Loadout l)
        {
            int st = p.street;
            var res = new Result();
            int dmg = Dmg(st, l);
            float reach = Reach(st, l);
            // alliés : recrutés en course, leur nombre et leur force grandissent avec le quartier
            int mapLv = Math.Min(3, Balance.MapOf(st));
            int maxMates = C.maxSquad[mapLv], mateDmg = C.mateDamage[mapLv];
            int mates = 0;
            int vests = 0, gearStep = 0, gunBonus = l.weapon;
            float shieldUntil = -1f;
            // le bouclier permanent (4e chiffre) encaisse 1 à 3 coups
            res.hitBudget = (C.baseHp + l.vest * C.hpPerVest) / Math.Max(1, C.damagePerHit) + l.squad;

            // 1-2. demandes, dans l'ordre du parcours (les alliés recrutés comptent pour les combats suivants)
            for (int i = 0; i < p.segs.Count; i++)
            {
                Balance.Seg g = p.segs[i];
                float t = TimeAt(st, g.z), v = SpeedAt(st, g.z);
                switch (g.kind)
                {
                    case Balance.Kind.Pack:
                        AddFight(res, i, t, v, g.hp, 0, g.n, dmg, reach, mates, mateDmg, gunBonus, shieldUntil, K.chargeSpeed, true, false, "");
                        break;
                    case Balance.Kind.Exit:
                        AddFight(res, i, t, v, g.hp, 0, g.n, dmg, reach, mates, mateDmg, gunBonus, shieldUntil, K.exitSpeed, true, false, "out of the stores");
                        break;
                    case Balance.Kind.Ambush:
                        // une bande par couloir : impossible d'esquiver, on affronte celle de son couloir
                        AddFight(res, i, t, v, g.hp, 0, g.n, dmg, reach, mates, mateDmg, gunBonus, shieldUntil, K.chargeSpeed, false, false, "ambush, all lanes");
                        break;
                    case Balance.Kind.Raid:
                        AddFight(res, i, t, v, g.hp, 0, g.n, dmg, reach, mates, mateDmg, gunBonus, shieldUntil, 0f, true, true, "store raid (optional)");
                        break;
                    case Balance.Kind.Barrier:
                    {
                        bool sign = g.obs == Balance.Obs.Sign;
                        if (sign && l.helmet >= 2) break;
                        res.demands.Add(new Demand { t = t, seg = i, b = K.bBarrier, what = sign ? "SALE signs across all lanes" : "carts across all lanes", canJump = !sign, canSlide = sign });
                        break;
                    }
                    case Balance.Kind.Obstacle:
                    {
                        Balance.Obs o = g.obs;
                        if (!(o == Balance.Obs.Sign && l.helmet >= 2))
                            res.demands.Add(new Demand
                            {
                                t = t, seg = i, canLane = true, canJump = o != Balance.Obs.Sign, canSlide = o == Balance.Obs.Sign,
                                b = o == Balance.Obs.Kiosk ? K.bKiosk : o == Balance.Obs.Sign ? K.bSlide : K.bJump,
                                what = o == Balance.Obs.Kiosk ? "kiosk" : o == Balance.Obs.Sign ? "SALE sign" : o == Balance.Obs.Boxes ? "boxes" : "carts"
                            });
                        if (g.side > 0) AddFight(res, i, t + 0.35f, v, g.hp, 0, g.side, dmg, reach, mates, mateDmg, gunBonus, shieldUntil, K.chargeSpeed, true, true, "other lane");
                        break;
                    }
                    case Balance.Kind.Recruits:
                        res.demands.Add(new Demand { t = t, seg = i, b = K.bRecruitGrab, canLane = true, optional = true, what = "recruit " + g.recruits });   // rater des recrues ne blesse pas
                        mates = Math.Min(maxMates, mates + g.recruits);
                        if (g.side > 0) AddFight(res, i, t + 0.5f, v, g.hp, 0, g.side, dmg, reach, mates, mateDmg, gunBonus, shieldUntil, K.chargeSpeed, true, true, "other lane");
                        break;
                    case Balance.Kind.Shoppers:
                        res.demands.Add(new Demand { t = t, seg = i, b = K.bShopper, canLane = true, what = "shoppers (do not hit)" });
                        break;
                    case Balance.Kind.Thrower:
                        if (l.helmet < 1)
                            for (int k = 0; k < g.n; k++)
                                res.demands.Add(new Demand { t = t - 1.2f + k * 0.2f, seg = i, b = K.bThrower, canLane = true, what = "thrown box" });
                        break;
                    case Balance.Kind.Gear:
                    {
                        // pièces de course : plaque de gilet (encaisse un coup) puis charge (8 s sans danger au contact)
                        if (gearStep == 0) { vests++; res.demands.Add(new Demand { t = t, seg = i, b = K.bVest, bonus = true, what = "run vest" }); }
                        else if (gearStep == 1) shieldUntil = t + C.shieldDuration;
                        gearStep++;
                        if (g.side > 0) AddFight(res, i, t + 0.4f, v, g.hp, 0, g.side, dmg, reach, mates, mateDmg, gunBonus, shieldUntil, K.chargeSpeed, true, true, "other lane");
                        break;
                    }
                }
                if (g.medkit > 0)
                    res.demands.Add(new Demand { t = t + (g.medkit == 2 ? 0.25f : 0.05f), seg = i, b = K.bMedkit, bonus = true, what = g.medkit == 2 ? "medkit (risky spot)" : "medkit", optional = g.medkit == 2 });
            }
            res.hitBudget += vests;

            // boss : un cycle d'attaque toutes les bossCycle s jusqu'à ce que ses PV tombent
            float len = C.streetLength[st];
            res.bossStart = TimeAt(st, len + 12f);
            {
                int type = Balance.StreetBoss[st];
                float perCycle = K.bossCycle * K.bossHitShare / K.swing;     // coups portés par cycle
                float normal = type == 6 ? 1f : dmg;                         // la grosse batte n'encaisse que 1 hors ouverture
                float dmgCycle = perCycle * normal + mates * 0.35f * mateDmg;
                // pistolet : tirs entre deux attaques ; mitraillette : rafales continues, mais le boss a 3× plus de vie (comme en jeu)
                if (l.weapon >= 3) dmgCycle += K.bossCycle / 0.11f;   // la mitraillette tire sur le boss toutes les 0,11 s
                else if (l.weapon == 2) dmgCycle += K.bossCycle * (1f - K.bossHitShare) / K.pistolRate;
                int cycles = 0;
                float hp = C.bossHp[st] * (l.weapon >= 3 ? 3 : 1);
                while (hp > 0f && cycles < 60)
                {
                    float tc = res.bossStart + cycles * K.bossCycle;
                    bool stun = type == 6 && cycles % 3 == 2;
                    hp -= stun ? perCycle * dmg : dmgCycle;
                    string move = type == 1 || type == 5 && cycles % 2 == 0 ? "shockwave" : type == 4 ? "charge" : type == 6 ? (cycles % 3 == 0 ? "bat sweep (2 lanes)" : cycles % 3 == 1 ? "shockwave" : "calls his gang") : "thrown box";
                    var d = new Demand { t = tc + K.bossCycle * 0.9f, seg = -1, b = K.bBossAttack, what = "boss: " + move };
                    if (move == "shockwave") d.canJump = true;
                    else if (move == "bat sweep (2 lanes)") { d.canJump = true; d.canLane = true; }
                    else if (move == "thrown box" && l.helmet >= 3) d = null;
                    else d.canLane = true;
                    if (d != null) res.demands.Add(d);
                    if ((type == 2 || type == 6) && cycles % 3 == 1)
                    {
                        var pack = new int[3 + Math.Min(3, st / 5)];
                        for (int k = 0; k < pack.Length; k++) pack[k] = C.hpNormal[st];
                        AddFight(res, -1, tc + K.bossCycle, SpeedAt(st, len), pack, 0, pack.Length, dmg, reach, mates, mateDmg, gunBonus, -1f, K.chargeSpeed, true, false, "boss backup");
                    }
                    cycles++;
                }
                res.duration = res.bossStart + cycles * K.bossCycle;
            }

            // 3-4. enchaînement et scores
            res.demands.Sort((a, b) => a.t.CompareTo(b.t));
            // trois « canaux » : le bras (frapper), le corps (sauter / glisser), le couloir. On peut frapper en sautant,
            // pas sauter pendant une glissade. Une séquence est impossible quand le canal requis par toutes les options est
            // encore occupé. L'attention ajoute une pression quand deux décisions se suivent de trop près.
            var busy = new float[] { -10f, -10f, -10f };   // 0 bras, 1 corps, 2 couloir
            float lastDecision = -10f;
            foreach (Demand d in res.demands)
            {
                if (d.bonus)
                {
                    // accessibilité : couloir à changer, position risquée, danger juste après
                    float alpha = 1f - K.rLane;
                    if (d.optional) alpha *= 1f - K.rRisky;
                    foreach (Demand o in res.demands)
                        if (!o.bonus && o.t > d.t && o.t - d.t < K.dangerAfterTime) { alpha *= 1f - K.rDangerAfter; break; }
                    d.pressure = alpha;
                    d.score = d.b * alpha;
                    d.action = "pick up (" + (int)Math.Round(alpha * 100f) + "% accessible)";
                    continue;
                }
                float best = float.MaxValue, bestEnd = d.t;
                int bestCh = 0;
                string bestAct = "";
                float rt = K.reaction;
                if (d.fightTau >= 0f)
                {
                    // on voit la bande arriver de loin : la réaction ne compte que si le bras est encore occupé
                    float start = Math.Max(d.t - d.fightWindow, busy[0] + rt);
                    float avail = d.t - start;
                    float need = Math.Max(K.impactDelay, d.fightTau - K.swing + K.impactDelay);
                    float pr = avail > 0.01f ? need / avail : 99f;
                    d.fightPressure = pr;
                    if (pr < best) { best = pr; bestEnd = start + need; bestCh = 0; bestAct = "fight (" + Math.Round(d.fightTau / K.swing, 1) + " swings)"; }
                }
                if (d.canLane) Option(d, K.laneChange, K.laneChange, busy[2], rt, 2, ref best, ref bestEnd, ref bestCh, ref bestAct, "change lane");
                if (d.canJump) Option(d, K.jumpLead, K.jump, busy[1], rt, 1, ref best, ref bestEnd, ref bestCh, ref bestAct, "jump");
                if (d.canSlide) Option(d, K.slideLead, K.slide, busy[1], rt, 1, ref best, ref bestEnd, ref bestCh, ref bestAct, "slide");

                // attention : décision trop rapprochée de la précédente (jamais impossible à elle seule)
                float gap = d.t - lastDecision;
                float att = gap > 0.01f ? Math.Min(0.95f, K.attention / gap) : 0.95f;
                if (att > best) { best = att; if (bestAct.Length > 0) bestAct += " (right after the previous one)"; }

                d.pressure = best;
                d.action = bestAct;
                d.impossible = best > 1f && !d.optional;
                float pr2 = Math.Min(K.pressureCap, best);
                d.score = Math.Min(K.demandCap, d.b * (1f + K.k * (float)Math.Pow(pr2, K.gamma))) + (d.impossible ? K.impossiblePenalty : 0f);   // un seul passage ne fait pas toute la difficulté
                // élément facultatif (bande d'un autre couloir, magasin attaqué) : il pèse moins et n'occupe le joueur que s'il le gère
                if (d.optional) { d.score *= K.optionalWeight; if (best > 1f) { d.action = "skip"; continue; } }
                lastDecision = d.t;
                if (d.impossible) res.impossible++;
                else busy[bestCh] = Math.Max(busy[bestCh], bestEnd);
            }

            // 5. agrégation par fenêtres
            int nw = Math.Max(1, (int)Math.Ceiling(res.duration / K.window) + 1);
            var raw = new float[nw];
            foreach (Demand d in res.demands)
            {
                int w = Math.Max(0, Math.Min(nw - 1, (int)(d.t / K.window)));
                raw[w] += d.score;
            }
            res.windows = new float[nw];
            double sum = 0;
            float carry = 0f;
            for (int w = 0; w < nw; w++)
            {
                carry = Math.Max(0f, raw[w] + K.lambda * carry);
                res.windows[w] = carry;
                sum += Math.Pow(carry, K.p);
            }
            res.D = (float)Math.Pow(sum, 1.0 / K.p);
            res.score = (float)(100.0 * (1.0 - Math.Exp(-res.D / K.d0)));

            // étoiles d'une course (déterministe) : adversaires des combats réussis, étoiles ramassables
            foreach (Balance.Seg g in p.segs)
            {
                if (g.kind == Balance.Kind.Stars) res.stars += 5;
                else if (g.kind == Balance.Kind.Gear || g.kind == Balance.Kind.Pack || g.kind == Balance.Kind.Shoppers || g.kind == Balance.Kind.Thrower) res.stars += 3;
                else if (g.kind == Balance.Kind.Raid) res.stars += C.storeSaveStars / 2;
            }
            foreach (Demand d in res.demands) if (d.fightTau >= 0f && d.fightPressure <= 1f) res.kills += d.n; // on se bat quand c'est faisable
            res.stars += res.kills * C.lootGuard + res.kills / 5 * C.starsCombo;
            return res;
        }

        static void Option(Demand d, float pre, float total, float busy, float rt, int ch, ref float best, ref float bestEnd, ref int bestCh, ref string bestAct, string name)
        {
            // le geste doit commencer au plus tard « pre » s avant le contact ; on le prépare dès qu'il est visible
            float from = Math.Max(d.t - K.visibility, busy);
            float avail = (d.t - pre) - from;
            float pr = avail > 0.01f ? rt / avail : 99f;
            if (name == "change lane") pr = d.t - from > 0.01f ? (rt + pre) / (d.t - from) : 99f;
            if (pr < best) { best = pr; bestEnd = d.t - pre + total; bestCh = ch; bestAct = name; }
        }

        static void AddFight(Result res, int seg, float t, float v, int[] hp, int from, int count, int dmg, float reach, int mates, int mateDmg,
                             int gunBonus, float shieldUntil, float charge, bool canLane, bool optional, string where)
        {
            if (count <= 0) return;
            string detail;
            float sw = Swings(hp, from, count, dmg, mates, mateDmg, GunShots(gunBonus, v, charge, reach), out detail);
            sw = Math.Max(count > 0 ? 0.5f : 0f, sw);
            bool shield = t < shieldUntil;
            var d = new Demand
            {
                t = t, seg = seg, optional = optional, canLane = canLane, n = count,
                b = K.bSwing * (shield ? 0.2f : sw),
                fightTau = shield ? 0f : sw * K.swing,
                fightWindow = reach / Math.Max(1f, v + charge),
                what = detail + (where.Length > 0 ? ", " + where : "") + (shield ? " [shield]" : "")
            };
            res.demands.Add(d);
        }

        // ---------------------------------------------------------------- trousses (placement structurel)

        /// <summary>
        /// Trousses placées sans simuler la vie : après les fenêtres les plus chargées (équipement de référence), au plus une
        /// par tiers, jamais dans les premiers % ni pendant le boss ; une part en position risquée.
        /// </summary>
        public static void PlaceMedkits(Balance.Plan p)
        {
            foreach (Balance.Seg g in p.segs) g.medkit = 0;
            lock (p.evalCache) p.evalCache.Clear();
            Result r = EvaluateRaw(p, Balance.RefLoadout(p.street));
            float len = C.streetLength[p.street];
            int placed = 0;
            var picks = new List<int>();
            for (int third = 0; third < 3; third++)
            {
                int bestSeg = -1;
                float bestCharge = C.medkitCharge;
                for (int i = 1; i < p.segs.Count - 1; i++)
                {
                    Balance.Seg g = p.segs[i];
                    float frac = g.z / len;
                    if (frac < C.medkitNoneBefore || (int)Math.Min(2, frac * 3f) != third) continue;
                    // charge de la fenêtre juste avant ce segment
                    int w = (int)(TimeAt(p.street, p.segs[i - 1].z) / C.coefs.window);
                    float charge = w < r.windows.Length ? r.windows[w] : 0f;
                    if (charge > bestCharge) { bestCharge = charge; bestSeg = i; }
                }
                if (bestSeg >= 0) picks.Add(bestSeg);
            }
            // au plus medkitsMax trousses : on garde celles qui suivent les passages les plus chargés
            picks.Sort((a, b) => ChargeBefore(p, r, b).CompareTo(ChargeBefore(p, r, a)));
            for (int k = 0; k < picks.Count && k < C.medkitsMax; k++)
            {
                Balance.Seg g = p.segs[picks[k]];
                bool canRisk = g.kind == Balance.Kind.Pack || g.kind == Balance.Kind.Obstacle;
                g.medkit = canRisk && ((placed * 37 + p.variant) % 100) / 100f < C.riskyMedkitShare ? 2 : 1;
                placed++;
            }
            lock (p.evalCache) p.evalCache.Clear();
        }

        static float ChargeBefore(Balance.Plan p, Result r, int i)
        {
            int w = (int)(TimeAt(p.street, p.segs[i - 1].z) / C.coefs.window);
            return w < r.windows.Length ? r.windows[w] : 0f;
        }

        // ---------------------------------------------------------------- diagnostic

        /// <summary>
        /// Parcours « accessible » : coups inévitables qui laissent une marge de vie (accessMargin coups d'avance), et score
        /// sous un seuil qui monte avec la vie (accessPerHit points par coup encaissable au-delà de 5) : avec plus de vie,
        /// on peut se permettre plus d'erreurs. Le gilet et le bouclier rendent donc une rue accessible, comme le casque et l'arme.
        /// </summary>
        public static float AccessCap(int hitBudget) => K.accessible + K.accessPerHit * (hitBudget - 5);
        public static bool Accessible(Result r) => r.score <= AccessCap(r.hitBudget) && r.impossible <= Math.Max(0, r.hitBudget - K.accessMargin);

        public static string Status(Result r)
        {
            if (r.Blocked) return "BLOCKED: unavoidable hits exceed health";
            string s = r.score < K.easy ? "easy" : r.score < K.moderate ? "moderate" : r.score < K.hard ? "hard" : r.score < K.veryHard ? "very hard" : "extreme";
            if (r.impossible > 0) s += ", " + r.impossible + " unavoidable hit" + (r.impossible > 1 ? "s" : "");
            return s;
        }

        static string Clock(float t) { int s = (int)Math.Round(t); return (s / 60).ToString("00") + ":" + (s % 60).ToString("00"); }

        /// <summary>Équipement le moins cher (en étoiles) qui rend le parcours accessible.</summary>
        public static bool MinimalAccessible(Balance.Plan p, out Balance.Loadout best, out Result bestRes)
        {
            best = new Balance.Loadout(3, 3, 3, 3);
            bestRes = null;
            int bestCost = int.MaxValue;
            for (int h = 0; h < 4; h++) for (int v = 0; v < 4; v++) for (int w = 0; w < 4; w++) for (int s = 0; s < 4; s++)
            {
                var l = new Balance.Loadout(h, v, w, s);
                int cost = Balance.CostOf(l);
                if (cost >= bestCost) continue;
                Result r = Evaluate(p, l);
                if (Accessible(r)) { best = l; bestCost = cost; bestRes = r; }
            }
            return bestRes != null;
        }

        /// <summary>Tentatives prévues : il faut gagner de quoi passer de l'équipement d'arrivée à l'équipement minimal accessible.</summary>
        public static float PredictedAttempts(Balance.Plan p, Balance.Loadout arrival, Balance.Loadout minimal, int starsPerRun)
        {
            int missing = Math.Max(0, Balance.CostOf(minimal) - Balance.CostOf(arrival));
            // l'équipement minimal n'est pas forcément « au-dessus » de celui d'arrivée : on compte ce qui manque niveau par niveau
            int need = 0;
            int[] a = { arrival.helmet, arrival.vest, arrival.weapon, arrival.squad }, m = { minimal.helmet, minimal.vest, minimal.weapon, minimal.squad };
            for (int g = 0; g < 4; g++) for (int k = a[g]; k < m[g]; k++) need += Math.Max(0, C.Cost(g, k));
            missing = need;
            return 1f + missing / (float)Math.Max(1, starsPerRun);
        }

        public static string Report(string[] streetNames, string[] mapNames, int[] mapFirst)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Black Friday Rush - difficulty report (deterministic model V1)");
            sb.AppendLine();
            sb.AppendLine("Intrinsic = base player (no upgrade). Effective = with the given upgrades. 0-100: <" + K.easy + " easy, <" + K.moderate + " moderate, <" + K.hard + " hard, <" + K.veryHard + " very hard, above = extreme. Unavoidable hit = a sequence where every option (fight, lane, jump, slide) needs more time than available. Model estimates, to be recalibrated with balance-log.csv.");
            sb.AppendLine();
            sb.AppendLine("## Summary");
            sb.AppendLine();
            sb.AppendLine("| Street | Variant | Duration | Intrinsic | Effective (arrival upgrades) | Unavoidable hits | Medkits | Cheapest accessible upgrades | Predicted attempts |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
            int n = C.streetLength.Length;
            for (int st = 0; st < n; st++)
                for (int v = 0; v < Balance.Variants; v++)
                {
                    Balance.Plan p = Balance.Get(st, v);
                    Balance.Loadout arr = Balance.RefLoadout(st);
                    Result r = Evaluate(p, arr);
                    Balance.Loadout min;
                    Result mr;
                    bool ok = MinimalAccessible(p, out min, out mr);
                    sb.AppendLine("| " + (st + 1) + " " + Name(streetNames, st) + " | " + Balance.VariantNames[v] + " | " + Clock(r.duration) + " | " + Math.Round(p.intrinsic) +
                                  " | " + Math.Round(r.score) + " (" + arr.Short + ") | " + r.impossible + " / " + r.hitBudget + " | " + p.Medkits +
                                  " | " + (ok ? min.Short + " → " + Math.Round(mr.score) : "none") + " | " + (ok ? PredictedAttempts(p, arr, min, Math.Max(1, r.stars)).ToString("0.0") : "-") + " |");
                }

            for (int st = 0; st < n; st++)
            {
                int map = 0;
                for (int m = 0; m < mapFirst.Length; m++) if (st >= mapFirst[m]) map = m;
                for (int v = 0; v < Balance.Variants; v++)
                {
                    Balance.Plan p = Balance.Get(st, v);
                    sb.AppendLine();
                    sb.AppendLine("## " + (mapNames != null && map < mapNames.Length ? mapNames[map] : "MAP " + (map + 1)) + " - street " + (st + 1) + " " + Name(streetNames, st) + " - variant " + (char)('A' + v) + " " + Balance.VariantNames[v]);
                    sb.AppendLine();
                    Result ri = Evaluate(p, new Balance.Loadout(0, 0, 0, 0));
                    sb.AppendLine("- Intrinsic difficulty: **" + Math.Round(ri.score) + "/100** (" + Status(ri) + ")");
                    sb.AppendLine("- " + p.segs.Count + " segments, " + Clock(ri.bossStart) + " before the boss, " + p.Medkits + " medkit(s)");
                    sb.AppendLine();
                    sb.AppendLine("| Upgrades | Effective | Status |");
                    sb.AppendLine("|---|---|---|");
                    foreach (string ls in C.reportLoadouts)
                    {
                        Balance.Loadout l = Balance.Parse(ls);
                        Result r = Evaluate(p, l);
                        sb.AppendLine("| " + l + " | " + Math.Round(r.score) + "/100 | " + Status(r) + " |");
                    }
                    Balance.Loadout arr = Balance.RefLoadout(st);
                    Result ra = Evaluate(p, arr);
                    // amélioration la plus utile depuis l'équipement d'arrivée
                    string bestUp = "";
                    float bestGain = 0f;
                    string[] names = { "helmet", "vest", "weapon", "shield" };
                    for (int g = 0; g < 4; g++)
                    {
                        int[] lv = { arr.helmet, arr.vest, arr.weapon, arr.squad };
                        if (lv[g] >= 3) continue;
                        lv[g]++;
                        Result rr = Evaluate(p, new Balance.Loadout(lv[0], lv[1], lv[2], lv[3]));
                        float gain = ra.score - rr.score + (ra.impossible - rr.impossible) * 5f;
                        if (gain > bestGain) { bestGain = gain; bestUp = names[g] + " +1: " + Math.Round(ra.score) + " → " + Math.Round(rr.score) + (rr.impossible != ra.impossible ? " (unavoidable hits " + ra.impossible + " → " + rr.impossible + ")" : ""); }
                    }
                    sb.AppendLine();
                    sb.AppendLine("Arrival upgrades (" + arr.Short + "): **" + Math.Round(ra.score) + "/100**, " + Status(ra) + ". Most useful next upgrade: " + (bestUp.Length > 0 ? bestUp : "none") + ".");
                    sb.AppendLine();
                    sb.AppendLine("Critical zones (arrival upgrades):");
                    var crit = new List<Demand>(ra.demands.FindAll(d => !d.bonus));
                    crit.Sort((a, b) => b.score.CompareTo(a.score));
                    var top = crit.GetRange(0, Math.Min(5, crit.Count));
                    foreach (Demand d in ra.demands) if (d.impossible && !top.Contains(d)) top.Add(d);
                    top.Sort((a, b) => a.t.CompareTo(b.t));
                    foreach (Demand d in top)
                        sb.AppendLine("- " + Clock(d.t) + " : " + d.what + " - " + d.action + ", pressure " + d.pressure.ToString("0.00") + (d.impossible ? " - **IMPOSSIBLE with these upgrades**" : ""));
                    float heal = 0f;
                    foreach (Demand d in ra.demands) if (d.bonus && d.b == K.bMedkit) heal += d.pressure;
                    sb.AppendLine("- Medkits: " + p.Medkits + " (" + (p.Medkits / Math.Max(0.1f, ri.bossStart / 60f)).ToString("0.0") + " per minute, average accessibility " + (p.Medkits > 0 ? Math.Round(heal / p.Medkits * 100f) + "%" : "-") + "). Boss: " + Clock(ra.bossStart) + ".");
                }
            }
            return sb.ToString();
        }

        static string Name(string[] a, int i) => a != null && i < a.Length ? a[i] : "";
    }
}
