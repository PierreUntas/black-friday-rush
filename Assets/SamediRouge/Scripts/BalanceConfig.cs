using System;

namespace SamediRouge
{
    /// <summary>
    /// Coefficients du modèle de difficulté (DifficultyModel). Tous modifiables dans balance.json.
    /// Temps en secondes, distances en mètres. Les durées d'action reprennent les valeurs du jeu.
    /// </summary>
    [Serializable]
    public class DifficultyCoefs
    {
        // ---- temps humains et durées d'action (jeu : saut vy 12,5 / g 38 = 0,66 s ; glissade 0,72 s ; couloir ≈ 0,2 s ; coup 0,26 s)
        public float reaction = 0.35f, jump = 0.66f, slide = 0.72f, laneChange = 0.2f, swing = 0.3f;
        /// <summary>Le saut / la glissade doit commencer au plus tard X s avant le contact.</summary>
        public float jumpLead = 0.25f, slideLead = 0.2f;
        /// <summary>Temps d'anticipation : on voit et on peut préparer une action X s avant le contact.</summary>
        public float visibility = 1.5f;
        /// <summary>Temps de décision : deux décisions séparées de moins que ça augmentent la pression.</summary>
        public float attention = 0.25f;
        /// <summary>Portée de frappe (poings, matraque, matraque renforcée) et vitesse de charge des adversaires.</summary>
        public float reachFists = 3.4f, reachBaton = 5.8f, reachHeavy = 6.3f, chargeSpeed = 5.5f, exitSpeed = 2.5f;
        /// <summary>Part maximale d'une bande éliminée par les alliés.</summary>
        public float allyShareMax = 0.7f;
        /// <summary>Délai entre le tap et l'impact du coup ; poids d'un élément facultatif (bande d'un autre couloir, magasin attaqué).</summary>
        public float impactDelay = 0.1f, optionalWeight = 0.4f;
        /// <summary>Armes à distance : portée (m), cadence du pistolet et de la mitraillette (s), part des tirs utiles (un seul couloir visé).</summary>
        public float gunRange = 30f, pistolRate = 0.3f, smgRate = 0.2f, gunEfficiency = 0.5f;

        // ---- scores de base par élément
        /// <summary>Par coup de matraque nécessaire (un adversaire isolé à 1 PV = 1 coup).</summary>
        public float bSwing = 2f;
        public float bJump = 1f, bSlide = 1.2f, bKiosk = 1.5f, bBarrier = 2f, bThrower = 1.5f, bShopper = 0.5f, bRecruitGrab = 0.3f, bBossAttack = 2f;
        /// <summary>Bonus (négatifs), multipliés par leur accessibilité.</summary>
        public float bMedkit = -3f, bVest = -2f;

        // ---- pression temporelle : d = b × (1 + k × pression^γ)
        public float k = 1.5f, gamma = 2f, pressureCap = 3f;
        /// <summary>Ajout pour une séquence impossible (pression > 1 pour toutes les options).</summary>
        public float impossiblePenalty = 8f;
        /// <summary>Score maximal d'un seul élément (hors pénalité) : une grosse bande ne pèse pas plus qu'un passage très dur.</summary>
        public float demandCap = 30f;

        // ---- agrégation : fenêtres de W s, report λ de la fenêtre précédente, norme p, échelle D0
        public float window = 2f, lambda = 0.5f, p = 3f, d0 = 40f;

        // ---- accessibilité des bonus : α = Π (1 − r)
        public float rLane = 0.15f, rRisky = 0.5f, rDangerAfter = 0.3f, dangerAfterTime = 0.6f;

        // ---- boss
        /// <summary>Durée d'un cycle d'attaque du boss, part du cycle où l'on peut frapper.</summary>
        public float bossCycle = 2f, bossHitShare = 0.5f;

        // ---- statut (score effectif sur 100)
        public float easy = 30f, moderate = 55f, hard = 75f, veryHard = 90f;
        /// <summary>Un parcours est « accessible » sous ce score (et sans séquence impossible au-delà de la vie).</summary>
        public float accessible = 85f;
        /// <summary>Coups d'avance à garder au-delà des coups inévitables pour qu'un parcours soit accessible.</summary>
        public int accessMargin = 4;
        /// <summary>Points de score accessibles en plus par coup encaissable au-delà de 5 (gilet, bouclier).</summary>
        public float accessPerHit = 1.5f;
    }

    /// <summary>
    /// Tous les chiffres d'équilibrage, sérialisables en JSON (Resources/SamediRouge/balance.json écrase ces valeurs par défaut).
    /// Aucune dépendance à Unity : le même code tourne dans le jeu, dans l'éditeur et dans le simulateur en ligne de commande.
    /// Tableaux « par rue » : une entrée par rue, toutes maps confondues (voir Tuning.MapFirst).
    /// </summary>
    [Serializable]
    public class BalanceConfig
    {
        public static BalanceConfig I = new BalanceConfig();

        // ---------------------------------------------------------------- rues
        //                        DOWNTOWN (0-4)                    SHOPPING DISTRICT (5-8)        CENTRAL STATION (9-12)         HARBOR DOCKS (13-16)           UPTOWN (17-21)
        public float[] streetLength = { 250f, 280f, 300f, 340f, 380f, 320f, 340f, 360f, 400f, 360f, 380f, 400f, 440f, 380f, 400f, 420f, 460f, 400f, 420f, 440f, 460f, 520f };
        public int[] bossHp =         { 8, 12, 16, 20, 26,              22, 25, 28, 36,             28, 31, 34, 44,              34, 37, 40, 52,              40, 43, 46, 50, 66 };
        public float[] startSpeed =   { 12f, 12.5f, 13f, 13.5f, 14f,    14f, 14.5f, 14.5f, 15f,     14.5f, 15f, 15f, 15.5f,      15f, 15.5f, 15.5f, 16f,      15.5f, 16f, 16f, 16.5f, 17f };
        public float[] maxSpeed =     { 18f, 20f, 22f, 24f, 26f,        25f, 25.5f, 26f, 26.5f,     26f, 26.5f, 27f, 27.5f,      27f, 27.5f, 28f, 28.5f,      28f, 28.5f, 29f, 29.5f, 30f };
        public int[] packMin =        { 2, 3, 2, 3, 3, 3, 4, 4, 5, 4, 4, 5, 5, 5, 5, 5, 6, 5, 6, 6, 6, 7 };
        public int[] packMax =        { 4, 5, 5, 6, 8, 7, 8, 8, 9, 8, 8, 9, 9, 9, 9, 10, 10, 10, 10, 10, 11, 11 };
        public float[] armoredShare = { 0.2f, 0.6f, 0.35f, 0.4f, 0.5f, 0.45f, 0.5f, 0.5f, 0.55f, 0.5f, 0.55f, 0.55f, 0.6f, 0.55f, 0.55f, 0.6f, 0.6f, 0.55f, 0.6f, 0.6f, 0.62f, 0.65f };
        public float[] heavyShare =   { 0f, 0.1f, 0.05f, 0.1f, 0.2f, 0.2f, 0.25f, 0.3f, 0.3f, 0.25f, 0.25f, 0.3f, 0.3f, 0.25f, 0.3f, 0.3f, 0.32f, 0.28f, 0.3f, 0.32f, 0.34f, 0.35f };
        /// <summary>Points de vie d'un adversaire normal / casqué / blindé, par rue.</summary>
        public int[] hpNormal =       { 1, 1, 1, 1, 1,                  1, 1, 2, 2,                 2, 2, 2, 3,                  3, 3, 3, 3,                  3, 3, 3, 4, 4 };
        public int[] hpArmored =      { 2, 2, 2, 2, 2,                  2, 3, 3, 3,                 4, 5, 5, 5,                  5, 5, 6, 6,                  6, 6, 6, 7, 7 };
        public int[] hpHeavy =        { 3, 3, 3, 3, 3,                  4, 4, 5, 5,                 7, 8, 8, 8,                  8, 8, 9, 9,                  9, 9, 10, 10, 10 };
        /// <summary>
        /// Calibrage : équipement typique d'un joueur en arrivant sur la rue (« tête corps arme bouclier »), score effectif
        /// visé pour cet équipement, et coups inévitables tolérés pour lui. Courbe voulue :
        /// niveau 1 pas trop facile mais faisable du premier coup ; niveau 2 dur, sans l'arme renforcée, on perd souvent
        /// (il faut gagner un objet de plus à chaque rue) et on gagne l'arme en le terminant ; niveau 3 de difficulté moyenne ;
        /// niveaux 4 et 5 de nouveau exigeants.
        /// </summary>
        public string[] refLoadout =    { "0000", "1000", "1010", "1110", "1111",   "2111", "2221", "2222", "2222",   "3222", "3222", "3222", "3322",   "3322", "3332", "3332", "3332",   "3333", "3333", "3333", "3333", "3333" };
        public float[] targetScore =    { 70f, 96f, 76f, 82f, 86f,   94f, 95f, 95f, 97f,   82f, 84f, 86f, 88f,   94f, 90f, 91f, 96f,   90f, 91f, 92f, 93f, 95f };
        public int[] allowedImpossible = { 1, 4, 1, 2, 2,   4, 4, 5, 5,   3, 3, 3, 3,   4, 4, 4, 4,   4, 4, 4, 4, 5 };
        /// <summary>Équipement qui doit rendre la rue accessible (sans coup inévitable) : en général un objet de plus que
        /// l'équipement d'arrivée, deux pour le boss de fin de quartier. Une fois tout gagné, seul le talent compte.</summary>
        public string[] accessLoadout = { "0000", "1010", "1010", "1110", "1111",   "2221", "2222", "2232", "3232",   "3222", "3222", "3222", "3322",   "3332", "3332", "3332", "3333",   "3333", "3333", "3333", "3333", "3333" };
        /// <summary>Densité de base : une rangée tous les N mètres (plus petit = plus dense).</summary>
        public float[] rowSpacing =   { 17f, 16f, 15.5f, 15f, 14.5f,    14f, 13.5f, 13f, 12.5f,     13f, 12.5f, 12f, 12f,        12.5f, 12f, 11.5f, 11.5f,    12f, 11.5f, 11f, 11f, 10.5f };
        /// <summary>Distance entre deux pièces d'équipement de course.</summary>
        public float[] gearGap =      { 110f, 110f, 110f, 120f, 120f,   150f, 150f, 160f, 160f,     170f, 170f, 180f, 180f,      180f, 190f, 190f, 200f,      200f, 200f, 210f, 210f, 220f };
        public int[] victoryBonus =   { 20, 40, 50, 60, 100, 50, 60, 70, 120, 70, 80, 90, 150, 90, 100, 110, 200, 120, 130, 140, 150, 500 };

        // ---------------------------------------------------------------- vie, soins
        public int baseHp = 100, hpPerVest = 25, damagePerHit = 25;
        public int medkitHeal = 20;
        /// <summary>Une trousse suit une fenêtre dont la charge dépasse ce seuil (charge de fenêtre du modèle de difficulté).</summary>
        public float medkitCharge = 8f;
        /// <summary>Au plus N trousses par tiers de parcours, jamais dans les X premiers % ni pendant le boss.</summary>
        public int medkitsPerThird = 1, medkitsMax = 2;
        public float medkitNoneBefore = 0.2f;
        /// <summary>Part des trousses placées en position risquée (derrière une bande, après un obstacle).</summary>
        public float riskyMedkitShare = 0.5f;
        /// <summary>Invincibilité après un coup reçu (s), durée du bouclier de course (s).</summary>
        public float invulnerability = 0.7f, shieldDuration = 5f;

        // ---------------------------------------------------------------- armes, escouade
        /// <summary>Dégâts par niveau d'arme (poings, matraque, lourde, électrique).</summary>
        public int[] weaponDamage = { 1, 2, 3, 4 };
        /// <summary>Dégâts de la matraque renforcée (map 2) par niveau d'arme.</summary>
        public int[] heavyDamage = { 2, 3, 4, 5 };
        /// <summary>Nombre max d'alliés et dégâts d'un allié, par niveau d'escouade.</summary>
        public int[] maxSquad = { 4, 6, 8, 10 };
        public int[] mateDamage = { 1, 1, 2, 3 };

        // ---------------------------------------------------------------- coûts des améliorations (niveau 1, 2, 3)
        public int[] costHelmet = { 60, 180, 420 };
        public int[] costVest = { 90, 240, 520 };
        public int[] costWeapon = { 250, 300, 700 };   // la 1re arme est offerte après la 1re tentative sur la rue 2 (Balance.FirstWeaponStreet)
        /// <summary>Ligne Bouclier (ex-escouade).</summary>
        public int[] costSquad = { 150, 360, 760 };

        // ---------------------------------------------------------------- étoiles
        public int lootGuard = 1, lootThief = 2, starsPerfect = 5, starsCombo = 3, storeSaveStars = 10, storeSavePerfectBonus = 5;
        public float replayBonusFactor = 0.4f, defeatKeep = 1f;

        // ---------------------------------------------------------------- plans pré-calibrés
        /// <summary>Intensité et densité de chaque plan (rue × 4 variantes), trouvées par Balance.Calibrate hors jeu ; valables
        /// tant que l'empreinte de la configuration vaut bakedHash (sinon le jeu recalibre, plus lentement).</summary>
        public string bakedHash = "b0ae771f383b808f";
        public float[] bakedIntensity = { 1.7f, 1.125f, 1.4f, 2.3f, 1.4f, 2.35f, 2f, 2.1f, 2.8f, 1.95f, 2.55f, 2.225f, 1.825f, 2.1f, 1.85f, 2.05f, 1.375f, 1.475f, 1.25f, 1.6f, 2.2f, 1.95f, 1.8f, 1.825f, 1.75f, 1.175f, 1.8f, 1.725f, 1.9f, 1.4f, 1.275f, 1.65f, 0.95f, 1.275f, 1.4f, 1.525f, 1.125f, 1.625f, 1.45f, 1.5f, 0.95f, 1.05f, 1.55f, 1.325f, 0.875f, 1.1f, 1.3f, 0.925f, 1.075f, 1.025f, 1.55f, 1.1f, 1.7f, 1.25f, 1.75f, 1.45f, 1.45f, 1.275f, 1.45f, 0.925f, 1.15f, 1.15f, 1.375f, 1.25f, 1.2f, 1.2f, 0.8f, 1.025f, 1.4f, 0.9f, 1.4f, 1f, 1.15f, 0.95f, 0.875f, 1.35f, 0.925f, 0.9f, 1.2f, 0.825f, 0.875f, 0.975f, 1.125f, 0.925f, 1.1f, 0.725f, 1f, 1.25f };
        public float[] bakedDensity = { 1f, 0.97f, 1f, 1f, 1.03f, 1f, 1f, 1f, 1f, 1f, 0.91f, 0.97f, 0.97f, 1f, 1.03f, 1f, 1.09f, 0.97f, 0.97f, 1.03f, 1f, 1.03f, 0.97f, 0.97f, 1.03f, 0.91f, 1.03f, 0.97f, 1f, 1.03f, 0.97f, 0.97f, 1f, 0.97f, 0.97f, 1.03f, 1.03f, 1.15f, 1.09f, 1.09f, 1f, 1.03f, 1f, 0.97f, 0.91f, 1f, 1.03f, 1.03f, 0.91f, 0.97f, 0.97f, 0.97f, 0.97f, 1.09f, 1.03f, 1.03f, 1.03f, 0.97f, 1.03f, 0.97f, 1f, 1f, 0.97f, 1.15f, 1.03f, 0.97f, 1.03f, 0.97f, 1.09f, 1.03f, 1.03f, 0.97f, 0.97f, 1f, 0.97f, 0.97f, 0.85f, 0.97f, 0.97f, 0.97f, 1.03f, 1.15f, 0.91f, 0.97f, 1.03f, 0.97f, 1.03f, 1.03f };

        // ---------------------------------------------------------------- modèle de difficulté
        public DifficultyCoefs coefs = new DifficultyCoefs();
        /// <summary>Équipements comparés dans le rapport (« casque gilet arme escouade »).</summary>
        public string[] reportLoadouts = { "0000", "0010", "1111", "2121", "2222", "3232", "3333" };

        public int Cost(int gear, int level)
        {
            int[] t = gear == 0 ? costHelmet : gear == 1 ? costVest : gear == 2 ? costWeapon : costSquad;
            return level >= 0 && level < t.Length ? t[level] : -1;
        }
    }
}
