using UnityEngine;
using UnityEngine.UI;

namespace SamediRouge
{
    /// <summary>
    /// V3 « Braquage » : interface minimale. En haut : niveau, butin à l'abri / objectif, étoiles de recherche, vies.
    /// Au-dessus du héros : le butin porté. Joystick flottant, anneau de braquage, écran de fin.
    /// </summary>
    public partial class Hud
    {
        public System.Action onHeistNext;

        RectTransform heistRoot, hjBase, hjKnob, hRobRt, hCarryRt, hResult;
        Text hLevel, hBank, hCarry, hResTitle, hResGain, hResTotal, hResBtn;
        Image hBankFill, hRob;
        Image[] hStars, hHearts;
        float hBankPop, hStarPop;
        int hLastStars = -1;

        public void InitHeist()
        {
            // l'interface du runner est masquée ; on garde les messages (toast), les textes flottants et l'écran titre
            toastT.transform.SetParent(safe, false);
            play.SetActive(false);
            if (menu != null) menu.SetActive(false);
            if (pause != null) pause.SetActive(false);
            if (end != null) end.SetActive(false);
            if (demoRoot != null) demoRoot.SetActive(false);
            HideMeta();

            heistRoot = New("heist", safe);
            Stretch(heistRoot);

            // niveau
            Image lv = Img(heistRoot, round, Panel);
            Place(lv.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -40f), new Vector2(240f, 84f), new Vector2(0f, 1f));
            hLevel = Label(lv.rectTransform, "", 40, Color.white, TextAnchor.MiddleCenter, true);
            Stretch(hLevel.rectTransform);

            // butin à l'abri / objectif
            Image bar = Img(heistRoot, round, Panel);
            Place(bar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(40f, -40f), new Vector2(440f, 84f), new Vector2(0.5f, 1f));
            hBankFill = Img(bar.rectTransform, white, new Color(0.35f, 0.85f, 0.4f));
            hBankFill.type = Image.Type.Filled;
            hBankFill.fillMethod = Image.FillMethod.Horizontal;
            Place(hBankFill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-16f, -16f));
            Image dollar = Img(bar.rectTransform, GearIcons.Art("dollar") ?? round, Color.white);
            Place(dollar.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-6f, 0f), new Vector2(96f, 96f));
            hBank = Label(bar.rectTransform, "", 40, Color.white, TextAnchor.MiddleCenter, true);
            Shadowed(hBank);
            Stretch(hBank.rectTransform);

            // étoiles de recherche
            hStars = new Image[5];
            Sprite wanted = WantedStar();
            for (int i = 0; i < 5; i++)
            {
                hStars[i] = Img(heistRoot, wanted, new Color(1f, 1f, 1f, 0.25f));
                Place(hStars[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(40f + (i - 2) * 76f, -170f), new Vector2(66f, 66f));
            }

            // vies
            hHearts = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                hHearts[i] = Img(heistRoot, heart, Color.white);
                Place(hHearts[i].rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f - i * 76f, -52f), new Vector2(66f, 60f), new Vector2(1f, 1f));
            }

            // butin porté, au-dessus du héros
            hCarryRt = New("carry", canvasRt);
            Place(hCarryRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 80f));
            hCarry = Label(hCarryRt, "", 52, Yellow, TextAnchor.MiddleCenter, true);
            Shadowed(hCarry);
            Stretch(hCarry.rectTransform);

            // anneau de braquage
            hRobRt = New("rob", canvasRt);
            Place(hRobRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 150f));
            Image robBg = Img(hRobRt, dot, new Color(0f, 0f, 0f, 0.45f));
            Stretch(robBg.rectTransform);
            hRob = Img(hRobRt, dot, Yellow);
            hRob.type = Image.Type.Filled;
            hRob.fillMethod = Image.FillMethod.Radial360;
            hRob.fillOrigin = 2;
            hRob.fillClockwise = true;
            Stretch(hRob.rectTransform);
            Image robIn = Img(hRobRt, GearIcons.Art("dollar") ?? dot, Color.white);
            Place(robIn.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(104f, 104f));

            // joystick
            hjBase = Img(canvasRt, dot, new Color(1f, 1f, 1f, 0.16f)).rectTransform;
            Place(hjBase, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260f, 260f));
            hjKnob = Img(canvasRt, dot, new Color(1f, 1f, 1f, 0.55f)).rectTransform;
            Place(hjKnob, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f));

            // fin de partie
            hResult = New("result", safe);
            Stretch(hResult);
            Image dim = Img(hResult, white, new Color(0f, 0f, 0f, 0.55f));
            Place(dim.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(400f, 400f));
            Image card = Img(hResult, round, Panel);
            Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(860f, 640f));
            hResTitle = Label(card.rectTransform, "", 84, Color.white, TextAnchor.MiddleCenter, true);
            Shadowed(hResTitle);
            Place(hResTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -110f), new Vector2(-40f, 140f));
            hResGain = Label(card.rectTransform, "", 110, Yellow, TextAnchor.MiddleCenter, true);
            Shadowed(hResGain);
            Place(hResGain.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(-40f, 160f));
            hResTotal = Label(card.rectTransform, "", 40, Muted, TextAnchor.MiddleCenter, true);
            Place(hResTotal.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 110f), new Vector2(-40f, 80f));
            Image btn = Img(hResult, round, Yellow);
            Place(btn.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -360f), new Vector2(640f, 150f));
            hResBtn = Label(btn.rectTransform, "", 60, new Color(0.12f, 0.1f, 0.05f), TextAnchor.MiddleCenter, true);
            Stretch(hResBtn.rectTransform);
            AddButton(btn.rectTransform, () => { hResult.gameObject.SetActive(false); onHeistNext?.Invoke(); });

            HeistHide();
        }

        /// <summary>Étoile pleine à 5 branches (niveau de recherche), bord sombre.</summary>
        static Sprite WantedStar()
        {
            const int n = 96;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            var poly = new Vector2[10];
            Vector2 c = new Vector2(n * 0.5f, n * 0.47f);
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? 45f : 19f;
                poly[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                bool inside = false;
                for (int i = 0, j = 9; i < 10; j = i++)
                    if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) inside = !inside;
                // distance au bord pour un liseré sombre
                float edge = 1e9f;
                for (int i = 0, j = 9; i < 10; j = i++)
                {
                    Vector2 a = poly[j], b = poly[i], ab = b - a;
                    float k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Vector2.Dot(ab, ab));
                    edge = Mathf.Min(edge, (a + ab * k - p).magnitude);
                }
                byte v = (byte)(edge < 4.5f ? 70 : 255);
                byte al = (byte)(inside ? 255 : edge < 1.5f ? 140 : 0);
                px[y * n + x] = new Color32(v, v, v, al);
            }
            t.SetPixels32(px);
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        public void HeistHide()
        {
            heistRoot.gameObject.SetActive(false);
            hCarryRt.gameObject.SetActive(false);
            hRobRt.gameObject.SetActive(false);
            hjBase.gameObject.SetActive(false);
            hjKnob.gameObject.SetActive(false);
            hResult.gameObject.SetActive(false);
        }

        public void HeistShow(int level, int target)
        {
            heistRoot.gameObject.SetActive(true);
            hResult.gameObject.SetActive(false);
            hLevel.text = L("LEVEL ", "NIVEAU ") + level;
            hLastStars = -1;
        }

        public void HeistPop() { hBankPop = 1f; }
        public void HeistStarPop() { hStarPop = 1f; }

        Vector2 ToCanvas(Vector3 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, null, out Vector2 lp);
            return lp;
        }

        public void HeistTick(int banked, int target, int carried, float stars, int hearts, Vector3 heroScreen, bool stick, Vector2 stickStart, Vector2 stickNow, float rob, Vector3 robScreen)
        {
            float udt = Time.unscaledDeltaTime;
            hBankFill.fillAmount = Mathf.Clamp01(banked / (float)Mathf.Max(1, target));
            hBank.text = banked + " / " + target + " $";
            hBankPop = Mathf.MoveTowards(hBankPop, 0f, udt * 3f);
            hBank.transform.parent.localScale = Vector3.one * (1f + 0.15f * hBankPop);

            int full = Mathf.FloorToInt(stars + 0.001f);
            float part = stars - full;
            hStarPop = Mathf.MoveTowards(hStarPop, 0f, udt * 2.5f);
            for (int i = 0; i < hStars.Length; i++)
            {
                bool on = i < full;
                bool fading = i == full && part > 0.01f;
                // étoiles allumées qui clignotent quand la police est sur toi
                float blink = on ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 10f) : 1f;
                hStars[i].color = on ? new Color(1f, 0.85f, 0.2f, blink) : fading ? new Color(1f, 0.85f, 0.2f, 0.25f + 0.5f * part) : new Color(1f, 1f, 1f, 0.22f);
                hStars[i].transform.localScale = Vector3.one * (on && i == full - 1 ? 1f + 0.5f * hStarPop : 1f);
            }
            for (int i = 0; i < hHearts.Length; i++) hHearts[i].color = i < hearts ? Color.white : new Color(0.2f, 0.2f, 0.22f, 0.6f);

            bool carry = carried > 0 && heroScreen.z > 0f;
            hCarryRt.gameObject.SetActive(carry);
            if (carry)
            {
                hCarryRt.anchoredPosition = ToCanvas(heroScreen) + Vector2.up * 30f;
                hCarry.text = carried + " $";
            }

            bool robbing = rob >= 0f && robScreen.z > 0f;
            hRobRt.gameObject.SetActive(robbing);
            if (robbing)
            {
                hRobRt.anchoredPosition = ToCanvas(robScreen);
                hRob.fillAmount = Mathf.Clamp01(rob);
                hRobRt.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(Time.unscaledTime * 14f));
            }

            hjBase.gameObject.SetActive(stick);
            hjKnob.gameObject.SetActive(stick);
            if (stick)
            {
                Vector2 a = ToCanvas(stickStart), b = ToCanvas(stickNow);
                Vector2 d = Vector2.ClampMagnitude(b - a, 110f);
                hjBase.anchoredPosition = a;
                hjKnob.anchoredPosition = a + d;
            }
        }

        public void HeistResult(bool won, int gain, int total)
        {
            heistRoot.gameObject.SetActive(false);
            hCarryRt.gameObject.SetActive(false);
            hRobRt.gameObject.SetActive(false);
            hjBase.gameObject.SetActive(false);
            hjKnob.gameObject.SetActive(false);
            hResult.gameObject.SetActive(true);
            hResult.SetAsLastSibling();
            hResTitle.text = won ? L("CLEAN GETAWAY!", "BRAQUAGE RÉUSSI !") : L("BUSTED!", "ARRÊTÉ !");
            hResTitle.color = won ? Good : Bad;
            hResGain.text = "+" + gain + " $";
            hResTotal.text = L("Total: ", "Total : ") + total + " $";
            hResBtn.text = won ? L("NEXT", "SUIVANT") : L("RETRY", "RÉESSAYER");
            LockClicks(0.6f);
        }
    }
}
