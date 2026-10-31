using UnityEngine;

namespace SamediRouge
{
    /// <summary>
    /// Version A « Street Conquest » : carte du quartier (5 rues), course par rue, victoire = rue conquise,
    /// défaite = gains partiels, équipement permanent à 3 niveaux, panneau debug.
    /// Tout passe par Progress (sauvegarde) et Tuning (réglages) ; la course elle-même reste dans Game.
    /// </summary>
    public partial class Game
    {
        enum MetaScreen { None, Map, Upgrades, Result }

        bool campaign, runWon, inputDebug, debugBossRush;
        int viewMap = -1, weaponTier, shieldLv, shieldHits;
        int streetIdx, debugDiff, helmetLv, vestLv, weaponLv, selStreet = -1, pendingMapAnim = -1, debugTutoStep;
        MetaScreen metaScreen = MetaScreen.None;
        float menuFreeBottom = 1720f;
        /// <summary>Part de la zone libre occupée par le personnage (plus petite sur l'écran d'amélioration pour le voir en entier, casque compris).</summary>
        float menuFill = 0.38f;
        /// <summary>Décalage vers le bas du personnage (en hauteur d'écran, -1 à 1) pour le centrer dans la zone libre.</summary>
        float menuDrop;
        /// <summary>Cadrage du menu sur la taille réelle du personnage (page Équipement).</summary>
        bool menuFit;
        int pendingUnlockMap = -1;

        /// <summary>Difficulté effective de la rue (avec le décalage debug).</summary>
        int Diff => Mathf.Clamp(streetIdx + debugDiff, 0, Tuning.Streets - 1);

        /// <summary>Un obstacle introduit à la rue « intro » est-il déjà autorisé ?</summary>
        bool Allowed(int intro) => !campaign || Diff >= intro;

        void InitMeta()
        {
            hud.onStreet = OnStreet;
            hud.onFaction = OnFaction;
            hud.onPlayStreet = () => { if (selStreet >= 0) StartStreet(selStreet); };
            hud.onUpgrades = () => { if (metaScreen == MetaScreen.Result && runWon) StartStreet(streetIdx); else OpenUpgrades(); };
            hud.onTutorial = () => StartTutorial(0);
            hud.onBackToMap = () => { if (st != St.Menu) ToMenu(); else ShowMapScreen(-1); };
            hud.onRetry = () => { if (tutorialMode) StartTutorial(tutStep); else StartStreet(streetIdx); };
            hud.onContinue = () =>
            {
                // victoire : rue suivante directement (sans repasser par la carte), sauf nouveau niveau débloqué
                int nx = Progress.NextStreet(thief);
                if (!tutorialMode && pendingUnlockMap < 0 && nx >= 0 && Tuning.MapOf(nx) == Tuning.MapOf(streetIdx)) StartStreet(nx);
                else ToMenu();
            };
            hud.onBuyGear = BuyGear;
            hud.onDebugOpen = () => { RefreshDebug(); hud.ShowDebug(true); };
            hud.onDebug = DebugAction;
            hud.onMapTab = OnMapTab;
        }

        /// <summary>Premier lancement : tutoriel jouable ; ensuite, directement la carte.</summary>
        /// <summary>Équilibrage : valeurs par défaut de BalanceConfig, écrasées par Resources/SamediRouge/balance.json s'il existe.</summary>
        void LoadBalance()
        {
            BalanceConfig.I = new BalanceConfig();
            TextAsset ta = Resources.Load<TextAsset>("SamediRouge/balance");
            if (ta != null)
            {
                try { JsonUtility.FromJsonOverwrite(ta.text, BalanceConfig.I); }
                catch (System.Exception ex) { Debug.LogWarning("balance.json ignored: " + ex.Message); BalanceConfig.I = new BalanceConfig(); }
            }
            Balance.ClearCache();
            Debug.Log("Samedi Rouge balance: " + (Balance.ConfigHash() == BalanceConfig.I.bakedHash ? "pre-calibrated plans" : "config changed, plans recalibrated at runtime (" + Balance.ConfigHash() + ")"));
        }

        void Boot()
        {
            LoadBalance();
            Balance.PrecomputeAsync(Mathf.Max(0, Progress.NextStreet(Progress.Thief)));
            if (Tuning.OnlyThief) Progress.Thief = true;
            thief = Progress.Thief;
            if (!Progress.TutorialDone && !Tuning.Arcade) StartTutorial(0);   // arcade : pas de tutoriel, le jeu se comprend seul
            else
            {
                ToMenu();
                hud.ShowTitle(null);   // écran titre par-dessus la carte, un tap pour commencer
            }
#if UNITY_EDITOR
            // outil de dev : Logs/sr-screen.txt (« thief », « cop », « upgrades », « tutorial ») ouvre un écran au Play
            try
            {
                string path = System.IO.Path.Combine(Application.dataPath, "../Logs/sr-screen.txt");
                if (System.IO.File.Exists(path)) { devCmd = System.IO.File.ReadAllText(path); devCmdT = 0f; }
            }
            catch (System.Exception) { }
#endif
        }

        string devCmd;
        float devCmdT = -1f;

        /// <summary>Outil de dev (éditeur) : exécute la commande de Logs/sr-screen.txt un peu après le démarrage.</summary>
        float devTrisAt = -1f;

        void DevTick(float udt)
        {
#if UNITY_EDITOR
            if (devTrisAt > 0f && Time.realtimeSinceStartup > devTrisAt) { devTrisAt = Time.realtimeSinceStartup + 6f; DevTris(); }
#endif
            if (devCmdT < 0f) return;
#if UNITY_EDITOR
            if (devCmd.Contains("tris")) devTrisAt = Time.realtimeSinceStartup + 5f;
            Perf.On = devCmd.Contains("perf");
#endif
            devCmdT = -1f; // une seule fois, à la première image
            hud.HideTitleNow();
            if (devCmd.Contains("thief")) OnFaction(true);
            if (devCmd.Contains("cop")) OnFaction(false);
            // « gear 3231 » : niveaux casque / tenue / arme / bouclier du camp choisi (éditeur seulement)
            var gm = System.Text.RegularExpressions.Regex.Match(devCmd, @"gear\s+(\d)(\d)(\d)(\d)");
            if (gm.Success)
                for (int g = 0; g < 4; g++) Progress.SetGearLevel(thief, g, int.Parse(gm.Groups[g + 1].Value));
            if (devCmd.Contains("upgrades")) OpenUpgrades();
            if (devCmd.Contains("tutorial")) StartTutorial(0);
            var sm = System.Text.RegularExpressions.Regex.Match(devCmd, @"street\s+(\d+)");
            if (sm.Success) StartStreet(int.Parse(sm.Groups[1].Value));
        }

#if UNITY_EDITOR
        /// <summary>Outil de dev : triangles visibles regroupés par objet, dans Logs/sr-tris.txt.</summary>
        void DevTris()
        {
            var sum = new System.Collections.Generic.Dictionary<string, int>();
            var cnt = new System.Collections.Generic.Dictionary<string, int>();
            int total = 0;
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.isVisible) continue;
                Mesh m = r is SkinnedMeshRenderer sk ? sk.sharedMesh : r.GetComponent<MeshFilter>() != null ? r.GetComponent<MeshFilter>().sharedMesh : null;
                if (m == null) continue;
                int t = 0;
                for (int i = 0; i < m.subMeshCount; i++) t += (int)m.GetIndexCount(i) / 3;
                string key = (r is SkinnedMeshRenderer ? "skin:" : "") + m.name;
                sum[key] = (sum.TryGetValue(key, out int a) ? a : 0) + t;
                cnt[key] = (cnt.TryGetValue(key, out int c) ? c : 0) + 1;
                total += t;
            }
            var keys = new System.Collections.Generic.List<string>(sum.Keys);
            keys.Sort((x, y) => sum[y].CompareTo(sum[x]));
            var sb = new System.Text.StringBuilder("total " + total + "\n");
            for (int i = 0; i < keys.Count && i < 40; i++) sb.Append(sum[keys[i]] + "\t" + cnt[keys[i]] + "x\t" + keys[i] + "\n");
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/sr-tris.txt"), sb.ToString()); } catch (System.Exception) { }
        }
#endif

        void StartStreet(int i)
        {
            if (i < 0 || i >= Tuning.Streets) i = 0;
            campaign = true;
            tutorialMode = false;
            streetIdx = i;
            StartRun(false);
        }

        /// <summary>Réglages de la course : équipement permanent, cœurs, longueur, décor de la rue.</summary>
        void ApplyRunConfig()
        {
            runWon = false;
            // le tutoriel se joue sans équipement permanent, pour apprendre chaque geste
            helmetLv = tutorialMode ? 0 : Progress.GearLevel(thief, Tuning.Helmet);
            vestLv = tutorialMode ? 0 : Progress.GearLevel(thief, Tuning.Vest);
            weaponLv = tutorialMode ? 0 : Progress.GearLevel(thief, Tuning.Weapon);
            gear[GHelmet] = helmetLv >= 1;
            gear[GBaton] = weaponLv >= 1;
            // armes gagnées : le pistolet (niv. 2) tire de loin au tap, la mitraillette (niv. 3) tire toute seule
            gear[GPistol] = weaponLv >= 2;
            gear[GSmg] = weaponLv >= 3;
            // bouclier gagné : encaisse 1 à 3 coups par course
            shieldLv = tutorialMode ? 0 : Progress.GearLevel(thief, Tuning.Shield);
            shieldHits = shieldLv;
            P.SetLevels(helmetLv, vestLv, weaponLv);
            P.SetPermShield(shieldLv);
            hud.SetRunGear(thief, helmetLv, vestLv, weaponLv, shieldLv);
            ApplyGear();
            MaxHearts = Tuning.MaxHp(vestLv);
            hearts = MaxHearts;
            hud.maxHearts = MaxHearts;
            PowerScale(helmetLv + vestLv + weaponLv);
            // les alliés se recrutent en course ; leur nombre maximum grandit avec le quartier (4, puis 6…)
            squadLv = tutorialMode ? 0 : Mathf.Clamp(Tuning.MapOf(streetIdx), 0, 3);

            int theme = tutorialMode ? 0 : Diff;
            streetIdx = Mathf.Clamp(streetIdx, 0, Tuning.Streets - 1);
            Color fog = Tuning.StreetFog[theme];
            RenderSettings.fogColor = fog;
            cam.backgroundColor = fog;
            int map = tutorialMode ? 0 : Tuning.MapOf(streetIdx);
            street.SetTheme(Tuning.MapTheme[map], pz);
            ThemeLighting(Tuning.MapTheme[map]);
            street.SetTint(Tuning.StreetFloor[theme]);
            // arme de la map : la matraque renforcée à partir de la map 2
            weaponTier = Tuning.MapWeaponTier[map];
            if (weaponTier >= 1) gear[GBaton] = true;
            P.SetHeavy(weaponTier >= 1);
            P.SetHeavyLevel(weaponLv);
            ApplyGear();
            shieldT = 0f;
            // plan d'équilibrage : une variante différente à chaque tentative (jamais plus facile, toutes calibrées pareil)
            plan = null;
            if (!tutorialMode)
            {
                int tries = Progress.Tries(thief, streetIdx);
                runVariant = debugVariant >= 0 ? debugVariant : (tries + Balance.FirstVariant(streetIdx)) % Balance.Variants;
                Progress.AddTry(thief, streetIdx);
                plan = Balance.Get(Diff, runVariant);
                planIdx = 0;
            }
            weaponIntroT = weaponTier >= 1 && !tutorialMode && PlayerPrefs.GetInt("sr_weapon2_seen", 0) == 0 ? 1.8f : -1f;

            if (tutorialMode)
            {
                StageLen = 1e6f;
                speed = Tuning.TutorialSpeed;
            }
            else
            {
                StageLen = debugBossRush ? 30f : Tuning.StreetLength[Diff] * (Tuning.Arcade ? ArcadeLengthMult : 1f);
                speed = Tuning.StartSpeed[Diff];
            }
        }

        int EnemyHp()
        {
            if (!campaign || tutorialMode) return 1;
            BalanceConfig c = BalanceConfig.I;
            float r = Random.value;
            if (r < c.heavyShare[Diff]) return c.hpHeavy[Diff];
            if (r < c.heavyShare[Diff] + c.armoredShare[Diff]) return c.hpArmored[Diff];
            return c.hpNormal[Diff];
        }

        // ------------------------------------------------------------------ fin de course

        /// <summary>Boss vaincu en campagne ou en tutoriel : petite célébration puis écran de résultat.</summary>
        void RunWon()
        {
            runWon = true;
            ClearBolts();
            ents.RemoveAll(x => x.dead);
            tipSlow = false;
            hud.HideTip();
            st = St.End;
            endT = 0f;
            P.anim = Anim.Cheer;
            foreach (Mate m in squad) m.rig.anim = Anim.Cheer;
            hud.Toast(tutorialMode ? L("TRAINING DONE!", "ENTRAÎNEMENT TERMINÉ !") : Tuning.Arcade ? L("JACKPOT!", "JACKPOT !") : L("STREET CONQUERED!", "RUE CONQUISE !"));
            pendingEnd = tutorialMode ? (System.Action)ShowTutorialDone : ShowVictory;
        }

        /// <summary>Résumé d'une ligne de la course (le détail chiffré reste dans le journal).</summary>
        string RunSummary()
        {
            string l = koCount + " KO";
            if (superCount > 0) l += "  ·  " + superCount + L(" perfect", " parfaits");
            if (storesSaved > 0) l += "  ·  " + storesSaved + M(L(" store", " magasin"), " barrage") + (storesSaved > 1 ? "s" : "");
            return l;
        }

        /// <summary>Détail des étoiles gagnées pendant la course.</summary>
        string StarLines()
        {
            string l = L("Enemies knocked out:  +", "Ennemis mis K.-O. :  +") + starsKO + L("\nStars picked up:  +", "\nÉtoiles ramassées :  +") + starsPickup;
            if (starsPerfect > 0) l += L("\nNinja strikes (", "\nCoups ninja (") + superCount + "):  +" + starsPerfect;
            if (starsStores > 0) l += "\n" + M(L("Stores saved", "Magasins sauvés"), L("Barrages broken", "Barrages enfoncés")) + " (" + storesSaved + "):  +" + starsStores;
            if (starsCombo > 0) l += L("\nCombos:  +", "\nCombos :  +") + starsCombo;
            return l;
        }

        /// <summary>Les 3 améliorations les moins chères encore disponibles, à acheter depuis le bilan.</summary>
        /// <summary>Les deux axes de progression du bilan : POWER (arme) et CREW (nombre d'alliés).</summary>
        Hud.Track[] Tracks()
        {
            int[] gears = { Tuning.Weapon, Tuning.Shield };
            string[] names = { L("WEAPON", "ARME"), L("SHIELD", "BOUCLIER") };
            var t = new Hud.Track[2];
            for (int i = 0; i < 2; i++)
            {
                int g = gears[i], lv = Progress.GearLevel(thief, g), cost = Progress.NextCost(thief, g);
                string next = cost < 0 ? Tuning.GearEffect[g, lv] : Tuning.GearEffect[g, lv + 1];
                t[i] = new Hud.Track
                {
                    gear = g, name = names[i], cost = cost,
                    // on gagne un objet : son nom, pas un niveau
                    level = cost < 0 ? L("COMPLETE", "COMPLET") : Tuning.ItemName(g, thief, lv + 1),
                    effect = (cost < 0 ? Tuning.ItemName(g, thief, lv) + " · " : L("Next: ", "Ensuite : ")) + next
                };
            }
            return t;
        }

        void ShowVictory()
        {
            LogRun(true);
            metaScreen = MetaScreen.Result;
            bool already = Progress.Conquered(thief, streetIdx);
            int bonus = already ? Mathf.RoundToInt(Tuning.VictoryBonus[streetIdx] * Tuning.ReplayBonusFactor) : Tuning.VictoryBonus[streetIdx];
            int crewN = squad.Count + 1;
            if (Tuning.Arcade) bonus += crewN * 10;   // arcade : chaque membre de la bande rapporte au pillage
            int total = coins + bonus;
            Progress.Coins += total;
            var openBefore = new bool[Tuning.Maps];
            for (int m = 0; m < Tuning.Maps; m++) openBefore[m] = Progress.MapUnlocked(thief, m);
            if (!already) Progress.SetConquered(thief, streetIdx, true);
            pendingUnlockMap = -1;
            for (int m = 0; m < Tuning.Maps; m++) if (!openBefore[m] && Progress.MapUnlocked(thief, m)) pendingUnlockMap = m;
            pendingMapAnim = already ? -1 : streetIdx;
            int n = Progress.ConqueredCount(thief);
            int next = Progress.NextStreet(thief);
            selStreet = next >= 0 ? next : streetIdx;
            string body = Tuning.Arcade ? L("Crew ×", "Bande ×") + crewN + "   ·   " + koCount + " KO" : RunSummary() + "  ·  " + (already ? L("replay bonus +", "bonus de rejeu +") : L("victory bonus +", "bonus de victoire +")) + bonus;
            // fin du niveau qui donne l'arme renforcée : c'est la récompense du niveau dur
            bool weaponWon = pendingUnlockMap >= 1 && Tuning.MapWeaponTier[pendingUnlockMap] >= 1 && Tuning.MapWeaponTier[pendingUnlockMap - 1] == 0;
            string tip = weaponWon ? M(L("YOU WIN THE REINFORCED BATON! Level " + (pendingUnlockMap + 1) + " unlocked", "TU GAGNES LA MATRAQUE RENFORCÉE ! Niveau " + (pendingUnlockMap + 1) + " débloqué"),
                                       L("YOU WIN THE IRON BAR! Level " + (pendingUnlockMap + 1) + " unlocked", "TU GAGNES LA BARRE DE FER ! Niveau " + (pendingUnlockMap + 1) + " débloqué"))
                       : pendingUnlockMap >= 0 ? L("NEW LEVEL UNLOCKED: " + Tuning.MapNames[pendingUnlockMap] + "!", "NOUVEAU NIVEAU DÉBLOQUÉ : " + Tuning.MapNames[pendingUnlockMap] + " !")
                       : next >= 0 ? L("Next: ", "Ensuite : ") + Tuning.StreetNames[next] + L("  (level ", "  (niveau ") + (Tuning.MapOf(next) + 1) + ")" : L("Every street is yours!", "Toutes les rues sont à toi !");
            if (GiveFirstWeapon()) tip = FirstWeaponTip(true);
            bool sameMap = next >= 0 && Tuning.MapOf(next) == Tuning.MapOf(streetIdx);
            string cont = pendingUnlockMap >= 0 ? L("CONTINUE - LEVEL ", "CONTINUER - NIVEAU ") + (pendingUnlockMap + 1) : sameMap ? L("NEXT: ", "SUIVANTE : ") + Tuning.StreetNames[next] : L("CONTINUE", "CONTINUER");
            hud.ShowResult(true, Tuning.Arcade && pendingUnlockMap < 0 ? L("MEGA STORE RAIDED!", "MEGA STORE PILLÉ !") : pendingUnlockMap >= 0 ? L("LEVEL " + (pendingUnlockMap + 1) + " UNLOCKED!", "NIVEAU " + (pendingUnlockMap + 1) + " DÉBLOQUÉ !") : already ? L("STREET HELD!", "RUE DÉFENDUE !") : L("STREET CONQUERED!", "RUE CONQUISE !"),
                           Tuning.StreetNames[streetIdx], body, total, Progress.Coins, Tracks(), tip, cont);
        }

        /// <summary>
        /// Journal des vraies parties (pour recaler le modèle d'équilibrage) : une ligne CSV par course dans
        /// balance-log.csv (dossier de données persistantes de l'appli).
        /// </summary>
        void LogRun(bool win)
        {
            if (tutorialMode || !campaign) return;
            try
            {
                string path = System.IO.Path.Combine(Application.persistentDataPath, "balance-log.csv");
                if (!System.IO.File.Exists(path))
                    System.IO.File.AppendAllText(path, "time,faction,street,variant,try,helmet,vest,weapon,squad,result,progress,duration_s,hp_25,hp_50,hp_75,hp_at_boss,hp_end,medkits,stars,perfects,cause\n");
                float progress = Mathf.Clamp01((pz - stageStartZ) / Mathf.Max(1f, StageLen));
                string line = System.DateTime.Now.ToString("s") + "," + (thief ? "thief" : "guard") + "," + (streetIdx + 1) + ","
                    + (plan != null ? Balance.VariantNames[runVariant] : "-") + "," + Progress.Tries(thief, streetIdx) + ","
                    + helmetLv + "," + vestLv + "," + weaponLv + "," + squadLv + "," + (win ? "win" : "loss") + ","
                    + progress.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "," + Mathf.RoundToInt(runT) + "," + hp25 + "," + hp50 + "," + hp75 + ","
                    + hpAtBoss + "," + Mathf.Max(0, hearts) + ","
                    + medkitsTaken + "," + coins + "," + superCount + "," + (win ? "" : cause.Replace(",", " ")) + "\n";
                System.IO.File.AppendAllText(path, line);
                // coups reçus de la course, un par ligne (balance-hits.csv)
                string hits = System.IO.Path.Combine(Application.persistentDataPath, "balance-hits.csv");
                if (!System.IO.File.Exists(hits)) System.IO.File.AppendAllText(hits, "run,street,variant,time_s,distance_m,phase,cause,hp_after\n");
                string id = System.DateTime.Now.ToString("s") + "," + (streetIdx + 1) + "," + (plan != null ? Balance.VariantNames[runVariant] : "-") + ",";
                foreach (string h in hitLog.ToString().Split('\n')) if (h.Length > 0) System.IO.File.AppendAllText(hits, id + h + "\n");
            }
            catch (System.Exception) { }
        }

        void ShowDefeat()
        {
            LogRun(false);
            metaScreen = MetaScreen.Result;
            int keep = Mathf.RoundToInt(coins * Tuning.DefeatKeep);
            Progress.Coins += keep;
            float progress = Mathf.Clamp01((pz - stageStartZ) / StageLen);
            string where = boss != null ? L("the boss", "le boss") : Mathf.RoundToInt(progress * 100f) + L("% of the street", " % de la rue");
            string body = RunSummary() + "\n<color=#FF7A6B>" + cause + L("  ·  reached ", "  ·  atteint : ") + where + "</color>";
            string tip = boss != null && superCount == 0 ? L("Tip: jump, then tap as you land = NINJA STRIKE x3!", "Astuce : saute, puis tape à l'atterrissage = COUP NINJA x3 !") : DefeatTip();
            if (GiveFirstWeapon()) tip = FirstWeaponTip(false);
            hud.ShowResult(false, L("KNOCKED OUT", "K.-O."), Tuning.StreetNames[streetIdx], body, keep, Progress.Coins, Tracks(), tip);
        }

        /// <summary>
        /// Première arme (batte / matraque) : offerte après la première tentative sur la rue où elle devient indispensable
        /// (Balance.FirstWeaponStreet). On y perd d'habitude la première fois ; avec l'arme, on passe au coup suivant.
        /// </summary>
        bool GiveFirstWeapon()
        {
            if (tutorialMode || !campaign || streetIdx != Balance.FirstWeaponStreet) return false;
            if (Progress.GearLevel(thief, Tuning.Weapon) > 0) return false;
            Progress.SetGearLevel(thief, Tuning.Weapon, 1);
            P.SetLevels(Progress.GearLevel(thief, 0), Progress.GearLevel(thief, 1), 1);
            Snd.Play(Snd.I.pickup, 0.8f, 0f);
            Haptics.Pulse(60, true);
            return true;
        }

        string FirstWeaponTip(bool won)
        {
            string item = Tuning.ItemName(Tuning.Weapon, thief, 1);
            return won ? L("NEW GEAR: " + item + "! Wide swings, it changes everything", "NOUVEL ÉQUIPEMENT : " + item + " ! Coups larges, ça change tout")
                       : L("NEW GEAR: " + item + "! Try again, it changes everything", "NOUVEL ÉQUIPEMENT : " + item + " ! Retente : ça change tout");
        }

        /// <summary>Conseil d'amélioration adapté à la cause de la défaite.</summary>
        string DefeatTip()
        {
            string c = cause.ToLowerInvariant();
            int g = Tuning.Vest;
            // pas encore d'arme : c'est elle qui change tout (la batte / la matraque)
            if (Progress.GearLevel(thief, Tuning.Weapon) == 0) g = Tuning.Weapon;
            else if (c.Contains("flying box") || c.Contains("sign") || c.Contains("crate") || c.Contains("carton") || c.Contains("panneau") || c.Contains("caisse")) g = Tuning.Helmet;
            else if (boss != null) g = Tuning.Weapon;
            if (Progress.NextCost(thief, g) < 0)
            {
                if (c.Contains("shopper") || c.Contains("client")) return L("Tip: never hit the shoppers, change lane early.", "Astuce : ne frappe jamais les clients, change de couloir tôt.");
                return L("Tip: grab the green medkits to heal.", "Astuce : ramasse les trousses de soin vertes pour te soigner.");
            }
            return L("Tip: get the ", "Astuce : gagne l'objet ") + Tuning.ItemName(g, thief, Progress.GearLevel(thief, g) + 1) + " - " + Tuning.GearEffect[g, Progress.GearLevel(thief, g) + 1].ToLowerInvariant();
        }

        string UpgradeHint()
        {
            int coinsNow = Progress.Coins;
            for (int g = 0; g < Tuning.GearCount; g++)
            {
                int c = Progress.NextCost(thief, g);
                if (c >= 0 && coinsNow >= c) return L("You can get: ", "Tu peux gagner : ") + Tuning.ItemName(g, thief, Progress.GearLevel(thief, g) + 1) + " !";
            }
            return "";
        }

        // ------------------------------------------------------------------ carte et améliorations

        string[] BossNamesForMap()
        {
            var names = new string[Tuning.Streets];
            for (int i = 0; i < Tuning.Streets; i++) names[i] = (thief ? BossThief : BossGuard)[Tuning.StreetBoss[i]];
            return names;
        }

        void ShowMapScreen(int animate)
        {
            metaScreen = MetaScreen.Map;
            menuFreeBottom = 1760f;
            menuFill = 0.38f;
            menuDrop = 0f;
            menuFit = false;
            campaign = false;
            tutorialMode = false;
            var conq = new bool[Tuning.Streets];
            for (int i = 0; i < Tuning.Streets; i++) conq[i] = Progress.Conquered(thief, i);
            int next = Progress.NextStreet(thief);
            if (selStreet < 0 || !Progress.Unlocked(thief, selStreet)) selStreet = next >= 0 ? next : 0;
            // la map affichée : celle de la rue sélectionnée (ou de la rue qu'on vient de conquérir)
            if (animate >= 0) viewMap = Tuning.MapOf(animate);
            else if (viewMap < 0) viewMap = Tuning.MapOf(selStreet);
            var open = new bool[Tuning.Maps];
            for (int m = 0; m < Tuning.Maps; m++) open[m] = Progress.MapUnlocked(thief, m);
            if (Tuning.MapOf(selStreet) != viewMap) selStreet = FirstPlayable(viewMap);
            PreviewGear();
            if (Tuning.MapWeaponTier[viewMap] >= 1) { gear[GBaton] = true; P.SetHeavy(true); P.SetHeavyLevel(Progress.GearLevel(thief, Tuning.Weapon)); ApplyGear(); }
            else P.SetHeavy(false);
            hud.ShowMap(thief, viewMap, open, conq, next, selStreet, Progress.Coins, BossNamesForMap(), animate, pendingUnlockMap);
            pendingUnlockMap = -1;
            // décor de fond : celui de la map affichée
            int theme = Tuning.MapTheme[viewMap];
            street.SetTheme(theme, pz);
            ThemeLighting(theme);
            Color fog = theme >= 1 ? Tuning.StreetFog[Tuning.MapFirst[viewMap]] : Pal.Fog;
            RenderSettings.fogColor = fog;
            cam.backgroundColor = fog;
            street.SetTint(theme >= 1 ? Tuning.StreetFloor[Tuning.MapFirst[viewMap]] : Color.white);
        }

        /// <summary>Première rue jouable d'une map (prochaine cible, sinon la première conquise), -1 si la map est fermée.</summary>
        int FirstPlayable(int map)
        {
            int first = Tuning.MapFirst[map];
            for (int i = 0; i < Tuning.MapCount[map]; i++) if (Progress.Unlocked(thief, first + i) && !Progress.Conquered(thief, first + i)) return first + i;
            for (int i = 0; i < Tuning.MapCount[map]; i++) if (Progress.Unlocked(thief, first + i)) return first + i;
            return -1;
        }

        void OnMapTab(int m)
        {
            if (!Progress.MapUnlocked(thief, m))
            {
                hud.InputDebug("Conquer all of " + Tuning.MapNames[m - 1] + " first!");
                Snd.Play(Snd.I.hurt, 0.3f);
                return;
            }
            if (m == viewMap) return;
            viewMap = m;
            selStreet = FirstPlayable(m);
            Snd.PlayPitch(Snd.I.whoosh, 0.5f, 1.2f);
            ShowMapScreen(-1);
        }

        void OnStreet(int i)
        {
            if (!Progress.Unlocked(thief, i))
            {
                hud.InputDebug("Conquer " + Tuning.StreetNames[Progress.NextStreet(thief)] + " first!");
                Snd.Play(Snd.I.hurt, 0.3f);
                return;
            }
            selStreet = i;
            hud.SelectStreet(i);
        }

        void OnFaction(bool t)
        {
            if (Tuning.OnlyThief) t = true;
            if (t == thief) return;
            thief = t;
            Progress.Thief = t;
            selStreet = -1;
            ResetPlayerRig();
            P.SetLevels(Progress.GearLevel(thief, 0), Progress.GearLevel(thief, 1), Progress.GearLevel(thief, 2));
            ShowMapScreen(-1);
        }

        void OpenUpgrades()
        {
            if (st != St.Menu) ToMenu();
            metaScreen = MetaScreen.Upgrades;
            menuFreeBottom = 1140f;
            menuFill = 0.35f;   // cadrage sur la taille réelle : marge en haut (casque) et en bas
            menuDrop = 0.1f;    // personnage un peu plus bas, plus près des cartes
            menuFit = true;
            PreviewGear();
            RefreshUpgrades();
        }

        /// <summary>Le personnage du menu porte l'équipement permanent acheté.</summary>
        void PreviewGear()
        {
            int h = Progress.GearLevel(thief, 0), v = Progress.GearLevel(thief, 1), w = Progress.GearLevel(thief, 2);
            P.SetLevels(h, v, w);
            PowerScale(h + v + w);
            for (int i = 0; i < gear.Length; i++) gear[i] = false;
            gear[GHelmet] = h >= 1;
            gear[GBaton] = w >= 1;
            gear[GPistol] = w >= 2;
            gear[GSmg] = w >= 3;
            ApplyGear();
            P.SetPermShield(Progress.GearLevel(thief, Tuning.Shield));
        }

        /// <summary>Le personnage grandit un peu avec sa puissance (niveaux cumulés), pour une différence visible.</summary>
        void PowerScale(int totalLevel)
        {
            if (P != null && P.root != null) P.root.localScale = Vector3.one * Rig.Scale * (1f + 0.01f * totalLevel);   // l'équipement 3D se voit déjà : croissance discrète
        }

        void RefreshUpgrades()
        {
            var lv = new int[Tuning.GearCount];
            var cost = new int[Tuning.GearCount];
            for (int i = 0; i < Tuning.GearCount; i++) { lv[i] = Progress.GearLevel(thief, i); cost[i] = Progress.NextCost(thief, i); }
            hud.ShowUpgrades(Progress.Coins, lv, cost);
        }

        void BuyGear(int g)
        {
            if (Progress.NextCost(thief, g) < 0) return;
            if (!Progress.Buy(thief, g))
            {
                hud.InputDebug("NOT ENOUGH " + Tuning.Cur);
                Snd.Play(Snd.I.hurt, 0.3f);
                return;
            }
            Snd.Play(Snd.I.pickup, 0.9f, 0f);
            Haptics.Pulse(40, true);
            hud.Flash(0.3f);
            if (metaScreen == MetaScreen.Result)
            {
                // achat depuis le bilan : on reste sur l'écran, le solde et les offres se mettent à jour
                hud.UpdateResultBank(Progress.Coins, Tracks());
                hud.InputDebug(Tuning.GearNames[g] + " LV " + Progress.GearLevel(thief, g) + "!");
                return;
            }
            PreviewGear();
            P.Land(0.3f);
            P.Flash(new Color(1f, 0.95f, 0.6f), 0.15f);
            RefreshUpgrades();
            hud.PopGear(g);
        }

        // ------------------------------------------------------------------ debug

        void RefreshDebug()
        {
            hud.SetDebugLabels(new[]
            {
                "+500 " + Tuning.Cur, "RESET ALL",
                "HELMET LV " + Progress.GearLevel(thief, 0), "VEST LV " + Progress.GearLevel(thief, 1),
                "WEAPON LV " + Progress.GearLevel(thief, 2), "CONQUER NEXT",
                "UNCONQUER ALL", "DIFFICULTY +" + debugDiff,
                "TUTO STEP " + (debugTutoStep + 1), "PLAY TUTO STEP",
                "INPUT DEBUG " + (inputDebug ? "ON" : "OFF"), "HAPTICS " + (Haptics.Enabled ? "ON" : "OFF"),
                "BOSS RUSH " + (debugBossRush ? "ON" : "OFF"), "TUTO DONE " + (Progress.TutorialDone ? "YES" : "NO"),
                "PLAY TUTORIAL", "CLOSE",
                "SHIELD LV " + Progress.GearLevel(thief, Tuning.Shield), "LAYOUT " + (debugVariant < 0 ? "AUTO" : Balance.VariantNames[debugVariant]),
                "RELOAD BALANCE", "BALANCE REPORT (LOG)"
            });
            hud.SetDebugInfo(BalanceInfo());
        }

        /// <summary>Prévision du modèle de difficulté pour la rue sélectionnée, avec tes améliorations actuelles.</summary>
        string BalanceInfo()
        {
            int s = selStreet >= 0 ? selStreet : Mathf.Max(0, Progress.NextStreet(thief));
            int v = debugVariant >= 0 ? debugVariant : Progress.Tries(thief, s) % Balance.Variants;
            Balance.Plan p = Balance.Get(s, v);
            var l = new Balance.Loadout(Progress.GearLevel(thief, 0), Progress.GearLevel(thief, 1), Progress.GearLevel(thief, 2), Progress.GearLevel(thief, 3));
            DifficultyModel.Result r = DifficultyModel.Evaluate(p, l);
            Balance.Loadout min;
            DifficultyModel.Result mr;
            bool ok = DifficultyModel.MinimalAccessible(p, out min, out mr);
            return Tuning.StreetNames[s] + " · next layout " + Balance.VariantNames[v] + " (try " + (Progress.Tries(thief, s) + 1) + ")\n"
                 + "Intrinsic " + Mathf.RoundToInt(p.intrinsic) + " · with your upgrades " + Mathf.RoundToInt(r.score) + " (" + DifficultyModel.Status(r) + ")\n"
                 + (ok ? "Accessible from " + min.Short : "No upgrade makes it accessible!") + " · medkits " + p.Medkits;
        }

        void DebugAction(int id)
        {
            switch (id)
            {
                case 0: Progress.Coins += 500; break;
                case 1: Progress.ResetAll(); selStreet = -1; break;
                case 2: case 3: case 4:
                {
                    int g = id - 2;
                    Progress.SetGearLevel(thief, g, (Progress.GearLevel(thief, g) + 1) % (Tuning.MaxLevel + 1));
                    P.SetLevels(Progress.GearLevel(thief, 0), Progress.GearLevel(thief, 1), Progress.GearLevel(thief, 2));
                    break;
                }
                case 5: { int n = Progress.NextStreet(thief); if (n >= 0) Progress.SetConquered(thief, n, true); break; }
                case 6: for (int i = 0; i < Tuning.Streets; i++) Progress.SetConquered(thief, i, false); selStreet = -1; break;
                case 7: debugDiff = (debugDiff + 1) % Tuning.Streets; break;
                case 16:
                    Progress.SetGearLevel(thief, Tuning.Squad, (Progress.GearLevel(thief, Tuning.Squad) + 1) % (Tuning.MaxLevel + 1));
                    break;
                case 17: debugVariant = debugVariant >= Balance.Variants - 1 ? -1 : debugVariant + 1; break;
                case 18: LoadBalance(); hud.InputDebug("Balance reloaded"); break;
                case 19:
                {
                    string rep = DifficultyModel.Report(Tuning.StreetNames, Tuning.MapNames, Tuning.MapFirst);
                    Debug.Log(rep);
                    hud.InputDebug("Report written to the log");
                    break;
                }
                case 8: debugTutoStep = (debugTutoStep + 1) % TutSteps; break;
                case 9: hud.ShowDebug(false); StartTutorial(debugTutoStep); return;
                case 10: inputDebug = !inputDebug; break;
                case 11: Haptics.Enabled = !Haptics.Enabled; break;
                case 12: debugBossRush = !debugBossRush; break;
                case 13: Progress.TutorialDone = !Progress.TutorialDone; break;
                case 14: hud.ShowDebug(false); StartTutorial(0); return;
                default: hud.ShowDebug(false); ShowMapScreen(-1); return;
            }
            RefreshDebug();
            ShowMapScreen(-1);
            hud.ShowDebug(true);
        }
    }
}
