using UnityEngine;

namespace SamediRouge
{
    /// <summary>
    /// Tous les réglages de la campagne « Street Conquest » au même endroit, pour itérer vite.
    /// Les valeurs sont lues au lancement de chaque course ; rien n'est sauvegardé ici.
    /// </summary>
    public static class Tuning
    {
        /// <summary>V3 : battle royale vue de dessus (Royale) au lieu du runner.</summary>
        public const bool Heist = true;

        /// <summary>
        /// Mode « arcade » (retours de l'éditeur YSO) : tir automatique dès le départ, coups de près automatiques,
        /// un seul geste (glisser à gauche / à droite), plus de saut ni de glissade, moins de texte.
        /// </summary>
        public const bool Arcade = true;
        /// <summary>Version de test pour l'éditeur : un seul camp, les voleurs (le choix du camp est masqué).</summary>
        public const bool OnlyThief = true;
        /// <summary>Arcade : délai entre deux tirs automatiques (s) avec le pistolet, et à mains nues.</summary>
        public const float AutoPistolRate = 0.28f, AutoBareRate = 0.45f;

        // ------------------------------------------------------------------ maps (quartiers)

        /// <summary>
        /// Les maps se suivent : la map suivante se débloque quand la dernière rue de la précédente est conquise.
        /// Pour ajouter une map : une entrée ici, ses rues à la suite dans les tableaux « par rue », un thème de décor.
        /// </summary>
        public static readonly string[] MapNames = { "DOWNTOWN", "SHOPPING DISTRICT", "CENTRAL STATION", "HARBOR DOCKS", "UPTOWN" };
        /// <summary>Noms courts pour les onglets de la carte.</summary>
        public static readonly string[] MapShort = { "DOWNTOWN", "SHOPPING", "STATION", "DOCKS", "UPTOWN" };
        public static readonly int[] MapFirst = { 0, 5, 9, 13, 17 };
        public static readonly int[] MapCount = { 5, 4, 4, 4, 5 };
        /// <summary>Thème de décor (0 galerie marchande, 1 rue commerçante pillée, 2 gare, 3 docks, 4 quartier chic de nuit).</summary>
        public static readonly int[] MapTheme = { 0, 1, 2, 3, 4 };
        /// <summary>Arme de la map (0 matraques classiques, 1 matraque renforcée).</summary>
        public static int[] MapWeaponTier => Balance.MapWeaponTier;
        public static int Maps => MapNames.Length;

        public static int MapOf(int street)
        {
            for (int m = Maps - 1; m >= 0; m--) if (street >= MapFirst[m]) return m;
            return 0;
        }

        // ------------------------------------------------------------------ rues (toutes maps confondues)

        public const int Streets = 22;

        public static readonly string[] StreetNames =
        {
            "MALL AVENUE", "MARKET STREET", "STATION ROAD", "HARBOR BOULEVARD", "CITY HALL SQUARE",
            "BOUTIQUE ROW", "JEWELRY LANE", "GRAND ARCADE", "OUTLET PLAZA",
            "PLATFORM 9", "TICKET HALL", "SUBWAY CONCOURSE", "GRAND TERMINAL",
            "CONTAINER ROW", "FISH MARKET", "CRANE YARD", "PIER 42",
            "GOLDEN MILE", "DIAMOND AVENUE", "SKYLINE BOULEVARD", "CASINO STRIP", "BLACK FRIDAY TOWER"
        };

        /// <summary>Longueur de course avant le boss (m).</summary>
        public static float[] StreetLength => BalanceConfig.I.streetLength;

        /// <summary>Boss de chaque rue (0 lanceur, 1 brute, 2 chef de bande, 3 double lancer, 4 charge, 5 mascotte, 6 grosse batte).</summary>
        public static int[] StreetBoss => Balance.StreetBoss;

        /// <summary>Points de vie du boss (un coup de poing ou de matraque niveau 1 = 1 point).</summary>
        public static int[] BossHp => BalanceConfig.I.bossHp;

        /// <summary>Vitesse de course au départ et plafond, par rue.</summary>
        public static float[] StartSpeed => BalanceConfig.I.startSpeed;
        public static float[] MaxSpeed => BalanceConfig.I.maxSpeed;

        /// <summary>Taille des bandes adverses (min, max) par rue.</summary>
        public static int[] PackMin => BalanceConfig.I.packMin;
        public static int[] PackMax => BalanceConfig.I.packMax;

        /// <summary>Part des adversaires casqués (2 points de vie) puis blindés (3 points de vie), par rue.</summary>
        public static float[] ArmoredShare => BalanceConfig.I.armoredShare;
        public static float[] HeavyShare => BalanceConfig.I.heavyShare;

        // ------------------------------------------------------------------ map 2 : pillage

        /// <summary>Part des rangées de la map 2 : pillards qui sortent des magasins, embuscades, magasins attaqués.</summary>
        public const float StoreExitShare = 0.18f, AmbushShare = 0.1f, StoreAttackShare = 0.16f;
        /// <summary>Étoiles pour un magasin sauvé, et bonus si un pillard du groupe tombe sur un Perfect Hit.</summary>
        public static int StoreSaveStars => BalanceConfig.I.storeSaveStars;
        public static int StoreSavePerfectBonus => BalanceConfig.I.storeSavePerfectBonus;
        /// <summary>Matraque renforcée : dégâts en plus, largeur de balayage, cibles max, projection.</summary>
        public const int HeavyBonusDamage = 1, HeavyMaxTargets = 3;
        /// <summary>Adversaires touchés par un coup : poings, batte / matraque. Délai entre deux coups (s).</summary>
        public const int FistMaxTargets = 1, BatonMaxTargets = 2;
        public const float SwingCooldown = 0.3f;
        public const float HeavyWidth = 2.7f, HeavyPower = 1.4f;

        /// <summary>
        /// Obstacles introduits rue après rue (la rue 1 apprend le saut, la 2 la glissade et les clients…).
        /// Indice = première rue (0-4) où l'obstacle apparaît.
        /// </summary>
        public const int IntroCarts = Balance.IntroCarts, IntroBoxes = Balance.IntroBoxes, IntroSigns = Balance.IntroSigns, IntroShoppers = Balance.IntroShoppers,
                         IntroKiosks = Balance.IntroKiosks, IntroThrowers = Balance.IntroThrowers, IntroBarriers = Balance.IntroBarriers;

        /// <summary>Couleur du sol et du brouillard par rue (décor varié à peu de frais).</summary>
        public static readonly Color[] StreetFloor =
        {
            new Color(1f, 1f, 1f), new Color(1f, 0.93f, 0.82f), new Color(0.85f, 0.92f, 1f), new Color(0.82f, 0.95f, 0.9f), new Color(0.95f, 0.86f, 0.95f),
            new Color(0.78f, 0.74f, 0.7f), new Color(0.72f, 0.7f, 0.74f), new Color(0.7f, 0.66f, 0.62f), new Color(0.74f, 0.68f, 0.66f),
            // gare : dalles claires, lumière froide
            new Color(0.74f, 0.76f, 0.8f), new Color(0.7f, 0.74f, 0.8f), new Color(0.68f, 0.71f, 0.78f), new Color(0.76f, 0.72f, 0.66f),
            // docks : béton humide, brume
            new Color(0.62f, 0.66f, 0.7f), new Color(0.6f, 0.64f, 0.66f), new Color(0.58f, 0.6f, 0.64f), new Color(0.55f, 0.58f, 0.62f),
            // quartier chic de nuit : sol sombre et brillant
            new Color(0.5f, 0.46f, 0.6f), new Color(0.48f, 0.44f, 0.58f), new Color(0.46f, 0.42f, 0.56f), new Color(0.5f, 0.4f, 0.52f), new Color(0.44f, 0.38f, 0.5f)
        };
        public static readonly Color[] StreetFog =
        {
            new Color(0.86f, 0.88f, 0.92f), new Color(0.95f, 0.88f, 0.78f), new Color(0.78f, 0.85f, 0.95f), new Color(0.75f, 0.9f, 0.88f), new Color(0.9f, 0.8f, 0.92f),
            new Color(0.72f, 0.6f, 0.55f), new Color(0.62f, 0.55f, 0.62f), new Color(0.66f, 0.5f, 0.42f), new Color(0.6f, 0.48f, 0.46f),
            new Color(0.8f, 0.86f, 0.94f), new Color(0.74f, 0.82f, 0.92f), new Color(0.7f, 0.76f, 0.88f), new Color(0.84f, 0.78f, 0.68f),
            new Color(0.5f, 0.58f, 0.64f), new Color(0.46f, 0.55f, 0.6f), new Color(0.44f, 0.5f, 0.58f), new Color(0.4f, 0.46f, 0.54f),
            new Color(0.16f, 0.12f, 0.26f), new Color(0.14f, 0.1f, 0.24f), new Color(0.12f, 0.1f, 0.22f), new Color(0.2f, 0.08f, 0.2f), new Color(0.1f, 0.08f, 0.18f)
        };

        // ------------------------------------------------------------------ récompenses

        /// <summary>Bonus de victoire (pièces) par rue, en plus des pièces ramassées.</summary>
        public static int[] VictoryBonus => BalanceConfig.I.victoryBonus;
        /// <summary>Bonus réduit quand on rejoue une rue déjà conquise.</summary>
        public static float ReplayBonusFactor => BalanceConfig.I.replayBonusFactor;
        /// <summary>Part des pièces ramassées conservée en cas de défaite.</summary>
        public static float DefeatKeep => BalanceConfig.I.defeatKeep;
        /// <summary>Récompense de fin de tutoriel.</summary>
        public const int TutorialReward = 50;
        /// <summary>Pièces lâchées par adversaire assommé (vigile / voleur).</summary>
        public static int LootGuard => BalanceConfig.I.lootGuard;
        public static int LootThief => BalanceConfig.I.lootThief;

        // ------------------------------------------------------------------ équipement permanent

        /// <summary>
        /// Lignes d'équipement : on GAGNE des objets (3 par ligne), on ne les « améliore » plus.
        /// Shield (ex-Squad) : le bouclier permanent remplace l'équipe ; les alliés se recrutent en course.
        /// </summary>
        public const int Helmet = 0, Vest = 1, Weapon = 2, Shield = 3, Squad = 3, MaxLevel = 3, GearCount = 4;
        public static string[] GearNames => new[] { L("HEAD", "TÊTE"), L("BODY", "CORPS"), L("WEAPON", "ARME"), L("SHIELD", "BOUCLIER") };

        /// <summary>Nom de l'objet d'une ligne au niveau donné (1 à 3), selon le camp.</summary>
        public static string ItemName(int gear, bool thief, int level)
        {
            int i = Mathf.Clamp(level, 1, 3) - 1;
            string[][] cop =
            {
                new[] { L("HELMET", "CASQUE"), L("VISOR HELMET", "CASQUE À VISIÈRE"), L("ELITE HELMET", "CASQUE D'ÉLITE") },
                new[] { L("VEST", "GILET"), L("BULLETPROOF VEST", "GILET PARE-BALLES"), L("RIOT ARMOR", "ARMURE") },
                new[] { L("BATON", "MATRAQUE"), L("TASER", "TASER"), L("FOAM SMG", "MITRAILLETTE À MOUSSE") },
                new[] { L("WOODEN SHIELD", "BOUCLIER EN BOIS"), L("RIOT SHIELD", "BOUCLIER ANTI-ÉMEUTE"), L("BALLISTIC SHIELD", "BOUCLIER BALISTIQUE") },
            };
            string[][] rob =
            {
                new[] { L("HEADBAND", "BANDEAU"), L("SKI GOGGLES", "LUNETTES DE SKI"), L("MOTO HELMET", "CASQUE DE MOTO") },
                new[] { L("PUFFER VEST", "DOUDOUNE"), L("LEATHER JACKET", "BLOUSON DE CUIR"), L("STOLEN VEST", "GILET VOLÉ") },
                new[] { L("BAT", "BATTE"), L("PAINT PISTOL", "PISTOLET À PEINTURE"), L("PAINT MINIGUN", "MITRAILLEUSE À PEINTURE") },
                new[] { L("TRASH CAN LID", "COUVERCLE DE POUBELLE"), L("STOP SIGN", "PANNEAU STOP"), L("STOLEN SHIELD", "BOUCLIER VOLÉ") },
            };
            return (thief ? rob : cop)[Mathf.Clamp(gear, 0, 3)][i];
        }

        /// <summary>Coût du niveau suivant (équipement, niveau actuel), -1 au maximum. Valeurs dans BalanceConfig.</summary>
        public static int UpgradeCost(int gear, int level) => BalanceConfig.I.Cost(gear, level);

        /// <summary>Effet de chaque niveau, affiché sur l'écran d'amélioration [équipement, niveau 0-3].</summary>
        public static string[,] GearEffect => new string[,]
        {
            { L("No helmet", "Pas de casque"), L("Blocks thrown boxes", "Bloque les cartons lancés"), L("Also blocks SALE signs", "Bloque aussi les panneaux SOLDES"), L("Also blocks the boss's crates", "Bloque aussi les caisses du boss") },
            { L("100 HP", "100 PV"), L("125 HP", "125 PV"), L("150 HP", "150 PV"), L("175 HP", "175 PV") },
            { L("Fists: short reach", "Poings : courte portée"), L("Wide swings", "Coups larges"), L("Tap to shoot from afar", "Tape pour tirer de loin"), L("Automatic bursts", "Rafales automatiques") },
            { L("No shield", "Pas de bouclier"), L("Blocks 1 hit per run", "Encaisse 1 coup par course"), L("Blocks 2 hits per run", "Encaisse 2 coups par course"), L("Blocks 3 hits per run", "Encaisse 3 coups par course") },
        };

        // ------------------------------------------------------------------ vie (jauge)

        public static int BaseHp => BalanceConfig.I.baseHp;
        public static int HpPerVest => BalanceConfig.I.hpPerVest;
        public static int MaxHp(int vestLevel) => BaseHp + vestLevel * HpPerVest;
        /// <summary>Dégâts d'un coup reçu (un kiosque compte double).</summary>
        public static int DamagePerHit => Mathf.RoundToInt(BalanceConfig.I.damagePerHit * HitDamageMult);
        /// <summary>
        /// Durcissement en jeu (le modèle de difficulté garde les valeurs de base) : coups reçus plus forts (3 coups = K.-O. à 100 PV),
        /// adversaires qui chargent plus vite, boss plus résistants.
        /// </summary>
        public const float HitDamageMult = 1.36f, EnemyRush = 1.3f, BossHpMult = 1.3f;
        /// <summary>Soin d'une trousse de secours, et soin à la victoire contre le boss (stages sans fin).</summary>
        public static int MedkitHeal => BalanceConfig.I.medkitHeal;
        public const int BossHeal = 30;
        /// <summary>Probabilité d'une trousse dans une rangée « risquée » (au milieu d'une bande, derrière un obstacle).</summary>
        public const float RiskyMedkit = 0.22f;

        // ------------------------------------------------------------------ étoiles (monnaie)

        /// <summary>Étoiles bonus : Perfect Hit, palier de combo (tous les 5).</summary>
        /// <summary>Monnaie gardée d'une course à l'autre (icône : l'étoile dorée, la « cote » de la rue).</summary>
        public const string Currency = "DOLLARS", Cur = "$"; // monnaie : des dollars, tout simplement
        static string L(string en, string fr) => Loc.T(en, fr);

        public static int StarsPerfect => BalanceConfig.I.starsPerfect;
        public static int StarsCombo => BalanceConfig.I.starsCombo;
        /// <summary>Dégâts par coup selon le niveau d'arme (poings, matraque, lourde, électrique).</summary>
        public static int[] WeaponDamage => BalanceConfig.I.weaponDamage;
        /// <summary>Dégâts de la matraque renforcée (map 2) par niveau d'arme.</summary>
        public static int[] HeavyDamage => BalanceConfig.I.heavyDamage;

        // ------------------------------------------------------------------ super coup ninja (Perfect Hit)

        /// <summary>Fenêtre du Perfect Hit : avant l'atterrissage (en l'air) et après (au sol), en secondes de jeu.
        /// L'après est un peu plus large car le tap est reconnu au relâchement du doigt.</summary>
        public const float PerfectEarly = 0.10f, PerfectLate = 0.10f; // fenêtre ±100 ms autour de l'atterrissage
        public const float PerfectGuard = 0.45f; // le Perfect Hit protège un court instant (un joueur habile peut finir un boss avec peu de vie)

        // ---------------------------------------------------------------- grades (montée automatique en conquérant les maps)
        public static string[] RankGuard => new[] { L("STREET COP", "FLIC DE QUARTIER"), L("REINFORCED COP", "FLIC RENFORCÉ"), L("RIOT SQUAD", "BRIGADE ANTI-ÉMEUTE"), L("SPECIAL UNIT", "UNITÉ SPÉCIALE"), L("COMMISSIONER", "COMMISSAIRE") };
        public static string[] RankThief => new[] { L("STREET RIOTER", "ÉMEUTIER DE QUARTIER"), L("RIOTER", "ÉMEUTIER"), L("GANG LEADER", "CHEF DE GANG"), L("HEIST MASTERMIND", "CERVEAU DU CASSE"), L("BLACK FRIDAY KING", "ROI DU BLACK FRIDAY") };
        // identité des camps : la police encaisse (organisation, équipement), les émeutiers bougent vite et sont nombreux
        /// <summary>Durée de la glissade : assez longue pour passer sous une banderole même en glissant un peu tôt.</summary>
        public const float SlideTime = 1.1f;
        public const float GuardLaneSpeed = 14f, ThiefLaneSpeed = 19f;
        public const int ThiefRecruitBonus = 1, PropSmashStars = 1;
        /// <summary>Multiplicateurs de dégâts : coup en l'air, Perfect Hit.</summary>
        public const int AirMult = 2, PerfectMult = 3;
        /// <summary>Zone du Perfect Hit : profondeur devant toi et demi-largeur (toute la largeur des couloirs).</summary>
        public const float PerfectReach = 7.5f, PerfectWidth = 3.4f;
        /// <summary>Un tap moins de AntiSpam s après le précédent ne peut pas être parfait (récompense le timing, pas le spam).</summary>
        public const float AntiSpam = 0.3f;
        /// <summary>Les ennemis à portée brillent en or à partir de CueLead s avant l'atterrissage.</summary>
        public const float CueLead = 0.3f;

        // ------------------------------------------------------------------ tutoriel

        public const float TutorialSpeed = 10f;
        public const float TutorialSlowMo = 0.35f;
        public const int TutorialBossHp = 4;
    }
}
