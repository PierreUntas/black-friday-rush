using System.Collections.Generic;
using UnityEngine;

namespace SamediRouge
{
    /// <summary>
    /// Allée de centre commercial générée par tronçons de 36 m devant le joueur et détruite derrière :
    /// carrelage brillant, boutiques éclairées sur deux niveaux, verrière, colonnes, plantes, bancs, banderoles.
    /// </summary>
    public partial class Street : MonoBehaviour
    {
        public const float Chunk = 36f, Lane = 2.4f, ShopX = 7.6f, UpperX = 10.4f, Level2 = 5f, Ceiling = 11.5f;

        class ChunkData { public GameObject go; public float z; public List<Walker> people = new List<Walker>(); }
        class Walker { public Rig rig; public float dir, v; }
        readonly List<ChunkData> chunks = new List<ChunkData>();
        float nextZ;
        int counter;
        /// <summary>Thème du décor : 0 galerie marchande, 1 rue commerçante pillée, 2 gare, 3 docks, 4 quartier chic de nuit (StreetThemes.cs).</summary>
        public int Theme { get; private set; }

        /// <summary>Change de décor : les tronçons sont reconstruits devant le joueur.</summary>
        public void SetTheme(int theme, float pz)
        {
            if (theme == Theme) return;
            Theme = theme;
            Clear(pz);
        }

        /// <summary>
        /// Gyrophare d'alarme (rouge) au mur : dôme lumineux et faisceaux qui balaient. Renvoie sa racine
        /// (le jeu peut le repeindre en vert quand le magasin est sauvé).
        /// </summary>
        public static Transform Beacon(Transform parent, Vector3 pos, Color c)
        {
            Transform b = Art.Node(parent, "beacon", pos);
            Art.Part(b, Art.Cylinder, Art.Mat(new Color(0.15f, 0.15f, 0.17f), 0.6f), Vector3.zero, new Vector3(0.36f, 0.06f, 0.36f), default, false);
            Art.Part(b, Art.Sphere, Art.Glow(c, c * 3f, 0.6f), new Vector3(0f, 0.12f, 0f), new Vector3(0.3f, 0.3f, 0.3f), default, false);
            Transform spin = Art.Node(b, "beams", new Vector3(0f, 0.12f, 0f));
            Material beam = Art.Particle(Art.SoftDot, new Color(c.r, c.g, c.b, 0.45f));
            Art.Part(spin, Art.Quad, beam, new Vector3(1.4f, 0f, 0f), new Vector3(2.8f, 0.7f, 1f), new Vector3(0f, 0f, 0f), false);
            Art.Part(spin, Art.Quad, beam, new Vector3(-1.4f, 0f, 0f), new Vector3(2.8f, 0.7f, 1f), new Vector3(0f, 180f, 0f), false);
            Art.Part(spin, Art.Quad, beam, new Vector3(1.4f, 0f, 0f), new Vector3(2.8f, 0.7f, 1f), new Vector3(90f, 0f, 0f), false);
            Art.Part(spin, Art.Quad, beam, new Vector3(-1.4f, 0f, 0f), new Vector3(2.8f, 0.7f, 1f), new Vector3(90f, 180f, 0f), false);
            spin.gameObject.AddComponent<Spinner>().speed = 420f;
            return b;
        }

        Material floor, slab, rail, glassRail, column, ceiling, skylight, frameMat, planter, leaves, trunk, bench, banner, bannerText, balloon1, balloon2, balloon3, wall;

        public void Init(float startZ)
        {
            floor = Art.Tex(Art.Tiles, new Vector2(6.33f, 15f), 0.82f);
            slab = Art.Mat(new Color(0.93f, 0.92f, 0.9f), 0.5f);
            rail = Art.Mat(new Color(0.75f, 0.77f, 0.8f), 0.85f, 0.9f);
            glassRail = Art.Mat(new Color(0.72f, 0.86f, 0.92f), 0.95f, 0.1f);
            column = Art.Mat(new Color(0.96f, 0.95f, 0.93f), 0.6f);
            ceiling = Art.Mat(new Color(0.9f, 0.9f, 0.9f), 0.2f);
            skylight = Art.Glow(new Color(0.9f, 0.95f, 1f), new Color(1.3f, 1.4f, 1.6f), 0.5f);
            frameMat = Art.Mat(new Color(0.55f, 0.57f, 0.6f), 0.7f, 0.8f);
            planter = Art.Mat(new Color(0.85f, 0.83f, 0.8f), 0.4f);
            leaves = Art.Mat(new Color(0.22f, 0.55f, 0.28f), 0.25f);
            trunk = Art.Mat(new Color(0.45f, 0.33f, 0.22f), 0.2f);
            bench = Art.Mat(new Color(0.55f, 0.38f, 0.22f), 0.45f);
            banner = Art.Glow(new Color(0.92f, 0.12f, 0.16f), new Color(0.6f, 0.05f, 0.06f), 0.3f);
            bannerText = Art.Glow(Color.white, new Color(1.1f, 1.1f, 1.1f), 0.3f);
            balloon1 = Art.Mat(new Color(0.95f, 0.25f, 0.3f), 0.8f);
            balloon2 = Art.Mat(new Color(0.98f, 0.8f, 0.15f), 0.8f);
            balloon3 = Art.Mat(new Color(0.25f, 0.55f, 0.98f), 0.8f);
            wall = Art.Mat(new Color(0.88f, 0.87f, 0.85f), 0.3f);
            InitThemes();
            nextZ = Mathf.Floor(startZ / Chunk) * Chunk - Chunk;
        }

        /// <summary>Teinte du carrelage (chaque rue du quartier a sa couleur).</summary>
        /// <summary>Sol illustré : légère teinte de la rue et un peu assombri pour ne plus éblouir.</summary>
        static Color FloorShade(Color c)
        {
            Color k = Color.Lerp(Color.white, c, 0.3f) * 0.74f;
            k.a = 1f;
            return k;
        }

        public void SetTint(Color c)
        {
            tint = c;
            foreach (Material m in floorArt.Values) if (m != null) m.color = FloorShade(c);   // sol illustré : teinte légère
            if (floor != null) floor.color = c;
            if (floorDock != null) floorDock.color = c;
            if (floorLux != null) floorLux.color = c;
        }

        public void Tick(float pz)
        {
            // les clients du décor se promènent (seulement ceux proches de la caméra sont animés)
            float dt = Time.deltaTime;
            foreach (ChunkData c in chunks)
            {
                if (c.z > pz + 90f) continue;
                foreach (Walker w in c.people)
                {
                    if (w.v > 0f)
                    {
                        Vector3 p = w.rig.root.localPosition;
                        p.z += w.dir * w.v * dt;
                        if (p.z < -4f || p.z > Chunk + 4f) w.dir = -w.dir;
                        w.rig.root.localPosition = p;
                        w.rig.root.localRotation = Quaternion.Euler(0f, w.dir > 0f ? 0f : 180f, 0f);
                    }
                    w.rig.Tick(dt);
                }
            }
            while (nextZ < pz + 170f)
            {
                BuildChunk(nextZ);
                nextZ += Chunk;
            }
            for (int i = chunks.Count - 1; i >= 0; i--)
            {
                if (chunks[i].z + Chunk < pz - 30f)
                {
                    Destroy(chunks[i].go);
                    chunks.RemoveAt(i);
                }
            }
        }

        public void Clear(float pz)
        {
            foreach (ChunkData c in chunks) Destroy(c.go);
            chunks.Clear();
            nextZ = Mathf.Floor(pz / Chunk) * Chunk - Chunk;
        }

        Color tint = Color.white;
        readonly Dictionary<int, Material> floorArt = new Dictionary<int, Material>();
        static readonly string[] FloorNames = { "floor_mall", "floor_looted", "floor_station", "floor_docks", "floor_uptown" };
        /// <summary>Taille (m) couverte par une image de sol, par thème.</summary>
        static readonly float[] FloorSize = { 7.2f, 6f, 7.2f, 9f, 7.2f };

        /// <summary>Sol illustré du thème (Resources/SamediRouge/Art/floor_*.png, répété en mosaïque), ou null.</summary>
        Material FloorArt()
        {
            Material m;
            if (floorArt.TryGetValue(Theme, out m)) return m;
            m = null;
            int i = Mathf.Clamp(Theme, 0, FloorNames.Length - 1);
            Texture2D tx = Resources.Load<Texture2D>("SamediRouge/Art/" + FloorNames[i]);
            if (tx != null)
            {
                tx.wrapMode = TextureWrapMode.Repeat;
                float k = FloorSize[i];
                m = Art.Tex(tx, new Vector2(ShopX * 2f / k, Chunk / k), Theme == 4 ? 0.55f : Theme == 0 ? 0.35f : 0.25f);
                m.color = FloorShade(tint);
            }
            floorArt[Theme] = m;
            return m;
        }

        void BuildChunk(float z0)
        {
            counter++;
            var go = new GameObject("Chunk" + counter);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, z0);
            Transform t = go.transform;
            var cd = new ChunkData { go = go, z = z0 };
            chunks.Add(cd);
            float mid = Chunk * 0.5f;
            if (Theme >= 2) { BuildThemed(t, cd); return; }

            // sol carrelé, verrière
            MeshRenderer fl = Art.Decal(t, FloorArt() ?? floor, new Vector3(0f, 0f, mid), new Vector2(ShopX * 2f, Chunk));
            fl.receiveShadows = true;
            Art.Part(t, Art.Cube, ceiling, new Vector3(0f, Ceiling + 0.2f, mid), new Vector3(UpperX * 2f + 2f, 0.4f, Chunk), default, false);
            for (float z = 3f; z < Chunk; z += 6f)
                Art.Part(t, Art.Cube, skylight, new Vector3(0f, Ceiling, z), new Vector3(9f, 0.05f, 3.6f), default, false);

            // collisions des corps projetés et des débris
            var col = Art.Node(t, "floor", new Vector3(0f, -0.25f, mid)).gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(30f, 0.5f, Chunk);
            for (int s = -1; s <= 1; s += 2)
            {
                var w = Art.Node(t, "wall", new Vector3(s * (ShopX + 0.5f), 6f, mid)).gameObject.AddComponent<BoxCollider>();
                w.size = new Vector3(1f, 12f, Chunk);
            }

            float[] propZ = new float[2];
            for (int s = -1; s <= 1; s += 2)
            {
                // boutiques : 4 par niveau et par côté
                for (int k = 0; k < 4; k++)
                {
                    float zc = 4.5f + k * 9f;
                    Store(t, s, zc, ShopX, 0f, Random.Range(0, Art.StoreCount));
                    Store(t, s, zc, UpperX, Level2, Random.Range(0, Art.StoreCount));
                }
                // coursive de l'étage : dalle, garde-corps vitré, main courante
                Art.Part(t, Art.Cube, slab, new Vector3(s * (ShopX + 1.4f), Level2 - 0.2f, mid), new Vector3(2.8f, 0.4f, Chunk));
                Art.Part(t, Art.Cube, glassRail, new Vector3(s * (ShopX + 0.1f), Level2 + 0.5f, mid), new Vector3(0.04f, 1f, Chunk), default, false);
                Art.Part(t, Art.Cube, rail, new Vector3(s * (ShopX + 0.1f), Level2 + 1.02f, mid), new Vector3(0.1f, 0.06f, Chunk), default, false);
                Art.Part(t, Art.Cube, wall, new Vector3(s * (UpperX + 0.4f), Ceiling - 1f, mid), new Vector3(0.5f, 2f, Chunk));
                // colonnes
                for (float z = 0f; z < Chunk; z += 18f)
                    Art.Part(t, Art.Cylinder, column, new Vector3(s * (ShopX - 0.2f), Ceiling * 0.5f, z + 1f), new Vector3(0.6f, Ceiling * 0.5f, 0.6f));
                // plantes et bancs le long des vitrines
                float r = Random.value;
                float zp = Random.Range(4f, Chunk - 4f);
                propZ[s < 0 ? 0 : 1] = zp;
                if (Tuning.Arcade) { if (r < 0.3f) Palm(t, s, zp); }   // arcade : décor plus calme
                else if (r < 0.45f) Palm(t, s, zp);
                else if (r < 0.75f) Bench(t, s, zp);
            }

            // vie du centre commercial : promeneurs à l'étage, lèche-vitrines au rez-de-chaussée
            int nUp = Tuning.Arcade ? 0 : Random.Range(0, 3);   // arcade : pas de promeneurs (lisibilité, et chaque personnage coûte)   // promeneurs de l'étage : peu nombreux (chaque personnage coûte au téléphone)
            for (int i = 0; i < nUp; i++)
            {
                int s = Random.value < 0.5f ? -1 : 1;
                float dir = Random.value < 0.5f ? -1f : 1f;
                Rig r = Rig.Build(Kind.Shopper, t, new Vector3(s * (ShopX + Random.Range(0.7f, 2f)), Level2, Random.Range(2f, Chunk - 2f)), dir > 0f ? 0f : 180f, Random.Range(0, 12), false);
                r.anim = Anim.Walk;
                r.speed = Random.Range(1.1f, 1.6f);
                cd.people.Add(new Walker { rig = r, dir = dir, v = r.speed });
            }
            if (!Tuning.Arcade && Random.value < 0.6f)
            {
                int s = Random.value < 0.5f ? -1 : 1;
                float zs = Random.Range(3f, Chunk - 3f);
                if (Mathf.Abs(zs - propZ[s < 0 ? 0 : 1]) < 2.5f) zs = Mathf.Repeat(zs + Chunk * 0.5f, Chunk - 6f) + 3f;
                Rig r = Rig.Build(Kind.Shopper, t, new Vector3(s * (ShopX - 0.9f), 0f, zs), s < 0 ? -90f : 90f, Random.Range(0, 12));
                r.anim = Random.value < 0.5f ? Anim.Idle : Anim.Stand;
                cd.people.Add(new Walker { rig = r, dir = 0f, v = 0f });
            }

            // map 2 : objets renversés, fumée légère, un gyrophare qui éclaire en rouge
            if (Theme == 1 && !Tuning.Arcade)
            {
                for (int s = -1; s <= 1; s += 2)
                    for (int k = 0; k < 2; k++) LootedProps(t, s, Random.Range(2f, Chunk - 2f));
                if (Random.value < 0.5f) Fx.Gas(t, new Vector3((Random.value < 0.5f ? -1f : 1f) * (ShopX - 1.5f), 0.6f, Random.Range(4f, Chunk - 4f)));
                if (Random.value < 0.6f)
                {
                    var l = Art.Node(t, "alarmLight", new Vector3((Random.value < 0.5f ? -1f : 1f) * (ShopX - 1f), 4.5f, Random.Range(4f, Chunk - 4f))).gameObject.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = new Color(1f, 0.12f, 0.08f);
                    l.range = 9f;
                    l.shadows = LightShadows.None;
                    l.gameObject.AddComponent<Flicker>().baseIntensity = 2.2f;
                }
            }

            // banderole SALE suspendue (décor, au-dessus des couloirs)
            if (Theme == 0 && Random.value < 0.3f)
            {
                float zb = Random.Range(6f, Chunk - 6f);
                Art.Part(t, Art.Cube, banner, new Vector3(0f, 10.6f, zb), new Vector3(8f, 1.6f, 0.06f), default, false);
                Art.Part(t, Art.Cube, bannerText, new Vector3(0f, 10.6f, zb - 0.04f), new Vector3(5f, 0.55f, 0.02f), default, false);
                Art.Part(t, Art.Cube, frameMat, new Vector3(-3.8f, 12.4f, zb), new Vector3(0.03f, 3f, 0.03f), default, false);
                Art.Part(t, Art.Cube, frameMat, new Vector3(3.8f, 12.4f, zb), new Vector3(0.03f, 3f, 0.03f), default, false);
            }

            // grappe de ballons
            if (Theme == 0 && !Tuning.Arcade && Random.value < 0.4f)
            {
                int s = Random.value < 0.5f ? -1 : 1;
                Vector3 b = new Vector3(s * (ShopX - 1.2f), 0f, Random.Range(3f, Chunk - 3f));
                if (PropH(t, "dec_ballons", 3.4f, b, Random.Range(0f, 360f))) goto balloonsDone;
                Material[] bm = { balloon1, balloon2, balloon3 };
                for (int i = 0; i < 5; i++)
                {
                    Vector3 p = b + new Vector3(Random.Range(-0.4f, 0.4f), 2.6f + Random.Range(0f, 0.8f), Random.Range(-0.4f, 0.4f));
                    Art.Part(t, Art.Sphere, bm[i % 3], p, new Vector3(0.45f, 0.55f, 0.45f), default, false);
                    Art.Part(t, Art.Cube, frameMat, (p + b) * 0.5f + new Vector3(0f, 0.2f, 0f), new Vector3(0.01f, p.y - 0.4f, 0.01f), default, false);
                }
                Art.Part(t, Art.Cube, planter, b + new Vector3(0f, 0.15f, 0f), new Vector3(0.4f, 0.3f, 0.4f));
            }
            balloonsDone:;
        }

        static readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Texture2D>> shopArt =
            new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Texture2D>>();
        /// <summary>Devantures illustrées du quartier : Resources/SamediRouge/Art/shop_N (galerie, rue pillée) ou station_N (gare).
        /// Sans images pour le quartier : null (devantures dessinées en code).</summary>
        Texture2D ShopArt(int v)
        {
            string prefix = Theme == 2 ? "station_" : Theme == 4 ? "uptown_" : "shop_";
            System.Collections.Generic.List<Texture2D> list;
            if (!shopArt.TryGetValue(prefix, out list))
            {
                list = new System.Collections.Generic.List<Texture2D>();
                for (int i = 1; i <= 12; i++)
                {
                    Texture2D t = Resources.Load<Texture2D>("SamediRouge/Art/" + prefix + i);
                    if (t != null) list.Add(t);
                }
                shopArt[prefix] = list;
            }
            return list.Count > 0 ? list[((v % list.Count) + list.Count) % list.Count] : null;
        }

        void Store(Transform t, int s, float zc, float x, float y, int v)
        {
            Art.Facade f = Art.GetStore(v);
            bool looted = Theme == 1;
            // map 2 : vitrines plus sombres (lumières en partie coupées)
            Material m;
            Texture2D shop = ShopArt(v);
            if (shop != null)
                // devanture illustrée : elle s'éclaire elle-même (texture en émission), moins fort dans la rue pillée
                m = Art.Tex(shop, Vector2.one, 0.5f, shop, looted ? new Color(0.35f, 0.32f, 0.3f) : new Color(0.55f, 0.55f, 0.55f));
            else
                m = Art.Tex(f.albedo, Vector2.one, 0.6f, f.emission, looted ? new Color(0.6f, 0.55f, 0.5f) : new Color(1.25f, 1.25f, 1.25f));
            float yaw = s < 0 ? -90f : 90f;
            Art.Part(t, Art.Quad, m, new Vector3(s * (x - 0.01f), y + 2.4f, zc), new Vector3(9f, 4.6f, 1f), new Vector3(0f, yaw, 0f), false);
            Art.Part(t, Art.Cube, wall, new Vector3(s * (x + 0.3f), y + 2.4f, zc), new Vector3(0.6f, 4.8f, 9f));
            Art.Part(t, Art.Cube, frameMat, new Vector3(s * (x - 0.05f), y + 2.4f, zc - 4.45f), new Vector3(0.12f, 4.8f, 0.12f), default, false);
            if (looted && y < 0.1f) LootedFront(t, s, zc, x, yaw);
        }

        /// <summary>Devanture pillée : vitrine fissurée, rideau métallique arraché, éclats au sol, parfois un gyrophare.</summary>
        void LootedFront(Transform t, int s, float zc, float x, float yaw)
        {
            Material crack = Art.Particle(Art.Cracks, new Color(0.9f, 0.95f, 1f, 0.85f));
            float cz = zc + Random.Range(-2.5f, 2.5f);
            Art.Part(t, Art.Quad, crack, new Vector3(s * (x - 0.03f), Random.Range(1.2f, 2.6f), cz), new Vector3(3.2f, 3.2f, 1f), new Vector3(0f, yaw, Random.Range(0f, 360f)), false);
            if (Random.value < 0.5f)
                Art.Part(t, Art.Quad, crack, new Vector3(s * (x - 0.03f), Random.Range(1.4f, 3f), cz + (Random.value < 0.5f ? -3f : 3f)), new Vector3(2.4f, 2.4f, 1f), new Vector3(0f, yaw, Random.Range(0f, 360f)), false);
            // éclats de verre devant la vitrine
            Art.Decal(t, Art.Particle(Art.Shards, new Color(0.85f, 0.9f, 1f, 0.9f)), new Vector3(s * (x - 1.1f), 0.02f, cz), new Vector2(2.2f, 3.2f));
            // rideau métallique à moitié descendu, tordu
            if (Random.value < 0.55f && PropH(t, "dec_rideau", 4.3f, new Vector3(s * (x - 0.2f), 0f, zc + Random.Range(-1.5f, 1.5f)), s < 0 ? 90f : -90f)) { }
            else if (Random.value < 0.55f)
            {
                Material shutter = Art.Mat(new Color(0.48f, 0.5f, 0.53f), 0.55f, 0.6f);
                Transform sh = Art.Node(t, "shutter", new Vector3(s * (x - 0.12f), 3.9f, zc + Random.Range(-1.5f, 1.5f)));
                sh.localRotation = Quaternion.Euler(0f, 0f, s * Random.Range(-8f, 8f));
                for (int k = 0; k < 6; k++)
                    Art.Part(sh, Art.Cube, shutter, new Vector3(0f, -k * 0.22f, 0f), new Vector3(0.06f, 0.2f, 4.2f - (k == 5 ? 1.6f : 0f)));
            }
            // gyrophare d'alarme rouge au-dessus de la porte
            if (Random.value < 0.4f)
            {
                Transform b = Beacon(t, new Vector3(s * (x - 0.25f), 4.95f, zc), new Color(1f, 0.1f, 0.08f));
                b.localRotation = Quaternion.Euler(0f, 0f, s * 90f);
            }
        }

        /// <summary>Objets renversés sur le trottoir : mannequin, portant de vêtements, cartons éventrés, télé cassée.</summary>
        void LootedProps(Transform t, int s, float z)
        {
            float x = s * (ShopX - Random.Range(1.2f, 2.2f));
            float r = Random.value;
            if (r < 0.3f)
            {
                Material skin = Art.Mat(new Color(0.92f, 0.88f, 0.82f), 0.6f);
                Transform mq = Art.Node(t, "mannequin", new Vector3(x, 0.18f, z));
                mq.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 90f);
                Art.Part(mq, Art.Capsule, skin, Vector3.zero, new Vector3(0.32f, 0.55f, 0.24f));
                Art.Part(mq, Art.Sphere, skin, new Vector3(0f, 0.72f, 0f), Vector3.one * 0.26f);
                Art.Part(mq, Art.Capsule, skin, new Vector3(0.12f, -0.75f, 0f), new Vector3(0.12f, 0.4f, 0.12f));
            }
            else if (r < 0.55f)
            {
                Material metal = Art.Mat(new Color(0.7f, 0.72f, 0.75f), 0.8f, 0.8f);
                Transform rack = Art.Node(t, "rack", new Vector3(x, 0.08f, z));
                rack.localRotation = Quaternion.Euler(0f, Random.Range(-30f, 30f), 0f);
                Art.Part(rack, Art.Cylinder, metal, new Vector3(0f, 0.05f, 0f), new Vector3(0.05f, 0.9f, 0.05f), new Vector3(90f, 0f, 0f));
                Color[] cl = { new Color(0.85f, 0.2f, 0.25f), new Color(0.2f, 0.45f, 0.85f), new Color(0.95f, 0.8f, 0.25f), new Color(0.3f, 0.7f, 0.4f) };
                for (int k = 0; k < 5; k++)
                    Art.Part(rack, Art.Cube, Art.Mat(cl[k % 4], 0.2f), new Vector3(Random.Range(-0.4f, 0.4f), 0.08f, -0.7f + k * 0.35f), new Vector3(0.6f, 0.06f, 0.3f), new Vector3(0f, Random.Range(-40f, 40f), 0f));
            }
            else if (r < 0.8f)
            {
                Material card = Art.Mat(new Color(0.72f, 0.55f, 0.36f), 0.15f);
                for (int k = 0; k < 4; k++)
                    Art.Part(t, Art.Cube, card, new Vector3(x + Random.Range(-0.6f, 0.6f), 0.15f, z + Random.Range(-0.8f, 0.8f)), new Vector3(0.5f, 0.3f, 0.4f), new Vector3(Random.Range(-20f, 20f), Random.Range(0f, 90f), Random.Range(-15f, 15f)));
            }
            else
            {
                Transform tv = Art.Node(t, "tv", new Vector3(x, 0.32f, z));
                tv.localRotation = Quaternion.Euler(-70f, Random.Range(0f, 360f), 0f);
                Art.Part(tv, Art.Cube, Art.Mat(new Color(0.08f, 0.08f, 0.09f), 0.6f), Vector3.zero, new Vector3(1.2f, 0.72f, 0.08f));
                Art.Part(tv, Art.Quad, Art.Particle(Art.Cracks, new Color(0.7f, 0.85f, 1f, 0.9f)), new Vector3(0f, 0f, -0.045f), new Vector3(1.1f, 0.65f, 1f), new Vector3(0f, 180f, 0f), false);
            }
        }

        /// <summary>Accessoire de décor 3D (Meshy) posé au sol : taille = plus grande dimension au sol. Faux si le modèle manque.</summary>
        public static bool Prop(Transform t, string name, float size, Vector3 pos, float yaw)
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
            m.localPosition = pos - q * (new Vector3(b.center.x, b.min.y, b.center.z) * k);
            return true;
        }

        /// <summary>Comme Prop, mais la taille donnée est la HAUTEUR (lampadaires, réverbères).</summary>
        public static bool PropH(Transform t, string name, float height, Vector3 pos, float yaw)
        {
            if (!GlbModel.Exists(name)) return false;
            Transform m = GlbModel.Spawn(name, t);
            Bounds b = new Bounds();
            bool first = true;
            foreach (MeshFilter f in m.GetComponentsInChildren<MeshFilter>())
            {
                if (first) { b = f.sharedMesh.bounds; first = false; } else b.Encapsulate(f.sharedMesh.bounds);
            }
            float k = height / Mathf.Max(0.001f, b.size.y);
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            m.localScale = Vector3.one * k;
            m.localRotation = q;
            m.localPosition = pos - q * (new Vector3(b.center.x, b.min.y, b.center.z) * k);
            return true;
        }

        void Palm(Transform t, int s, float z)
        {
            float x = s * (ShopX - 1.3f);
            if (Prop(t, "dec_plante", 1.7f, new Vector3(x, 0f, z), Random.Range(0f, 360f))) return;
            Art.Part(t, Art.Cube, planter, new Vector3(x, 0.4f, z), new Vector3(1.2f, 0.8f, 1.2f));
            Art.Part(t, Art.Cylinder, trunk, new Vector3(x, 1.9f, z), new Vector3(0.18f, 1.2f, 0.18f));
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f;
                Art.Part(t, Art.Cube, leaves, new Vector3(x + Mathf.Sin(a * Mathf.Deg2Rad) * 0.6f, 3.1f, z + Mathf.Cos(a * Mathf.Deg2Rad) * 0.6f),
                         new Vector3(0.35f, 0.05f, 1.3f), new Vector3(25f, a, 0f), false);
            }
            Art.Part(t, Art.Sphere, leaves, new Vector3(x, 3.15f, z), Vector3.one * 0.45f);
        }

        void Bench(Transform t, int s, float z)
        {
            float x = s * (ShopX - 1.1f);
            if (Prop(t, "dec_banc", 2.2f, new Vector3(x, 0f, z), 90f)) return;
            Art.Part(t, Art.Cube, bench, new Vector3(x, 0.45f, z), new Vector3(0.6f, 0.1f, 2.2f));
            Art.Part(t, Art.Cube, frameMat, new Vector3(x, 0.22f, z - 0.9f), new Vector3(0.5f, 0.44f, 0.08f), default, false);
            Art.Part(t, Art.Cube, frameMat, new Vector3(x, 0.22f, z + 0.9f), new Vector3(0.5f, 0.44f, 0.08f), default, false);
        }
    }
}
