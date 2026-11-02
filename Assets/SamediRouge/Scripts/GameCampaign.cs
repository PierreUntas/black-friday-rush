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
        enum MetaScreen { None, Map, Upgrades, Result, Home }

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
                // arcade : retour à l'accueil (améliorations, puis « glisse pour jouer »), comme Hell Escape
                if (Tuning.Arcade) { ToMenu(); return; }
                // victoire : rue suivante directement (sans repasser par la carte), sauf nouveau niveau débloqué
                int nx = Progress.NextStreet(thief);
                if (!tutorialMode && pendingUnlockMap < 0 && nx >= 0 && Tuning.MapOf(nx) == Tuning.MapOf(streetIdx)) StartStreet(nx);
                else ToMenu();
            };
            hud.onBuyGear = BuyGear;
            hud.onDebugOpen = () => { RefreshDebug(); hud.ShowDebug(true); };
            hud.onDebug = DebugAction;
            hud.onMapTab = OnMapTab;
            hud.onHomePlay = () => { if (st == St.Menu && metaScreen == MetaScreen.Home) StartStreet(HomeStreet()); };
            hud.onHomeBuy = HomeBuy;
            hud.onHomeReset = () =>
            {
                // remise à zéro complète : progression, améliorations, solde
                Progress.ResetAll();
                PlayerPrefs.SetInt(KCrew, 0);
                PlayerPrefs.SetInt(KIncome, 0);
                PlayerPrefs.SetInt(KFirstRun, 0);
                PlayerPrefs.Save();
                selStreet = -1;
                Snd.Play(Snd.I.whoosh, 0.6f);
                ShowHomeScreen();
                hud.ShowTitle(() => { if (st == St.Menu) StartStreet(HomeStreet()); });   // comme une première installation
            };
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
                // arcade : la toute première fois, le tap sur l'écran titre lance directement la partie ;
                // l'accueil (améliorations) n'apparaît qu'après le premier run
                bool first = Tuning.Arcade && PlayerPrefs.GetInt(KFirstRun, 0) == 0;
                hud.ShowTitle(first ? () => { if (st == St.Menu) StartStreet(HomeStreet()); } : (System.Action)null);
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

        /// <summary>Outil de dev : enregistre l'écran (avec l'interface) en images JPG à 30 i/s dans Logs/rec/, pour monter une vidéo.</summary>
        System.Collections.IEnumerator DevRecord()
        {
#if UNITY_EDITOR
            string dir = System.IO.Path.Combine(Application.dataPath, "../Logs/rec");
            try { if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true); System.IO.Directory.CreateDirectory(dir); } catch (System.Exception) { }
            Time.captureFramerate = 30;
            var m = System.Text.RegularExpressions.Regex.Match(devCmd, @"rec\s+(\d+)");
            int frames = (m.Success ? int.Parse(m.Groups[1].Value) : 40) * 30;
            for (int i = 0; i < frames; i++)
            {
                yield return new WaitForEndOfFrame();
                Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
                byte[] jpg = shot.EncodeToJPG(85);
                Destroy(shot);
                try { System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "f" + i.ToString("00000") + ".jpg"), jpg); } catch (System.Exception) { }
            }
            Time.captureFramerate = 0;
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "done.txt"), frames.ToString()); } catch (System.Exception) { }
#else
            yield break;
#endif
        }

        void DevTick(float udt)
        {
#if UNITY_EDITOR
            if (devTrisAt > 0f && Time.realtimeSinceStartup > devTrisAt) { devTrisAt = Time.realtimeSinceStartup + 6f; DevTris(); }
#endif
            if (devCmdT < 0f) return;
#if UNITY_EDITOR
            if (devCmd.Contains("tris")) devTrisAt = Time.realtimeSinceStartup + 5f;
            Perf.On = devCmd.Contains("perf");
            devCenter = devCmd.Contains("center");
            devAuto = devCmd.Contains("auto");
            if (devCmd.Contains("rec")) StartCoroutine(DevRecord());
            if (devCmd.Contains("bossrush")) debugBossRush = true;
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
            if (devCmd.Contains("home")) ToMenu();
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
            // tailles réelles (boîtes englobantes) : héros, un coéquipier, un adversaire
            System.Func<Transform, string> H = t =>
            {
                if (t == null) return "-";
                float lo = 1e9f, hi = -1e9f; var tm = new Mesh();
                foreach (SkinnedMeshRenderer r in t.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!r.enabled) continue;
                    r.BakeMesh(tm, false);
                    Matrix4x4 b = Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one);
                    foreach (Vector3 v in tm.vertices) { float y = b.MultiplyPoint3x4(v).y; lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y); }
                }
                Destroy(tm);
                return hi > lo ? (hi - lo).ToString("0.00") + " m (échelle " + t.localScale.x.ToString("0.000") + ", bas " + lo.ToString("0.000") + " / sol " + t.position.y.ToString("0.000") + ")" : "?";
            };
            Ent foe = ents.Find(x => x.type == ET.Casseur && !x.hit && x.rig != null);
 {
                float la = 1e9f, lb = 1e9f; var tmp = new Mesh();
                foreach (SkinnedMeshRenderer sk in P.root.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!sk.enabled) continue;
                    sk.BakeMesh(tmp, false);
                    Matrix4x4 a = sk.transform.localToWorldMatrix, b = Matrix4x4.TRS(sk.transform.position, sk.transform.rotation, Vector3.one);
                    foreach (Vector3 v in tmp.vertices) { la = Mathf.Min(la, a.MultiplyPoint3x4(v).y); lb = Mathf.Min(lb, b.MultiplyPoint3x4(v).y); }
                }
                Destroy(tmp);
                sb.Append("écart semelle/sol depuis la dernière mesure : min " + Skin3D.DebugMinGap.ToString("0.000") + " max " + Skin3D.DebugMaxGap.ToString("0.000") + "\n");
                Skin3D.DebugMinGap = 9f; Skin3D.DebugMaxGap = -9f;
                sb.Append("maillage réel héros : bas A " + la.ToString("0.000") + " B " + lb.ToString("0.000") + "\n");
            }
            sb.Append("taille héros " + H(P.root) + " · coéquipier " + H(squad.Count > 0 ? squad[0].rig.root : null) + " · adversaire " + H(foe != null ? foe.rig.root : null) + "\n");
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
            if (Tuning.Arcade) return ArcadeEnemyHp();
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

        /// <summary>Arcade : une seule amélioration proposée en fin de course, l'arme de départ.</summary>
        Hud.Track[] ArcadeTracks() => new[] { Tracks()[0] };

        void ShowVictory()
        {
            if (Tuning.Arcade) { PlayerPrefs.SetInt(KFirstRun, 1); PlayerPrefs.Save(); }   // l'accueil s'affichera désormais
            LogRun(true);
            metaScreen = MetaScreen.Result;
            bool already = Progress.Conquered(thief, streetIdx);
            int bonus = already ? Mathf.RoundToInt(Tuning.VictoryBonus[streetIdx] * Tuning.ReplayBonusFactor) : Tuning.VictoryBonus[streetIdx];
            int crewN = squad.Count + 1;
            if (Tuning.Arcade) bonus += crewN * 4 + 40;   // arcade : chaque membre de la bande rapporte au pillage, + prime de victoire
            int total = coins + bonus;
            if (Tuning.Arcade) total = Mathf.RoundToInt(total * IncomeMult);   // amélioration « Revenus » de l'accueil
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
            if (Tuning.Arcade)
            {
                // arcade (retours YSO) : écran de fin minimal — le gain, une amélioration (l'arme de départ), CONTINUER
                hud.minimalResult = true;
                hud.ShowResult(true, L("MEGA STORE RAIDED!", "MEGA STORE PILLÉ !"), "", "", total, Progress.Coins, null, "", L("CONTINUE", "CONTINUER"));
                return;
            }
            hud.minimalResult = false;
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
            if (Tuning.Arcade) { PlayerPrefs.SetInt(KFirstRun, 1); PlayerPrefs.Save(); }   // l'accueil s'affichera désormais
            LogRun(false);
            metaScreen = MetaScreen.Result;
            int keep = Mathf.RoundToInt(coins * Tuning.DefeatKeep);
            Progress.Coins += keep;
            float progress = Mathf.Clamp01((pz - stageStartZ) / StageLen);
            string where = boss != null ? L("the boss", "le boss") : Mathf.RoundToInt(progress * 100f) + L("% of the street", " % de la rue");
            string body = RunSummary() + "\n<color=#FF7A6B>" + cause + L("  ·  reached ", "  ·  atteint : ") + where + "</color>";
            string tip = boss != null && superCount == 0 ? L("Tip: jump, then tap as you land = NINJA STRIKE x3!", "Astuce : saute, puis tape à l'atterrissage = COUP NINJA x3 !") : DefeatTip();
            if (GiveFirstWeapon()) tip = FirstWeaponTip(false);
            hud.minimalResult = Tuning.Arcade;
            if (Tuning.Arcade) { hud.ShowResult(false, L("KNOCKED OUT", "K.-O."), "", "", keep, Progress.Coins, null, ""); return; }
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
            if (Tuning.Arcade) { ShowHomeScreen(); return; }   // arcade : accueil épuré à la place de la carte
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

        // ------------------------------------------------------------------ accueil arcade (à la Hell Escape)

        const string KCrew = "sr_arc_crew", KIncome = "sr_arc_income", KFirstRun = "sr_arc_played";
        const int HomeMax = 10;
        static int ArcLevel(string k) => Mathf.Clamp(PlayerPrefs.GetInt(k, 0), 0, HomeMax);
        static int ArcCost(int lv, int b, int step) => lv >= HomeMax ? -1 : b + step * lv + 10 * lv * lv;
        /// <summary>Membres de la bande dès le départ (amélioration « Bande »).</summary>
        static int StartCrew => ArcLevel(KCrew);
        /// <summary>Multiplicateur des gains (amélioration « Revenus ») : +20 % par niveau.</summary>
        static float IncomeMult => 1f + 0.2f * ArcLevel(KIncome);

        int HomeStreet()
        {
            int nx = Progress.NextStreet(thief);
            return nx >= 0 ? nx : Tuning.Streets - 1;
        }

        // économie arcade : une première partie rapporte de quoi acheter UNE amélioration (la batte, ~250 $)
        // l'arme : on lance déjà la batte au départ, la carte vend donc le pistolet puis la mitraillette « au départ »
        int ArcWeapon => Mathf.Clamp(Progress.GearLevel(thief, Tuning.Weapon), 1, 3);
        int HomeCost(int i) => i == 0 ? ArcCost(ArcLevel(KCrew), 180, 70) : i == 1 ? (ArcWeapon == 1 ? 250 : ArcWeapon == 2 ? 900 : -1) : ArcCost(ArcLevel(KIncome), 220, 90);

        void ShowHomeScreen()
        {
            metaScreen = MetaScreen.Home;
            menuFreeBottom = 760f;
            menuFill = 0.34f;
            menuDrop = 0.1f;
            menuFit = true;   // cadrage sur la taille réelle du personnage (comme la page Équipement)
            campaign = false;
            tutorialMode = false;
            int si = HomeStreet();
            selStreet = si;
            PreviewGear();
            int map = Tuning.MapOf(si);
            int theme = Tuning.MapTheme[map];
            street.SetTheme(theme, pz);
            ThemeLighting(theme);
            Color fog = theme >= 1 ? Tuning.StreetFog[Tuning.MapFirst[map]] : Pal.Fog;
            RenderSettings.fogColor = fog;
            cam.backgroundColor = fog;
            street.SetTint(theme >= 1 ? Tuning.StreetFloor[Tuning.MapFirst[map]] : Color.white);
            RefreshHome();
        }

        void RefreshHome()
        {
            int crew = ArcLevel(KCrew), wl = ArcWeapon, inc = ArcLevel(KIncome);
            string[] names = { L("CREW", "BANDE"), L("WEAPON", "ARME"), L("INCOME", "REVENUS") };
            Sprite[] icons = { GearIcons.Get("gang"), GearIcons.Upgrade(Tuning.Weapon, thief, Mathf.Clamp(wl + 1, 2, 3)), GearIcons.Art("dollar") };
            string[] wNames = { "", L("Pistol at start", "Pistolet au départ"), L("SMG at start", "Mitraillette au départ"), L("Submachine gun", "Mitraillette") };
            string[] lv =
            {
                "+" + crew + L(" at start", " au départ"),
                wNames[Mathf.Clamp(wl, 0, 3)],
                "×" + IncomeMult.ToString("0.0").Replace('.', ',')
            };
            int[] costs = { HomeCost(0), HomeCost(1), HomeCost(2) };
            hud.ShowHome(HomeStreet() + 1, Progress.Coins, names, icons, lv, costs);
        }

        void HomeBuy(int i)
        {
            if (metaScreen != MetaScreen.Home) return;
            int cost = HomeCost(i);
            if (cost < 0) return;
            if (Progress.Coins < cost)
            {
                Snd.Play(Snd.I.hurt, 0.3f);
                return;
            }
            if (i == 1) { Progress.Coins -= cost; Progress.SetGearLevel(thief, Tuning.Weapon, ArcWeapon + 1); }
            else
            {
                Progress.Coins -= cost;
                string k = i == 0 ? KCrew : KIncome;
                PlayerPrefs.SetInt(k, ArcLevel(k) + 1);
                PlayerPrefs.Save();
            }
            Snd.Play(Snd.I.pickup, 0.9f, 0f);
            Haptics.Pulse(40, true);
            PreviewGear();
            P.Land(0.3f);
            P.Flash(new Color(1f, 0.95f, 0.6f), 0.15f);
            RefreshHome();
            hud.PopHome(i);
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
            if (P != null && P.root != null) P.root.localScale = Vector3.one * Rig.Scale * (1f + 0.01f * totalLevel) * (Tuning.Arcade ? CrewScale : 1f);   // l'équipement 3D se voit déjà : croissance discrète
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
