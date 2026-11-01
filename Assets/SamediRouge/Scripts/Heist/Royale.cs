using System.Collections.Generic;
using UnityEngine;

namespace SamediRouge
{
    /// <summary>
    /// V3 « Battle royale » (façon PUBG, vue de dessus, parties de 2-3 minutes) : 15 joueurs sautent en parachute
    /// sur la ville, ramassent armes, gilets, casques et trousses de soin, se tirent dessus (visée automatique),
    /// et la zone se resserre jusqu'au dernier survivant.
    /// </summary>
    public class Royale : MonoBehaviour
    {
        static string L(string en, string fr) => Loc.T(en, fr);

        // ------------------------------------------------------------------ armes et objets

        public struct Weapon
        {
            public string name, model;
            public float dmg, range, rate, spread;
            public int pellets;
            public Color color;
        }

        public static readonly Weapon[] Weapons =
        {
            new Weapon { name = "Poings",        dmg = 9f,  range = 1.5f, rate = 0.45f, spread = 0f,    pellets = 1, color = new Color(0.8f, 0.8f, 0.8f) },
            new Weapon { name = "Pistolet",      dmg = 14f, range = 9f,   rate = 0.42f, spread = 0.12f, pellets = 1, color = new Color(0.85f, 0.85f, 0.85f), model = "arme_voleur_niv2" },
            new Weapon { name = "Mitraillette",  dmg = 8f,  range = 9.5f, rate = 0.11f, spread = 0.22f, pellets = 1, color = new Color(0.3f, 0.65f, 1f),    model = "arme_voleur_niv3" },
            new Weapon { name = "Fusil lourd",   dmg = 30f, range = 13f,  rate = 0.7f,  spread = 0.06f, pellets = 1, color = new Color(0.75f, 0.35f, 1f),   model = "arme_police_niv3" },
        };

        enum Item { Weapon1, Weapon2, Weapon3, Vest, Helmet, Medkit }

        class Loot
        {
            public Item item;
            public Transform tr;
            public Vector3 pos;
            public bool taken;
        }

        class Fighter
        {
            public string name;
            public bool player, alive = true;
            public Rig rig;
            public Vector3 pos, vel, goal;
            public float yaw, hp = 100f, armor, cd, think, strafe, hitFlash, stuckT;
            public bool helmet;
            public int weapon, kills;
            public float dropY;            // hauteur pendant la descente en parachute
            public Transform chute;
            public Fighter target;
            public float vanish = -1f, vanishS;
            public Vector3 lastPos;
        }

        class Bolt { public Transform tr; public Vector3 from, to; public float t, dur; public Fighter shooter, victim; public float dmg; }

        const int Players = 15;
        const float Speed = 6.2f, Radius = 0.45f, CharScale = 1.3f, DropH = 26f, DropSpeed = 6.5f;

        enum St { Title, Drop, Play, End, Result }

        static readonly string[] BotNames =
        {
            "Kévin_du_93", "LaFouine", "Pépito", "xX_Ninja_Xx", "TontonFlingueur", "Mamie_Turbo", "Brioche", "Le_Gaulois",
            "Chouquette", "RoiDuBlackFriday", "Zizou_77", "Baguette_Sniper", "MrPropre", "Ratatouille", "Kiki", "Gros_Nounours",
            "SoldeMan", "Caddie_Fou", "Promo-50%", "La_Teigne"
        };

        // ------------------------------------------------------------------ état

        Camera cam;
        Transform world;
        Hud hud;
        HeistCity city;
        St st = St.Title;
        float stT;
        Fighter me;
        readonly List<Fighter> fighters = new List<Fighter>();
        readonly List<Loot> loot = new List<Loot>();
        readonly List<Bolt> bolts = new List<Bolt>();
        readonly Stack<Transform> boltPool = new Stack<Transform>();
        float camShake;
        int alive;
        int place;

        // zone
        Vector3 zoneC, zoneFromC, zoneToC;
        float zoneR, zoneFromR, zoneToR, zoneT, zoneWait, zoneShrink;
        int zonePhase;
        bool zoneMoving;
        Transform zoneWall, zoneNext, arrow;
        Mesh wallMesh;

        // joystick
        bool stickOn;
        Vector2 stickStart, stickNow;
        int stickId = -1;

        const string KCash = "sr_royale_cash", KWins = "sr_royale_wins";

        // ------------------------------------------------------------------ démarrage

        void Awake()
        {
            world = new GameObject("World").transform;
            SetupCamera();
            SetupLighting();
            PostFx.Setup(cam);
            Fx.Init(world);
            gameObject.AddComponent<Snd>();
            Hud.Quiet = false;
            hud = new GameObject("HUD", typeof(RectTransform)).AddComponent<Hud>();
            hud.Init(cam);
            hud.InitRoyale();
            hud.onRoyaleNext = () => { if (st == St.Result) { BuildMatch(); StartDrop(); } };
#if UNITY_EDITOR
            DevStart();
#endif
            BuildMatch();
            if (devSkipTitle || !hud.ShowTitle(() => { if (st == St.Title) StartDrop(); })) StartDrop();
        }

        bool devSkipTitle, devBot;
#if UNITY_EDITOR
        void DevStart()
        {
            string cmd = "";
            try
            {
                string path = System.IO.Path.Combine(Application.dataPath, "../Logs/sr-screen.txt");
                if (System.IO.File.Exists(path)) cmd = System.IO.File.ReadAllText(path);
            }
            catch (System.Exception) { }
            devSkipTitle = cmd.Contains("play");
            devBot = cmd.Contains("bot");
            Perf.On = cmd.Contains("perf");
        }
#endif

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
            cam.backgroundColor = new Color(0.55f, 0.62f, 0.7f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 140f;
            cam.fieldOfView = 46f;
            cam.allowHDR = true;
        }

        void SetupLighting()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(58f, -35f, 0f);
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.6f;
            RenderSettings.sun = sun;
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.transform.rotation = Quaternion.Euler(35f, 140f, 0f);
            fill.color = new Color(0.85f, 0.9f, 1f);
            fill.intensity = 0.35f;
            fill.shadows = LightShadows.None;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.8f, 0.83f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.62f, 0.62f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.4f, 0.38f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = cam.backgroundColor;
            RenderSettings.fogStartDistance = 50f;
            RenderSettings.fogEndDistance = 130f;
        }

        // ------------------------------------------------------------------ partie

        void ClearMatch()
        {
            foreach (Fighter f in fighters) if (f.rig != null) Destroy(f.rig.root.gameObject);
            foreach (Loot l in loot) if (l.tr != null) Destroy(l.tr.gameObject);
            foreach (Bolt b in bolts) if (b.tr != null) Destroy(b.tr.gameObject);
            fighters.Clear(); loot.Clear(); bolts.Clear(); boltPool.Clear();
            if (city != null) Destroy(city.root.gameObject);
            if (zoneWall != null) Destroy(zoneWall.gameObject);
            if (zoneNext != null) Destroy(zoneNext.gameObject);
            if (arrow != null) Destroy(arrow.gameObject);
        }

        void BuildMatch()
        {
            ClearMatch();
            int seed = Random.Range(0, 100000);
            city = new HeistCity(world, seed);

            // butin éparpillé : surtout des pistolets et des soins, quelques armes rares
            int n = 0;
            foreach (Vector3 p in city.lootSpots)
            {
                if (n++ >= 46) break;
                float r = Random.value;
                Item it = r < 0.3f ? Item.Weapon1 : r < 0.45f ? Item.Weapon2 : r < 0.52f ? Item.Weapon3 : r < 0.68f ? Item.Vest : r < 0.8f ? Item.Helmet : Item.Medkit;
                SpawnLoot(it, p);
            }

            // les combattants : toi + 14 adversaires (pillards et policiers)
            var names = new List<string>(BotNames);
            for (int i = 0; i < Players; i++)
            {
                var f = new Fighter { player = i == 0 };
                if (f.player) f.name = L("You", "Toi");
                else { int k = Random.Range(0, names.Count); f.name = names[k]; names.RemoveAt(k); }
                // points de chute répartis sur la carte
                float a = i * 2.39996f, rr = HeistCity.Size * 0.42f * Mathf.Sqrt((i + 0.5f) / Players);
                Vector3 p = new Vector3(HeistCity.Size * 0.5f + Mathf.Cos(a) * rr, 0f, HeistCity.Size * 0.5f + Mathf.Sin(a) * rr);
                f.pos = city.PushOut(p, Radius);
                f.goal = f.pos;
                f.dropY = DropH + Random.Range(-3f, 4f);
                f.rig = f.player ? Rig.Build(Kind.Thief, world, f.pos, 0f, -1)
                      : i % 2 == 0 ? Rig.Build(Kind.Guard, world, f.pos, 0f, 0)
                      : Rig.Build(Kind.Thief, world, f.pos, 0f, 1);
                f.rig.root.localScale *= CharScale;
                f.rig.SetGear(new bool[6]);
                f.chute = Parachute(f.rig.root, f.player ? new Color(1f, 0.3f, 0.25f) : Color.HSVToRGB(Random.value, 0.6f, 0.95f));
                f.yaw = Random.Range(0f, 360f);
                f.think = Random.Range(0f, 0.5f);
                fighters.Add(f);
                if (f.player) me = f;
            }
            alive = Players;

            // zone : couvre toute la ville au départ, puis 5 resserrements
            zoneC = zoneFromC = zoneToC = new Vector3(HeistCity.Size * 0.5f, 0f, HeistCity.Size * 0.5f);
            zoneR = zoneFromR = zoneToR = HeistCity.Size * 0.78f;
            zonePhase = 0;
            zoneMoving = false;
            zoneWait = 22f;
            zoneT = 0f;
            BuildZoneVisuals();
            arrow = MakeArrow();
            st = St.Title;
            hud.RoyaleHide();
            PlaceCamera(true);
            Perf.Note("partie : " + Players + " joueurs, " + loot.Count + " objets");
        }

        void StartDrop()
        {
            st = St.Drop;
            stT = 0f;
            hud.RoyaleShow();
            hud.Toast(L("JUMP!", "SAUTE !"));
            Snd.Play(Snd.I.whoosh, 0.8f);
        }

        Transform Parachute(Transform parent, Color c)
        {
            Transform t = Art.Node(parent, "chute", new Vector3(0f, 3.1f, 0f));
            Material cloth = Art.Mat(c, 0.3f);
            Material white = Art.Mat(new Color(0.95f, 0.95f, 0.95f), 0.3f);
            Art.Part(t, Art.Sphere, cloth, Vector3.zero, new Vector3(3.2f, 1.1f, 2.4f), default, false);
            Art.Part(t, Art.Sphere, white, new Vector3(0f, 0.05f, 0f), new Vector3(1.2f, 1.12f, 2.42f), default, false);
            Material line = Art.Mat(new Color(0.2f, 0.2f, 0.2f), 0.2f);
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -1.2f : 1.2f, z = i < 2 ? -0.7f : 0.7f;
                Vector3 mid = new Vector3(x * 0.5f, -1.2f, z * 0.5f);
                Art.Part(t, Art.Cylinder, line, mid, new Vector3(0.03f, 1.3f, 0.03f), new Vector3(z * 20f, 0f, -x * 22f), false);
            }
            t.gameObject.SetActive(false);
            return t;
        }

        Transform MakeArrow()
        {
            Transform a = Art.Node(world, "arrow", Vector3.zero);
            Material m = Art.Glow(new Color(0.4f, 0.75f, 1f), new Color(0.5f, 0.9f, 1.4f), 0.5f);
            Art.Part(a, Art.Cube, m, new Vector3(0f, 0f, 0.1f), new Vector3(0.28f, 0.06f, 0.9f), default, false);
            Art.Part(a, Art.Cube, m, new Vector3(-0.22f, 0f, 0.62f), new Vector3(0.22f, 0.06f, 0.62f), new Vector3(0f, 40f, 0f), false);
            Art.Part(a, Art.Cube, m, new Vector3(0.22f, 0f, 0.62f), new Vector3(0.22f, 0.06f, 0.62f), new Vector3(0f, -40f, 0f), false);
            a.gameObject.SetActive(false);
            return a;
        }

        // ------------------------------------------------------------------ butin

        void SpawnLoot(Item it, Vector3 p)
        {
            var l = new Loot { item = it, pos = new Vector3(p.x, 0f, p.z) };
            l.tr = Art.Node(world, "loot", l.pos);
            Color c = ItemColor(it);
            Art.Decal(l.tr, Art.Particle(HeistCity.RingTex, new Color(c.r, c.g, c.b, 0.85f)), new Vector3(0f, 0.17f, 0f), new Vector2(1.5f, 1.5f));
            Transform spin = Art.Node(l.tr, "spin", new Vector3(0f, 0.9f, 0f));
            string model = it == Item.Weapon1 ? Weapons[1].model : it == Item.Weapon2 ? Weapons[2].model : it == Item.Weapon3 ? Weapons[3].model
                         : it == Item.Vest ? "gilet_voleur_niv2" : it == Item.Helmet ? "casque_voleur_niv3" : "trousse_soin";
            if (Fit(spin, model, it == Item.Weapon3 ? 1.1f : 0.85f) == null)
                Art.Part(spin, Art.Cube, Art.Glow(c, c * 1.4f, 0.5f), Vector3.zero, Vector3.one * 0.5f, default, false);
            loot.Add(l);
        }

        static Color ItemColor(Item it)
        {
            switch (it)
            {
                case Item.Weapon1: return Weapons[1].color;
                case Item.Weapon2: return Weapons[2].color;
                case Item.Weapon3: return Weapons[3].color;
                case Item.Vest: return new Color(0.35f, 0.6f, 1f);
                case Item.Helmet: return new Color(1f, 0.8f, 0.25f);
                default: return new Color(0.4f, 1f, 0.45f);
            }
        }

        static Transform Fit(Transform parent, string name, float size)
        {
            if (string.IsNullOrEmpty(name) || !GlbModel.Exists(name)) return null;
            Transform m = GlbModel.Spawn(name, parent);
            if (m == null) return null;
            m.localPosition = Vector3.zero;
            m.localRotation = Quaternion.identity;
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

        /// <summary>Vrai si l'objet améliore l'équipement de ce combattant (sinon on passe dessus sans le prendre).</summary>
        static bool Wants(Fighter f, Item it)
        {
            switch (it)
            {
                case Item.Weapon1: return f.weapon < 1;
                case Item.Weapon2: return f.weapon < 2;
                case Item.Weapon3: return f.weapon < 3;
                case Item.Vest: return f.armor < 30f;
                case Item.Helmet: return !f.helmet;
                default: return f.hp < 80f;
            }
        }

        void Take(Fighter f, Loot l)
        {
            l.taken = true;
            Destroy(l.tr.gameObject);
            string what = "";
            switch (l.item)
            {
                case Item.Weapon1: f.weapon = 1; what = Weapons[1].name; break;
                case Item.Weapon2: f.weapon = 2; what = Weapons[2].name; break;
                case Item.Weapon3: f.weapon = 3; what = Weapons[3].name; break;
                case Item.Vest: f.armor = 50f; what = L("Vest", "Gilet"); break;
                case Item.Helmet: f.helmet = true; what = L("Helmet", "Casque"); break;
                default: f.hp = Mathf.Min(100f, f.hp + 50f); what = L("+50 HP", "+50 PV"); break;
            }
            Gear(f);
            if (f.player)
            {
                hud.Float(f.pos + Vector3.up * 2.6f, what, ItemColor(l.item), true);
                Snd.Play(Snd.I.pickup, 0.8f);
                Fx.Pickup(f.pos + Vector3.up);
                hud.RoyalePop();
            }
        }

        static void Gear(Fighter f)
        {
            var g = new bool[6];
            g[0] = f.helmet;
            g[1] = f.armor > 0f;
            g[4] = f.weapon == 1;
            g[5] = f.weapon >= 2;
            f.rig.SetGear(g);
        }

        // ------------------------------------------------------------------ zone

        static readonly float[] ZoneRadius = { 0.78f, 0.5f, 0.34f, 0.22f, 0.12f, 0.035f };
        static readonly float[] ZoneWaitT = { 22f, 16f, 13f, 11f, 9f };
        static readonly float[] ZoneShrinkT = { 14f, 12f, 10f, 9f, 8f };
        static readonly float[] ZoneDps = { 3f, 5f, 8f, 12f, 18f, 25f };

        void BuildZoneVisuals()
        {
            if (wallMesh == null) wallMesh = CylinderWall(64);
            zoneWall = new GameObject("zone").transform;
            zoneWall.SetParent(world, false);
            zoneWall.gameObject.AddComponent<MeshFilter>().sharedMesh = wallMesh;
            var mr = zoneWall.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Art.Particle(null, new Color(0.25f, 0.5f, 1f, 0.28f));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            zoneNext = Art.Decal(world, Art.Particle(RingThin(), new Color(1f, 1f, 1f, 0.9f)), Vector3.up * 0.2f, Vector2.one).transform;
            zoneNext.gameObject.SetActive(false);
            PlaceZone();
        }

        static Texture2D ringThin;
        static Texture2D RingThin()
        {
            if (ringThin != null) return ringThin;
            const int n = 512;
            ringThin = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "royale_ring", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.985f) / 0.012f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            ringThin.SetPixels32(px);
            ringThin.Apply(true, true);
            return ringThin;
        }

        /// <summary>Paroi cylindrique ouverte, visible des deux côtés (rayon 1, hauteur 1, base à 0).</summary>
        static Mesh CylinderWall(int seg)
        {
            var m = new Mesh { name = "zone_wall" };
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var tris = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                Vector3 p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v.Add(p); v.Add(p + Vector3.up);
                uv.Add(new Vector2(i / (float)seg, 0f)); uv.Add(new Vector2(i / (float)seg, 1f));
            }
            for (int i = 0; i < seg; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(c); tris.Add(b); tris.Add(d);
                tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(c); tris.Add(d); tris.Add(b);
            }
            m.vertices = v.ToArray();
            m.uv = uv.ToArray();
            m.triangles = tris.ToArray();
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        void PlaceZone()
        {
            zoneWall.localPosition = zoneC;
            zoneWall.localScale = new Vector3(zoneR, 14f, zoneR);
            if (zoneNext.gameObject.activeSelf)
            {
                zoneNext.localPosition = zoneToC + Vector3.up * 0.2f;
                zoneNext.localScale = new Vector3(zoneToR * 2f, zoneToR * 2f, 1f);
            }
        }

        void TickZone(float dt)
        {
            if (zonePhase >= ZoneWaitT.Length) return;
            zoneT += dt;
            if (!zoneMoving)
            {
                if (!zoneNext.gameObject.activeSelf)
                {
                    // annonce de la prochaine zone (cercle blanc)
                    zoneFromC = zoneC;
                    zoneFromR = zoneR;
                    zoneToR = HeistCity.Size * ZoneRadius[zonePhase + 1];
                    Vector2 off = Random.insideUnitCircle * Mathf.Max(0f, zoneR - zoneToR) * 0.8f;
                    zoneToC = zoneC + new Vector3(off.x, 0f, off.y);
                    zoneToC.x = Mathf.Clamp(zoneToC.x, zoneToR * 0.3f, HeistCity.Size - zoneToR * 0.3f);
                    zoneToC.z = Mathf.Clamp(zoneToC.z, zoneToR * 0.3f, HeistCity.Size - zoneToR * 0.3f);
                    zoneNext.gameObject.SetActive(true);
                }
                if (zoneT >= ZoneWaitT[zonePhase])
                {
                    zoneMoving = true;
                    zoneT = 0f;
                    if (me.alive) hud.Toast(L("THE ZONE IS CLOSING!", "LA ZONE SE RESSERRE !"));
                    Snd.PlayPitch(Snd.I.siren, 0.4f, 0.8f);
                }
            }
            else
            {
                float k = Mathf.Clamp01(zoneT / ZoneShrinkT[zonePhase]);
                zoneC = Vector3.Lerp(zoneFromC, zoneToC, k);
                zoneR = Mathf.Lerp(zoneFromR, zoneToR, k);
                if (k >= 1f)
                {
                    zoneMoving = false;
                    zoneT = 0f;
                    zonePhase++;
                    zoneNext.gameObject.SetActive(false);
                }
            }
            PlaceZone();
        }

        float ZoneTimer => zonePhase >= ZoneWaitT.Length ? -1f : zoneMoving ? -1f : Mathf.Max(0f, ZoneWaitT[zonePhase] - zoneT);
        bool Outside(Vector3 p) => (Flat(p) - Flat(zoneC)).magnitude > zoneR;

        // ------------------------------------------------------------------ boucle

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            Perf.Frame();
            stT += dt;
            ReadInput();
            hud.Tick(dt);

            if (st == St.Drop) DropTick(dt);
            if (st == St.Play || st == St.End) PlayTick(dt);
            TickBolts(dt);
            TickLoot(dt);
            foreach (Fighter f in fighters) Pose(f, dt);
            PlaceCamera(false);
            if (st == St.Drop || st == St.Play)
                hud.RoyaleTick(me.hp, me.armor, me.helmet, me.weapon, alive, me.kills, ZoneTimer, zoneMoving, me.alive && Outside(me.pos), stickOn, stickStart, stickNow);
            if (st == St.End)
            {
                endT += dt;
                if (endT > 2f) ShowResult();
            }
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        void PlaceCamera(bool snap)
        {
            Fighter focus = me;
            if (me != null && !me.alive && me.target != null && me.target.alive) focus = me.target;   // spectateur : celui qui t'a éliminé
            Vector3 p = focus != null ? focus.pos + Vector3.up * (focus.dropY > 0f ? focus.dropY : 0f) : new Vector3(HeistCity.Size * 0.5f, 0f, HeistCity.Size * 0.5f);
            float h = st == St.Drop ? 28f : 20f, back = st == St.Drop ? 18f : 15f;
            Vector3 want = new Vector3(p.x, (focus != null ? focus.dropY * 0.6f : 0f), p.z + 2.2f) + new Vector3(0f, h, -back);
            float k = snap ? 1f : 1f - Mathf.Exp(-5f * Time.deltaTime);
            cam.transform.position = Vector3.Lerp(cam.transform.position, want, k);
            cam.transform.rotation = Quaternion.Euler(53f, 0f, 0f);
            if (camShake > 0f) cam.transform.position += Random.insideUnitSphere * camShake * 0.25f;
            camShake = Mathf.MoveTowards(camShake, 0f, Time.deltaTime * 2.5f);
        }

        // ------------------------------------------------------------------ entrées

        Vector2 MoveInput()
        {
            Vector2 k = new Vector2((Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f),
                                    (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.Z) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f));
            if (k.sqrMagnitude > 0.01f) return k.normalized;
            if (!stickOn) return Vector2.zero;
            float r = Screen.height * 0.06f;
            Vector2 d = (stickNow - stickStart) / r;
            if (d.magnitude < 0.12f) return Vector2.zero;
            return Vector2.ClampMagnitude(d, 1f);
        }

        void ReadInput()
        {
            bool down = false, held = false, up = false;
            Vector2 pos = Vector2.zero;
            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch t = Input.GetTouch(i);
                    if (stickId < 0 && t.phase == TouchPhase.Began) { stickId = t.fingerId; down = true; pos = t.position; break; }
                    if (t.fingerId != stickId) continue;
                    pos = t.position;
                    if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) up = true; else held = true;
                }
            }
            else
            {
                pos = Input.mousePosition;
                if (Input.GetMouseButtonDown(0)) down = true;
                else if (Input.GetMouseButton(0)) held = true;
                else if (Input.GetMouseButtonUp(0)) up = true;
            }
            if (down)
            {
                if (hud.Click(pos)) { stickId = -1; return; }
                if (hud.TitleOpen) return;
                stickOn = st == St.Drop || st == St.Play;
                stickStart = stickNow = pos;
                if (Input.touchCount == 0) stickId = 0;
            }
            else if (held && stickOn) stickNow = pos;
            if (up) { stickOn = false; stickId = -1; }
        }

        // ------------------------------------------------------------------ descente en parachute

        void DropTick(float dt)
        {
            bool allDown = true;
            foreach (Fighter f in fighters)
            {
                if (f.dropY <= 0f) continue;
                f.chute.gameObject.SetActive(true);
                f.dropY = Mathf.Max(0f, f.dropY - DropSpeed * dt);
                Vector3 v = Vector3.zero;
                if (f.player)
                {
                    Vector2 mv = devBot ? Vector2.zero : MoveInput();
                    v = new Vector3(mv.x, 0f, mv.y) * 7f;
                }
                else v = (Flat(f.goal) - Flat(f.pos)).normalized * 2f;
                f.pos += v * dt;
                f.pos.x = Mathf.Clamp(f.pos.x, 2f, HeistCity.Size - 2f);
                f.pos.z = Mathf.Clamp(f.pos.z, 2f, HeistCity.Size - 2f);
                if (v.sqrMagnitude > 0.1f) f.yaw = Mathf.MoveTowardsAngle(f.yaw, Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg, 300f * dt);
                f.rig.anim = Anim.Jump;
                if (f.dropY <= 0f)
                {
                    f.pos = city.PushOut(f.pos, Radius);
                    f.chute.gameObject.SetActive(false);
                    f.rig.Land(0.5f);
                    Fx.Dust(f.pos);
                    if (f.player) { Snd.Play(Snd.I.land, 0.7f); camShake = 0.3f; }
                }
                else allDown = false;
            }
            if (me.dropY <= 0f && st == St.Drop) { st = St.Play; hud.Toast(L("LOOT UP!", "ÉQUIPE-TOI !")); }
        }

        // ------------------------------------------------------------------ jeu

        float endT;

        void PlayTick(float dt)
        {
            Perf.Mark("zone");
            if (st == St.Play) TickZone(dt);
            if (st == St.Play) DropTick(dt);   // les adversaires encore en l'air finissent leur descente
            Perf.Mark("fighters");
            foreach (Fighter f in fighters)
            {
                if (!f.alive || f.dropY > 0f) continue;
                if (f.player) PlayerTick(f, dt);
                else BotTick(f, dt);
                // dégâts de zone
                if (Outside(f.pos))
                {
                    Damage(f, ZoneDps[Mathf.Min(zonePhase, ZoneDps.Length - 1)] * dt, null, false);
                    if (!f.alive) continue;
                }
                // ramassage automatique
                foreach (Loot l in loot)
                {
                    if (l.taken) continue;
                    if ((Flat(l.pos) - Flat(f.pos)).sqrMagnitude < 1.3f && Wants(f, l.item)) { Take(f, l); break; }
                }
            }
            loot.RemoveAll(l => l.taken);
            for (int i = 0; i < fighters.Count; i++)
                for (int j = i + 1; j < fighters.Count; j++)
                {
                    Fighter a = fighters[i], b = fighters[j];
                    if (!a.alive || !b.alive || a.dropY > 0f || b.dropY > 0f) continue;
                    Vector3 d = Flat(a.pos) - Flat(b.pos);
                    float m = d.magnitude;
                    if (m < 0.9f && m > 1e-4f) { a.pos += d / m * (0.9f - m) * 0.5f; b.pos -= d / m * (0.9f - m) * 0.5f; }
                }
            TickArrow();
        }

        void PlayerTick(Fighter f, float dt)
        {
            Vector2 mv = devBot ? BotMove(f) : MoveInput();
            Vector3 want = new Vector3(mv.x, 0f, mv.y) * Speed;
            Move(f, want, dt);
            Fighter t = Nearest(f, Weapons[f.weapon].range + (f.weapon == 0 ? 0f : 0.5f));
            f.cd -= dt;
            if (t != null)
            {
                f.yaw = Mathf.Atan2(t.pos.x - f.pos.x, t.pos.z - f.pos.z) * Mathf.Rad2Deg;
                if (f.cd <= 0f) Fire(f, t);
            }
            else if (f.vel.sqrMagnitude > 0.3f) f.yaw = Mathf.MoveTowardsAngle(f.yaw, Mathf.Atan2(f.vel.x, f.vel.z) * Mathf.Rad2Deg, 900f * dt);
        }

        /// <summary>Outil de dev : le héros joue tout seul comme un bot.</summary>
        Vector2 BotMove(Fighter f)
        {
            Vector3 d = Flat(Route(f.pos, BotGoal(f))) - Flat(f.pos);
            if (d.magnitude < 0.5f) return Vector2.zero;
            d.Normalize();
            return new Vector2(d.x, d.z);
        }

        void Move(Fighter f, Vector3 want, float dt)
        {
            f.vel = Vector3.Lerp(f.vel, want, 1f - Mathf.Exp(-12f * dt));
            Vector3 before = f.pos;
            f.pos += f.vel * dt;
            f.pos = city.PushOut(f.pos, Radius);
            f.pos.x = Mathf.Clamp(f.pos.x, -4f, HeistCity.Size + 4f);
            f.pos.z = Mathf.Clamp(f.pos.z, -4f, HeistCity.Size + 4f);
            if ((f.pos - before).sqrMagnitude < (f.vel * dt).sqrMagnitude * 0.2f && f.vel.sqrMagnitude > 1f) f.stuckT += dt; else f.stuckT = 0f;
            if (f.vel.sqrMagnitude > 0.3f && f.target == null) f.yaw = Mathf.MoveTowardsAngle(f.yaw, Mathf.Atan2(f.vel.x, f.vel.z) * Mathf.Rad2Deg, 720f * dt);
        }

        Fighter Nearest(Fighter from, float range)
        {
            Fighter best = null;
            float bd = range * range;
            foreach (Fighter o in fighters)
            {
                if (o == from || !o.alive || o.dropY > 0f) continue;
                float d = (Flat(o.pos) - Flat(from.pos)).sqrMagnitude;
                if (d < bd && city.Clear(from.pos + Vector3.up, o.pos + Vector3.up)) { bd = d; best = o; }
            }
            return best;
        }

        // ------------------------------------------------------------------ IA des adversaires

        Vector3 BotGoal(Fighter f)
        {
            // 1) hors zone ou zone qui va se fermer : rentrer
            bool soon = zoneMoving || ZoneTimer < 6f;
            Vector3 c = zoneNext.gameObject.activeSelf ? zoneToC : zoneC;
            float r = zoneNext.gameObject.activeSelf ? zoneToR : zoneR;
            if ((Flat(f.pos) - Flat(c)).magnitude > r * 0.85f && (soon || Outside(f.pos))) return c;
            // 2) un objet utile pas trop loin
            Loot bestL = null;
            float bd = 26f * 26f;
            foreach (Loot l in loot)
            {
                if (l.taken || !Wants(f, l.item) || Outside(l.pos)) continue;
                float d = (Flat(l.pos) - Flat(f.pos)).sqrMagnitude;
                if (d < bd) { bd = d; bestL = l; }
            }
            if (bestL != null) return bestL.pos;
            // 3) chasser : vers le combattant le plus proche
            Fighter prey = null;
            float pd = 1e9f;
            foreach (Fighter o in fighters)
            {
                if (o == f || !o.alive || o.dropY > 0f) continue;
                float d = (Flat(o.pos) - Flat(f.pos)).sqrMagnitude;
                if (d < pd) { pd = d; prey = o; }
            }
            // au début les adversaires s'équipent plutôt que de chasser ; plus la zone est petite, plus ils traquent
            float hunt = 14f + 8f * zonePhase;
            if (prey != null && f.weapon > 0 && pd < hunt * hunt) return prey.pos;
            if ((Flat(f.goal) - Flat(f.pos)).magnitude > 1.5f && !Outside(f.goal)) return f.goal;
            Vector3 w = city.walkSpots[Random.Range(0, city.walkSpots.Count)];
            return Outside(w) ? c : w;
        }

        /// <summary>Contourne les immeubles : si la ligne droite est bouchée, passe par le carrefour qui raccourcit le trajet.</summary>
        Vector3 Route(Vector3 from, Vector3 to)
        {
            if (city.Clear(from, to)) return to;
            Vector3 best = to;
            float bc = 1e9f;
            for (int i = 0; i <= HeistCity.N; i++)
                for (int j = 0; j <= HeistCity.N; j++)
                {
                    Vector3 n = HeistCity.Node(i, j);
                    if ((Flat(n) - Flat(from)).magnitude < 1.2f || !city.Clear(from, n)) continue;
                    float c = (n - from).magnitude + (to - n).magnitude * (city.Clear(n, to) ? 1f : 1.6f);
                    if (c < bc) { bc = c; best = n; }
                }
            return best;
        }

        void BotTick(Fighter f, float dt)
        {
            f.think -= dt;
            if (f.think <= 0f)
            {
                f.think = Random.Range(0.35f, 0.6f);
                f.goal = BotGoal(f);
                f.target = Nearest(f, Weapons[f.weapon].range + 3f);
                f.strafe = Random.value < 0.5f ? -1f : 1f;
            }
            Vector3 want;
            Weapon w = Weapons[f.weapon];
            if (f.target != null && f.target.alive && !Outside(f.pos))
            {
                // combat : garder sa distance de tir en tournant autour
                Vector3 to = Flat(f.target.pos) - Flat(f.pos);
                float d = to.magnitude;
                Vector3 n = to / Mathf.Max(0.01f, d);
                Vector3 side = new Vector3(n.z, 0f, -n.x) * f.strafe;
                float keep = Mathf.Max(1.0f, w.range * 0.7f);
                want = (n * Mathf.Clamp(d - keep, -1f, 1f) + side * 0.7f).normalized * Speed * 0.8f;
                f.yaw = Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg;
                f.cd -= dt;
                if (f.cd <= 0f && d <= w.range + 0.3f && city.Clear(f.pos + Vector3.up, f.target.pos + Vector3.up)) Fire(f, f.target);
            }
            else
            {
                f.target = null;
                Vector3 step = Route(f.pos, f.goal);
                Vector3 d = Flat(step) - Flat(f.pos);
                want = d.magnitude > 0.6f ? d.normalized * Speed * 0.9f : Vector3.zero;
                if (f.stuckT > 0.6f) { f.goal = city.walkSpots[Random.Range(0, city.walkSpots.Count)]; f.stuckT = 0f; }
            }
            Move(f, want, dt);
        }

        // ------------------------------------------------------------------ tirs et dégâts

        void Fire(Fighter f, Fighter t)
        {
            Weapon w = Weapons[f.weapon];
            f.cd = w.rate * (f.player ? 1f : Random.Range(1.1f, 1.5f));   // les bots tirent un peu moins vite
            float d = (Flat(t.pos) - Flat(f.pos)).magnitude;
            if (f.weapon == 0)
            {
                // coup de poing
                f.rig.swingT = 0f;
                if (d <= w.range) Damage(t, w.dmg, f, true);
                return;
            }
            f.rig.shootT = 0f;
            if (f.weapon >= 2) f.rig.Recoil();
            // précision : baisse avec la distance et la vitesse de la cible ; les bots visent un peu moins bien
            float acc = Mathf.Clamp01(1f - w.spread * (d / Mathf.Max(1f, w.range)) * 2.2f - t.vel.magnitude * 0.03f) * (f.player ? 1f : 0.82f);
            bool hit = Random.value < acc;
            Vector3 from = f.pos + Vector3.up * 1.3f;
            Vector3 to = t.pos + Vector3.up * 1.1f;
            if (!hit) to += new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(-0.3f, 0.6f), Random.Range(-1.2f, 1.2f));
            Transform b = boltPool.Count > 0 ? boltPool.Pop() : null;
            if (b == null)
            {
                b = Art.Node(world, "bolt", from);
                Art.Part(b, Art.Capsule, Art.Glow(new Color(1f, 0.85f, 0.3f), new Color(3f, 2.2f, 0.6f), 0.5f), Vector3.zero, new Vector3(0.08f, 0.22f, 0.08f), new Vector3(90f, 0f, 0f), false);
            }
            b.gameObject.SetActive(true);
            b.position = from;
            b.rotation = Quaternion.LookRotation(to - from);
            bolts.Add(new Bolt { tr = b, from = from, to = to, dur = Mathf.Max(0.05f, (to - from).magnitude / 45f), shooter = f, victim = hit ? t : null, dmg = w.dmg });
            bool heard = (Flat(f.pos) - Flat(me.pos)).magnitude < 16f;
            if (f.player || heard) Snd.PlayPitch(Snd.I.shot, f.player ? 0.35f : 0.15f, f.weapon == 3 ? 0.7f : f.weapon == 2 ? 1.15f : 1f);
            if (f.player && f.weapon == 3) camShake = Mathf.Max(camShake, 0.2f);
        }

        void TickBolts(float dt)
        {
            for (int i = bolts.Count - 1; i >= 0; i--)
            {
                Bolt b = bolts[i];
                b.t += dt;
                float k = b.t / b.dur;
                b.tr.position = Vector3.Lerp(b.from, b.to, k);
                if (k < 1f) continue;
                b.tr.gameObject.SetActive(false);
                boltPool.Push(b.tr);
                bolts.RemoveAt(i);
                if (b.victim != null && b.victim.alive) Damage(b.victim, b.dmg, b.shooter, true);
                else if (b.victim == null) Fx.Dust(b.to);
            }
        }

        void Damage(Fighter f, float dmg, Fighter by, bool hitFx)
        {
            if (!f.alive) return;
            if (by != null && f.helmet) dmg *= 0.8f;
            if (by != null && f.armor > 0f)
            {
                float absorbed = Mathf.Min(f.armor, dmg * 0.5f);
                f.armor -= absorbed;
                dmg -= absorbed;
                if (f.armor <= 0f) { f.armor = 0f; Gear(f); }
            }
            f.hp -= dmg;
            if (hitFx)
            {
                f.rig.Flash(Color.white, 0.08f);
                Fx.Hit(f.pos + Vector3.up * 1.1f);
                bool show = f.player || (by != null && by.player);
                if (show) hud.Float(f.pos + Vector3.up * 2.4f, Mathf.RoundToInt(dmg).ToString(), f.player ? Hud.Bad : Hud.Yellow, false);
                if (f.player) { hud.Damage(0.5f); camShake = Mathf.Max(camShake, 0.25f); Haptics.Pulse(15); }
                if (by != null && by.player) Snd.PlayPitch(Snd.I.bonk, 0.35f, 1.3f);
            }
            else if (f.player && Mathf.Repeat(stT, 0.5f) < Time.deltaTime) hud.Damage(0.35f);
            if (by != null && !f.player && f.target == null && by.alive) f.target = by;   // il riposte
            if (f.hp <= 0f) Eliminate(f, by);
        }

        void Eliminate(Fighter f, Fighter by)
        {
            f.alive = false;
            f.hp = 0f;
            int rank = alive;
            alive--;
            if (by != null) by.kills++;
            f.vanish = 0.4f;
            f.vanishS = f.rig.root.localScale.x;
            Fx.Hit(f.pos + Vector3.up);
            Fx.Puff(f.pos + Vector3.up * 0.5f);
            // il lâche son arme et un soin
            if (f.weapon > 0) SpawnLoot(f.weapon == 1 ? Item.Weapon1 : f.weapon == 2 ? Item.Weapon2 : Item.Weapon3, f.pos + new Vector3(0.6f, 0f, 0f));
            if (Random.value < 0.6f) SpawnLoot(Item.Medkit, f.pos + new Vector3(-0.6f, 0f, 0.3f));
            string who = by == null ? L("the zone", "la zone") : by.name;
            hud.RoyaleFeed(by != null && by.player ? L("You eliminated ", "Tu as éliminé ") + f.name : f.player ? who + L(" eliminated you", " t'a éliminé") : who + " > " + f.name, by != null && by.player ? Hud.Yellow : f.player ? Hud.Bad : Hud.Info);
            if (by != null && by.player)
            {
                hud.Toast(L("ELIMINATED!", "ÉLIMINÉ !"));
                Snd.Play(Snd.I.perfect, 0.6f, 0f);
                camShake = Mathf.Max(camShake, 0.3f);
            }
            if (f.player)
            {
                place = rank;
                me.target = by;
                st = St.End;
                endT = 0f;
                stickOn = false;
                Snd.Play(Snd.I.hurt, 1f);
                hud.Toast("#" + rank);
            }
            else if (alive <= 1 && me.alive)
            {
                place = 1;
                st = St.End;
                endT = -0.6f;
                stickOn = false;
                hud.Toast(L("TOP 1!", "TOP 1 !"));
                Snd.Play(Snd.I.perfect, 1f, 0f);
                me.rig.anim = Anim.Cheer;
                Fx.Confetti(me.pos + Vector3.up * 2f);
            }
        }

        void ShowResult()
        {
            st = St.Result;
            int gain = me.kills * 25 + Mathf.Max(0, (Players - place + 1)) * 10 + (place == 1 ? 150 : 0);
            PlayerPrefs.SetInt(KCash, PlayerPrefs.GetInt(KCash, 0) + gain);
            if (place == 1) PlayerPrefs.SetInt(KWins, PlayerPrefs.GetInt(KWins, 0) + 1);
            PlayerPrefs.Save();
            hud.RoyaleResult(place, Players, me.kills, gain, PlayerPrefs.GetInt(KCash, 0));
            Perf.Note("fin : place " + place + "/" + Players + ", " + me.kills + " éliminations");
        }

        // ------------------------------------------------------------------ rendu des combattants, objets, flèche

        void Pose(Fighter f, float dt)
        {
            if (f.vanish >= 0f)
            {
                f.vanish -= dt;
                float k = Mathf.Clamp01(f.vanish / 0.4f);
                f.rig.root.localScale = Vector3.one * f.vanishS * k;
                f.rig.root.localRotation = Quaternion.Euler(-80f * (1f - k), f.yaw, 0f);
                if (f.vanish <= 0f) f.rig.root.gameObject.SetActive(false);
                f.rig.Tick(dt);
                return;
            }
            if (!f.alive) return;
            if (f.dropY <= 0f && st != St.Title)
            {
                float sp = f.vel.magnitude;
                if (st == St.End && f.player && place == 1) f.rig.anim = Anim.Cheer;
                else f.rig.anim = sp > 0.6f ? Anim.Run : Anim.Stand;
                f.rig.speed = Mathf.Max(4f, sp * 1.4f);
            }
            f.rig.root.localPosition = f.pos + Vector3.up * (f.dropY + (OnWalk(f.pos) ? 0.14f : 0f));
            f.rig.root.localRotation = Quaternion.Euler(0f, f.yaw, 0f);
            f.rig.Tick(dt);
        }

        static bool OnWalk(Vector3 p)
        {
            for (int i = 0; i < HeistCity.N; i++)
                for (int j = 0; j < HeistCity.N; j++)
                {
                    Rect b = HeistCity.BlockRect(i, j);
                    if (p.x > b.xMin - 2.2f && p.x < b.xMax + 2.2f && p.z > b.yMin - 2.2f && p.z < b.yMax + 2.2f) return true;
                }
            return false;
        }

        void TickLoot(float dt)
        {
            foreach (Loot l in loot)
            {
                if (l.taken || l.tr == null) continue;
                Transform s = l.tr.GetChild(l.tr.childCount - 1);
                s.localRotation = Quaternion.Euler(0f, stT * 90f, 0f);
                s.localPosition = new Vector3(0f, 0.9f + 0.15f * Mathf.Sin(stT * 3f + l.pos.x), 0f);
            }
        }

        void TickArrow()
        {
            // flèche bleue vers la zone quand tu es dehors (ou que la zone va te rattraper)
            bool show = me.alive && me.dropY <= 0f && (Outside(me.pos) || (zoneNext.gameObject.activeSelf && (Flat(me.pos) - Flat(zoneToC)).magnitude > zoneToR));
            arrow.gameObject.SetActive(show);
            if (!show) return;
            Vector3 c = zoneNext.gameObject.activeSelf ? zoneToC : zoneC;
            Vector3 dir = (Flat(c) - Flat(me.pos)).normalized;
            arrow.position = me.pos + dir * 1.9f + Vector3.up * 0.25f;
            arrow.rotation = Quaternion.LookRotation(dir);
            float s = 1f + 0.12f * Mathf.Sin(stT * 8f);
            arrow.localScale = new Vector3(s, 1f, s);
        }
    }
}
