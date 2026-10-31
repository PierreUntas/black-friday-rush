using System.Collections.Generic;
using UnityEngine;

namespace SamediRouge
{
    public enum ET { Casseur, Loup, Manif, Pave, Poubelle, Barricade, Banderole, Voiture, Onde, Piece, Gilet, Bouclier, Renfort, Cafe, Casque, Matraque, Pistolet, Mitraillette, Recrue }
    public enum Mode { Casse, Fonce, Lance, Sortie }

    /// <summary>Un élément de la course : casseur, obstacle, bonus…</summary>
    public class Ent
    {
        public ET type;
        public Mode mode;
        public int lane, hp;
        public float x, y, z, depth = 0.5f, timer, vz, escape, bob;
        public bool hit, dead, fled;
        public int raid = -1; // magasin attaqué auquel appartient ce pillard (-1 : aucun)
        public GameObject ring; // cercle rouge au sol sous un ennemi
        public GameObject go;
        public Rig rig;
        public List<Rig> crowd;
        public Transform spin;
        public bool IsPickup => type >= ET.Gilet;
    }

    /// <summary>Boucle de jeu : runner en portrait, on joue un CRS qui dégomme les casseurs de la Meute du Loup.</summary>
    public partial class Game : MonoBehaviour
    {
        enum St { Menu, Play, Pause, End }

        const float Lane = Street.Lane;
        int MaxHearts = 3;

        St st = St.Menu;
        bool thief;
        string M(string guard, string asThief) => thief ? asThief : guard;
        /// <summary>Texte traduit (anglais, français), voir Loc.</summary>
        static string L(string en, string fr) => Loc.T(en, fr);
        Camera cam;
        Hud hud;
        InputCtl inp;
        Street street;
        Transform world;
        ParticleSystem snow;
        Transform skyGlow;
        Light playerLight;

        // joueur
        Rig P;
        int lane = 1;
        float px, py, pz, vy, slide, inv, stumble, swingCd, speed = 8f;
        class PendingHit { public Ent e; public float t, power; public int dmg = 1; public bool super; }

        // super coup ninja : frapper pile à l'atterrissage d'un saut
        float landT = -9f, lastTapT = -9f;
        bool superQueued, superUsed;
        int superCount;
        static readonly Color NinjaGold = new Color(1f, 0.8f, 0.15f);
        readonly List<PendingHit> pendings = new List<PendingHit>();
        int swingCount;

        // partie
        float dist, score, comboT, ren, recT, tickT, loupT, endT, hintT;

        // équipement ramassé en route, dans l'ordre : casque, gilet, matraque, bouclier, pistolet, mitraillette
        const int GHelmet = 0, GVest = 1, GBaton = 2, GShield = 3, GPistol = 4, GSmg = 5;
        static readonly ET[] GearOrder = { ET.Casque, ET.Gilet, ET.Matraque, ET.Bouclier, ET.Pistolet, ET.Mitraillette };
        readonly bool[] gear = new bool[6];
        float nextGearZ, shootCd, smgT, gearGap = 110f;

        // escouade : collègues recrutés en route qui courent et frappent avec toi
        class Mate { public Rig rig; public float x, z, cd; public int slot; }
        readonly List<Mate> squad = new List<Mate>();
        // équilibrage : plan de parcours calibré (variante), vies des adversaires du segment, escouade selon l'amélioration
        Balance.Plan plan;
        int planIdx, runVariant, squadLv, debugVariant = -1;
        float shieldT;
        readonly Queue<int> hpQueue = new Queue<int>();
        int MaxSquad => Tuning.Arcade ? ArcadeMaxSquad : campaign && !tutorialMode ? BalanceConfig.I.maxSquad[Mathf.Clamp(squadLv, 0, 3)] : 10;
        int MateDmg => campaign && !tutorialMode ? BalanceConfig.I.mateDamage[Mathf.Clamp(squadLv, 0, 3)] : 1;
        int MateArmor => campaign && !tutorialMode ? (squadLv >= 3 ? 2 : squadLv >= 2 ? 1 : 0) : 0;
        static readonly Vector2[] Slots =
        {
            new Vector2(-0.85f, -0.7f), new Vector2(0.85f, -0.7f), new Vector2(0f, -1.35f), new Vector2(-1.7f, -1.3f), new Vector2(1.7f, -1.3f),
            new Vector2(-0.85f, -2f), new Vector2(0.85f, -2f), new Vector2(0f, -2.65f), new Vector2(-1.7f, -2.6f), new Vector2(1.7f, -2.6f)
        };

        // étapes : ~260 m de course, puis un boss à battre ; chaque boss vaincu ouvre l'étape suivante
        float StageLen = 260f;
        int stage = 1;
        float stageStartZ, bossWind = -1f, stageToastT;
        Ent boss;
        int bossType, bossMaxHp, bossTargetLane, bossSecondLane;
        bool bossThrow;
        Transform warn, warn2;
        // six boss différents par camp, dans un ordre mélangé à chaque partie
        static readonly string[] BossGuard = { "THE GODFATHER", "THE BRUTE", "THE GANG LEADER", "FAKE SANTA", "THE SPEEDSTER", "BIG BEAR", "BIG BAT" };
        static readonly string[] BossThief = { "SECURITY CHIEF", "THE BOUNCER", "RIOT CAPTAIN", "THE MANAGER", "SEGWAY COP", "BIG DOG", "INTERVENTION CHIEF" };
        readonly int[] bossOrder = { 0, 1, 2, 3, 4, 5 };
        int bossAttacks, bossMove;
        bool bossCharging;
        float bossStunT;

        // map 2 : magasins attaqués à protéger, matraque renforcée
        class StoreRaid { public int left; public bool lost, perfect, told; public float z; public int side; public Transform beacon; }
        readonly List<StoreRaid> raids = new List<StoreRaid>();
        bool superHitNow, meleeHitNow;
        bool Looting => !Tuning.Arcade && campaign && !tutorialMode && !demo && Tuning.MapTheme[Tuning.MapOf(streetIdx)] == 1;
        bool Heavy => weaponTier >= 1 && !tutorialMode;
        int BaseDmg => Heavy ? Tuning.HeavyDamage[weaponLv] : Tuning.WeaponDamage[weaponLv];
        string BossName => (thief ? BossThief : BossGuard)[bossType];

        // tutoriel : une consigne à la fois, au bon moment, avec ralenti sur les gestes à apprendre
        const int TLane = 0, TTap = 1, TJump = 2, TSlide = 3, TRecruit = 4, TGear = 5, TShop = 6, TBoss = 7, TPerfect = 8, TWeapon = 9;
        readonly bool[] tipDone = new bool[10];
        int tip = -1;
        float tipShownT;
        bool tipSlow, tutorial, actLane, actSwing, actJump, actSlide;
        Ent tipEnt;

        // démo jouée toute seule au lancement
        bool demo;
        float demoT, autoT;

        class Bolt { public Transform tr; public Ent target; public bool smg; }
        int smgShots;
        readonly List<Bolt> bolts = new List<Bolt>();
        int hearts, coins, combo, idle;
        // détail des étoiles gagnées pendant la course (bilan de fin)
        int starsKO, starsPickup, starsPerfect, starsCombo, starsStores, storesSaved;
        float weaponIntroT = -1f, perfectGuardT, rankToastT;
        int medkitsTaken, hpAtBoss = -1, hp25 = -1, hp50 = -1, hp75 = -1;
        float runT;
        readonly System.Text.StringBuilder hitLog = new System.Text.StringBuilder();
        bool rec;
        string cause = "";
        float nextSpawnZ;
        readonly List<Ent> ents = new List<Ent>();
        int koCount, missed, bavures, filmed, loups, maxCombo, coinStreak;
        float coinStreakT;

        // caméra
        Vector3 camPos;
        float shake, hitStop, slowMo, fov = 62f, fovKick, camRoll, zoom;
        bool camInit, rollOnLand;
        ParticleSystem speedLines;

        // drone, renfort
        Transform drone;
        Transform[] rotors;
        MeshRenderer droneLed;
        Light droneLight;
        Rig buddy;
        float buddyX;

        static string[] IdleThief => new[]
        {
            L("Attention shoppers: please report anyone in a black hoodie and a balaclava", "Avis aux clients : signalez toute personne en sweat noir et cagoule"),
            L("Security, the jewelry store needs backup!", "Sécurité, la bijouterie a besoin de renfort !"),
            L("Reminder: running in the mall is not allowed", "Rappel : il est interdit de courir dans le centre commercial"),
            L("The Head of Security is on his way", "Le chef de la sécurité arrive"),
            L("Doorbuster deals end in 10 minutes!", "Les promos choc se terminent dans 10 minutes !"),
            L("Lost: one very large bag of cash. Please return to customer service", "Perdu : un très gros sac de billets. Merci de le rapporter à l'accueil")
        };

        static string[] Idle => new[]
        {
            L("Attention shoppers: TVs are 70% off on level 2!", "Avis aux clients : les télés à -70 % au niveau 2 !"),
            L("Security to Electronics, security to Electronics", "Sécurité au rayon high-tech, sécurité au rayon high-tech"),
            L("A lost sneaker has been found near the fountain", "Une basket perdue a été retrouvée près de la fontaine"),
            L("Doorbuster deals end in 10 minutes!", "Les promos choc se terminent dans 10 minutes !"),
            L("Please do not tackle the mannequins", "Merci de ne pas plaquer les mannequins"),
            L("Free coffee for staff in the break room", "Café offert au personnel en salle de pause")
        };

        // ================================================================== mise en place

        /// <summary>Interface (recréée quand le joueur change de langue).</summary>
        void CreateHud()
        {
            Hud.Quiet = Tuning.Arcade;
            hud = new GameObject("HUD", typeof(RectTransform)).AddComponent<Hud>();
            hud.Init(cam);

            hud.onPick = t => { thief = t; StartGame(); };
            hud.onBuy = Buy;
            InitMeta();
            hud.onPause = () => SetPause(true);
            hud.onResume = () => SetPause(false);
            hud.onQuit = ToMenu;
            hud.onAgain = StartGame;
            hud.onMenu = ToMenu;
            hud.onLang = SwitchLanguage;
        }

        /// <summary>Bouton EN / FR : la langue est mémorisée, l'interface est reconstruite dans la nouvelle langue.</summary>
        void SwitchLanguage()
        {
            Loc.Set(!Loc.Fr);
            Destroy(hud.gameObject);
            CreateHud();
            ToMenu();
        }

        void Awake()
        {
            world = new GameObject("World").transform;
            SetupCamera();
            SetupLighting();
            PostFx.Setup(cam);
            Fx.Init(world);
            speedLines = Fx.SpeedLines(cam.transform);

            street = new GameObject("Street").AddComponent<Street>();
            street.transform.SetParent(world, false);
            street.Init(0f);

            inp = gameObject.AddComponent<InputCtl>();
            gameObject.AddComponent<Snd>();

            CreateHud();

            // repère rouge au sol : le couloir visé par l'attaque du boss
            warn = Art.Decal(world, Art.Particle(Art.SoftDot, new Color(1f, 0.15f, 0.1f, 0.85f)), Vector3.zero, new Vector2(2.4f, 3.2f)).transform;
            warn.gameObject.SetActive(false);
            warn2 = Art.Decal(world, Art.Particle(Art.SoftDot, new Color(1f, 0.15f, 0.1f, 0.85f)), Vector3.zero, new Vector2(2.4f, 3.2f)).transform;
            warn2.gameObject.SetActive(false);
            snow = Fx.Snow(world);
            snow.gameObject.SetActive(false);
            skyGlow.gameObject.SetActive(false);
            buddy = Rig.Build(Kind.Guard, world, new Vector3(0f, 0f, -50f), 0f);
            buddy.root.gameObject.SetActive(false);

            P = Rig.Build(Kind.Guard, world, Vector3.zero, 0f, -1);
            P.SetVisible(true);
            Boot();
        }

        void SetupCamera()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Pal.Fog;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 170f;
            cam.fieldOfView = fov;
            cam.allowHDR = true;
        }

        void SetupLighting()
        {
            var moon = new GameObject("Moon").AddComponent<Light>();
            moonL = moon;
            moon.type = LightType.Directional;
            moon.transform.rotation = Quaternion.Euler(62f, -25f, 0f);
            moon.color = new Color(1f, 0.96f, 0.9f);
            moon.intensity = 1.05f;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.7f;
            RenderSettings.sun = moon;

            var fill = new GameObject("Fill").AddComponent<Light>();
            fillL = fill;
            fill.type = LightType.Directional;
            fill.transform.rotation = Quaternion.Euler(28f, 8f, 0f);
            fill.color = new Color(1f, 0.97f, 0.92f);
            fill.intensity = 0.35f;
            fill.shadows = LightShadows.None;

            playerLight = new GameObject("PlayerLight").AddComponent<Light>();
            playerLight.type = LightType.Point;
            playerLight.color = new Color(1f, 0.86f, 0.7f);
            playerLight.range = 9f;
            playerLight.intensity = 0.6f;
            playerLight.shadows = LightShadows.None;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.8f, 0.85f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.6f, 0.58f);
            RenderSettings.ambientGroundColor = new Color(0.45f, 0.42f, 0.4f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Pal.Fog;
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 160f;

            // halo orangé de la ville au bout du boulevard
            skyGlow = Art.Part(null, Art.Quad, Art.Particle(Art.SkyGlow, new Color(1f, 0.55f, 0.3f, 0.35f)), Vector3.zero, new Vector3(90f, 40f, 1f), new Vector3(0f, 0f, 0f), false).transform;
        }

        Light moonL, fillL;

        /// <summary>Ambiance lumineuse du quartier : jour de soldes (galerie, rue), hall de gare, docks sous un ciel gris, quartier chic la nuit.</summary>
        void ThemeLighting(int theme)
        {
            if (moonL == null) return;
            Color sky = new Color(0.78f, 0.8f, 0.85f), eq = new Color(0.62f, 0.6f, 0.58f), gr = new Color(0.45f, 0.42f, 0.4f);
            float moon = 1.05f, fill = 0.35f;
            Color moonC = new Color(1f, 0.96f, 0.9f), glow = new Color(1f, 0.55f, 0.3f, 0.35f);
            if (theme == 2) { sky = new Color(0.8f, 0.84f, 0.9f); eq = new Color(0.66f, 0.66f, 0.66f); moonC = new Color(0.95f, 0.97f, 1f); }
            else if (theme == 3) { sky = new Color(0.62f, 0.68f, 0.76f); eq = new Color(0.5f, 0.54f, 0.58f); gr = new Color(0.36f, 0.36f, 0.38f); moon = 0.8f; moonC = new Color(0.86f, 0.9f, 1f); glow = new Color(0.7f, 0.8f, 1f, 0.25f); }
            else if (theme == 4) { sky = new Color(0.3f, 0.28f, 0.48f); eq = new Color(0.32f, 0.26f, 0.38f); gr = new Color(0.22f, 0.18f, 0.24f); moon = 0.45f; fill = 0.18f; moonC = new Color(0.62f, 0.66f, 1f); glow = new Color(1f, 0.3f, 0.7f, 0.4f); }
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = eq;
            RenderSettings.ambientGroundColor = gr;
            moonL.intensity = moon;
            moonL.color = moonC;
            fillL.intensity = fill;
            if (skyGlow != null)
            {
                var mr = skyGlow.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = Art.Particle(Art.SkyGlow, glow);
            }
        }

        void BuildDrone()
        {
            drone = Art.Node(world, "Drone", new Vector3(0f, 6f, 10f));
            Material dark = Art.Mat(new Color(0.15f, 0.16f, 0.18f), 0.6f, 0.4f);
            Art.Part(drone, Art.Cube, dark, Vector3.zero, new Vector3(0.6f, 0.16f, 0.6f));
            Art.Part(drone, Art.Cube, dark, Vector3.zero, new Vector3(1.4f, 0.05f, 0.08f), new Vector3(0f, 45f, 0f));
            Art.Part(drone, Art.Cube, dark, Vector3.zero, new Vector3(1.4f, 0.05f, 0.08f), new Vector3(0f, -45f, 0f));
            rotors = new Transform[4];
            Material blade = Art.Mat(new Color(0.75f, 0.78f, 0.8f, 1f), 0.5f);
            for (int i = 0; i < 4; i++)
            {
                float a = (45f + i * 90f) * Mathf.Deg2Rad;
                rotors[i] = Art.Node(drone, "rotor", new Vector3(Mathf.Sin(a) * 0.5f, 0.08f, Mathf.Cos(a) * 0.5f));
                Art.Part(rotors[i], Art.Cube, blade, Vector3.zero, new Vector3(0.5f, 0.01f, 0.05f), default, false);
            }
            Art.Part(drone, Art.Sphere, dark, new Vector3(0f, -0.14f, 0.15f), Vector3.one * 0.16f);
            droneLed = Art.Part(drone, Art.Sphere, Art.Glow(Pal.Rouge, Pal.Rouge * 5f), new Vector3(0f, 0.05f, 0.32f), Vector3.one * 0.09f, default, false);
            // drone 3D (Meshy) : les formes simples sont masquées, le gyrophare et la lumière restent
            if (GlbModel.Exists("dec_drone"))
            {
                foreach (MeshRenderer mr in drone.GetComponentsInChildren<MeshRenderer>()) if (mr != droneLed) mr.enabled = false;
                Transform dm = GlbModel.Spawn("dec_drone", drone);
                Bounds db = dm.GetComponentsInChildren<MeshFilter>()[0].sharedMesh.bounds;
                float dk = 1.5f / Mathf.Max(0.001f, Mathf.Max(db.size.x, db.size.z));
                dm.localScale = Vector3.one * dk;
                dm.localRotation = Quaternion.Euler(0f, 180f, 0f);
                dm.localPosition = -(dm.localRotation * (db.center * dk));
            }
            droneLight = Art.Node(drone, "led", new Vector3(0f, -0.2f, 0.4f)).gameObject.AddComponent<Light>();
            droneLight.type = LightType.Point;
            droneLight.color = new Color(1f, 0.15f, 0.1f);
            droneLight.range = 6f;
            droneLight.intensity = 2f;
            droneLight.enabled = false;
        }

        // ================================================================== états

        void ToMenu()
        {
            demo = false;
            if (P != null) P.SetVisible(true);   // caché en entrant dans le MEGA STORE (arcade)
            if (store != null) { Destroy(store); store = null; }
            pendingEnd = null;
            warn.gameObject.SetActive(false);
            warn2.gameObject.SetActive(false);
            tipSlow = false;
            tip = -1;
            gearGap = 110f;
            ClearBolts();
            ClearSquad();
            for (int i = 0; i < gear.Length; i++) gear[i] = false;
            ApplyGear();
            Time.timeScale = 1f;
            st = St.Menu;
            ClearEnts();
            ResetPlayerRig();
            speed = 7.5f;
            lane = 1;
            px = 0f;
            py = 0f;
            buddy.root.gameObject.SetActive(false);
            // retour au menu : on affiche directement le niveau où se poursuit la progression
            int nx = Progress.NextStreet(thief);
            if (pendingMapAnim < 0 && nx >= 0 && Progress.MapUnlocked(thief, Tuning.MapOf(nx)) && (viewMap < 0 || Progress.ConqueredIn(thief, viewMap) >= Tuning.MapCount[viewMap]))
            {
                viewMap = Tuning.MapOf(nx);
                selStreet = nx;
            }
            ShowMapScreen(pendingMapAnim);
            pendingMapAnim = -1;
        }

        // ------------------------------------------------------------------ améliorations entre les parties

        const string KBank = "sr_bank";
        static readonly string[] UpKeys = { "sr_up_helmet", "sr_up_vest", "sr_up_baton", "sr_up_mates" };
        static readonly int[] UpMax = { 1, 1, 1, 3 };

        static int UpLevel(int id) => PlayerPrefs.GetInt(UpKeys[id], 0);
        static int UpCost(int id)
        {
            int lv = UpLevel(id);
            if (lv >= UpMax[id]) return -1;
            switch (id)
            {
                case 0: return 40;
                case 1: return 100;
                case 2: return 200;
                default: return 120 * (lv + 1);
            }
        }

        void ShowMenu()
        {
            var levels = new int[UpKeys.Length];
            var costs = new int[UpKeys.Length];
            for (int i = 0; i < UpKeys.Length; i++) { levels[i] = UpLevel(i); costs[i] = UpCost(i); }
            hud.ShowMenu(PlayerPrefs.GetInt("sr_best_guard", 0), PlayerPrefs.GetInt("sr_best_thief", 0), PlayerPrefs.GetInt(KBank, 0), levels, costs);
        }

        void Buy(int id)
        {
            int cost = UpCost(id);
            int bank = PlayerPrefs.GetInt(KBank, 0);
            if (cost < 0) return;
            if (bank < cost)
            {
                hud.Toast(L("NOT ENOUGH COINS", "PAS ASSEZ DE PIÈCES"));
                Snd.Play(Snd.I.hurt, 0.3f);
                return;
            }
            PlayerPrefs.SetInt(KBank, bank - cost);
            PlayerPrefs.SetInt(UpKeys[id], UpLevel(id) + 1);
            PlayerPrefs.Save();
            Snd.Play(Snd.I.pickup, 0.8f, 0f);
            hud.Flash(0.2f);
            ShowMenu();
        }

        // ------------------------------------------------------------------ démo au lancement

        /// <summary>Quelques secondes de jeu en pilote automatique pour montrer le gameplay, avant le menu.</summary>
        void StartDemo()
        {
            // la démo alterne les camps d'un lancement à l'autre : vigiles, puis bande de casseurs
            int n = PlayerPrefs.GetInt("sr_demo_n", 0);
            PlayerPrefs.SetInt("sr_demo_n", n + 1);
            PlayerPrefs.Save();
            thief = n % 2 == 1;
            StartRun(true);
        }

        void StartGame() { StartRun(false); }

        void StartRun(bool isDemo)
        {
            P.root.gameObject.SetActive(true);
            metaScreen = MetaScreen.None;
            pendingEnd = null;
            demo = isDemo;
            demoT = 0f;
            autoT = 0f;
            Time.timeScale = 1f;
            ClearEnts();
            ClearSquad();
            ResetPlayerRig();
            if (buddy.kind != (thief ? Kind.Thief : Kind.Guard))
            {
                Destroy(buddy.root.gameObject);
                Rig.OutlineOverride = Art.OutlineTint(AllyBlue);
                buddy = Rig.Build(thief ? Kind.Thief : Kind.Guard, world, new Vector3(0f, 0f, pz - 50f), 0f, thief ? Rig.Crew : 0);
                Rig.OutlineOverride = null;
                AllyRing(buddy);
                buddy.root.gameObject.SetActive(false);
            }
            hud.SetMode(thief);
            st = St.Play;
            lane = 1; px = 0f; py = 0f; vy = 0f; slide = 0f; inv = 0f; stumble = 0f; swingCd = 0f; speed = 11f;
            dist = 0f; score = 0f; comboT = 0f; ren = 0f; shootCd = 0f; combo = 0; coins = 0; hearts = MaxHearts;
            rec = false; recT = Random.Range(12f, 16f); tickT = 0f; loupT = Random.Range(28f, 36f); hintT = 6f;
            koCount = missed = bavures = filmed = loups = maxCombo = 0;
            pendings.Clear();
            cause = "";
            ClearBolts();
            for (int i = 0; i < gear.Length; i++) gear[i] = false;
            swingCount = 0;
            smgT = 0f;
            starsKO = starsPickup = starsPerfect = starsCombo = starsStores = storesSaved = 0;
            medkitsTaken = 0; hpAtBoss = -1; runT = 0f; hp25 = hp50 = hp75 = -1; hitLog.Length = 0;
            hud.ResetHealth();
            superCount = 0;
            superQueued = superUsed = false;
            landT = lastTapT = -9f;
            stage = 1;
            stageStartZ = pz;
            for (int i = 0; i < tipDone.Length; i++) tipDone[i] = false;
            tip = -1;
            tipSlow = false;
            tipEnt = null;
            if (!isDemo)
            {
                int runs = PlayerPrefs.GetInt("sr_runs", 0);
                tutorial = runs < 3;
                PlayerPrefs.SetInt("sr_runs", runs + 1);
                PlayerPrefs.Save();
            }
            boss = null;
            bossWind = -1f;
            stageToastT = 0f;
            warn.gameObject.SetActive(false);
            warn2.gameObject.SetActive(false);
            bossCharging = false;
            // ordre des boss : le premier est toujours un lanceur (le plus simple), les autres au hasard
            bossOrder[0] = Random.value < 0.5f ? 0 : 3;
            int bk = 1;
            for (int b = 0; b < 6; b++) if (b != bossOrder[0]) bossOrder[bk++] = b;
            for (int i = 5; i > 1; i--) { int j = Random.Range(1, i + 1); int tmp = bossOrder[i]; bossOrder[i] = bossOrder[j]; bossOrder[j] = tmp; }

            if (demo)
            {
                // démo : déjà bien équipé, une petite escouade, et du monde tout de suite
                gear[GHelmet] = gear[GVest] = gear[GBaton] = true;
                ApplyGear();
                AddMates(2);
                hintT = 0f;
                speed = 13f;
                gearGap = 34f;
                nextGearZ = pz + 52f;
                nextSpawnZ = pz + 62f;
                SpawnPack(1, pz + 22f, 4);
                SpawnRecruits(0, pz + 36f, 3);
                SpawnPack(2, pz + 44f, 3);
                hud.ShowDemo();
                hud.Ticker(M(L("Black Friday Rush: stop the robbers, recruit your squad, gear up!", "Black Friday Rush : arrête les voleurs, recrute ton escouade, équipe-toi !"), L("Black Friday Rush: lead your crew, steal the cops' gear!", "Black Friday Rush : mène ta bande, vole l'équipement des policiers !")), true);
                return;
            }

            // équipement permanent, cœurs, rue et difficulté (voir GameCampaign)
            ApplyRunConfig();
            if (Tuning.Arcade) ArcadeStart();
            gearGap = campaign && !tutorialMode ? BalanceConfig.I.gearGap[Diff] : 110f;
            nextSpawnZ = pz + 60f;
            nextGearZ = pz + 55f;
            if (tutorialMode) { StartTutorialRun(); return; }
            Spawn(ET.Casseur, 1, pz + 30f, Mode.Casse);
            for (int i = 0; i < 4; i++) Spawn(ET.Piece, 1, pz + 40f + i * 2f);
            hud.ShowPlay();
            hud.Toast(Tuning.StreetNames[streetIdx]);
            rankToastT = 1.6f;
            if (plan != null) hud.Float(new Vector3(px, 3.2f, pz + 6f), "LAYOUT: " + Balance.VariantNames[runVariant], Hud.Info, false);
            hud.Ticker(M(L("Take back ", "Reprends ") + Tuning.StreetNames[streetIdx] + L(": stop the robbers and beat their boss!", " : arrête les voleurs et bats leur boss !"),
                         L("Take over ", "Prends ") + Tuning.StreetNames[streetIdx] + L(": get past security and beat their boss!", " : passe la sécurité et bats leur boss !")), true);
            Snd.Play(Snd.I.siren, 0.5f, 0f);
        }

        void SetPause(bool on)
        {
            if (on && st != St.Play) return;
            if (!on && st != St.Pause) return;
            st = on ? St.Pause : St.Play;
            Time.timeScale = on ? 0f : 1f;
            hud.ShowPause(on);
        }

        void EndGame()
        {
            ClearBolts();
            ents.RemoveAll(x => x.dead);
            warn.gameObject.SetActive(false);
            warn2.gameObject.SetActive(false);
            tipSlow = false;
            tip = -1;
            st = St.End;
            endT = 0f;
            P.KO(new Vector3(0f, 3f, speed * 0.5f), new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-3f, 3f)));
            slowMo = 0.8f;
            Snd.Play(Snd.I.crash, 0.9f);
            Haptics.Heavy();
            if (campaign) { pendingEnd = ShowDefeat; return; }
            int sc = Mathf.FloorToInt(score);
            string bestKey = thief ? "sr_best_thief" : "sr_best_guard";
            int best = PlayerPrefs.GetInt(bestKey, 0);
            if (sc > best) { PlayerPrefs.SetInt(bestKey, sc); PlayerPrefs.Save(); }

            string km = (dist / 1000f).ToString("0.0").Replace('.', ',');
            string title;
            if (thief)
            {
                if (loups > 0) title = L("Stage " + stage + " reached, " + loups + " boss" + (loups > 1 ? "es" : "") + " beaten!", "Étape " + stage + " atteinte, " + loups + " boss battu" + (loups > 1 ? "s" : "") + " !");
                else if (coins >= 60) title = L("I grabbed " + coins + " coins on Black Friday", "J'ai raflé " + coins + " pièces pour le Black Friday");
                else if (koCount >= 10) title = L("I shoved " + koCount + " cops out of my way", "J'ai bousculé " + koCount + " policiers sur mon passage");
                else title = L("Caught at " + Mathf.FloorToInt(dist) + " m", "Attrapé à " + Mathf.FloorToInt(dist) + " m");
            }
            else
            {
                if (loups > 0) title = L("Stage " + stage + " reached, " + loups + " boss" + (loups > 1 ? "es" : "") + " beaten!", "Étape " + stage + " atteinte, " + loups + " boss battu" + (loups > 1 ? "s" : "") + " !");
                else if (koCount >= 20 && bavures == 0) title = L(koCount + " robbers stopped. Zero innocent shoppers hurt.", koCount + " voleurs arrêtés. Zéro client blessé.");
                else if (koCount >= 10) title = L("I stopped " + koCount + " robbers in " + km + " km", "J'ai arrêté " + koCount + " voleurs en " + km + " km");
                else title = L("Shift over at " + Mathf.FloorToInt(dist) + " m", "Service terminé à " + Mathf.FloorToInt(dist) + " m");
            }
            int bank = PlayerPrefs.GetInt(KBank, 0) + coins;
            PlayerPrefs.SetInt(KBank, bank);
            PlayerPrefs.Save();
            string verdict = M(L("Knocked out: ", "K.-O. : ") + cause.ToLowerInvariant() + ".",
                               L("Busted: ", "Pris : ") + cause.ToLowerInvariant() + ".") +
                             "\n<color=#F9D61F>+" + coins + L(" coins</color> · bank ", " pièces</color> · banque ") + bank + L(". Upgrades in the menu!", ". Améliorations dans le menu !");
            string labels = M(L("Best\nStage\nBest combo\nBosses beaten\nThieves escaped\nWrong person hit\nCoins", "Record\nÉtape\nMeilleur combo\nBoss battus\nVoleurs échappés\nMauvaises cibles\nPièces"),
                              L("Best\nStage\nBest combo\nBosses beaten\nGuards dodged\nWrong person hit\nCoins", "Record\nÉtape\nMeilleur combo\nBoss battus\nGardes esquivés\nMauvaises cibles\nPièces"));
            string values = (sc > best ? L("New record!", "Nouveau record !") : best.ToString()) + "\n" + stage + " (" + Mathf.FloorToInt(dist) + " m)\n" + maxCombo + "\n" +
                            loups + "\n" + missed + "\n" + bavures + "\n" + coins;
            pendingEnd = () => hud.ShowEnd(title, verdict, koCount, sc, labels, values);
        }

        System.Action pendingEnd;

        void ResetPlayerRig()
        {
            Kind want = thief ? Kind.Thief : Kind.Guard;
            if (P != null && (P.ko || P.kind != want))
            {
                Destroy(P.root.gameObject);
                P = Rig.Build(want, world, new Vector3(px, 0f, pz), 0f, -1);
            }
            P.SetVisible(true);
        }

        void ClearEnts()
        {
            foreach (Ent e in ents) if (e.go != null) Destroy(e.go);
            ents.Clear();
            foreach (StoreRaid r in raids) if (r.beacon != null) Destroy(r.beacon.gameObject);
            raids.Clear();
        }

        // ================================================================== boucle

        void Update()
        {
            float udt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            DevTick(udt);
            if (st != St.Pause)
            {
                if (hitStop > 0f) hitStop -= udt;
                else if (slowMo > 0f) slowMo -= udt;
                // arcade : pas d'arrêt sur image à chaque coup (avec la foule qui frappe en continu, ça ressemble à du lag)
                if (Tuning.Arcade) hitStop = 0f;
                Time.timeScale = hitStop > 0f ? 0.06f : slowMo > 0f ? 0.3f : (tipSlow && st == St.Play) ? 0.35f : 1f;
            }
            float dt = Time.deltaTime;

            Perf.Frame();
            HandleInput();
            Perf.Mark("input");

            switch (st)
            {
                case St.Menu: MenuTick(dt); break;
                case St.Play: PlayTick(dt); break;
                case St.End: EndTick(dt, udt); break;
            }

            Perf.Mark("tick-rest");
            street.Tick(pz);
            Perf.Mark("street");
            UpdateCamera(udt);
            UpdateAmbience(dt);
            PostFx.Tick(udt);
            Perf.Mark("cam+fx");
            hud.BeginTags();
            if (st == St.Play && Tuning.Arcade) ArcadeTags();
            hud.EndTags();
            hud.Tick(udt);
            if (st == St.Play)
            {
                hud.SetPlay(Mathf.FloorToInt(dist), Mathf.FloorToInt(score), hearts, koCount, ComboMult(), rec, gear, squad.Count, shieldHits, shieldT);
                hud.SetStars(coins);
            }
            Perf.Mark("hud");
        }

        void HandleInput()
        {
            if (demo)
            {
                // pendant la démo, toucher l'écran (ou une touche) ouvre le menu
                if (inp.taps.Count > 0 || inp.keyStart || inp.keySwing || inp.swipes.Count > 0) ToMenu();
                return;
            }
            foreach (Vector2 tap in inp.taps)
            {
                if (hud.Click(tap)) continue;
                if (st == St.Play && !Tuning.Arcade) Swing();
            }
            if (inp.keyPause)
            {
                if (st == St.Play) SetPause(true);
                else if (st == St.Pause) SetPause(false);
            }
            if (inp.keyStart && st == St.Menu && metaScreen == MetaScreen.Map) hud.onPlayStreet?.Invoke();
            if (st != St.Play) return;
            if (inp.keySwing && !Tuning.Arcade) Swing();
            if (inp.taps.Count > 0 || inp.keySwing) actSwing = true;
            if (inputDebug)
            {
                if (inp.taps.Count > 0) hud.InputDebug("TAP = ATTACK");
                foreach (int sw in inp.swipes)
                    hud.InputDebug(sw == InputCtl.Left ? "SWIPE LEFT" : sw == InputCtl.Right ? "SWIPE RIGHT" : sw == InputCtl.Up ? "SWIPE UP = JUMP" : "SWIPE DOWN = SLIDE");
            }
            foreach (int s in inp.swipes)
            {
                hintT = 0f;
                if (Tuning.Arcade && (s == InputCtl.Up || s == InputCtl.Down)) continue;   // arcade : un seul geste, gauche / droite
                if (s == InputCtl.Left || s == InputCtl.Right) actLane = true;
                else if (s == InputCtl.Up) actJump = true;
                else actSlide = true;
                switch (s)
                {
                    case InputCtl.Left:
                    case InputCtl.Right:
                    {
                        int nl = Mathf.Clamp(lane + (s == InputCtl.Left ? -1 : 1), 0, 2);
                        if (nl != lane)
                        {
                            lane = nl;
                            Snd.PlayPitch(Snd.I.whoosh, 0.22f, 1.35f);
                            if (py < 0.05f) Fx.Skid(new Vector3(px, 0.08f, pz));
                        }
                        break;
                    }
                    case InputCtl.Up:
                        if (py <= 0.01f)
                        {
                            vy = 12.5f; slide = 0f; Snd.Play(Snd.I.jump, 0.6f);
                            P.Takeoff();
                            foreach (Mate m in squad) m.rig.Takeoff();
                            Fx.Skid(new Vector3(px, 0.08f, pz));
                        }
                        break;
                    case InputCtl.Down:
                        if (py > 0.01f) { vy = -24f; rollOnLand = true; }
                        else if (slide <= 0f) P.Roll();
                        slide = Tuning.SlideTime;
                        Snd.Play(Snd.I.land, 0.5f);
                        break;
                }
            }
        }

        void MenuTick(float dt)
        {
            px = Mathf.Lerp(px, 0f, dt * 3f);
            // sur la carte, la fiche joueur remplace le personnage (il reste visible dans les améliorations)
            bool showRig = metaScreen != MetaScreen.Map;
            if (P.root.gameObject.activeSelf != showRig) P.root.gameObject.SetActive(showRig);
            if (metaScreen == MetaScreen.Upgrades)
            {
                // aperçu de l'équipement : le personnage se tourne doucement face à la caméra
                P.anim = Anim.Stand;
                P.root.localRotation = Quaternion.Euler(0f, 25f * Mathf.Sin(Time.unscaledTime * 0.8f), 0f);
            }
            else
            {
                pz += speed * dt;
                P.anim = Anim.Run;
                P.root.localRotation = Quaternion.identity;
            }
            P.speed = speed;
            P.root.localPosition = new Vector3(px, 0f, pz);
            P.Tick(dt);
        }

        void EndTick(float dt, float udt)
        {
            endT += udt;
            if (runWon) P.anim = Anim.Cheer;
            P.Tick(dt);
            foreach (Ent e in ents) if (!e.dead && e.go != null) TickVisual(e, dt);
            foreach (Mate m in squad) { m.rig.anim = Anim.Stand; m.rig.Tick(dt); }
            // fin de course : on passe vite au bilan (pas de temps mort)
            if (endT > 0.75f && pendingEnd != null)
            {
                pendingEnd();
                pendingEnd = null;
            }
        }

        void PlayTick(float dt)
        {
            // vitesse qui monte avec la distance
            // journal d'équilibrage : temps de course, vie à 25 / 50 / 75 % du parcours
            runT += dt;
            if (campaign && !tutorialMode && StageLen > 1f)
            {
                float fr = (pz - stageStartZ) / StageLen;
                if (hp25 < 0 && fr >= 0.25f) hp25 = hearts;
                if (hp50 < 0 && fr >= 0.5f) hp50 = hearts;
                if (hp75 < 0 && fr >= 0.75f) hp75 = hearts;
            }
            float target = tutorialMode ? Tuning.TutorialSpeed : Mathf.Min(Tuning.MaxSpeed[Diff], Tuning.StartSpeed[Diff] + dist / 60f);
            float want = stumble > 0f ? target * 0.6f : target;
            speed = Mathf.MoveTowards(speed, want, (stumble > 0f ? 30f : 5f) * dt);
            float dz = speed * dt;
            pz += dz;
            dist += dz;
            score += dz * 0.5f;

            if (shootCd > 0f) shootCd -= dt;
            if (ren > 0f) ren -= dt;
            if (comboT > 0f) { comboT -= dt; if (comboT <= 0f) combo = 0; }
            if (stumble > 0f) stumble -= dt;
            if (inv > 0f) inv -= dt;
            if (perfectGuardT > 0f) perfectGuardT -= dt;
            if (shieldT > 0f)
            {
                // bouclier de course limité dans le temps
                shieldT -= dt;
                if (shieldT <= 0f && gear[GShield])
                {
                    gear[GShield] = false;
                    ApplyGear();
                    Fx.Debris(new Vector3(px, 1.3f, pz + 0.4f));
                    hud.Float(new Vector3(px, 2.4f, pz), L("SHIELD BROKE", "BOUCLIER CASSÉ"), Hud.Info, true);
                    Snd.PlayPitch(Snd.I.glass, 0.5f, 0.8f);
                }
            }
            if (swingCd > 0f) swingCd -= dt;
            if (coinStreakT > 0f) { coinStreakT -= dt; if (coinStreakT <= 0f) coinStreak = 0; }
            if (hintT > 0f) { hintT -= dt; hud.Hint(hintT > 0f); }
            else hud.Hint(false);
            hud.TapHint(false);
            hud.nextGearSlot = MissingGear();
            if (weaponIntroT > 0f && tip < 0)
            {
                weaponIntroT -= dt;
                if (weaponIntroT <= 0f)
                {
                    // première course avec la matraque renforcée : on la présente
                    PlayerPrefs.SetInt("sr_weapon2_seen", 1);
                    ShowTip(TWeapon, 0, M(L("NEW WEAPON: REINFORCED BATON", "NOUVELLE ARME : MATRAQUE RENFORCÉE"), L("NEW WEAPON: IRON BAR", "NOUVELLE ARME : BARRE DE FER")),
                            M(L("Hits harder and wider: smash whole groups!", "Frappe plus fort et plus large : écrase des groupes entiers !"), L("Hits harder and wider, and TAP to smash obstacles!", "Frappe plus fort et plus large, et TAPE pour casser les obstacles !")), null, false);
                    hud.Toast(L("POWER UP!", "PLUS FORT !"));
                    P.Flash(Hud.StarGold, 0.3f);
                    Fx.Confetti(new Vector3(px, 1.6f, pz + 1f));
                    Snd.Play(Snd.I.pickup, 0.8f, 0f);
                }
            }
            if (rankToastT > 0f)
            {
                rankToastT -= dt;
                if (rankToastT <= 0f && campaign && !tutorialMode && !demo)
                {
                    int m = Tuning.MapOf(streetIdx);
                    string[] ranks = thief ? Tuning.RankThief : Tuning.RankGuard;
                    hud.Float(new Vector3(px, 2.8f, pz + 2f), L("RANK: ", "GRADE : ") + ranks[Mathf.Min(m, ranks.Length - 1)], thief ? Hud.ThiefRed : Hud.GuardBlue, true);
                }
            }
            if (!demo && !tutorialMode && !Tuning.Arcade) UpdateTips();
            if (demo)
            {
                demoT += Time.unscaledDeltaTime;
                hud.DemoCaption(demoT);
                if (demoT > 14f) { ToMenu(); return; }
                Autopilot(dt);
            }

            // déplacement latéral et vertical
            float tx = (lane - 1) * Lane;
            px = Mathf.Lerp(px, tx, 1f - Mathf.Exp(-(thief ? Tuning.ThiefLaneSpeed : Tuning.GuardLaneSpeed) * dt));
            bool air = py > 0.01f;
            vy -= 38f * dt;
            py += vy * dt;
            if (py <= 0f)
            {
                py = 0f;
                if (air)
                {
                    landT = Time.time;
                    if (superQueued) { superQueued = false; SuperStrike(); }
                }
                if (air && vy < -4f)
                {
                    P.Land(Mathf.Clamp(-vy * 0.016f, 0.14f, 0.3f));
                    Fx.Dust(new Vector3(px, 0.1f, pz));
                    Fx.Skid(new Vector3(px, 0.08f, pz));
                    if (rollOnLand && slide > 0f) P.Roll();
                }
                rollOnLand = false;
                vy = 0f;
            }
            if (slide > 0f) slide -= dt;
            if (py > 0.3f) superUsed = false;
            float h = (slide > 0f && py < 0.05f) ? 1f : 2.1f;

            P.speed = speed;
            P.vy = vy;
            P.lean = (tx - px) * 9f;
            P.anim = py > 0.05f ? Anim.Jump : (slide > 0f ? Anim.Slide : Anim.Run);
            P.root.localPosition = new Vector3(px, py, pz);
            P.root.localRotation = Quaternion.Euler(0f, (tx - px) * 7f, (tx - px) * -6f);
            P.SetVisible(inv <= 0f || Mathf.Repeat(Time.unscaledTime * 14f, 1f) < 0.5f);
            P.Tick(dt);
            NinjaCue();
            if (P.stepped && py < 0.05f && slide <= 0f) Fx.Step(new Vector3(px, 0.06f, pz - 0.1f));

            // coup de matraque : l'impact tombe au moment où le bras descend
            for (int i = pendings.Count - 1; i >= 0; i--)
            {
                PendingHit ph = pendings[i];
                ph.t -= dt;
                if (ph.t > 0f) continue;
                pendings.RemoveAt(i);
                Ent e = ph.e;
                bool inRange = ph.super || (Mathf.Abs(e.x - px) < 2.3f && e.z - pz > -1.2f && e.z - pz < 6.8f);
                if (!e.hit && !e.dead && inRange)
                {
                    if (e.type == ET.Manif) Bavure(e);
                    else
                    {
                        superHitNow = ph.super;
                        meleeHitNow = true;
                        HitCasseur(e, false, ph.power, false, ph.dmg);
                        superHitNow = meleeHitNow = false;
                    }
                }
                if (st != St.Play) return;
            }

            // drone de Canal Info
            recT -= dt;
            if (recT <= 0f)
            {
                rec = !rec;
                recT = rec ? Random.Range(5f, 7f) : Random.Range(12f, 18f);
                rec = false;
            }

            Perf.Mark("player");
            UpdateBuddy(dt);
            UpdateBolts(dt);
            Perf.Mark("bolts");
            if (st != St.Play) return;
            UpdateRaids();
            UpdateSmg(dt);
            Perf.Mark("smg+melee");
            UpdateSquad(dt, h);
            Perf.Mark("squad");
            if (st != St.Play) return;
            if (Tuning.Arcade) ArcadeTick(dt);
            Perf.Mark("arcade");

            UpdateStage(dt);
            if (st != St.Play) return;

            // génération devant
            if (tutorialMode) TutorialTick();
            else while (nextSpawnZ < pz + 110f)
            {
                float a = Tuning.Arcade ? ArcadeSpawn(nextSpawnZ) : 0f;
                if (a > 0f) nextSpawnZ += a;
                else if (Tuning.Arcade && campaign && !demo && nextSpawnZ - stageStartZ <= StageLen) nextSpawnZ += ArcadeRow(nextSpawnZ, ShuffledLanes());
                else nextSpawnZ += SpawnRow(nextSpawnZ);
            }
            Perf.Mark("spawn");
            if (st != St.Play) return;

            for (int i = 0; i < ents.Count; i++)
            {
                UpdateEnt(ents[i], dt, h);
                if (st != St.Play) return;
            }
            ents.RemoveAll(e => e.dead);
            Perf.Mark("ents");

            tickT += dt;
            if (tickT > 9f)
            {
                tickT = 0f;
                hud.Ticker(thief ? IdleThief[idle++ % IdleThief.Length] : Idle[idle++ % Idle.Length], false);
            }
        }

        // ================================================================== entités

        void UpdateEnt(Ent e, float dt, float h)
        {
            switch (e.type)
            {
                case ET.Casseur:
                    if (!e.hit)
                    {
                        if (e.mode == Mode.Fonce && e.z - pz < 45f)
                        {
                            e.z -= e.vz * dt;
                            e.rig.anim = e.z - pz < 16f ? Anim.Charge : Anim.Run;
                            e.rig.speed = e.vz;
                        }
                        else if (e.mode == Mode.Sortie && e.z - pz < 34f)
                        {
                            // pillard qui jaillit d'une boutique : il court en diagonale vers son couloir
                            float tx = (e.lane - 1) * Lane;
                            e.x = Mathf.MoveTowards(e.x, tx, 6.5f * dt);
                            e.z -= 2.5f * dt;
                            e.rig.anim = Anim.Run;
                            e.rig.speed = 7f;
                            e.rig.root.localRotation = Quaternion.Euler(0f, 180f + Mathf.Clamp((e.x - tx) * 14f, -55f, 55f), 0f);
                            if (Mathf.Abs(e.x - tx) < 0.05f) { e.mode = Mode.Fonce; e.rig.root.localRotation = Quaternion.Euler(0f, 180f, 0f); }
                        }
                        else if (e.mode == Mode.Lance)
                        {
                            e.timer -= dt;
                            float d = e.z - pz;
                            if (e.timer <= 0f && d < 40f && d > 12f)
                            {
                                e.timer = Random.Range(1.8f, 2.6f);
                                e.rig.throwT = 0f;
                                Ent pave = Spawn(ET.Pave, e.lane, e.z - 0.8f);
                                pave.y = 1.7f;
                            }
                        }
                    }
                    break;

                case ET.Loup:
                    if (!e.hit) UpdateBoss(e, dt);
                    break;

                case ET.Onde:
                    // onde de choc du boss : couvre toute la largeur, il faut sauter
                    e.x = px;
                    e.timer += dt;
                    if (e.spin != null) e.spin.localScale = new Vector3(1f, 1f + Mathf.Sin(e.timer * 30f) * 0.25f, 1f);
                    break;

                case ET.Recrue:
                    e.timer -= dt;
                    if (e.timer <= 0f && e.z - pz < 34f && e.z - pz > 3f)
                    {
                        e.timer = 0.75f;
                        hud.Float(new Vector3(e.x, 2.1f, e.z), "+" + e.hp, Hud.Good, true);
                    }
                    break;

                case ET.Pave:
                    if (!e.hit)
                    {
                        e.z -= (e.vz > 0f ? e.vz : 15f) * dt;
                        e.y = Mathf.Max(1.1f, e.y - dt * 0.4f);
                        e.spin.Rotate(400f * dt, 250f * dt, 0f, Space.Self);
                    }
                    break;
            }

            // une fois touché, l'objet appartient à la physique : on ne le replace plus
            if (!e.hit && e.go != null) e.go.transform.localPosition = new Vector3(e.x, e.y, e.z);

            TickVisual(e, dt);

            if (!e.hit && !e.dead && Mathf.Abs(e.z - pz) < e.depth + 0.35f && Mathf.Abs(e.x - px) < 1.05f) Collide(e, h);
            if (st != St.Play) return;

            if (e.type == ET.Casseur && !e.hit && e.z < pz - 2f)
            {
                e.hit = true;
                missed++;
                RaidMemberGone(e, false);
                if (Random.value < 0.3f) hud.Float(new Vector3(px, 2f, pz + 2f), M(L("A robber got away", "Un voleur s'est échappé"), L("A cop is on your tail", "Un policier te colle aux basques")), Hud.Info);
            }
            if (!e.dead && FarZ(e) < pz - 14f)
            {
                e.dead = true;
                if (e.go != null) Destroy(e.go);
            }
        }

        /// <summary>Position la plus avancée de l'entité (les corps projetés peuvent filer devant).</summary>
        static float FarZ(Ent e)
        {
            float z = e.z;
            if (e.go != null) z = Mathf.Max(z, e.go.transform.position.z);
            if (e.rig != null && e.rig.root != null) z = Mathf.Max(z, e.rig.root.position.z);
            if (e.crowd != null) foreach (Rig r in e.crowd) if (r.root != null) z = Mathf.Max(z, r.root.position.z);
            return z;
        }

        void TickVisual(Ent e, float dt)
        {
            if (e.ring != null && e.hit) { Destroy(e.ring); e.ring = null; }
            if (e.rig != null) e.rig.Tick(dt);
            if (e.crowd != null) foreach (Rig r in e.crowd) r.Tick(dt);
            if (e.spin != null && e.type != ET.Pave)
            {
                e.bob += dt;
                e.spin.localRotation = Quaternion.Euler(0f, e.bob * 160f, 0f);
                e.spin.localPosition = new Vector3(0f, Mathf.Sin(e.bob * 3f) * 0.12f, 0f);
            }
        }

        void Swing()
        {
            if (st != St.Play) return;
            hintT = 0f;

            // super coup ninja : tap juste avant (en l'air) ou juste après l'atterrissage, sans spammer
            float now = Time.time;
            bool spam = now - lastTapT < Tuning.AntiSpam;
            lastTapT = now;
            bool air = py > 0.05f;
            if (!spam && !superUsed && !superQueued)
            {
                if (air && TimeToLand() <= Tuning.PerfectEarly) { superQueued = true; return; }
                if (!air && now - landT <= Tuning.PerfectLate) { SuperStrike(); return; }
            }
            if (inputDebug) hud.InputDebug(air ? "AIR HIT x" + Tuning.AirMult + (spam ? " (spam)" : "") : "TAP" + (spam ? " (spam)" : ""));

            float reach = Heavy ? 6.3f : gear[GBaton] ? 5.8f : 3.4f;
            float range = gear[GPistol] && !gear[GSmg] ? 32f : reach;

            // cible la plus proche dans le couloir (un client compris : attention à qui on vise)
            Ent best = null;
            float bd = 1e9f;
            foreach (Ent e in ents)
            {
                if (e.hit || e.dead) continue;
                if (e.type != ET.Casseur && e.type != ET.Loup && e.type != ET.Manif) continue;
                if (Mathf.Abs(e.x - px) > 1.2f) continue;
                float d = e.z - pz;
                if (d < -0.8f || d > range) continue;
                if (d < bd) { bd = d; best = e; }
            }

            // trop loin pour la matraque : un tir de pistolet
            if (best != null && bd > reach)
            {
                if (shootCd > 0f) return;
                shootCd = 0.3f;
                Shoot(best, false);
                return;
            }

            if (swingCd > 0f) return;
            swingCd = Tuning.SwingCooldown;
            int style = swingCount++ % 3;
            P.swingStyle = style;
            P.swingT = 0f;
            Snd.PlayPitch(Snd.I.whoosh, Heavy ? 0.9f : 0.7f, (style == 2 ? 0.8f : 1f + style * 0.1f) * (Heavy ? 0.72f : 1f));

            // un coup ne touche que quelques adversaires : poings 1, batte / matraque 2, arme renforcée 3
            float width = Heavy ? Tuning.HeavyWidth : gear[GBaton] ? 2f : 1.0f;
            float power = (gear[GBaton] ? 1.5f : 1f) * (style == 2 ? 1.35f : 1f) * (Heavy ? Tuning.HeavyPower : 1f);
            int maxHits = Heavy ? Tuning.HeavyMaxTargets : gear[GBaton] ? Tuning.BatonMaxTargets : Tuning.FistMaxTargets;
            var hits = new List<Ent>();
            foreach (Ent e in ents)
            {
                if (e.hit || e.dead) continue;
                bool foe = e.type == ET.Casseur || e.type == ET.Loup;
                if (!foe && e.type != ET.Manif) continue;
                float d = e.z - pz;
                if (d < -0.8f || d > reach) continue;
                if (Mathf.Abs(e.x - px) > (foe ? width : 1.0f)) continue;
                hits.Add(e);
            }
            hits.Sort((x, y) => x.z.CompareTo(y.z));
            if (thief && Heavy) SmashProps(reach);
            // coup en l'air : dégâts augmentés
            int dmg = BaseDmg * (air ? Tuning.AirMult : 1);
            if (air) power *= 1.2f;
            for (int i = 0; i < hits.Count && i < maxHits; i++)
                pendings.Add(new PendingHit { e = hits[i], t = 0.085f + i * 0.035f, power = power, dmg = dmg });
        }

        /// <summary>Temps (s) avant de toucher le sol (gravité 38 m/s²), 0 au sol.</summary>
        float TimeToLand()
        {
            if (py <= 0.01f) return 0f;
            return (vy + Mathf.Sqrt(vy * vy + 76f * py)) / 38f;
        }

        bool NinjaWindowOpen()
        {
            if (superUsed || st != St.Play) return false;
            return py > 0.05f ? TimeToLand() <= Tuning.CueLead : Time.time - landT <= Tuning.PerfectLate;
        }

        bool InNinjaRange(Ent e)
        {
            if (e.hit || e.dead || (e.type != ET.Casseur && e.type != ET.Loup)) return false;
            float dz = e.z - pz;
            return dz > -1f && dz < Tuning.PerfectReach && Mathf.Abs(e.x - px) < Tuning.PerfectWidth;
        }

        /// <summary>Indice visuel : les ennemis à portée brillent en or pendant la fenêtre du Perfect Hit.</summary>
        void NinjaCue()
        {
            if (!NinjaWindowOpen()) return;
            foreach (Ent e in ents)
                if (InNinjaRange(e) && e.rig != null) e.rig.Flash(NinjaGold, 0.06f);
        }

        /// <summary>
        /// SUPER COUP NINJA : vrille et coup de matraque au sol, onde dorée sur toute la largeur,
        /// dégâts ×3 sur tous les ennemis devant toi (6 au plus, jamais les clients), ralenti et gros impact.
        /// </summary>
        void SuperStrike()
        {
            superUsed = true;
            superCount++;
            perfectGuardT = Tuning.PerfectGuard;
            coins += Tuning.StarsPerfect;
            starsPerfect += Tuning.StarsPerfect;
            swingCd = 0.2f;
            P.swingStyle = 2;
            P.swingT = 0f;
            P.Spin();
            P.Land(0.45f);
            P.Flash(NinjaGold, 0.18f);
            if (!Tuning.Arcade)
            {
                slowMo = Mathf.Max(slowMo, 0.4f);
                zoom = Mathf.Max(zoom, 0.6f);
            }
            shake = Mathf.Max(shake, 0.5f);
            fovKick = Mathf.Max(fovKick, 9f);
            hud.Flash(0.55f);
            Snd.Play(Snd.I.perfect, 1f, 0f);
            Haptics.Pulse(90, true);
            Vector3 feet = new Vector3(px, 0.15f, pz + 1.2f);
            Fx.Ninja(feet);
            string[] calls = { L("NINJA STRIKE!", "COUP NINJA !"), L("PERFECT!", "PARFAIT !"), "SUPER BONK!" };
            hud.Toast(calls[Random.Range(0, calls.Length)]);
            hud.Float(new Vector3(px, 2.6f, pz + 1f), L("PERFECT x", "PARFAIT x") + Tuning.PerfectMult + "  +" + Tuning.StarsPerfect + " " + Tuning.Cur, NinjaGold, true);
            if (inputDebug) hud.InputDebug("PERFECT HIT");

            var hits = new List<Ent>();
            foreach (Ent e in ents) if (InNinjaRange(e)) hits.Add(e);
            hits.Sort((x, y) => x.z.CompareTo(y.z));
            int dmg = BaseDmg * Tuning.PerfectMult;
            for (int i = 0; i < hits.Count && i < 6; i++)
                pendings.Add(new PendingHit { e = hits[i], t = 0.07f + i * 0.03f, power = 2.4f, dmg = dmg, super = true });
            if (tip == TPerfect) EndTip();
            PlayerPrefs.SetInt("sr_perfect_learned", 1);
        }

        void Shoot(Ent target, bool smg, Vector3? from = null)
        {
            Vector3 muzzle;
            if (from.HasValue)
            {
                // tir d'un membre de la bande : discret (pas de son ni de traînée, il y en a beaucoup)
                Transform mt = Art.Node(world, "bolt", from.Value);
                Color mc = Random.value < 0.5f ? new Color(1f, 0.31f, 0.64f) : new Color(0.25f, 0.85f, 0.45f);
                Art.Part(mt, Art.Sphere, Art.Glow(mc, mc * 2f, 0.6f), Vector3.zero, Vector3.one * 0.12f, default, false);
                bolts.Add(new Bolt { tr = mt, target = target, smg = true });
                return;
            }
            if (smg)
            {
                P.Recoil();
                Snd.PlayPitch(Snd.I.shot, 0.32f, Random.Range(0.75f, 0.9f));
                muzzle = P.smg != null ? P.smg.position + P.smg.forward * 0.45f : new Vector3(px, 1.2f, pz + 0.6f);
            }
            else
            {
                P.shootT = 0f;
                Snd.Play(Snd.I.shot, 0.6f);
                muzzle = P.pistol != null ? P.pistol.position + P.pistol.forward * 0.3f : new Vector3(px, 1.2f, pz + 0.6f);
                fovKick = Mathf.Max(fovKick, 1.5f);
            }
            Fx.Sparkle(muzzle);
            Transform tr = Art.Node(world, "bolt", muzzle);
            // projectiles cartoon, non létaux : policiers = décharge du taser / fléchettes en mousse ; voleurs = billes de peinture
            Color c;
            if (thief) c = (smg && (smgShots++ & 1) == 1) ? new Color(0.25f, 0.85f, 0.45f) : new Color(1f, 0.31f, 0.64f);
            else c = smg ? new Color(1f, 0.55f, 0.15f) : new Color(0.45f, 0.95f, 1f);
            if (thief) Art.Part(tr, Art.Sphere, Art.Glow(c, c * 2f, 0.6f), Vector3.zero, Vector3.one * (smg ? 0.12f : 0.16f), default, false);
            else if (smg)
            {
                Art.Part(tr, Art.Capsule, Art.Glow(c, c * 1.5f, 0.4f), Vector3.zero, new Vector3(0.07f, 0.16f, 0.07f), new Vector3(90f, 0f, 0f), false);
                Art.Part(tr, Art.Sphere, Art.Glow(new Color(0.24f, 0.49f, 1f), new Color(0.3f, 0.6f, 1.6f), 0.5f), new Vector3(0f, 0f, 0.15f), Vector3.one * 0.08f, default, false);
            }
            else Art.Part(tr, Art.Capsule, Art.Glow(c, c * 4f, 0.5f), Vector3.zero, new Vector3(0.08f, 0.35f, 0.08f), new Vector3(90f, 0f, 0f), false);
            TrailRenderer trl = Trail(tr);
            trl.emitting = true;
            bolts.Add(new Bolt { tr = tr, target = target, smg = smg });
        }

        static TrailRenderer Trail(Transform tr)
        {
            TrailRenderer t = tr.gameObject.AddComponent<TrailRenderer>();
            t.time = 0.1f;
            t.minVertexDistance = 0.05f;
            t.widthMultiplier = 0.1f;
            t.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            t.material = Art.Particle(null, Color.white);
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Pal.Jaune, 1f) },
                      new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            t.colorGradient = g;
            t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return t;
        }

        /// <summary>Les tirs filent vers leur cible ; au contact, c'est un coup comme un autre (ou une bavure sur un client).</summary>
        void UpdateBolts(float dt)
        {
            for (int i = bolts.Count - 1; i >= 0; i--)
            {
                Bolt b = bolts[i];
                Ent e = b.target;
                if (e == null || e.hit || e.dead || e.go == null)
                {
                    Destroy(b.tr.gameObject);
                    bolts.RemoveAt(i);
                    continue;
                }
                Vector3 aim = new Vector3(e.x, 1.15f, e.z);
                Vector3 d = aim - b.tr.position;
                float step = 75f * dt;
                if (d.magnitude <= step + 0.3f)
                {
                    Destroy(b.tr.gameObject);
                    bolts.RemoveAt(i);
                    Fx.Sparkle(aim);
                    if (e.type == ET.Manif) Bavure(e);
                    else HitCasseur(e, false, b.smg ? 0.9f : 1.2f, b.smg, Tuning.Arcade && runTier >= 3 ? 2 : 1);
                    if (st != St.Play) return;
                    continue;
                }
                b.tr.position += d / d.magnitude * step;
                b.tr.rotation = Quaternion.LookRotation(d);
            }
        }

        void ClearBolts()
        {
            foreach (Bolt b in bolts) if (b.tr != null) Destroy(b.tr.gameObject);
            bolts.Clear();
        }

        /// <summary>Pickups d'équipement en course (temporaires) : plaque d'armure, bouclier, pistolet, mitraillette.</summary>
        static readonly int[] RunGear = { GVest, GShield }; // le pistolet et la mitraillette se gagnent maintenant (ligne Arme)

        int MissingGear()
        {
            foreach (int i in RunGear) if (!gear[i]) return i;
            return -1;
        }

        void ApplyGear()
        {
            if (P != null) P.SetGear(gear);
        }

        void GotGear(int i, string title, string what)
        {
            gear[i] = true;
            ApplyGear();
            P.Land(0.2f);
            hud.Flash(0.25f);
            Fx.Confetti(new Vector3(px, 1.6f, pz));
            int n = 0;
            foreach (bool g in gear) if (g) n++;
            hud.Toast(n == gear.Length ? L("FULLY EQUIPPED!", "ÉQUIPEMENT COMPLET !") : title);
            hud.Float(new Vector3(px, 2.3f, pz), what, Hud.Good);
            hud.Ticker(n == gear.Length
                ? M(L("Fully equipped. No robber gets past you now!", "Équipement complet. Plus aucun voleur ne passe !"), L("You've got all their gear. Nobody can stop you now!", "Tu as tout leur équipement. Plus personne ne t'arrête !"))
                : M(L("Gear ", "Équipement "), L("Stolen gear ", "Équipement volé ")) + n + "/" + gear.Length + ": " + what.ToLower(), false);
        }

        // ================================================================== tutoriel

        void ShowTip(int id, int kind, string title, string sub, Ent target, bool slow)
        {
            if (Tuning.Arcade && !tutorialMode) return;   // arcade : pas de bulles d'explication, le jeu se comprend en jouant
            tip = id;
            tipShownT = 0f;
            tipEnt = target;
            tipSlow = slow;
            actLane = actSwing = actJump = actSlide = false;
            hud.ShowTip(kind, title, sub);
        }

        void EndTip()
        {
            if (tip >= 0) tipDone[tip] = true;
            tip = -1;
            tipSlow = false;
            tipEnt = null;
            hud.HideTip();
        }

        void UpdateTips()
        {
            float udt = Time.unscaledDeltaTime;
            if (tip >= 0)
            {
                tipShownT += udt;
                bool done = false;
                switch (tip)
                {
                    case TLane: done = actLane && tipShownT > 0.6f; break;
                    case TTap: done = actSwing; break;
                    case TJump: done = actJump; break;
                    case TSlide: done = actSlide; break;
                }
                // l'obstacle ou l'adversaire est passé : la consigne n'a plus lieu d'être
                if (tipEnt != null && (tipEnt.dead || (tipEnt.hit && tip != TRecruit) || tipEnt.z < pz - 0.5f)) done = true;
                if (tip == TPerfect && py <= 0.01f && Time.time - landT > 0.3f) done = true;
                if (done || tipShownT > (tip == TLane ? 4f : 3.2f)) EndTip();
                return;
            }

            // le Perfect Hit s'explique la première fois qu'il est possible (jusqu'à ce qu'il soit réussi une fois)
            if (!tipDone[TPerfect] && PlayerPrefs.GetInt("sr_perfect_learned", 0) == 0 && py > 0.3f && vy < 0f && NinjaWindowOpen())
            {
                foreach (Ent e in ents)
                    if (InNinjaRange(e)) { ShowTip(TPerfect, 0, L("TAP AS YOU LAND!", "TAPE À L'ATTERRISSAGE !"), L("Gold enemies = NINJA STRIKE: triple damage", "Ennemis dorés = COUP NINJA : dégâts x3"), null, true); return; }
            }
            if (!tutorial) return;

            // les gestes de base sont appris dans le tutoriel jouable : ici, seulement les nouveautés
            tipDone[TLane] = tipDone[TTap] = tipDone[TJump] = tipDone[TSlide] = tipDone[TRecruit] = true;
            if (!tipDone[TLane] && dist > 3f) { ShowTip(TLane, 1, L("SWIPE LEFT / RIGHT", "GLISSE À GAUCHE / DROITE"), L("to change lanes", "pour changer de couloir"), null, false); return; }
            foreach (Ent e in ents)
            {
                if (e.hit || e.dead) continue;
                float dz = e.z - pz;
                bool mine = Mathf.Abs(e.x - px) < 1.3f;
                if (!tipDone[TTap] && e.type == ET.Casseur && mine && dz > 2f && dz < 8f)
                { ShowTip(TTap, 0, L("TAP TO HIT!", "TAPE POUR FRAPPER !"), M(L("Knock out the robber in front of you", "Assomme le voleur devant toi"), L("Shove the cop in front of you", "Bouscule le policier devant toi")), e, true); return; }
                if (!tipDone[TJump] && (e.type == ET.Barricade || e.type == ET.Poubelle || e.type == ET.Voiture) && mine && dz > 3f && dz < 8f)
                { ShowTip(TJump, 2, L("SWIPE UP", "GLISSE VERS LE HAUT"), L("to jump over it", "pour sauter par-dessus"), e, true); return; }
                if (!tipDone[TSlide] && e.type == ET.Banderole && mine && dz > 3f && dz < 8f)
                { ShowTip(TSlide, 3, L("SWIPE DOWN", "GLISSE VERS LE BAS"), L("to slide under the sign", "pour glisser sous le panneau"), e, true); return; }
                if (!tipDone[TRecruit] && e.type == ET.Recrue && dz > 4f && dz < 24f)
                { ShowTip(TRecruit, 4, L("RECRUIT THEM!", "RECRUTE-LES !"), M(L("Run into your colleagues: they fight with you", "Fonce sur tes collègues : ils se battent avec toi"), L("Run into the rioters: they fight with you", "Fonce sur les casseurs : ils se battent avec toi")), e, false); return; }
                if (!tipDone[TGear] && e.type >= ET.Gilet && e.type != ET.Recrue && e.type != ET.Cafe && e.type != ET.Renfort && dz > 4f && dz < 24f)
                { ShowTip(TGear, 4, L("GRAB YOUR GEAR", "RAMASSE TON ÉQUIPEMENT"), L("Armor plate, then the CHARGE: smash through everything!", "Plaque d'armure, puis la CHARGE : fonce à travers tout !"), e, false); return; }
                if (!tipDone[TShop] && e.type == ET.Manif && dz > 4f && dz < 18f)
                { ShowTip(TShop, 4, L("DON'T HIT SHOPPERS!", "NE FRAPPE PAS LES CLIENTS !"), L("Hitting one costs you a heart", "En toucher un te coûte un cœur"), e, false); return; }
            }
        }

        // ================================================================== étapes et boss

        void UpdateStage(float dt)
        {
            if (stageToastT > 0f)
            {
                stageToastT -= dt;
                if (stageToastT <= 0f)
                {
                    hud.Toast(L("STAGE ", "ÉTAPE ") + stage);
                    hud.Ticker(L("Stage ", "Étape ") + stage + ": " + M(L("more robbers, a tougher boss!", "plus de voleurs, un boss plus coriace !"), L("more cops, a tougher boss!", "plus de policiers, un boss plus coriace !")), true);
                }
            }
            float frac = Mathf.Clamp01((pz - stageStartZ) / StageLen);
            if (boss == null && !demo && !tutorialMode && !runWon && store == null && pz - stageStartZ >= StageLen + 12f) StartBoss();
            if (boss != null && (boss.dead || boss.go == null)) boss = null;
            hud.SetStage(stage, frac, boss != null ? BossName : null, boss != null ? boss.hp / (float)bossMaxHp : 0f);
            if (boss == null) { warn.gameObject.SetActive(false); warn2.gameObject.SetActive(false); }
        }

        void StartBoss()
        {
            bossType = tutorialMode ? 0 : campaign ? Tuning.StreetBoss[streetIdx] : bossOrder[(stage - 1) % 6];
            int baseHp = tutorialMode ? Tuning.TutorialBossHp : campaign ? Mathf.RoundToInt(Tuning.BossHp[Mathf.Clamp(streetIdx + debugDiff, 0, Tuning.Streets - 1)] * Tuning.BossHpMult) : 6 + (stage - 1) * 3;
            bossMaxHp = gear[GSmg] ? baseHp * 3 : baseHp;
            if (Tuning.Arcade) bossMaxHp = Mathf.RoundToInt(bossMaxHp * (1f + squad.Count / 8f));   // grosse bande : le boss tient le temps d'un vrai combat
            hpAtBoss = hearts;
            bossAttacks = 0;
            bossCharging = false;
            bossStunT = 0f;
            boss = Spawn(ET.Loup, 1, pz + 30f);
            if (bossType == 6)
            {
                // le chef de bande arrive avec son escorte
                SpawnPack(0, pz + 20f, 3);
                SpawnPack(2, pz + 23f, 3);
            }
            boss.hp = bossMaxHp;
            boss.escape = 0f;
            bossWind = -1f;
            hud.Toast(BossName + "!");
            string how;
            switch (bossType)
            {
                case 0: ShowTip(TBoss, 1, L("DODGE THE BOXES", "ÉVITE LES CARTONS"), L("The red mark shows where he aims: change lane!", "La marque rouge montre où il vise : change de couloir !"), null, false); how = L("Dodge the boxes!", "Évite les cartons !"); break;
                case 1: ShowTip(TBoss, 2, L("JUMP THE SHOCKWAVE", "SAUTE L'ONDE DE CHOC"), L("Swipe up when he slams the ground", "Glisse vers le haut quand il frappe le sol"), null, false); how = L("Jump over the shockwaves!", "Saute par-dessus les ondes de choc !"); break;
                case 2: ShowTip(TBoss, 0, L("TAP TO HIT HIM", "TAPE POUR LE FRAPPER"), L("Knock out his gang, then hit him again and again", "Assomme sa bande, puis frappe-le encore et encore"), null, false); how = L("Watch out for the gang!", "Attention à sa bande !"); break;
                case 3: ShowTip(TBoss, 1, L("FIND THE SAFE LANE", "TROUVE LE BON COULOIR"), L("Two lanes get hit: move to the one without a red mark", "Deux couloirs sont visés : va dans celui sans marque rouge"), null, false); how = L("Two lanes at once!", "Deux couloirs à la fois !"); break;
                case 4: ShowTip(TBoss, 1, L("DODGE THE CHARGE", "ÉVITE LA CHARGE"), L("Change lane, or tap right when he arrives", "Change de couloir, ou tape pile quand il arrive"), null, false); how = L("He charges straight at you!", "Il fonce droit sur toi !"); break;
                case 6: ShowTip(TBoss, 0, L("NINJA STRIKES HURT HIM", "LES COUPS NINJA LE BLESSENT"), L("Jump and tap as you land, or hit him while he glows gold", "Saute et tape à l'atterrissage, ou frappe-le quand il brille en doré"), null, false); how = L("Only Perfect Hits really hurt him!", "Seuls les coups parfaits le blessent vraiment !"); break;
                default: ShowTip(TBoss, 4, L("JUMP OR DODGE", "SAUTE OU ESQUIVE"), L("Shockwaves and boxes, one after the other", "Ondes de choc et cartons, l'un après l'autre"), null, false); how = L("Shockwaves, then boxes!", "Ondes de choc, puis cartons !"); break;
            }
            hud.Ticker(L("BOSS FIGHT: " + BossName.ToLower() + ". " + how, "COMBAT DE BOSS : " + BossName + ". " + how), true);
            Snd.Play(Snd.I.siren, 0.7f, 0f);
            shake = Mathf.Max(shake, 0.3f);
        }

        /// <summary>Attaque que le boss prépare : 0 lancer, 1 onde de choc, 2 appel de la bande, 3 double lancer, 4 charge.</summary>
        int BossMove()
        {
            switch (bossType)
            {
                case 2: return Random.value < 0.4f ? 0 : 2;
                case 5: return bossAttacks % 2 == 0 ? 1 : 0;
                case 6: return bossAttacks % 3 == 0 ? 5 : bossAttacks % 3 == 1 ? 1 : 2; // balayage, frappe au sol, renforts
                default: return bossType;
            }
        }

        void ThrowBox(Ent e, int ln)
        {
            Ent box = Spawn(ET.Pave, ln, e.z - 0.8f);
            box.y = 1.6f;
            box.vz = 4f;
            box.spin.localScale = Vector3.one * 2f;
        }

        /// <summary>Le boss reste devant toi, change de couloir et attaque selon son type, après un temps d'armé bien visible.</summary>
        void UpdateBoss(Ent e, float dt)
        {
            float lvl = campaign ? Diff : stage - 1;
            e.escape = Mathf.MoveTowards(e.escape, 0f, 3.5f * dt);
            if (bossStunT > 0f)
            {
                // ouverture après la frappe au sol : il brille en or, chaque coup porte
                bossStunT -= dt;
                e.rig.Flash(NinjaGold, 0.06f);
                e.timer = Mathf.Max(e.timer, 0.5f);
            }
            float gap = 3.2f + e.escape;
            float want = pz + gap;
            e.rig.speed = speed;
            bool riding = bossType == 4 && thief; // gyropode : pas de foulées

            // charge : il fonce à reculons sur toi, te dépasse, puis revient se placer devant
            if (bossCharging)
            {
                e.z += (speed - 26f - lvl * 2f) * dt;
                e.rig.anim = riding ? Anim.Stand : Anim.Charge;
                if (e.z < pz - 3f)
                {
                    bossCharging = false;
                    e.fled = true;
                    e.lane = e.lane == 1 ? (Random.value < 0.5f ? 0 : 2) : 1;
                }
                return;
            }
            if (e.fled)
            {
                // retour devant toi par un autre couloir
                e.x = Mathf.Lerp(e.x, (e.lane - 1) * Lane, 1f - Mathf.Exp(-6f * dt));
                e.z = Mathf.MoveTowards(e.z, want, (speed + 12f) * dt);
                e.rig.anim = riding ? Anim.Stand : Anim.Run;
                if (e.z >= want - 0.05f) e.fled = false;
                return;
            }
            e.z = e.z > want + 0.5f ? Mathf.MoveTowards(e.z, want, (speed * 0.6f + 6f) * dt) + speed * dt : want;

            if (bossWind < 0f)
            {
                e.timer -= dt;
                if (e.timer < 0.9f && e.timer > 0f && Random.value < dt * 1.5f)
                    e.lane = Mathf.Clamp(e.lane + (Random.value < 0.5f ? -1 : 1), 0, 2);
                e.x = Mathf.Lerp(e.x, (e.lane - 1) * Lane, 1f - Mathf.Exp(-5f * dt));
                e.rig.anim = riding ? Anim.Stand : Anim.Run;
                if (e.timer <= 0f)
                {
                    // armé de l'attaque : on voit venir le coup
                    bossWind = 0f;
                    bossMove = BossMove();
                    if (Tuning.Arcade && bossMove == 1) bossMove = 3;   // onde de choc (à sauter) remplacée par deux caisses
                    bossTargetLane = lane;
                    bossThrow = bossMove == 0 || bossMove == 3;
                    switch (bossMove)
                    {
                        case 1:
                            hud.Float(new Vector3(px, 2.6f, pz + 1f), L("JUMP!", "SAUTE !"), Hud.Yellow, true);
                            e.rig.Land(0.35f);
                            break;
                        case 2:
                            hud.Float(new Vector3(e.x, 3f, e.z), M(L("GET HIM!", "ATTRAPE-LE !"), L("GET HER!", "ATTRAPE-LA !")), Hud.Bad, true);
                            break;
                        case 3:
                        {
                            // deux couloirs visés : un seul reste sûr
                            int safe = Random.Range(0, 3);
                            if (safe == lane) safe = (lane + Random.Range(1, 3)) % 3;
                            bossTargetLane = (safe + 1) % 3;
                            bossSecondLane = (safe + 2) % 3;
                            e.rig.throwT = 0f;
                            warn.gameObject.SetActive(true);
                            warn2.gameObject.SetActive(true);
                            break;
                        }
                        case 5:
                        {
                            // balayage de batte : deux couloirs, le tien et un voisin
                            bossTargetLane = lane;
                            bossSecondLane = lane == 0 ? 1 : lane == 2 ? 1 : (Random.value < 0.5f ? 0 : 2);
                            e.rig.throwT = 0f;
                            warn.gameObject.SetActive(true);
                            warn2.gameObject.SetActive(true);
                            hud.Float(new Vector3(px, 2.6f, pz + 1f), L("DODGE OR JUMP!", "ESQUIVE OU SAUTE !"), Hud.Bad, true);
                            break;
                        }
                        case 4:
                            e.lane = lane;
                            warn.gameObject.SetActive(true);
                            hud.Float(new Vector3(e.x, 3f, e.z), "!", Hud.Bad, true);
                            break;
                        default:
                            e.rig.throwT = 0f;
                            warn.gameObject.SetActive(true);
                            break;
                    }
                }
                return;
            }

            bossWind += dt;
            float windDur = bossMove == 1 ? 0.75f
                          : bossMove == 0 ? Mathf.Max(0.5f, 0.68f - lvl * 0.04f)
                          : bossMove == 4 ? Mathf.Max(0.6f, 0.85f - lvl * 0.04f)
                          : bossMove == 5 ? Mathf.Max(0.62f, 0.9f - lvl * 0.03f)
                          : 0.7f;
            if (bossMove == 2) e.rig.anim = Anim.Cheer;
            if (bossMove == 4)
            {
                // il se met dans ton couloir et piétine avant de charger
                e.x = Mathf.Lerp(e.x, (e.lane - 1) * Lane, 1f - Mathf.Exp(-8f * dt));
                bossTargetLane = e.lane;
                e.rig.Land(0.08f);
            }
            if (warn.gameObject.activeSelf) warn.localPosition = new Vector3((bossTargetLane - 1) * Lane, 0.05f, pz + 2.2f);
            if (warn2.gameObject.activeSelf) warn2.localPosition = new Vector3((bossSecondLane - 1) * Lane, 0.05f, pz + 2.2f);
            if (bossMove == 1)
            {
                // il bondit puis frappe le sol
                float u = bossWind / windDur;
                e.y = Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI) * 1.4f;
                e.rig.anim = Anim.Jump;
                e.rig.vy = u < 0.5f ? 8f : -8f;
            }
            if (bossWind < windDur) return;

            bossWind = -1f;
            bossAttacks++;
            warn.gameObject.SetActive(false);
            warn2.gameObject.SetActive(false);
            e.y = 0f;
            switch (bossMove)
            {
                case 0:
                    ThrowBox(e, bossTargetLane);
                    if (lvl >= 2 && Random.value < 0.4f) ThrowBox(e, Mathf.Clamp(bossTargetLane + (Random.value < 0.5f ? -1 : 1), 0, 2));
                    e.timer = Mathf.Max(0.7f, 1.5f - lvl * 0.12f);
                    break;
                case 1:
                {
                    Ent w = Spawn(ET.Onde, 1, e.z - 0.6f);
                    w.x = px;
                    Fx.Debris(new Vector3(e.x, 0.3f, e.z));
                    Fx.Dust(new Vector3(e.x, 0.1f, e.z));
                    Snd.Play(Snd.I.crash, 0.8f);
                    shake = Mathf.Max(shake, 0.35f);
                    e.rig.Land(0.4f);
                    e.timer = Mathf.Max(1.2f, 2.3f - lvl * 0.15f);
                    if (bossType == 6)
                    {
                        bossStunT = 1.3f;
                        e.timer = Mathf.Max(e.timer, 1.6f);
                        hud.Float(new Vector3(e.x, 3.2f, e.z), L("OPENING! HIT HIM!", "OUVERTURE ! FRAPPE-LE !"), NinjaGold, true);
                    }
                    break;
                }
                case 5:
                {
                    // la batte balaie deux couloirs : sauter ou changer de couloir
                    Snd.PlayPitch(Snd.I.whoosh, 1f, 0.55f);
                    Snd.PlayPitch(Snd.I.crash, 0.5f, 0.8f);
                    shake = Mathf.Max(shake, 0.3f);
                    Fx.Skid(new Vector3((bossTargetLane - 1) * Lane, 0.1f, pz + 1f));
                    Fx.Skid(new Vector3((bossSecondLane - 1) * Lane, 0.1f, pz + 1f));
                    for (int i = squad.Count - 1; i >= 0; i--)
                    {
                        int ml = Mathf.Clamp(Mathf.RoundToInt(squad[i].x / Lane) + 1, 0, 2);
                        if (ml == bossTargetLane || ml == bossSecondLane) LoseMate(i, new Vector3(Random.Range(-5f, 5f), 6f, 2f));
                    }
                    if ((lane == bossTargetLane || lane == bossSecondLane) && py < 0.8f) Hurt(1, L("The bat sweep got you", "La batte t'a balayé"));
                    e.timer = Mathf.Max(1f, 1.6f - lvl * 0.1f);
                    break;
                }
                case 2:
                {
                    int other = Mathf.Clamp(e.lane + (Random.value < 0.5f ? -1 : 1), 0, 2);
                    SpawnPack(other, pz + 24f, 3 + Mathf.Min(3, (int)lvl / 2));
                    e.timer = Mathf.Max(1.4f, 2.6f - lvl * 0.15f);
                    break;
                }
                case 3:
                    ThrowBox(e, bossTargetLane);
                    ThrowBox(e, bossSecondLane);
                    e.timer = Mathf.Max(0.9f, 1.7f - lvl * 0.12f);
                    break;
                case 4:
                    bossCharging = true;
                    Snd.PlayPitch(Snd.I.whoosh, 0.9f, 0.6f);
                    Fx.Skid(new Vector3(e.x, 0.1f, e.z));
                    e.timer = Mathf.Max(1.1f, 2f - lvl * 0.12f);
                    break;
            }
        }

        /// <summary>Boss vaincu : ralenti, pluie de confettis, récompenses, étape suivante.</summary>
        void BossDown(Ent e, Vector3 chest)
        {
            e.hit = true;
            loups++;
            int bonus = Mathf.RoundToInt(1000f * stage * ComboMult());
            int loot = 15 * stage;
            score += bonus;
            coins += loot;
            e.rig.KO(new Vector3(Random.value < 0.5f ? -4f : 4f, 9f, speed + 7f), new Vector3(Random.Range(-6f, 6f), Random.Range(-3f, 3f), Random.Range(-10f, 10f)));
            slowMo = Tuning.Arcade ? 0.5f : 1.1f;   // arcade : ralenti court, la finale enchaîne
            shake = 0.5f;
            zoom = 1f;
            hud.Flash(0.6f);
            for (int i = 0; i < 3; i++) Fx.Confetti(chest + new Vector3((i - 1) * 1.2f, 0.5f, 0f));
            Fx.Pickup(chest);
            hearts = Mathf.Min(MaxHearts, hearts + Tuning.BossHeal);
            hud.Toast(L("STAGE " + stage + " CLEAR!", "ÉTAPE " + stage + " TERMINÉE !"));
            hud.Float(chest, "+" + loot + " " + Tuning.Cur, Hud.StarGold);
            hud.Ticker(L("The boss is down! +" + Tuning.BossHeal + " HP, +" + loot + " stars.", "Le boss est à terre ! +" + Tuning.BossHeal + " PV, +" + loot + " étoiles."), true);
            Snd.Play(Snd.I.pickup, 0.9f, 0f);
            warn.gameObject.SetActive(false);
            Haptics.Pulse(120, true);
            if (campaign || tutorialMode)
            {
                boss = null;
                bossWind = -1f;
                bossCharging = false;
                warn.gameObject.SetActive(false);
                warn2.gameObject.SetActive(false);
                if (Tuning.Arcade && ArcadeFinale()) return;
                RunWon();
                return;
            }
            stage++;
            stageStartZ = pz;
            nextSpawnZ = Mathf.Max(nextSpawnZ, pz + 30f);
            boss = null;
            bossWind = -1f;
            bossCharging = false;
            warn2.gameObject.SetActive(false);
            stageToastT = 1.6f;
        }

        // ================================================================== pilote automatique (démo)

        void ChangeLane(int dir)
        {
            int nl = Mathf.Clamp(lane + dir, 0, 2);
            if (nl == lane) return;
            lane = nl;
            Snd.PlayPitch(Snd.I.whoosh, 0.22f, 1.35f);
            if (py < 0.05f) Fx.Skid(new Vector3(px, 0.08f, pz));
        }

        void Autopilot(float dt)
        {
            float cx = (lane - 1) * Lane;
            float reach = gear[GBaton] ? 5f : 3f;
            foreach (Ent e in ents)
            {
                if (e.hit || e.dead) continue;
                float dz = e.z - pz;
                if (Mathf.Abs(e.x - cx) > 1.1f || dz < 0.3f) continue;
                if (dz < 3.4f && py <= 0.01f)
                {
                    if (e.type == ET.Barricade || e.type == ET.Poubelle || e.type == ET.Voiture)
                    {
                        vy = 12.5f; slide = 0f; P.Takeoff(); Snd.Play(Snd.I.jump, 0.6f);
                        foreach (Mate m in squad) m.rig.Takeoff();
                    }
                    else if ((e.type == ET.Banderole || e.type == ET.Pave) && slide <= 0f) { slide = Tuning.SlideTime; P.Roll(); }
                }
                if ((e.type == ET.Casseur || e.type == ET.Loup) && dz < reach) Swing();
            }

            autoT -= dt;
            if (autoT > 0f) return;
            autoT = 0.2f;
            float[] cost = new float[3];
            for (int l = 0; l < 3; l++)
            {
                float lx = (l - 1) * Lane;
                cost[l] = Mathf.Abs(l - lane) * 0.6f;
                foreach (Ent e in ents)
                {
                    if (e.hit || e.dead) continue;
                    float dz = e.z - pz;
                    if (dz < 2f || dz > 20f || Mathf.Abs(e.x - lx) > 1.2f) continue;
                    float w = 1f - dz / 22f;
                    switch (e.type)
                    {
                        case ET.Voiture: cost[l] += 6f * w; break;
                        case ET.Manif: cost[l] += 8f * w; break;
                        case ET.Barricade: case ET.Poubelle: case ET.Banderole: cost[l] += 1.5f * w; break;
                        case ET.Recrue: cost[l] -= 6f * w; break;
                        case ET.Piece: cost[l] -= 0.5f * w; break;
                        case ET.Casseur: case ET.Loup: cost[l] -= 1.2f * w; break;
                        default: if (e.IsPickup) cost[l] -= 5f * w; break;
                    }
                }
            }
            int best = lane;
            for (int l = 0; l < 3; l++) if (cost[l] < cost[best] - 0.3f) best = l;
            if (best != lane) ChangeLane(best > lane ? 1 : -1);
        }

        // ================================================================== escouade

        /// <summary>Les collègues touchés rejoignent l'escouade (ils gardent leur position et filent vers leur place).</summary>
        void Recruit(Ent e)
        {
            int added = 0;
            foreach (Rig r in e.crowd)
            {
                if (squad.Count >= MaxSquad) { Destroy(r.root.gameObject); continue; }
                r.root.SetParent(world, true);
                Vector3 p = r.root.position;
                r.Takeoff();
                squad.Add(new Mate { rig = r, x = p.x, z = p.z, slot = squad.Count, cd = Random.Range(0f, 0.2f) });
                added++;
            }
            e.crowd.Clear();
            e.dead = true;
            Destroy(e.go);
            if (added == 0) return;
            if (thief && squad.Count < MaxSquad)
            {
                // les émeutiers, c'est le nombre : chaque groupe recruté entraîne un complice de plus
                int before = squad.Count;
                AddMates(Tuning.ThiefRecruitBonus);
                added += squad.Count - before;
            }
            Fx.Pickup(new Vector3(e.x, 1f, e.z));
            Snd.Play(Snd.I.pickup, 0.7f, 0f);
            hud.Toast("+" + added + M(L(" COP", " POLICIER"), L(" BUDDY", " POTE")) + (added > 1 ? "S!" : "!"));
            hud.Float(new Vector3(px, 2.2f, pz), M(L("Squad: ", "Escouade : "), L("Crew: ", "Bande : ")) + (squad.Count + 1), Hud.Good);
            P.Land(0.15f);
        }

        void AddMates(int n)
        {
            for (int i = 0; i < n && squad.Count < MaxSquad; i++)
            {
                Vector2 sl = SlotAt(squad.Count);
                Rig.OutlineOverride = Art.OutlineTint(AllyBlue);
                Rig r = Rig.Build(thief ? Kind.Thief : Kind.Guard, world, new Vector3(px + sl.x, 0f, pz + sl.y), 0f, thief ? Rig.Crew : 0, true, MateArmor);
                Rig.OutlineOverride = null;
                AllyRing(r);
                squad.Add(new Mate { rig = r, x = px + sl.x, z = pz + sl.y, slot = squad.Count });
            }
        }

        void ClearSquad()
        {
            foreach (Mate m in squad) if (m.rig != null && m.rig.root != null) Destroy(m.rig.root.gameObject);
            squad.Clear();
        }

        /// <summary>Le collègue est mis au tapis (adversaire ou obstacle) : il quitte l'escouade.</summary>
        void LoseMate(int i, Vector3 push)
        {
            Mate m = squad[i];
            squad.RemoveAt(i);
            m.rig.KO(push, new Vector3(Random.Range(-5f, 5f), Random.Range(-3f, 3f), Random.Range(-6f, 6f)));
            Destroy(m.rig.root.gameObject, 2.5f);
            for (int k = 0; k < squad.Count; k++) squad[k].slot = k;
            hud.Float(new Vector3(m.x, 2f, m.z), "-1", Hud.Bad, true);
            Snd.Play(Snd.I.hurt, 0.35f);
        }

        readonly List<Ent> threats = new List<Ent>();

        void UpdateSquad(float dt, float h)
        {
            // une bande approche : l'escouade charge avec toi (chacun fonce sur un adversaire, jusqu'à ta hauteur et un peu devant)
            threats.Clear();
            foreach (Ent e in ents)
            {
                if (e.hit || e.dead || e.fled) continue;
                if (e.type != ET.Casseur && e.type != ET.Loup) continue;
                float dz = e.z - pz;
                if (dz > -0.5f && dz < 11f) threats.Add(e);
            }
            threats.Sort((a, b) => a.z.CompareTo(b.z));
            for (int i = squad.Count - 1; i >= 0; i--)
            {
                Mate m = squad[i];
                Vector2 sl = SlotAt(m.slot);
                float tx = Mathf.Clamp(px + sl.x, -3.5f, 3.5f), tz = pz + sl.y;
                bool charge = threats.Count > 0;
                if (charge)
                {
                    // cibles réparties : les plus proches d'abord, en évitant d'empiler toute l'escouade sur le même
                    Ent target = threats[m.slot % threats.Count];
                    if (Tuning.Arcade)
                    {
                        // arcade : la foule fonce en gardant sa forme de grappe (pas une file indienne)
                        tx = Mathf.Clamp(target.x * 0.6f + px * 0.4f + sl.x * 0.8f, -3.5f, 3.5f);
                        tz = Mathf.Min(target.z - 1.2f, pz + 2.2f) + sl.y * 0.7f;
                    }
                    else
                    {
                        tx = Mathf.Clamp(target.x + (m.slot % 2 == 0 ? -0.35f : 0.35f), -3.5f, 3.5f);
                        tz = Mathf.Min(target.z - 1.1f, pz + 2.2f - (m.slot / 3) * 0.6f);
                    }
                }
                m.x = Mathf.Lerp(m.x, tx, 1f - Mathf.Exp((charge ? -11f : -7f) * dt));
                m.z = Mathf.Lerp(m.z, tz, 1f - Mathf.Exp((charge ? -8f : -5f) * dt));
                Rig r = m.rig;
                r.speed = speed;
                r.vy = vy;
                r.lean = (tx - m.x) * 9f;
                r.anim = py > 0.05f ? Anim.Jump : slide > 0f ? Anim.Slide : Anim.Run;
                r.root.localPosition = new Vector3(m.x, py, m.z);
                r.root.localRotation = Quaternion.Euler(0f, (tx - m.x) * 6f, 0f);
                r.Tick(dt);

                // il frappe les adversaires juste devant lui
                m.cd -= dt;
                Ent hitE = null, touch = null;
                foreach (Ent e in ents)
                {
                    if (e.hit || e.dead) continue;
                    float dz = e.z - m.z, dx = Mathf.Abs(e.x - m.x);
                    if (e.type == ET.Casseur || e.type == ET.Loup)
                    {
                        if (dx < 1.2f && dz > -0.2f && dz < 3.2f && hitE == null) hitE = e;
                        if (dx < 0.55f && dz > -0.4f && dz < 0.5f && !e.fled) touch = e;
                    }
                    else if ((dx < 0.9f || e.type == ET.Onde) && Mathf.Abs(dz) < e.depth + 0.2f)
                    {
                        // obstacles : mêmes règles que pour toi (il saute et glisse en même temps que toi)
                        bool safe = e.type == ET.Barricade ? py > 0.75f
                                  : e.type == ET.Poubelle ? py > 0.85f
                                  : e.type == ET.Voiture ? py > 1.45f
                                  : e.type == ET.Banderole ? h < 1.5f
                                  : e.type == ET.Onde ? py > 0.45f
                                  : true;
                        if (!safe && !gear[GShield]) { touch = e; break; }
                    }
                }
                if (hitE != null && m.cd <= 0f)
                {
                    m.cd = 0.36f;
                    r.swingStyle = Random.Range(0, 3);
                    r.swingT = 0f;
                    HitCasseur(hitE, true, 1.3f, false, MateDmg);
                    if (st != St.Play) return;
                    continue;
                }
                if (touch != null)
                {
                    if (touch.type == ET.Casseur) HitCasseur(touch, true, 0.8f); // échange un pour un
                    LoseMate(i, new Vector3(Random.Range(-3f, 3f), 4f, speed * 0.4f));
                    if (st != St.Play) return;
                }
            }
        }

        /// <summary>La mitraillette tire toute seule sur l'adversaire le plus proche devant toi.</summary>
        void UpdateSmg(float dt)
        {
            if (Tuning.Arcade) AutoMelee();
            if (!gear[GSmg] && !Tuning.Arcade) return;
            smgT -= dt;
            if (smgT > 0f) return;
            Ent best = null;
            float bd = 1e9f;
            foreach (Ent e in ents)
            {
                if (e.hit || e.dead) continue;
                if (e.type != ET.Casseur && e.type != ET.Loup) continue;
                if (Mathf.Abs(e.x - px) > 1.7f && e.type != ET.Loup) continue;   // le boss est visé où qu'il soit
                float d = e.z - pz;
                if (d < 1.5f || d > 30f) continue;
                bool taken = false;
                foreach (Bolt b in bolts) if (b.target == e && e.type != ET.Loup) { taken = true; break; }
                if (taken) continue;
                if (d < bd) { bd = d; best = e; }
            }
            if (best == null) return;
            // cadence : mitraillette en rafale, pistolet régulier, à mains nues un lancer plus lent
            smgT = gear[GSmg] ? 0.11f : gear[GPistol] ? Tuning.AutoPistolRate : Tuning.AutoBareRate;
            Shoot(best, gear[GSmg]);
        }

        /// <summary>Arcade : dès qu'un adversaire arrive à portée de bras dans le couloir, le coup part tout seul.</summary>
        void AutoMelee()
        {
            if (swingCd > 0f || pendings.Count > 0) return;
            float reach = Heavy ? 6.3f : gear[GBaton] ? 5.8f : 3.4f;
            foreach (Ent e in ents)
            {
                if (e.hit || e.dead) continue;
                if (e.type != ET.Casseur && e.type != ET.Loup) continue;
                if (Mathf.Abs(e.x - px) > 1.4f) continue;
                float d = e.z - pz;
                if (d < -0.8f || d > reach * 0.8f) continue;
                Swing();
                return;
            }
        }

        float ComboMult() => 1f + Mathf.Min(4, combo / 3) * 0.5f;

        /// <summary>
        /// Un adversaire prend un coup. power règle la projection (matraque, coup par-dessus la tête) ;
        /// light pour les coups de l'escouade et les rafales : pas d'arrêt sur image, effets plus légers.
        /// </summary>
        void HitCasseur(Ent e, bool byBuddy, float power = 1f, bool light = false, int dmg = 1)
        {
            double pt0 = Perf.Begin();
            HitCasseurIn(e, byBuddy, power, light, dmg);
            Perf.End("·hit", pt0);
        }

        void HitCasseurIn(Ent e, bool byBuddy, float power, bool light, int dmg)
        {
            light |= byBuddy;
            Vector3 chest = new Vector3(e.x, 1.2f, e.z);
            Haptics.Pulse(light ? 8 : 18);
            Fx.Hit(chest);
            Snd.PlayPitch(Snd.I.bonk, light ? 0.6f : 1f, Random.Range(0.9f, 1.1f) / Mathf.Sqrt(power));
            if (!light)
            {
                hitStop = 0.05f + 0.02f * power;
                shake = Mathf.Max(shake, 0.12f + 0.08f * power);
                fovKick = Mathf.Max(fovKick, 3f + 2f * power);
                hud.Flash(0.1f + 0.04f * power);
            }
            else shake = Mathf.Max(shake, 0.06f);

            if (Heavy && meleeHitNow)
            {
                // matraque renforcée : impact plus lourd, à l'oreille comme à l'écran
                Snd.PlayPitch(Snd.I.crash, 0.35f, Random.Range(1.3f, 1.5f));
                Fx.Sparkle(chest);
                shake = Mathf.Max(shake, 0.22f);
                hitStop = Mathf.Max(hitStop, 0.07f);
            }
            if (e.type == ET.Loup)
            {
                if (bossType == 6 && !superHitNow && bossStunT <= 0f)
                {
                    // le chef de bande encaisse : seuls les Perfect Hits (ou l'ouverture dorée) font mal
                    dmg = 1;
                    if (!light && Random.value < 0.5f) hud.Float(chest + Vector3.up * 1.6f, L("TRY A PERFECT HIT!", "TENTE UN COUP PARFAIT !"), NinjaGold, false);
                }
                e.hp -= dmg;
                e.rig.Flash(Color.white, 0.08f);
                if (!light) hud.Float(chest + Vector3.up, Random.value < 0.5f ? "BONK!" : "POW!", Hud.Yellow, true);
                if (e.hp > 0)
                {
                    e.rig.Land(light ? 0.1f : 0.25f);
                    e.escape += light ? 0.35f : 2.6f; // recul
                    score += 20f;
                    return;
                }
                BossDown(e, chest);
                return;
            }

            // adversaire protégé : le coup fait sauter une protection, il faut insister (ou une arme plus forte)
            if (e.type == ET.Casseur && e.hp > dmg)
            {
                e.hp -= dmg;
                e.rig.Flash(Color.white, 0.08f);
                e.rig.Land(0.3f);
                e.rig.ShedArmor(new Vector3(Mathf.Sign(e.x - px + 0.01f) * 3f, 2f, 3f));
                e.z += light ? 0.4f : 1.6f;
                Snd.PlayPitch(Snd.I.glass, 0.45f, 1.6f);
                if (!light) hud.Float(chest + Vector3.up * 0.6f, L("CLANG! " + e.hp + " more", "CLANG ! encore " + e.hp), Hud.Info, false);
                return;
            }

            e.hit = true;
            koCount++;
            combo++;
            RaidMemberGone(e, true);
            comboT = 2.5f;
            maxCombo = Mathf.Max(maxCombo, combo);
            int pts = Mathf.RoundToInt(100f * ComboMult());
            score += pts;
            // chaque adversaire assommé lâche des pièces
            int loot = thief ? Tuning.LootThief : Tuning.LootGuard;
            coins += loot;
            starsKO += loot;
            Fx.Coin(chest + Vector3.up * 0.3f);
            if (!light || Random.value < 0.35f)
                hud.Float(chest + Vector3.up * 0.6f, power >= 1.9f ? L("SMASH!", "ÉCRASÉ !") : thief ? (Random.value < 0.5f ? L("SHOVE!", "POUSSE-TOI !") : L("OUTTA MY WAY!", "DÉGAGE !")) : (Random.value < 0.5f ? "BONK!" : L("GOTCHA!", "JE TE TIENS !")), Hud.Yellow, true);
            hud.Float(chest, "+" + loot + " " + Tuning.Cur, Hud.StarGold);
            hud.ComboPop();
            if (!light) Fx.Confetti(chest);
            if (combo % 5 == 0)
            {
                coins += Tuning.StarsCombo;
                starsCombo += Tuning.StarsCombo;
                hud.Float(chest + Vector3.up * 1.2f, "COMBO +" + Tuning.StarsCombo + " " + Tuning.Cur, Hud.StarGold, true);
                // palier de combo : ralenti et zoom sur le voleur qui s'envole
                if (!Tuning.Arcade)
                {
                    slowMo = Mathf.Max(slowMo, 0.45f);
                    zoom = 0.7f;
                }
                if (!Tuning.Arcade) hud.Toast("COMBO x" + combo + "!");
                Fx.Confetti(chest + Vector3.up * 0.4f);
            }
            float side = Mathf.Abs(e.x - px) < 0.3f ? (Random.value < 0.5f ? -1f : 1f) : Mathf.Sign(e.x - px);
            e.rig.KO(new Vector3(side * Random.Range(4.5f, 7.5f) * power, Random.Range(4f, 6.5f) * Mathf.Sqrt(power), speed + Random.Range(1f, 4f) * power),
                     new Vector3(Random.Range(-8f, 8f), Random.Range(-4f, 4f), -side * Random.Range(4f, 9f)) * power);
        }

        void Bavure(Ent e)
        {
            e.hit = true;
            bavures++;
            combo = 0;
            foreach (Rig r in e.crowd)
            {
                float side = Mathf.Sign(r.root.position.x - px + 0.01f);
                r.KO(new Vector3(side * 3f, 3f, speed * 0.8f), new Vector3(0f, 0f, -side * 4f));
            }
            Snd.Play(Snd.I.bonk, 0.8f);
            hud.Toast(L("WRONG PERSON!", "MAUVAISE CIBLE !"));
            hud.Ticker(M(L("A shopper was hit by security. The manager is not happy.", "Un client a été frappé par la sécurité. Le directeur n'est pas content."), L("A shopper got knocked over. Everyone is staring at you.", "Un client s'est fait renverser. Tout le monde te regarde.")), true);
            Hurt(1, L("You hit a shopper", "Tu as frappé un client"));
        }

        void Hurt(int n, string why)
        {
            if (inv > 0f || perfectGuardT > 0f || finaleZ > 0f) return;
            if (tutorialMode) { TutorialFail(why); return; }
            if (demo) { inv = 0.6f; P.Flash(new Color(1f, 0.25f, 0.2f), 0.12f); return; }
            if (Tuning.Arcade && squad.Count > 0)
            {
                // arcade : la bande encaisse à ta place (un ou deux membres tombent)
                int lose = Mathf.Min(squad.Count, n + 1);
                for (int k = 0; k < lose; k++) LoseMate(squad.Count - 1, new Vector3(Random.Range(-3f, 3f), 4f, 2f));
                inv = 0.45f;
                hud.Damage(0.12f);
                Haptics.Pulse(25);
                return;
            }
            if (shieldHits > 0)
            {
                // bouclier gagné : il encaisse le coup et se fissure ; au dernier coup il se brise
                shieldHits--;
                inv = 1f;
                P.ShieldHit(shieldHits);
                Fx.Debris(new Vector3(px - 0.4f, 1.3f, pz + 0.3f));
                hud.Float(new Vector3(px, 2.4f, pz), shieldHits > 0 ? L("BLOCKED! ", "BLOQUÉ ! ") + shieldHits + L(" left", " restant" + (shieldHits > 1 ? "s" : "")) : L("SHIELD BROKEN", "BOUCLIER BRISÉ"), Hud.Info, true);
                hud.Damage(0.25f);
                Snd.PlayPitch(Snd.I.glass, 0.6f, shieldHits > 0 ? 1.2f : 0.8f);
                Haptics.Pulse(30);
                return;
            }
            if (gear[GVest])
            {
                // le gilet encaisse le coup et se perd : il sera reproposé plus loin
                gear[GVest] = false;
                ApplyGear();
                Fx.Debris(new Vector3(px, 1.4f, pz));
                inv = 1f;
                hud.Float(new Vector3(px, 2.4f, pz), L("The vest took it", "Le gilet a encaissé"), Hud.Info);
                hud.Damage(0.4f);
                PostFx.Hit(0.4f);
                Snd.Play(Snd.I.hurt, 0.5f);
                return;
            }
            int dmgHp = n * Tuning.DamagePerHit;
            hearts -= dmgHp;
            // chaque coup reçu : instant, distance, cause (à comparer aux zones critiques du rapport de difficulté)
            hitLog.Append(runT.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                  .Append(Mathf.RoundToInt(pz - stageStartZ)).Append(',').Append(boss != null ? "boss" : "course").Append(',')
                  .Append((why ?? "").Replace(",", " ")).Append(',').Append(Mathf.Max(0, hearts)).Append('\n');
            hud.Float(new Vector3(px, 2.8f, pz), "-" + dmgHp, Hud.Bad, true);
            Haptics.Pulse(70, true);
            P.Flash(new Color(1f, 0.25f, 0.2f), 0.15f);
            P.Land(0.2f);
            inv = campaign && !tutorialMode ? BalanceConfig.I.invulnerability : 1.2f;
            stumble = 0.5f;
            combo = 0;
            shake = 0.4f;
            hud.Damage();
            PostFx.Hit(1f);
            Snd.Play(Snd.I.hurt, 1f);
            hud.Float(new Vector3(px, 2.2f, pz), why, Hud.Bad);
            if (hearts <= 0)
            {
                cause = why;
                EndGame();
            }
        }

        void Collide(Ent e, float h)
        {
            switch (e.type)
            {
                case ET.Piece:
                    e.hit = true;
                    e.dead = true;
                    Destroy(e.go);
                    coins++;
                    starsPickup++;
                    score += thief ? 20f : 10f;
                    coinStreak++;
                    coinStreakT = 0.6f;
                    Fx.Coin(new Vector3(e.x, 1f, e.z));
                    Fx.Sparkle(new Vector3(e.x, 1.1f, e.z));
                    Snd.PlayPitch(Snd.I.coin, 0.45f, 1f + Mathf.Min(coinStreak, 8) * 0.06f);
                    return;
                case ET.Gilet:
                case ET.Bouclier:
                case ET.Renfort:
                case ET.Cafe:
                case ET.Casque:
                case ET.Matraque:
                case ET.Pistolet:
                case ET.Mitraillette:
                    e.hit = true;
                    e.dead = true;
                    Destroy(e.go);
                    Pickup(e.type, new Vector3(e.x, 1f, e.z));
                    return;
                case ET.Recrue:
                    e.hit = true;
                    Recruit(e);
                    return;
                case ET.Casseur:
                    // bouclier : on passe à travers en les bousculant
                    if (P.swingT >= 0f || gear[GShield]) { HitCasseur(e, false); return; }
                    e.hit = true;
                    RaidMemberGone(e, true);
                    e.rig.KO(new Vector3(Mathf.Sign(e.x - px + 0.01f) * 3f, 3f, speed * 0.6f), new Vector3(0f, 0f, 5f));
                    Hurt(1, M(L("Tackled by a robber", "Plaqué par un voleur"), L("Tackled by a cop", "Plaqué par un policier")));
                    return;
                case ET.Loup:
                    if (e.fled) return;
                    if (P.swingT >= 0f || gear[GShield])
                    {
                        // contre parfait pendant sa charge : il est renvoyé devant
                        if (bossCharging) { bossCharging = false; e.fled = true; e.z = pz + 1.2f; hud.Float(new Vector3(px, 2.4f, pz + 1f), L("COUNTER!", "CONTRE !"), Hud.Yellow, true); }
                        HitCasseur(e, false, 1.4f);
                        return;
                    }
                    if (bossCharging) { bossCharging = false; e.fled = true; e.z = pz - 1.5f; Hurt(1, L(BossName.ToLower() + " ran you over", BossName + " t'a renversé")); return; }
                    e.escape += 3f;
                    Hurt(1, L(BossName.ToLower() + " shoved you", BossName + " t'a bousculé"));
                    return;
                case ET.Onde:
                    if (py > 0.45f) return;
                    e.hit = true;
                    Hurt(1, L("Knocked down by the shockwave", "Renversé par l'onde de choc"));
                    return;
                case ET.Pave:
                    if (py > 0.9f || h < 1.5f) return;
                    e.hit = true;
                    e.dead = true;
                    Destroy(e.go);
                    Fx.Debris(new Vector3(e.x, 1.2f, e.z));
                    // les cartons du boss sont trop gros pour le casque : seul le bouclier les arrête
                    if ((helmetLv >= 1 && e.vz <= 0f) || (helmetLv >= 3 && e.vz > 0f) || gear[GShield]) { hud.Float(new Vector3(px, 2f, pz), gear[GShield] ? L("Blocked", "Bloqué") : L("Bounced off the helmet", "Le casque a tout pris"), Hud.Info); Snd.Play(Snd.I.land, 0.8f); return; }
                    Hurt(1, L("Hit by a flying box", "Touché par un carton volant"));
                    return;
                case ET.Barricade:
                    if (py > 0.75f) return;
                    e.hit = true;
                    Break(e, gear[GShield] ? 1.6f : 0.8f);
                    if (gear[GShield]) { hud.Float(new Vector3(e.x, 2f, e.z), "CLANG!", Hud.Yellow, true); return; }
                    Hurt(1, L("Tripped on a cart", "Trébuché sur un chariot"));
                    return;
                case ET.Poubelle:
                    if (py > 0.85f) return;
                    e.hit = true;
                    Break(e, gear[GShield] ? 1.5f : 1f);
                    if (gear[GShield]) return;
                    Hurt(1, L("Crashed into boxes", "Foncé dans des cartons"));
                    return;
                case ET.Banderole:
                    if (h < 1.5f) return;
                    e.hit = true;
                    Break(e, helmetLv >= 2 ? 1.3f : 0.8f);
                    if (helmetLv >= 2) { hud.Float(new Vector3(e.x, 2.4f, e.z), L("HEADBUTT!", "COUP DE TÊTE !"), Hud.Yellow, true); return; }
                    Hurt(1, L("Hit the SALE sign", "Pris le panneau SOLDES"));
                    return;
                case ET.Voiture:
                    if (py > 1.45f) return;
                    e.hit = true;
                    Snd.Play(Snd.I.crash, 1f);
                    Hurt(2, L("Crashed into a kiosk", "Foncé dans un kiosque"));
                    return;
                case ET.Manif:
                    e.hit = true;
                    foreach (Rig r in e.crowd) r.KO(new Vector3(Mathf.Sign(r.root.position.x - px + 0.01f) * 3f, 2.5f, speed * 0.6f), Vector3.zero);
                    bavures++;
                    Hurt(1, L("Ran into shoppers", "Foncé dans des clients"));
                    return;
            }
        }

        /// <summary>L'obstacle part en morceaux physiques.</summary>
        void Break(Ent e, float force)
        {
            Fx.Debris(new Vector3(e.x, 0.8f, e.z));
            Snd.Play(Snd.I.crash, 0.6f);
            var col = e.go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.6f, 0f);
            col.size = new Vector3(e.type == ET.Poubelle ? 0.9f : 2.2f, 1.1f, 0.7f);
            var rb = e.go.AddComponent<Rigidbody>();
            rb.mass = 40f;
            float side = Mathf.Sign(e.x - px + 0.01f);
            rb.AddForce(new Vector3(side * 3f * force, 5f * force, speed * 0.9f + 3f * force), ForceMode.VelocityChange);
            rb.AddTorque(new Vector3(Random.Range(-6f, 6f), Random.Range(-4f, 4f), Random.Range(-6f, 6f)) * force, ForceMode.VelocityChange);
        }

        void Pickup(ET t, Vector3 p)
        {
            // arcade : les armes ramassées sont des caisses qui fusionnent (voir GameArcade)
            if (Tuning.Arcade && (t == ET.Pistolet || t == ET.Mitraillette)) { Fx.Pickup(p); WeaponCrate(p); return; }
            Fx.Pickup(p);
            Fx.Sparkle(p + Vector3.up * 0.5f);
            P.Flash(new Color(1f, 0.95f, 0.6f), 0.1f);
            Snd.Play(Snd.I.pickup, 0.7f, 0f);
            switch (t)
            {
                case ET.Casque: GotGear(GHelmet, L("HELMET!", "CASQUE !"), L("Boxes and signs bounce off", "Cartons et panneaux rebondissent")); break;
                case ET.Gilet: GotGear(GVest, L("ARMOR PLATE!", "PLAQUE D'ARMURE !"), L("Blocks one hit", "Bloque un coup")); break;
                case ET.Matraque: GotGear(GBaton, L("BATON!", "MATRAQUE !"), L("Wide swings, they fly further", "Coups larges, ils volent plus loin")); break;
                case ET.Bouclier:
                    GotGear(GShield, L("CHARGE!", "CHARGE !"), M(L("Smash through robbers and carts", "Fonce à travers voleurs et chariots"), L("Smash through cops and carts", "Fonce à travers policiers et chariots")) + (campaign && !tutorialMode ? " (" + Mathf.RoundToInt(BalanceConfig.I.shieldDuration) + " s)" : ""));
                    if (campaign && !tutorialMode) shieldT = BalanceConfig.I.shieldDuration;
                    break;
                case ET.Pistolet: GotGear(GPistol, L("STUN PISTOL!", "PISTOLET PARALYSANT !"), L("Tap to shoot from afar", "Tape pour tirer de loin")); break;
                case ET.Mitraillette: GotGear(GSmg, L("SUBMACHINE GUN!", "MITRAILLETTE !"), L("Fires on its own. Steer into them!", "Elle tire toute seule. Fonce sur eux !")); break;
                case ET.Renfort:
                    ren = 8f;
                    buddyX = px;
                    hud.Toast(L("BACKUP!", "RENFORTS !"));
                    hud.Ticker(M(L("Backup is on the way!", "Les renforts arrivent !"), L("Your accomplice joins the run!", "Ton complice te rejoint !")), false);
                    break;
                case ET.Cafe:
                {
                    // trousse de secours : la jauge remonte en vert
                    int before = hearts;
                    hearts = Mathf.Min(MaxHearts, hearts + Tuning.MedkitHeal);
                    int healed = hearts - before;
                    medkitsTaken++;
                    if (healed > 0) { hud.Toast(L("MEDKIT +", "SOIN +") + healed); hud.Float(new Vector3(px, 2.6f, pz), "+" + healed, Hud.HealGreen, true); }
                    else { score += 150f; hud.Toast(L("FULL HEALTH: +150", "SANTÉ MAX : +150")); }
                    P.Flash(Hud.HealGreen, 0.2f);
                    break;
                }
            }
        }

        void UpdateBuddy(float dt)
        {
            bool on = ren > 0f;
            if (buddy.root.gameObject.activeSelf != on) buddy.root.gameObject.SetActive(on);
            if (!on) return;
            int bl = lane == 1 ? (Mathf.Sin(Time.time * 0.5f) > 0f ? 2 : 0) : 1;
            buddyX = Mathf.Lerp(buddyX, (bl - 1) * Lane, 1f - Mathf.Exp(-6f * dt));
            buddy.root.localPosition = new Vector3(buddyX, 0f, pz + 0.8f);
            buddy.anim = Anim.Run;
            buddy.speed = speed;
            buddy.Tick(dt);
            foreach (Ent e in ents)
            {
                if (e.type != ET.Casseur || e.hit) continue;
                float d = e.z - pz;
                if (d > 0f && d < 7f && Mathf.Abs(e.x - px) > 1f)
                {
                    buddyX = e.x;
                    buddy.swingT = 0f;
                    HitCasseur(e, true);
                    break;
                }
            }
        }

        // ================================================================== génération

        static int[] ShuffledLanes()
        {
            int[] l = { 0, 1, 2 };
            for (int i = 2; i > 0; i--) { int j = Random.Range(0, i + 1); int t = l[i]; l[i] = l[j]; l[j] = t; }
            return l;
        }

        float SpawnRow(float z)
        {
            float hard = Mathf.Clamp01(dist / 2000f);
            int[] lanes = { 0, 1, 2 };
            for (int i = 2; i > 0; i--) { int j = Random.Range(0, i + 1); int tmp = lanes[i]; lanes[i] = lanes[j]; lanes[j] = tmp; }

            // arène du boss : en fin d'étape, plus que des pièces (et un café) pour laisser place au combat
            if (!demo && z - stageStartZ > StageLen)
            {
                // pendant le boss : jamais de trousse en campagne (tension jusqu'au bout)
                if (plan == null && Random.value < 0.35f) Spawn(ET.Cafe, lanes[0], z);
                else for (int i = 0; i < 4; i++) Spawn(ET.Piece, lanes[0], z + i * 2f);
                return 14f;
            }
            if (plan != null && campaign && !tutorialMode && !demo) return SpawnPlanned(z, lanes);

            // pièce d'équipement suivante, sur une rangée dégagée, pièces d'or en guide
            int g = MissingGear();
            if (g >= 0 && z >= nextGearZ)
            {
                nextGearZ = z + gearGap;
                Spawn(GearOrder[g], lanes[0], z + 6f);
                for (int i = 0; i < 3; i++) Spawn(ET.Piece, lanes[0], z + i * 1.8f);
                if (Random.value < 0.7f) SpawnPack(lanes[1], z + Random.Range(4f, 8f), Random.Range(2, 4));
                return 16f;
            }
            float r = Random.value;
            if (Looting)
            {
                float lr = Random.value;
                if (lr < Tuning.StoreAttackShare) return SpawnRaid(z);
                if (lr < Tuning.StoreAttackShare + Tuning.StoreExitShare) return SpawnStoreExit(z, lanes);
                if (lr < Tuning.StoreAttackShare + Tuning.StoreExitShare + Tuning.AmbushShare) return SpawnAmbush(z);
            }

            if (r < 0.1f && (!campaign || Allowed(Tuning.IntroBarriers) || Random.value < 0.4f))
            {
                ET t = Random.value < 0.55f ? ET.Barricade : ET.Banderole;
                for (int l = 0; l < 3; l++) if (!Tuning.Arcade || l != lanes[0]) Spawn(t, l, z);
                if (t == ET.Barricade)
                    for (int i = 0; i < 3; i++) Spawn(ET.Piece, lanes[0], z + (i - 1) * 1.4f).y = 1.6f - Mathf.Abs(i - 1) * 0.4f;
                return 11f;
            }
            if (r < 0.44f)
            {
                // une bande d'adversaires, plus grosse quand ton escouade grossit
                int n = campaign ? Random.Range(Tuning.PackMin[Diff], Tuning.PackMax[Diff] + 1) + squad.Count / 4
                                 : Mathf.Clamp(2 + Mathf.RoundToInt(Random.Range(0f, 2f + hard * 3f)) + squad.Count / 3 + (stage - 1), 2, 9);
                SpawnPack(lanes[0], z, n);
                // trousse risquée : derrière la bande, il faut la traverser pour la prendre
                if (Random.value < Tuning.RiskyMedkit) Spawn(ET.Cafe, lanes[0], z + n / 3 * 1.15f + 2.5f);
                if (Random.value < 0.5f) Spawn(Random.value < 0.5f ? ET.Manif : ET.Voiture, lanes[2], z + Random.Range(0f, 4f));
                else for (int i = 0; i < 4; i++) Spawn(ET.Piece, lanes[2], z + i * 2f);
                return 17f + (1f - hard) * 4f;
            }
            if (r < 0.6f && squad.Count < MaxSquad)
            {
                // des collègues qui attendent qu'on passe les chercher
                int n = squad.Count < 3 ? Random.Range(2, 4) : Random.Range(1, 3);
                SpawnRecruits(lanes[0], z + 3f, n);
                SpawnPack(lanes[1], z + Random.Range(6f, 10f), Random.Range(2, 4));
                if (Random.value < 0.4f) Spawn(ET.Poubelle, lanes[2], z + 4f);
                return 16f;
            }
            if (r < 0.76f)
            {
                ET[] pool = { ET.Voiture, ET.Manif, ET.Poubelle, ET.Barricade, ET.Banderole };
                Spawn(pool[Random.Range(0, pool.Length)], lanes[0], z);
                // trousse risquée : juste derrière l'obstacle, dans le même couloir
                if (Random.value < Tuning.RiskyMedkit) Spawn(ET.Cafe, lanes[0], z + 3.2f);
                SpawnPack(lanes[1], z + Random.Range(3f, 7f), Random.Range(1, 3));
                if (Random.value < 0.3f + hard * 0.3f)
                {
                    ET[] pool2 = { ET.Poubelle, ET.Manif, ET.Voiture };
                    Spawn(pool2[Random.Range(0, pool2.Length)], lanes[2], z + Random.Range(6f, 10f));
                }
                return 14f + (1f - hard) * 5f;
            }
            if (r < 0.86f)
            {
                Spawn(ET.Manif, lanes[0], z);
                Spawn(ET.Manif, lanes[1], z);
                for (int i = 0; i < 5; i++) Spawn(ET.Piece, lanes[2], z + i * 2f);
                return 13f;
            }
            ET[] block = { ET.Casseur, ET.Poubelle, ET.Barricade };
            Spawn(ET.Cafe, lanes[0], z); // trousse de secours dans un couloir dégagé
            Spawn(block[Random.Range(0, block.Length)], lanes[1], z);
            return 12f;
        }

        // ================================================================== map 2 : pillage

        /// <summary>Des pillards jaillissent des boutiques de part et d'autre et rejoignent la piste en diagonale.</summary>
        float SpawnStoreExit(float z, int[] lanes, int n = -1)
        {
            if (n < 0) n = Random.Range(2, 4) + Diff / 3;
            for (int i = 0; i < n; i++)
            {
                int side = i % 2 == 0 ? -1 : 1;
                int ln = side < 0 ? Random.Range(0, 2) : Random.Range(1, 3);
                Ent e = Spawn(ET.Casseur, ln, z + i * 2.2f, Mode.Sortie);
                e.x = side * (Street.ShopX - 0.8f);
                e.go.transform.localPosition = new Vector3(e.x, 0f, e.z);
                e.rig.anim = Anim.Stand;
            }
            Fx.Debris(new Vector3(-(Street.ShopX - 0.6f), 1f, z));
            for (int i = 0; i < 3; i++) Spawn(ET.Piece, lanes[0], z + 6f + i * 1.8f);
            return 16f;
        }

        /// <summary>Embuscade : une bande dans chaque couloir en même temps.</summary>
        float SpawnAmbush(float z, int n = -1)
        {
            if (n < 0) n = 2 + Diff / 3;
            for (int l = 0; l < 3; l++) SpawnPack(l, z + Random.Range(-0.6f, 0.6f), n);
            hud.Float(new Vector3(0f, 3f, z), L("AMBUSH!", "EMBUSCADE !"), Hud.Bad, false);
            return 18f;
        }

        /// <summary>
        /// Magasin attaqué : 3 ou 4 pillards cassent la vitrine sous un gyrophare rouge.
        /// Tous neutralisés : STORE SAVED (+étoiles, bonus si un Perfect Hit). L'un d'eux s'échappe : STORE LOOTED.
        /// </summary>
        float SpawnRaid(float z, int n = -1)
        {
            int side = Random.value < 0.5f ? -1 : 1;
            int ln = side < 0 ? 0 : 2;
            var raid = new StoreRaid { z = z, side = side };
            // côté émeutiers : un barrage de police à percer (gyrophare bleu) au lieu d'un magasin à défendre
            raid.beacon = Street.Beacon(world, new Vector3(side * (Street.ShopX - 0.15f), 3.4f, z), thief ? new Color(0.2f, 0.45f, 1f) : new Color(1f, 0.12f, 0.1f));
            raid.beacon.localRotation = Quaternion.Euler(0f, 0f, side * 90f);
            if (n < 0) n = Random.Range(3, 5);
            int id = raids.Count;
            for (int i = 0; i < n; i++)
            {
                Ent e = Spawn(ET.Casseur, ln, thief ? z : z + (i / 2) * 1.2f, Mode.Casse);
                if (thief) e.x = Mathf.Clamp(e.x + (i - (n - 1) * 0.5f) * 0.75f, -3.4f, 3.4f); // ligne de boucliers
                else e.x = Mathf.Clamp(e.x + side * ((i % 2) * 0.9f + 0.2f), -3.4f, 3.4f);
                e.go.transform.localPosition = new Vector3(e.x, 0f, e.z);
                e.rig.anim = thief ? Anim.Stand : Anim.Smash;
                e.raid = id;
            }
            raid.left = n;
            raids.Add(raid);
            Art.Decal(world, Art.Particle(Art.SoftDot, thief ? new Color(0.2f, 0.4f, 1f, 0.35f) : new Color(1f, 0.1f, 0.1f, 0.35f)), new Vector3((ln - 1) * Lane, 0.04f, z + 0.6f), new Vector2(3.4f, 3.6f));
            // le couloir opposé reste jouable : étoiles ou trousse
            // trousse : seulement hors campagne (en campagne, c'est le plan d'équilibrage qui les place)
            if (plan == null && Random.value < 0.3f) Spawn(ET.Cafe, 2 - ln, z + 2f);
            else for (int i = 0; i < 3; i++) Spawn(ET.Piece, 1, z + 4f + i * 1.8f);
            return 17f;
        }

        void UpdateRaids()
        {
            for (int i = 0; i < raids.Count; i++)
            {
                StoreRaid r = raids[i];
                if (!r.told && r.left > 0 && r.z - pz < 30f && r.z - pz > 8f)
                {
                    r.told = true;
                    hud.Float(new Vector3(r.side * 2.4f, 3.2f, r.z), M(L("STORE UNDER ATTACK!", "MAGASIN ATTAQUÉ !"), L("POLICE BARRAGE!", "BARRAGE DE POLICE !")), Hud.Bad, true);
                    Snd.PlayPitch(Snd.I.siren, 0.25f, 1.3f);
                }
                if (r.beacon != null && r.z < pz - 20f) { Destroy(r.beacon.gameObject); r.beacon = null; }
            }
        }

        /// <summary>Un pillard d'un magasin attaqué est assommé (ko) ou s'est échappé.</summary>
        void RaidMemberGone(Ent e, bool ko)
        {
            if (e.raid < 0 || e.raid >= raids.Count) return;
            StoreRaid r = raids[e.raid];
            e.raid = -1;
            if (r.left <= 0) return;
            r.left--;
            if (!ko) r.lost = true;
            if (ko && superHitNow) r.perfect = true;
            if (r.left > 0) return;
            Vector3 at = new Vector3(r.side * 2.4f, 2.8f, Mathf.Max(r.z, pz + 3f));
            if (r.lost)
            {
                hud.Float(at, M(L("STORE LOOTED", "MAGASIN PILLÉ"), L("BARRAGE HELD", "LE BARRAGE TIENT")), Hud.Info, true);
                return;
            }
            int stars = Tuning.StoreSaveStars + (r.perfect ? Tuning.StoreSavePerfectBonus : 0);
            coins += stars;
            starsStores += stars;
            storesSaved++;
            hud.Toast(M(L("STORE SAVED!", "MAGASIN SAUVÉ !"), L("BARRAGE BROKEN!", "BARRAGE ENFONCÉ !")));
            hud.Float(at, "+" + stars + " " + Tuning.Cur + (r.perfect ? L("  PERFECT BONUS!", "  BONUS PARFAIT !") : ""), Hud.StarGold, true);
            Snd.Play(Snd.I.pickup, 0.8f, 0f);
            Haptics.Pulse(40, true);
            Fx.Confetti(at);
            if (r.beacon != null)
            {
                // le gyrophare passe au vert : magasin protégé
                Vector3 bp = r.beacon.localPosition;
                Quaternion br = r.beacon.localRotation;
                Destroy(r.beacon.gameObject);
                r.beacon = Street.Beacon(world, bp, thief ? Hud.StarGold : new Color(0.2f, 1f, 0.35f));
                r.beacon.localRotation = br;
            }
        }

        /// <summary>Émeutier armé de la barre de fer : le coup casse aussi les obstacles devant lui (destruction du décor).</summary>
        void SmashProps(float reach)
        {
            foreach (Ent e in ents)
            {
                if (e.hit || e.dead) continue;
                if (e.type != ET.Barricade && e.type != ET.Poubelle && e.type != ET.Banderole) continue;
                float d = e.z - pz;
                if (d < -0.5f || d > reach || Mathf.Abs(e.x - px) > 1.3f) continue;
                e.hit = true;
                e.dead = true;
                Vector3 at = new Vector3(e.x, 0.8f, e.z);
                Fx.Debris(at);
                Fx.Dust(new Vector3(e.x, 0.1f, e.z));
                Snd.PlayPitch(Snd.I.crash, 0.6f, Random.Range(0.9f, 1.1f));
                Snd.PlayPitch(Snd.I.glass, 0.35f, 0.9f);
                shake = Mathf.Max(shake, 0.25f);
                coins += Tuning.PropSmashStars;
                starsPickup += Tuning.PropSmashStars;
                hud.Float(at + Vector3.up, L("SMASHED! +", "CASSÉ ! +") + Tuning.PropSmashStars, Hud.StarGold, true);
                Destroy(e.go);
            }
        }

        /// <summary>Rayonnage renversé en travers du couloir, marchandise éparpillée (à sauter).</summary>
        void ToppledShelf(Transform t)
        {
            // rayon renversé 3D (télé fêlée tournée vers le joueur), en travers du couloir
            if (ObstacleModel(t, "obs_rayon", 2.3f, Vector3.zero, 180f + Random.Range(-8f, 8f))) return;
            Material metal = Art.Mat(new Color(0.62f, 0.64f, 0.68f), 0.7f, 0.6f);
            Material red = Art.Mat(new Color(0.8f, 0.15f, 0.15f), 0.4f);
            Transform s = Art.Node(t, "shelf", new Vector3(0f, 0.32f, 0f));
            s.localRotation = Quaternion.Euler(0f, Random.Range(-8f, 8f), 72f);
            Art.Part(s, Art.Cube, metal, new Vector3(0f, 0f, -0.25f), new Vector3(0.7f, 2.2f, 0.06f));
            for (int i = 0; i < 4; i++)
                Art.Part(s, Art.Cube, metal, new Vector3(0f, -0.95f + i * 0.62f, 0f), new Vector3(0.7f, 0.05f, 0.5f));
            Art.Part(s, Art.Cube, red, new Vector3(-0.2f, -0.6f, 0.05f), new Vector3(0.25f, 0.3f, 0.25f));
            Art.Part(s, Art.Cube, Art.Mat(new Color(0.2f, 0.45f, 0.9f), 0.4f), new Vector3(0.15f, 0.05f, 0.05f), new Vector3(0.3f, 0.22f, 0.3f));
            Color[] goods = { new Color(0.95f, 0.8f, 0.2f), new Color(0.9f, 0.3f, 0.5f), new Color(0.3f, 0.75f, 0.4f), Color.white };
            for (int i = 0; i < 6; i++)
                Art.Part(t, Art.Cube, Art.Mat(goods[i % goods.Length], 0.35f), new Vector3(Random.Range(-1f, 1f), 0.09f, Random.Range(-0.6f, 0.6f)),
                         new Vector3(0.22f, 0.16f, 0.3f), new Vector3(0f, Random.Range(0f, 360f), 0f), false);
        }

        /// <summary>Pile de cartons de télés éventrés (à sauter).</summary>
        void LootPile(Transform t)
        {
            Material card = Art.Mat(new Color(0.72f, 0.55f, 0.36f), 0.15f);
            Material screen = Art.Glow(new Color(0.08f, 0.08f, 0.1f), new Color(0.15f, 0.35f, 0.7f), 0.9f);
            Material brand = Art.Mat(new Color(0.1f, 0.25f, 0.6f), 0.3f);
            Art.Part(t, Art.Cube, card, new Vector3(-0.3f, 0.38f, 0f), new Vector3(1.1f, 0.76f, 0.3f), new Vector3(0f, 8f, 0f));
            Art.Part(t, Art.Cube, brand, new Vector3(-0.3f, 0.45f, -0.16f), new Vector3(0.7f, 0.25f, 0.01f), new Vector3(0f, 8f, 0f), false);
            Art.Part(t, Art.Cube, card, new Vector3(0.45f, 0.32f, 0.15f), new Vector3(0.9f, 0.64f, 0.28f), new Vector3(0f, -14f, 6f));
            // télé sortie de son carton, écran fêlé
            Transform tv = Art.Node(t, "tv", new Vector3(0.05f, 0.95f, 0f));
            tv.localRotation = Quaternion.Euler(0f, 6f, -12f);
            Art.Part(tv, Art.Cube, Art.Mat(new Color(0.1f, 0.1f, 0.12f), 0.7f), Vector3.zero, new Vector3(1.0f, 0.6f, 0.08f));
            Art.Part(tv, Art.Cube, screen, new Vector3(0f, 0f, -0.045f), new Vector3(0.9f, 0.5f, 0.01f), default, false);
            Art.Part(tv, Art.Quad, Art.Particle(Art.Cracks, Color.white), new Vector3(0f, 0f, -0.055f), new Vector3(0.9f, 0.5f, 1f), new Vector3(0f, 0f, 0f), false);
        }

        /// <summary>Vie → protections visibles (0 rien, 1 casque, 2 casque + plastron).</summary>
        int ArmorVis(int hp)
        {
            if (!campaign || tutorialMode) return Mathf.Clamp(hp - 1, 0, 2);
            BalanceConfig c = BalanceConfig.I;
            return hp >= c.hpHeavy[Diff] ? 2 : hp >= c.hpArmored[Diff] ? 1 : 0;
        }

        static ET ObsType(Balance.Obs o)
        {
            return o == Balance.Obs.Sign ? ET.Banderole : o == Balance.Obs.Kiosk ? ET.Voiture : o == Balance.Obs.Boxes ? ET.Poubelle : ET.Barricade;
        }

        /// <summary>
        /// Rangée suivante du plan d'équilibrage : mêmes situations qu'avant, mais quantités, vies et trousses fixées par le plan
        /// (voir Balance.cs) au lieu d'être tirées au hasard.
        /// </summary>
        float SpawnPlanned(float z, int[] lanes)
        {
            if (planIdx >= plan.segs.Count)
            {
                for (int i = 0; i < 4; i++) Spawn(ET.Piece, lanes[0], z + i * 2f);
                return 14f;
            }
            Balance.Seg g = plan.segs[planIdx++];
            hpQueue.Clear();
            foreach (int h in g.hp) hpQueue.Enqueue(h);
            bool risky = g.medkit == 2, safe = g.medkit == 1;
            switch (g.kind)
            {
                case Balance.Kind.Pack:
                    SpawnPack(lanes[0], z, g.n);
                    if (risky) Spawn(ET.Cafe, lanes[0], z + (g.n - 1) / 3 * 1.15f + 2.5f);
                    if (!safe) for (int i = 0; i < 4; i++) Spawn(ET.Piece, lanes[2], z + i * 2f);
                    break;
                case Balance.Kind.Barrier:
                {
                    ET t = ObsType(g.obs == Balance.Obs.Sign ? Balance.Obs.Sign : Balance.Obs.Carts);
                    for (int l = 0; l < 3; l++) if (!Tuning.Arcade || l != lanes[0]) Spawn(t, l, z);
                    if (t == ET.Barricade)
                        for (int i = 0; i < 3; i++) Spawn(ET.Piece, lanes[0], z + (i - 1) * 1.4f).y = 1.6f - Mathf.Abs(i - 1) * 0.4f;
                    break;
                }
                case Balance.Kind.Obstacle:
                    Spawn(ObsType(g.obs), lanes[0], z);
                    if (risky) Spawn(ET.Cafe, lanes[0], z + (g.obs == Balance.Obs.Kiosk ? 4.5f : 3.2f));
                    if (g.side > 0) SpawnPack(lanes[1], z + Random.Range(3f, 7f), g.side);
                    break;
                case Balance.Kind.Recruits:
                    SpawnRecruits(lanes[0], z + 3f, g.recruits);
                    if (g.side > 0) SpawnPack(lanes[1], z + Random.Range(6f, 10f), g.side);
                    break;
                case Balance.Kind.Shoppers:
                    Spawn(ET.Manif, lanes[0], z);
                    Spawn(ET.Manif, lanes[1], z);
                    if (!safe) for (int i = 0; i < 5; i++) Spawn(ET.Piece, lanes[2], z + i * 2f);
                    break;
                case Balance.Kind.Thrower:
                    for (int i = 0; i < g.n; i++) Spawn(ET.Casseur, lanes[i % 2], z + i * 3f, Mode.Lance);
                    if (!safe) for (int i = 0; i < 4; i++) Spawn(ET.Piece, lanes[2], z + i * 2f);
                    break;
                case Balance.Kind.Raid: SpawnRaid(z, g.n); break;
                case Balance.Kind.Exit: SpawnStoreExit(z, lanes, g.n); break;
                case Balance.Kind.Ambush: SpawnAmbush(z, g.n); break;
                case Balance.Kind.Gear:
                {
                    int gi = MissingGear();
                    if (gi >= 0) Spawn(GearOrder[gi], lanes[0], z + 6f);
                    for (int i = 0; i < 3; i++) Spawn(ET.Piece, lanes[0], z + i * 1.8f);
                    if (g.side > 0) SpawnPack(lanes[1], z + Random.Range(4f, 8f), g.side);
                    break;
                }
                default:
                    for (int i = 0; i < 5; i++) Spawn(ET.Piece, lanes[0], z + i * 2f);
                    break;
            }
            // trousse « sûre » : dans le couloir resté libre
            if (safe) Spawn(ET.Cafe, lanes[2], z + 1f);
            hpQueue.Clear();
            return g.spacing;
        }

        Ent SpawnRecruits(int ln, float z, int n)
        {
            Ent e = Spawn(ET.Recrue, ln, z);
            n = Mathf.Clamp(n, 1, e.crowd.Count);
            e.hp = n;
            while (e.crowd.Count > n)
            {
                Rig r = e.crowd[e.crowd.Count - 1];
                e.crowd.RemoveAt(e.crowd.Count - 1);
                Destroy(r.root.gameObject);
            }
            return e;
        }

        /// <summary>Bande de n adversaires groupés autour d'un couloir (3 de front, en rangs).</summary>
        void SpawnPack(int ln, float z, int n)
        {
            bool hard = campaign && !tutorialMode;
            Mode m = Random.value < (hard ? 0.85f : 0.7f) ? Mode.Fonce : Mode.Casse;
            float vz = Random.Range(4.5f, 7f) * (hard ? Tuning.EnemyRush : 1f);
            for (int i = 0; i < n; i++)
            {
                int row = i / 3, col = i % 3;
                float off = n == 1 ? 0f : (col - 1) * 0.85f + Random.Range(-0.15f, 0.15f);
                if (row > 0 && n - row * 3 < 3) off += 0.42f * (row % 2 == 1 ? 1f : -1f);
                Ent e = Spawn(ET.Casseur, ln, z + row * 1.15f + Random.Range(-0.2f, 0.2f), m);
                e.x = Mathf.Clamp(e.x + off, -3.4f, 3.4f);
                e.vz = vz;
                e.go.transform.localPosition = new Vector3(e.x, 0f, e.z);
            }
        }

        Ent Spawn(ET type, int ln, float z, Mode? mode = null)
        {
            // obstacles pas encore introduits dans cette rue : remplacés par un plus simple
            if (campaign && !tutorialMode)
            {
                if (type == ET.Banderole && !Allowed(Tuning.IntroSigns)) type = ET.Barricade;
                if (type == ET.Voiture && !Allowed(Tuning.IntroKiosks)) type = Allowed(Tuning.IntroShoppers) ? ET.Manif : ET.Poubelle;
                if (type == ET.Manif && !Allowed(Tuning.IntroShoppers)) type = ET.Poubelle;
            }
            var e = new Ent { type = type, lane = ln, x = (ln - 1) * Lane, z = z, y = 0f };
            e.go = new GameObject(type.ToString());
            e.go.transform.SetParent(world, false);
            e.go.transform.localPosition = new Vector3(e.x, 0f, z);
            Transform t = e.go.transform;

            switch (type)
            {
                case ET.Casseur:
                {
                    e.depth = 0.55f;
                    e.mode = mode ?? (Mode)Random.Range(0, 3);
                    if (e.mode == Mode.Lance && !Allowed(Tuning.IntroThrowers)) e.mode = Mode.Fonce;
                    if (Random.value < 0.25f && mode == null) e.mode = Mode.Casse;
                    int v = Random.Range(0, 6);
                    e.hp = hpQueue.Count > 0 ? hpQueue.Dequeue() : EnemyHp();
                    Rig.OutlineOverride = Art.OutlineTint(DangerRed);
                    int armorVis = ArmorVis(e.hp);
                    e.rig = thief ? Rig.Build(Kind.Guard, t, Vector3.zero, 180f, 0, true, armorVis) : Rig.Build(Kind.Thief, t, Vector3.zero, 180f, e.mode == Mode.Lance ? 1 : v * 2, true, armorVis);
                    Rig.OutlineOverride = null;
                    e.ring = Art.Decal(t, Art.Particle(Art.SoftDot, new Color(1f, 0.1f, 0.08f, 0.75f)), new Vector3(0f, 0.035f, 0f), new Vector2(1.5f, 1.5f)).gameObject;
                    e.rig.anim = e.mode == Mode.Casse ? Anim.Smash : e.mode == Mode.Fonce ? Anim.Run : Anim.Stand;
                    e.vz = Random.Range(4.5f, 7.5f) * (campaign && !tutorialMode ? Tuning.EnemyRush : 1f);
                    e.timer = Random.Range(0.3f, 1.2f);
                    break;
                }
                case ET.Loup:
                {
                    // boss face au joueur : lanceur, brute (onde de choc) ou chef de bande (appelle des renforts)
                    e.depth = 0.7f;
                    e.timer = 1.5f;
                    float[] sizes = { 1.3f, 1.65f, 1.4f, 1.45f, 1.25f, 1.5f, 1.6f };
                    Rig.OutlineOverride = Art.OutlineTint(DangerRed);
                    e.rig = Rig.Build(Kind.Boss, t, Vector3.zero, 180f, Rig.BossLook + (bossType == 6 ? 12 + (thief ? 1 : 0) : bossType + (thief ? 6 : 0)));
                    Rig.OutlineOverride = null;
                    e.rig.root.localScale = Vector3.one * sizes[bossType] * Rig.Scale;
                    e.rig.anim = Anim.Run;
                    Color[] auras =
                    {
                        new Color(0.7f, 0.2f, 1f, 0.6f), new Color(1f, 0.35f, 0.1f, 0.65f), new Color(1f, 0.8f, 0.2f, 0.6f),
                        new Color(1f, 0.2f, 0.25f, 0.6f), new Color(0.2f, 0.9f, 1f, 0.6f), new Color(0.4f, 1f, 0.4f, 0.6f),
                        new Color(1f, 0.15f, 0.1f, 0.75f)
                    };
                    Art.Decal(t, Art.Particle(Art.SoftDot, auras[bossType]), new Vector3(0f, 0.04f, 0f), new Vector2(3.2f, 3.2f));
                    break;
                }
                case ET.Onde:
                    e.depth = 0.35f;
                    e.spin = Art.Node(t, "wave", Vector3.zero);
                    Art.Part(e.spin, Art.Cube, Art.Glow(new Color(1f, 0.15f, 0.1f), new Color(2.6f, 0.3f, 0.15f)), new Vector3(0f, 0.18f, 0f), new Vector3(7.8f, 0.3f, 0.4f), default, false);
                    Art.Decal(e.spin, Art.Particle(Art.SoftDot, new Color(1f, 0.15f, 0.1f, 0.85f)), new Vector3(0f, 0.03f, 0f), new Vector2(8.5f, 1.6f));
                    break;
                case ET.Manif:
                    e.depth = 0.5f;
                    e.crowd = new List<Rig>();
                    for (int i = 0; i < 2; i++)
                    {
                        Rig r = Rig.Build(Kind.Shopper, t, new Vector3(i == 0 ? -0.5f : 0.5f, 0f, Random.Range(-0.2f, 0.2f)), 180f + Random.Range(-15f, 15f), Random.Range(0, 6));
                        r.anim = Anim.Cheer;
                        e.crowd.Add(r);
                    }
                    break;
                case ET.Recrue:
                {
                    // collègues (ou complices) qui attendent en agitant les bras, sur un cercle bleu
                    e.depth = 0.8f;
                    e.hp = 2;
                    e.crowd = new List<Rig>();
                    for (int i = 0; i < 3; i++)
                    {
                        Vector3 pos = new Vector3((i - 1) * 0.55f, 0f, (i % 2) * 0.45f);
                        Rig.OutlineOverride = Art.OutlineTint(AllyBlue);
                        Rig r = thief ? Rig.Build(Kind.Thief, t, pos, 180f, Rig.Crew, true, MateArmor) : Rig.Build(Kind.Guard, t, pos, 180f, 0, true, MateArmor);
                        Rig.OutlineOverride = null;
                        AllyRing(r);
                        r.anim = Anim.Cheer;
                        e.crowd.Add(r);
                    }
                    Art.Decal(t, Art.Particle(Art.SoftDot, new Color(0.3f, 0.6f, 1f, 0.7f)), new Vector3(0f, 0.04f, 0.2f), new Vector2(2.8f, 2.8f));
                    break;
                }
                case ET.Pave:
                    e.depth = 0.35f;
                    e.spin = Art.Node(t, "pave", Vector3.zero);
                    // caisse en bois 3D (projectile qui tourne : centrée sur son pivot)
                    if (ObstacleModel(e.spin, "obs_caisse", 0.34f, Vector3.zero, 0f))
                    {
                        Transform cm = e.spin.GetChild(e.spin.childCount - 1);
                        cm.localPosition += new Vector3(0f, -0.17f, 0f);
                        break;
                    }
                    Art.Part(e.spin, Art.Cube, Art.Mat(new Color(0.95f, 0.55f, 0.15f), 0.3f), Vector3.zero, new Vector3(0.36f, 0.22f, 0.26f));
                    Art.Part(e.spin, Art.Cube, Art.Mat(Color.white, 0.3f), new Vector3(0f, 0.112f, 0f), new Vector3(0.2f, 0.01f, 0.27f), default, false);
                    break;
                case ET.Poubelle:
                    e.depth = 0.45f;
                    if (Looting) { if (!ObstacleModel(t, "obs_butin", 1.6f, Vector3.zero, Random.Range(-15f, 15f))) LootPile(t); break; }
                    // pile de cartons 3D (deux piles côte à côte), sinon les cartons dessinés
                    if (ObstacleModel(t, "obs_cartons", 0.72f, new Vector3(-0.4f, 0f, 0f), Random.Range(-12f, 12f)))
                    {
                        ObstacleModel(t, "obs_cartons", 0.66f, new Vector3(0.42f, 0f, 0.05f), Random.Range(-20f, 20f));
                        break;
                    }
                    // pile de cartons de livraison (à sauter)
                    Material card = Art.Mat(new Color(0.72f, 0.55f, 0.36f), 0.15f), tape = Art.Mat(new Color(0.85f, 0.75f, 0.55f), 0.4f);
                    Art.Part(t, Art.Cube, card, new Vector3(-0.35f, 0.3f, 0f), new Vector3(0.75f, 0.6f, 0.7f), new Vector3(0f, 6f, 0f));
                    Art.Part(t, Art.Cube, card, new Vector3(0.4f, 0.28f, 0.05f), new Vector3(0.7f, 0.56f, 0.65f), new Vector3(0f, -9f, 0f));
                    Art.Part(t, Art.Cube, card, new Vector3(0.05f, 0.86f, 0f), new Vector3(0.7f, 0.52f, 0.62f), new Vector3(0f, 14f, 0f));
                    Art.Part(t, Art.Cube, tape, new Vector3(0.05f, 1.125f, 0f), new Vector3(0.12f, 0.01f, 0.63f), new Vector3(0f, 14f, 0f), false);
                    break;
                case ET.Barricade:
                {
                    e.depth = 0.45f;
                    if (Looting) { ToppledShelf(t); break; }
                    // rangée de caddies (à sauter)
                    for (int c = -1; c <= 1; c += 2) Cart(t, new Vector3(c * 0.55f, 0f, c * 0.1f));
                    break;
                }
                case ET.Banderole:
                {
                    e.depth = 0.25f;
                    // portique SALE 3D : étiré en hauteur pour que la banderole passe au-dessus de la glissade
                    if (ObstacleModel(t, "obs_portique", 2.5f, Vector3.zero, 0f))
                    {
                        Transform pm = t.GetChild(t.childCount - 1);
                        pm.localScale = Vector3.Scale(pm.localScale, new Vector3(1f, 2.0f, 1f));
                        pm.localPosition = new Vector3(pm.localPosition.x, pm.localPosition.y * 2.0f, pm.localPosition.z);
                        break;
                    }
                    // panneau SALE sur portique (à passer en glissant)
                    Material pole = Art.Mat(new Color(0.8f, 0.82f, 0.85f), 0.8f, 0.9f);
                    Art.Part(t, Art.Cylinder, pole, new Vector3(-1.15f, 1.3f, 0f), new Vector3(0.07f, 1.3f, 0.07f));
                    Art.Part(t, Art.Cylinder, pole, new Vector3(1.15f, 1.3f, 0f), new Vector3(0.07f, 1.3f, 0.07f));
                    Art.Part(t, Art.Cube, Art.Glow(new Color(0.92f, 0.12f, 0.16f), new Color(0.7f, 0.06f, 0.08f)), new Vector3(0f, 1.95f, 0f), new Vector3(2.3f, 0.8f, 0.06f));
                    Art.Part(t, Art.Cube, Art.Glow(Color.white, new Color(1.2f, 1.2f, 1.2f)), new Vector3(0f, 1.95f, -0.04f), new Vector3(1.4f, 0.3f, 0.02f), default, false);
                    Art.Part(t, Art.Cube, Art.Glow(Pal.Jaune, Pal.Jaune * 0.8f), new Vector3(0.95f, 2.25f, -0.05f), new Vector3(0.3f, 0.3f, 0.02f), new Vector3(0f, 0f, 45f), false);
                    break;
                }
                case ET.Voiture:
                {
                    e.depth = 1.9f;
                    // kiosque de démonstration 3D, dans l'axe du couloir
                    if (ObstacleModel(t, "obs_kiosque", 3.6f, Vector3.zero, 90f)) break;
                    // kiosque de démonstration (à sauter, 2 cœurs)
                    Material wood = Art.Mat(new Color(0.95f, 0.95f, 0.96f), 0.6f), top = Art.Mat(new Color(0.15f, 0.15f, 0.18f), 0.85f);
                    Art.Part(t, Art.Cube, wood, new Vector3(0f, 0.55f, 0f), new Vector3(1.9f, 1.1f, 3.6f));
                    Art.Part(t, Art.Cube, top, new Vector3(0f, 1.13f, 0f), new Vector3(2f, 0.06f, 3.7f));
                    Art.Part(t, Art.Cube, Art.Glow(new Color(0.15f, 0.5f, 0.95f), new Color(0.3f, 0.8f, 1.6f)), new Vector3(0f, 0.6f, -1.81f), new Vector3(1.6f, 0.5f, 0.02f), default, false);
                    for (int i = 0; i < 3; i++)
                        Art.Part(t, Art.Cube, Art.Glow(new Color(0.1f, 0.1f, 0.12f), new Color(0.4f, 0.6f, 1.2f), 0.9f), new Vector3(0f, 1.35f, -1.1f + i * 1.1f), new Vector3(0.7f, 0.42f, 0.05f), new Vector3(-12f, 0f, 0f), false);
                    break;
                }
                case ET.Piece:
                    e.depth = 0.45f;
                    e.y = 0f;
                {
                    e.spin = Art.Node(t, "star", Vector3.zero);
                    // pièce dollar 3D (Meshy) dressée face au joueur, qui tourne ; sinon l'étoile de police dessinée
                    if (GlbModel.Exists("piece"))
                    {
                        Transform c = GlbModel.SpawnGlow("piece", e.spin, 0.55f);
                        Bounds cb = c.GetComponentsInChildren<MeshFilter>()[0].sharedMesh.bounds;
                        float ck = 0.8f / Mathf.Max(0.001f, Mathf.Max(cb.size.x, cb.size.y));
                        c.localScale = Vector3.one * ck;
                        c.localPosition = new Vector3(0f, 1.05f, 0f) - cb.center * ck;
                        Art.Decal(t, Art.Particle(Art.SoftDot, new Color(1f, 0.85f, 0.3f, 0.35f)), new Vector3(0f, 0.03f, 0f), new Vector2(1.3f, 1.3f));
                        break;
                    }
                    // étoile de police : 6 branches à boules, médaillon central, dorée et brillante
                    Material gold = Art.Glow(Pal.Gold, Pal.Gold * 0.7f, 0.85f);
                    Material dark = Art.Glow(new Color(0.75f, 0.5f, 0.1f), new Color(0.4f, 0.25f, 0.05f), 0.8f);
                    Art.Part(e.spin, Art.Star, gold, new Vector3(0f, 1.05f, 0f), new Vector3(0.9f, 0.9f, 0.9f), default, false);
                    for (int k = 0; k < 6; k++)
                    {
                        float a = Mathf.PI * 0.5f + k * Mathf.PI / 3f;
                        Art.Part(e.spin, Art.Sphere, gold, new Vector3(Mathf.Cos(a) * 0.47f, 1.05f + Mathf.Sin(a) * 0.47f, 0f), Vector3.one * 0.11f, default, false);
                    }
                    Art.Part(e.spin, Art.Cylinder, dark, new Vector3(0f, 1.05f, 0f), new Vector3(0.26f, 0.08f, 0.26f), new Vector3(90f, 0f, 0f), false);
                    Art.Decal(t, Art.Particle(Art.SoftDot, new Color(1f, 0.85f, 0.3f, 0.35f)), new Vector3(0f, 0.03f, 0f), new Vector2(1.3f, 1.3f));
                }
                    break;
                default:
                    e.depth = 0.6f;
                    e.spin = Art.Node(t, "pickup", Vector3.zero);
                    BuildPickup(e.spin, type, thief);
                    {
                        Color pc = PickupColor(type);
                        Art.Decal(t, Art.Particle(Art.SoftDot, new Color(pc.r, pc.g, pc.b, 0.7f)), new Vector3(0f, 0.04f, 0f), new Vector2(2.4f, 2.4f));
                        Beam(t, pc);
                    }
                    break;
            }
            // contours cartoon sur les obstacles et bonus (les personnages ont déjà les leurs)
            if (type != ET.Casseur && type != ET.Loup && type != ET.Manif && type != ET.Piece && type != ET.Recrue && type != ET.Onde)
            {
                bool danger = type == ET.Poubelle || type == ET.Barricade || type == ET.Banderole || type == ET.Voiture || type == ET.Pave;
                Art.Outline(t, danger ? 0.035f : 0.03f, Art.OutlineTint(danger ? DangerRed : PickupColor(type)));
                // zone rouge au sol devant l'obstacle : on voit le danger venir
                if (danger && type != ET.Pave)
                    Art.Decal(t, Art.Particle(Art.SoftDot, new Color(1f, 0.08f, 0.06f, 0.6f)), new Vector3(0f, 0.03f, -0.4f), new Vector2(2.4f, e.depth * 2f + 1.6f));
            }
            ents.Add(e);
            return e;
        }

        /// <summary>Caddie en fil métallique.</summary>
        /// <summary>
        /// Obstacle 3D (Meshy) posé au sol : « size » = plus grande dimension au sol, position et orientation données.
        /// Renvoie faux si le modèle manque (on garde alors l'obstacle dessiné).
        /// </summary>
        static bool ObstacleModel(Transform t, string name, float size, Vector3 pos, float yaw)
        {
            if (!GlbModel.Exists(name)) return false;
            Transform m = GlbModel.Spawn(name, t);
            Bounds b = new Bounds();
            bool first = true;
            foreach (MeshFilter f in m.GetComponentsInChildren<MeshFilter>())
            {
                if (first) { b = f.sharedMesh.bounds; first = false; } else b.Encapsulate(f.sharedMesh.bounds);
            }
            float k = size / Mathf.Max(0.001f, Mathf.Max(b.size.x, b.size.z));
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            m.localScale = Vector3.one * k;
            m.localRotation = q;
            Vector3 c = new Vector3(b.center.x, b.min.y, b.center.z) * k;
            m.localPosition = pos - q * c;
            foreach (MeshRenderer r in m.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return true;
        }

        static void Cart(Transform parent, Vector3 pos)
        {
            // caddie 3D : poussé dans l'axe du couloir (côté long le long de la course)
            if (ObstacleModel(parent, "obs_caddie", 1.05f, pos, 90f + Random.Range(-12f, 12f))) return;
            Transform c = Art.Node(parent, "cart", pos);
            c.localRotation = Quaternion.Euler(0f, Random.Range(-12f, 12f), 0f);
            Material wire = Art.Mat(new Color(0.78f, 0.8f, 0.83f), 0.85f, 0.9f);
            Material red = Art.Mat(new Color(0.9f, 0.18f, 0.2f), 0.5f);
            Material wheel = Art.Mat(new Color(0.1f, 0.1f, 0.12f), 0.4f);
            // panier : fond, côtés en grille
            Art.Part(c, Art.Cube, wire, new Vector3(0f, 0.5f, 0f), new Vector3(0.6f, 0.03f, 0.85f));
            for (int i = -2; i <= 2; i++)
            {
                Art.Part(c, Art.Cube, wire, new Vector3(i * 0.15f, 0.8f, -0.42f), new Vector3(0.02f, 0.6f, 0.02f), default, false);
                Art.Part(c, Art.Cube, wire, new Vector3(i * 0.15f, 0.8f, 0.42f), new Vector3(0.02f, 0.6f, 0.02f), default, false);
            }
            for (int k = 0; k < 3; k++)
            {
                float y = 0.6f + k * 0.2f;
                Art.Part(c, Art.Cube, wire, new Vector3(-0.3f, y, 0f), new Vector3(0.02f, 0.02f, 0.85f), default, false);
                Art.Part(c, Art.Cube, wire, new Vector3(0.3f, y, 0f), new Vector3(0.02f, 0.02f, 0.85f), default, false);
                Art.Part(c, Art.Cube, wire, new Vector3(0f, y, -0.42f), new Vector3(0.6f, 0.02f, 0.02f), default, false);
                Art.Part(c, Art.Cube, wire, new Vector3(0f, y, 0.42f), new Vector3(0.6f, 0.02f, 0.02f), default, false);
            }
            Art.Part(c, Art.Cube, red, new Vector3(0f, 1.12f, -0.5f), new Vector3(0.62f, 0.05f, 0.05f));
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = new Vector3(i < 2 ? -0.25f : 0.25f, 0.08f, i % 2 == 0 ? -0.35f : 0.35f);
                Art.Part(c, Art.Cylinder, wheel, p, new Vector3(0.14f, 0.03f, 0.14f), new Vector3(0f, 0f, 90f), false);
                Art.Part(c, Art.Cube, wire, p + new Vector3(0f, 0.22f, 0f), new Vector3(0.02f, 0.4f, 0.02f), default, false);
            }
            // un carton de télé dans le caddie
            if (Random.value < 0.6f)
                Art.Part(c, Art.Cube, Art.Mat(new Color(0.72f, 0.55f, 0.36f), 0.15f), new Vector3(0f, 0.8f, 0f), new Vector3(0.5f, 0.55f, 0.3f), new Vector3(0f, 0f, 8f));
        }

        static readonly Color DangerRed = new Color(1f, 0.12f, 0.08f);
        static readonly Color AllyBlue = new Color(0.2f, 0.55f, 1f);
        static readonly Color HealColor = new Color(0.2f, 1f, 0.35f);
        static readonly Color LootGold = new Color(1f, 0.8f, 0.15f);

        /// <summary>Code couleur d'un bonus : vert = soin, bleu = renfort, or = équipement à ramasser.</summary>
        static Color PickupColor(ET type) => type == ET.Cafe ? HealColor : type == ET.Renfort ? AllyBlue : LootGold;

        /// <summary>Faisceau lumineux vertical au-dessus d'un bonus (visible de loin).</summary>
        static void Beam(Transform t, Color c)
        {
            Material m = Art.Particle(Art.SoftDot, new Color(c.r, c.g, c.b, 0.42f));
            for (int i = 0; i < 4; i++)
                Art.Part(t, Art.Quad, m, new Vector3(0f, 2.3f, 0f), new Vector3(0.75f, 4.6f, 1f), new Vector3(0f, i * 45f, 0f), false);
        }

        /// <summary>Cercle bleu au sol sous un allié.</summary>
        static void AllyRing(Rig r)
        {
            if (r == null) return;
            Art.Decal(r.root, Art.Particle(Art.SoftDot, new Color(0.2f, 0.55f, 1f, 0.7f)), new Vector3(0f, 0.035f, 0f), new Vector2(1.5f, 1.5f));
        }

        /// <summary>Bonus 3D : modèle d'équipement (Meshy) du camp joué, à la place des formes dorées.</summary>
        static bool PickupModel(Transform t, string name, float size)
        {
            if (!GlbModel.Exists(name)) return false;
            Transform m = GlbModel.Spawn(name, t);
            Bounds b = new Bounds();
            bool first = true;
            foreach (MeshFilter f in m.GetComponentsInChildren<MeshFilter>())
            {
                if (first) { b = f.sharedMesh.bounds; first = false; } else b.Encapsulate(f.sharedMesh.bounds);
            }
            float k = size / Mathf.Max(0.001f, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)));
            m.localScale = Vector3.one * k;
            m.localRotation = Quaternion.Euler(0f, 180f, 0f);
            m.localPosition = new Vector3(0f, 1.05f, 0f) - m.localRotation * (b.center * k);
            return true;
        }

        static void BuildPickup(Transform t, ET type, bool thief)
        {
            Color pcol = PickupColor(type);
            Material ring = Art.Glow(pcol, pcol * 1.4f, 0.5f);
            string f = thief ? "voleur" : "police";
            string model = type == ET.Casque ? (thief ? "casque_voleur_niv3" : "casque_police_niv2")
                         : type == ET.Gilet ? (thief ? "gilet_voleur_niv3" : "gilet_police_niv2")
                         : type == ET.Matraque ? "arme_" + f + "_niv1"
                         : type == ET.Pistolet ? "arme_" + f + "_niv2"
                         : type == ET.Mitraillette ? "arme_" + f + "_niv3"
                         : type == ET.Bouclier ? "bouclier_" + f + "_niv2"
                         : type == ET.Renfort ? (thief ? "casque_voleur_niv1" : "casque_police_niv1")   // renfort : casquette / bandana de la bande
                         : type == ET.Cafe ? "trousse_soin" : null;
            float size = type == ET.Matraque || type == ET.Mitraillette ? 1.0f : type == ET.Bouclier || type == ET.Gilet ? 0.9f : 0.75f;
            if (model != null && PickupModel(t, model, size))
            {
                Art.Part(t, Art.Cylinder, ring, new Vector3(0f, 0.12f, 0f), new Vector3(1f, 0.02f, 1f), default, false);
                return;
            }
            // équipement : doré et brillant (plus de « mallettes noires ») ; le renfort en bleu
            Material item = type == ET.Renfort ? Art.Glow(new Color(0.2f, 0.45f, 1f), new Color(0.1f, 0.3f, 0.9f), 0.7f)
                                               : Art.Glow(new Color(1f, 0.8f, 0.3f), new Color(0.55f, 0.38f, 0.05f), 0.85f);
            Art.Part(t, Art.Cylinder, ring, new Vector3(0f, 0.12f, 0f), new Vector3(1f, 0.02f, 1f), default, false);
            switch (type)
            {
                case ET.Gilet:
                    Art.Part(t, Art.Cube, item, new Vector3(0f, 1.05f, 0f), new Vector3(0.62f, 0.72f, 0.34f));
                    Art.Part(t, Art.Cube, Art.Glow(Color.white, new Color(1.2f, 1.2f, 1.2f)), new Vector3(0f, 0.98f, 0f), new Vector3(0.63f, 0.09f, 0.35f), default, false);
                    Art.Part(t, Art.Cube, item, new Vector3(-0.15f, 0.82f, 0.18f), new Vector3(0.16f, 0.16f, 0.08f));
                    Art.Part(t, Art.Cube, item, new Vector3(0.15f, 0.82f, 0.18f), new Vector3(0.16f, 0.16f, 0.08f));
                    break;
                case ET.Casque:
                    Art.Part(t, Art.Sphere, item, new Vector3(0f, 1.1f, 0f), new Vector3(0.62f, 0.56f, 0.62f));
                    Art.Part(t, Art.Sphere, Art.Mat(new Color(0.55f, 0.75f, 0.92f), 0.95f, 0.2f), new Vector3(0f, 1.02f, 0.2f), new Vector3(0.56f, 0.38f, 0.32f));
                    Art.Part(t, Art.Cube, Art.Glow(Color.white, new Color(1.2f, 1.2f, 1.2f)), new Vector3(0f, 1.36f, 0f), new Vector3(0.09f, 0.08f, 0.56f), default, false);
                    break;
                case ET.Matraque:
                    Art.Part(t, Art.Cylinder, item, new Vector3(0f, 1.05f, 0f), new Vector3(0.1f, 0.48f, 0.1f), new Vector3(0f, 0f, 35f));
                    Art.Part(t, Art.Cylinder, item, new Vector3(-0.12f, 0.92f, 0f), new Vector3(0.08f, 0.12f, 0.08f), new Vector3(0f, 0f, -55f));
                    Art.Part(t, Art.Cylinder, Art.Glow(Color.white, new Color(1.2f, 1.2f, 1.2f)), new Vector3(0.27f, 1.43f, 0f), new Vector3(0.11f, 0.04f, 0.11f), new Vector3(0f, 0f, 35f), false);
                    break;
                case ET.Mitraillette:
                    Art.Part(t, Art.Cube, item, new Vector3(0f, 1.15f, 0f), new Vector3(0.95f, 0.22f, 0.16f));
                    Art.Part(t, Art.Cube, item, new Vector3(0.05f, 0.88f, 0f), new Vector3(0.12f, 0.42f, 0.12f), new Vector3(0f, 0f, 10f));
                    Art.Part(t, Art.Cube, item, new Vector3(-0.32f, 0.95f, 0f), new Vector3(0.13f, 0.3f, 0.13f), new Vector3(0f, 0f, -15f));
                    Art.Part(t, Art.Cylinder, item, new Vector3(0.58f, 1.17f, 0f), new Vector3(0.08f, 0.14f, 0.08f), new Vector3(0f, 0f, 90f));
                    Art.Part(t, Art.Cube, Art.Glow(new Color(1f, 0.55f, 0.1f), new Color(1.6f, 0.7f, 0.1f)), new Vector3(0f, 1.27f, 0f), new Vector3(0.6f, 0.03f, 0.17f), default, false);
                    break;
                case ET.Pistolet:
                    Art.Part(t, Art.Cube, item, new Vector3(0f, 1.15f, 0f), new Vector3(0.6f, 0.2f, 0.16f));
                    Art.Part(t, Art.Cube, item, new Vector3(-0.18f, 0.95f, 0f), new Vector3(0.15f, 0.32f, 0.14f), new Vector3(0f, 0f, -15f));
                    Art.Part(t, Art.Cube, Art.Glow(Color.white, new Color(1.2f, 1.2f, 1.2f)), new Vector3(0.32f, 1.15f, 0f), new Vector3(0.06f, 0.18f, 0.15f), default, false);
                    break;
                case ET.Bouclier:
                    Art.Part(t, Art.Cube, Art.Mat(new Color(0.7f, 0.85f, 0.95f), 0.92f, 0.1f), new Vector3(0f, 1.1f, 0f), new Vector3(0.66f, 0.98f, 0.06f));
                    Art.Part(t, Art.Cube, Art.Glow(Color.white, new Color(1.2f, 1.2f, 1.2f)), new Vector3(0f, 1.38f, 0.04f), new Vector3(0.52f, 0.08f, 0.02f));
                    break;
                case ET.Renfort:
                    Art.Part(t, Art.Cylinder, item, new Vector3(0f, 1f, 0f), new Vector3(0.5f, 0.1f, 0.5f));
                    Art.Part(t, Art.Sphere, item, new Vector3(0f, 1.06f, 0f), new Vector3(0.48f, 0.22f, 0.48f));
                    Art.Part(t, Art.Cube, item, new Vector3(0f, 0.97f, 0.3f), new Vector3(0.38f, 0.04f, 0.24f));
                    Art.Part(t, Art.Cube, Art.Glow(Color.white, new Color(1.2f, 1.2f, 1.2f)), new Vector3(0f, 1.32f, 0f), new Vector3(0.2f, 0.06f, 0.54f), default, false);
                    break;
                case ET.Cafe:
                {
                    // trousse de secours verte à croix blanche (vert = soin)
                    Material green = Art.Glow(new Color(0.2f, 0.75f, 0.3f), new Color(0.15f, 0.6f, 0.2f), 0.5f);
                    Material whiteC = Art.Glow(Color.white, new Color(0.9f, 0.9f, 0.9f), 0.4f);
                    Art.Part(t, Art.Cube, green, new Vector3(0f, 1f, 0f), new Vector3(0.62f, 0.46f, 0.3f));
                    Art.Part(t, Art.Cube, Art.Mat(new Color(0.1f, 0.35f, 0.15f), 0.4f), new Vector3(0f, 1.27f, 0f), new Vector3(0.24f, 0.08f, 0.06f));
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Art.Part(t, Art.Cube, whiteC, new Vector3(0f, 1f, side * 0.153f), new Vector3(0.3f, 0.09f, 0.01f), default, false);
                        Art.Part(t, Art.Cube, whiteC, new Vector3(0f, 1f, side * 0.153f), new Vector3(0.09f, 0.3f, 0.01f), default, false);
                    }
                    break;
                }
            }
        }

        // ================================================================== caméra, ambiance

        float fitHalf = 0.85f, fitMid = 0.8f;
        bool fitInit;

        /// <summary>Hauteur réelle du personnage du menu (boîte englobante de tout ce qui est affiché), lissée.</summary>
        void MenuBounds(float udt)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (Renderer r in P.root.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || !(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                Bounds b = r.bounds;
                if (b.min.y < lo) lo = b.min.y;
                if (b.max.y > hi) hi = b.max.y;
            }
            if (hi <= lo) return;
            float h = (hi - lo) * 0.5f, m = (hi + lo) * 0.5f;
            float k = fitInit ? 1f - Mathf.Exp(-4f * udt) : 1f;
            fitInit = true;
            fitHalf = Mathf.Lerp(fitHalf, h, k);
            fitMid = Mathf.Lerp(fitMid, m, k);
        }

        void UpdateCamera(float udt)
        {
            bool portrait = Screen.height >= Screen.width;
            Vector3 target = new Vector3(px, py, pz);
            Vector3 want, look;
            float wantFov;
            if (st == St.Menu)
            {
                // menu : la caméra regarde vers le bas pour placer le personnage en entier dans le haut de l'écran, au-dessus du titre
                if (portrait)
                {
                    // place le personnage en entier dans l'espace libre au-dessus du titre, quel que soit le format d'écran
                    float canvasH = 1080f * Screen.height / Mathf.Max(1f, Screen.width);
                    float y0 = Mathf.Clamp(2f * menuFreeBottom / canvasH - 1f, -0.2f, 0.75f);  // bas de la zone libre (écran de -1 à 1)
                    float half = (1f - y0) * menuFill;                                    // demi-hauteur occupée par le personnage
                    float t = Mathf.Tan(32f * Mathf.Deg2Rad);
                    float halfH = 0.85f, midY = 0.8f;
                    if (menuFit)
                    {
                        // page Équipement : cadrage sur la taille réelle du personnage (modèle 3D, casque, armes, bouclier)
                        MenuBounds(udt);
                        halfH = fitHalf; midY = fitMid - target.y;
                    }
                    float d = Mathf.Clamp(halfH / (t * half), 3f, 14f);
                    float tilt = (1f + y0) * 0.5f * t;                                 // tangente de l'inclinaison vers le bas
                    Vector3 dir = new Vector3(0.22f, 0f, 1f).normalized;
                    want = target + dir * d + Vector3.up * midY;
                    look = target + Vector3.up * (midY - d * tilt + menuDrop * d * t);
                }
                else
                {
                    want = target + new Vector3(1.6f, 1.2f, 4.6f);
                    look = target + new Vector3(-0.2f, -0.4f, 0f);
                }
                wantFov = portrait ? 64f : 48f;
            }
            else if (st == St.End)
            {
                Vector3 body = P.root.position;
                want = body + new Vector3(3.2f, 3.6f, -3.8f);
                look = body + new Vector3(0f, 0.6f, 0f);
                if (Tuning.Arcade && store != null)
                {
                    // victoire arcade : plan large sur la façade du MEGA STORE pillé
                    Vector3 sp = store.transform.position;
                    want = sp + new Vector3(2.5f, 5.5f, -15f);
                    look = sp + new Vector3(0f, 5f, 0f);
                }
                wantFov = portrait ? 62f : 50f;
            }
            else
            {
                want = new Vector3(px * 0.55f, portrait ? 4.9f : 4.1f, pz - (portrait ? 7.6f : 6.8f));
                if (Tuning.Arcade)
                {
                    // grosse bande : la caméra prend de la hauteur pour voir la foule et la route devant
                    float ck = Mathf.Clamp01(squad.Count / (float)ArcadeMaxSquad);
                    want += new Vector3(0f, 2.6f + 2.2f * ck, -0.4f - 1f * ck);
                }
                look = Tuning.Arcade ? new Vector3(px * 0.4f, 0.4f, pz + 6.5f) : new Vector3(px * 0.4f, 1.25f, pz + 9f);
                wantFov = (portrait ? 66f : 54f) + Mathf.InverseLerp(12f, 26f, speed) * 7f;
                // pendant un ralenti : la caméra se rapproche et resserre le cadre
                float zk = slowMo > 0f ? zoom : 0f;
                want += new Vector3(0f, -1.3f, 2.6f) * zk;
                look += new Vector3(0f, 0.2f, -2f) * zk;
                wantFov -= 9f * zk;
            }
            if (!camInit) { camPos = want; camInit = true; }
            float k = st == St.Play ? 1f - Mathf.Exp(-8f * udt) : 1f - Mathf.Exp(-3f * udt);
            camPos.x = Mathf.Lerp(camPos.x, want.x, k);
            camPos.y = Mathf.Lerp(camPos.y, want.y, k);
            camPos.z = st == St.Play ? Mathf.Lerp(camPos.z, want.z, 1f - Mathf.Exp(-20f * udt)) : Mathf.Lerp(camPos.z, want.z, k);
            fov = Mathf.Lerp(fov, wantFov, 1f - Mathf.Exp(-3f * udt));
            fovKick = Mathf.MoveTowards(fovKick, 0f, udt * 22f);
            if (slowMo <= 0f) zoom = Mathf.MoveTowards(zoom, 0f, udt * 2f);
            cam.fieldOfView = fov + fovKick;

            shake = Mathf.MoveTowards(shake, 0f, udt * 1.4f);
            float tt = Time.unscaledTime * 28f;
            Vector3 jitter = new Vector3(Mathf.PerlinNoise(tt, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, tt) - 0.5f, 0f) * shake * 0.9f;
            cam.transform.position = camPos + jitter;
            cam.transform.LookAt(look + jitter * 0.5f);
            // léger roulis de caméra dans les changements de couloir
            float wantRoll = st == St.Play ? ((lane - 1) * Lane - px) * -1.4f : 0f;
            camRoll = Mathf.Lerp(camRoll, wantRoll, 1f - Mathf.Exp(-10f * udt));
            cam.transform.Rotate(0f, 0f, camRoll, Space.Self);
        }

        void UpdateAmbience(float dt)
        {
            Vector3 cp = cam.transform.position;
            snow.transform.position = new Vector3(0f, cp.y + 9f, pz + 26f);
            skyGlow.position = new Vector3(0f, 14f, pz + 150f);
            playerLight.transform.position = new Vector3(px, 3.4f, pz + 2.5f);

            // traits de vitesse quand on va vite (et pendant un bouclier)
            var em = speedLines.emission;
            float rate = st == St.Play ? Mathf.InverseLerp(15f, 26f, speed) * 60f : 0f;
            if (slowMo > 0f || hitStop > 0f) rate = 0f;
            em.rateOverTime = rate;

            if (drone != null)
            {
                float t = Time.time;
                Vector3 dp = new Vector3(px * 0.6f + Mathf.Sin(t * 0.7f) * 2f, 5.6f + Mathf.Sin(t * 2.3f) * 0.25f, pz + 9.5f);
                drone.localPosition = Vector3.Lerp(drone.localPosition, dp, 1f - Mathf.Exp(-4f * dt));
                drone.LookAt(new Vector3(px, 1.2f, pz));
                foreach (Transform r in rotors) r.Rotate(0f, 2000f * dt, 0f, Space.Self);
                bool led = st == St.Play && rec && Mathf.Repeat(Time.unscaledTime * 2f, 1f) < 0.5f;
                droneLed.enabled = led;
                droneLight.enabled = led;
            }
        }
    }
}
