using System.Collections.Generic;
using UnityEngine;

namespace SamediRouge
{
    /// <summary>
    /// V3 « Braquage » : vue de dessus façon GTA, parties courtes. On braque des boutiques (rester dans le cercle),
    /// on ramène le butin au van ; chaque braquage fait monter le niveau de recherche (étoiles) : flics à pied,
    /// puis voitures de police qui déposent des agents. Tir automatique, recrues qui suivent et tirent.
    /// </summary>
    public partial class Heist : MonoBehaviour
    {
        static string L(string en, string fr) => Loc.T(en, fr);

        // ------------------------------------------------------------------ réglages

        const float HeroSpeed = 6.6f, HeroRadius = 0.45f, ShootRange = 10.5f, BoltSpeed = 32f;
        const float RobTime = 2.6f, MaxStars = 5f;
        const int MaxCrew = 6, MaxHearts = 3;
        const float CharScale = 1.3f;   // personnages un peu plus gros que nature : lisibles vu d'en haut

        enum St { Title, Play, Win, Lose, Result }

        class Agent
        {
            public Rig rig;
            public Vector3 pos, vel;
            public float yaw, cd, hp, vanish = -1f, vanishS, hitT, stuck;
            public Vector3 goal;
            public bool dead;
            public Transform mark;
        }

        class Car
        {
            public Transform tr;
            public Vector3 pos, dir;
            public float speed, life, stopT = -1f, siren;
            public bool police, parked;
            public Transform lightA, lightB;
            public int unload;
        }

        class Bolt { public Transform tr; public Agent target; public float life; }

        // ------------------------------------------------------------------ état

        Camera cam;
        Transform world;
        Hud hud;
        HeistCity city;
        St st = St.Title;
        int level = 1;
        float stT;

        Rig hero;
        Vector3 heroPos, heroVel;
        float heroYaw, heroCd, inv;
        int hearts;
        Transform bag;

        int carried, banked, target, earned;
        float stars, starDecayT, copSpawnT, carSpawnT, trafficT;
        HeistCity.Shop robbing;

        readonly List<Agent> cops = new List<Agent>();
        readonly List<Agent> crew = new List<Agent>();
        readonly List<Agent> recruits = new List<Agent>();
        readonly List<Agent> walkers = new List<Agent>();
        readonly List<Car> cars = new List<Car>();
        readonly List<Bolt> bolts = new List<Bolt>();
        readonly Stack<Transform> boltPool = new Stack<Transform>();
        Transform arrow;
        float camShake;

        // joystick flottant
        bool stickOn;
        Vector2 stickStart, stickNow;
        int stickId = -1;

        const string KLevel = "sr_heist_level", KCash = "sr_heist_cash";

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
            hud.InitHeist();
            hud.onHeistNext = () => { if (st == St.Result) { BuildLevel(); Begin(); } };
            level = Mathf.Max(1, PlayerPrefs.GetInt(KLevel, 1));
#if UNITY_EDITOR
            DevStart();
#endif
            BuildLevel();
            if (devSkipTitle || !hud.ShowTitle(() => { if (st == St.Title) Begin(); })) Begin();
        }

        bool devSkipTitle, devBot;
        Vector3 lastGoal;
#if UNITY_EDITOR
        string devCmd = "";
        void DevStart()
        {
            try
            {
                string path = System.IO.Path.Combine(Application.dataPath, "../Logs/sr-screen.txt");
                if (System.IO.File.Exists(path)) devCmd = System.IO.File.ReadAllText(path);
            }
            catch (System.Exception) { }
            var m = System.Text.RegularExpressions.Regex.Match(devCmd, @"lv\s+(\d+)");
            if (m.Success) level = int.Parse(m.Groups[1].Value);
            devSkipTitle = devCmd.Contains("play");
            devBot = devCmd.Contains("bot");
            Perf.On = devCmd.Contains("perf");
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
            cam.farClipPlane = 120f;
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
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 110f;
        }

        // ------------------------------------------------------------------ niveau

        void ClearLevel()
        {
            foreach (Agent a in cops) if (a.rig != null) Destroy(a.rig.root.gameObject);
            foreach (Agent a in crew) if (a.rig != null) Destroy(a.rig.root.gameObject);
            foreach (Agent a in recruits) { if (a.rig != null) Destroy(a.rig.root.gameObject); if (a.mark != null) Destroy(a.mark.gameObject); }
            foreach (Agent a in walkers) if (a.rig != null) Destroy(a.rig.root.gameObject);
            foreach (Car c in cars) if (c.tr != null) Destroy(c.tr.gameObject);
            foreach (Bolt b in bolts) if (b.tr != null) Destroy(b.tr.gameObject);
            cops.Clear(); crew.Clear(); recruits.Clear(); walkers.Clear(); cars.Clear(); bolts.Clear(); boltPool.Clear();
            if (city != null) Destroy(city.root.gameObject);
            if (hero != null) Destroy(hero.root.gameObject);
            if (arrow != null) Destroy(arrow.gameObject);
        }

        void BuildLevel()
        {
            ClearLevel();
            city = new HeistCity(world, level, 1000 + level * 7919);
            target = 300 + 200 * (level - 1);
            carried = banked = earned = 0;
            stars = 0f;
            starDecayT = 0f;
            copSpawnT = 6f;
            carSpawnT = 0f;
            trafficT = 0f;
            hearts = MaxHearts;
            inv = 0f;
            robbing = null;

            heroPos = city.vanDoor + new Vector3(-0.5f, 0f, -2f);
            heroYaw = 0f;
            hero = Rig.Build(Kind.Thief, world, heroPos, 0f, -1);
            hero.root.localScale *= CharScale;
            hero.SetGear(new[] { false, false, false, false, true, false });
            hero.SetVisible(true);
            bag = Art.Node(hero.spine != null ? hero.spine : hero.root, "bag", new Vector3(0f, 0.1f, -0.32f));
            Material sack = Art.Mat(new Color(0.55f, 0.42f, 0.26f), 0.25f);
            Art.Part(bag, Art.Sphere, sack, Vector3.zero, new Vector3(0.6f, 0.62f, 0.5f), default, false);
            Art.Part(bag, Art.Sphere, Art.Glow(new Color(0.3f, 0.75f, 0.35f), new Color(0.2f, 0.6f, 0.25f), 0.4f), new Vector3(0f, 0.08f, -0.24f), new Vector3(0.24f, 0.24f, 0.06f), default, false);
            bag.gameObject.SetActive(false);

            arrow = MakeArrow();

            // recrues qui attendent au coin des rues
            foreach (Vector3 p in city.recruitSpots)
            {
                var a = new Agent { pos = p + Vector3.up * 0.14f, yaw = Random.Range(0f, 360f) };
                a.rig = Rig.Build(Kind.Thief, world, a.pos, a.yaw, Rig.Crew);
                a.rig.root.localScale *= CharScale;
                a.rig.anim = Anim.Stand;
                a.goal = p;
                a.mark = Art.Decal(world, Art.Particle(HeistCity.RingTex, new Color(0.3f, 1f, 0.45f, 0.9f)), a.pos + new Vector3(0f, 0.04f, 0f), new Vector2(2.4f, 2.4f)).transform;
                recruits.Add(a);
            }
            // promeneurs
            for (int i = 0; i < 7 && i < city.walkSpots.Count; i++)
            {
                Vector3 p = city.walkSpots[(i * 5) % city.walkSpots.Count] + Vector3.up * 0.14f;
                var a = new Agent { pos = p, yaw = Random.Range(0f, 360f) };
                a.rig = Rig.Build(Kind.Shopper, world, p, a.yaw, Random.Range(0, 6));
                a.rig.root.localScale *= CharScale * 0.95f;
                a.goal = city.walkSpots[Random.Range(0, city.walkSpots.Count)];
                walkers.Add(a);
            }
            // un peu de circulation
            for (int i = 0; i < 6; i++) SpawnCar(false, true);

            st = St.Title;
            hud.HeistHide();
            PlaceCamera(true);
            Perf.Note("niveau " + level + " : objectif " + target + " $, " + city.shops.Count + " boutiques");
        }

        void Begin()
        {
            st = St.Play;
            stT = 0f;
            hud.HeistShow(level, target);
            hud.Toast(L("ROB THE SHOPS!", "BRAQUE LES BOUTIQUES !"));
            Snd.Play(Snd.I.whoosh, 0.6f);
        }

        Transform MakeArrow()
        {
            Transform a = Art.Node(world, "arrow", Vector3.zero);
            Material m = Art.Glow(new Color(1f, 0.85f, 0.2f), new Color(1.3f, 0.95f, 0.2f), 0.5f);
            Art.Part(a, Art.Cube, m, new Vector3(0f, 0f, 0.1f), new Vector3(0.28f, 0.06f, 0.9f), default, false);
            Art.Part(a, Art.Cube, m, new Vector3(-0.22f, 0f, 0.62f), new Vector3(0.22f, 0.06f, 0.62f), new Vector3(0f, 40f, 0f), false);
            Art.Part(a, Art.Cube, m, new Vector3(0.22f, 0f, 0.62f), new Vector3(0.22f, 0.06f, 0.62f), new Vector3(0f, -40f, 0f), false);
            return a;
        }

        // ------------------------------------------------------------------ boucle

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            Perf.Frame();
            stT += dt;
            ReadInput();
            hud.Tick(dt);

            if (st == St.Play) PlayTick(dt);
            else if (st == St.Win) WinTick(dt);
            else if (st == St.Lose) LoseTick(dt);

            TickAgents(dt);
            TickCars(dt);
            TickBolts(dt);
            TickShops(dt);
            if (hero != null)
            {
                hero.root.localPosition = heroPos;
                hero.root.localRotation = Quaternion.Euler(0f, heroYaw, 0f);
                hero.Tick(dt);
            }
            PlaceCamera(false);
            if (st == St.Play || st == St.Win) hud.HeistTick(banked, target, carried, stars, hearts, cam.WorldToScreenPoint(heroPos + Vector3.up * 2.6f), stickOn, stickStart, stickNow, RobFraction(), robbing != null ? cam.WorldToScreenPoint(robbing.door + Vector3.up * 1.8f) : Vector3.zero);
        }

        float RobFraction() => robbing != null ? robbing.progress : -1f;

        void PlaceCamera(bool snap)
        {
            Vector3 focus = heroPos + new Vector3(0f, 0f, 2.2f);
            Vector3 want = focus + new Vector3(0f, 20f, -15f);
            if (st == St.Win && city != null) want = city.van.position + new Vector3(0f, 18f, -11f);
            float k = snap ? 1f : 1f - Mathf.Exp(-6f * Time.deltaTime);
            cam.transform.position = Vector3.Lerp(cam.transform.position, want, k);
            Vector3 shake = camShake > 0f ? Random.insideUnitSphere * camShake * 0.3f : Vector3.zero;
            camShake = Mathf.MoveTowards(camShake, 0f, Time.deltaTime * 2f);
            cam.transform.rotation = Quaternion.Euler(53f, 0f, 0f);
            cam.transform.position += shake;
        }

        // ------------------------------------------------------------------ entrées : joystick flottant + clavier

        Vector2 Move()
        {
            Vector2 k = new Vector2((Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f),
                                    (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.Z) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f));
            if (k.sqrMagnitude > 0.01f) return k.normalized;
            if (devBot && st == St.Play)
            {
                // outil de dev : le héros suit la flèche (contourne mal les immeubles, mais suffit pour tester la boucle)
                Vector3 aim = lastGoal;
                if (!city.Clear(heroPos, lastGoal))
                {
                    // contourne par le carrefour qui raccourcit le trajet
                    float best = 1e9f;
                    for (int i = 0; i <= HeistCity.N; i++)
                        for (int j = 0; j <= HeistCity.N; j++)
                        {
                            Vector3 n = HeistCity.Node(i, j);
                            if ((Flat(n) - Flat(heroPos)).magnitude < 1.5f || !city.Clear(heroPos, n)) continue;
                            float c = (n - heroPos).magnitude + (lastGoal - n).magnitude * (city.Clear(n, lastGoal) ? 1f : 1.6f);
                            if (c < best) { best = c; aim = n; }
                        }
                }
                Vector3 dg = Flat(aim) - Flat(heroPos);
                if (dg.magnitude < 0.6f) return Vector2.zero;
                Vector3 nd = dg.normalized;
                return new Vector2(nd.x, nd.z);
            }
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
                stickOn = st == St.Play;
                stickStart = stickNow = pos;
                if (Input.touchCount == 0) stickId = 0;
            }
            else if (held && stickOn) stickNow = pos;
            if (up) { stickOn = false; stickId = -1; }
        }

        // ------------------------------------------------------------------ jeu

        void PlayTick(float dt)
        {
            Perf.Mark("input");
            Vector2 mv = Move();
            Vector3 want = new Vector3(mv.x, 0f, mv.y) * HeroSpeed * (robbing != null ? 0.0f : 1f);
            if (robbing != null && mv.magnitude > 0.5f) want = new Vector3(mv.x, 0f, mv.y) * HeroSpeed;   // on peut toujours partir
            heroVel = Vector3.Lerp(heroVel, want, 1f - Mathf.Exp(-14f * dt));
            heroPos += heroVel * dt;
            heroPos = city.PushOut(heroPos, HeroRadius);
            heroPos.y = OnWalk(heroPos) ? 0.14f : 0f;
            if (heroVel.sqrMagnitude > 0.3f) heroYaw = Mathf.MoveTowardsAngle(heroYaw, Mathf.Atan2(heroVel.x, heroVel.z) * Mathf.Rad2Deg, 900f * dt);
            hero.anim = heroVel.magnitude > 0.6f ? Anim.Run : Anim.Stand;
            hero.speed = Mathf.Max(4f, heroVel.magnitude * 1.4f);
            inv -= dt;
            if (inv > 0f) hero.SetVisible(Mathf.Repeat(inv * 10f, 1f) > 0.35f); else hero.SetVisible(true);

            // braquage : rester dans le cercle d'une boutique
            HeistCity.Shop near = null;
            foreach (HeistCity.Shop s in city.shops)
                if (!s.robbed && (Flat(s.door) - Flat(heroPos)).magnitude < 1.7f) { near = s; break; }
            if (near != robbing)
            {
                if (robbing != null && !robbing.robbed) robbing.progress = Mathf.Max(0f, robbing.progress);
                robbing = near;
                if (robbing != null) { Snd.PlayPitch(Snd.I.siren, 0.25f, 1.2f); robbing.alarm.gameObject.SetActive(true); }
            }
            if (robbing != null)
            {
                robbing.progress += dt * (1f + 0.35f * crew.Count) / RobTime;
                if (Mathf.Repeat(stT, 0.25f) < dt) { Fx.Coin(robbing.door + Vector3.up * 1.2f); Snd.PlayPitch(Snd.I.coin, 0.25f, 1f + robbing.progress * 0.6f); }
                heroYaw = Mathf.MoveTowardsAngle(heroYaw, Mathf.Atan2(-robbing.face.x, -robbing.face.z) * Mathf.Rad2Deg, 600f * dt);
                if (robbing.progress >= 1f) Robbed(robbing);
            }

            // planque : on dépose le butin
            if (carried > 0 && (Flat(city.vanDoor) - Flat(heroPos)).magnitude < 2.2f)
            {
                banked += carried;
                hud.Float(city.vanDoor + Vector3.up * 2.4f, "+" + carried + " $", Hud.Good, true);
                Fx.Confetti(city.vanDoor + Vector3.up * 1.5f);
                Snd.Play(Snd.I.pickup, 0.9f, 0f);
                carried = 0;
                hud.HeistPop();
                if (banked >= target) { Win(); return; }
            }
            bag.gameObject.SetActive(carried > 0);
            if (carried > 0) bag.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.7f, Mathf.Clamp01(carried / 500f));

            // recrues : on passe dessus
            for (int i = recruits.Count - 1; i >= 0; i--)
            {
                Agent r = recruits[i];
                if ((Flat(r.pos) - Flat(heroPos)).magnitude > 1.6f) continue;
                recruits.RemoveAt(i);
                if (crew.Count >= MaxCrew) { Destroy(r.rig.root.gameObject); continue; }
                // le cercle vert disparaît, il prend un pistolet
                if (r.mark != null) Destroy(r.mark.gameObject);
                r.rig.CrewWeapon(1);
                r.rig.Flash(new Color(0.5f, 1f, 0.6f), 0.2f);
                crew.Add(r);
                hud.Float(r.pos + Vector3.up * 2.4f, L("+1 CREW", "+1 COMPLICE"), Hud.Good, true);
                Snd.Play(Snd.I.pickup, 0.7f);
                Fx.Pickup(r.pos + Vector3.up);
            }

            // niveau de recherche : il retombe si on se fait oublier
            starDecayT += dt;
            bool chased = false;
            foreach (Agent c in cops) if (!c.dead && (Flat(c.pos) - Flat(heroPos)).magnitude < 16f) { chased = true; break; }
            if (chased) starDecayT = Mathf.Min(starDecayT, 4f);
            if (stars > 0f && starDecayT > 14f) { stars = Mathf.Max(0f, stars - dt * 0.12f); }

            // auto-tir
            heroCd -= dt;
            if (heroCd <= 0f)
            {
                Agent t = Nearest(heroPos, ShootRange);
                if (t != null) { Shoot(heroPos + Vector3.up * 1.3f, t, true); heroCd = 0.32f; heroYaw = Mathf.Atan2(t.pos.x - heroPos.x, t.pos.z - heroPos.z) * Mathf.Rad2Deg; }
            }

            SpawnPolice(dt);
            TickArrow();
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>Sur un trottoir (légèrement surélevé) ?</summary>
        bool OnWalk(Vector3 p)
        {
            for (int i = 0; i < HeistCity.N; i++)
                for (int j = 0; j < HeistCity.N; j++)
                {
                    Rect b = HeistCity.BlockRect(i, j);
                    if (p.x > b.xMin - 2.2f && p.x < b.xMax + 2.2f && p.z > b.yMin - 2.2f && p.z < b.yMax + 2.2f) return true;
                }
            return false;
        }

        void Robbed(HeistCity.Shop s)
        {
            s.robbed = true;
            s.progress = 1f;
            int gain = Mathf.RoundToInt(s.value * (1f + 0.15f * crew.Count) / 5f) * 5;
            carried += gain;
            earned += gain;
            robbing = null;
            hud.Float(s.door + Vector3.up * 2.6f, "+" + gain + " $", Hud.Yellow, true);
            Fx.Confetti(s.door + Vector3.up * 1.4f);
            Fx.Glass(s.door + Vector3.up * 1.5f - s.face * 0.8f);
            Snd.Play(Snd.I.glass, 0.7f);
            Snd.Play(Snd.I.perfect, 0.6f, 0f);
            hero.Land(0.3f);
            camShake = 0.5f;
            // vitrine éteinte, cercle grisé
            s.ring.gameObject.SetActive(false);
            s.sign.gameObject.SetActive(false);
            s.robbedAt = stT;
            if (s.front != null) s.front.sharedMaterial = Art.Mat(new Color(0.25f, 0.25f, 0.28f), 0.6f);
            // les flics arrivent
            float before = stars;
            stars = Mathf.Min(MaxStars, Mathf.Floor(stars) + 1f);
            starDecayT = 0f;
            if (Mathf.FloorToInt(stars) > Mathf.FloorToInt(before)) { hud.HeistStarPop(); Snd.PlayPitch(Snd.I.siren, 0.5f, 1f); }
            copSpawnT = Mathf.Min(copSpawnT, 1.2f);
            carSpawnT = Mathf.Min(carSpawnT, stars >= 2f ? 1.5f : 99f);
        }

        void TickArrow()
        {
            // flèche au sol : vers le van si on porte du butin (ou s'il n'y a plus rien à braquer), sinon vers la boutique la plus proche
            Vector3 goal = city.vanDoor;
            bool toVan = carried > 0 && (carried + banked >= target || carried >= 200);
            if (!toVan)
            {
                float best = 1e9f;
                foreach (HeistCity.Shop s in city.shops)
                {
                    if (s.robbed) continue;
                    float d = (s.door - heroPos).sqrMagnitude;
                    if (d < best) { best = d; goal = s.door; }
                }
                if (best > 1e8f) toVan = true;
            }
            lastGoal = goal;
            Vector3 d3 = Flat(goal) - Flat(heroPos);
            bool show = d3.magnitude > 4f && robbing == null;
            arrow.gameObject.SetActive(show);
            if (!show) return;
            Vector3 dir = d3.normalized;
            arrow.position = heroPos + dir * 1.9f + Vector3.up * 0.2f;
            arrow.rotation = Quaternion.LookRotation(dir);
            float s2 = 1f + 0.12f * Mathf.Sin(stT * 8f);
            arrow.localScale = new Vector3(s2, 1f, s2);
            Color c = toVan ? new Color(0.35f, 1f, 0.45f) : new Color(1f, 0.85f, 0.2f);
            foreach (MeshRenderer mr in arrow.GetComponentsInChildren<MeshRenderer>()) mr.sharedMaterial = Art.Glow(c, c * 1.3f, 0.5f);
        }

        // ------------------------------------------------------------------ police

        int CopTarget => Mathf.Clamp(Mathf.CeilToInt(stars * 1.6f) + (stars > 0f ? crew.Count / 2 : 0), 0, 12);

        void SpawnPolice(float dt)
        {
            int alive = 0;
            foreach (Agent c in cops) if (!c.dead) alive++;
            int want = CopTarget;
            copSpawnT -= dt;
            if (stars >= 1f && alive < want && copSpawnT <= 0f)
            {
                // un agent à pied arrive d'un carrefour hors de l'écran
                copSpawnT = Mathf.Lerp(3.5f, 1.2f, stars / MaxStars);
                SpawnCop(OffscreenNode());
            }
            carSpawnT -= dt;
            if (stars >= 2f && alive + 2 <= want + 2 && carSpawnT <= 0f)
            {
                carSpawnT = Mathf.Lerp(9f, 4f, stars / MaxStars);
                SpawnCar(true, false);
            }
            trafficT -= dt;
            int civ = 0;
            foreach (Car c in cars) if (!c.police) civ++;
            if (civ < 6 && trafficT <= 0f) { trafficT = 2f; SpawnCar(false, false); }
        }

        Vector3 OffscreenNode()
        {
            Vector3 best = HeistCity.Node(0, 0);
            float bd = -1f;
            for (int k = 0; k < 6; k++)
            {
                Vector3 n = HeistCity.Node(Random.Range(0, HeistCity.N + 1), Random.Range(0, HeistCity.N + 1));
                float d = (n - heroPos).magnitude;
                if (d > 18f && d < 40f) return n;
                if (d > bd) { bd = d; best = n; }
            }
            return best;
        }

        void SpawnCop(Vector3 at)
        {
            var a = new Agent { pos = at, hp = 1f + Mathf.Floor(stars / 2f), cd = Random.Range(0.4f, 1f) };
            a.rig = Rig.Build(Kind.Guard, world, at, 0f, 0);
            a.rig.root.localScale *= CharScale;
            cops.Add(a);
        }

        void Shoot(Vector3 from, Agent t, bool heroShot)
        {
            Transform b = boltPool.Count > 0 ? boltPool.Pop() : null;
            if (b == null)
            {
                b = Art.Node(world, "bolt", from);
                Color c = new Color(1f, 0.31f, 0.64f);
                Art.Part(b, Art.Sphere, Art.Glow(c, c * 2f, 0.6f), Vector3.zero, Vector3.one * 0.22f, default, false);
            }
            b.gameObject.SetActive(true);
            b.position = from;
            bolts.Add(new Bolt { tr = b, target = t, life = 1.2f });
            if (heroShot) { hero.shootT = 0f; Snd.PlayPitch(Snd.I.shot, 0.35f, Random.Range(0.95f, 1.1f)); }
        }

        void TickBolts(float dt)
        {
            for (int i = bolts.Count - 1; i >= 0; i--)
            {
                Bolt b = bolts[i];
                b.life -= dt;
                Agent t = b.target;
                if (t == null || t.dead || b.life <= 0f) { Free(b); bolts.RemoveAt(i); continue; }
                Vector3 aim = t.pos + Vector3.up * 1.1f;
                Vector3 d = aim - b.tr.position;
                float step = BoltSpeed * dt;
                if (d.magnitude <= step + 0.25f)
                {
                    Free(b);
                    bolts.RemoveAt(i);
                    HitCop(t);
                    continue;
                }
                b.tr.position += d.normalized * step;
            }
        }

        void Free(Bolt b) { b.tr.gameObject.SetActive(false); boltPool.Push(b.tr); }

        void HitCop(Agent c)
        {
            if (c.dead) return;
            c.hp -= 1f;
            c.rig.Flash(Color.white, 0.1f);
            c.pos += (Flat(c.pos) - Flat(heroPos)).normalized * 0.5f;
            Snd.PlayPitch(Snd.I.bonk, 0.5f, Random.Range(0.9f, 1.15f));
            if (c.hp > 0f) return;
            c.dead = true;
            c.vanish = 0.35f;
            c.vanishS = c.rig.root.localScale.x;
            Fx.Hit(c.pos + Vector3.up * 1.1f);
            earned += 5;
        }

        Agent Nearest(Vector3 from, float range)
        {
            Agent best = null;
            float bd = range * range;
            foreach (Agent c in cops)
            {
                if (c.dead) continue;
                float d = (Flat(c.pos) - Flat(from)).sqrMagnitude;
                if (d < bd && city.Clear(from + Vector3.up, c.pos + Vector3.up)) { bd = d; best = c; }
            }
            return best;
        }

        void Hurt(Agent by)
        {
            if (inv > 0f || st != St.Play) return;
            inv = 1.4f;
            // le butin porté s'envole en partie
            int lost = Mathf.RoundToInt(carried * 0.4f / 5f) * 5;
            carried -= lost;
            if (lost > 0) { hud.Float(heroPos + Vector3.up * 2.6f, "-" + lost + " $", Hud.Bad, true); Fx.Coin(heroPos + Vector3.up * 1.2f); }
            hearts--;
            hud.Damage(1f);
            hero.Flash(new Color(1f, 0.3f, 0.25f), 0.2f);
            hero.Land(0.4f);
            camShake = 0.8f;
            Snd.Play(Snd.I.hurt, 0.9f);
            Haptics.Pulse(40, true);
            heroVel += (Flat(heroPos) - Flat(by.pos)).normalized * 9f;
            if (hearts <= 0) Lose();
        }

        // ------------------------------------------------------------------ agents (flics, bande, recrues, passants)

        void TickAgents(float dt)
        {
            Perf.Mark("agents");
            // flics : foncent sur le plus proche (toi ou un complice) ; un contact = un coup
            for (int i = cops.Count - 1; i >= 0; i--)
            {
                Agent c = cops[i];
                if (c.dead)
                {
                    c.vanish -= dt;
                    float k = Mathf.Clamp01(c.vanish / 0.35f);
                    c.rig.root.localScale = Vector3.one * c.vanishS * k;
                    c.rig.root.localRotation = Quaternion.Euler(-70f * (1f - k), c.yaw, 0f);
                    if (c.vanish <= 0f) { Fx.Puff(c.pos + Vector3.up * 0.6f); Destroy(c.rig.root.gameObject); cops.RemoveAt(i); }
                    continue;
                }
                Vector3 goal = heroPos;
                Agent mate = null;
                float gd = (Flat(heroPos) - Flat(c.pos)).sqrMagnitude;
                foreach (Agent m in crew)
                {
                    float d = (Flat(m.pos) - Flat(c.pos)).sqrMagnitude;
                    if (d < gd) { gd = d; goal = m.pos; mate = m; }
                }
                bool active = st == St.Play;
                float sp = active ? Mathf.Min(4.4f + 0.35f * stars, 6.3f) : 0f;
                Vector3 to = Flat(goal) - Flat(c.pos);
                Vector3 v = to.magnitude > 0.1f ? to.normalized * sp : Vector3.zero;
                c.vel = Vector3.Lerp(c.vel, v, 1f - Mathf.Exp(-6f * dt));
                c.pos += c.vel * dt;
                c.pos = city.PushOut(c.pos, 0.4f);
                c.pos.y = OnWalk(c.pos) ? 0.14f : 0f;
                Separate(c, cops, 0.8f);
                if (c.vel.sqrMagnitude > 0.2f) c.yaw = Mathf.MoveTowardsAngle(c.yaw, Mathf.Atan2(c.vel.x, c.vel.z) * Mathf.Rad2Deg, 600f * dt);
                c.cd -= dt;
                if (active && Mathf.Sqrt(gd) < 1.0f && c.cd <= 0f)
                {
                    c.cd = 1.2f;
                    c.rig.swingT = 0f;
                    if (mate != null) LoseMate(mate);
                    else Hurt(c);
                }
                c.rig.anim = c.vel.magnitude > 0.5f ? Anim.Run : Anim.Stand;
                c.rig.speed = Mathf.Max(4f, c.vel.magnitude * 1.4f);
                Pose(c, dt);
            }

            // bande : en grappe derrière toi, tire sur les flics à portée
            for (int i = 0; i < crew.Count; i++)
            {
                Agent m = crew[i];
                float ang = i * 2.39996f + 1.2f, r = 1.1f + 0.35f * Mathf.Sqrt(i);
                Vector3 slot = heroPos + Quaternion.Euler(0f, heroYaw, 0f) * new Vector3(Mathf.Cos(ang) * r, 0f, -0.9f + Mathf.Sin(ang) * r * 0.7f);
                Vector3 to = Flat(slot) - Flat(m.pos);
                float sp = Mathf.Min(HeroSpeed * 1.25f, to.magnitude * 5f);
                m.vel = Vector3.Lerp(m.vel, to.magnitude > 0.05f ? to.normalized * sp : Vector3.zero, 1f - Mathf.Exp(-10f * dt));
                m.pos += m.vel * dt;
                m.pos = city.PushOut(m.pos, 0.35f);
                m.pos.y = OnWalk(m.pos) ? 0.14f : 0f;
                Separate(m, crew, 0.7f);
                if (m.vel.sqrMagnitude > 0.3f) m.yaw = Mathf.MoveTowardsAngle(m.yaw, Mathf.Atan2(m.vel.x, m.vel.z) * Mathf.Rad2Deg, 700f * dt);
                m.cd -= dt;
                if (st == St.Play && m.cd <= 0f)
                {
                    Agent t = Nearest(m.pos, ShootRange * 0.9f);
                    if (t != null) { Shoot(m.pos + Vector3.up * 1.2f, t, false); m.cd = Random.Range(0.55f, 0.75f); m.yaw = Mathf.Atan2(t.pos.x - m.pos.x, t.pos.z - m.pos.z) * Mathf.Rad2Deg; m.rig.shootT = 0f; }
                }
                m.rig.anim = m.vel.magnitude > 0.6f ? Anim.Run : Anim.Stand;
                m.rig.speed = Mathf.Max(4f, m.vel.magnitude * 1.4f);
                Pose(m, dt);
            }

            // recrues : sur place, se dandinent en t'attendant
            foreach (Agent r in recruits)
            {
                Vector3 d = Flat(heroPos) - Flat(r.pos);
                if (d.magnitude < 9f) r.yaw = Mathf.MoveTowardsAngle(r.yaw, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 300f * dt);
                r.rig.anim = d.magnitude < 9f ? Anim.Cheer : Anim.Stand;
                Pose(r, dt);
            }

            // passants : se promènent ; avec la police aux trousses, ils fuient loin de toi
            foreach (Agent w in walkers)
            {
                Vector3 to = Flat(w.goal) - Flat(w.pos);
                bool panic = stars >= 1f && (Flat(heroPos) - Flat(w.pos)).magnitude < 9f;
                if (panic) to = (Flat(w.pos) - Flat(heroPos));
                else if (to.magnitude < 0.8f) { w.goal = city.walkSpots[Random.Range(0, city.walkSpots.Count)]; }
                float sp = panic ? 5f : 1.6f;
                w.vel = Vector3.Lerp(w.vel, to.magnitude > 0.1f ? to.normalized * sp : Vector3.zero, 1f - Mathf.Exp(-4f * dt));
                w.pos += w.vel * dt;
                Vector3 before = w.pos;
                w.pos = city.PushOut(w.pos, 0.35f);
                if ((w.pos - before).sqrMagnitude > 1e-6f) { w.stuck += dt; if (w.stuck > 1.5f) { w.goal = city.walkSpots[Random.Range(0, city.walkSpots.Count)]; w.stuck = 0f; } }
                w.pos.y = OnWalk(w.pos) ? 0.14f : 0f;
                if (w.vel.sqrMagnitude > 0.1f) w.yaw = Mathf.MoveTowardsAngle(w.yaw, Mathf.Atan2(w.vel.x, w.vel.z) * Mathf.Rad2Deg, 360f * dt);
                w.rig.anim = panic ? Anim.Run : w.vel.magnitude > 0.3f ? Anim.Walk : Anim.Stand;
                w.rig.speed = Mathf.Max(2f, w.vel.magnitude * 1.4f);
                Pose(w, dt);
            }
        }

        static void Separate(Agent a, List<Agent> list, float r)
        {
            foreach (Agent o in list)
            {
                if (o == a || o.dead) continue;
                Vector3 d = Flat(a.pos) - Flat(o.pos);
                float m = d.magnitude;
                if (m < r && m > 1e-4f) a.pos += d / m * (r - m) * 0.5f;
            }
        }

        static void Pose(Agent a, float dt)
        {
            a.rig.root.localPosition = a.pos;
            if (a.vanish < 0f) a.rig.root.localRotation = Quaternion.Euler(0f, a.yaw, 0f);
            a.rig.Tick(dt);
        }

        void LoseMate(Agent m)
        {
            crew.Remove(m);
            Fx.Hit(m.pos + Vector3.up);
            Snd.Play(Snd.I.hurt, 0.5f);
            m.rig.KO(new Vector3(Random.Range(-3f, 3f), 5f, Random.Range(-3f, 3f)), new Vector3(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f)));
            Destroy(m.rig.root.gameObject, 1.6f);
        }

        // ------------------------------------------------------------------ voitures (circulation, police)

        static readonly Vector3[] Dirs = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };

        void SpawnCar(bool police, bool anywhere)
        {
            int i, j;
            if (police)
            {
                Vector3 n = OffscreenNode();
                i = Mathf.RoundToInt(n.x / HeistCity.Pitch);
                j = Mathf.RoundToInt(n.z / HeistCity.Pitch);
            }
            else if (anywhere) { i = Random.Range(0, HeistCity.N + 1); j = Random.Range(0, HeistCity.N + 1); }
            else
            {
                Vector3 n = OffscreenNode();
                i = Mathf.RoundToInt(n.x / HeistCity.Pitch);
                j = Mathf.RoundToInt(n.z / HeistCity.Pitch);
            }
            Vector3 dir = Dirs[Random.Range(0, 4)];
            for (int k = 0; k < 4 && !Inside(HeistCity.Node(i, j) + dir * HeistCity.Pitch); k++) dir = Dirs[(System.Array.IndexOf(Dirs, dir) + 1) % 4];
            var c = new Car { police = police, dir = dir, speed = police ? 13f : Random.Range(6f, 8.5f), life = 0f };
            c.pos = HeistCity.Node(i, j) + LaneOff(dir);
            if (anywhere) c.pos += dir * Random.Range(0f, HeistCity.Pitch * 0.8f);
            c.tr = police ? PoliceCar() : CivCar();
            c.tr.SetParent(world, false);
            c.lightA = pendingLights.Item1;
            c.lightB = pendingLights.Item2;
            if (police) { c.unload = 2 + Mathf.FloorToInt(stars / 2f); c.siren = 0f; Snd.PlayPitch(Snd.I.siren, 0.35f, 1f); }
            cars.Add(c);
        }

        static Vector3 LaneOff(Vector3 dir) => new Vector3(dir.z, 0f, -dir.x) * 2.4f;   // on roule à droite

        static bool Inside(Vector3 p) => p.x > -1f && p.z > -1f && p.x < HeistCity.Size + 1f && p.z < HeistCity.Size + 1f;

        void TickCars(float dt)
        {
            Perf.Mark("cars");
            for (int i = cars.Count - 1; i >= 0; i--)
            {
                Car c = cars[i];
                c.life += dt;
                if (c.parked)
                {
                    c.stopT -= dt;
                    if (c.stopT <= 0f && (Flat(c.pos) - Flat(heroPos)).magnitude > 22f) { Destroy(c.tr.gameObject); cars.RemoveAt(i); continue; }
                }
                else
                {
                    // la voiture de police s'arrête près de toi et dépose ses agents
                    if (c.police && st == St.Play && (Flat(c.pos) - Flat(heroPos)).magnitude < 11f)
                    {
                        c.parked = true;
                        c.stopT = 12f;
                        Fx.Skid(c.pos);
                        for (int k = 0; k < c.unload; k++)
                            SpawnCop(c.pos + new Vector3(c.dir.z, 0f, -c.dir.x) * -1.6f + c.dir * (k - 0.5f) * 1.2f);
                        camShake = Mathf.Max(camShake, 0.3f);
                    }
                    else
                    {
                        Vector3 before = c.pos;
                        float sp = c.speed;
                        // ralentir derrière un piéton / le héros sur la chaussée
                        if (!c.police && (Flat(heroPos) - Flat(c.pos + c.dir * 3f)).magnitude < 2.2f) sp = 0f;
                        c.pos += c.dir * sp * dt;
                        // carrefour franchi : choisir une direction
                        Vector3 baseP = c.pos - LaneOff(c.dir);
                        float along = Vector3.Dot(baseP, c.dir), prev = Vector3.Dot(before - LaneOff(c.dir), c.dir);
                        bool crossed = Mathf.FloorToInt(prev / HeistCity.Pitch) != Mathf.FloorToInt(along / HeistCity.Pitch);
                        if (crossed) Turn(c, baseP);
                        if (!Inside(c.pos)) { Destroy(c.tr.gameObject); cars.RemoveAt(i); continue; }
                    }
                    // la voiture bouscule ce qui est sur son chemin
                    if (st == St.Play && (Flat(heroPos) - Flat(c.pos)).magnitude < 1.7f && c.speed > 0f && !c.parked)
                    {
                        Vector3 side = new Vector3(c.dir.z, 0f, -c.dir.x);
                        float s = Mathf.Sign(Vector3.Dot(heroPos - c.pos, side) + 0.001f);
                        heroVel += side * s * 10f;
                        hero.Land(0.3f);
                    }
                }
                c.tr.localPosition = c.pos;
                c.tr.localRotation = Quaternion.LookRotation(c.dir);
                if (c.police && c.lightA != null)
                {
                    c.siren += dt;
                    bool a = Mathf.Repeat(c.siren * 4f, 1f) < 0.5f;
                    c.lightA.gameObject.SetActive(a);
                    c.lightB.gameObject.SetActive(!a);
                }
            }
        }

        void Turn(Car c, Vector3 baseP)
        {
            // recale sur le carrefour
            int i = Mathf.RoundToInt(baseP.x / HeistCity.Pitch), j = Mathf.RoundToInt(baseP.z / HeistCity.Pitch);
            Vector3 n = HeistCity.Node(i, j);
            var opts = new List<Vector3>();
            foreach (Vector3 d in Dirs)
            {
                if (d == -c.dir) continue;
                if (!Inside(n + d * HeistCity.Pitch)) continue;
                opts.Add(d);
            }
            if (opts.Count == 0) return;
            Vector3 pick = opts[Random.Range(0, opts.Count)];
            if (c.police)
            {
                // la police prend la rue qui la rapproche de toi
                float best = 1e9f;
                foreach (Vector3 d in opts)
                {
                    float dd = (n + d * HeistCity.Pitch * 0.5f - heroPos).sqrMagnitude;
                    if (dd < best) { best = dd; pick = d; }
                }
            }
            if (pick == c.dir) return;
            c.dir = pick;
            c.pos = n + LaneOff(pick);
        }

        Transform PoliceCar()
        {
            Transform t = new GameObject("police_car").transform;
            Material white = Art.Mat(new Color(0.95f, 0.95f, 0.97f), 0.6f, 0.1f);
            Material blue = Art.Mat(new Color(0.12f, 0.22f, 0.6f), 0.5f);
            Material glass = Art.Mat(new Color(0.1f, 0.13f, 0.18f), 0.9f, 0.4f);
            Material tire = Art.Mat(new Color(0.07f, 0.07f, 0.08f), 0.2f);
            Art.Part(t, Art.Cube, white, new Vector3(0f, 0.65f, 0f), new Vector3(1.9f, 0.7f, 4.2f));
            Art.Part(t, Art.Cube, glass, new Vector3(0f, 1.2f, -0.2f), new Vector3(1.7f, 0.55f, 2.1f), default, false);
            Art.Part(t, Art.Cube, white, new Vector3(0f, 1.49f, -0.3f), new Vector3(1.66f, 0.06f, 1.6f), default, false);
            Art.Part(t, Art.Cube, blue, new Vector3(0f, 0.62f, 0f), new Vector3(1.94f, 0.18f, 4.22f), default, false);
            Transform a = Art.Part(t, Art.Cube, Art.Glow(new Color(1f, 0.15f, 0.15f), new Color(4f, 0.4f, 0.3f), 0.5f), new Vector3(-0.35f, 1.6f, -0.2f), new Vector3(0.6f, 0.2f, 0.3f), default, false).transform;
            Transform b = Art.Part(t, Art.Cube, Art.Glow(new Color(0.2f, 0.4f, 1f), new Color(0.4f, 0.8f, 4f), 0.5f), new Vector3(0.35f, 1.6f, -0.2f), new Vector3(0.6f, 0.2f, 0.3f), default, false).transform;
            for (int i = 0; i < 4; i++)
                Art.Part(t, Art.Cylinder, tire, new Vector3(i % 2 == 0 ? -0.95f : 0.95f, 0.36f, i < 2 ? 1.35f : -1.35f), new Vector3(0.72f, 0.12f, 0.72f), new Vector3(0f, 0f, 90f), false);
            pendingLights = (a, b);
            return t;
        }

        (Transform, Transform) pendingLights;

        static readonly Color[] CarColors =
        {
            new Color(0.85f, 0.2f, 0.18f), new Color(0.98f, 0.78f, 0.15f), new Color(0.2f, 0.55f, 0.85f),
            new Color(0.25f, 0.7f, 0.4f), new Color(0.92f, 0.92f, 0.9f), new Color(0.15f, 0.15f, 0.18f), new Color(0.95f, 0.5f, 0.15f)
        };

        Transform CivCar()
        {
            Transform t = new GameObject("car").transform;
            Material body = Art.Mat(CarColors[Random.Range(0, CarColors.Length)], 0.6f, 0.15f);
            Material glass = Art.Mat(new Color(0.1f, 0.13f, 0.18f), 0.9f, 0.4f);
            Material tire = Art.Mat(new Color(0.07f, 0.07f, 0.08f), 0.2f);
            bool truck = Random.value < 0.2f;
            if (truck)
            {
                Art.Part(t, Art.Cube, body, new Vector3(0f, 0.9f, 1.4f), new Vector3(2f, 1.3f, 1.6f));
                Art.Part(t, Art.Cube, Art.Mat(new Color(0.9f, 0.9f, 0.88f), 0.3f), new Vector3(0f, 1.3f, -0.9f), new Vector3(2.1f, 2.1f, 3.4f));
                Art.Part(t, Art.Cube, glass, new Vector3(0f, 1.25f, 2.2f), new Vector3(1.8f, 0.6f, 0.1f), default, false);
            }
            else
            {
                Art.Part(t, Art.Cube, body, new Vector3(0f, 0.6f, 0f), new Vector3(1.8f, 0.65f, 3.8f));
                Art.Part(t, Art.Cube, glass, new Vector3(0f, 1.1f, -0.25f), new Vector3(1.6f, 0.55f, 1.9f), default, false);
                Art.Part(t, Art.Cube, body, new Vector3(0f, 1.4f, -0.3f), new Vector3(1.58f, 0.06f, 1.5f), default, false);
            }
            for (int i = 0; i < 4; i++)
                Art.Part(t, Art.Cylinder, tire, new Vector3(i % 2 == 0 ? -0.9f : 0.9f, 0.34f, i < 2 ? 1.25f : -1.25f), new Vector3(0.68f, 0.12f, 0.68f), new Vector3(0f, 0f, 90f), false);
            pendingLights = (null, null);
            return t;
        }

        // ------------------------------------------------------------------ boutiques

        void TickShops(float dt)
        {
            if (city == null) return;
            foreach (HeistCity.Shop s in city.shops)
            {
                if (s.robbed && st == St.Play && stT - s.robbedAt > 35f && (Flat(s.door) - Flat(heroPos)).magnitude > 12f)
                {
                    // la boutique a remis de l'argent en caisse
                    s.robbed = false;
                    s.progress = 0f;
                    s.ring.gameObject.SetActive(true);
                    s.sign.gameObject.SetActive(true);
                    if (s.front != null) s.front.sharedMaterial = s.frontMat;
                }
                if (!s.robbed)
                {
                    float k = 1f + 0.08f * Mathf.Sin(stT * 5f + s.door.x);
                    s.ring.localScale = new Vector3(3.4f * k, 3.4f * k, 1f);
                    s.sign.rotation = Quaternion.Euler(0f, stT * 120f, 0f);
                    s.sign.position = s.door + Vector3.up * (3.2f + 0.2f * Mathf.Sin(stT * 3f + s.door.z));
                    if (s != robbing && s.progress > 0f) { s.progress = Mathf.Max(0f, s.progress - dt * 0.15f); }
                    if (s != robbing && s.progress <= 0f) s.alarm.gameObject.SetActive(false);
                }
                if (s.alarm.gameObject.activeSelf)
                {
                    float f = Mathf.Repeat(stT * 3f, 1f) < 0.5f ? 1f : 0.4f;
                    s.alarm.localScale = Vector3.one * 0.45f * f;
                }
            }
            // le van de la planque respire quand on porte du butin
            if (city.van != null)
            {
                float p = carried > 0 ? 1f + 0.03f * Mathf.Sin(stT * 8f) : 1f;
                city.van.localScale = new Vector3(p, p, p);
            }
        }

        // ------------------------------------------------------------------ fin de partie

        float endT;

        void Win()
        {
            st = St.Win;
            endT = 0f;
            stickOn = false;
            hud.Toast(L("GETAWAY!", "ON SE CASSE !"));
            Snd.Play(Snd.I.perfect, 1f, 0f);
            hero.SetVisible(false);
            foreach (Agent m in crew) m.rig.SetVisible(false);
            arrow.gameObject.SetActive(false);
            foreach (Agent c in cops) { c.vel = Vector3.zero; }
        }

        void WinTick(float dt)
        {
            endT += dt;
            // le van démarre en trombe
            if (endT > 0.5f)
            {
                float sp = Mathf.Min(22f, (endT - 0.5f) * 18f);
                city.van.position += Vector3.forward * sp * dt;
                if (Mathf.Repeat(endT, 0.1f) < dt) Fx.Dust(city.van.position - Vector3.forward * 2.6f);
            }
            if (endT > 2.4f) ShowResult(true);
        }

        void Lose()
        {
            st = St.Lose;
            endT = 0f;
            stickOn = false;
            hud.Toast(L("BUSTED!", "ARRÊTÉ !"));
            hero.KO(new Vector3(0f, 3f, 0f), new Vector3(4f, 0f, 2f));
            Snd.Play(Snd.I.crash, 0.8f);
            arrow.gameObject.SetActive(false);
        }

        void LoseTick(float dt)
        {
            endT += dt;
            if (endT > 1.8f) ShowResult(false);
        }

        void ShowResult(bool won)
        {
            st = St.Result;
            int reward = won ? banked + 50 * level : Mathf.RoundToInt(banked * 0.5f);
            PlayerPrefs.SetInt(KCash, PlayerPrefs.GetInt(KCash, 0) + reward);
            if (won) { level++; PlayerPrefs.SetInt(KLevel, level); }
            PlayerPrefs.Save();
            hud.HeistResult(won, reward, PlayerPrefs.GetInt(KCash, 0));
            Perf.Note((won ? "gagné" : "perdu") + " : butin " + banked + "/" + target + ", gain " + reward);
        }
    }
}
