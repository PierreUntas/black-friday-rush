using UnityEngine;
using UnityEngine.UI;

namespace SamediRouge
{
    /// <summary>
    /// V3 battle royale : en haut, survivants et éliminations ; minuteur de zone ; fil des éliminations ;
    /// en bas, vie + gilet et arme équipée. Joystick flottant. Écran de fin (TOP 1 / #place).
    /// </summary>
    public partial class Hud
    {
        public System.Action onRoyaleNext;

        RectTransform rRoot, rjBase, rjKnob, rResult, rWeaponBox;
        Text rAlive, rKills, rZone, rWeapon, rResTitle, rResSub, rResGain, rResTotal, rResBtn, rHpT;
        Image rHp, rArmor, rHelmet, rWeaponIcon, rOutside;
        readonly Text[] rFeed = new Text[4];
        readonly float[] rFeedT = new float[4];
        float rPop;
        int rLastWeapon = -1;

        public void InitRoyale()
        {
            toastT.transform.SetParent(safe, false);
            play.SetActive(false);
            if (menu != null) menu.SetActive(false);
            if (pause != null) pause.SetActive(false);
            if (end != null) end.SetActive(false);
            if (demoRoot != null) demoRoot.SetActive(false);
            HideMeta();

            // voile bleu quand on est hors de la zone
            rOutside = Img(canvasRt, vignette, new Color(0.2f, 0.45f, 1f, 0f));
            Stretch(rOutside.rectTransform);
            rOutside.transform.SetAsFirstSibling();

            rRoot = New("royale", safe);
            Stretch(rRoot);

            // survivants
            Image a = Img(rRoot, round, Panel);
            Place(a.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -40f), new Vector2(230f, 90f), new Vector2(0f, 1f));
            Image ai = Img(a.rectTransform, GearIcons.Art("gang") ?? round, Color.white);
            Place(ai.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(52f, 0f), new Vector2(82f, 82f));
            rAlive = Label(a.rectTransform, "", 50, Color.white, TextAnchor.MiddleCenter, true);
            Place(rAlive.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(50f, 0f), new Vector2(-100f, 0f));

            // éliminations
            Image k = Img(rRoot, round, Panel);
            Place(k.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -40f), new Vector2(230f, 90f), new Vector2(1f, 1f));
            rKills = Label(k.rectTransform, "", 44, Yellow, TextAnchor.MiddleCenter, true);
            Stretch(rKills.rectTransform);

            // minuteur de zone
            rZone = Label(rRoot, "", 42, Color.white, TextAnchor.MiddleCenter, true);
            Shadowed(rZone);
            Place(rZone.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -85f), new Vector2(520f, 70f));

            // fil des éliminations
            for (int i = 0; i < rFeed.Length; i++)
            {
                rFeed[i] = Label(rRoot, "", 32, Info, TextAnchor.MiddleRight, true);
                Shadowed(rFeed[i]);
                Place(rFeed[i].rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -170f - i * 46f), new Vector2(760f, 44f), new Vector2(1f, 0.5f));
            }

            // vie + gilet
            Image bar = Img(rRoot, round, Panel);
            Place(bar.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(620f, 58f), new Vector2(0.5f, 0f));
            rHp = Img(bar.rectTransform, white, new Color(0.35f, 0.88f, 0.42f));
            rHp.type = Image.Type.Filled;
            rHp.fillMethod = Image.FillMethod.Horizontal;
            Place(rHp.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-14f, -14f));
            rArmor = Img(bar.rectTransform, white, new Color(0.35f, 0.6f, 1f));
            rArmor.type = Image.Type.Filled;
            rArmor.fillMethod = Image.FillMethod.Horizontal;
            Place(rArmor.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 12f), new Vector2(-14f, 12f));
            rHpT = Label(bar.rectTransform, "", 34, Color.white, TextAnchor.MiddleCenter, true);
            Shadowed(rHpT);
            Stretch(rHpT.rectTransform);
            rHelmet = Img(bar.rectTransform, GearIcons.Upgrade(0, true, 3) ?? round, Color.white);
            Place(rHelmet.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-50f, 0f), new Vector2(78f, 78f));

            // arme
            Image wb = Img(rRoot, round, Panel);
            rWeaponBox = wb.rectTransform;
            Place(rWeaponBox, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(380f, 96f), new Vector2(0.5f, 0f));
            rWeaponIcon = Img(rWeaponBox, round, Color.white);
            rWeaponIcon.preserveAspect = true;
            Place(rWeaponIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(58f, 0f), new Vector2(84f, 84f));
            rWeapon = Label(rWeaponBox, "", 38, Color.white, TextAnchor.MiddleCenter, true);
            Place(rWeapon.rectTransform, Vector2.zero, Vector2.one, new Vector2(46f, 0f), new Vector2(-100f, 0f));

            // joystick
            rjBase = Img(canvasRt, dot, new Color(1f, 1f, 1f, 0.16f)).rectTransform;
            Place(rjBase, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260f, 260f));
            rjKnob = Img(canvasRt, dot, new Color(1f, 1f, 1f, 0.55f)).rectTransform;
            Place(rjKnob, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f));

            // écran de fin
            rResult = New("royale_result", safe);
            Stretch(rResult);
            Image dim = Img(rResult, white, new Color(0f, 0f, 0f, 0.55f));
            Place(dim.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(400f, 400f));
            Image card = Img(rResult, round, Panel);
            Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 140f), new Vector2(860f, 760f));
            rResTitle = Label(card.rectTransform, "", 130, Yellow, TextAnchor.MiddleCenter, true);
            Shadowed(rResTitle);
            Place(rResTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -130f), new Vector2(-40f, 180f));
            rResSub = Label(card.rectTransform, "", 44, Color.white, TextAnchor.MiddleCenter, true);
            Place(rResSub.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -270f), new Vector2(-40f, 80f));
            rResGain = Label(card.rectTransform, "", 100, Yellow, TextAnchor.MiddleCenter, true);
            Shadowed(rResGain);
            Place(rResGain.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -70f), new Vector2(-40f, 150f));
            rResTotal = Label(card.rectTransform, "", 40, Muted, TextAnchor.MiddleCenter, true);
            Place(rResTotal.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 90f), new Vector2(-40f, 80f));
            Image btn = Img(rResult, round, Yellow);
            Place(btn.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -340f), new Vector2(640f, 150f));
            rResBtn = Label(btn.rectTransform, L("PLAY AGAIN", "REJOUER"), 60, new Color(0.12f, 0.1f, 0.05f), TextAnchor.MiddleCenter, true);
            Stretch(rResBtn.rectTransform);
            AddButton(btn.rectTransform, () => { rResult.gameObject.SetActive(false); onRoyaleNext?.Invoke(); });

            RoyaleHide();
        }

        public void RoyaleHide()
        {
            rRoot.gameObject.SetActive(false);
            rjBase.gameObject.SetActive(false);
            rjKnob.gameObject.SetActive(false);
            rResult.gameObject.SetActive(false);
            rOutside.color = new Color(0.2f, 0.45f, 1f, 0f);
            for (int i = 0; i < rFeed.Length; i++) { rFeed[i].text = ""; rFeedT[i] = 0f; }
            rLastWeapon = -1;
        }

        public void RoyaleShow()
        {
            rRoot.gameObject.SetActive(true);
            rResult.gameObject.SetActive(false);
        }

        public void RoyalePop() { rPop = 1f; }

        /// <summary>Ajoute une ligne en haut du fil des éliminations.</summary>
        public void RoyaleFeed(string msg, Color c)
        {
            for (int i = rFeed.Length - 1; i > 0; i--)
            {
                rFeed[i].text = rFeed[i - 1].text;
                rFeed[i].color = rFeed[i - 1].color;
                rFeedT[i] = rFeedT[i - 1];
            }
            rFeed[0].text = msg;
            rFeed[0].color = c;
            rFeedT[0] = 4.5f;
        }

        Vector2 RToCanvas(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, null, out Vector2 lp);
            return lp;
        }

        public void RoyaleTick(float hp, float armor, bool helmet, int weapon, int alive, int kills, float zoneTimer, bool zoneMoving, bool outside, bool stick, Vector2 stickStart, Vector2 stickNow)
        {
            float udt = Time.unscaledDeltaTime;
            rAlive.text = alive.ToString();
            rKills.text = kills + " KO";
            rZone.text = zoneMoving ? L("ZONE CLOSING", "LA ZONE SE RESSERRE") : zoneTimer >= 0f ? L("Zone: ", "Zone : ") + Mathf.CeilToInt(zoneTimer) + " s" : "";
            rZone.color = zoneMoving ? new Color(0.5f, 0.75f, 1f) : Color.white;

            rHp.fillAmount = Mathf.Clamp01(hp / 100f);
            rHp.color = hp > 50f ? new Color(0.35f, 0.88f, 0.42f) : hp > 25f ? new Color(1f, 0.75f, 0.2f) : new Color(1f, 0.3f, 0.25f);
            rArmor.fillAmount = Mathf.Clamp01(armor / 50f);
            rArmor.enabled = armor > 0f;
            rHpT.text = Mathf.CeilToInt(Mathf.Max(0f, hp)).ToString();
            rHelmet.enabled = helmet;

            if (weapon != rLastWeapon)
            {
                rLastWeapon = weapon;
                Royale.Weapon w = Royale.Weapons[Mathf.Clamp(weapon, 0, Royale.Weapons.Length - 1)];
                rWeapon.text = w.name;
                rWeapon.color = w.color;
                Sprite s = weapon == 0 ? GearIcons.Art("gang") : GearIcons.Upgrade(2, true, Mathf.Clamp(weapon + 1, 1, 3));
                rWeaponIcon.sprite = s;
                rWeaponIcon.enabled = s != null;
                rPop = 1f;
            }
            rPop = Mathf.MoveTowards(rPop, 0f, udt * 3f);
            rWeaponBox.localScale = Vector3.one * (1f + 0.15f * rPop);

            for (int i = 0; i < rFeed.Length; i++)
            {
                if (rFeedT[i] <= 0f) continue;
                rFeedT[i] -= udt;
                Color c = rFeed[i].color;
                c.a = Mathf.Clamp01(rFeedT[i] / 0.6f);
                rFeed[i].color = c;
                if (rFeedT[i] <= 0f) rFeed[i].text = "";
            }

            float wantA = outside ? 0.55f + 0.15f * Mathf.Sin(Time.unscaledTime * 6f) : 0f;
            Color oc = rOutside.color;
            oc.a = Mathf.MoveTowards(oc.a, wantA, udt * 2f);
            rOutside.color = oc;

            rjBase.gameObject.SetActive(stick);
            rjKnob.gameObject.SetActive(stick);
            if (stick)
            {
                Vector2 a = RToCanvas(stickStart), b = RToCanvas(stickNow);
                rjBase.anchoredPosition = a;
                rjKnob.anchoredPosition = a + Vector2.ClampMagnitude(b - a, 110f);
            }
        }

        public void RoyaleResult(int place, int players, int kills, int gain, int total)
        {
            rRoot.gameObject.SetActive(false);
            rjBase.gameObject.SetActive(false);
            rjKnob.gameObject.SetActive(false);
            rOutside.color = new Color(0.2f, 0.45f, 1f, 0f);
            rResult.gameObject.SetActive(true);
            rResult.SetAsLastSibling();
            rResTitle.text = place == 1 ? L("TOP 1!", "TOP 1 !") : "#" + place;
            rResTitle.color = place == 1 ? Yellow : place <= 5 ? Good : Color.white;
            rResSub.text = (place == 1 ? L("Last one standing", "Dernier survivant") : L("out of ", "sur ") + players) + "  ·  " + kills + " KO";
            rResGain.text = "+" + gain + " $";
            rResTotal.text = L("Total: ", "Total : ") + total + " $";
            LockClicks(0.8f);
        }
    }
}
