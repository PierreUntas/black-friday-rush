using UnityEngine;
using UnityEngine.UI;

namespace SamediRouge
{
    /// <summary>
    /// Démonstration des gestes : une main (index tendu) mime le geste en grand au milieu de l'écran.
    /// Elle se pose (onde au contact du doigt), glisse en laissant une traînée, puis se lève et recommence.
    /// Toucher : deux appuis rapides. Gauche/droite : vers la rue libre si elle est connue, sinon en alternance.
    /// </summary>
    public partial class Hud
    {
        const int TrailDots = 10;
        static Sprite handSprite;
        GameObject gestRoot;
        RectTransform gestHand;
        Image gestHandImg, gestShadow, gestPress;
        Image[] gestRipple = new Image[2];
        Image[] gestTrail = new Image[TrailDots];
        Text gestLabel, gestSub;
        int gestDir;

        void BuildGesture(RectTransform p)
        {
            if (handSprite == null) handSprite = MakeHand();
            RectTransform root = New("gesture", p);
            Place(root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-30f, 720f), new Vector2(10f, 10f)); // bas de l'écran : là où joue le pouce
            gestRoot = root.gameObject;
            for (int i = 0; i < TrailDots; i++)
            {
                gestTrail[i] = Img(root, dot, Color.white);
                gestTrail[i].rectTransform.sizeDelta = new Vector2(46f, 46f);
            }
            for (int i = 0; i < 2; i++)
            {
                gestRipple[i] = Img(root, Ring(), Yellow);
                gestRipple[i].rectTransform.sizeDelta = new Vector2(90f, 90f);
            }
            gestPress = Img(root, dot, new Color(1f, 0.85f, 0.2f, 0.9f));
            gestPress.rectTransform.sizeDelta = new Vector2(52f, 52f);
            // ombre portée de la main (décalée), puis la main ; pivot sur le bout de l'index
            RectTransform sh = New("handShadow", root);
            gestShadow = sh.gameObject.AddComponent<Image>();
            gestShadow.sprite = handSprite;
            gestShadow.color = new Color(0f, 0f, 0f, 0.35f);
            gestShadow.raycastTarget = false;
            RectTransform h = New("hand", root);
            gestHandImg = h.gameObject.AddComponent<Image>();
            gestHandImg.sprite = handSprite;
            gestHandImg.raycastTarget = false;
            gestHand = h;
            // main illustrée (Art/hand_point, 268×400, bout de l'index en 61,28 depuis le haut) si elle existe
            Sprite art = GearIcons.Art("hand_point");
            if (art != null) { gestShadow.sprite = art; gestHandImg.sprite = art; }
            foreach (RectTransform r in new[] { sh, h })
            {
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                if (art != null)
                {
                    r.pivot = new Vector2(61f / 268f, 1f - 28f / 400f);
                    r.sizeDelta = new Vector2(268f * 0.95f, 400f * 0.95f);
                }
                else
                {
                    r.pivot = new Vector2(HandTipX / (float)HandW, HandTipY / (float)HandH);
                    r.sizeDelta = new Vector2(HandW * 1.7f, HandH * 1.7f);
                }
            }
            gestLabel = Label(root, "", 44, Color.white, TextAnchor.MiddleCenter, true);
            Shadowed(gestLabel);
            Place(gestLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(30f, 250f), new Vector2(760f, 60f));
            gestSub = Label(root, L("with your thumb, one quick flick", "avec le pouce, un petit coup sec"), 30, Yellow, TextAnchor.MiddleCenter, true);
            Shadowed(gestSub);
            Place(gestSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(30f, 200f), new Vector2(760f, 44f));
            gestRoot.SetActive(false);
        }

        static Sprite ringSprite;
        static Sprite Ring()
        {
            if (ringSprite != null) return ringSprite;
            const int n = 96;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f));
                    float a = Mathf.Clamp01(5f - Mathf.Abs(d - (n * 0.5f - 7f)));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            t.SetPixels32(px);
            t.Apply();
            ringSprite = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return ringSprite;
        }

        /// <summary>Active la démonstration pour un type de consigne (0 toucher, 1 gauche/droite, 2 haut, 3 bas).</summary>
        void ShowGesture(int kind, int dir)
        {
            if (gestRoot == null) return;
            gestDir = dir;
            gestRoot.SetActive(kind >= 0 && kind <= 3);
        }

        void HideGesture() { if (gestRoot != null) gestRoot.SetActive(false); }

        static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        void TickGesture(float t)
        {
            if (gestRoot == null || !gestRoot.activeSelf) return;
            const float cycle = 1.4f;
            int n = Mathf.FloorToInt(t / cycle);
            float c = (t - n * cycle) / cycle;
            Vector2 from = Vector2.zero, to = Vector2.zero;
            string label = "";
            // un vrai swipe est court et rapide : le pouce se pose, part d'un coup sec, se relève
            switch (tipKind)
            {
                case 0: label = L("TAP!", "TAPE !"); break;
                case 1:
                {
                    int d = gestDir != 0 ? gestDir : (n % 2 == 0 ? 1 : -1);
                    from = new Vector2(-50f * d, 0f); to = new Vector2(80f * d, 0f);
                    label = d > 0 ? L("SWIPE RIGHT", "GLISSE À DROITE") : L("SWIPE LEFT", "GLISSE À GAUCHE");
                    if (gestDir == 0) label = L("SWIPE LEFT / RIGHT", "GLISSE À GAUCHE / DROITE");
                    break;
                }
                case 2: from = new Vector2(0f, -50f); to = new Vector2(0f, 90f); label = L("SWIPE UP", "GLISSE VERS LE HAUT"); break;
                case 3: from = new Vector2(0f, 40f); to = new Vector2(0f, -90f); label = L("SWIPE DOWN", "GLISSE VERS LE BAS"); break;
            }
            gestLabel.text = label;
            gestSub.text = tipKind == 0 ? L("with your thumb, anywhere on screen", "avec le pouce, n'importe où sur l'écran") : L("with your thumb, one quick flick", "avec le pouce, un petit coup sec");

            Vector2 tip;
            float press, alpha, drag;   // press : 0 levé, 1 posé ; drag : avancement du glissé
            if (tipKind == 0)
            {
                // deux tapes rapides, puis une pause
                float a1 = Mathf.InverseLerp(0.08f, 0.13f, c) - Mathf.InverseLerp(0.19f, 0.24f, c);
                float a2 = Mathf.InverseLerp(0.34f, 0.39f, c) - Mathf.InverseLerp(0.45f, 0.5f, c);
                press = Mathf.Clamp01(Mathf.Max(a1, a2));
                alpha = Mathf.InverseLerp(0f, 0.06f, c) * (1f - Mathf.InverseLerp(0.7f, 0.85f, c));
                drag = 0f;
                tip = Vector2.zero;
                Ripple(0, c - 0.12f, Vector2.zero);
                Ripple(1, c - 0.38f, Vector2.zero);
            }
            else
            {
                // apparition 0–0.1, appui 0.12–0.18, coup sec 0.2–0.32 (départ rapide, fin amortie), levée 0.32–0.42, pause
                alpha = Mathf.InverseLerp(0f, 0.1f, c) * (1f - Mathf.InverseLerp(0.55f, 0.7f, c));
                press = Mathf.InverseLerp(0.12f, 0.18f, c) * (1f - Mathf.InverseLerp(0.32f, 0.42f, c));
                float k = Mathf.Clamp01(Mathf.InverseLerp(0.2f, 0.32f, c));
                drag = 1f - (1f - k) * (1f - k) * (1f - k);
                tip = Vector2.Lerp(from, to, drag);
                Ripple(0, c - 0.17f, from);
                gestRipple[1].enabled = false;
            }
            // la main « descend » vers l'écran quand le doigt appuie : légèrement plus petite, ombre plus proche
            float lift = 1f - press;
            float s = 0.9f + 0.12f * lift;
            gestHand.anchoredPosition = tip;
            gestHand.localScale = Vector3.one * s;
            gestHand.localRotation = Quaternion.Euler(0f, 0f, tipKind == 1 ? 6f * drag * Mathf.Sign(to.x - from.x) : 0f); // le pouce pivote un peu sur sa base
            gestHandImg.color = new Color(1f, 1f, 1f, alpha);
            RectTransform srt = gestShadow.rectTransform;
            srt.anchoredPosition = tip + new Vector2(10f + 26f * lift, -10f - 26f * lift);
            srt.localScale = gestHand.localScale;
            srt.localRotation = gestHand.localRotation;
            gestShadow.color = new Color(0f, 0f, 0f, 0.3f * alpha * (0.5f + 0.5f * press));
            // point de contact sous le bout du doigt
            gestPress.rectTransform.anchoredPosition = tip;
            gestPress.color = new Color(1f, 0.85f, 0.2f, 0.85f * press * alpha);
            gestPress.transform.localScale = Vector3.one * (0.8f + 0.3f * press);
            // traînée : du point de départ jusqu'au doigt, de plus en plus marquée vers le doigt
            float trailFade = 1f - Mathf.InverseLerp(0.34f, 0.6f, c);
            for (int i = 0; i < TrailDots; i++)
            {
                float f = (i + 1) / (float)TrailDots;
                Image d = gestTrail[i];
                bool on = tipKind != 0 && drag > 0.01f;
                d.enabled = on;
                if (!on) continue;
                d.rectTransform.anchoredPosition = Vector2.Lerp(from, tip, f);
                d.transform.localScale = Vector3.one * (0.35f + 0.65f * f);
                d.color = new Color(1f, 0.92f, 0.5f, 0.55f * f * trailFade);
            }
            gestLabel.color = new Color(1f, 1f, 1f, 0.6f + 0.4f * Mathf.Max(alpha, 0.4f));
            gestSub.color = new Color(Yellow.r, Yellow.g, Yellow.b, 0.9f);
            gestLabel.transform.localScale = Vector3.one * (1f + 0.06f * press);
        }

        void Ripple(int i, float t, Vector2 pos)
        {
            Image r = gestRipple[i];
            bool on = t >= 0f && t < 0.3f;
            r.enabled = on;
            if (!on) return;
            float k = t / 0.3f;
            r.rectTransform.anchoredPosition = pos;
            r.transform.localScale = Vector3.one * (0.6f + 1.4f * k);
            r.color = new Color(Yellow.r, Yellow.g, Yellow.b, 1f - k);
        }

        // ---------------------------------------------------------------- dessin de la main

        const int HandW = 200, HandH = 230, HandTipX = 74, HandTipY = 197;

        static float Capsule(Vector2 p, Vector2 a, Vector2 b, float r)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01((pa.x * ba.x + pa.y * ba.y) / (ba.x * ba.x + ba.y * ba.y));
            return Vector2.Distance(p, a + ba * h) - r;
        }

        static float RoundBox(Vector2 p, Vector2 c, Vector2 half, float r)
        {
            float qx = Mathf.Abs(p.x - c.x) - half.x + r, qy = Mathf.Abs(p.y - c.y) - half.y + r;
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        /// <summary>
        /// Main qui tient le téléphone, vue de face : seul le pouce passe sur l'écran (ongle, pli de l'articulation),
        /// le talon de la paume et la manche bleue dépassent en bas à droite. Pivot sur le bout du pouce.
        /// </summary>
        static Sprite MakeHand()
        {
            var t = new Texture2D(HandW, HandH, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[HandW * HandH];
            Color skin = new Color(1f, 0.878f, 0.76f), skinLight = new Color(1f, 0.93f, 0.85f), skin2 = new Color(0.965f, 0.776f, 0.627f), skin3 = new Color(0.886f, 0.667f, 0.518f);
            Color line = new Color(0.19f, 0.118f, 0.094f), nail = new Color(1f, 0.95f, 0.93f), nailEdge = new Color(0.91f, 0.745f, 0.667f), lunula = new Color(0.98f, 0.88f, 0.85f);
            Color cuff = new Color(0.24f, 0.49f, 1f), cuffDark = new Color(0.15f, 0.3f, 0.7f);
            // un vrai pouce, vu de dessus : épais, court, incliné, avec un large ongle ; il sort de la paume en bas à droite
            Vector2 B = new Vector2(150f, 62f), J = new Vector2(116f, 122f), T = new Vector2(88f, 172f);
            Vector2 d = (T - J).normalized, n = new Vector2(-d.y, d.x);
            Vector2 n0 = T - d * 14f, n1 = T + d * 8f;
            for (int y = 0; y < HandH; y++)
                for (int x = 0; x < HandW; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float th = SMin(RCone(p, B, J, 36f, 31f), RCone(p, J, T, 31f, 30f), 8f);
                    float pm = SMin(Vector2.Distance(p, new Vector2(178f, 46f)) - 58f, RoundBox(p, new Vector2(176f, 20f), new Vector2(40f, 30f), 20f), 20f);
                    float sl = RoundBox(p, new Vector2(178f, 8f), new Vector2(40f, 14f), 5f);
                    float hand = SMin(pm, th, 14f);
                    float all = Mathf.Min(hand, sl);
                    if (all > 4.5f) { px[y * HandW + x] = new Color32(0, 0, 0, 0); continue; }
                    Color c;
                    float a = 1f;
                    if (all > 0f) { c = line; a = Mathf.Clamp01(4.5f - all); }
                    else if (sl < 0f) c = sl > -3.5f ? line : (y < 8 ? cuffDark : cuff);
                    else
                    {
                        if (th < 0f && th < pm)
                        {
                            // lumière venant de la gauche : bord gauche éclairé, bord droit plus foncé
                            float side = Vector2.Dot(p - J, n);
                            c = side < -20f ? skin2 : side > 22f ? skinLight : skin;
                        }
                        else c = x > 188 ? skin3 : x > 176 ? skin2 : skin;
                        // plis de l'articulation, en travers du pouce
                        Vector2 k0 = J - d * 4f, k1 = J + d * 4f;
                        if (th < -5f && RCone(p, k0 - n * 13f, k0 + n * 5f, 1.1f, 1.1f) < 0.5f) c = line;
                        if (th < -5f && RCone(p, k1 - n * 10f, k1 + n * 4f, 1.1f, 1.1f) < 0.5f) c = skin3;
                        // ongle large, lunule, reflet
                        float nl = RCone(p, n0, n1, 17f, 16f);
                        if (nl < 0f) c = nl > -2.2f ? nailEdge : nail;
                        if (nl < -2.2f && nl > -6f && RCone(p, n0, n0 + d * 4f, 11f, 11f) < 0f) c = lunula;
                        Vector2 h0 = T + n * 8f;
                        if (RCone(p, h0, h0 + d * 9f, 2.6f, 2.2f) < 0f) c = Color.white;
                    }
                    px[y * HandW + x] = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), (byte)(a * 255f));
                }
            t.SetPixels32(px);
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, HandW, HandH), new Vector2(HandTipX / (float)HandW, HandTipY / (float)HandH), 100f);
        }

        /// <summary>Cône arrondi : capsule dont le rayon passe de ra à rb.</summary>
        static float RCone(Vector2 p, Vector2 a, Vector2 b, float ra, float rb)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01((pa.x * ba.x + pa.y * ba.y) / (ba.x * ba.x + ba.y * ba.y));
            return (pa - ba * h).magnitude - Mathf.Lerp(ra, rb, h);
        }

        /// <summary>Union adoucie de deux formes (raccord arrondi).</summary>
        static float SMin(float a, float b, float k)
        {
            float h = Mathf.Max(k - Mathf.Abs(a - b), 0f) / k;
            return Mathf.Min(a, b) - h * h * k * 0.25f;
        }
    }
}
