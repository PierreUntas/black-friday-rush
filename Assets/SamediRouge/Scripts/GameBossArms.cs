using UnityEngine;

namespace SamediRouge
{
    /// <summary>
    /// Mode arcade : l'arme du boss décide de son attaque (retour de Pierre, piste 1), et le boss change d'arme
    /// en cours de combat (piste 2) : à 50 % de vie à partir du niveau 3, à 66 % et 33 % pour les boss de fin de quartier.
    /// Sans saut ni glissade en arcade, chaque attaque s'esquive en changeant de couloir, avec un signal rouge avant le coup.
    /// </summary>
    public partial class Game
    {
        /// <summary>0 matraque (charge), 1 matraque lourde (frappe au sol, 2 couloirs), 2 taser (éclair sur 1 couloir),
        /// 3 mitraillette (rafale sur 2 couloirs), 4 bouclier (invulnérable de face, puis ouverture), 5 deux matraques (double balayage).</summary>
        int bossWeapon, bossPhase, bossPhases, bossRepeat, chargeKills;
        float bossGuardT;

        static string[] WeaponNames => new[]
        {
            L("THE BATON", "LA MATRAQUE"), L("THE HEAVY BATON", "LA MATRAQUE LOURDE"), L("THE TASER", "LE TASER"),
            L("THE SMG", "LA MITRAILLETTE"), L("THE RIOT SHIELD", "LE BOUCLIER"), L("TWO BATONS", "DEUX MATRAQUES")
        };
        static string[] WeaponHints => new[]
        {
            L("He charges: change lane!", "Il charge : change de couloir !"),
            L("Ground slam on 2 lanes: go to the third!", "Frappe au sol sur 2 couloirs : va dans le 3e !"),
            L("The lit lane gets zapped: leave it!", "Le couloir allumé va être électrocuté : quitte-le !"),
            L("Bursts on 2 lanes: find the safe one!", "Rafales sur 2 couloirs : trouve le bon !"),
            L("Shield up: wait, then hit the opening!", "Bouclier levé : attends, puis frappe à l'ouverture !"),
            L("Double sweep: dodge twice!", "Double balayage : esquive deux fois !")
        };

        /// <summary>Au début du combat : arme de départ, nombre de phases.</summary>
        void BossArmsStart()
        {
            bossWeapon = ((streetIdx * 5 + bossType) % 6 + 6) % 6;
            bool districtEnd = (streetIdx + 1) % Hud.ZoneLen == 0;
            bossPhases = districtEnd ? 3 : streetIdx >= 2 ? 2 : 1;
            bossPhase = 0;
            bossRepeat = 0;
            bossGuardT = 0f;
            chargeKills = 0;
        }

        void BossArmsAnnounce(Ent e)
        {
            if (e == null) return;
            hud.Float(new Vector3(e.x, 3.4f, e.z), WeaponNames[bossWeapon] + " !", Hud.Bad, true);
            hud.Ticker(WeaponHints[bossWeapon], true);
        }

        /// <summary>Attaque choisie selon l'arme (numéros d'attaque du combat de boss : 4 charge, 5 balayage, 3 double lancer, 0 lancer, 6 éclair, 8 bouclier, 9 frappe au sol).</summary>
        int ArcadeBossMove()
        {
            switch (bossWeapon)
            {
                case 0: return 4;
                case 1: return 9;
                case 2: return 6;
                case 3: return 7;
                case 4: return bossAttacks % 2 == 0 ? 8 : 0;
                default: return 5;
            }
        }

        /// <summary>Perte de voleurs dans les couloirs visés, plafonnée (une attaque ne vide pas toute la foule).</summary>
        void LaneStrike(int l1, int l2, string why)
        {
            int cap = Mathf.Max(2, Mathf.RoundToInt(Crew * 0.15f));
            int lost = 0;
            for (int i = squad.Count - 1; i >= 0 && lost < cap; i--)
            {
                int ml = Mathf.Clamp(Mathf.RoundToInt(squad[i].x / Lane) + 1, 0, 2);
                if ((ml == l1 || ml == l2) && !Defended()) { LoseMate(i, new Vector3(Random.Range(-5f, 5f), 6f, 2f)); lost++; }
                if (st != St.Play) return;
            }
            if (lane == l1 || lane == l2) Hurt(1, why);
        }

        /// <summary>La charge te percute : avant, la bande n'encaissait que 2 voleurs (le 1er boss ne faisait pas mal, retour de Pierre).
        /// Maintenant elle fauche ~18 % de la bande (piétinés compris), avec un « -N » bien visible, et le héros seul perd un cœur.</summary>
        void ChargeHit(Ent e)
        {
            if (inv > 0f || finaleZ > 0f) return;
            int cap = Mathf.Max(3, Mathf.RoundToInt(Crew * 0.18f)) - chargeKills;
            int lost = 0;
            for (int i = squad.Count - 1; i >= 0 && lost < cap; i--)
            {
                LoseMate(i, new Vector3(Random.Range(-5f, 5f), 7f, -4f));
                lost++;
                if (st != St.Play) return;
            }
            int total = lost + chargeKills;
            if (total > 0) hud.Float(new Vector3(px, 2.8f, pz + 0.5f), "-" + total, Hud.Bad, true);
            shake = Mathf.Max(shake, 0.45f);
            hud.Damage(0.35f);
            Haptics.Pulse(60, true);
            Snd.Play(Snd.I.crash, 0.7f);
            lossCause = "boss";
            if (squad.Count == 0) Hurt(1, L(BossName.ToLower() + " ran you over", BossName + " t'a renversé"));
            else inv = 0.6f;
        }

        void ZapLine(int ln, float z0, float z1, Color c)
        {
            float x = (ln - 1) * Lane;
            var go = new GameObject("zap");
            go.transform.SetParent(world, false);
            Art.Part(go.transform, Art.Cube, Art.Glow(c, c * 3f, 0.5f), new Vector3(x, 1.0f, (z0 + z1) * 0.5f), new Vector3(0.22f, 0.22f, Mathf.Abs(z1 - z0)), default, false);
            Art.Decal(go.transform, Art.Particle(Art.SoftDot, new Color(c.r, c.g, c.b, 0.8f)), new Vector3(x, 0.04f, (z0 + z1) * 0.5f), new Vector2(1.6f, Mathf.Abs(z1 - z0)));
            Destroy(go, 0.25f);
        }

        /// <summary>Armé des attaques propres à l'arcade (6 éclair, 7 rafale, 8 bouclier, 9 frappe au sol).</summary>
        bool ArcadeBossWind(Ent e)
        {
            switch (bossMove)
            {
                case 6:
                    bossTargetLane = lane;
                    warn.gameObject.SetActive(true);
                    hud.Float(new Vector3(px, 2.6f, pz + 1f), L("ZAP INCOMING!", "ÉCLAIR !"), Hud.Bad, true);
                    return true;
                case 7:
                case 9:
                {
                    // deux couloirs visés (le tien et un voisin) : le troisième est sûr
                    bossTargetLane = lane;
                    bossSecondLane = lane == 0 ? 1 : lane == 2 ? 1 : (Random.value < 0.5f ? 0 : 2);
                    warn.gameObject.SetActive(true);
                    warn2.gameObject.SetActive(true);
                    if (bossMove == 9) e.rig.Land(0.35f);
                    else e.rig.throwT = 0f;
                    return true;
                }
                case 8:
                    hud.Float(new Vector3(e.x, 3.2f, e.z), L("SHIELD UP!", "BOUCLIER !"), Hud.Info, true);
                    return true;
            }
            return false;
        }

        float ArcadeWindDur(float lvl) => bossMove == 6 ? Mathf.Max(0.75f, 1.0f - lvl * 0.03f)
                                        : bossMove == 7 ? Mathf.Max(0.6f, 0.8f - lvl * 0.03f)
                                        : bossMove == 9 ? Mathf.Max(0.7f, 0.9f - lvl * 0.03f)
                                        : bossMove == 8 ? 0.3f : -1f;

        /// <summary>Déclenchement des attaques propres à l'arcade. Renvoie vrai si l'attaque a été gérée ici.</summary>
        bool ArcadeBossFire(Ent e, float lvl)
        {
            switch (bossMove)
            {
                case 6:
                    ZapLine(bossTargetLane, pz - 2f, e.z, new Color(0.45f, 0.85f, 1f));
                    Snd.PlayPitch(Snd.I.crash, 0.5f, 1.6f);
                    shake = Mathf.Max(shake, 0.25f);
                    LaneStrike(bossTargetLane, -1, L("Zapped by the taser", "Électrocuté par le taser"));
                    e.timer = Mathf.Max(0.9f, 1.5f - lvl * 0.1f);
                    return true;
                case 7:
                    for (int k = 0; k < 2; k++)
                    {
                        Ent b1 = Spawn(ET.Pave, bossTargetLane, e.z - 0.8f - k * 1.2f); b1.y = 1.2f; b1.vz = 10f;
                        Ent b2 = Spawn(ET.Pave, bossSecondLane, e.z - 0.8f - k * 1.2f); b2.y = 1.2f; b2.vz = 10f;
                    }
                    Snd.PlayPitch(Snd.I.whoosh, 0.8f, 1.4f);
                    e.timer = Mathf.Max(0.8f, 1.4f - lvl * 0.1f);
                    return true;
                case 8:
                    bossGuardT = 2.4f;
                    e.timer = 2.4f + 1.3f;
                    return true;
                case 9:
                    Fx.Debris(new Vector3(e.x, 0.3f, e.z));
                    Fx.Dust(new Vector3(e.x, 0.1f, e.z));
                    Fx.Skid(new Vector3((bossTargetLane - 1) * Lane, 0.1f, pz + 1f));
                    Fx.Skid(new Vector3((bossSecondLane - 1) * Lane, 0.1f, pz + 1f));
                    Snd.Play(Snd.I.crash, 0.8f);
                    shake = Mathf.Max(shake, 0.4f);
                    e.rig.Land(0.4f);
                    LaneStrike(bossTargetLane, bossSecondLane, L("Crushed by the ground slam", "Écrasé par la frappe au sol"));
                    e.timer = Mathf.Max(1.1f, 1.8f - lvl * 0.1f);
                    return true;
            }
            return false;
        }

        /// <summary>Chaque image du combat : bouclier levé (bleu) puis ouverture (doré) ; la charge emporte les voleurs sur son passage.</summary>
        void BossArmsTick(Ent e, float dt)
        {
            if (bossGuardT > 0f)
            {
                bossGuardT -= dt;
                e.rig.Flash(new Color(0.4f, 0.7f, 1f), 0.06f);
                if (bossGuardT <= 0f)
                {
                    // il baisse le bouclier : ouverture, chaque coup compte double
                    bossStunT = 1.3f;
                    hud.Float(new Vector3(e.x, 3.2f, e.z), L("OPENING! FIRE!", "OUVERTURE ! TIRE !"), NinjaGold, true);
                }
            }
            if (bossCharging && chargeKills < Mathf.Max(3, Crew / 8))
            {
                for (int i = squad.Count - 1; i >= 0; i--)
                {
                    Mate m = squad[i];
                    if (Mathf.Abs(m.x - e.x) < 0.9f && Mathf.Abs(m.z - e.z) < 0.9f)
                    {
                        LoseMate(i, new Vector3(Random.Range(-4f, 4f), 6f, -3f));
                        chargeKills++;
                        if (st != St.Play) return;
                    }
                }
            }
        }

        /// <summary>Dégâts sur le boss : bloqués par le bouclier, doublés pendant l'ouverture, et changement d'arme aux paliers de vie.</summary>
        int BossArmsDamage(Ent e, int dmg, Vector3 chest)
        {
            if (bossGuardT > 0f)
            {
                if (Random.value < 0.08f) hud.Float(chest + Vector3.up * 1.2f, L("BLOCKED!", "BLOQUÉ !"), Hud.Info, false);
                Fx.Sparkle(chest);
                return 0;
            }
            if (bossStunT > 0f) dmg *= 2;
            return dmg;
        }

        void BossArmsAfterHit(Ent e)
        {
            if (bossPhase >= bossPhases - 1 || e.hp <= 0) return;
            float threshold = bossMaxHp * (1f - (bossPhase + 1f) / bossPhases);
            if (e.hp > threshold) return;
            // changement d'arme : il jette la sienne (ralenti), en sort une autre plus dangereuse
            bossPhase++;
            int old = bossWeapon;
            do bossWeapon = Random.Range(0, 6); while (bossWeapon == old);
            Vector3 hand = new Vector3(e.x + 0.5f, 1.4f, e.z);
            e.rig.ClearBossArm();
            Fx.Debris(hand);
            Fx.Sparkle(hand);
            e.rig.BossArm(bossWeapon, thief);
            bossWind = -1f;
            bossCharging = false;
            bossGuardT = 0f;
            bossRepeat = 0;
            warn.gameObject.SetActive(false);
            warn2.gameObject.SetActive(false);
            e.timer = 1.3f;
            slowMo = Mathf.Max(slowMo, 0.6f);
            zoom = 1f;
            shake = Mathf.Max(shake, 0.3f);
            Snd.Play(Snd.I.siren, 0.6f, 0f);
            hud.Toast(L("NEW WEAPON: ", "IL SORT ") + WeaponNames[bossWeapon] + " !");
            hud.Ticker(WeaponHints[bossWeapon], true);
            e.rig.Flash(Color.white, 0.3f);
            Rhythm.Event(Rhythm.Kind.Spectacle, "boss weapon switch → " + bossWeapon);
        }
    }
}
