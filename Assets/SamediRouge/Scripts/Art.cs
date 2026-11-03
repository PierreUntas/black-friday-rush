using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SamediRouge
{
    /// <summary>Palette du jeu : soir d'hiver, néons, gyrophares.</summary>
    public static class Pal
    {
        public static readonly Color Fog = new Color(0.86f, 0.88f, 0.92f);
        public static readonly Color Sidewalk = new Color(0.52f, 0.53f, 0.55f);
        public static readonly Color Curb = new Color(0.66f, 0.66f, 0.64f);
        public static readonly Color Line = new Color(0.88f, 0.87f, 0.82f);
        public static readonly Color CrsNavy = new Color(0.13f, 0.18f, 0.32f);
        public static readonly Color CrsDark = new Color(0.07f, 0.09f, 0.15f);
        public static readonly Color Helmet = new Color(0.09f, 0.13f, 0.27f);
        public static readonly Color Visor = new Color(0.55f, 0.72f, 0.88f);
        public static readonly Color Shield = new Color(0.68f, 0.76f, 0.84f);
        public static readonly Color Black = new Color(0.06f, 0.065f, 0.075f);
        public static readonly Color Hood = new Color(0.1f, 0.105f, 0.115f);
        public static readonly Color Rouge = new Color(0.86f, 0.2f, 0.15f);
        public static readonly Color Jaune = new Color(0.97f, 0.83f, 0.08f);
        public static readonly Color Reflect = new Color(0.86f, 0.89f, 0.92f);
        public static readonly Color Gold = new Color(0.98f, 0.76f, 0.22f);
        public static readonly Color Wood = new Color(0.52f, 0.37f, 0.23f);
        public static readonly Color Tire = new Color(0.07f, 0.07f, 0.08f);
        public static readonly Color Lamp = new Color(1f, 0.77f, 0.48f);
        public static readonly Color Metal = new Color(0.55f, 0.57f, 0.6f);
        public static readonly Color Bin = new Color(0.22f, 0.36f, 0.27f);

        static readonly Color[] skins =
        {
            new Color(0.93f, 0.75f, 0.61f), new Color(0.79f, 0.56f, 0.41f), new Color(0.55f, 0.35f, 0.24f),
            new Color(0.96f, 0.81f, 0.7f), new Color(0.71f, 0.48f, 0.32f)
        };
        static readonly Color[] pants =
        {
            new Color(0.18f, 0.23f, 0.32f), new Color(0.36f, 0.29f, 0.24f), new Color(0.22f, 0.22f, 0.25f), new Color(0.3f, 0.34f, 0.28f)
        };
        static readonly Color[] hair =
        {
            new Color(0.12f, 0.08f, 0.06f), new Color(0.35f, 0.22f, 0.12f), new Color(0.6f, 0.45f, 0.25f), new Color(0.5f, 0.5f, 0.52f)
        };
        public static Color Skin() => skins[Random.Range(0, skins.Length)];
        public static Color Pants() => pants[Random.Range(0, pants.Length)];
        public static Color Hair() => hair[Random.Range(0, hair.Length)];
    }

    /// <summary>Matériaux, maillages et textures générés par code : aucun asset à importer.</summary>
    public static class Art
    {
        static Material litSrc, emisSrc, partSrc;
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static readonly Dictionary<string, Texture2D> texCache = new Dictionary<string, Texture2D>();
        public static Mesh Cube, Sphere, Capsule, Cylinder, Quad, Star;

        public static void Init(Material lit, Material emis, Material part)
        {
            mats.Clear();
            texCache.Clear();
            facades = null;
            outlineMat = null;
            outlineChecked = false;
            shops = null;
            stores = null;
            litSrc = lit;
            emisSrc = emis;
            partSrc = part;
            Cube = Prim(PrimitiveType.Cube);
            Sphere = Prim(PrimitiveType.Sphere);
            Capsule = Prim(PrimitiveType.Capsule);
            Cylinder = Prim(PrimitiveType.Cylinder);
            Quad = Prim(PrimitiveType.Quad);
            Star = MakeStarMesh();
            if (emisSrc == null)
            {
                emisSrc = new Material(litSrc);
                emisSrc.EnableKeyword("_EMISSION");
            }
            if (partSrc == null)
            {
                Shader sh = Shader.Find("Sprites/Default");
                partSrc = sh != null ? new Material(sh) : new Material(litSrc);
            }
        }

        /// <summary>Étoile à 5 branches en relief (pièces : étoiles de police), 1 m de diamètre, face vers +Z et -Z.</summary>
        static Mesh MakeStarMesh()
        {
            const float depth = 0.12f;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            // étoile de police à 6 branches (les boules des pointes sont ajoutées à part)
            const int pts = 12;
            var outline = new Vector3[pts];
            for (int i = 0; i < pts; i++)
            {
                float a = Mathf.PI * 0.5f + i * Mathf.PI / 6f;
                float r = i % 2 == 0 ? 0.5f : 0.27f;
                outline[i] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
            }
            for (int side = 0; side < 2; side++)
            {
                float z = side == 0 ? -depth * 0.5f : depth * 0.5f;
                int c = verts.Count;
                verts.Add(new Vector3(0f, 0f, z * 1.6f)); // centre bombé
                for (int i = 0; i < pts; i++) verts.Add(outline[i] + new Vector3(0f, 0f, z));
                for (int i = 0; i < pts; i++)
                {
                    int a = c + 1 + i, b = c + 1 + (i + 1) % pts;
                    if (side == 0) { tris.Add(c); tris.Add(a); tris.Add(b); }
                    else { tris.Add(c); tris.Add(b); tris.Add(a); }
                }
            }
            for (int i = 0; i < pts; i++)
            {
                int c = verts.Count;
                Vector3 p0 = outline[i], p1 = outline[(i + 1) % pts];
                verts.Add(p0 + new Vector3(0f, 0f, -depth * 0.5f));
                verts.Add(p1 + new Vector3(0f, 0f, -depth * 0.5f));
                verts.Add(p1 + new Vector3(0f, 0f, depth * 0.5f));
                verts.Add(p0 + new Vector3(0f, 0f, depth * 0.5f));
                tris.Add(c); tris.Add(c + 2); tris.Add(c + 1);
                tris.Add(c); tris.Add(c + 3); tris.Add(c + 2);
            }
            var m = new Mesh { name = "Star" };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static Mesh Prim(PrimitiveType t)
        {
            GameObject go = GameObject.CreatePrimitive(t);
            Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
            if (litSrc == null) litSrc = go.GetComponent<Renderer>().sharedMaterial;
            go.SetActive(false);
            Object.Destroy(go);
            return mesh;
        }

        static string Key(Color c) => c.r.ToString("0.000") + "," + c.g.ToString("0.000") + "," + c.b.ToString("0.000") + "," + c.a.ToString("0.00");

        /// <summary>Matériau opaque éclairé, teinté.</summary>
        public static Material Mat(Color c, float smooth = 0.2f, float metal = 0f)
        {
            string key = "L" + Key(c) + "|" + smooth.ToString("0.00") + "|" + metal.ToString("0.00");
            if (mats.TryGetValue(key, out Material m) && m != null) return m;
            m = new Material(litSrc) { color = c };
            Surface(m, smooth, metal);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
            mats[key] = m;
            return m;
        }

        /// <summary>Matériau qui brille (néons, lampadaires, gilets fluo). L'émission au-dessus de 1 nourrit le bloom.</summary>
        public static Material Glow(Color baseCol, Color emission, float smooth = 0.3f)
        {
            string key = "E" + Key(baseCol) + "|" + Key(emission) + "|" + smooth.ToString("0.00");
            if (mats.TryGetValue(key, out Material m) && m != null) return m;
            m = new Material(emisSrc) { color = baseCol };
            Surface(m, smooth, 0f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            mats[key] = m;
            return m;
        }

        /// <summary>Matériau texturé, avec carte d'émission optionnelle (fenêtres allumées).</summary>
        public static Material Tex(Texture2D albedo, Vector2 tiling, float smooth, Texture2D emisMap = null, Color emis = default)
        {
            string key = "T" + albedo.name + "|" + tiling.x.ToString("0.00") + "x" + tiling.y.ToString("0.00") + "|" + smooth.ToString("0.00") + "|" + (emisMap != null ? emisMap.name : "-") + "|" + Key(emis);
            if (mats.TryGetValue(key, out Material m) && m != null) return m;
            m = new Material(emisMap != null ? emisSrc : litSrc) { color = Color.white };
            m.mainTexture = albedo;
            m.mainTextureScale = tiling;
            Surface(m, smooth, 0f);
            if (emisMap != null)
            {
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", emisMap);
                m.SetColor("_EmissionColor", emis);
            }
            else if (m.HasProperty("_EmissionColor"))
            {
                m.SetColor("_EmissionColor", Color.black);
            }
            mats[key] = m;
            return m;
        }

        /// <summary>Matériau de particule / halo (alpha, couleurs de sommets).</summary>
        public static Material Particle(Texture2D tex, Color tint)
        {
            string key = "P" + (tex != null ? tex.name : "-") + "|" + Key(tint);
            if (mats.TryGetValue(key, out Material m) && m != null) return m;
            m = new Material(partSrc);
            if (tex != null)
            {
                m.mainTexture = tex;
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            }
            m.color = tint;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            mats[key] = m;
            return m;
        }

        static void Surface(Material m, float smooth, float metal)
        {
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
        }

        // ---------------------------------------------------------------- construction

        public static Transform Node(Transform parent, string name, Vector3 pos)
        {
            Transform t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        public static MeshRenderer Part(Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, Vector3 euler = default, bool shadows = true)
        {
            GameObject go = new GameObject(mesh.name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return r;
        }

        static Material outlineMat;
        static bool outlineChecked;

        /// <summary>Matériau des contours cartoon : noir, faces avant masquées (coque inversée). Null si la pipeline ne le permet pas.</summary>
        public static Material OutlineMat
        {
            get
            {
                if (outlineChecked) return outlineMat;
                outlineChecked = true;
                if (litSrc == null || !litSrc.HasProperty("_Cull")) return null;
                outlineMat = new Material(litSrc) { color = new Color(0.03f, 0.03f, 0.05f), name = "Outline" };
                Surface(outlineMat, 0f, 0f);
                if (outlineMat.HasProperty("_EmissionColor")) outlineMat.SetColor("_EmissionColor", Color.black);
                outlineMat.SetFloat("_Cull", 1f); // Front : seul le dos de la coque grossie reste visible
                if (outlineMat.HasProperty("_SpecularHighlights")) outlineMat.SetFloat("_SpecularHighlights", 0f);
                if (outlineMat.HasProperty("_EnvironmentReflections")) outlineMat.SetFloat("_EnvironmentReflections", 0f);
                return outlineMat;
            }
        }

        /// <summary>
        /// Contour noir cartoon autour de chaque pièce d'un objet (coque inversée légèrement grossie).
        /// Les petits détails (yeux, bandes, badges : pièces sans ombre) n'en ont pas, pour rester lisibles.
        /// </summary>
        static readonly Dictionary<string, Material> tintedOutlines = new Dictionary<string, Material>();

        /// <summary>
        /// Contour coloré (codes couleur du jeu) : rouge = danger/ennemi, vert = soin, bleu = allié, or = à ramasser.
        /// Même coque inversée que le contour noir, mais lumineuse pour se lire de loin.
        /// </summary>
        public static Material OutlineTint(Color c)
        {
            Material baseM = OutlineMat;
            if (baseM == null) return null;
            string key = Key(c);
            if (tintedOutlines.TryGetValue(key, out Material m) && m != null) return m;
            m = new Material(baseM) { color = c, name = "OutlineTint" };
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * 1.4f);
            tintedOutlines[key] = m;
            return m;
        }

        public static void Outline(Transform root, float width, Material tint = null)
        {
            Material om = tint != null ? tint : OutlineMat;
            if (om == null || root == null) return;
            MeshRenderer[] rs = root.GetComponentsInChildren<MeshRenderer>(true);
            foreach (MeshRenderer r in rs)
            {
                if (r.shadowCastingMode == ShadowCastingMode.Off) continue;
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf == null) continue;
                Mesh m = mf.sharedMesh;
                if (m != Cube && m != Sphere && m != Capsule && m != Cylinder) continue;
                Vector3 ls = r.transform.localScale;
                Vector3 b = m.bounds.size;
                if (Mathf.Min(ls.x * b.x, Mathf.Min(ls.y * b.y, ls.z * b.z)) < 0.06f) continue;
                Vector3 k = new Vector3(1f + 2f * width / Mathf.Max(0.01f, ls.x * b.x),
                                        1f + 2f * width / Mathf.Max(0.01f, ls.y * b.y),
                                        1f + 2f * width / Mathf.Max(0.01f, ls.z * b.z));
                GameObject go = new GameObject("ol");
                go.transform.SetParent(r.transform, false);
                go.transform.localScale = k;
                go.AddComponent<MeshFilter>().sharedMesh = m;
                MeshRenderer o = go.AddComponent<MeshRenderer>();
                o.sharedMaterial = om;
                o.shadowCastingMode = ShadowCastingMode.Off;
                o.receiveShadows = false;
            }
        }

        /// <summary>Quad posé au sol (face vers le haut).</summary>
        public static MeshRenderer Decal(Transform parent, Material mat, Vector3 pos, Vector2 size)
        {
            MeshRenderer r = Part(parent, Quad, mat, pos, new Vector3(size.x, size.y, 1f), new Vector3(90f, 0f, 0f), false);
            r.receiveShadows = false;
            return r;
        }

        // ---------------------------------------------------------------- textures

        static Texture2D NewTex(string name, int w, int h, bool repeat)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4
            };
        }

        static Texture2D Finish(Texture2D t, Color32[] px)
        {
            t.SetPixels32(px);
            t.Apply(true, true);
            texCache[t.name] = t;
            return t;
        }

        public static Texture2D Radial(string name, int n, float hard, float power)
        {
            if (texCache.TryGetValue(name, out Texture2D cached)) return cached;
            Texture2D t = NewTex(name, n, n, false);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = d >= 1f ? 0f : (d <= hard ? 1f : Mathf.Pow(1f - (d - hard) / (1f - hard), power));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            return Finish(t, px);
        }

        public static Texture2D SoftDot => Radial("softdot", 64, 0f, 1.8f);

        /// <summary>Ombre de contact (tache sombre en fondu) : mélange alpha classique — le matériau « particule » du jeu est additif,
        /// une tache noire y restait invisible (personnages qui semblaient flotter).</summary>
        public static Material Shadow(float alpha)
        {
            string key = "SH|" + alpha.ToString("0.00");
            if (mats.TryGetValue(key, out Material m) && m != null) return m;
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) return Particle(SoftDot, new Color(0f, 0f, 0f, alpha));
            m = new Material(sh);
            m.mainTexture = SoftDot;
            m.color = new Color(0f, 0f, 0f, alpha);
            m.renderQueue = 3050;   // après le sol et les décors au sol
            mats[key] = m;
            return m;
        }
        public static Texture2D HardDot => Radial("harddot", 32, 0.75f, 1f);

        public static Texture2D SmokeTex
        {
            get
            {
                const string name = "smoke";
                if (texCache.TryGetValue(name, out Texture2D cached)) return cached;
                const int n = 64;
                Texture2D t = NewTex(name, n, n, false);
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float noise = Mathf.PerlinNoise(x * 0.11f + 3.1f, y * 0.11f + 7.7f) * 0.6f + Mathf.PerlinNoise(x * 0.27f, y * 0.27f) * 0.4f;
                    float a = Mathf.Clamp01((1f - d) * 1.6f) * (0.45f + 0.55f * noise);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
                return Finish(t, px);
            }
        }

        /// <summary>Fissures de vitrine brisée (traits blancs rayonnant d'un impact), fond transparent.</summary>
        public static Texture2D Cracks
        {
            get
            {
                const string name = "cracks";
                if (texCache.TryGetValue(name, out Texture2D cached)) return cached;
                const int n = 128;
                Texture2D t = NewTex(name, n, n, false);
                var px = new Color32[n * n];
                var rnd = new System.Random(7);
                Vector2 c = new Vector2(n * 0.45f, n * 0.55f);
                for (int r = 0; r < 14; r++)
                {
                    float a = r / 14f * Mathf.PI * 2f + (float)rnd.NextDouble() * 0.3f;
                    Vector2 p = c;
                    for (int seg = 0; seg < 5; seg++)
                    {
                        a += ((float)rnd.NextDouble() - 0.5f) * 0.6f;
                        Vector2 q = p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (8f + (float)rnd.NextDouble() * 10f);
                        CrackLine(px, n, p.x, p.y, q.x, q.y, 1f - seg * 0.12f);
                        p = q;
                    }
                }
                for (int ring = 1; ring <= 3; ring++)
                {
                    float rr = ring * 11f;
                    for (int k = 0; k < 9; k++)
                    {
                        float a0 = k / 9f * Mathf.PI * 2f, a1 = (k + 0.7f) / 9f * Mathf.PI * 2f;
                        CrackLine(px, n, c.x + Mathf.Cos(a0) * rr, c.y + Mathf.Sin(a0) * rr, c.x + Mathf.Cos(a1) * rr, c.y + Mathf.Sin(a1) * rr, 0.7f);
                    }
                }
                return Finish(t, px);
            }
        }

        static void CrackLine(Color32[] px, int n, float x0, float y0, float x1, float y1, float w)
        {
            int steps = (int)(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) * 2f) + 1;
            for (int i = 0; i <= steps; i++)
            {
                float u = i / (float)steps;
                int cx = Mathf.RoundToInt(Mathf.Lerp(x0, x1, u)), cy = Mathf.RoundToInt(Mathf.Lerp(y0, y1, u));
                for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    int x = cx + ox, y = cy + oy;
                    if (x < 0 || y < 0 || x >= n || y >= n) continue;
                    float a = Mathf.Clamp01(w - Mathf.Sqrt(ox * ox + oy * oy) * 0.6f);
                    int k = y * n + x;
                    px[k] = new Color32(255, 255, 255, (byte)Mathf.Max(px[k].a, a * 255f));
                }
            }
        }

        /// <summary>Éclats de verre au sol : petits triangles clairs dispersés, fond transparent.</summary>
        public static Texture2D Shards
        {
            get
            {
                const string name = "shards";
                if (texCache.TryGetValue(name, out Texture2D cached)) return cached;
                const int n = 128;
                Texture2D t = NewTex(name, n, n, false);
                var px = new Color32[n * n];
                var rnd = new System.Random(11);
                for (int i = 0; i < 70; i++)
                {
                    float cx = (float)rnd.NextDouble() * n, cy = (float)rnd.NextDouble() * n;
                    float dx = cx - n * 0.5f, dy = cy - n * 0.5f;
                    if (dx * dx + dy * dy > n * n * 0.2f) continue;
                    float sz = 2f + (float)rnd.NextDouble() * 4f;
                    for (int y = (int)(cy - sz); y <= cy + sz; y++)
                    for (int x = (int)(cx - sz); x <= cx + sz; x++)
                    {
                        if (x < 0 || y < 0 || x >= n || y >= n) continue;
                        if (Mathf.Abs(x - cx) + Mathf.Abs(y - cy) * 1.6f > sz) continue;
                        byte v = (byte)(200 + rnd.Next(55));
                        px[y * n + x] = new Color32(v, v, 255, 230);
                    }
                }
                return Finish(t, px);
            }
        }

        static float TorusNoise(int x, int y, int n, float freq, float ox, float oy)
        {
            float u = x / (float)n * Mathf.PI * 2f, v = y / (float)n * Mathf.PI * 2f;
            return Mathf.PerlinNoise(ox + Mathf.Cos(u) * freq + Mathf.Sin(v) * 0.31f * freq, oy + Mathf.Sin(u) * freq + Mathf.Cos(v) * freq);
        }

        /// <summary>Bitume mouillé : bruit tuilable + gravillons.</summary>
        public static Texture2D Asphalt
        {
            get
            {
                const string name = "asphalt";
                if (texCache.TryGetValue(name, out Texture2D cached)) return cached;
                const int n = 256;
                Texture2D t = NewTex(name, n, n, true);
                var px = new Color32[n * n];
                var rng = new System.Random(7);
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float a = TorusNoise(x, y, n, 1.2f, 11f, 5f) * 0.6f + TorusNoise(x, y, n, 4f, 2f, 9f) * 0.4f;
                    float g = 0.19f + a * 0.08f + (float)rng.NextDouble() * 0.05f;
                    if (rng.NextDouble() < 0.02) g += 0.08f;
                    byte b = (byte)(Mathf.Clamp01(g) * 255f);
                    px[y * n + x] = new Color32(b, b, (byte)Mathf.Min(255, b + 6), 255);
                }
                return Finish(t, px);
            }
        }

        /// <summary>Dalles de trottoir.</summary>
        public static Texture2D Slabs
        {
            get
            {
                const string name = "slabs";
                if (texCache.TryGetValue(name, out Texture2D cached)) return cached;
                const int n = 128;
                Texture2D t = NewTex(name, n, n, true);
                var px = new Color32[n * n];
                var rng = new System.Random(3);
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    bool joint = (x % 64) < 2 || (y % 64) < 2;
                    float g = joint ? 0.32f : 0.56f + TorusNoise(x, y, n, 2f, 4f, 1f) * 0.08f + (float)rng.NextDouble() * 0.03f;
                    byte b = (byte)(Mathf.Clamp01(g) * 255f);
                    px[y * n + x] = new Color32(b, b, (byte)Mathf.Min(255, b + 4), 255);
                }
                return Finish(t, px);
            }
        }

        /// <summary>Façade haussmannienne : 3 travées, N étages, fenêtres allumées au hasard (carte d'émission).</summary>
        public struct Facade
        {
            public Texture2D albedo, emission;
            public int floors;
        }

        static List<Facade> facades;
        static List<Facade> shops;

        public static Facade GetFacade(int i)
        {
            if (facades == null) BuildFacades();
            return facades[((i % facades.Count) + facades.Count) % facades.Count];
        }

        public static Facade GetShop(int i)
        {
            if (shops == null) BuildShops();
            return shops[((i % shops.Count) + shops.Count) % shops.Count];
        }

        public static int FacadeCount => 6;
        public static int ShopCount => 6;

        static void BuildFacades()
        {
            facades = new List<Facade>();
            Color[] stones =
            {
                new Color(0.86f, 0.81f, 0.71f), new Color(0.8f, 0.75f, 0.64f), new Color(0.9f, 0.86f, 0.77f),
                new Color(0.76f, 0.7f, 0.6f), new Color(0.84f, 0.8f, 0.74f), new Color(0.72f, 0.68f, 0.62f)
            };
            for (int i = 0; i < 6; i++) facades.Add(MakeFacade("facade" + i, i % 2 == 0 ? 4 : 5, stones[i], i * 17 + 3, i % 3 == 1));
        }

        static Facade MakeFacade(string name, int floors, Color stone, int seed, bool shutters)
        {
            const int CW = 64, CH = 80, cols = 3;
            int W = cols * CW, H = floors * CH;
            Texture2D ta = NewTex(name, W, H, false);
            Texture2D te = NewTex(name + "_e", W, H, false);
            var a = new Color32[W * H];
            var e = new Color32[W * H];
            var rng = new System.Random(seed);
            Color frame = new Color(0.93f, 0.91f, 0.86f);
            Color iron = new Color(0.08f, 0.08f, 0.09f);
            Color shutter = new Color(0.22f, 0.32f, 0.27f);
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.07f + seed, y * 0.07f) * 0.08f;
                Color c = stone * (0.94f + n);
                if (y % 10 == 0) c *= 0.93f;
                if (y % CH < 5) c = Color.Lerp(c, Color.white, 0.18f);
                if (y > H - 7) c = Color.Lerp(c, Color.white, 0.22f);
                c.a = 1f;
                a[y * W + x] = c;
                e[y * W + x] = new Color32(0, 0, 0, 255);
            }
            for (int f = 0; f < floors; f++)
            for (int col = 0; col < cols; col++)
            {
                int x0 = col * CW + 17, x1 = x0 + 30, y0 = f * CH + 14, y1 = y0 + 52;
                bool lit = rng.NextDouble() < 0.4;
                Color warm = rng.NextDouble() < 0.7 ? new Color(1f, 0.74f, 0.42f) : new Color(0.75f, 0.85f, 1f);
                for (int y = y0 - 2; y <= y1 + 2; y++)
                for (int x = x0 - 2; x <= x1 + 2; x++)
                {
                    int i = y * W + x;
                    bool inner = x >= x0 && x <= x1 && y >= y0 && y <= y1;
                    if (!inner) { a[i] = frame; continue; }
                    bool mullion = x == (x0 + x1) / 2 || y == y0 + 34;
                    if (mullion) { a[i] = frame * 0.95f; continue; }
                    float v = (y - y0) / (float)(y1 - y0);
                    if (lit)
                    {
                        bool curtain = x < x0 + 5 || x > x1 - 5;
                        Color c = curtain ? warm * 0.72f : warm * (0.75f + 0.25f * v);
                        a[i] = c;
                        e[i] = curtain ? warm * 0.45f : warm * (0.6f + 0.4f * v);
                    }
                    else
                    {
                        float refl = ((x - x0) + (y - y0)) % 24 < 4 ? 0.06f : 0f;
                        a[i] = new Color(0.1f + refl, 0.13f + refl, 0.19f + refl);
                    }
                }
                if (shutters)
                {
                    for (int y = y0; y <= y1; y++)
                    for (int x = x0 - 10; x < x0 - 3; x++)
                    {
                        a[y * W + x] = (y % 4 == 0) ? shutter * 0.8f : shutter;
                        a[y * W + (x1 + 3 + (x - (x0 - 10)))] = (y % 4 == 0) ? shutter * 0.8f : shutter;
                    }
                }
                if (f == 1 || f == floors - 1)
                {
                    for (int y = y0 - 1; y < y0 + 12; y++)
                    for (int x = x0 - 6; x <= x1 + 6; x++)
                    {
                        bool bar = y == y0 - 1 || y == y0 + 11 || (x % 3 == 0);
                        if (bar) { a[y * W + x] = iron; e[y * W + x] = new Color32(0, 0, 0, 255); }
                    }
                }
            }
            Finish(ta, a);
            Finish(te, e);
            return new Facade { albedo = ta, emission = te, floors = floors };
        }

        static void BuildShops()
        {
            shops = new List<Facade>();
            Color[] signs =
            {
                new Color(0.55f, 0.18f, 0.15f), new Color(0.18f, 0.36f, 0.3f), new Color(0.2f, 0.25f, 0.42f),
                new Color(0.45f, 0.32f, 0.18f), new Color(0.3f, 0.2f, 0.35f), new Color(0.15f, 0.15f, 0.17f)
            };
            for (int i = 0; i < 6; i++) shops.Add(MakeShop("shop" + i, signs[i], i == 2 || i == 5, i * 31 + 9));
        }

        /// <summary>Rez-de-chaussée : enseigne + vitrine éclairée, ou vitrine condamnée (contreplaqué).</summary>
        static Facade MakeShop(string name, Color sign, bool boarded, int seed)
        {
            const int W = 192, H = 86;
            Texture2D ta = NewTex(name, W, H, false);
            Texture2D te = NewTex(name + "_e", W, H, false);
            var a = new Color32[W * H];
            var e = new Color32[W * H];
            var rng = new System.Random(seed);
            Color glow = rng.NextDouble() < 0.5 ? new Color(1f, 0.86f, 0.62f) : new Color(0.8f, 0.92f, 1f);
            Color ply = new Color(0.72f, 0.56f, 0.36f);
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                Color c;
                Color em = Color.black;
                if (y >= H - 16)
                {
                    c = sign;
                    bool letters = y > H - 12 && y < H - 4 && x > 30 && x < W - 30 && ((x / 5) % 3 != 2);
                    if (letters) { c = new Color(0.95f, 0.9f, 0.75f); em = c * 0.8f; }
                }
                else if (y < 4 || x < 6 || x > W - 7) c = new Color(0.2f, 0.2f, 0.22f);
                else if (x > 78 && x < 112) c = boarded ? ply * 0.9f : new Color(0.12f, 0.1f, 0.09f);
                else if (boarded)
                {
                    c = ply * (0.9f + Mathf.PerlinNoise(x * 0.05f, y * 0.4f) * 0.2f);
                    if (y % 22 == 0) c *= 0.7f;
                    if ((x * 7 + y * 3 + seed) % 97 == 0) c = new Color(0.86f, 0.2f, 0.15f);
                }
                else
                {
                    bool mullion = x % 36 < 2 || y == 40;
                    float v = y / (float)H;
                    c = mullion ? new Color(0.15f, 0.15f, 0.17f) : glow * (0.65f + 0.35f * v);
                    if (!mullion)
                    {
                        em = glow * (0.55f + 0.45f * v);
                        if (y < 22 && (x / 9) % 2 == 0) { c *= 0.55f; em *= 0.4f; }
                    }
                }
                c.a = 1f;
                em.a = 1f;
                a[i] = c;
                e[i] = em;
            }
            Finish(ta, a);
            Finish(te, e);
            return new Facade { albedo = ta, emission = te, floors = 1 };
        }

        /// <summary>Dégradé vertical (halo de la ville au loin).</summary>
        public static Texture2D SkyGlow
        {
            get
            {
                const string name = "skyglow";
                if (texCache.TryGetValue(name, out Texture2D cached)) return cached;
                Texture2D t = NewTex(name, 4, 64, false);
                var px = new Color32[4 * 64];
                for (int y = 0; y < 64; y++)
                {
                    float v = y / 63f;
                    float a = Mathf.Pow(1f - v, 2.2f);
                    for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
                return Finish(t, px);
            }
        }
        // ---------------------------------------------------------------- centre commercial

        /// <summary>Carrelage clair et brillant du centre commercial (dalles de 1,2 m).</summary>
        public static Texture2D Tiles
        {
            get
            {
                const string name = "tiles";
                if (texCache.TryGetValue(name, out Texture2D cached)) return cached;
                const int n = 128;
                Texture2D t = NewTex(name, n, n, true);
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    bool joint = (x % 64) < 2 || (y % 64) < 2;
                    bool alt = ((x / 64) + (y / 64)) % 2 == 0;
                    float g = joint ? 0.62f : (alt ? 0.93f : 0.86f) + TorusNoise(x, y, n, 3f, 2f, 7f) * 0.04f;
                    Color c = joint ? new Color(g, g, g) : new Color(g, g * 0.985f, g * 0.95f);
                    c.a = 1f;
                    px[y * n + x] = c;
                }
                return Finish(t, px);
            }
        }

        static List<Facade> stores;
        public static int StoreCount => 8;

        public static Facade GetStore(int i)
        {
            if (stores == null) BuildStores();
            return stores[((i % stores.Count) + stores.Count) % stores.Count];
        }

        static void BuildStores()
        {
            stores = new List<Facade>();
            Color[] signs =
            {
                new Color(0.9f, 0.2f, 0.25f), new Color(0.15f, 0.5f, 0.95f), new Color(0.98f, 0.62f, 0.1f), new Color(0.2f, 0.75f, 0.45f),
                new Color(0.65f, 0.3f, 0.9f), new Color(0.1f, 0.75f, 0.8f), new Color(0.95f, 0.35f, 0.65f), new Color(0.15f, 0.15f, 0.18f)
            };
            for (int i = 0; i < 8; i++) stores.Add(MakeStore("store" + i, signs[i], i, i * 37 + 5));
        }

        /// <summary>Boutique : enseigne lumineuse, vitrine éclairée avec produits, affiches SALE rouges.</summary>
        static Facade MakeStore(string name, Color sign, int kind, int seed)
        {
            const int W = 192, H = 96;
            Texture2D ta = NewTex(name, W, H, false);
            Texture2D te = NewTex(name + "_e", W, H, false);
            var a = new Color32[W * H];
            var e = new Color32[W * H];
            var rng = new System.Random(seed);
            Color frame = new Color(0.82f, 0.84f, 0.86f);
            Color glass = new Color(0.96f, 0.95f, 0.9f);
            Color[] products = { new Color(0.95f, 0.3f, 0.3f), new Color(0.3f, 0.55f, 0.95f), new Color(0.98f, 0.8f, 0.2f), new Color(0.35f, 0.8f, 0.5f), new Color(0.9f, 0.9f, 0.92f), new Color(0.2f, 0.2f, 0.25f) };
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                Color c, em = Color.black;
                if (y >= H - 20)
                {
                    c = sign;
                    em = sign * 0.9f;
                    bool letters = y > H - 16 && y < H - 5 && x > 36 && x < W - 36 && ((x / 6) % 4 != 3);
                    if (letters) { c = Color.white; em = Color.white * 1.2f; }
                }
                else if (y < 4 || x < 5 || x > W - 6 || (y >= H - 24)) c = frame;
                else if (x > 82 && x < 110) { c = new Color(0.75f, 0.85f, 0.9f); em = new Color(0.4f, 0.45f, 0.5f); if (x == 96) c = frame; }
                else
                {
                    float v = y / (float)H;
                    c = glass * (0.88f + 0.12f * v);
                    em = glass * 0.75f;
                }
                c.a = 1f; em.a = 1f;
                a[i] = c; e[i] = em;
            }
            // produits en vitrine : étagères ou portants
            for (int side = 0; side < 2; side++)
            {
                int x0 = side == 0 ? 10 : 116, x1 = side == 0 ? 78 : 186;
                for (int shelf = 0; shelf < 3; shelf++)
                {
                    int sy = 8 + shelf * 21;
                    for (int x = x0; x < x1; x++) { a[sy * W + x] = frame * 0.7f; e[sy * W + x] = Color.black; }
                    int px0 = x0 + 2;
                    while (px0 < x1 - 6)
                    {
                        int w = 6 + rng.Next(8), h = 8 + rng.Next(10);
                        Color pc = kind == 7 ? products[4 + rng.Next(2)] : products[rng.Next(products.Length)];
                        for (int y = sy + 1; y < sy + 1 + h && y < H - 26; y++)
                            for (int x = px0; x < px0 + w && x < x1; x++) { a[y * W + x] = pc; e[y * W + x] = pc * 0.35f; }
                        px0 += w + 2 + rng.Next(3);
                    }
                }
                // affiche SALE
                int ay = 52 + rng.Next(6), ax = x0 + 8 + rng.Next(30);
                for (int y = ay; y < ay + 16; y++)
                    for (int x = ax; x < ax + 26; x++)
                    {
                        bool txt = y > ay + 4 && y < ay + 12 && x > ax + 3 && x < ax + 23 && (x - ax) % 5 != 0;
                        Color pc = txt ? Color.white : new Color(0.92f, 0.12f, 0.15f);
                        a[y * W + x] = pc; e[y * W + x] = pc * 0.9f;
                    }
            }
            Finish(ta, a);
            Finish(te, e);
            return new Facade { albedo = ta, emission = te, floors = 1 };
        }
    }
}
