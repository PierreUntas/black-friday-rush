using UnityEngine;

namespace SamediRouge
{
    /// <summary>
    /// Tutoriel jouable (≈30 s) : une consigne à la fois, geste animé, ralenti à l'approche,
    /// l'étape se rejoue tant qu'elle n'est pas réussie (aucun cœur perdu).
    /// Étapes : 0 changer de couloir, 1 sauter, 2 glisser, 3 attaquer, 4 éviter les clients, 5 recruter 3 alliés, 6 mini-boss.
    /// </summary>
    public partial class Game
    {
        const int TutSteps = 7;

        bool tutorialMode, tutShown, tutFailed;
        int tutStep, tutFrom, tutDir, tutLane;
        float tutDelay = -1f, tutTipT;
        Ent tutEnt;

        static string[] TutTitle => new[] { L("SWIPE TO CHANGE LANE", "CHANGE DE COULOIR"), L("SWIPE UP TO JUMP", "GLISSE EN HAUT : SAUTE"), L("SWIPE DOWN TO SLIDE", "GLISSE EN BAS : GLISSADE"), L("TAP TO ATTACK", "TAPE POUR ATTAQUER"), L("DODGE THE SHOPPERS", "ÉVITE LES CLIENTS"), L("BUILD YOUR CREW", "FORME TON ÉQUIPE"), L("BEAT THE BOSS", "BATS LE BOSS") };
        static readonly int[] TutKind = { 1, 2, 3, 0, 1, 1, 0 };

        string TutSub(int s)
        {
            switch (s)
            {
                case 0: return L("Change lane to avoid the kiosk", "Glisse sur le côté pour éviter le kiosque");
                case 1: return L("Jump over the carts", "Saute par-dessus les chariots");
                case 2: return L("Slide under the sign", "Glisse sous le panneau");
                case 3: return M(L("Tap anywhere: knock out the robber", "Tape n'importe où : assomme le voleur"), L("Tap anywhere: shove the cop", "Tape n'importe où : bouscule le policier"));
                case 4: return L("Don't hit them: change lane", "Ne les frappe pas : change de couloir");
                case 5: return M(L("Swipe over to your 3 colleagues", "Glisse vers tes 3 collègues"), L("Swipe over to your 3 buddies", "Glisse vers tes 3 potes"));
                default: return L("Tap to hit him, dodge his boxes", "Tape pour le frapper, évite ses cartons");
            }
        }

        /// <summary>Lance le tutoriel à partir d'une étape (0 = début ; les autres servent au debug).</summary>
        void StartTutorial(int fromStep)
        {
            tutorialMode = true;
            campaign = false;
            tutFrom = Mathf.Clamp(fromStep, 0, TutSteps - 1);
            streetIdx = 0;
            StartRun(false);
        }

        /// <summary>Appelé par StartRun une fois la course remise à zéro.</summary>
        void StartTutorialRun()
        {
            nextSpawnZ = 1e9f;
            hintT = 0f;
            hud.ShowPlay();
            hud.Toast(L("TRAINING", "ENTRAÎNEMENT"));
            hud.Ticker(M(L("Training day: learn the moves before your first shift!", "Jour d'entraînement : apprends les gestes avant ton premier service !"), L("Training day: learn the moves before the big heist!", "Jour d'entraînement : apprends les gestes avant le gros coup !")), true);
            if (tutFrom >= 6) AddMates(3);
            tutStep = tutFrom;
            tutDelay = 1.2f;
            tutEnt = null;
        }

        void BeginStep()
        {
            tutShown = false;
            tutFailed = false;
            float z = pz + 24f;
            int other = lane == 2 ? 1 : lane + 1;   // toujours le couloir voisin (un seul coup de pouce)
            tutDir = other > lane ? 1 : -1;
            tutLane = other;
            switch (tutStep)
            {
                case 0:
                    // un kiosque dans ton couloir, des pièces dans le couloir libre pour guider
                    tutEnt = Spawn(ET.Voiture, lane, z);
                    for (int i = 0; i < 4; i++) Spawn(ET.Piece, other, z - 3f + i * 2f);
                    break;
                case 1:
                    for (int l = 0; l < 3; l++) { Ent e = Spawn(ET.Barricade, l, z); if (l == lane) tutEnt = e; }
                    break;
                case 2:
                    for (int l = 0; l < 3; l++) { Ent e = Spawn(ET.Banderole, l, z); if (l == lane) tutEnt = e; }
                    break;
                case 3:
                    tutEnt = Spawn(ET.Casseur, lane, z, Mode.Casse);
                    break;
                case 4:
                    // des clients dans ton couloir (personnages neutres) : on les contourne, on ne les frappe pas
                    tutEnt = Spawn(ET.Manif, lane, z);
                    for (int i = 0; i < 4; i++) Spawn(ET.Piece, other, z - 3f + i * 2f);
                    break;
                case 5:
                    // les collègues attendent dans le couloir voisin : on glisse pour aller sur eux
                    tutEnt = SpawnRecruits(other, z, 3);
                    for (int i = 0; i < 3; i++) Spawn(ET.Piece, other, z - 8f + i * 2f);
                    break;
                default:
                    StartBoss();
                    tutEnt = boss;
                    ShowTutTip();
                    tutTipT = 3f;
                    break;
            }
        }

        void ShowTutTip()
        {
            tutShown = true;
            hud.ShowTip(TutKind[tutStep], TutTitle[tutStep], TutSub(tutStep), tutStep == 0 || tutStep == 4 || tutStep == 5 ? tutDir : 0);
            // ralenti tant que le geste n'est pas fait
            tipSlow = tutStep <= 5;
            actLane = actSwing = actJump = actSlide = false;
        }

        void TutorialTick()
        {
            if (tutDelay > 0f)
            {
                tutDelay -= Time.unscaledDeltaTime;
                if (tutDelay <= 0f) BeginStep();
                return;
            }
            if (tutEnt == null) return;
            if (tutTipT > 0f) { tutTipT -= Time.unscaledDeltaTime; if (tutTipT <= 0f) hud.HideTip(); }
            float dz = tutEnt.z - pz;

            if (!tutShown && dz < (tutStep == 5 ? 18f : 11f)) ShowTutTip();

            // le bon geste a été fait : on lève le ralenti et la consigne
            if (tutShown && tipSlow)
            {
                bool did = tutStep == 5 ? actLane || lane == tutLane : tutStep == 0 || tutStep == 4 ? actLane : tutStep == 1 ? actJump : tutStep == 2 ? actSlide : actSwing;
                if (did) { tipSlow = false; hud.HideTip(); }
            }

            bool passed = tutEnt.z < pz - 1.2f || tutEnt.dead;
            switch (tutStep)
            {
                case 3:
                    if (tutEnt.hit && !tutFailed) { StepDone(); return; }
                    break;
                case 5:
                    if (squad.Count >= 3) { StepDone(); return; }
                    break;
                case 6:
                    return; // la victoire passe par BossDown → RunWon
            }
            if (!passed) return;
            if (tutFailed || (tutStep == 3 && !tutEnt.hit) || tutStep == 5) RetryStep();
            else StepDone();
        }

        void StepDone()
        {
            tipSlow = false;
            hud.HideTip();
            string[] cheers = { L("NICE!", "BIEN !"), L("GREAT!", "SUPER !"), L("PERFECT!", "PARFAIT !"), L("AWESOME!", "GÉNIAL !") };
            hud.Toast(cheers[Random.Range(0, cheers.Length)]);
            Fx.Confetti(new Vector3(px, 1.6f, pz + 1.5f));
            Snd.Play(Snd.I.pickup, 0.7f, 0f);
            Haptics.Pulse(30);
            tutStep++;
            tutEnt = null;
            tutDelay = 0.7f;
        }

        void RetryStep()
        {
            tipSlow = false;
            hud.HideTip();
            hud.Toast(L("TRY AGAIN!", "RECOMMENCE !"));
            tutEnt = null;
            tutDelay = 0.6f;
            if (tutStep == 5 && squad.Count > 0) ClearSquad();
        }

        /// <summary>Raté pendant le tutoriel : pas de cœur perdu, on rejoue l'étape.</summary>
        void TutorialFail(string why)
        {
            inv = 0.8f;
            tutFailed = true;
            stumble = 0.3f;
            P.Flash(new Color(1f, 0.25f, 0.2f), 0.15f);
            PostFx.Hit(0.5f);
            Haptics.Pulse(50);
            hud.Float(new Vector3(px, 2.2f, pz), why, Hud.Bad);
            if (tutStep == 6) hud.Toast(L("DODGE IT!", "ESQUIVE !"));
        }

        void ShowTutorialDone()
        {
            metaScreen = MetaScreen.Result;
            bool first = !Progress.TutorialDone;
            Progress.TutorialDone = true;
            int reward = (first ? Tuning.TutorialReward : 0) + coins;
            Progress.Coins += reward;
            pendingMapAnim = -1;
            string body = L("Swipe to move, jump and slide.\nTap to attack (and tap as you land: NINJA STRIKE).\nRecruit your crew. Grab green medkits.\n\nNext: conquer " + Tuning.StreetNames[0] + "!",
                             "Glisse pour te déplacer, sauter et glisser.\nTape pour attaquer (et tape à l'atterrissage : COUP NINJA).\nRecrute ton équipe. Ramasse les trousses de soin vertes.\n\nEnsuite : conquiers " + Tuning.StreetNames[0] + " !");
            hud.ShowResult(true, L("TRAINING COMPLETE!", "ENTRAÎNEMENT TERMINÉ !"), M(L("You're ready for your shift", "Tu es prêt pour ton service"), L("You're ready for the heist", "Tu es prêt pour le casse")), body, reward, Progress.Coins, null,
                           L("Spend your " + Tuning.Currency + " to win new gear: weapon, shield, head, body.", "Dépense tes " + Tuning.Currency + " pour gagner de nouveaux équipements : arme, bouclier, tête, corps."));
        }
    }
}
