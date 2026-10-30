using UnityEngine;
using UnityEngine.UI;

namespace SamediRouge
{
    /// <summary>
    /// Écrans de méta-progression « Street Conquest » : carte du quartier, améliorations, résultats, panneau debug.
    /// Partie de Hud (mêmes outils de construction), séparée pour pouvoir brancher une autre méta (version B).
    /// </summary>
    public partial class Hud
    {
        public static readonly Color GuardBlue = new Color(0.22f, 0.5f, 1f);
        public static readonly Color ThiefRed = new Color(0.92f, 0.22f, 0.2f);

        // rappels vers le jeu
        public System.Action<int> onStreet, onBuyGear, onDebug;
        public System.Action<bool> onFaction;
        public System.Action onPlayStreet, onUpgrades, onTutorial, onBackToMap, onRetry, onContinue, onDebugOpen, onLang;

        /// <summary>Nombre de cœurs affichés pendant la course (dépend du gilet).</summary>
        public int maxHearts = 3;
        /// <summary>Case d'équipement à faire clignoter (« NEXT »), -1 pour aucune.</summary>
        public int nextGearSlot = -1;

        GameObject mapRoot, upgRoot, resRoot, dbgRoot;

        // jauge de vie (rouge = dégâts, vert = soin)
        Image hpBg, hpLag, hpFill, hpGlow, healOverlay;
        Text hpText;
        float hpShown = -1f, hpLagV = 1f, hpLagDelay, hpGlowA, healA, lowPulse;
        int hpLast = -1;
        public static readonly Color HealGreen = new Color(0.3f, 0.9f, 0.4f);
        public static readonly Color StarGold = new Color(1f, 0.82f, 0.2f);
        float clickLockUntil;
        void LockClicks(float s) { clickLockUntil = Time.unscaledTime + s; }

        // carte
        Text mapCoins, mapCount, mapNext, playLabel;
        Image playBg, facGuard, facThief;
        Image[] stBg, stFill, stGlow;
        Text[] stName, stSub, stTag;
        float mapT, conquerAnimT = -1f;
        int conquerStreet = -1, selStreet;
        bool mapThief;
        bool[] mapConq = new bool[Tuning.Streets];
        int mapNextIdx, mapIdxShown, mapNewTab = -1;
        // déverrouillage d'un niveau : bannière animée puis bascule sur la nouvelle map
        Image unlockBanner;
        Text unlockTitle, unlockSub;
        float unlockT = -1f;
        int pendingUnlock = -1;
        Text resContLabel;

        // améliorations
        Text upgCoins;
        Text[] gName, gLevel, gNow, gNext, gCost;
        Image[] gBuy;
        Image[] gTile = new Image[8], gIcon = new Image[8], gNextIcon = new Image[8];
        Image[,] gTiles, gTileIcon, gTileCheck, gTileRing;
        Image[,] gPips;

        // résultats
        Text resTag, resTitle, resBody, resTip;
        Image resTagBg;
        GameObject resRetry, resUpg, resMap, resCont;

        // debug
        Text[] dbgLabels;
        Text inputDbg;
        float inputDbgT;

        const int SlotCount = 5;
        readonly int[] slotStreet = new int[SlotCount];
        Image[] tabBg;
        Text[] tabLabel;
        int mapIdx;
        public System.Action<int> onMapTab;

        // ------------------------------------------------------------------ construction

        Image Box(Transform parent, Color c, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            Image i = Img(parent, round, c);
            Place(i.rectTransform, anchor, anchor, pos, size);
            return i;
        }

        /// <summary>Icône illustrée à gauche du texte d'un bouton (rien si l'image manque).</summary>
        void ButtonIcon(RectTransform b, Text t, Sprite s)
        {
            if (s == null) return;
            Image ic = Img(b, s, Color.white);
            ic.preserveAspect = true;
            Place(ic.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(96f, 96f), new Vector2(0f, 0.5f));
            RectTransform r = t.rectTransform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(124f, 4f); r.offsetMax = new Vector2(-16f, -4f);
        }

        Image Button(Transform parent, string label, Color c, Vector2 anchor, Vector2 pos, Vector2 size, int font, System.Action act, out Text t)
        {
            Image b = Box(parent, c, anchor, pos, size);
            t = Label(b.rectTransform, label, font, Color.white, TextAnchor.MiddleCenter, true);
            Stretch(t.rectTransform);
            t.rectTransform.sizeDelta = new Vector2(-24f, -8f);
            Fit(t, Mathf.Max(16, font * 55 / 100));
            AddButton(b.rectTransform, act);
            return b;
        }

        /// <summary>
        /// Étoile de police (insigne de shérif) : 6 branches à boules, liseré sombre, médaillon central.
        /// Rendue en niveaux de gris pour être teintée en or.
        /// </summary>
        static Sprite bagSprite;

        /// <summary>Sac de butin (icône du camp des voleurs) : sac en toile noué, corde sombre, contour épais. Le « $ » est un texte posé dessus.</summary>
        static Sprite MakeBag()
        {
            if (bagSprite != null) return bagSprite;
            const int n = 96;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            Color cloth = new Color(0.86f, 0.69f, 0.43f), clothDark = new Color(0.72f, 0.54f, 0.3f), rope = new Color(0.42f, 0.26f, 0.12f), line = new Color(0.16f, 0.1f, 0.06f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float body = Vector2.Distance(p, new Vector2(48f, 36f)) - 31f;
                    float neck = Mathf.Max(Mathf.Abs(p.x - 48f) - 9f, Mathf.Abs(p.y - 68f) - 7f);
                    float flapL = Vector2.Distance(p, new Vector2(38f, 82f)) - 9f;
                    float flapR = Vector2.Distance(p, new Vector2(58f, 82f)) - 9f;
                    float flapM = Vector2.Distance(p, new Vector2(48f, 79f)) - 7f;
                    float d = Mathf.Min(Mathf.Min(body, neck), Mathf.Min(Mathf.Min(flapL, flapR), flapM));
                    if (d > 3.5f) { px[y * n + x] = new Color32(0, 0, 0, 0); continue; }
                    Color c;
                    float a = 1f;
                    if (d > 0f) { c = line; a = Mathf.Clamp01(3.5f - d); }
                    else if (Mathf.Abs(p.y - 66f) < 3.5f && Mathf.Abs(p.x - 48f) < 14f) c = rope;
                    else c = (p.x - 48f) * 0.7f - (p.y - 36f) * 0.3f > 12f ? clothDark : cloth;
                    px[y * n + x] = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), (byte)(a * 255f));
                }
            t.SetPixels32(px);
            t.Apply();
            bagSprite = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return bagSprite;
        }

        /// <summary>Icône du camp : étoile de shérif dorée (policiers) ou sac de butin marqué « $ » (voleurs).</summary>
        Image FactionIcon(RectTransform parent, bool thiefSide, Vector2 anchor, Vector2 pos, float size)
        {
            Sprite art = GearIcons.Art(thiefSide ? "faction_thief" : "faction_cop");
            if (art != null)
            {
                Image a = Img(parent, art, Color.white);
                Place(a.rectTransform, anchor, anchor, pos, new Vector2(size * 1.1f, size * 1.1f));
                return a;
            }
            Image ic = Img(parent, thiefSide ? MakeBag() : star, thiefSide ? Color.white : StarGold);
            Place(ic.rectTransform, anchor, anchor, pos, new Vector2(size, size));
            if (thiefSide)
            {
                Text dollar = Label(ic.rectTransform, "$", Mathf.RoundToInt(size * 0.42f), new Color(0.12f, 0.32f, 0.12f), TextAnchor.MiddleCenter, true);
                Place(dollar.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, -size * 0.12f), Vector2.zero);
            }
            return ic;
        }

        /// <summary>Icône de la monnaie : un « $ » doré en gras (support transparent, le texte s'adapte à la taille de l'icône).</summary>
        Image DollarIcon(Transform parent)
        {
            Sprite art = GearIcons.Art("dollar");
            if (art != null) return Img(parent, art, Color.white);
            Image holder = Img(parent, round, Color.clear);
            Text d = Label(holder.rectTransform, "$", 140, StarGold, TextAnchor.MiddleCenter, true);
            Stretch(d.rectTransform);
            Fit(d, 10);
            var sh = d.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.55f);
            sh.effectDistance = new Vector2(2f, -3f);
            return holder;
        }

        // ------------------------------------------------------------------ fiche joueur (carte)

        RectTransform profCard;
        Image profBadge, profStar, profBag, profBarFill;
        Text profRank, profProgress, profGearTitle;
        Image[] profGearIcon, profGearBg, profGearGlow, profGearPlus;
        Text[] profGearItem = new Text[4];
        Image[,] profGearPip;
        const float ProfW = 1000f, ProfH = 272f;

        void BuildProfile(RectTransform sa)
        {
            Image card = Box(sa, new Color(0.08f, 0.1f, 0.14f, 0.94f), new Vector2(0.5f, 0f), new Vector2(0f, 1900f), new Vector2(ProfW, ProfH));
            profCard = card.rectTransform;
            // insigne du camp dans un médaillon coloré
            Image ring = Img(profCard, dot, new Color(1f, 1f, 1f, 0.9f)); // liseré blanc, dessiné avant le médaillon
            Place(ring.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(95f, -86f), new Vector2(150f, 150f));
            profBadge = Img(profCard, dot, GuardBlue);
            Place(profBadge.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(95f, -86f), new Vector2(140f, 140f));
            profStar = FactionIcon(profBadge.rectTransform, false, new Vector2(0.5f, 0.5f), Vector2.zero, 104f);
            profBag = FactionIcon(profBadge.rectTransform, true, new Vector2(0.5f, 0.5f), Vector2.zero, 108f);
            // grade
            profRank = Label(profCard, "", 44, Color.white, TextAnchor.MiddleLeft, true);
            Shadowed(profRank);
            Place(profRank.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(190f, -22f), new Vector2(520f, 58f), new Vector2(0f, 1f));
            Fit(profRank, 26);
            // progression : rues conquises (toutes maps) + barre
            profProgress = Label(profCard, "", 26, Muted, TextAnchor.MiddleLeft, true);
            Place(profProgress.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(190f, -84f), new Vector2(520f, 36f), new Vector2(0f, 1f));
            Image bar = Img(profCard, round, new Color(1f, 1f, 1f, 0.12f));
            Place(bar.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(190f, -126f), new Vector2(520f, 20f), new Vector2(0f, 1f));
            profBarFill = Img(bar.rectTransform, round, StarGold);
            Place(profBarFill.rectTransform, Vector2.zero, new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f));
            // argent
            Image coinBg = Box(profCard, new Color(0.03f, 0.04f, 0.06f, 0.9f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(240f, 72f));
            coinBg.rectTransform.pivot = new Vector2(1f, 1f);
            mapCoins = Label(coinBg.rectTransform, "", 42, StarGold, TextAnchor.MiddleCenter, true);
            Stretch(mapCoins.rectTransform);
            Fit(mapCoins, 24);
            // niveaux d'équipement : 4 pastilles (nom + 3 points)
            int ng = Tuning.GearCount;
            profGearIcon = new Image[ng]; profGearBg = new Image[ng]; profGearGlow = new Image[ng]; profGearPlus = new Image[ng];
            profGearPip = new Image[ng, Tuning.MaxLevel];
            float w = (ProfW - 40f) / ng;
            for (int g = 0; g < ng; g++)
            {
                Vector2 pos = new Vector2(20f + g * w + 6f, 14f), size = new Vector2(w - 12f, 84f);
                // halo jaune : amélioration achetable maintenant
                profGearGlow[g] = Img(profCard, round, Yellow);
                Place(profGearGlow[g].rectTransform, Vector2.zero, Vector2.zero, pos - new Vector2(5f, 5f), size + new Vector2(10f, 10f), Vector2.zero);
                Image chip = Img(profCard, round, new Color(1f, 1f, 1f, 0.06f));
                Place(chip.rectTransform, Vector2.zero, Vector2.zero, pos, size, Vector2.zero);
                profGearBg[g] = chip;
                profGearIcon[g] = Img(chip.rectTransform, GearIcons.Upgrade(g, false, 1), Color.white);
                Place(profGearIcon[g].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(76f, 76f));
                for (int k = 0; k < Tuning.MaxLevel; k++)
                {
                    profGearPip[g, k] = Img(chip.rectTransform, round, Muted);
                    profGearPip[g, k].gameObject.SetActive(false); // plus de points : le nom de l'objet suffit
                }
                profGearItem[g] = Label(chip.rectTransform, "", 20, Color.white, TextAnchor.MiddleLeft, true);
                profGearItem[g].gameObject.SetActive(false); // l'icône seule suffit (nom visible dans l'écran Équipement)
                Place(profGearItem[g].rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(42f, 0f), new Vector2(-96f, -8f), new Vector2(0.5f, 0.5f));
                Fit(profGearItem[g], 12);
                // badge « + »
                profGearPlus[g] = Img(chip.rectTransform, dot, new Color(0.25f, 0.82f, 0.48f));
                Place(profGearPlus[g].rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-4f, -4f), new Vector2(34f, 34f));
                Text plus = Label(profGearPlus[g].rectTransform, "+", 30, new Color(0.03f, 0.14f, 0.06f), TextAnchor.MiddleCenter, true);
                Stretch(plus.rectTransform);
            }
        }

        /// <summary>Remplit la fiche et la place dans l'espace libre au-dessus des boutons de camp (réduite sur les écrans courts).</summary>
        void UpdateProfile(bool thief)
        {
            if (profCard == null) return;
            Color side = thief ? ThiefRed : GuardBlue;
            profBadge.color = side;
            profStar.gameObject.SetActive(!thief);
            profBag.gameObject.SetActive(thief);
            int top = 0;
            for (int m = 0; m < Tuning.Maps; m++) if (Progress.MapUnlocked(thief, m)) top = m;
            string[] ranks = thief ? Tuning.RankThief : Tuning.RankGuard;
            profRank.text = ranks[Mathf.Min(top, ranks.Length - 1)];
            int done = Progress.ConqueredCount(thief);
            profProgress.text = L("STREETS ", "RUES ") + done + "/" + Tuning.Streets + "  ·  " + L("LEVEL ", "NIVEAU ") + (top + 1);
            profBarFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(done / (float)Tuning.Streets), 1f);
            profBarFill.color = side;
            int coinsNow = Progress.Coins;
            for (int g = 0; g < Tuning.GearCount; g++)
            {
                int lv = Progress.GearLevel(thief, g), cost = Progress.NextCost(thief, g);
                bool maxed = cost < 0, afford = !maxed && coinsNow >= cost;
                GearTile(profGearBg[g], profGearIcon[g], g, thief, lv, maxed);
                profGearGlow[g].gameObject.SetActive(afford);
                profGearPlus[g].gameObject.SetActive(afford);
                profGearItem[g].text = lv > 0 ? Tuning.ItemName(g, thief, lv) : L("NONE", "AUCUN");
                profGearItem[g].color = maxed ? new Color(0.25f, 0.17f, 0.02f) : lv > 0 ? Color.white : Muted;
            }
            LayoutMapPage();
        }

        /// <summary>Tuile d'équipement : grisée (pas encore), couleur du camp (possédé), dorée (au maximum), avec l'icône du niveau atteint.</summary>
        void GearTile(Image bg, Image icon, int gear, bool thief, int lv, bool maxed)
        {
            Color side = thief ? ThiefRed : GuardBlue;
            bg.color = maxed ? new Color(1f, 0.8f, 0.25f) : lv > 0 ? new Color(side.r * 0.8f, side.g * 0.8f, side.b * 0.8f, 1f) : new Color(0.13f, 0.15f, 0.2f, 1f);
            icon.sprite = GearIcons.Upgrade(gear, thief, lv);
            icon.color = lv > 0 ? Color.white : new Color(1f, 1f, 1f, 0.3f);
        }

        RectTransform mapPanelRt, btnEquip, btnTuto, dbgRt, langRt;

        /// <summary>
        /// Page carte : une colonne calculée sur la hauteur réelle de l'écran (zone sûre comprise) — fiche joueur, camp,
        /// quartiers, carte, bouton principal, Équipement / Tutoriel, pied de page — avec des espacements égaux.
        /// Écran court : la carte rétrécit un peu ; écran long : les espacements grandissent (plafonnés) et le bloc est centré.
        /// </summary>
        void LayoutMapPage()
        {
            if (mapPanelRt == null || profCard == null) return;
            float canvasH = 1080f * Screen.height / Mathf.Max(1f, Screen.width);
            Rect safeR = Screen.safeArea;
            float topInset = Screen.height > 0 ? (Screen.height - safeR.yMax) / Screen.height * canvasH : 0f;
            float botInset = Screen.height > 0 ? safeR.yMin / Screen.height * canvasH : 0f;
            float top = canvasH - topInset - 28f, bottom = botInset + 26f;
            float avail = top - bottom;
            const float hProf = ProfH, hFac = 96f, hTab = 84f, hPlay = 160f, hRow = 130f, hFoot = 64f, hMap = MapPanelH;
            float fixedH = hProf + hFac + hTab + hPlay + hRow + hFoot;
            // espaces : fiche|camp, camp|quartiers (serré), quartiers|carte, carte|bouton, bouton|ligne (serré), ligne|pied
            float[] weight = { 1.2f, 0.6f, 1f, 1.2f, 0.6f, 0.9f };
            float wsum = 0f; foreach (float w in weight) wsum += w;
            float minUnit = 24f;
            float mapScale = Mathf.Clamp((avail - fixedH - wsum * minUnit) / hMap, 0.62f, 1f);
            float profScale = mapScale < 0.75f ? 0.85f : 1f;
            float used = fixedH - hProf * (1f - profScale) + hMap * mapScale;
            float unit = Mathf.Clamp((avail - used) / wsum, minUnit, 60f);
            float total = used + unit * wsum;
            float y = top - Mathf.Max(0f, (avail - total) * 0.5f);

            y -= hProf * profScale * 0.5f;
            profCard.localScale = Vector3.one * profScale;
            profCard.anchoredPosition = new Vector2(0f, y);
            y -= hProf * profScale * 0.5f + unit * weight[0];

            y -= hFac * 0.5f;
            facGuard.rectTransform.anchoredPosition = new Vector2(-255f, y);
            facThief.rectTransform.anchoredPosition = new Vector2(255f, y);
            y -= hFac * 0.5f + unit * weight[1];

            y -= hTab * 0.5f;
            for (int m2 = 0; m2 < tabBg.Length; m2++) { Vector2 p = tabBg[m2].rectTransform.anchoredPosition; tabBg[m2].rectTransform.anchoredPosition = new Vector2(p.x, y); }
            y -= hTab * 0.5f + unit * weight[2];

            y -= hMap * mapScale * 0.5f;
            mapPanelRt.localScale = Vector3.one * mapScale;
            mapPanelRt.anchoredPosition = new Vector2(0f, y);
            if (unlockBanner != null) unlockBanner.rectTransform.anchoredPosition = new Vector2(0f, y);
            y -= hMap * mapScale * 0.5f + unit * weight[3];

            y -= hPlay * 0.5f;
            playBg.rectTransform.anchoredPosition = new Vector2(0f, y);
            y -= hPlay * 0.5f + unit * weight[4];

            y -= hRow * 0.5f;
            btnEquip.anchoredPosition = new Vector2(255f, y);    // équipement à droite, tutoriel à gauche
            btnTuto.anchoredPosition = new Vector2(-255f, y);
            y -= hRow * 0.5f + unit * weight[5];

            y -= hFoot * 0.5f;
            langRt.anchoredPosition = new Vector2(30f, y);
            dbgRt.anchoredPosition = new Vector2(-30f, y);
        }

        static Sprite billSprite;

        /// <summary>Icône de la monnaie (CRED) : deux billets verts empilés, cadre clair, médaillon central et coins marqués.</summary>
        static Sprite MakeBill()
        {
            if (billSprite != null) return billSprite;
            const int n = 96;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            Color line = new Color(0.07f, 0.2f, 0.1f), back = new Color(0.25f, 0.55f, 0.3f), face = new Color(0.45f, 0.78f, 0.45f);
            Color frame = new Color(0.78f, 0.93f, 0.72f), mark = new Color(0.16f, 0.42f, 0.2f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    // billet du dessus (centré un peu bas) et billet du dessous (décalé en haut à droite)
                    float top = RBox(p, new Vector2(45f, 42f), new Vector2(40f, 23f), 5f);
                    float under = RBox(p, new Vector2(52f, 54f), new Vector2(40f, 23f), 5f);
                    Color c; float a = 1f;
                    if (top <= 3f)
                    {
                        if (top > 0f) { c = line; a = 1f; }
                        else
                        {
                            c = face;
                            float inner = RBox(p, new Vector2(45f, 42f), new Vector2(33f, 16f), 3f);
                            if (Mathf.Abs(inner) < 1.4f) c = frame;
                            float oval = Vector2.Distance(new Vector2((p.x - 45f) * 0.85f, p.y - 42f), Vector2.zero);
                            if (oval < 11f) c = oval > 8.5f ? mark : frame;
                            if (oval < 5f) c = mark;
                            foreach (var k in new[] { new Vector2(17f, 30f), new Vector2(73f, 30f), new Vector2(17f, 54f), new Vector2(73f, 54f) })
                                if (Vector2.Distance(p, k) < 3.2f) c = mark;
                        }
                    }
                    else if (under <= 3f) { c = under > 0f ? line : back; }
                    else { px[y * n + x] = new Color32(0, 0, 0, 0); continue; }
                    if (top > 0f && top <= 3f) a = Mathf.Clamp01(3.5f - top);
                    px[y * n + x] = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), (byte)(a * 255f));
                }
            t.SetPixels32(px);
            t.Apply();
            billSprite = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return billSprite;
        }

        static float RBox(Vector2 p, Vector2 c, Vector2 half, float r)
        {
            float qx = Mathf.Abs(p.x - c.x) - half.x + r, qy = Mathf.Abs(p.y - c.y) - half.y + r;
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        static Sprite MakeStar()
        {
            const int n = 96;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            var outer = new Vector2[12];
            var inner = new Vector2[12];
            Vector2 c = new Vector2(n * 0.5f, n * 0.5f);
            for (int i = 0; i < 12; i++)
            {
                float a = Mathf.PI * 0.5f + i * Mathf.PI / 6f;
                float r = i % 2 == 0 ? 38f : 20f;
                outer[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                inner[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (r - 6f);
            }
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float cov = 0f, shade = 0f;
                for (int sy = 0; sy < 2; sy++)
                for (int sx = 0; sx < 2; sx++)
                {
                    Vector2 p = new Vector2(x + 0.25f + sx * 0.5f, y + 0.25f + sy * 0.5f);
                    float d = (p - c).magnitude;
                    bool tip = false;
                    for (int k = 0; k < 6; k++)
                    {
                        float a = Mathf.PI * 0.5f + k * Mathf.PI / 3f;
                        if ((p - (c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 40f)).magnitude < 6.5f) tip = true;
                    }
                    bool body = InPoly(p, outer) || tip;
                    if (!body) continue;
                    cov += 0.25f;
                    // liseré sombre, corps clair, médaillon central avec anneau
                    float v = InPoly(p, inner) || tip ? 1f : 0.55f;
                    if (d < 15f) v = d > 12f ? 0.55f : 0.85f;
                    if (d < 6f) v = 1f;
                    shade += v * 0.25f;
                }
                byte g = (byte)(cov > 0f ? Mathf.Clamp01(shade / cov) * 255f : 0f);
                px[y * n + x] = new Color32(g, g, g, (byte)(cov * 255f));
            }
            t.SetPixels32(px);
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        static bool InPoly(Vector2 p, Vector2[] poly)
        {
            bool c = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if (((poly[i].y > p.y) != (poly[j].y > p.y)) && (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)) c = !c;
            return c;
        }

        void BuildHealth()
        {
            RectTransform p = (RectTransform)play.transform;
            hpGlow = Img(p, round, new Color(1f, 1f, 1f, 0f));
            Place(hpGlow.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -212f), new Vector2(628f, 100f), new Vector2(0f, 1f));
            // cadre clair + fond sombre
            Image frame = Img(p, round, new Color(0.92f, 0.94f, 0.97f, 0.95f));
            Place(frame.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -222f), new Vector2(604f, 80f), new Vector2(0f, 1f));
            hpFrame = frame;
            hpBg = Img(frame.rectTransform, round, new Color(0.08f, 0.09f, 0.12f, 1f));
            Place(hpBg.rectTransform, Vector2.zero, Vector2.one, new Vector2(30f, 0f), new Vector2(-72f, -12f));
            RectTransform b = hpBg.rectTransform;
            hpLag = Img(b, round, new Color(0.95f, 0.22f, 0.18f));
            Place(hpLag.rectTransform, Vector2.zero, new Vector2(1f, 1f), Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f));
            hpFill = Img(b, round, HealGreen);
            Place(hpFill.rectTransform, Vector2.zero, new Vector2(1f, 1f), Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f));
            // reflet en haut de la barre
            Image shine = Img(b, round, new Color(1f, 1f, 1f, 0.16f));
            Place(shine.rectTransform, new Vector2(0f, 0.55f), new Vector2(1f, 0.95f), Vector2.zero, new Vector2(-12f, 0f));
            // graduations tous les 25 PV
            hpTicks = new Image[7];
            for (int i = 0; i < hpTicks.Length; i++)
            {
                hpTicks[i] = Img(b, white, new Color(0f, 0f, 0f, 0.35f));
                Place(hpTicks[i].rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(3f, 0f));
                hpTicks[i].gameObject.SetActive(false);
            }
            hpText = Label(b, "", 36, Color.white, TextAnchor.MiddleCenter, true);
            Shadowed(hpText);
            Stretch(hpText.rectTransform);
            // médaillon cœur à gauche
            Image badge = Img(frame.rectTransform, dot, new Color(0.92f, 0.94f, 0.97f, 1f));
            Place(badge.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-14f, 0f), new Vector2(96f, 96f));
            Image badgeIn = Img(badge.rectTransform, dot, new Color(0.75f, 0.12f, 0.12f, 1f));
            Place(badgeIn.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80f, 80f));
            hpHeart = Img(badgeIn.rectTransform, heart, Color.white);
            Place(hpHeart.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -2f), new Vector2(50f, 46f));
            hpLabel = Label(frame.rectTransform, L("HP", "PV"), 26, new Color(0.1f, 0.12f, 0.16f), TextAnchor.MiddleCenter, true);
            Place(hpLabel.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-21f, 0f), new Vector2(40f, 0f));

            healOverlay = Img(canvasRt, vignette, new Color(0.3f, 0.9f, 0.4f, 0f));
            Stretch(healOverlay.rectTransform);
            healOverlay.transform.SetSiblingIndex(1);
            critOverlay = Img(canvasRt, vignette, new Color(0.9f, 0.08f, 0.06f, 0f));
            Stretch(critOverlay.rectTransform);
            critOverlay.transform.SetSiblingIndex(1);

            // compteur d'étoiles de police (en haut à droite) : discret, il « pope » à chaque gain
            Image chip = Img(p, round, new Color(0.06f, 0.08f, 0.11f, 0.82f));
            Place(chip.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -228f), new Vector2(240f, 66f), new Vector2(1f, 1f));
            starChip = chip;
            starIcon = DollarIcon(chip.rectTransform);
            Place(starIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-6f, 0f), new Vector2(84f, 84f), new Vector2(0f, 0.5f));
            starCount = Label(chip.rectTransform, "0", 40, StarGold, TextAnchor.MiddleRight, true);
            Shadowed(starCount);
            Place(starCount.rectTransform, Vector2.zero, Vector2.one, new Vector2(-18f, 0f), new Vector2(-110f, 0f), new Vector2(1f, 0.5f));
        }

        Color hpFrameBase = new Color(0.92f, 0.94f, 0.97f, 0.95f);
        Image hpFrame, hpHeart, critOverlay, starChip, starIcon;
        Image[] hpTicks;
        Text hpLabel, starCount;
        int starShown = -1;
        float starPop, critT;

        /// <summary>Étoiles gagnées pendant la course (compteur du coin).</summary>
        public void SetStars(int n)
        {
            if (starCount == null) return;
            if (starShown >= 0 && n > starShown) starPop = 1f;
            starShown = n;
            starCount.text = n.ToString();
        }

        static void SetBar(Image img, float f)
        {
            RectTransform rt = img.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(f), 1f);
            rt.sizeDelta = Vector2.zero;
            img.enabled = f > 0.005f;
        }

        /// <summary>Jauge de vie : la barre suit la vie, une traîne rouge montre les dégâts, un halo vert les soins.</summary>
        void SetHealth(int hp, int max)
        {
            if (hpFill == null) return;
            float f = max > 0 ? Mathf.Clamp01(hp / (float)max) : 0f;
            if (hpLast >= 0 && hp < hpLast) { hpLagDelay = 0.35f; hpGlowA = 0f; }
            if (hpLast >= 0 && hp > hpLast) { hpGlowA = 1f; healA = 0.45f; hpLagV = f; }
            if (hpShown < 0f) { hpShown = f; hpLagV = f; }
            hpLast = hp;
            hpText.text = Mathf.Max(0, hp) + " / " + max;
            hpTarget = f;
            int segs = Mathf.Max(1, max / Tuning.DamagePerHit);
            for (int i = 0; i < hpTicks.Length; i++)
            {
                bool on = i < segs - 1;
                if (hpTicks[i].gameObject.activeSelf != on) hpTicks[i].gameObject.SetActive(on);
                if (on) hpTicks[i].rectTransform.anchorMin = hpTicks[i].rectTransform.anchorMax = new Vector2((i + 1f) / segs, 0f);
                if (on) { hpTicks[i].rectTransform.anchorMax = new Vector2((i + 1f) / segs, 1f); hpTicks[i].rectTransform.sizeDelta = new Vector2(3f, 0f); hpTicks[i].rectTransform.anchoredPosition = Vector2.zero; }
            }
        }

        float hpTarget = 1f;

        /// <summary>Remet la jauge à zéro (nouvelle course).</summary>
        public void ResetHealth() { hpShown = -1f; hpLast = -1; starShown = -1; critT = 0f; SetStars(0); }

        void TickHealth(float udt)
        {
            if (hpFill == null || !play.activeSelf) return;
            hpShown = Mathf.MoveTowards(hpShown, hpTarget, udt * 1.6f);
            if (hpLagDelay > 0f) hpLagDelay -= udt;
            else hpLagV = Mathf.MoveTowards(hpLagV, hpShown, udt * 0.6f);
            if (hpLagV < hpShown) hpLagV = hpShown;
            SetBar(hpFill, hpShown);
            SetBar(hpLag, hpLagV);
            // vert en bonne santé, jaune puis rouge quand la vie baisse ; pulsation sous 30 %
            Color c = hpShown > 0.5f ? HealGreen : hpShown > 0.3f ? Color.Lerp(new Color(1f, 0.3f, 0.2f), new Color(1f, 0.8f, 0.2f), (hpShown - 0.3f) / 0.2f) : new Color(1f, 0.3f, 0.2f);
            if (hpShown <= 0.3f) { lowPulse += udt * 6f; c = Color.Lerp(c, Color.white, 0.25f + 0.25f * Mathf.Sin(lowPulse)); }
            hpFill.color = c;
            hpGlowA = Mathf.MoveTowards(hpGlowA, 0f, udt * 1.5f);
            hpGlow.color = new Color(HealGreen.r, HealGreen.g, HealGreen.b, hpGlowA * 0.8f);
            hpBg.transform.localScale = Vector3.one * (1f + hpGlowA * 0.08f + (hpLagDelay > 0f ? 0.04f * Mathf.Sin(hpLagDelay * 60f) : 0f));
            healA = Mathf.MoveTowards(healA, 0f, udt * 1.2f);
            healOverlay.color = new Color(0.3f, 0.9f, 0.4f, healA);

            // vie critique : bords rouges qui battent comme un cœur, médaillon qui pulse
            bool crit = hpShown > 0f && hpShown <= 0.3f;
            if (crit)
            {
                critT += udt;
                float beat = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(critT * 7f)), 6f);
                critOverlay.color = new Color(0.9f, 0.08f, 0.06f, 0.12f + 0.28f * beat);
                hpHeart.transform.localScale = Vector3.one * (1f + 0.25f * beat);
                hpFrame.color = Color.Lerp(hpFrameBase, new Color(0.75f, 0.12f, 0.1f, 0.95f), beat);
                if (Mathf.Repeat(critT * 7f / (2f * Mathf.PI), 1f) < udt * 7f / (2f * Mathf.PI) + 0.001f)
                    Snd.PlayPitch(Snd.I != null ? Snd.I.bonk : null, 0.12f, 0.55f);
            }
            else
            {
                critOverlay.color = new Color(0.9f, 0.08f, 0.06f, Mathf.MoveTowards(critOverlay.color.a, 0f, udt));
                hpHeart.transform.localScale = Vector3.one;
                hpFrame.color = hpFrameBase;
            }

            starPop = Mathf.MoveTowards(starPop, 0f, udt * 4f);
            starIcon.transform.localScale = Vector3.one * (1f + 0.35f * starPop);
            starIcon.transform.localRotation = Quaternion.Euler(0f, 0f, starPop * 25f);
            starChip.transform.localScale = Vector3.one * (1f + 0.06f * starPop);
        }

        void BuildMeta()
        {
            BuildHealth();
            CompactPlayLayout();
            BuildMap();
            BuildUpgrades();
            BuildResult();
            BuildDebug();
            inputDbg = Label(canvasRt, "", 54, Yellow, TextAnchor.MiddleCenter, true);
            Shadowed(inputDbg);
            Place(inputDbg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(900f, 90f));
        }

        void BuildMap()
        {
            mapRoot = New("map", canvasRt).gameObject;
            RectTransform m = (RectTransform)mapRoot.transform;
            Stretch(m);
            // fond plein et sombre : la page est une interface, le décor 3D n'y apporte rien
            Image bgFull = Img(m, white, new Color(0.06f, 0.075f, 0.105f, 1f));
            Stretch(bgFull.rectTransform);
            Image fade = Img(m, fadeUp, new Color(0.1f, 0.13f, 0.19f, 1f));
            Place(fade.rectTransform, new Vector2(0f, 0.45f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            fade.transform.localScale = new Vector3(1f, -1f, 1f);
            RectTransform sa = New("safe", m);
            Stretch(sa);

            // fiche joueur (en haut) : insigne du camp, grade, progression, argent, niveaux d'équipement
            BuildProfile(sa);

            // camp
            facGuard = Button(sa, L("COPS", "POLICIERS"), GuardBlue, new Vector2(0.5f, 0f), new Vector2(-255f, 1660f), new Vector2(490f, 96f), 40, () => onFaction?.Invoke(false), out _);
            facThief = Button(sa, L("ROBBERS", "VOLEURS"), ThiefRed, new Vector2(0.5f, 0f), new Vector2(255f, 1660f), new Vector2(490f, 96f), 40, () => onFaction?.Invoke(true), out _);
            FactionIcon(facGuard.rectTransform, false, new Vector2(0f, 0.5f), new Vector2(52f, 0f), 70f);
            FactionIcon(facThief.rectTransform, true, new Vector2(0f, 0.5f), new Vector2(52f, 0f), 74f);
            if (Tuning.OnlyThief) { facGuard.gameObject.SetActive(false); facThief.gameObject.SetActive(false); }

            // onglets des maps (quartiers)
            tabBg = new Image[Tuning.Maps];
            tabLabel = new Text[Tuning.Maps];
            for (int m2 = 0; m2 < Tuning.Maps; m2++)
            {
                int mi = m2;
                float step = 1020f / Tuning.Maps;
                float w = step - 20f;
                float x = -510f + (m2 + 0.5f) * step;
                tabBg[m2] = Button(sa, "", new Color(0.16f, 0.19f, 0.25f), new Vector2(0.5f, 0f), new Vector2(x, 1548f), new Vector2(w, 84f), 30, () => onMapTab?.Invoke(mi), out tabLabel[m2]);
            }

            // carte du quartier : vrai petit plan de ville découpé en zones (voir HudMap.cs)
            Image panel = Box(sa, new Color(0.1f, 0.12f, 0.16f, 0.96f), new Vector2(0.5f, 0f), new Vector2(0f, 1010f), new Vector2(1000f, MapPanelH));
            RectTransform pr = panel.rectTransform;
            mapPanelRt = pr;
            BuildCityMap(pr);
            Image head = Img(pr, round, new Color(0.08f, 0.09f, 0.12f, 0.82f));
            Place(head.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -10f), new Vector2(-20f, 64f), new Vector2(0.5f, 1f));
            mapCount = Label(pr, "", 34, Color.white, TextAnchor.MiddleLeft, true);
            Shadowed(mapCount);
            Place(mapCount.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -42f), new Vector2(420f, 44f), new Vector2(0f, 0.5f));
            mapNext = Label(pr, "", 28, Yellow, TextAnchor.MiddleRight, true);
            Place(mapNext.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -42f), new Vector2(540f, 44f), new Vector2(1f, 0.5f));

            // actions
            playBg = Button(sa, "", Yellow, new Vector2(0.5f, 0f), new Vector2(0f, 400f), new Vector2(1000f, 160f), 50, () => onPlayStreet?.Invoke(), out playLabel);
            playLabel.color = new Color(0.12f, 0.1f, 0.04f);
            btnEquip = Button(sa, L("EQUIPMENT", "ÉQUIPEMENT"), Blue, new Vector2(0.5f, 0f), new Vector2(255f, 225f), new Vector2(490f, 130f), 40, () => onUpgrades?.Invoke(), out Text eqT).rectTransform;
            btnTuto = Button(sa, L("TUTORIAL", "TUTORIEL"), new Color(0.2f, 0.24f, 0.3f), new Vector2(0.5f, 0f), new Vector2(-255f, 225f), new Vector2(490f, 130f), 40, () => onTutorial?.Invoke(), out Text tuT).rectTransform;
            ButtonIcon(btnEquip, eqT, GearIcons.Art("icon_equip") ?? GearIcons.Art("plate"));
            ButtonIcon(btnTuto, tuT, GearIcons.Art("icon_tuto"));
            Image dbg = Button(sa, "DEBUG", new Color(0.15f, 0.17f, 0.2f, 0.9f), new Vector2(1f, 0f), new Vector2(-30f, 70f), new Vector2(170f, 64f), 24, () => onDebugOpen?.Invoke(), out Text dt);
            dbg.rectTransform.pivot = new Vector2(1f, 0.5f);
            dt.color = Muted;
            dbgRt = dbg.rectTransform;
            // langue : la langue active en blanc, l'autre grisée ; un tap bascule
            Image lang = Button(sa, Loc.Fr ? "<color=#7F8794>EN</color>  ·  FR" : "EN  ·  <color=#7F8794>FR</color>", new Color(0.15f, 0.17f, 0.2f, 0.9f),
                                new Vector2(0f, 0f), new Vector2(30f, 70f), new Vector2(200f, 64f), 28, () => onLang?.Invoke(), out _);
            lang.rectTransform.pivot = new Vector2(0f, 0.5f);
            langRt = lang.rectTransform;

            // bannière « NEW LEVEL UNLOCKED! »
            unlockBanner = Box(sa, StarGold, new Vector2(0.5f, 0f), new Vector2(0f, 1150f), new Vector2(960f, 330f));
            Image ui = Box(unlockBanner.rectTransform, new Color(0.07f, 0.09f, 0.13f, 0.98f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(930f, 300f));
            unlockTitle = Label(ui.rectTransform, L("NEW LEVEL UNLOCKED!", "NOUVEAU NIVEAU DÉBLOQUÉ !"), 60, StarGold, TextAnchor.MiddleCenter, true);
            Shadowed(unlockTitle);
            Place(unlockTitle.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            unlockSub = Label(ui.rectTransform, "", 42, Color.white, TextAnchor.MiddleCenter, true);
            Place(unlockSub.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            unlockBanner.gameObject.SetActive(false);
            mapRoot.SetActive(false);
        }

        void BuildUpgrades()
        {
            upgRoot = New("upgrades", canvasRt).gameObject;
            RectTransform u = (RectTransform)upgRoot.transform;
            Stretch(u);
            Image fade = Img(u, fadeUp, new Color(0.05f, 0.07f, 0.1f, 0.97f));
            Place(fade.rectTransform, Vector2.zero, new Vector2(1f, 0.55f), Vector2.zero, Vector2.zero);
            RectTransform sa = New("safe", u);
            Stretch(sa);
            Text title = Label(sa, L("EQUIPMENT", "ÉQUIPEMENT"), 56, Color.white, TextAnchor.MiddleLeft, true);
            Shadowed(title);
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(600f, 80f), new Vector2(0f, 1f));
            Image coinBg = Box(sa, new Color(0.06f, 0.08f, 0.11f, 0.85f), new Vector2(1f, 1f), new Vector2(-36f, -45f), new Vector2(300f, 70f));
            coinBg.rectTransform.pivot = new Vector2(1f, 1f);
            upgCoins = Label(coinBg.rectTransform, "", 38, StarGold, TextAnchor.MiddleCenter, true);
            Stretch(upgCoins.rectTransform);
            Image us = DollarIcon(coinBg.rectTransform);
            us.gameObject.SetActive(false);
            Place(us.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(48f, 48f), new Vector2(0f, 0.5f));

            int ng = Tuning.GearCount;
            gName = new Text[ng]; gLevel = new Text[ng]; gNow = new Text[ng]; gNext = new Text[ng]; gCost = new Text[ng];
            gBuy = new Image[ng];
            gPips = new Image[ng, 3];
            gTiles = new Image[ng, 3]; gTileIcon = new Image[ng, 3]; gTileCheck = new Image[ng, 3]; gTileRing = new Image[ng, 3];
            for (int i = 0; i < ng; i++)
            {
                int id = i;
                float y = 1000f - i * 215f;
                Image card = Box(sa, new Color(0.11f, 0.14f, 0.19f, 0.97f), new Vector2(0.5f, 0f), new Vector2(0f, y), new Vector2(1000f, 200f));
                RectTransform cr = card.rectTransform;
                // ligne d'équipement : son nom, puis ses 3 objets (gagnés / prochain / à venir), le nom de l'objet et le bouton d'achat
                gName[i] = Label(cr, Tuning.GearNames[i], 24, Muted, TextAnchor.MiddleLeft, true);
                Place(gName[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -10f), new Vector2(300f, 34f), new Vector2(0f, 1f));
                for (int k = 0; k < 3; k++)
                {
                    Vector2 tp = new Vector2(24f + k * 146f, -48f);
                    gTileRing[i, k] = Img(cr, round, Yellow);
                    Place(gTileRing[i, k].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), tp + new Vector2(-6f, 6f), new Vector2(144f, 144f), new Vector2(0f, 1f));
                    gTiles[i, k] = Img(cr, round, GuardBlue);
                    Place(gTiles[i, k].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), tp, new Vector2(132f, 132f), new Vector2(0f, 1f));
                    gTileIcon[i, k] = Img(gTiles[i, k].rectTransform, GearIcons.Upgrade(i, false, k + 1), Color.white);
                    Place(gTileIcon[i, k].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 110f));
                    // coche verte : objet gagné
                    gTileCheck[i, k] = Img(gTiles[i, k].rectTransform, dot, new Color(0.25f, 0.82f, 0.48f));
                    Place(gTileCheck[i, k].rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(40f, 40f));
                    Image c1 = Img(gTileCheck[i, k].rectTransform, white, new Color(0.03f, 0.14f, 0.06f));
                    Place(c1.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-5f, -2f), new Vector2(6f, 14f));
                    c1.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    Image c2 = Img(gTileCheck[i, k].rectTransform, white, new Color(0.03f, 0.14f, 0.06f));
                    Place(c2.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(4f, 2f), new Vector2(6f, 22f));
                    c2.transform.localRotation = Quaternion.Euler(0f, 0f, -40f);
                }
                gNow[i] = Label(cr, "", 32, Color.white, TextAnchor.MiddleLeft, true);
                Place(gNow[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(466f, -46f), new Vector2(262f, 46f), new Vector2(0f, 1f));
                Fit(gNow[i], 18);
                gNext[i] = Label(cr, "", 24, Yellow, TextAnchor.MiddleLeft, true);
                Place(gNext[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(466f, -94f), new Vector2(262f, 36f), new Vector2(0f, 1f));
                Fit(gNext[i], 15);
                gLevel[i] = Label(cr, "", 22, new Color(0.8f, 0.83f, 0.88f), TextAnchor.UpperLeft, false);
                Place(gLevel[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(466f, -134f), new Vector2(262f, 52f), new Vector2(0f, 1f));
                Fit(gLevel[i], 14);
                gBuy[i] = Button(cr, "", Yellow, new Vector2(1f, 0.5f), new Vector2(-116f, 0f), new Vector2(196f, 124f), 46, () => onBuyGear?.Invoke(id), out gCost[i]);
                gCost[i].color = new Color(0.12f, 0.1f, 0.04f);
            }
            Button(sa, L("BACK TO MAP", "RETOUR À LA CARTE"), new Color(0.2f, 0.24f, 0.3f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(1000f, 140f), 44, () => onBackToMap?.Invoke(), out _);
            upgRoot.SetActive(false);
        }

        /// <summary>
        /// Écran de fin de course = centre de progression : gain animé (« +N STREET CRED! »), solde, deux améliorations
        /// en un tap (POWER = arme, CREW = nombre d'alliés) avec jauge vers le prochain achat, et RETRY tout de suite.
        /// </summary>
        void BuildResult()
        {
            resRoot = New("result", canvasRt).gameObject;
            RectTransform r = (RectTransform)resRoot.transform;
            Stretch(r);
            Image fade = Img(r, fadeUp, new Color(0.05f, 0.07f, 0.1f, 0.96f));
            Place(fade.rectTransform, Vector2.zero, new Vector2(1f, 0.9f), Vector2.zero, Vector2.zero);
            Image card = Box(r, new Color(0.11f, 0.14f, 0.19f, 0.98f), new Vector2(0.5f, 0f), new Vector2(0f, 1125f), new Vector2(1000f, 1085f));
            RectTransform cr = card.rectTransform;
            resTagBg = Img(cr, round, Good);
            Place(resTagBg.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -28f), new Vector2(-80f, 88f), new Vector2(0.5f, 1f));
            resTag = Label(resTagBg.rectTransform, "", 50, Color.white, TextAnchor.MiddleCenter, true);
            Shadowed(resTag);
            Stretch(resTag.rectTransform);
            resTitle = Label(cr, "", 34, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleCenter, true);
            Place(resTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -124f), new Vector2(-80f, 46f), new Vector2(0.5f, 1f));

            // gain : rayons, insigne, « +N » animé et le nom de la monnaie
            resRays = Img(cr, MakeRays(), new Color(1f, 0.85f, 0.3f, 0.35f));
            Place(resRays.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-190f, -250f), new Vector2(280f, 280f));
            resStar = DollarIcon(cr);
            Place(resStar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-190f, -250f), new Vector2(140f, 140f));
            resEarned = Label(cr, "", 104, StarGold, TextAnchor.MiddleLeft, true);
            Shadowed(resEarned);
            Place(resEarned.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-105f, -228f), new Vector2(560f, 120f), new Vector2(0f, 0.5f));
            resCurName = Label(cr, Tuning.Currency + L("!", " !"), 36, StarGold, TextAnchor.MiddleLeft, true);
            Place(resCurName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-100f, -306f), new Vector2(560f, 46f), new Vector2(0f, 0.5f));
            resTotal = Label(cr, "", 36, Color.white, TextAnchor.MiddleCenter, true);
            Place(resTotal.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -385f), new Vector2(-80f, 48f), new Vector2(0.5f, 1f));
            resBody = Label(cr, "", 27, new Color(0.85f, 0.88f, 0.92f), TextAnchor.UpperCenter, false);
            resBody.lineSpacing = 1.1f;
            Place(resBody.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -425f), new Vector2(-80f, 80f), new Vector2(0.5f, 1f));
            resTip = Label(cr, "", 27, Yellow, TextAnchor.MiddleCenter, true);
            Place(resTip.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -490f), new Vector2(-80f, 40f), new Vector2(0.5f, 1f));

            // les deux axes de progression, achetables en un tap
            resTracks = new TrackUI[2];
            for (int i = 0; i < 2; i++)
            {
                int k = i;
                var t = new TrackUI();
                t.bg = Img(cr, round, new Color(0.16f, 0.19f, 0.25f, 1f));
                Place(t.bg.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -545f - i * 240f), new Vector2(-70f, 222f), new Vector2(0.5f, 1f));
                RectTransform tr = t.bg.rectTransform;
                t.icon = Img(tr, GearIcons.Upgrade(2, false, 1), Color.white);
                Place(t.icon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -4f), new Vector2(78f, 78f), new Vector2(0f, 1f));
                t.name = Label(tr, "", 46, Color.white, TextAnchor.MiddleLeft, true);
                Shadowed(t.name);
                Place(t.name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(104f, -14f), new Vector2(420f, 58f), new Vector2(0f, 1f));
                t.level = Label(tr, "", 32, Yellow, TextAnchor.MiddleRight, true);
                Place(t.level.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-26f, -14f), new Vector2(380f, 58f), new Vector2(1f, 1f));
                t.effect = Label(tr, "", 28, new Color(0.88f, 0.9f, 0.93f), TextAnchor.MiddleLeft, false);
                Place(t.effect.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(26f, -74f), new Vector2(-52f, 44f), new Vector2(0f, 1f));
                // jauge : solde / prix
                Image barBg = Img(tr, round, new Color(0.06f, 0.07f, 0.1f, 1f));
                // la jauge s'arrête avant le bouton (marge pour son animation de zoom)
                Place(barBg.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(26f, 24f), new Vector2(-26f - 40f - 220f - 50f, 60f), Vector2.zero);
                t.fill = Img(barBg.rectTransform, round, StarGold);
                Place(t.fill.rectTransform, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f));
                t.bar = Label(barBg.rectTransform, "", 30, Color.white, TextAnchor.MiddleCenter, true);
                Shadowed(t.bar);
                Stretch(t.bar.rectTransform);
                t.pill = Img(tr, round, new Color(0.3f, 0.32f, 0.36f));
                // centré avec de la marge : même au plus fort du zoom (×1,18) il reste à l'intérieur de la carte
                Place(t.pill.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f - 110f, 22f + 32f), new Vector2(220f, 64f), new Vector2(0.5f, 0.5f));
                t.pillText = Label(t.pill.rectTransform, "", 32, Color.white, TextAnchor.MiddleCenter, true);
                Fit(t.pillText, 18);
                Stretch(t.pillText.rectTransform);
                AddButton(tr, () => TapTrack(k));
                resTracks[i] = t;
            }
            resMore = Label(cr, L("Head & body: EQUIPMENT", "Tête et corps : ÉQUIPEMENT"), 26, Muted, TextAnchor.MiddleCenter, false);
            Place(resMore.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 18f), new Vector2(-80f, 40f), new Vector2(0.5f, 0f));

            resCont = Button(r, L("CONTINUE", "CONTINUER"), Yellow, new Vector2(0.5f, 0f), new Vector2(0f, 400f), new Vector2(1000f, 170f), 54, () => onContinue?.Invoke(), out Text ct).gameObject;
            ct.color = new Color(0.12f, 0.1f, 0.04f);
            resContLabel = ct;
            resRetry = Button(r, L("RETRY", "RÉESSAYER"), Yellow, new Vector2(0.5f, 0f), new Vector2(0f, 400f), new Vector2(1000f, 170f), 64, () => onRetry?.Invoke(), out Text rt).gameObject;
            rt.color = new Color(0.12f, 0.1f, 0.04f);
            resUpg = Button(r, L("EQUIPMENT", "ÉQUIPEMENT"), Blue, new Vector2(0.5f, 0f), new Vector2(-255f, 228f), new Vector2(490f, 120f), 38, () => onUpgrades?.Invoke(), out resUpgLabel).gameObject;
            resMap = Button(r, L("MAP", "CARTE"), new Color(0.2f, 0.24f, 0.3f), new Vector2(0.5f, 0f), new Vector2(255f, 228f), new Vector2(490f, 120f), 38, () => onBackToMap?.Invoke(), out _).gameObject;
            resRoot.SetActive(false);
        }

        Image resStar, resRays;

        /// <summary>Rayons de lumière (derrière l'insigne du bilan).</summary>
        static Sprite MakeRays()
        {
            const int n = 128;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Atan2(v, u);
                float ray = Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 6f)), 6f);
                float fall = Mathf.Clamp01(1f - r) * Mathf.Clamp01(r * 3f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(ray * fall * 1.4f) * 255f));
            }
            t.SetPixels32(px);
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }
        Text resEarned, resTotal, resCurName, resMore, resUpgLabel;
        float resCountT = -1f, resLastShown;
        int resEarnedTarget, resBankTarget, resBankBefore;
        bool resVictory;

        /// <summary>Un axe de progression proposé en fin de course (POWER, CREW).</summary>
        public struct Track { public int gear; public string name, level, effect; public int cost; }

        class TrackUI
        {
            public Image bg, fill, pill, icon;
            public Text name, level, effect, bar, pillText;
            public Track data;
            public bool on, afford;
            public float shake;
        }
        TrackUI[] resTracks;

        /// <summary>
        /// Fin de course : gain animé, solde, POWER et CREW avec jauge vers le prochain achat, RETRY (ou suite en cas de victoire).
        /// tracks null : pas d'améliorations (tutoriel), le texte prend la place.
        /// </summary>
        public void ShowResult(bool victory, string tag, string title, string body, int earned, int bank, Track[] tracks, string tip, string contLabel = null)
        {
            if (resContLabel != null) resContLabel.text = contLabel ?? L("CONTINUE", "CONTINUER");
            HideGameScreens();
            HideMeta();
            resRoot.SetActive(true);
            LockClicks(0.5f);
            resVictory = victory;
            resTag.text = tag;
            resTagBg.color = victory ? new Color(0.25f, 0.65f, 0.3f) : new Color(0.75f, 0.2f, 0.17f);
            resTitle.text = title;
            resBody.text = body;
            Place(resBody.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -425f), new Vector2(-80f, tracks == null ? 520f : 60f), new Vector2(0.5f, 1f));
            resTip.text = tip;
            if (tracks == null)
            {
                // pas de jauges (fin du tutoriel) : le conseil passe sous le texte, sur plusieurs lignes si besoin
                Canvas.ForceUpdateCanvases();
                float bodyH = Mathf.Max(40f, resBody.preferredHeight);
                Place(resTip.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -425f - bodyH - 30f), new Vector2(-80f, 110f), new Vector2(0.5f, 1f));
                resTip.alignment = TextAnchor.UpperCenter;
            }
            else
            {
                Place(resTip.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -490f), new Vector2(-80f, 40f), new Vector2(0.5f, 1f));
                resTip.alignment = TextAnchor.MiddleCenter;
            }
            resCurName.text = Tuning.Currency + L("!", " !");
            resEarnedTarget = earned;
            resBankTarget = bank;
            resBankBefore = bank - earned;
            resCountT = 0f;
            resLastShown = 0f;
            resEarned.text = "+0";
            SetTracks(tracks, resBankBefore);
            resMore.gameObject.SetActive(tracks != null);
            resCont.SetActive(victory);
            resRetry.SetActive(!victory);
            // ligne secondaire : REPLAY + MAP après une victoire, ALL UPGRADES + MAP après un échec
            resUpg.SetActive(tracks != null);
            resUpgLabel.text = victory ? L("REPLAY", "REJOUER") : L("EQUIPMENT", "ÉQUIPEMENT");
            resMap.SetActive(tracks != null);
            resRoot.transform.localScale = Vector3.one * 0.94f;
        }

        /// <summary>Après un achat : nouveau solde et jauges (sans rejouer le compteur).</summary>
        public void UpdateResultBank(int bank, Track[] tracks)
        {
            resBankTarget = bank;
            resBankBefore = bank - resEarnedTarget;
            resCountT = -1f;
            resEarned.text = "+" + resEarnedTarget;
            SetTracks(tracks, bank);
            resStar.transform.localScale = Vector3.one * 1.3f;
        }

        void SetTracks(Track[] tracks, int bank)
        {
            for (int i = 0; i < resTracks.Length; i++)
            {
                TrackUI t = resTracks[i];
                t.on = tracks != null && i < tracks.Length;
                t.bg.gameObject.SetActive(t.on);
                if (!t.on) continue;
                t.data = tracks[i];
                t.name.text = t.data.name;
                int glv = Progress.GearLevel(thiefMode, t.data.gear);
                t.icon.sprite = GearIcons.Upgrade(t.data.gear, thiefMode, t.data.cost < 0 ? glv : glv + 1); // le niveau à gagner : ça donne envie
                t.level.text = t.data.level;
                t.effect.text = t.data.effect;
                RefreshTrack(t, bank, false);
            }
            resTotal.text = L("BALANCE:  ", "SOLDE :  ") + bank + " " + Tuning.Cur;
        }

        /// <summary>Jauge, prix et état d'un axe pour un solde donné (animé pendant le compteur de gain).</summary>
        void RefreshTrack(TrackUI t, int bank, bool announce)
        {
            int cost = t.data.cost;
            bool maxed = cost < 0;
            bool afford = !maxed && bank >= cost;
            if (afford && !t.afford && announce)
            {
                // le solde vient de dépasser le prix : l'achat s'allume
                t.pill.transform.localScale = Vector3.one * 1.15f;
                Snd.Play(Snd.I != null ? Snd.I.perfect : null, 0.5f, 0f);
                Haptics.Pulse(40, true);
            }
            t.afford = afford;
            float frac = maxed ? 1f : Mathf.Clamp01(bank / (float)Mathf.Max(1, cost));
            RectTransform fr = t.fill.rectTransform;
            fr.anchorMax = new Vector2(Mathf.Max(0.04f, frac), 1f);
            fr.sizeDelta = Vector2.zero;
            t.fill.color = maxed ? new Color(0.35f, 0.4f, 0.5f) : afford ? HealGreen : StarGold;
            t.bar.text = maxed ? L("MAXED OUT", "AU MAXIMUM") : Mathf.Min(bank, cost) + " / " + cost + " " + Tuning.Cur;
            t.bg.color = afford ? new Color(0.17f, 0.3f, 0.19f, 1f) : new Color(0.16f, 0.19f, 0.25f, 1f);
            t.pill.color = maxed ? new Color(0.2f, 0.35f, 0.25f) : afford ? HealGreen : new Color(0.3f, 0.32f, 0.36f);
            if (maxed) t.pillText.text = "MAX";
            else if (afford) t.pillText.text = L("BUY", "ACHETER");
            else
            {
                int missing = cost - bank;
                int runs = resEarnedTarget > 0 ? Mathf.CeilToInt(missing / (float)resEarnedTarget) : -1;
                t.pillText.text = runs > 0 && runs <= 9 ? "≈" + runs + (runs > 1 ? L(" RUNS", " PARTIES") : L(" RUN", " PARTIE")) : L(missing + " MORE", "ENCORE " + missing);
            }
            t.pillText.color = afford ? new Color(0.06f, 0.2f, 0.08f) : Color.white;
        }

        /// <summary>Tap sur un axe : achat s'il est payable, sinon petite secousse et ce qui manque.</summary>
        void TapTrack(int i)
        {
            TrackUI t = resTracks[i];
            if (!t.on) return;
            if (t.afford) { onBuyGear?.Invoke(t.data.gear); return; }
            t.shake = 0.35f;
            Snd.Play(Snd.I != null ? Snd.I.tick : null, 0.4f);
            if (t.data.cost > 0) InputDebug((t.data.cost - resBankTarget) + " " + Tuning.Cur + " MORE: keep playing!");
        }

        void TickResultCount(float udt)
        {
            if (resRoot == null || !resRoot.activeSelf) return;
            resRoot.transform.localScale = Vector3.MoveTowards(resRoot.transform.localScale, Vector3.one, udt * 0.8f);
            resStar.transform.localScale = Vector3.MoveTowards(resStar.transform.localScale, Vector3.one, udt * 1.5f);
            resRays.transform.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * 25f);
            resRays.transform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(Time.unscaledTime * 3f));
            foreach (TrackUI t in resTracks)
            {
                if (!t.on) continue;
                float pulse = 1f; // la carte reste fixe : seul le bouton BUY bouge
                t.bg.transform.localScale = Vector3.MoveTowards(t.bg.transform.localScale, Vector3.one * pulse, udt * 0.8f);
                t.pill.transform.localScale = Vector3.one * (t.afford ? BuyPulse(Time.unscaledTime, 0f) : 1f);
                if (t.shake > 0f)
                {
                    // pas encore payable : petite secousse
                    t.shake -= udt;
                    t.bg.transform.localRotation = Quaternion.Euler(0f, 0f, t.shake > 0f ? Mathf.Sin(Time.unscaledTime * 60f) * 2.5f : 0f);
                }
            }
            if (resCountT < 0f) return;
            // compteur rapide (≈ 0,7 s) : les jauges se remplissent en même temps que le gain s'affiche
            resCountT += udt;
            float u = Mathf.Clamp01((resCountT - 0.12f) / 0.6f);
            float shown = Mathf.Round(resEarnedTarget * (1f - (1f - u) * (1f - u)));
            if (shown > resLastShown)
            {
                if (Mathf.FloorToInt(shown / 3f) != Mathf.FloorToInt(resLastShown / 3f)) Snd.PlayPitch(Snd.I != null ? Snd.I.coin : null, 0.25f, 1f + u * 0.6f);
                resStar.transform.localScale = Vector3.one * 1.12f;
                resLastShown = shown;
                int bankNow = resBankBefore + (int)shown;
                resTotal.text = L("BALANCE:  ", "SOLDE :  ") + bankNow + " " + Tuning.Cur;
                foreach (TrackUI t in resTracks) if (t.on) RefreshTrack(t, bankNow, true);
            }
            resEarned.text = "+" + shown;
            if (u >= 1f)
            {
                resCountT = -1f;
                resTotal.text = L("BALANCE:  ", "SOLDE :  ") + resBankTarget + " " + Tuning.Cur;
                foreach (TrackUI t in resTracks) if (t.on) RefreshTrack(t, resBankTarget, true);
                resStar.transform.localScale = Vector3.one * 1.35f;
                Snd.Play(Snd.I != null ? Snd.I.pickup : null, 0.6f, 0f);
                Haptics.Pulse(30);
            }
        }

        void BuildDebug()
        {
            dbgRoot = New("debug", canvasRt).gameObject;
            RectTransform d = (RectTransform)dbgRoot.transform;
            Stretch(d);
            Image dim = Img(d, white, new Color(0f, 0f, 0f, 0.7f));
            Stretch(dim.rectTransform);
            AddButton(dim.rectTransform, null); // bloque les taps vers la carte en dessous
            Image card = Box(d, new Color(0.1f, 0.12f, 0.15f, 0.98f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 1500f));
            Text t = Label(card.rectTransform, "DEBUG", 50, Yellow, TextAnchor.MiddleCenter, true);
            Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -60f), new Vector2(0f, 80f), new Vector2(0.5f, 0.5f));
            const int n = 20;
            dbgLabels = new Text[n];
            for (int i = 0; i < n; i++)
            {
                int id = i;
                float x = i % 2 == 0 ? -240f : 240f;
                float y = 590f - (i / 2) * 118f;
                Button(card.rectTransform, "", new Color(0.22f, 0.26f, 0.33f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(460f, 104f), 30, () => onDebug?.Invoke(id), out dbgLabels[i]);
            }
            // prévision d'équilibrage de la rue sélectionnée
            dbgInfo = Label(card.rectTransform, "", 26, Muted, TextAnchor.MiddleCenter, false);
            Place(dbgInfo.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 75f), new Vector2(-60f, 110f), new Vector2(0.5f, 0.5f));
            dbgRoot.SetActive(false);
        }

        // ------------------------------------------------------------------ API

        public void HideMeta()
        {
            if (mapRoot == null) return;
            mapRoot.SetActive(false);
            upgRoot.SetActive(false);
            resRoot.SetActive(false);
            dbgRoot.SetActive(false);
        }

        void HideGameScreens()
        {
            menu.SetActive(false);
            play.SetActive(false);
            pause.SetActive(false);
            end.SetActive(false);
            demoRoot.SetActive(false);
            HideTip();
        }

        /// <summary>Carte du quartier. animate : rue fraîchement conquise (remplissage animé), -1 sinon.</summary>
        /// <summary>
        /// Carte d'une map (quartier) : ses rues, onglets des maps, rue sélectionnée.
        /// conquered et bossNames couvrent toutes les rues ; animate : rue fraîchement conquise (remplissage animé), -1 sinon.
        /// </summary>
        public void ShowMap(bool thief, int map, bool[] mapOpen, bool[] conquered, int next, int selected, int coins, string[] bossNames, int animate, int unlockMap = -1)
        {
            if (unlockMap >= 0) { pendingUnlock = unlockMap; unlockT = -1f; }
            mapIdxShown = map;
            HideGameScreens();
            HideMeta();
            mapRoot.SetActive(true);
            LockClicks(0.45f);
            mapThief = thief;
            mapIdx = map;
            for (int i = 0; i < Tuning.Streets; i++) mapConq[i] = conquered[i];
            mapNextIdx = next;
            selStreet = selected;
            conquerStreet = animate;
            conquerAnimT = animate >= 0 ? 0f : -1f;
            mapCoins.text = "$ " + coins;
            UpdateProfile(thief);

            // onglet du niveau où se trouve la prochaine rue : « NEW! » clignotant
            mapNewTab = next >= 0 && Tuning.MapOf(next) != map && mapOpen[Tuning.MapOf(next)] ? Tuning.MapOf(next) : -1;
            for (int m = 0; m < tabBg.Length; m++)
            {
                bool sel = m == map;
                tabLabel[m].text = L("LEVEL ", "NIV. ") + (m + 1) + "\n<size=21>" + (m == mapNewTab ? L("NEW!", "NOUVEAU !") : mapOpen[m] ? Tuning.MapShort[m] : L("LOCKED", "FERMÉ")) + "</size>";
                tabBg[m].color = sel ? new Color(0.98f, 0.84f, 0.12f) : mapOpen[m] ? new Color(0.2f, 0.24f, 0.31f) : new Color(0.12f, 0.13f, 0.16f);
                tabLabel[m].color = sel ? new Color(0.12f, 0.1f, 0.04f) : mapOpen[m] ? Color.white : Muted;
            }

            int first = Tuning.MapFirst[map], count = Tuning.MapCount[map];
            int n = 0;
            for (int i = 0; i < count; i++) if (conquered[first + i]) n++;
            mapCount.text = n + "/" + count + L(" STREETS", " RUES");
            bool inMap = next >= first && next < first + count;
            mapNext.text = !mapOpen[map] ? L("Conquer " + Tuning.MapNames[map - 1] + " first", "Conquiers d'abord " + Tuning.MapNames[map - 1]) : inMap ? L("BOSS: ", "BOSS : ") + bossNames[next].ToUpperInvariant() : n >= count ? Tuning.MapNames[map] + L(" CONQUERED!", " CONQUIS !") : "";
            facGuard.color = thief ? new Color(GuardBlue.r, GuardBlue.g, GuardBlue.b, 0.3f) : GuardBlue;
            facThief.color = thief ? ThiefRed : new Color(ThiefRed.r, ThiefRed.g, ThiefRed.b, 0.3f);
            Color mine = thief ? ThiefRed : GuardBlue, theirs = thief ? GuardBlue : ThiefRed;
            LayoutCityMap(count, Tuning.MapTheme[map], map);
            streetMine = mine; streetTheirs = theirs;
            for (int k = 0; k < SlotCount; k++)
            {
                bool on = k < count;
                slotStreet[k] = on ? first + k : -1;
                if (!on) continue;
                int i = first + k;
                bool locked = !conquered[i] && i != next;
                bool done = conquered[i] && i != animate;
                // rue : barrière (verrouillée), rubalise + voiture de police (à prendre), liseré du camp (prise)
                SetStreetState(k, locked ? 0 : done ? 2 : 1);
                stNum[k].text = "";
                string nm = Tuning.StreetNames[i].ToUpperInvariant();
                stName[k].text = locked ? (k + 1) + L(" · ROAD CLOSED", " · ROUTE BARRÉE") : done ? (k + 1) + " · " + nm + L(" · OURS", " · À NOUS") : (k + 1) + " · " + nm;
                stName[k].fontSize = locked || done ? 24 : 30;
                stName[k].color = locked ? new Color(0.81f, 0.83f, 0.85f) : Color.white;
                // le fond est porté par stFill (au-dessus du liseré blanc), stBg reste transparent
                stBg[k].color = Color.clear;
                stFill[k].color = locked ? new Color(0.17f, 0.18f, 0.22f) : done ? mine : theirs;
                stBorder[k].color = locked ? new Color(0.61f, 0.63f, 0.67f) : Color.white;
                float w = 44f + stName[k].text.Length * (locked || done ? 14.5f : 18f);
                stBg[k].rectTransform.sizeDelta = new Vector2(w, locked || done ? 48f : 62f);
                stGlow[k].rectTransform.sizeDelta = new Vector2(w + 22f, locked || done ? 64f : 80f);
                stSub[k].text = "";
                // l'étiquette reste dans le cadre de la carte
                Vector2 lp = stBox[k].anchoredPosition;
                float hw = w * 0.5f + 14f;
                lp.x = Mathf.Clamp(lp.x, -480f + hw, 480f - hw);
                stBox[k].anchoredPosition = lp;
                stSubBg[k].gameObject.SetActive(stSub[k].text.Length > 0);
                stSubBg[k].rectTransform.sizeDelta = new Vector2(40f + stSub[k].text.Length * 13.5f, 36f);
                stTag[k].text = "";
                stTag[k].color = Yellow;
            }
            UpdatePlayLabel();
        }

        int SlotOf(int street)
        {
            for (int k = 0; k < SlotCount; k++) if (slotStreet[k] == street) return k;
            return -1;
        }

        public void SelectStreet(int i)
        {
            selStreet = i;
            UpdatePlayLabel();
        }

        void UpdatePlayLabel()
        {
            if (selStreet < 0 || SlotOf(selStreet) < 0) { playLabel.text = L("CHOOSE A STREET", "CHOISIS UNE RUE"); playBg.color = new Color(0.3f, 0.32f, 0.36f); return; }
            bool replay = mapConq[selStreet];
            // bouton principal : toujours la suite de la progression, en jaune ; rejouer une rue = bouton en retrait
            playBg.color = replay ? new Color(0.3f, 0.34f, 0.42f) : Yellow;
            playLabel.color = replay ? Color.white : new Color(0.12f, 0.1f, 0.04f);
            playLabel.text = replay ? L("REPLAY ", "REJOUER ") + Tuning.StreetNames[selStreet]
                                    : L("CONTINUE - LEVEL ", "CONTINUER - NIVEAU ") + (Tuning.MapOf(selStreet) + 1) + "\n<size=32>" + Tuning.StreetNames[selStreet] + "</size>";
        }


        /// <summary>Bannière de déverrouillage : apparaît après l'animation de conquête, puis bascule sur la nouvelle map.</summary>
        void TickUnlock(float udt)
        {
            if (pendingUnlock >= 0 && unlockT < 0f && conquerAnimT < 0f)
            {
                unlockT = 0f;
                unlockSub.text = L("LEVEL ", "NIVEAU ") + (pendingUnlock + 1) + " · " + Tuning.MapNames[pendingUnlock];
                if (pendingUnlock >= 1 && Tuning.MapWeaponTier[pendingUnlock] >= 1 && Tuning.MapWeaponTier[pendingUnlock - 1] == 0)
                    unlockSub.text += "\n<size=34>" + (thiefMode ? L("+ IRON BAR", "+ BARRE DE FER") : L("+ REINFORCED BATON", "+ MATRAQUE RENFORCÉE")) + "</size>";
                unlockBanner.gameObject.SetActive(true);
                Flash(0.5f);
                Snd.Play(Snd.I != null ? Snd.I.perfect : null, 0.8f, 0f);
                Haptics.Pulse(80, true);
                LockClicks(2.4f);
            }
            if (unlockT < 0f) return;
            unlockT += udt;
            float pop = unlockT < 0.35f ? Mathf.SmoothStep(0.3f, 1.12f, unlockT / 0.35f) : 1f + 0.12f * Mathf.Max(0f, 1f - (unlockT - 0.35f) * 4f);
            unlockBanner.transform.localScale = Vector3.one * pop;
            unlockBanner.color = Color.Lerp(StarGold, Color.white, 0.5f + 0.5f * Mathf.Sin(unlockT * 12f));
            if (pendingUnlock >= 0 && unlockT > 2f)
            {
                int m = pendingUnlock;
                pendingUnlock = -1;
                onMapTab?.Invoke(m);
            }
            if (unlockT > 3.2f)
            {
                unlockT = -1f;
                unlockBanner.gameObject.SetActive(false);
            }
        }

        void TickMap(float udt)
        {
            if (mapRoot == null || !mapRoot.activeSelf) return;
            mapT += udt;
            for (int k = 0; k < SlotCount; k++)
            {
                int i = slotStreet[k];
                if (i < 0) continue;
                stGlow[k].gameObject.SetActive(i == selStreet || i == mapNextIdx);
                float a = i == selStreet ? 0.9f : 0.35f + 0.3f * Mathf.Sin(mapT * 5f);
                stGlow[k].color = new Color(i == selStreet ? 1f : Yellow.r, i == selStreet ? 1f : Yellow.g, i == selStreet ? 1f : Yellow.b, a);
                stBg[k].transform.localScale = Vector3.one * (i == mapNextIdx ? 1f + 0.08f * Mathf.Sin(mapT * 5f) : 1f);
            }
            playBg.transform.localScale = Vector3.one * (1f + 0.025f * Mathf.Sin(mapT * 4f));
            if (mapNewTab >= 0 && mapNewTab < tabBg.Length)
            {
                float p = 0.5f + 0.5f * Mathf.Sin(mapT * 6f);
                tabBg[mapNewTab].color = Color.Lerp(new Color(0.2f, 0.24f, 0.31f), StarGold, p);
                tabBg[mapNewTab].transform.localScale = Vector3.one * (1f + 0.04f * p);
            }
            TickUnlock(udt);
            TickStreets(udt);

            // conquête : la rue se remplit de la couleur du camp, puis tampon « OURS »
            int cs = conquerStreet >= 0 ? SlotOf(conquerStreet) : -1;
            if (conquerAnimT >= 0f && cs >= 0)
            {
                conquerAnimT += udt;
                float f = Mathf.Clamp01((conquerAnimT - 0.3f) / 1.1f);
                SetFill(cs, f * f * (3f - 2f * f));
                float pop = conquerAnimT > 1.4f ? Mathf.Max(0f, 1f - (conquerAnimT - 1.4f) * 3f) : 0f;
                stBg[cs].transform.localScale = Vector3.one * (1f + 0.15f * pop);
                if (conquerAnimT > 1.4f && stTag[cs].text.Length == 0)
                {
                    stTag[cs].text = L("CONQUERED!", "CONQUISE !");
                    Flash(0.35f);
                    Snd.Play(Snd.I != null ? Snd.I.pickup : null, 0.9f, 0f);
                    Haptics.Pulse(60, true);
                }
                if (conquerAnimT > 3f) { stTag[cs].text = L("OURS", "À NOUS"); conquerAnimT = -1f; }
            }
        }

        public void ShowUpgrades(int coins, int[] levels, int[] costs)
        {
            HideGameScreens();
            HideMeta();
            upgRoot.SetActive(true);
            LockClicks(0.35f);
            upgCoins.text = "$ " + coins;
            for (int i = 0; i < levels.Length && i < gName.Length; i++)
            {
                int lv = levels[i];
                bool maxed = costs[i] < 0;
                bool afford = !maxed && coins >= costs[i];
                Color side = mapThief ? ThiefRed : GuardBlue;
                for (int k = 0; k < 3; k++)
                {
                    bool owned = k < lv, nextOne = k == lv;
                    gTiles[i, k].color = owned ? (maxed ? new Color(1f, 0.8f, 0.25f) : new Color(side.r * 0.8f, side.g * 0.8f, side.b * 0.8f, 1f))
                                       : nextOne ? new Color(0.1f, 0.13f, 0.18f) : new Color(0.07f, 0.08f, 0.11f);
                    gTileIcon[i, k].sprite = GearIcons.Upgrade(i, mapThief, k + 1);
                    gTileIcon[i, k].color = owned ? Color.white : nextOne ? new Color(1f, 1f, 1f, afford ? 1f : 0.5f) : new Color(0.3f, 0.32f, 0.38f, 0.35f);
                    gTileCheck[i, k].gameObject.SetActive(owned);
                    gTileRing[i, k].gameObject.SetActive(nextOne && afford);
                }
                gName[i].text = Tuning.GearNames[i];
                gNow[i].text = lv > 0 ? Tuning.ItemName(i, mapThief, lv) : L("Nothing yet", "Rien pour l'instant");
                gNow[i].color = lv > 0 ? Color.white : Muted;
                gNext[i].text = maxed ? L("COMPLETE!", "COMPLET !") : L("Next: ", "Prochain : ") + Tuning.ItemName(i, mapThief, lv + 1);
                gLevel[i].text = Tuning.GearEffect[i, maxed ? 3 : lv + 1];
                gCost[i].text = maxed ? "MAX" : costs[i] + " " + Tuning.Cur;
                gAfford[i] = afford;
                gBuy[i].color = maxed ? new Color(0.2f, 0.35f, 0.25f) : afford ? Yellow : new Color(0.3f, 0.32f, 0.36f);
                gCost[i].color = afford ? new Color(0.12f, 0.1f, 0.04f) : new Color(1f, 1f, 1f, 0.7f);
            }
        }

        /// <summary>Petit rebond sur la carte achetée.</summary>
        public void PopGear(int i) { if (gBuy != null) gBuy[i].transform.localScale = Vector3.one * 1.15f; }

        /// <summary>Écran de fin de course (ou de tutoriel). victory : bouton CONTINUE ; sinon RETRY + UPGRADES + MAP.</summary>
        public void ShowDebug(bool on) { dbgRoot.SetActive(on); }

        Text dbgInfo;
        public void SetDebugInfo(string s) { if (dbgInfo != null) dbgInfo.text = s; }

        public void SetDebugLabels(string[] labels)
        {
            for (int i = 0; i < dbgLabels.Length; i++) dbgLabels[i].text = i < labels.Length ? labels[i] : "";
        }

        /// <summary>Affiche le geste reconnu (mode debug des commandes).</summary>
        public void InputDebug(string s)
        {
            inputDbg.text = s;
            inputDbgT = 0.7f;
        }

        /// <summary>
        /// Bouton BUY payable : « zoom, dézoom, zoom, dézoom » puis une courte pause, en boucle (cycle de 1,3 s).
        /// </summary>
        public static float BuyPulse(float t, float phase)
        {
            float c = Mathf.Repeat(t + phase, 1.3f);
            if (c > 0.8f) return 1f;
            return 1f + 0.18f * Mathf.Abs(Mathf.Sin(c / 0.8f * Mathf.PI * 2f));
        }

        readonly bool[] gAfford = new bool[8];

        void TickMeta(float udt)
        {
            TickMap(udt);
            TickTitle(udt);
            if (upgRoot != null && upgRoot.activeSelf && gBuy != null)
                for (int i = 0; i < gBuy.Length; i++)
                    if (gAfford[i]) gBuy[i].transform.localScale = Vector3.one * BuyPulse(Time.unscaledTime, i * 0.15f);
                    else gBuy[i].transform.localScale = Vector3.MoveTowards(gBuy[i].transform.localScale, Vector3.one, udt * 2f);
            TickHealth(udt);
            TickResultCount(udt);
            if (inputDbgT > 0f)
            {
                inputDbgT -= udt;
                inputDbg.color = new Color(Yellow.r, Yellow.g, Yellow.b, Mathf.Clamp01(inputDbgT / 0.3f));
                if (inputDbgT <= 0f) inputDbg.text = "";
            }
            if (resRoot != null && resRoot.activeSelf)
                resRoot.transform.localScale = Vector3.MoveTowards(resRoot.transform.localScale, Vector3.one, udt * 1.5f);
            if (gBuy != null)
                foreach (Image b in gBuy) b.transform.localScale = Vector3.MoveTowards(b.transform.localScale, Vector3.one, udt * 1.2f);
        }
    }
}
