using System.Collections.Generic;
using UnityEngine;

namespace SamediRouge
{
    /// <summary>
    /// V3 « Braquage » : petite ville vue de dessus, générée. Grille de pâtés de maisons séparés par des rues ;
    /// immeubles à façades, boutiques à braquer (vitrine + cercle devant la porte), la planque (van), des recrues.
    /// Les immeubles sont des rectangles pleins (collisions simples, on glisse le long des murs).
    /// </summary>
    public class HeistCity
    {
        public const int N = 4;                 // pâtés par côté
        public const float Block = 22f, Road = 10f, Pitch = Block + Road;
        public const float Size = N * Pitch;    // routes extérieures comprises : de 0 à Size
        const float Walk = 2.2f;                // largeur du trottoir autour des immeubles

        public class Shop
        {
            public Vector3 door;      // centre du cercle de braquage (sur le trottoir)
            public Vector3 face;      // direction vers la rue
            public int value;
            public bool robbed;
            public float progress;
            public Transform ring, sign, alarm;
            public MeshRenderer front;
            public Material frontMat;
            public float robbedAt;
        }

        public readonly List<Rect> solids = new List<Rect>();
        public readonly List<Shop> shops = new List<Shop>();
        public readonly List<Vector3> recruitSpots = new List<Vector3>();
        public readonly List<Vector3> walkSpots = new List<Vector3>();
        public Vector3 vanPos, vanDoor;
        public Transform root, van;

        static Texture2D ringTex;

        public static Texture2D RingTex
        {
            get
            {
                if (ringTex != null) return ringTex;
                const int n = 128;
                ringTex = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "heist_ring", wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.86f) / 0.1f);
                    float fill = r < 0.8f ? 0.28f : 0f;
                    float a = Mathf.Max(ring, fill);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
                ringTex.SetPixels32(px);
                ringTex.Apply(true, true);
                return ringTex;
            }
        }

        /// <summary>Centre de la rue (intersection) i, j.</summary>
        public static Vector3 Node(int i, int j) => new Vector3(i * Pitch, 0f, j * Pitch);

        public HeistCity(Transform parent, int level, int seed)
        {
            Random.State keep = Random.state;
            Random.InitState(seed);
            root = Art.Node(parent, "City", Vector3.zero);
            Ground();
            var faces = new List<(int bi, int bj, int side)>();
            for (int bi = 0; bi < N; bi++)
                for (int bj = 0; bj < N; bj++)
                {
                    BlockAt(bi, bj);
                    for (int s = 0; s < 4; s++) faces.Add((bi, bj, s));
                }
            // la planque : un van garé au bord du pâté central-sud
            vanPos = new Vector3(2f * Pitch + 3.9f, 0f, 1.5f * Pitch);   // garé le long du trottoir, au milieu de la ville
            vanDoor = vanPos + new Vector3(-2.9f, 0f, 0f);
            van = Van(vanPos);

            // boutiques : des faces de pâtés tirées au hasard, pas trop près de la planque
            Shuffle(faces);
            int want = 7;
            foreach (var f in faces)
            {
                if (shops.Count >= want) break;
                Vector3 door = DoorOf(f.bi, f.bj, f.side, out Vector3 face);
                if ((door - vanPos).magnitude < 14f) continue;
                bool near = false;
                foreach (Shop o in shops) if ((o.door - door).magnitude < 16f) { near = true; break; }
                if (near) continue;
                shops.Add(MakeShop(f.bi, f.bj, f.side, door, face, level));
            }
            // recrues et promeneurs : coins de trottoir
            for (int bi = 0; bi < N; bi++)
                for (int bj = 0; bj < N; bj++)
                {
                    Rect b = BlockRect(bi, bj);
                    walkSpots.Add(new Vector3(b.xMin - Walk * 0.5f, 0f, b.yMin - Walk * 0.5f));
                    walkSpots.Add(new Vector3(b.xMax + Walk * 0.5f, 0f, b.yMax + Walk * 0.5f));
                    walkSpots.Add(new Vector3(b.xMin - Walk * 0.5f, 0f, b.yMax + Walk * 0.5f));
                    walkSpots.Add(new Vector3(b.xMax + Walk * 0.5f, 0f, b.yMin - Walk * 0.5f));
                }
            Shuffle(walkSpots);
            for (int i = 0; i < walkSpots.Count && recruitSpots.Count < 3 + Mathf.Min(level, 3); i++)
                if ((walkSpots[i] - vanPos).magnitude > 12f) recruitSpots.Add(walkSpots[i]);
            Random.state = keep;
        }

        /// <summary>Carte de battle royale : mêmes rues, quelques pâtés transformés en places (herbe, arbres, murets),
        /// des voitures garées pour se mettre à couvert, et des points de butin un peu partout.</summary>
        public readonly List<Vector3> lootSpots = new List<Vector3>();

        public HeistCity(Transform parent, int seed)
        {
            Random.State keep = Random.state;
            Random.InitState(seed);
            root = Art.Node(parent, "Map", Vector3.zero);
            Ground();
            var parks = new HashSet<int>();
            while (parks.Count < 4) parks.Add(Random.Range(0, N * N));
            for (int bi = 0; bi < N; bi++)
                for (int bj = 0; bj < N; bj++)
                {
                    if (parks.Contains(bi * N + bj)) Park(bi, bj);
                    else BlockAt(bi, bj);
                }
            // voitures garées : abris sur la chaussée
            for (int k = 0; k < 14; k++)
            {
                bool ns = Random.value < 0.5f;
                int line = Random.Range(0, N + 1), seg = Random.Range(0, N);
                float along = seg * Pitch + Road * 0.5f + Random.Range(3f, Block - 3f);
                float side = Random.value < 0.5f ? -3.4f : 3.4f;
                Vector3 at = ns ? new Vector3(line * Pitch + side, 0f, along) : new Vector3(along, 0f, line * Pitch + side);
                ParkedCar(at, ns);
            }
            // points de butin : trottoirs (coins et milieux), places, chaussée
            for (int bi = 0; bi < N; bi++)
                for (int bj = 0; bj < N; bj++)
                {
                    Rect b = BlockRect(bi, bj);
                    float w = Walk * 0.5f;
                    lootSpots.Add(new Vector3(b.center.x, 0f, b.yMin - w));
                    lootSpots.Add(new Vector3(b.center.x, 0f, b.yMax + w));
                    lootSpots.Add(new Vector3(b.xMin - w, 0f, b.center.y));
                    lootSpots.Add(new Vector3(b.xMax + w, 0f, b.center.y));
                    walkSpots.Add(new Vector3(b.xMin - w, 0f, b.yMin - w));
                    walkSpots.Add(new Vector3(b.xMax + w, 0f, b.yMax + w));
                    walkSpots.Add(new Vector3(b.xMin - w, 0f, b.yMax + w));
                    walkSpots.Add(new Vector3(b.xMax + w, 0f, b.yMin - w));
                }
            for (int i = 0; i <= N; i++)
                for (int j = 0; j <= N; j++)
                    lootSpots.Add(Node(i, j) + new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f)));
            Shuffle(lootSpots);
            Random.state = keep;
        }

        void Park(int bi, int bj)
        {
            Rect b = BlockRect(bi, bj);
            float sw = b.width + 2f * Walk;
            Material slab = Art.Tex(Art.Slabs, new Vector2(sw / 3f, sw / 3f), 0.25f);
            Art.Part(root, Art.Cube, slab, new Vector3(b.center.x, 0.07f, b.center.y), new Vector3(sw, 0.14f, sw), default, false);
            Material grass = Art.Mat(new Color(0.42f, 0.66f, 0.32f), 0.15f);
            Art.Part(root, Art.Cube, grass, new Vector3(b.center.x, 0.09f, b.center.y), new Vector3(b.width, 0.14f, b.height), default, false);
            // allées en croix
            Material path = Art.Mat(new Color(0.82f, 0.76f, 0.62f), 0.2f);
            Art.Part(root, Art.Cube, path, new Vector3(b.center.x, 0.1f, b.center.y), new Vector3(2.2f, 0.14f, b.height), default, false);
            Art.Part(root, Art.Cube, path, new Vector3(b.center.x, 0.1f, b.center.y), new Vector3(b.width, 0.14f, 2.2f), default, false);
            // murets (abris) et arbres
            Material wall = Art.Mat(new Color(0.78f, 0.74f, 0.68f), 0.3f);
            for (int k = 0; k < 4; k++)
            {
                float x = b.center.x + (k % 2 == 0 ? -1f : 1f) * b.width * 0.25f, z = b.center.y + (k < 2 ? -1f : 1f) * b.height * 0.25f;
                bool along = Random.value < 0.5f;
                Vector3 size = along ? new Vector3(4f, 1.1f, 0.6f) : new Vector3(0.6f, 1.1f, 4f);
                Art.Part(root, Art.Cube, wall, new Vector3(x, 0.55f, z), size);
                solids.Add(new Rect(x - size.x * 0.5f, z - size.z * 0.5f, size.x, size.z));
                lootSpots.Add(new Vector3(x + (along ? 0f : 1.4f), 0f, z + (along ? 1.4f : 0f)));
                Prop("dec_arbuste", new Vector3(b.center.x + (k % 2 == 0 ? 1f : -1f) * b.width * 0.36f, 0.16f, b.center.y + (k < 2 ? 1f : -1f) * b.height * 0.36f), 2.2f);
            }
            for (int c = 0; c < 4; c++)
            {
                float x = c % 2 == 0 ? b.xMin - Walk * 0.55f : b.xMax + Walk * 0.55f;
                float z = c < 2 ? b.yMin - Walk * 0.55f : b.yMax + Walk * 0.55f;
                Prop("dec_lampadaire", new Vector3(x, 0.14f, z), 3.6f);
            }
        }

        void ParkedCar(Vector3 at, bool northSouth)
        {
            Transform t = Art.Node(root, "parked", at);
            t.localRotation = Quaternion.Euler(0f, northSouth ? (Random.value < 0.5f ? 0f : 180f) : (Random.value < 0.5f ? 90f : -90f), 0f);
            Color[] cols = { new Color(0.85f, 0.2f, 0.18f), new Color(0.98f, 0.78f, 0.15f), new Color(0.2f, 0.55f, 0.85f), new Color(0.92f, 0.92f, 0.9f), new Color(0.2f, 0.2f, 0.24f) };
            Material body = Art.Mat(cols[Random.Range(0, cols.Length)], 0.6f, 0.15f);
            Material glass = Art.Mat(new Color(0.1f, 0.13f, 0.18f), 0.9f, 0.4f);
            Material tire = Art.Mat(new Color(0.07f, 0.07f, 0.08f), 0.2f);
            Art.Part(t, Art.Cube, body, new Vector3(0f, 0.6f, 0f), new Vector3(1.8f, 0.65f, 3.8f));
            Art.Part(t, Art.Cube, glass, new Vector3(0f, 1.1f, -0.25f), new Vector3(1.6f, 0.55f, 1.9f), default, false);
            Art.Part(t, Art.Cube, body, new Vector3(0f, 1.4f, -0.3f), new Vector3(1.58f, 0.06f, 1.5f), default, false);
            for (int i = 0; i < 4; i++)
                Art.Part(t, Art.Cylinder, tire, new Vector3(i % 2 == 0 ? -0.9f : 0.9f, 0.34f, i < 2 ? 1.25f : -1.25f), new Vector3(0.68f, 0.12f, 0.68f), new Vector3(0f, 0f, 90f), false);
            Vector2 half = northSouth ? new Vector2(0.95f, 1.95f) : new Vector2(1.95f, 0.95f);
            solids.Add(new Rect(at.x - half.x, at.z - half.y, half.x * 2f, half.y * 2f));
        }

        static void Shuffle<T>(List<T> l)
        {
            for (int i = l.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); T t = l[i]; l[i] = l[j]; l[j] = t; }
        }

        /// <summary>Emprise d'un pâté (immeubles), sans le trottoir.</summary>
        public static Rect BlockRect(int bi, int bj)
        {
            float x0 = bi * Pitch + Road * 0.5f + Walk, z0 = bj * Pitch + Road * 0.5f + Walk;
            float s = Block - 2f * Walk;
            return new Rect(x0, z0, s, s);
        }

        void Ground()
        {
            Material asphalt = Art.Tex(Art.Asphalt, new Vector2(Size / 8f, Size / 8f), 0.15f);
            Art.Part(root, Art.Quad, asphalt, new Vector3(Size * 0.5f, 0f, Size * 0.5f), new Vector3(Size + 60f, Size + 60f, 1f), new Vector3(90f, 0f, 0f), false).receiveShadows = true;
            // pointillés au milieu des rues
            Material line = Art.Mat(new Color(0.95f, 0.93f, 0.82f), 0.2f);
            for (int k = 0; k <= N; k++)
                for (int m = 0; m < N; m++)
                {
                    float a = m * Pitch + Road * 0.5f;
                    for (float t = a + 1.5f; t < a + Block - 1f; t += 4f)
                    {
                        Art.Part(root, Art.Quad, line, new Vector3(k * Pitch, 0.012f, t + 1f), new Vector3(0.22f, 2f, 1f), new Vector3(90f, 0f, 0f), false);
                        Art.Part(root, Art.Quad, line, new Vector3(t + 1f, 0.012f, k * Pitch), new Vector3(2f, 0.22f, 1f), new Vector3(90f, 0f, 0f), false);
                    }
                }
            // passages piétons aux carrefours
            Material zebra = Art.Mat(new Color(0.92f, 0.92f, 0.9f), 0.2f);
            for (int i = 0; i <= N; i++)
                for (int j = 0; j <= N; j++)
                    for (int s = -1; s <= 1; s += 2)
                        for (int k = -3; k <= 3; k++)
                        {
                            Vector3 c = Node(i, j);
                            Art.Part(root, Art.Quad, zebra, c + new Vector3(k * 1.2f, 0.013f, s * (Road * 0.5f + 1.1f)), new Vector3(0.6f, 1.8f, 1f), new Vector3(90f, 0f, 0f), false);
                        }
        }

        void BlockAt(int bi, int bj)
        {
            Rect b = BlockRect(bi, bj);
            // trottoir
            float sw = b.width + 2f * Walk;
            Material slab = Art.Tex(Art.Slabs, new Vector2(sw / 3f, sw / 3f), 0.25f);
            Art.Part(root, Art.Cube, slab, new Vector3(b.center.x, 0.07f, b.center.y), new Vector3(sw, 0.14f, sw), default, false);
            Material curb = Art.Mat(Pal.Curb, 0.3f);
            Art.Part(root, Art.Cube, curb, new Vector3(b.center.x, 0.08f, b.center.y), new Vector3(sw + 0.3f, 0.12f, sw + 0.3f), default, false);

            // un ou deux immeubles par pâté (coupés en deux au hasard)
            bool split = Random.value < 0.6f;
            if (!split) Building(b);
            else if (Random.value < 0.5f)
            {
                float cut = b.width * Random.Range(0.4f, 0.6f);
                Building(new Rect(b.xMin, b.yMin, cut - 0.3f, b.height));
                Building(new Rect(b.xMin + cut + 0.3f, b.yMin, b.width - cut - 0.3f, b.height));
            }
            else
            {
                float cut = b.height * Random.Range(0.4f, 0.6f);
                Building(new Rect(b.xMin, b.yMin, b.width, cut - 0.3f));
                Building(new Rect(b.xMin, b.yMin + cut + 0.3f, b.width, b.height - cut - 0.3f));
            }
            solids.Add(b);

            // décor de trottoir : arbres et réverbères aux coins
            for (int c = 0; c < 4; c++)
            {
                float x = c % 2 == 0 ? b.xMin - Walk * 0.55f : b.xMax + Walk * 0.55f;
                float z = c < 2 ? b.yMin - Walk * 0.55f : b.yMax + Walk * 0.55f;
                if (Random.value < 0.5f) Prop("dec_arbuste", new Vector3(x, 0.14f, z), 1.6f);
                else Prop("dec_lampadaire", new Vector3(x, 0.14f, z), 3.6f);
            }
        }

        void Prop(string model, Vector3 at, float size)
        {
            if (!GlbModel.Exists(model)) return;
            Transform h = Art.Node(root, model, at);
            Transform m = GlbModel.Spawn(model, h);
            if (m == null) return;
            Renderer[] rs = m.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            Bounds bb = rs[0].bounds;
            foreach (Renderer r in rs) bb.Encapsulate(r.bounds);
            float k = size / Mathf.Max(0.01f, bb.size.y);
            m.localScale = Vector3.one * k;
            m.localPosition = new Vector3(-(bb.center.x - at.x) * k, -(bb.min.y - at.y) * k, -(bb.center.z - at.z) * k);
            foreach (Renderer r in rs) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static readonly Color[] Roofs =
        {
            new Color(0.42f, 0.44f, 0.48f), new Color(0.55f, 0.36f, 0.3f), new Color(0.36f, 0.4f, 0.36f), new Color(0.5f, 0.48f, 0.44f)
        };

        void Building(Rect r)
        {
            float h = Random.Range(5f, 9.5f);
            Art.Facade f = Art.GetFacade(Random.Range(0, Art.FacadeCount));
            Material roof = Art.Mat(Roofs[Random.Range(0, Roofs.Length)], 0.2f);
            Art.Part(root, Art.Cube, roof, new Vector3(r.center.x, h * 0.5f, r.center.y), new Vector3(r.width, h, r.height));
            // façades : textures d'immeuble sur les 4 côtés
            float bayW = 3.2f;
            Material sideX = Art.Tex(f.albedo, new Vector2(Mathf.Max(1f, Mathf.Round(r.width / (bayW * 3f))), 1f), 0.3f, f.emission, new Color(0.35f, 0.3f, 0.2f));
            Material sideZ = Art.Tex(f.albedo, new Vector2(Mathf.Max(1f, Mathf.Round(r.height / (bayW * 3f))), 1f), 0.3f, f.emission, new Color(0.35f, 0.3f, 0.2f));
            Art.Part(root, Art.Quad, sideX, new Vector3(r.center.x, h * 0.5f, r.yMin - 0.02f), new Vector3(r.width, h, 1f), new Vector3(0f, 0f, 0f), false);
            Art.Part(root, Art.Quad, sideX, new Vector3(r.center.x, h * 0.5f, r.yMax + 0.02f), new Vector3(r.width, h, 1f), new Vector3(0f, 180f, 0f), false);
            Art.Part(root, Art.Quad, sideZ, new Vector3(r.xMin - 0.02f, h * 0.5f, r.center.y), new Vector3(r.height, h, 1f), new Vector3(0f, 90f, 0f), false);
            Art.Part(root, Art.Quad, sideZ, new Vector3(r.xMax + 0.02f, h * 0.5f, r.center.y), new Vector3(r.height, h, 1f), new Vector3(0f, -90f, 0f), false);
            // toit : rebord et une ou deux clims
            Material edge = Art.Mat(new Color(0.75f, 0.74f, 0.7f), 0.3f);
            Art.Part(root, Art.Cube, edge, new Vector3(r.center.x, h + 0.15f, r.yMin + 0.15f), new Vector3(r.width, 0.3f, 0.3f), default, false);
            Art.Part(root, Art.Cube, edge, new Vector3(r.center.x, h + 0.15f, r.yMax - 0.15f), new Vector3(r.width, 0.3f, 0.3f), default, false);
            Art.Part(root, Art.Cube, edge, new Vector3(r.xMin + 0.15f, h + 0.15f, r.center.y), new Vector3(0.3f, 0.3f, r.height), default, false);
            Art.Part(root, Art.Cube, edge, new Vector3(r.xMax - 0.15f, h + 0.15f, r.center.y), new Vector3(0.3f, 0.3f, r.height), default, false);
            Material ac = Art.Mat(new Color(0.82f, 0.83f, 0.85f), 0.5f, 0.3f);
            for (int i = 0; i < Random.Range(1, 3); i++)
                Art.Part(root, Art.Cube, ac, new Vector3(Random.Range(r.xMin + 1.5f, r.xMax - 1.5f), h + 0.45f, Random.Range(r.yMin + 1.5f, r.yMax - 1.5f)), new Vector3(1.6f, 0.9f, 1.2f), default, false);
        }

        /// <summary>Porte d'une face de pâté : 0 sud, 1 nord, 2 ouest, 3 est.</summary>
        static Vector3 DoorOf(int bi, int bj, int side, out Vector3 face)
        {
            Rect b = BlockRect(bi, bj);
            float off = Random.Range(-b.width * 0.25f, b.width * 0.25f);
            switch (side)
            {
                case 0: face = Vector3.back; return new Vector3(b.center.x + off, 0f, b.yMin - Walk * 0.5f);
                case 1: face = Vector3.forward; return new Vector3(b.center.x + off, 0f, b.yMax + Walk * 0.5f);
                case 2: face = Vector3.left; return new Vector3(b.xMin - Walk * 0.5f, 0f, b.center.y + off);
                default: face = Vector3.right; return new Vector3(b.xMax + Walk * 0.5f, 0f, b.center.y + off);
            }
        }

        Shop MakeShop(int bi, int bj, int side, Vector3 door, Vector3 face, int level)
        {
            var s = new Shop { door = door, face = face, value = Mathf.RoundToInt(Random.Range(90, 160) * (1f + 0.2f * (level - 1)) / 10f) * 10 };
            // vitrine plaquée sur le mur, face à la rue
            Vector3 wall = door - face * (Walk * 0.5f + 0.04f);
            float yaw = face == Vector3.back ? 0f : face == Vector3.forward ? 180f : face == Vector3.left ? 90f : -90f;
            Art.Facade shopTex = Art.GetShop(Random.Range(0, Art.ShopCount));
            Material m = Art.Tex(shopTex.albedo, Vector2.one, 0.4f, shopTex.emission, new Color(0.8f, 0.7f, 0.5f));
            s.frontMat = m;
            s.front = Art.Part(root, Art.Quad, m, wall + Vector3.up * 2.3f, new Vector3(7f, 4.6f, 1f), new Vector3(0f, yaw, 0f), false);
            // auvent coloré
            Color aw = Color.HSVToRGB(Random.value, 0.65f, 0.95f);
            Art.Part(root, Art.Cube, Art.Mat(aw, 0.3f), wall + face * 0.6f + Vector3.up * 4.75f, new Vector3(7.2f, 0.2f, 1.2f), new Vector3(0f, yaw, 0f), false);
            // cercle de braquage au sol, enseigne « $ » qui flotte
            s.ring = Art.Decal(root, Art.Particle(RingTex, new Color(1f, 0.82f, 0.2f, 0.95f)), door + Vector3.up * 0.16f, new Vector2(3.4f, 3.4f)).transform;
            s.sign = Art.Node(root, "sign", door + Vector3.up * 3.2f);
            Sprite dollar = GearIcons.Art("dollar");
            if (dollar != null)
            {
                var sr = new GameObject("dollar").AddComponent<SpriteRenderer>();
                sr.sprite = dollar;
                sr.transform.SetParent(s.sign, false);
                float k = 1.4f / Mathf.Max(0.01f, dollar.bounds.size.x);
                sr.transform.localScale = Vector3.one * k;
            }
            else Art.Part(s.sign, Art.Cylinder, Art.Glow(new Color(1f, 0.8f, 0.2f), new Color(1.4f, 1f, 0.2f), 0.5f), Vector3.zero, new Vector3(1f, 0.08f, 1f), new Vector3(90f, 0f, 0f), false);
            // gyrophare d'alarme (allumé pendant et après le braquage)
            s.alarm = Art.Part(root, Art.Sphere, Art.Glow(new Color(1f, 0.15f, 0.1f), new Color(3f, 0.3f, 0.2f), 0.5f), wall + face * 0.3f + Vector3.up * 5.3f, Vector3.one * 0.45f, default, false).transform;
            s.alarm.gameObject.SetActive(false);
            return s;
        }

        Transform Van(Vector3 at)
        {
            Transform v = Art.Node(root, "van", at);
            Material body = Art.Mat(new Color(0.18f, 0.16f, 0.24f), 0.55f, 0.2f);
            Material glass = Art.Mat(new Color(0.12f, 0.16f, 0.22f), 0.85f, 0.4f);
            Material tire = Art.Mat(new Color(0.08f, 0.08f, 0.09f), 0.2f);
            Material gold = Art.Glow(new Color(1f, 0.8f, 0.2f), new Color(1.2f, 0.85f, 0.15f), 0.5f);
            Art.Part(v, Art.Cube, body, new Vector3(0f, 1.35f, 0f), new Vector3(2.3f, 1.9f, 5.2f));
            Art.Part(v, Art.Cube, glass, new Vector3(0f, 1.75f, 2.2f), new Vector3(2.1f, 0.8f, 0.9f), default, false);
            Art.Part(v, Art.Cube, gold, new Vector3(1.16f, 1.4f, -0.6f), new Vector3(0.05f, 0.9f, 2.4f), default, false);
            for (int i = 0; i < 4; i++)
                Art.Part(v, Art.Cylinder, tire, new Vector3(i % 2 == 0 ? -1.1f : 1.1f, 0.42f, i < 2 ? 1.7f : -1.7f), new Vector3(0.84f, 0.12f, 0.84f), new Vector3(0f, 0f, 90f), false);
            solids.Add(new Rect(at.x - 1.25f, at.z - 2.7f, 2.5f, 5.4f));
            return v;
        }

        /// <summary>Repousse un disque (rayon r) hors des immeubles et dans les limites de la ville.</summary>
        public Vector3 PushOut(Vector3 p, float r)
        {
            p.x = Mathf.Clamp(p.x, -Road * 0.4f, Size + Road * 0.4f);
            p.z = Mathf.Clamp(p.z, -Road * 0.4f, Size + Road * 0.4f);
            foreach (Rect b in solids)
            {
                float cx = Mathf.Clamp(p.x, b.xMin, b.xMax), cz = Mathf.Clamp(p.z, b.yMin, b.yMax);
                float dx = p.x - cx, dz = p.z - cz;
                float d2 = dx * dx + dz * dz;
                if (d2 >= r * r) continue;
                if (d2 > 1e-6f)
                {
                    float d = Mathf.Sqrt(d2);
                    p.x = cx + dx / d * r;
                    p.z = cz + dz / d * r;
                }
                else
                {
                    // centre dans le rectangle : sortie par le bord le plus proche
                    float l = p.x - b.xMin, rr = b.xMax - p.x, bo = p.z - b.yMin, t = b.yMax - p.z;
                    float m = Mathf.Min(Mathf.Min(l, rr), Mathf.Min(bo, t));
                    if (m == l) p.x = b.xMin - r; else if (m == rr) p.x = b.xMax + r; else if (m == bo) p.z = b.yMin - r; else p.z = b.yMax + r;
                }
            }
            return p;
        }

        /// <summary>Vrai si le segment a-b ne traverse aucun immeuble (ligne de tir).</summary>
        public bool Clear(Vector3 a, Vector3 b)
        {
            for (int k = 1; k < 8; k++)
            {
                Vector3 p = Vector3.Lerp(a, b, k / 8f);
                foreach (Rect r in solids) if (r.Contains(new Vector2(p.x, p.z))) return false;
            }
            return true;
        }
    }
}
