using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SamediRouge
{
    /// <summary>
    /// Carte du quartier en vue aérienne : toits colorés, parcs, vraies rues (trottoirs, chaussée, marquage, passages
    /// piétons, voitures). Chaque niveau est une rue du parcours :
    /// prise = bordée de bleu avec fanions ; à prendre = rubalise jaune et noire qui défile, voiture de police au gyrophare ;
    /// verrouillée = barrière « ROAD CLOSED ». Une étiquette par rue (numéro, nom, état).
    /// La géométrie est écrite dans le repère de la maquette (560 × 480, y vers le bas) puis convertie.
    /// </summary>
    public partial class Hud
    {
        const int MapW = 500, MapH = 440;
        const float PanelInnerW = 980f, PanelInnerH = 860f, MockW = 560f, MockH = 480f;

        // ---------------------------------------------------------------- géométrie des parcours (repère maquette)

        static readonly Vector2[][] Route5 =
        {
            new[] { new Vector2(40, 420), new Vector2(300, 420) },
            new[] { new Vector2(300, 420), new Vector2(300, 250) },
            new[] { new Vector2(300, 250), new Vector2(90, 250) },
            Bezier(new Vector2(90, 250), new Vector2(90, 150), new Vector2(200, 110), new Vector2(330, 110), 10),
            new[] { new Vector2(330, 110), new Vector2(482, 110) },
        };
        static readonly Vector2[] Label5 = { new Vector2(150, 462), new Vector2(395, 335), new Vector2(195, 298), new Vector2(190, 118), new Vector2(410, 70) };
        static readonly Vector2[] Plaza5 = { new Vector2(515, 110) };
        static readonly float[] GridX5 = { 170, 450 }, GridY5 = { 180, 40 };

        static readonly Vector2[][] Route3 =
        {
            new[] { new Vector2(40, 380), new Vector2(280, 380) },
            new[] { new Vector2(280, 380), new Vector2(280, 200) },
            Bezier(new Vector2(280, 200), new Vector2(280, 120), new Vector2(380, 100), new Vector2(482, 100), 10),
        };
        static readonly Vector2[] Label3 = { new Vector2(160, 422), new Vector2(380, 292), new Vector2(390, 150) };
        static readonly Vector2[] Plaza3 = { new Vector2(515, 100) };
        static readonly float[] GridX3 = { 140, 420 }, GridY3 = { 250, 30 };

        static readonly Vector2[][] Route4 =
        {
            new[] { new Vector2(40, 410), new Vector2(240, 410) },
            new[] { new Vector2(240, 410), new Vector2(240, 240) },
            new[] { new Vector2(240, 240), new Vector2(440, 240) },
            Bezier(new Vector2(440, 240), new Vector2(440, 160), new Vector2(460, 100), new Vector2(482, 100), 10),
        };
        static readonly Vector2[] Label4 = { new Vector2(140, 452), new Vector2(330, 330), new Vector2(340, 285), new Vector2(345, 140) };
        static readonly Vector2[] Plaza4 = { new Vector2(515, 100) };
        static readonly float[] GridX4 = { 130, 350 }, GridY4 = { 150, 40 };

        // ---------------------------------------------------------------- parcours relevés sur les fonds illustrés
        // (repère maquette, image 1254 px : x = X × 560/1254, y = (Y − 75) × 480/1103). Le fond dessine déjà la route :
        // on n'y pose que les couleurs d'état, les rubans, la barrière et les étiquettes.
        static readonly Dictionary<int, Vector2[][]> ArtRoute = new Dictionary<int, Vector2[][]>
        {
            [0] = new[]
            {
                new[] { new Vector2(8, 373), new Vector2(304, 373) },
                new[] { new Vector2(304, 373), new Vector2(304, 187) },
                new[] { new Vector2(304, 187), new Vector2(134, 187) },
                Bezier(new Vector2(134, 187), new Vector2(134, 100), new Vector2(160, 60), new Vector2(210, 60), 10),
                new[] { new Vector2(210, 60), new Vector2(522, 60) },
            },
            // Shopping District (4 rues) : zigzag en diagonales, fontaine en haut à droite
            [1] = new[]
            {
                new[] { new Vector2(5, 422), new Vector2(188, 252) },
                new[] { new Vector2(188, 252), new Vector2(298, 356) },
                new[] { new Vector2(298, 356), new Vector2(480, 95) },
                Bezier(new Vector2(480, 95), new Vector2(493, 72), new Vector2(502, 45), new Vector2(510, 28), 8),
            },
            // Central Station (4 rues) : grand Z, la place de la gare en bas à droite
            [2] = new[]
            {
                Join(new[] { new Vector2(0, 16), new Vector2(429, 16) }, Bezier(new Vector2(429, 16), new Vector2(462, 16), new Vector2(460, 37), new Vector2(430, 55), 6)),
                Join(new[] { new Vector2(430, 55), new Vector2(306, 125), new Vector2(215, 200), new Vector2(127, 280) }, Bezier(new Vector2(127, 280), new Vector2(89, 311), new Vector2(58, 354), new Vector2(89, 354), 6)),
                new[] { new Vector2(89, 354), new Vector2(357, 354) },
                Bezier(new Vector2(357, 354), new Vector2(402, 354), new Vector2(420, 386), new Vector2(469, 420), 8),
            },
            // Harbor Docks (4 rues, tracé en escalier ; la mer en haut à gauche)
            [3] = new[]
            {
                new[] { new Vector2(8, 379), new Vector2(317, 379) },
                new[] { new Vector2(317, 379), new Vector2(317, 209) },
                new[] { new Vector2(317, 209), new Vector2(476, 209) },
                Join(new[] { new Vector2(476, 209), new Vector2(476, 122) }, Bezier(new Vector2(476, 122), new Vector2(476, 88), new Vector2(497, 76), new Vector2(524, 77), 8)),
            },
            // Uptown (5 rues) : spirale — bas, gauche, haut, droite, puis le boulevard vers la tour au centre
            [4] = new[]
            {
                new[] { new Vector2(451, 456), new Vector2(85, 456), new Vector2(75, 455), new Vector2(66, 453), new Vector2(60, 448), new Vector2(55, 442), new Vector2(54, 433) },
                new[] { new Vector2(54, 433), new Vector2(54, 41) },
                new[] { new Vector2(54, 41), new Vector2(55, 32), new Vector2(60, 24), new Vector2(66, 18), new Vector2(75, 14), new Vector2(85, 13), new Vector2(478, 13) },
                new[] { new Vector2(478, 13), new Vector2(488, 14), new Vector2(495, 18), new Vector2(501, 24), new Vector2(504, 32), new Vector2(505, 41), new Vector2(505, 159) },
                new[] { new Vector2(505, 159), new Vector2(502, 174), new Vector2(495, 184), new Vector2(484, 190), new Vector2(468, 193), new Vector2(450, 194), new Vector2(429, 194), new Vector2(411, 195), new Vector2(392, 199), new Vector2(371, 205), new Vector2(347, 212), new Vector2(319, 220), new Vector2(286, 228) },
            },
        };
        static readonly Dictionary<int, Vector2[]> ArtLabels = new Dictionary<int, Vector2[]>
        {
            [0] = new[] { new Vector2(150, 415), new Vector2(400, 290), new Vector2(205, 232), new Vector2(205, 110), new Vector2(390, 102) },
            [1] = new[] { new Vector2(60, 315), new Vector2(250, 352), new Vector2(440, 300), new Vector2(400, 45) },
            [2] = new[] { new Vector2(250, 48), new Vector2(130, 190), new Vector2(214, 385), new Vector2(339, 437) },
            [3] = new[] { new Vector2(150, 421), new Vector2(205, 290), new Vector2(395, 250), new Vector2(370, 140) },
            [4] = new[] { new Vector2(250, 429), new Vector2(116, 228), new Vector2(250, 41), new Vector2(447, 98), new Vector2(424, 237) },
        };

        static Vector2[] Join(Vector2[] a, Vector2[] b)
        {
            var r = new Vector2[a.Length + b.Length - 1];
            a.CopyTo(r, 0);
            for (int i = 1; i < b.Length; i++) r[a.Length + i - 1] = b[i];
            return r;
        }

        static Vector2[][] RouteOf(int count) => count == 3 ? Route3 : count == 4 ? Route4 : Route5;
        static Vector2[] LabelsOf(int count) => count == 3 ? Label3 : count == 4 ? Label4 : Label5;

        static Vector2[] Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, int n)
        {
            var p = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n, u = 1f - t;
                p[i] = a * (u * u * u) + b * (3f * u * u * t) + c * (3f * u * t * t) + d * (t * t * t);
            }
            return p;
        }

        /// <summary>Maquette → panneau (UI, centre du panneau, y vers le haut).</summary>
        /// <summary>Bandeau du haut du panneau (compteur de rues, état du quartier) ; la carte est dessinée dessous.</summary>
        public const float MapHeadH = 74f, MapPanelH = 880f + MapHeadH;
        const float MapShiftY = -MapHeadH * 0.5f;

        static void MapArea(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(0.5f, 0.5f);
            r.offsetMin = new Vector2(10f, 10f); r.offsetMax = new Vector2(-10f, -10f - MapHeadH);
        }

        static Vector2 ToUi(Vector2 m) => new Vector2(-PanelInnerW * 0.5f + m.x / MockW * PanelInnerW, PanelInnerH * 0.5f - m.y / MockH * PanelInnerH);
        /// <summary>Maquette → texture de fond (pixels, y vers le haut).</summary>
        static Vector2 ToTex(Vector2 m) => new Vector2(m.x / MockW * MapW, MapH - m.y / MockH * MapH);
        static float UiScale => PanelInnerW / MockW;

        // ---------------------------------------------------------------- objets

        static readonly Dictionary<int, Sprite> citySprites = new Dictionary<int, Sprite>();
        static Sprite disc;
        static Texture2D solidTex, tapeTex, stripeTex;

        Image mapCity, mapArt;
        static readonly string[] MapArt = { "map_downtown", "map_shopping", "map_station", "map_docks", "map_uptown" };
        RectTransform streetRoot;
        int streetLayout = -1;
        RectTransform[] stBox;
        Text[] stNum;
        Image[] stSubBg;
        readonly Image[] stBorder = new Image[5];
        /// <summary>Par rue : chaussée teintée (segments + jointures), bords (rubalise ou liseré), barrière, voiture de police.</summary>
        readonly List<Graphic>[] stBody = new List<Graphic>[5], stEdge = new List<Graphic>[5];
        readonly RawImage[][] stTape = new RawImage[5][];
        readonly GameObject[] stBarrier = new GameObject[5], stCop = new GameObject[5];
        readonly Image[][] stLights = new Image[5][];
        readonly int[] stState = new int[5];
        readonly float[] stFillF = new float[5];

        // ---------------------------------------------------------------- construction

        void BuildCityMap(RectTransform pr)
        {
            if (disc == null) disc = MakeDisc();
            if (solidTex == null) MakeStreetTextures();
            // fond illustré du quartier (Resources/SamediRouge/Art/map_*.png) sous le plan des rues
            // la carte occupe le panneau SOUS le bandeau du haut (compteur de rues) : rien n'est plus caché ni coupé
            mapArt = Img(pr, null, Color.white);
            MapArea(mapArt.rectTransform);
            mapArt.enabled = false;
            mapCity = Img(pr, null, Color.white);
            MapArea(mapCity.rectTransform);
            streetRoot = New("streets", pr);
            Place(streetRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, MapShiftY), new Vector2(PanelInnerW, PanelInnerH));
            streetRoot.gameObject.AddComponent<RectMask2D>();   // les rues ne débordent plus du cadre

            stBox = new RectTransform[SlotCount];
            stBg = new Image[SlotCount];
            stGlow = new Image[SlotCount];
            stFill = new Image[SlotCount];
            stName = new Text[SlotCount];
            stSub = new Text[SlotCount];
            stTag = new Text[SlotCount];
            stNum = new Text[SlotCount];
            stSubBg = new Image[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                int id = i;
                RectTransform box = New("street" + i, pr);
                Place(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(470f, 120f));
                stBox[i] = box;
                // étiquette : halo (prochaine rue), pastille colorée avec numéro et nom, ligne d'info jaune dessous
                stGlow[i] = Img(box, round, Yellow);
                Place(stGlow[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(430f, 80f));
                stBg[i] = Img(box, round, GuardBlue);
                Place(stBg[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(410f, 62f));
                // liseré blanc : image enfant agrandie, rendue derrière grâce au fond intérieur
                Image border = Img(stBg[i].rectTransform, round, new Color(1f, 1f, 1f, 0.95f));
                Stretch(border.rectTransform);
                border.rectTransform.sizeDelta = new Vector2(6f, 6f);
                stBorder[i] = border;
                stFill[i] = Img(stBg[i].rectTransform, round, GuardBlue);
                Stretch(stFill[i].rectTransform);
                stFill[i].rectTransform.sizeDelta = new Vector2(-2f, -2f);
                stName[i] = Label(stBg[i].rectTransform, "", 30, Color.white, TextAnchor.MiddleCenter, true);
                Shadowed(stName[i]);
                Stretch(stName[i].rectTransform);
                stNum[i] = Label(box, "", 1, Color.clear, TextAnchor.MiddleCenter, false); // gardé pour la compatibilité, invisible
                stSubBg[i] = Img(box, round, Yellow);
                Place(stSubBg[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -36f), new Vector2(440f, 36f));
                stSub[i] = Label(stSubBg[i].rectTransform, "", 21, new Color(0.14f, 0.1f, 0f), TextAnchor.MiddleCenter, true);
                Stretch(stSub[i].rectTransform);
                stTag[i] = Label(box, "", 24, Yellow, TextAnchor.MiddleCenter, true);
                Shadowed(stTag[i]);
                Place(stTag[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 62f), new Vector2(300f, 32f));
                AddButton(box, () => { if (slotStreet[id] >= 0) onStreet?.Invoke(slotStreet[id]); });
            }
        }

        /// <summary>Fond et rues d'une map (reconstruits seulement quand le nombre de rues ou le thème change).</summary>
        void LayoutCityMap(int count, int theme, int map)
        {
            Sprite art = map >= 0 && map < MapArt.Length ? GearIcons.Art(MapArt[map]) : null;
            mapArt.sprite = art;
            mapArt.enabled = art != null;
            bool own = art != null && ArtRoute.ContainsKey(map) && ArtRoute[map].Length == count;
            mapCity.enabled = !own;
            if (!own) mapCity.sprite = CitySprite(theme, map, count, art != null);
            Vector2[][] route = own ? ArtRoute[map] : RouteOf(count);
            Vector2[] labels = own ? ArtLabels[map] : LabelsOf(count);
            int key = (own ? 1000 + map * 10 : 0) + count * 10 + theme;
            if (key != streetLayout)
            {
                streetLayout = key;
                for (int c = streetRoot.childCount - 1; c >= 0; c--) Destroy(streetRoot.GetChild(c).gameObject);
                for (int k = 0; k < SlotCount; k++) { stBody[k] = null; stEdge[k] = null; stTape[k] = null; stBarrier[k] = null; stCop[k] = null; }
                for (int k = 0; k < count; k++) BuildStreet(k, route[k]);
            }
            for (int k = 0; k < SlotCount; k++)
            {
                stBox[k].gameObject.SetActive(k < count);
                if (k < count) stBox[k].anchoredPosition = ToUi(labels[k]) + new Vector2(0f, MapShiftY);
            }
        }

        void BuildStreet(int k, Vector2[] pts)
        {
            float s = UiScale;
            float body = 24f * s, edgeOff = 15.5f * s, edgeW = 4.5f * s;
            stBody[k] = new List<Graphic>();
            stEdge[k] = new List<Graphic>();
            var tapes = new List<RawImage>();
            for (int i = 0; i < pts.Length - 1; i++)
            {
                Vector2 a = ToUi(pts[i]), b = ToUi(pts[i + 1]);
                Vector2 d = b - a;
                float len = d.magnitude;
                if (len < 0.5f) continue;
                Vector2 n = new Vector2(-d.y, d.x) / len;
                float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                stBody[k].Add(Segment(a, b, body + 2f, ang, solidTex, 1f));
                for (int side = -1; side <= 1; side += 2)
                {
                    RawImage t = Segment(a + n * edgeOff * side, b + n * edgeOff * side, edgeW, ang, tapeTex, len / (24f * s));
                    stEdge[k].Add(t);
                    tapes.Add(t);
                }
                // jointure arrondie (les virages restent pleins)
                if (i > 0) stBody[k].Add(Dot(a, body + 2f));
            }
            stTape[k] = tapes.ToArray();

            Vector2 start = ToUi(pts[0]), next = ToUi(pts[1]);
            Vector2 dir = (next - start).normalized;
            // barrière « ROAD CLOSED » à l'entrée de la rue
            RectTransform bar = New("closed", streetRoot);
            bar.anchoredPosition = start + dir * 34f * s * 0.5f;
            bar.sizeDelta = new Vector2(body + 18f, 14f);
            bar.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f);
            RawImage stripes = bar.gameObject.AddComponent<RawImage>();
            stripes.texture = stripeTex;
            stripes.uvRect = new Rect(0f, 0f, 4f, 1f);
            stripes.raycastTarget = false;
            stBarrier[k] = bar.gameObject;

            // voiture de police au milieu de la rue (rue à prendre)
            Vector2 mid = PointAt(pts, 0.3f);
            Vector2 mdir = DirAt(pts, 0.3f);
            RectTransform car = New("cop", streetRoot);
            car.anchoredPosition = ToUi(mid);
            car.sizeDelta = new Vector2(30f * s * 0.9f, 15f * s * 0.9f);
            car.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-mdir.y, mdir.x) * Mathf.Rad2Deg);
            Image carBody = car.gameObject.AddComponent<Image>();
            carBody.sprite = round;
            carBody.type = Image.Type.Sliced;
            carBody.color = new Color(0.11f, 0.12f, 0.15f);
            carBody.raycastTarget = false;
            Image roof = Img(car, round, new Color(0.95f, 0.95f, 0.97f));
            Place(roof.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-2f, 0f), new Vector2(car.sizeDelta.x * 0.4f, car.sizeDelta.y * 0.7f));
            var lights = new Image[2];
            for (int l = 0; l < 2; l++)
            {
                lights[l] = Img(car, round, l == 0 ? new Color(1f, 0.2f, 0.2f) : new Color(0.25f, 0.5f, 1f));
                Place(lights[l].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(6f, (l == 0 ? 1f : -1f) * car.sizeDelta.y * 0.22f), new Vector2(9f, car.sizeDelta.y * 0.38f));
            }
            stLights[k] = lights;
            stCop[k] = car.gameObject;
        }

        static Vector2 PointAt(Vector2[] pts, float f)
        {
            float total = 0f;
            for (int i = 0; i < pts.Length - 1; i++) total += Vector2.Distance(pts[i], pts[i + 1]);
            float target = total * f;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                float l = Vector2.Distance(pts[i], pts[i + 1]);
                if (target <= l) return Vector2.Lerp(pts[i], pts[i + 1], target / Mathf.Max(0.01f, l));
                target -= l;
            }
            return pts[pts.Length - 1];
        }

        static Vector2 DirAt(Vector2[] pts, float f)
        {
            float total = 0f;
            for (int i = 0; i < pts.Length - 1; i++) total += Vector2.Distance(pts[i], pts[i + 1]);
            float target = total * f;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                float l = Vector2.Distance(pts[i], pts[i + 1]);
                if (target <= l) return (pts[i + 1] - pts[i]).normalized;
                target -= l;
            }
            return (pts[pts.Length - 1] - pts[pts.Length - 2]).normalized;
        }

        RawImage Segment(Vector2 a, Vector2 b, float width, float angle, Texture2D tex, float uvRepeat)
        {
            RectTransform rt = New("seg", streetRoot);
            rt.anchoredPosition = (a + b) * 0.5f;
            rt.sizeDelta = new Vector2(Vector2.Distance(a, b) + (tex == solidTex ? 0f : 0f), width);
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);
            RawImage r = rt.gameObject.AddComponent<RawImage>();
            r.texture = tex;
            r.uvRect = new Rect(0f, 0f, uvRepeat, 1f);
            r.raycastTarget = false;
            return r;
        }

        Image Dot(Vector2 p, float size)
        {
            Image d = Img(streetRoot, disc, Color.white);
            d.rectTransform.anchoredPosition = p;
            d.rectTransform.sizeDelta = new Vector2(size, size);
            return d;
        }

        /// <summary>État d'une rue : 0 verrouillée, 1 à prendre, 2 prise.</summary>
        void SetStreetState(int k, int state)
        {
            stState[k] = state;
            stFillF[k] = state == 2 ? 1f : 0f;
            if (stBody[k] == null) return;
            stBarrier[k].SetActive(state == 0);
            stCop[k].SetActive(state == 1);
            ApplyStreetColors(k, stFillF[k]);
        }

        Color streetMine = new Color(0.24f, 0.49f, 1f), streetTheirs = new Color(0.94f, 0.27f, 0.27f);

        void ApplyStreetColors(int k, float f)
        {
            if (stBody[k] == null) return;
            int st = stState[k];
            Color bodyC, edgeC;
            if (st == 0) { bodyC = new Color(0.1f, 0.11f, 0.14f, 0.55f); edgeC = new Color(0.55f, 0.57f, 0.62f, 0f); }
            else
            {
                Color from = new Color(streetTheirs.r, streetTheirs.g, streetTheirs.b, 0.32f), to = new Color(streetMine.r, streetMine.g, streetMine.b, 0.3f);
                bodyC = Color.Lerp(from, to, f);
                edgeC = Color.Lerp(Color.white, streetMine, f);
            }
            foreach (Graphic g in stBody[k]) g.color = bodyC;
            foreach (Graphic g in stEdge[k])
            {
                g.color = edgeC;
                // rubalise jaune et noire pour la rue à prendre, liseré bleu plein une fois prise
                ((RawImage)g).texture = st == 1 && f < 0.99f ? tapeTex : solidTex;
            }
        }

        /// <summary>Compatibilité : la conquête fait passer la rue de « à prendre » à « prise ».</summary>
        void SetFill(int k, float f)
        {
            stFillF[k] = f;
            if (f > 0f && stState[k] == 1) { ApplyStreetColors(k, f); stFill[k].color = Color.Lerp(streetTheirs, streetMine, f); }
            if (f >= 1f && stCop[k] != null) stCop[k].SetActive(false);
        }

        /// <summary>Animation de la carte : rubalise qui défile, gyrophare de la voiture de police.</summary>
        void TickStreets(float udt)
        {
            for (int k = 0; k < SlotCount; k++)
            {
                if (stTape[k] == null || stState[k] != 1) continue;
                foreach (RawImage t in stTape[k])
                {
                    Rect r = t.uvRect;
                    r.x = Mathf.Repeat(r.x - udt * 1.6f, 1f);
                    t.uvRect = r;
                }
                if (stLights[k] != null)
                {
                    bool on = Mathf.Repeat(mapT * 4f, 1f) < 0.5f;
                    stLights[k][0].enabled = on;
                    stLights[k][1].enabled = !on;
                }
            }
        }

        // ---------------------------------------------------------------- textures

        static Sprite MakeDisc()
        {
            const int n = 128;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - n * 0.5f, dy = y + 0.5f - n * 0.5f;
                    float a = Mathf.Clamp01(n * 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            t.SetPixels32(px);
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Textures des rues : blanc plein, rubalise (jaune / noir), rayures de barrière (rouge / blanc).</summary>
        static void MakeStreetTextures()
        {
            solidTex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            solidTex.SetPixels32(new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) });
            solidTex.Apply();
            tapeTex = new Texture2D(16, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            var tp = new Color32[32];
            for (int x = 0; x < 16; x++)
            {
                Color32 c = x < 8 ? new Color32(255, 216, 74, 255) : new Color32(24, 24, 26, 255);
                tp[x] = c; tp[16 + x] = c;
            }
            tapeTex.SetPixels32(tp);
            tapeTex.Apply();
            stripeTex = new Texture2D(8, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            var sp = new Color32[16];
            for (int x = 0; x < 8; x++)
            {
                Color32 c = x < 4 ? new Color32(226, 59, 59, 255) : new Color32(250, 250, 250, 255);
                sp[x] = c; sp[8 + x] = c;
            }
            stripeTex.SetPixels32(sp);
            stripeTex.Apply();
        }

        static Sprite ToSprite(Color32[] px)
        {
            var t = new Texture2D(MapW, MapH, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            t.SetPixels32(px);
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, MapW, MapH), new Vector2(0.5f, 0.5f), 100f);
        }

        static void Rect(Color32[] px, int x0, int y0, int x1, int y1, Color32 c)
        {
            x0 = Mathf.Max(0, x0); y0 = Mathf.Max(0, y0); x1 = Mathf.Min(MapW, x1); y1 = Mathf.Min(MapH, y1);
            for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) px[y * MapW + x] = c;
        }

        static void Disc(Color32[] px, float cx, float cy, float r, Color32 c)
        {
            int x0 = Mathf.Max(0, (int)(cx - r)), x1 = Mathf.Min(MapW - 1, (int)(cx + r) + 1);
            int y0 = Mathf.Max(0, (int)(cy - r)), y1 = Mathf.Min(MapH - 1, (int)(cy + r) + 1);
            float r2 = r * r;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    if (dx * dx + dy * dy <= r2) px[y * MapW + x] = c;
                }
        }

        /// <summary>Trait épais le long d'une polyligne (disques posés tous les pixels), avec pointillés optionnels.</summary>
        static void Stroke(Color32[] px, Vector2[] pts, float width, Color32 c, float dash = 0f, float gap = 0f)
        {
            float run = 0f;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                Vector2 a = pts[i], b = pts[i + 1];
                float len = Vector2.Distance(a, b);
                for (float t = 0f; t <= len; t += 0.8f)
                {
                    float pos = run + t;
                    if (dash > 0f && Mathf.Repeat(pos, dash + gap) > dash) continue;
                    Vector2 p = Vector2.Lerp(a, b, t / Mathf.Max(0.01f, len));
                    Disc(px, p.x, p.y, width * 0.5f, c);
                }
                run += len;
            }
        }

        static Color32 C(float r, float g, float b) => new Color(r, g, b, 1f);

        static Vector2[] ToTexPts(Vector2[] m)
        {
            var o = new Vector2[m.Length];
            for (int i = 0; i < m.Length; i++) o[i] = ToTex(m[i]);
            return o;
        }

        /// <summary>
        /// Vue aérienne : toits (tuiles, zinc, terrasses), parc arboré, rues avec trottoirs, chaussée, marquage,
        /// passages piétons, place ronde au bout du parcours, quelques voitures.
        /// </summary>
        static Sprite CitySprite(int theme, int map, int count, bool roadsOnly = false)
        {
            int key = ((theme * 10 + map) * 10 + count) * 2 + (roadsOnly ? 1 : 0);
            Sprite s;
            if (citySprites.TryGetValue(key, out s)) return s;
            var r = new System.Random(77 + map * 31 + count);
            var px = new Color32[MapW * MapH];
            Color32 ground = theme == 4 ? C(0.2f, 0.19f, 0.26f) : theme == 3 ? C(0.5f, 0.52f, 0.54f) : C(0.55f, 0.54f, 0.51f);
            for (int i = 0; i < px.Length; i++) px[i] = ground;
            Color32[] roofs =
                theme == 1 ? new[] { C(0.84f, 0.45f, 0.4f), C(0.62f, 0.73f, 0.8f), C(0.86f, 0.73f, 0.45f), C(0.78f, 0.6f, 0.72f), C(0.88f, 0.85f, 0.8f), C(0.55f, 0.75f, 0.66f) }
                // gare : zinc, ardoise, verrières
              : theme == 2 ? new[] { C(0.62f, 0.66f, 0.7f), C(0.5f, 0.54f, 0.6f), C(0.72f, 0.78f, 0.84f), C(0.8f, 0.76f, 0.68f), C(0.56f, 0.6f, 0.58f) }
                // docks : conteneurs de toutes les couleurs
              : theme == 3 ? new[] { C(0.72f, 0.2f, 0.15f), C(0.15f, 0.35f, 0.62f), C(0.9f, 0.55f, 0.12f), C(0.22f, 0.52f, 0.36f), C(0.84f, 0.84f, 0.8f), C(0.52f, 0.22f, 0.44f) }
                // quartier chic, la nuit : toits sombres
              : theme == 4 ? new[] { C(0.24f, 0.22f, 0.3f), C(0.3f, 0.27f, 0.36f), C(0.2f, 0.2f, 0.26f), C(0.34f, 0.3f, 0.38f) }
              : new[] { C(0.79f, 0.44f, 0.35f), C(0.85f, 0.82f, 0.77f), C(0.62f, 0.7f, 0.76f), C(0.78f, 0.65f, 0.42f), C(0.7f, 0.68f, 0.66f) };
            // toits : cellules de 27 px, chacune un ou deux bâtiments avec un détail (cheminée, verrière)
            const int cell = 27;
            for (int cy = 0; cy < MapH; cy += cell)
                for (int cx = 0; cx < MapW; cx += cell)
                {
                    Color32 roof = roofs[r.Next(roofs.Length)];
                    Rect(px, cx + 2, cy + 2, cx + cell - 1, cy + cell - 1, roof);
                    Color32 dark = new Color32((byte)(roof.r * 0.8f), (byte)(roof.g * 0.8f), (byte)(roof.b * 0.8f), 255);
                    if (r.NextDouble() < 0.5) Rect(px, cx + 2, cy + cell / 2, cx + cell - 1, cy + cell / 2 + 1, dark);
                    int dx = r.Next(4, cell - 9), dy = r.Next(4, cell - 9);
                    Rect(px, cx + dx, cy + dy, cx + dx + 5, cy + dy + 4, dark);
                }
            // quartier chic, la nuit : fenêtres allumées sur les toits
            if (theme == 4)
                for (int w = 0; w < 260; w++)
                {
                    int x = r.Next(4, MapW - 6), y = r.Next(4, MapH - 6);
                    Rect(px, x, y, x + 2, y + 2, r.NextDouble() < 0.7 ? C(1f, 0.82f, 0.45f) : C(0.6f, 0.8f, 1f));
                }
            // parc (gare : faisceau de voies ferrées ; docks : bassin du port)
            Vector2 pk = ToTex(new Vector2(380, 270)), pk2 = ToTex(new Vector2(490, 390));
            Color32 tree = C(0.36f, 0.56f, 0.27f), treeHi = C(0.45f, 0.65f, 0.34f);
            if (theme == 2)
            {
                Rect(px, (int)pk.x, (int)pk2.y, (int)pk2.x, (int)pk.y, C(0.46f, 0.43f, 0.4f));
                for (int y = (int)pk2.y + 4; y < (int)pk.y - 3; y += 9)
                {
                    Rect(px, (int)pk.x, y, (int)pk2.x, y + 1, C(0.72f, 0.72f, 0.74f));
                    Rect(px, (int)pk.x, y + 4, (int)pk2.x, y + 5, C(0.72f, 0.72f, 0.74f));
                }
                Rect(px, (int)pk.x + 8, (int)pk.y - 12, (int)pk.x + 40, (int)pk.y - 6, C(0.85f, 0.2f, 0.2f));   // un train
            }
            else if (theme == 3)
            {
                Rect(px, (int)pk.x, (int)pk2.y, MapW, (int)pk.y, C(0.18f, 0.36f, 0.48f));
                for (int w = 0; w < 18; w++)
                {
                    int x = r.Next((int)pk.x + 4, MapW - 12), y = r.Next((int)pk2.y + 4, (int)pk.y - 4);
                    Rect(px, x, y, x + 7, y + 1, C(0.32f, 0.52f, 0.64f));
                }
                Rect(px, (int)pk.x + 20, (int)pk2.y + 20, (int)pk.x + 70, (int)pk2.y + 32, C(0.85f, 0.85f, 0.82f));   // un cargo
                Rect(px, (int)pk.x + 24, (int)pk2.y + 22, (int)pk.x + 50, (int)pk2.y + 30, C(0.72f, 0.2f, 0.15f));
            }
            else
            {
                if (theme == 4) { tree = C(0.16f, 0.3f, 0.2f); treeHi = C(0.22f, 0.38f, 0.26f); }
                Rect(px, (int)pk.x, (int)pk2.y, (int)pk2.x, (int)pk.y, theme == 4 ? C(0.2f, 0.32f, 0.22f) : C(0.5f, 0.68f, 0.39f));
                for (int t = 0; t < 9; t++)
                {
                    float x = r.Next((int)pk.x + 6, (int)pk2.x - 6), y = r.Next((int)pk2.y + 6, (int)pk.y - 6);
                    Disc(px, x, y, r.Next(5, 8), tree);
                    Disc(px, x - 1.5f, y + 1.5f, 2.5f, treeHi);
                }
            }

            // avec un fond illustré : on ne garde que ce qui est dessiné à partir d'ici (rues, place, voitures, cadre)
            Color32[] under = roadsOnly ? (Color32[])px.Clone() : null;
            // rues : quadrillage de fond + rues du parcours
            Vector2[][] route = RouteOf(count);
            float[] gx = count == 3 ? GridX3 : count == 4 ? GridX4 : GridX5, gy = count == 3 ? GridY3 : count == 4 ? GridY4 : GridY5;
            var roads = new List<Vector2[]>();
            if (!roadsOnly)
            {
                foreach (float x in gx) roads.Add(new[] { ToTex(new Vector2(x, -10)), ToTex(new Vector2(x, MockH + 10)) });
                foreach (float y in gy) roads.Add(new[] { ToTex(new Vector2(-10, y)), ToTex(new Vector2(MockW + 10, y)) });
            }
            foreach (Vector2[] st in route) roads.Add(ToTexPts(st));
            float k = MapW / MockW;
            Color32 walk = C(0.81f, 0.78f, 0.73f), asphalt = C(0.28f, 0.29f, 0.33f), paint = C(0.96f, 0.95f, 0.92f);
            foreach (Vector2[] rd in roads) Stroke(px, rd, 34f * k, walk);
            Vector2 pl = ToTex((count == 3 ? Plaza3 : count == 4 ? Plaza4 : Plaza5)[0]);
            Disc(px, pl.x, pl.y, 34f * k, walk);
            foreach (Vector2[] rd in roads) Stroke(px, rd, 24f * k, asphalt);
            Disc(px, pl.x, pl.y, 24f * k, C(0.85f, 0.83f, 0.78f));
            Disc(px, pl.x, pl.y, 8f * k, tree);
            foreach (Vector2[] rd in roads) Stroke(px, rd, 1.6f, paint, 5f, 5f);
            // passages piétons aux extrémités des rues du parcours
            foreach (Vector2[] st in route)
            {
                Vector2[] t = ToTexPts(st);
                Vector2 a = t[0], d = (t[1] - t[0]).normalized, n = new Vector2(-d.y, d.x);
                for (int z = -2; z <= 2; z++)
                {
                    Vector2 c0 = a + d * (14f * k) + n * (z * 4f * k);
                    Stroke(px, new[] { c0 - d * 3f, c0 + d * 3f }, 2f, paint);
                }
            }
            // voitures
            Color32[] cars = { C(0.91f, 0.78f, 0.29f), C(0.72f, 0.76f, 0.81f), C(0.79f, 0.29f, 0.23f), C(0.2f, 0.4f, 0.75f) };
            for (int c = 0; c < 6; c++)
            {
                Vector2[] rd = roads[r.Next(roads.Count)];
                Vector2 p = Vector2.Lerp(rd[0], rd[rd.Length - 1], (float)r.NextDouble() * 0.8f + 0.1f);
                Vector2 d = (rd[rd.Length - 1] - rd[0]).normalized, n = new Vector2(-d.y, d.x);
                p += n * (5f * k);
                Stroke(px, new[] { p - d * 5f, p + d * 5f }, 6f, cars[c % cars.Length]);
            }
            // cadre
            Color32 frame = C(0.12f, 0.13f, 0.16f);
            Rect(px, 0, 0, MapW, 3, frame); Rect(px, 0, MapH - 3, MapW, MapH, frame);
            Rect(px, 0, 0, 3, MapH, frame); Rect(px, MapW - 3, 0, MapW, MapH, frame);
            if (under != null)
                for (int i = 0; i < px.Length; i++)
                    if (px[i].r == under[i].r && px[i].g == under[i].g && px[i].b == under[i].b) px[i].a = 0;
            s = ToSprite(px);
            citySprites[key] = s;
            return s;
        }
    }
}
