using UnityEngine;

namespace SamediRouge
{
    /// <summary>
    /// Décors des quartiers 3 à 5, construits sur le même couloir que la galerie (couloirs, murs de collision, magasins
    /// au niveau du sol pour les pillages) :
    /// 2 gare (grand hall voûté, verrière, tableau des départs, horloge, valises),
    /// 3 docks (piles de conteneurs, portiques, lampadaires au sodium, l'eau au loin),
    /// 4 quartier chic de nuit (boutiques de luxe, immeubles aux fenêtres allumées, néons, réverbères, tapis rouge).
    /// </summary>
    public partial class Street
    {
        Material floorDock, floorLux;
        // gare
        Material iron, vault, stone, board, boardText, clockFace, clockHand, yellowSign, signText;
        Material[] luggage;
        // docks
        Material[] boxes, boxRibs;
        Material crane, craneDark, water, sodium, safety, crateWood, steel, bollard, ropeMat;
        // quartier chic
        Material gold, awning, neonPink, neonCyan, neonGold, carpet, lampWarm, lampPole, topiary, carBody, carGlass;

        void InitThemes()
        {
            floorDock = Art.Tex(Art.Asphalt, new Vector2(3f, 7f), 0.35f);
            floorLux = Art.Tex(Art.Slabs, new Vector2(4f, 9f), 0.92f);

            iron = Art.Mat(new Color(0.16f, 0.3f, 0.27f), 0.55f, 0.6f);
            vault = Art.Glow(new Color(0.78f, 0.88f, 0.96f), new Color(0.55f, 0.66f, 0.8f), 0.9f);
            stone = Art.Mat(new Color(0.86f, 0.81f, 0.72f), 0.25f);
            board = Art.Mat(new Color(0.05f, 0.06f, 0.08f), 0.7f);
            boardText = Art.Glow(new Color(1f, 0.72f, 0.2f), new Color(2.4f, 1.5f, 0.3f));
            clockFace = Art.Glow(new Color(0.98f, 0.97f, 0.92f), new Color(1.2f, 1.15f, 1.05f));
            clockHand = Art.Mat(new Color(0.06f, 0.06f, 0.07f), 0.4f);
            yellowSign = Art.Glow(new Color(1f, 0.8f, 0.1f), new Color(0.9f, 0.65f, 0.05f));
            signText = Art.Mat(new Color(0.08f, 0.08f, 0.1f), 0.3f);
            luggage = new[]
            {
                Art.Mat(new Color(0.75f, 0.2f, 0.22f), 0.5f), Art.Mat(new Color(0.18f, 0.32f, 0.6f), 0.5f),
                Art.Mat(new Color(0.15f, 0.15f, 0.17f), 0.5f), Art.Mat(new Color(0.85f, 0.65f, 0.2f), 0.5f)
            };

            Color[] bc =
            {
                new Color(0.72f, 0.18f, 0.13f), new Color(0.13f, 0.33f, 0.62f), new Color(0.9f, 0.52f, 0.1f),
                new Color(0.2f, 0.52f, 0.34f), new Color(0.84f, 0.84f, 0.8f), new Color(0.5f, 0.2f, 0.42f)
            };
            boxes = new Material[bc.Length];
            boxRibs = new Material[bc.Length];
            for (int i = 0; i < bc.Length; i++)
            {
                boxes[i] = Art.Mat(bc[i], 0.35f, 0.3f);
                boxRibs[i] = Art.Mat(bc[i] * 0.72f, 0.35f, 0.3f);
            }
            crane = Art.Mat(new Color(0.95f, 0.7f, 0.1f), 0.4f, 0.3f);
            craneDark = Art.Mat(new Color(0.2f, 0.2f, 0.22f), 0.5f, 0.5f);
            water = Art.Mat(new Color(0.12f, 0.27f, 0.36f), 0.95f);
            sodium = Art.Glow(new Color(1f, 0.72f, 0.35f), new Color(3f, 1.8f, 0.6f));
            safety = Art.Mat(new Color(0.95f, 0.78f, 0.1f), 0.3f);
            crateWood = Art.Mat(new Color(0.6f, 0.45f, 0.28f), 0.15f);
            steel = Art.Mat(new Color(0.55f, 0.58f, 0.62f), 0.6f, 0.7f);
            bollard = Art.Mat(new Color(0.14f, 0.14f, 0.16f), 0.4f, 0.3f);
            ropeMat = Art.Mat(new Color(0.78f, 0.7f, 0.5f), 0.1f);

            gold = Art.Mat(new Color(0.86f, 0.68f, 0.3f), 0.85f, 0.9f);
            awning = Art.Mat(new Color(0.07f, 0.07f, 0.09f), 0.6f);
            neonPink = Art.Glow(new Color(1f, 0.3f, 0.7f), new Color(3f, 0.6f, 2f));
            neonCyan = Art.Glow(new Color(0.3f, 0.9f, 1f), new Color(0.5f, 2.4f, 3f));
            neonGold = Art.Glow(new Color(1f, 0.82f, 0.4f), new Color(2.6f, 1.9f, 0.7f));
            carpet = Art.Mat(new Color(0.62f, 0.05f, 0.1f), 0.15f);
            lampWarm = Art.Glow(new Color(1f, 0.9f, 0.7f), new Color(2.6f, 2.1f, 1.3f));
            lampPole = Art.Mat(new Color(0.06f, 0.06f, 0.07f), 0.6f, 0.6f);
            topiary = Art.Mat(new Color(0.16f, 0.36f, 0.2f), 0.2f);
            carBody = Art.Mat(new Color(0.06f, 0.06f, 0.08f), 0.95f, 0.8f);
            carGlass = Art.Mat(new Color(0.12f, 0.16f, 0.22f), 0.95f);
        }

        void BuildThemed(Transform t, ChunkData cd)
        {
            float mid = Chunk * 0.5f;
            Material fl = FloorArt() ?? (Theme == 3 ? floorDock : Theme == 4 ? floorLux : floor);
            MeshRenderer f = Art.Decal(t, fl, new Vector3(0f, 0f, mid), new Vector2(ShopX * 2f, Chunk));
            f.receiveShadows = true;
            var col = Art.Node(t, "floor", new Vector3(0f, -0.25f, mid)).gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(30f, 0.5f, Chunk);
            for (int s = -1; s <= 1; s += 2)
            {
                var w = Art.Node(t, "wall", new Vector3(s * (ShopX + 0.5f), 6f, mid)).gameObject.AddComponent<BoxCollider>();
                w.size = new Vector3(1f, 12f, Chunk);
            }
            if (Theme == 2) Station(t, cd);
            else if (Theme == 3) Docks(t, cd);
            else Uptown(t, cd);
        }

        /// <summary>Promeneurs le long des murs (voyageurs, dockers, clients du soir).</summary>
        void SideWalkers(Transform t, ChunkData cd, int n, float inset)
        {
            for (int i = 0; i < n; i++)
            {
                int s = Random.value < 0.5f ? -1 : 1;
                float dir = Random.value < 0.5f ? -1f : 1f;
                Rig r = Rig.Build(Kind.Shopper, t, new Vector3(s * (ShopX - inset), 0f, Random.Range(2f, Chunk - 2f)), dir > 0f ? 0f : 180f, Random.Range(0, 12), false);
                r.anim = Anim.Walk;
                r.speed = Random.Range(1.1f, 1.6f);
                cd.people.Add(new Walker { rig = r, dir = dir, v = r.speed });
            }
        }

        // ================================================================ gare

        void Station(Transform t, ChunkData cd)
        {
            const float springY = 8.6f, radius = 8.1f;
            for (int s = -1; s <= 1; s += 2)
            {
                // boutiques et guichets au rez-de-chaussée, mur de pierre et grandes baies au-dessus
                for (int k = 0; k < 4; k++)
                {
                    float zc = 4.5f + k * 9f;
                    Store(t, s, zc, ShopX, 0f, Random.Range(0, Art.StoreCount));
                    Art.Part(t, Art.Quad, vault, new Vector3(s * (ShopX + 0.25f), 6.9f, zc), new Vector3(6.4f, 2.8f, 1f), new Vector3(0f, s < 0 ? -90f : 90f, 0f), false);
                    Art.Part(t, Art.Cube, iron, new Vector3(s * (ShopX + 0.22f), 6.9f, zc), new Vector3(0.06f, 2.8f, 0.12f), default, false);
                }
                Art.Part(t, Art.Cube, stone, new Vector3(s * (ShopX + 0.6f), 6.9f, Chunk * 0.5f), new Vector3(0.6f, 4.2f, Chunk));
                Art.Part(t, Art.Cube, stone, new Vector3(s * (ShopX - 0.1f), 4.95f, Chunk * 0.5f), new Vector3(0.5f, 0.3f, Chunk), default, false);
                // colonnes de fonte vertes
                for (float z = 0f; z < Chunk; z += 12f)
                {
                    Art.Part(t, Art.Cylinder, iron, new Vector3(s * (ShopX - 0.45f), springY * 0.5f, z + 1f), new Vector3(0.38f, springY * 0.5f, 0.38f));
                    Art.Part(t, Art.Cube, iron, new Vector3(s * (ShopX - 0.45f), springY - 0.2f, z + 1f), new Vector3(0.7f, 0.3f, 0.7f), default, false);
                }
                // panneau jaune des voies
                if (Random.value < 0.6f)
                {
                    float zs = Random.Range(5f, Chunk - 5f);
                    Art.Part(t, Art.Cube, yellowSign, new Vector3(s * (ShopX - 0.2f), 5.6f, zs), new Vector3(0.12f, 0.8f, 2.6f), default, false);
                    Art.Part(t, Art.Cube, signText, new Vector3(s * (ShopX - 0.28f), 5.6f, zs), new Vector3(0.02f, 0.18f, 1.8f), default, false);
                }
                // valises et chariots à bagages
                float r = Random.value;
                float zp = Random.Range(4f, Chunk - 4f);
                if (r < 0.5f) Luggage(t, s, zp);
                else if (r < 0.8f) Bench(t, s, zp);
            }

            // voûte : arcs de fonte et verrière continue (8 pans)
            const int segs = 8;
            for (int i = 0; i < segs; i++)
            {
                float a0 = Mathf.PI * i / segs, a1 = Mathf.PI * (i + 1) / segs, am = (a0 + a1) * 0.5f;
                Vector3 c = new Vector3(Mathf.Cos(am) * radius, springY + Mathf.Sin(am) * radius, 0f);
                float len = 2f * radius * Mathf.Sin((a1 - a0) * 0.5f);
                float rz = am * Mathf.Rad2Deg + 90f;
                Art.Part(t, Art.Cube, vault, c + new Vector3(0f, 0f, Chunk * 0.5f), new Vector3(len + 0.05f, 0.06f, Chunk), new Vector3(0f, 0f, rz), false);
                for (float z = 3f; z < Chunk; z += 6f)
                    Art.Part(t, Art.Cube, iron, c + new Vector3(0f, -0.15f, z), new Vector3(len + 0.1f, 0.3f, 0.25f), new Vector3(0f, 0f, rz), false);
            }
            // tirants de la voûte
            for (float z = 3f; z < Chunk; z += 12f)
                Art.Part(t, Art.Cube, iron, new Vector3(0f, springY, z), new Vector3(radius * 2f, 0.1f, 0.1f), default, false);

            // tableau des départs suspendu
            if (Random.value < 0.5f)
            {
                float zb = Random.Range(8f, Chunk - 8f);
                Art.Part(t, Art.Cube, board, new Vector3(0f, 7.4f, zb), new Vector3(6.4f, 1.9f, 0.25f), default, false);
                for (int row = 0; row < 4; row++)
                {
                    float w = Random.Range(2.8f, 5.6f);
                    Art.Part(t, Art.Cube, boardText, new Vector3(-3f + w * 0.5f + 0.1f, 8.05f - row * 0.42f, zb - 0.14f), new Vector3(w, 0.16f, 0.02f), default, false);
                }
                Art.Part(t, Art.Cube, clockHand, new Vector3(-2.6f, 9.6f, zb), new Vector3(0.05f, 2.6f, 0.05f), default, false);
                Art.Part(t, Art.Cube, clockHand, new Vector3(2.6f, 9.6f, zb), new Vector3(0.05f, 2.6f, 0.05f), default, false);
            }
            // horloge à double cadran
            else if (Random.value < 0.6f)
            {
                float zc = Random.Range(8f, Chunk - 8f);
                Art.Part(t, Art.Cylinder, iron, new Vector3(0f, 8.2f, zc), new Vector3(1.6f, 0.14f, 1.6f), new Vector3(90f, 0f, 0f), false);
                Art.Part(t, Art.Cylinder, clockFace, new Vector3(0f, 8.2f, zc), new Vector3(1.4f, 0.16f, 1.4f), new Vector3(90f, 0f, 0f), false);
                Art.Part(t, Art.Cube, clockHand, new Vector3(0f, 8.38f, zc - 0.17f), new Vector3(0.07f, 0.45f, 0.02f), default, false);
                Art.Part(t, Art.Cube, clockHand, new Vector3(0.18f, 8.2f, zc - 0.17f), new Vector3(0.38f, 0.06f, 0.02f), new Vector3(0f, 0f, -20f), false);
                Art.Part(t, Art.Cube, clockHand, new Vector3(0f, 9.6f, zc), new Vector3(0.06f, 1.4f, 0.06f), default, false);
            }

            SideWalkers(t, cd, Random.Range(1, 3), 1.6f);
        }

        void Luggage(Transform t, int s, float z)
        {
            float x = s * (ShopX - 1.3f);
            // chariot : plateau, poignée, deux valises dessus
            Art.Part(t, Art.Cube, steel, new Vector3(x, 0.25f, z), new Vector3(0.7f, 0.06f, 1.2f));
            Art.Part(t, Art.Cube, steel, new Vector3(x, 0.7f, z - 0.62f), new Vector3(0.7f, 0.9f, 0.05f), default, false);
            for (int k = 0; k < 2; k++)
                Art.Part(t, Art.Cube, luggage[Random.Range(0, luggage.Length)], new Vector3(x, 0.52f + k * 0.42f, z + 0.1f), new Vector3(0.55f, 0.4f, 0.8f - k * 0.15f));
            Art.Part(t, Art.Cube, luggage[Random.Range(0, luggage.Length)], new Vector3(x + s * -0.9f, 0.36f, z + 1.2f), new Vector3(0.4f, 0.72f, 0.26f));
        }

        // ================================================================ docks

        void Docks(Transform t, ChunkData cd)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                // lignes de sécurité jaunes au sol
                Art.Part(t, Art.Cube, safety, new Vector3(s * 5.3f, 0.015f, Chunk * 0.5f), new Vector3(0.18f, 0.02f, Chunk), default, false);
                // piles de conteneurs (les portes s'ouvrent côté couloir : les pillards en sortent)
                for (int k = 0; k < 5; k++)
                {
                    float zc = 3.6f + k * 7.2f;
                    int h = Random.Range(1, 4);
                    for (int y = 0; y < h; y++)
                    {
                        int c = Random.Range(0, boxes.Length);
                        float jitter = y > 0 ? Random.Range(-0.3f, 0.3f) : 0f;
                        Vector3 p = new Vector3(s * (ShopX + 1.3f), 1.3f + y * 2.6f, zc + jitter);
                        Art.Part(t, Art.Cube, boxes[c], p, new Vector3(2.5f, 2.55f, 6.1f));
                        Texture2D side = DockArt(Random.Range(0, 12));
                        if (side != null)
                            // flanc illustré (conteneur, façade d'entrepôt) collé côté couloir
                            Art.Part(t, Art.Quad, Art.Tex(side, Vector2.one, 0.35f), p + new Vector3(-s * 1.26f, 0f, 0f), new Vector3(6.1f, 2.55f, 1f), new Vector3(0f, s < 0 ? -90f : 90f, 0f), false);
                        else if (y == 0)
                            for (int rb = -1; rb <= 1; rb++)
                                Art.Part(t, Art.Cube, boxRibs[c], p + new Vector3(-s * 1.27f, 0f, rb * 2f), new Vector3(0.05f, 2.4f, 0.22f), default, false);
                    }
                    // seconde rangée plus haute, derrière
                    if (Random.value < 0.6f)
                        Art.Part(t, Art.Cube, boxes[Random.Range(0, boxes.Length)], new Vector3(s * (ShopX + 4f), 2.6f + Random.Range(0, 2) * 2.6f, zc), new Vector3(2.5f, 5.2f, 6.1f), default, false);
                }
                // l'eau du port, au-delà des conteneurs
                Art.Part(t, Art.Cube, water, new Vector3(s * (ShopX + 22f), -0.6f, Chunk * 0.5f), new Vector3(30f, 0.2f, Chunk), default, false);
                // bittes d'amarrage, caisses, fûts
                float r = Random.value;
                float zp = Random.Range(3f, Chunk - 3f);
                if (r < 0.35f) Crates(t, s, zp);
                else if (r < 0.65f) Barrels(t, s, zp);
                else if (r < 0.85f)
                {
                    Art.Part(t, Art.Cylinder, bollard, new Vector3(s * (ShopX - 0.9f), 0.35f, zp), new Vector3(0.45f, 0.35f, 0.45f));
                    Art.Part(t, Art.Cylinder, ropeMat, new Vector3(s * (ShopX - 1.9f), 0.12f, zp + 0.4f), new Vector3(0.9f, 0.12f, 0.9f), default, false);
                }
            }
            // lampadaires au sodium, en quinconce
            for (int k = 0; k < 3; k++)
            {
                int s = k % 2 == 0 ? -1 : 1;
                float z = 6f + k * 12f;
                Art.Part(t, Art.Cylinder, steel, new Vector3(s * (ShopX - 0.5f), 3.6f, z), new Vector3(0.14f, 3.6f, 0.14f));
                Art.Part(t, Art.Cube, steel, new Vector3(s * (ShopX - 1.3f), 7.15f, z), new Vector3(1.7f, 0.1f, 0.12f), default, false);
                Art.Part(t, Art.Cube, sodium, new Vector3(s * (ShopX - 2f), 7.02f, z), new Vector3(0.6f, 0.12f, 0.32f), default, false);
                Art.Part(t, Art.Quad, Art.Particle(Art.SoftDot, new Color(1f, 0.7f, 0.35f, 0.35f)), new Vector3(s * (ShopX - 2f), 6.6f, z), new Vector3(3.2f, 3.2f, 1f), default, false);
            }
            // portique de levage au-dessus de la route, un tronçon sur deux
            if (counter % 2 == 0)
            {
                float zc = Random.Range(8f, Chunk - 8f);
                for (int s = -1; s <= 1; s += 2)
                    for (int k = 0; k < 2; k++)
                        Art.Part(t, Art.Cube, crane, new Vector3(s * (ShopX + 1.6f), 8.5f, zc + k * 4f), new Vector3(0.7f, 17f, 0.7f));
                for (int k = 0; k < 2; k++)
                    Art.Part(t, Art.Cube, crane, new Vector3(0f, 16.6f, zc + k * 4f), new Vector3(2f * ShopX + 6f, 1.1f, 0.9f), default, false);
                Art.Part(t, Art.Cube, craneDark, new Vector3(-2f, 15.4f, zc + 2f), new Vector3(2.2f, 1.6f, 2.4f), default, false);
                Art.Part(t, Art.Cube, craneDark, new Vector3(-2f, 13.6f, zc + 2f), new Vector3(0.05f, 3.4f, 0.05f), default, false);
                Art.Part(t, Art.Cube, boxes[Random.Range(0, boxes.Length)], new Vector3(-2f, 10.6f, zc + 2f), new Vector3(2.5f, 2.55f, 6.1f), default, false);
            }
            SideWalkers(t, cd, Random.Range(0, 2), 1.4f);
        }

        static System.Collections.Generic.List<Texture2D> dockArt;
        /// <summary>Flancs illustrés des docks : Resources/SamediRouge/Art/container_N et warehouse_N (null s'il n'y en a aucun).</summary>
        static Texture2D DockArt(int v)
        {
            if (dockArt == null)
            {
                dockArt = new System.Collections.Generic.List<Texture2D>();
                foreach (string pre in new[] { "container_", "warehouse_" })
                    for (int i = 1; i <= 8; i++)
                    {
                        Texture2D tx = Resources.Load<Texture2D>("SamediRouge/Art/" + pre + i);
                        if (tx != null) dockArt.Add(tx);
                    }
            }
            return dockArt.Count > 0 ? dockArt[v % dockArt.Count] : null;
        }

        void Crates(Transform t, int s, float z)
        {
            float x = s * (ShopX - 1.3f);
            for (int k = 0; k < 3; k++)
            {
                Vector3 p = new Vector3(x + Random.Range(-0.3f, 0.3f), 0.45f + (k == 2 ? 0.9f : 0f), z + (k == 1 ? 1f : 0f));
                Art.Part(t, Art.Cube, crateWood, p, new Vector3(0.9f, 0.9f, 0.9f), new Vector3(0f, Random.Range(-12f, 12f), 0f));
                Art.Part(t, Art.Cube, craneDark, p, new Vector3(0.92f, 0.1f, 0.92f), default, false);
            }
        }

        void Barrels(Transform t, int s, float z)
        {
            float x = s * (ShopX - 1.2f);
            Material m = Random.value < 0.5f ? boxes[1] : boxes[0];
            for (int k = 0; k < 3; k++)
                Art.Part(t, Art.Cylinder, m, new Vector3(x + (k % 2) * 0.6f * -s, 0.5f, z + k * 0.55f), new Vector3(0.55f, 0.5f, 0.55f));
        }

        // ================================================================ quartier chic, de nuit

        void Uptown(Transform t, ChunkData cd)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                float yaw = s < 0 ? -90f : 90f;
                for (int k = 0; k < 4; k++)
                {
                    float zc = 4.5f + k * 9f;
                    // boutique de luxe : vitrine éclairée, cadre doré, auvent noir
                    Material m;
                    Texture2D lux = ShopArt(Random.Range(0, 12));
                    if (lux != null) m = Art.Tex(lux, Vector2.one, 0.6f, lux, new Color(0.8f, 0.8f, 0.8f));   // devanture illustrée, éclairée la nuit
                    else
                    {
                        Art.Facade shop = Art.GetShop(Random.Range(0, Art.ShopCount));
                        m = Art.Tex(shop.albedo, Vector2.one, 0.7f, shop.emission, new Color(1.5f, 1.4f, 1.25f));
                    }
                    Art.Part(t, Art.Quad, m, new Vector3(s * (ShopX - 0.01f), 2.4f, zc), new Vector3(9f, 4.6f, 1f), new Vector3(0f, yaw, 0f), false);
                    Art.Part(t, Art.Cube, wall, new Vector3(s * (ShopX + 0.3f), 2.4f, zc), new Vector3(0.6f, 4.8f, 9f));
                    Art.Part(t, Art.Cube, gold, new Vector3(s * (ShopX - 0.06f), 2.4f, zc - 4.45f), new Vector3(0.12f, 4.8f, 0.12f), default, false);
                    Art.Part(t, Art.Cube, awning, new Vector3(s * (ShopX - 0.55f), 4.75f, zc), new Vector3(1.1f, 0.18f, 7.6f), new Vector3(0f, 0f, s * -8f), false);
                    Art.Part(t, Art.Cube, gold, new Vector3(s * (ShopX - 1.08f), 4.68f, zc), new Vector3(0.04f, 0.1f, 7.6f), default, false);
                    // immeuble au-dessus : façade, fenêtres allumées
                    Art.Facade fa = Art.GetFacade(Random.Range(0, Art.FacadeCount));
                    float h = 9f * fa.floors * 80f / 192f;
                    Material fm = Art.Tex(fa.albedo, Vector2.one, 0.3f, fa.emission, new Color(1.6f, 1.4f, 1.1f));
                    Art.Part(t, Art.Quad, fm, new Vector3(s * (ShopX + 0.2f), 5f + h * 0.5f, zc), new Vector3(9f, h, 1f), new Vector3(0f, yaw, 0f), false);
                }
                Art.Part(t, Art.Cube, gold, new Vector3(s * (ShopX + 0.1f), 5.05f, Chunk * 0.5f), new Vector3(0.3f, 0.12f, Chunk), default, false);
                // enseigne néon verticale
                if (Random.value < 0.7f)
                {
                    float zn = Random.Range(3f, Chunk - 3f);
                    Material n = Random.value < 0.4f ? neonPink : Random.value < 0.5f ? neonCyan : neonGold;
                    Art.Part(t, Art.Cube, awning, new Vector3(s * (ShopX - 0.5f), 8f, zn), new Vector3(0.9f, 3.4f, 0.2f), default, false);
                    Art.Part(t, Art.Cube, n, new Vector3(s * (ShopX - 0.5f), 8f, zn - 0.11f), new Vector3(0.7f, 3.1f, 0.03f), default, false);
                    Art.Part(t, Art.Quad, Art.Particle(Art.SoftDot, n.color * new Color(1f, 1f, 1f, 0.3f)), new Vector3(s * (ShopX - 0.6f), 8f, zn - 0.3f), new Vector3(3f, 5f, 1f), default, false);
                }
                // décor du trottoir
                float r = Random.value;
                float zp = Random.Range(4f, Chunk - 4f);
                if (r < 0.3f) RedCarpet(t, s, zp);
                else if (r < 0.6f) Topiary(t, s, zp);
                else if (r < 0.8f) Limo(t, s, zp);
            }
            // réverbères anciens, en quinconce
            for (int k = 0; k < 3; k++)
            {
                int s = k % 2 == 0 ? 1 : -1;
                float z = 4f + k * 12f;
                float x = s * (ShopX - 1.9f);
                Art.Part(t, Art.Cylinder, lampPole, new Vector3(x, 2.1f, z), new Vector3(0.14f, 2.1f, 0.14f));
                Art.Part(t, Art.Cylinder, lampPole, new Vector3(x, 0.2f, z), new Vector3(0.36f, 0.2f, 0.36f));
                Art.Part(t, Art.Sphere, lampWarm, new Vector3(x, 4.45f, z), new Vector3(0.48f, 0.56f, 0.48f), default, false);
                Art.Part(t, Art.Cube, lampPole, new Vector3(x, 4.8f, z), new Vector3(0.36f, 0.1f, 0.36f), default, false);
                Art.Part(t, Art.Quad, Art.Particle(Art.SoftDot, new Color(1f, 0.85f, 0.55f, 0.32f)), new Vector3(x, 4.45f, z), new Vector3(3.4f, 3.4f, 1f), default, false);
            }
            SideWalkers(t, cd, Random.Range(1, 3), 1.0f);
        }

        void RedCarpet(Transform t, int s, float z)
        {
            Art.Part(t, Art.Cube, carpet, new Vector3(s * (ShopX - 1.2f), 0.012f, z), new Vector3(2.4f, 0.02f, 1.8f), default, false);
            for (int k = -1; k <= 1; k += 2)
            {
                Vector3 p = new Vector3(s * (ShopX - 2.1f), 0f, z + k * 1.1f);
                Art.Part(t, Art.Cylinder, gold, p + new Vector3(0f, 0.45f, 0f), new Vector3(0.08f, 0.45f, 0.08f));
                Art.Part(t, Art.Sphere, gold, p + new Vector3(0f, 0.93f, 0f), Vector3.one * 0.13f, default, false);
            }
            Art.Part(t, Art.Cube, carpet, new Vector3(s * (ShopX - 2.1f), 0.78f, z), new Vector3(0.07f, 0.07f, 2.1f), default, false);
        }

        void Topiary(Transform t, int s, float z)
        {
            float x = s * (ShopX - 1.1f);
            Art.Part(t, Art.Cube, awning, new Vector3(x, 0.35f, z), new Vector3(0.8f, 0.7f, 0.8f));
            Art.Part(t, Art.Cube, gold, new Vector3(x, 0.72f, z), new Vector3(0.84f, 0.05f, 0.84f), default, false);
            Art.Part(t, Art.Sphere, topiary, new Vector3(x, 1.3f, z), Vector3.one * 0.95f);
            Art.Part(t, Art.Sphere, topiary, new Vector3(x, 2.05f, z), Vector3.one * 0.6f);
        }

        void Limo(Transform t, int s, float z)
        {
            float x = s * (ShopX - 1.5f);
            Art.Part(t, Art.Cube, carBody, new Vector3(x, 0.55f, z), new Vector3(1.7f, 0.6f, 4.4f));
            Art.Part(t, Art.Cube, carGlass, new Vector3(x, 1.08f, z - 0.2f), new Vector3(1.5f, 0.5f, 2.4f));
            Art.Part(t, Art.Cube, lampWarm, new Vector3(x, 0.6f, z + 2.21f), new Vector3(1.3f, 0.12f, 0.02f), default, false);
            for (int k = -1; k <= 1; k += 2)
                for (int j = -1; j <= 1; j += 2)
                    Art.Part(t, Art.Cylinder, lampPole, new Vector3(x + k * 0.8f, 0.3f, z + j * 1.4f), new Vector3(0.6f, 0.1f, 0.6f), new Vector3(0f, 0f, 90f), false);
        }
    }
}
