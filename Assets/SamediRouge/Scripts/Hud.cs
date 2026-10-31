using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SamediRouge
{
    /// <summary>Interface en uGUI construite par code. Les boutons sont testés à la main (pas d'EventSystem requis).</summary>
    public partial class Hud : MonoBehaviour
    {
        /// <summary>Texte traduit (anglais, français), voir Loc.</summary>
        static string L(string en, string fr) => Loc.T(en, fr);
        public static readonly Color Good = new Color(0.56f, 0.86f, 0.45f);
        public static readonly Color Bad = new Color(1f, 0.48f, 0.42f);
        public static readonly Color Info = new Color(0.95f, 0.94f, 0.9f);
        public static readonly Color Yellow = new Color(0.98f, 0.84f, 0.12f);
        static readonly Color Panel = new Color(0.06f, 0.08f, 0.11f, 0.82f);
        static readonly Color Muted = new Color(0.64f, 0.68f, 0.73f);
        static readonly Color Blue = new Color(0.26f, 0.45f, 0.82f);
        static readonly Color Red = new Color(0.85f, 0.22f, 0.17f);

        class Btn { public RectTransform rt; public System.Action act; }
        class Floater { public Text t; public Vector3 world; public float life, max; }

        public System.Action<bool> onPick;
        public System.Action<int> onBuy;
        public System.Action onPause, onResume, onQuit, onAgain, onMenu;

        readonly List<Btn> buttons = new List<Btn>();
        readonly List<Floater> floaters = new List<Floater>();
        Font font;
        Sprite white, round, heart, vignette, fadeUp, dot, arrow, star;
        Camera cam;
        Canvas canvas;
        CanvasScaler scaler;
        RectTransform canvasRt, safe;
        Rect lastSafe;

        GameObject play, menu, pause, end, demoRoot;
        Text demoCap, demoSub, demoSkip, bankT, stageT, stageRight;
        Image stageBg, stageFill;
        GameObject tipRoot;
        Image tipDot, tipArrow, tipRing;
        Text tipTitle, tipSub, tipBang;
        int tipKind = -1;
        float tipT;
        Image demoDot;
        Image[] upBg;
        Text[] upTitle, upSub;
        Text hintT, tapT, ticker, distT, scoreT, koT, comboT, recT, toastT, bestT, endTitle, endVerdict, endKo, endScore, endLabels, endValues;
        Image[] hearts;
        Image damage, gas, comboBg, flash;
        GameObject hint, tapHint;
        Image[] slotBg;
        Text[] slotTimer;
        float toastLife, damageA, comboPop, tickerIn, flashA;

        // ------------------------------------------------------------------ construction

        public void Init(Camera camera)
        {
            cam = camera;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch (System.Exception) { font = null; }
            if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Helvetica Neue", "Arial", "Roboto" }, 32);
            MakeSprites();

            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 2340f);
            scaler.matchWidthOrHeight = 0f;
            canvasRt = (RectTransform)transform;

            // overlays plein écran (hors zone sûre)
            damage = Img(canvasRt, vignette, new Color(0.9f, 0.1f, 0.08f, 0f));
            Stretch(damage.rectTransform);
            gas = Img(canvasRt, white, new Color(0.95f, 0.93f, 0.96f, 0f));
            Stretch(gas.rectTransform);
            flash = Img(canvasRt, white, new Color(1f, 1f, 1f, 0f));
            Stretch(flash.rectTransform);

            safe = New("safe", canvasRt);
            Stretch(safe);

            BuildPlay();
            BuildMenu();
            BuildPause();
            BuildEnd();
            BuildMeta();

            for (int i = 0; i < 18; i++)
            {
                Text t = Label(canvasRt, "", 40, Info, TextAnchor.MiddleCenter, true);
                Shadowed(t);
                Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 90f));
                t.gameObject.SetActive(false);
                floaters.Add(new Floater { t = t });
            }
        }

        void BuildPlay()
        {
            play = New("play", safe).gameObject;
            RectTransform p = (RectTransform)play.transform;
            Stretch(p);

            // bandeau info
            Image bar = Img(p, round, Panel);
            Place(bar.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -22f), new Vector2(-56f, 62f), new Vector2(0.5f, 1f));
            Image live = Img(bar.rectTransform, round, Red);
            Place(live.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(210f, 62f), new Vector2(0f, 0.5f));
            Text liveT = Label(live.rectTransform, "ATTENTION", 28, Color.white, TextAnchor.MiddleCenter, true);
            Stretch(liveT.rectTransform);
            ticker = Label(bar.rectTransform, "", 30, Color.white, TextAnchor.MiddleLeft, true);
            Place(ticker.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(118f, 0f), new Vector2(-260f, 0f));
            ticker.horizontalOverflow = HorizontalWrapMode.Overflow;

            // pause, distance, score
            Image pb = Img(p, round, Panel);
            Place(pb.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -104f), new Vector2(92f, 92f), new Vector2(0f, 1f));
            pauseRt = pb.rectTransform;
            Text pt = Label(pb.rectTransform, "II", 40, Color.white, TextAnchor.MiddleCenter, true);
            Stretch(pt.rectTransform);
            AddButton(pb.rectTransform, () => onPause?.Invoke());

            distT = Label(p, "0", 76, Color.white, TextAnchor.UpperLeft, true);
            Shadowed(distT);
            Place(distT.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(140f, -96f), new Vector2(420f, 90f), new Vector2(0f, 1f));
            Text dl = distLabel = Label(p, L("METERS", "MÈTRES"), 26, Muted, TextAnchor.UpperLeft, true);
            Place(dl.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(144f, -178f), new Vector2(300f, 40f), new Vector2(0f, 1f));
            scoreT = Label(p, "0", 76, Color.white, TextAnchor.UpperRight, true);
            Shadowed(scoreT);
            Place(scoreT.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -96f), new Vector2(460f, 90f), new Vector2(1f, 1f));
            Text sl = scoreLabel = Label(p, "SCORE", 26, Muted, TextAnchor.UpperRight, true);
            Place(sl.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -178f), new Vector2(300f, 40f), new Vector2(1f, 1f));

            // cœurs, compteur, combo, REC
            hearts = new Image[6];
            for (int i = 0; i < hearts.Length; i++)
            {
                hearts[i] = Img(p, heart, Red);
                Place(hearts[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f + i * 48f, -236f), new Vector2(44f, 42f), new Vector2(0f, 1f));
            }
            Image kb = Img(p, round, Panel);
            Place(kb.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -306f), new Vector2(240f, 54f), new Vector2(1f, 1f));
            koT = Label(kb.rectTransform, L("0 ROBBERS", "0 VOLEURS"), 28, Color.white, TextAnchor.MiddleCenter, true);
            Stretch(koT.rectTransform);
            comboBg = Img(p, round, Yellow);
            Place(comboBg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -378f), new Vector2(240f, 56f), new Vector2(0f, 1f));
            comboT = Label(comboBg.rectTransform, "COMBO x2", 28, new Color(0.12f, 0.1f, 0.04f), TextAnchor.MiddleCenter, true);
            Stretch(comboT.rectTransform);
            Image rb = Img(p, round, Red);
            Place(rb.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -228f), new Vector2(150f, 60f), new Vector2(1f, 1f));
            recT = Label(rb.rectTransform, "● REC", 28, Color.white, TextAnchor.MiddleCenter, true);
            Stretch(recT.rectTransform);

            // message central
            toastT = Label(p, "", 96, Color.white, TextAnchor.MiddleCenter, true);
            Shadowed(toastT);
            Place(toastT.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 340f), new Vector2(1000f, 260f));

            // progression de l'étape, puis barre de vie du boss
            stageBg = Img(p, round, new Color(0.06f, 0.08f, 0.11f, 0.7f));
            Place(stageBg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -312f), new Vector2(600f, 50f), new Vector2(0f, 1f));
            stageFill = Img(stageBg.rectTransform, round, Blue);
            Place(stageFill.rectTransform, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f));
            stageT = Label(stageBg.rectTransform, L("STAGE 1", "ÉTAPE 1"), 28, Color.white, TextAnchor.MiddleLeft, true);
            Shadowed(stageT);
            Place(stageT.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-40f, 0f));
            stageRight = Label(stageBg.rectTransform, "BOSS >", 26, Yellow, TextAnchor.MiddleRight, true);
            Shadowed(stageRight);
            Place(stageRight.rectTransform, Vector2.zero, Vector2.one, new Vector2(-10f, 0f), new Vector2(-40f, 0f));

            // aide de départ (ancienne version texte, remplacée par les cartes de geste ci-dessous)
            Image h = Img(p, round, new Color(0f, 0f, 0f, 0f));
            Place(h.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(10f, 10f), new Vector2(0.5f, 0f));
            Text ht = hintT = Label(h.rectTransform, "", 30, Color.white, TextAnchor.MiddleCenter, false);
            hint = h.gameObject;

            // carte de geste : une seule consigne à la fois, grosse, avec le geste animé à gauche
            Image card = Img(p, round, new Color(0.05f, 0.07f, 0.1f, 0.88f));
            Place(card.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 230f), new Vector2(940f, 250f), new Vector2(0.5f, 0f));
            tipRoot = card.gameObject;
            RectTransform cr = card.rectTransform;
            Image edge = Img(cr, round, Yellow);
            Place(edge.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(14f, -40f), new Vector2(0f, 0.5f));
            edge.rectTransform.anchoredPosition = new Vector2(16f, 0f);
            RectTransform icon = New("icon", cr);
            Place(icon, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(150f, 0f), new Vector2(220f, 220f));
            tipArrow = Img(icon, arrow, new Color(1f, 1f, 1f, 0.75f));
            Place(tipArrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190f, 95f));
            tipRing = Img(icon, dot, new Color(1f, 0.85f, 0.2f, 0.4f));
            Place(tipRing.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90f, 90f));
            tipDot = Img(icon, dot, Yellow);
            Place(tipDot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(78f, 78f));
            tipBang = Label(tipDot.rectTransform, "!", 60, new Color(0.1f, 0.08f, 0.02f), TextAnchor.MiddleCenter, true);
            Stretch(tipBang.rectTransform);
            tipTitle = Label(cr, "", 66, Yellow, TextAnchor.LowerLeft, true);
            Shadowed(tipTitle);
            Fit(tipTitle, 34);
            Place(tipTitle.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(150f, 6f), new Vector2(-330f, -24f), new Vector2(0.5f, 0.5f));
            tipSub = Label(cr, "", 38, Color.white, TextAnchor.UpperLeft, false);
            Place(tipSub.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(150f, -8f), new Vector2(-330f, -24f), new Vector2(0.5f, 0.5f));
            Fit(tipSub, 24);
            tipRoot.SetActive(false);
            BuildGesture(p);

            // démo du lancement : légende géante, point qui pulse, invitation à jouer
            demoRoot = New("demo", p).gameObject;
            RectTransform d = (RectTransform)demoRoot.transform;
            Stretch(d);
            Image band = Img(d, round, new Color(0.05f, 0.07f, 0.1f, 0.72f));
            Place(band.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 300f), new Vector2(1000f, 300f), new Vector2(0.5f, 0f));
            demoCap = Label(band.rectTransform, "", 84, Yellow, TextAnchor.MiddleCenter, true);
            Shadowed(demoCap);
            Place(demoCap.rectTransform, new Vector2(0f, 0.38f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-40f, 0f));
            demoSub = Label(band.rectTransform, "", 36, Color.white, TextAnchor.MiddleCenter, false);
            Place(demoSub.rectTransform, new Vector2(0f, 0.05f), new Vector2(1f, 0.4f), Vector2.zero, new Vector2(-40f, 0f));
            demoDot = Img(d, round, new Color(1f, 1f, 1f, 0.5f));
            Place(demoDot.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 680f), new Vector2(120f, 120f));
            demoSkip = Label(d, L("TAP TO PLAY", "TAPE POUR JOUER"), 44, Color.white, TextAnchor.MiddleCenter, true);
            Shadowed(demoSkip);
            Place(demoSkip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(900f, 80f));
            demoRoot.SetActive(false);

            // équipement
            // équipement ramassé en route (5 pièces) + renfort
            string[] names = { L("HELMET", "CASQUE"), L("VEST", "GILET"), L("BATON", "MATRAQUE"), L("SHIELD", "BOUCLIER"), L("PISTOL", "PISTOLET"), L("SMG", "MITRA"), L("SQUAD", "ÉQUIPE") };
            slotBg = new Image[names.Length];
            slotTimer = new Text[names.Length];
            slotIcon = new Image[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                slotBg[i] = Img(p, round, Panel);
                Place(slotBg[i].rectTransform, Vector2.zero, Vector2.zero, new Vector2(20f + i * 149f, 36f), new Vector2(140f, 100f), Vector2.zero);
                // une icône par case (le nom n'est plus affiché : l'icône suffit)
                Text n = Label(slotBg[i].rectTransform, names[i], 23, Color.white, TextAnchor.MiddleCenter, true);
                n.gameObject.SetActive(false);
                squadLabel = n;
                slotIcon[i] = Img(slotBg[i].rectTransform, GearIcons.Slot(i, false), Color.white);
                Place(slotIcon[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(70f, 70f));
                slotTimer[i] = Label(slotBg[i].rectTransform, "", 24, Yellow, TextAnchor.MiddleCenter, true);
                Shadowed(slotTimer[i]);
                Place(slotTimer[i].rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.3f), Vector2.zero, Vector2.zero);
            }
            Text th = tapT = Label(p, "", 26, Muted, TextAnchor.LowerRight, true);
            Place(th.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-34f, 160f), new Vector2(300f, 90f), new Vector2(1f, 0f));
            tapHint = th.gameObject;
        }

        void BuildMenu()
        {
            menu = New("menu", canvasRt).gameObject;
            RectTransform m = (RectTransform)menu.transform;
            Stretch(m);
            Image fade = Img(m, fadeUp, new Color(0.05f, 0.07f, 0.1f, 0.97f));
            Place(fade.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.72f), Vector2.zero, Vector2.zero);
            RectTransform c = New("content", m);
            Stretch(c);

            Text kicker = Label(c, L("DOORS OPEN · 6:00 AM", "OUVERTURE · 6 H 00"), 34, new Color(0.5f, 0.66f, 1f), TextAnchor.LowerLeft, true);
            Place(kicker.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 1400f), new Vector2(-96f, 60f), new Vector2(0.5f, 0f));
            // une seule ligne (sinon le texte déborde vers le haut et passe sous le sur-titre)
            Text t1 = Label(c, "BLACK FRIDAY", 116, Color.white, TextAnchor.LowerLeft, true);
            t1.horizontalOverflow = HorizontalWrapMode.Overflow;
            Shadowed(t1);
            Place(t1.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 1230f), new Vector2(-96f, 190f), new Vector2(0.5f, 0f));
            Text t2 = Label(c, "RUSH", 250, Yellow, TextAnchor.LowerLeft, true);
            Shadowed(t2);
            Place(t2.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 950f), new Vector2(-96f, 280f), new Vector2(0.5f, 0f));
            Text lede = Label(c, L("The busiest day of the year. Pick a side: stop the robbers as a cop, or grab the loot as a robber. Either way, never hit a shopper.", "Le jour le plus chargé de l'année. Choisis ton camp : arrête les voleurs en policier, ou rafle le butin en voleur. Dans tous les cas, ne frappe jamais un client."), 38, new Color(0.88f, 0.9f, 0.92f), TextAnchor.UpperLeft, false);
            Place(lede.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 900f), new Vector2(-96f, 250f), new Vector2(0.5f, 1f));

            // améliorations achetées avec les pièces gagnées en partie
            bankT = Label(c, "", 34, Yellow, TextAnchor.MiddleLeft, true);
            Place(bankT.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 700f), new Vector2(-96f, 50f), new Vector2(0.5f, 0f));
            string[] ups = { L("START: HELMET", "DÉPART : CASQUE"), L("START: VEST", "DÉPART : GILET"), L("START: BATON", "DÉPART : MATRAQUE"), L("+1 SQUAD MATE", "+1 COÉQUIPIER") };
            upBg = new Image[4];
            upTitle = new Text[4];
            upSub = new Text[4];
            for (int i = 0; i < 4; i++)
            {
                int id = i;
                Image box = Img(c, round, new Color(0.13f, 0.16f, 0.21f, 0.95f));
                float x = (i % 2) * 0.5f;
                Place(box.rectTransform, new Vector2(x, 0f), new Vector2(x + 0.5f, 0f), new Vector2(0f, 560f - (i / 2) * 130f), new Vector2(-62f, 116f), new Vector2(0.5f, 0f));
                box.rectTransform.anchoredPosition = new Vector2(i % 2 == 0 ? 24f : -24f, 560f - (i / 2) * 130f);
                upBg[i] = box;
                upTitle[i] = Label(box.rectTransform, ups[i], 32, Yellow, TextAnchor.MiddleLeft, true);
                Place(upTitle[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(24f, -4f), new Vector2(-40f, 0f));
                upSub[i] = Label(box.rectTransform, "", 28, Muted, TextAnchor.MiddleLeft, false);
                Place(upSub[i].rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(24f, 6f), new Vector2(-40f, 0f));
                AddButton(box.rectTransform, () => onBuy?.Invoke(id));
            }

            // choix du camp
            PickButton(c, true);
            PickButton(c, false);

            bestT = Label(c, "", 32, Muted, TextAnchor.MiddleLeft, true);
            Place(bestT.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 160f), new Vector2(-96f, 50f), new Vector2(0.5f, 0f));
            Text keys = Label(c, L("Keyboard: arrows or WASD, Space to jump, F to tap. Cartoon fun, nobody gets hurt for real.", "Clavier : flèches ou WASD, Espace pour sauter, F pour taper. C'est un dessin animé, personne n'est blessé pour de vrai."), 26, Muted, TextAnchor.UpperLeft, false);
            Place(keys.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 140f), new Vector2(-96f, 100f), new Vector2(0.5f, 1f));
        }

        void BuildPause()
        {
            pause = New("pause", canvasRt).gameObject;
            RectTransform p = (RectTransform)pause.transform;
            Stretch(p);
            Image dim = Img(p, white, new Color(0.05f, 0.07f, 0.1f, 0.75f));
            Stretch(dim.rectTransform);
            Image card = Img(p, round, new Color(0.11f, 0.14f, 0.19f, 1f));
            Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 420f));
            Text title = Label(card.rectTransform, L("PAUSED", "PAUSE"), 90, Color.white, TextAnchor.UpperCenter, true);
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -40f), new Vector2(0f, 120f), new Vector2(0.5f, 1f));
            MakeButton(card.rectTransform, L("RESUME", "REPRENDRE"), Blue, new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(30f, 40f), () => onResume?.Invoke());
            MakeButton(card.rectTransform, L("QUIT", "QUITTER"), new Color(0.2f, 0.24f, 0.3f), new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 40f), () => onQuit?.Invoke());
            pause.SetActive(false);
        }

        void BuildEnd()
        {
            end = New("end", canvasRt).gameObject;
            RectTransform e = (RectTransform)end.transform;
            Stretch(e);
            Image fade = Img(e, fadeUp, new Color(0.05f, 0.07f, 0.1f, 0.95f));
            Place(fade.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.8f), Vector2.zero, Vector2.zero);
            Image card = Img(e, round, new Color(0.11f, 0.14f, 0.19f, 0.98f));
            Place(card.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 48f), new Vector2(-64f, 1240f), new Vector2(0.5f, 0f));
            RectTransform cr = card.rectTransform;

            Image tag = Img(cr, round, Red);
            Place(tag.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(380f, 54f), new Vector2(0f, 1f));
            Text tagT = Label(tag.rectTransform, L("SHIFT REPORT", "RAPPORT DE SERVICE"), 24, Color.white, TextAnchor.MiddleCenter, true);
            Stretch(tagT.rectTransform);
            endTitle = Label(cr, "", 62, Color.white, TextAnchor.UpperLeft, true);
            Place(endTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -120f), new Vector2(-80f, 230f), new Vector2(0.5f, 1f));
            endVerdict = Label(cr, "", 36, new Color(0.86f, 0.88f, 0.9f), TextAnchor.UpperLeft, false);
            Place(endVerdict.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -360f), new Vector2(-80f, 110f), new Vector2(0.5f, 1f));

            endKo = StatBox(cr, L("ROBBERS", "VOLEURS"), new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(40f, -480f), new Vector2(-50f, 170f));
            endScore = StatBox(cr, "SCORE", new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(10f, -480f), new Vector2(-50f, 170f));

            endLabels = Label(cr, "", 34, Muted, TextAnchor.UpperLeft, false);
            Place(endLabels.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(40f, -680f), new Vector2(0f, 360f), new Vector2(0f, 1f));
            endLabels.lineSpacing = 1.15f;
            endValues = Label(cr, "", 34, Color.white, TextAnchor.UpperLeft, true);
            Place(endValues.rectTransform, new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(0f, -680f), new Vector2(-40f, 360f), new Vector2(0f, 1f));
            endValues.lineSpacing = 1.15f;

            MakeButton(cr, L("PLAY AGAIN", "REJOUER"), Blue, new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(40f, 40f), () => onAgain?.Invoke());
            MakeButton(cr, "MENU", new Color(0.2f, 0.24f, 0.3f), new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 40f), () => onMenu?.Invoke());
            end.SetActive(false);
        }

        void PickButton(RectTransform c, bool guard)
        {
            Image b = Img(c, round, guard ? Blue : new Color(0.12f, 0.12f, 0.14f, 1f));
            float x = guard ? 0f : 0.5f;
            Place(b.rectTransform, new Vector2(x, 0f), new Vector2(x + 0.5f, 0f), new Vector2(guard ? 24f : -24f, 210f), new Vector2(-62f, 190f), new Vector2(0.5f, 0f));
            if (!guard)
            {
                // bouton noir avec un liseré rouge (casseur cagoulé)
                Image edge = Img(b.rectTransform, white, new Color(0.86f, 0.2f, 0.15f, 0.85f));
                Place(edge.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 14f), new Vector2(-60f, 6f));
            }
            Text t = Label(b.rectTransform, guard ? L("COP", "POLICIER") : L("ROBBER", "VOLEUR"), 66, guard ? Color.white : Yellow, TextAnchor.MiddleCenter, true);
            Place(t.rectTransform, new Vector2(0f, 0.4f), new Vector2(1f, 1f), new Vector2(40f, 0f), new Vector2(-80f, 0f));
            FactionIcon(b.rectTransform, !guard, new Vector2(0f, 0.7f), new Vector2(66f, 0f), 96f);
            Text sub = Label(b.rectTransform, guard ? L("stop the robbers", "arrête les voleurs") : L("grab the loot, steal their gear", "rafle le butin, vole leur équipement"), 30, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleCenter, false);
            Place(sub.rectTransform, new Vector2(0f, 0.08f), new Vector2(1f, 0.42f), Vector2.zero, Vector2.zero);
            AddButton(b.rectTransform, () => onPick?.Invoke(!guard));
        }

        /// <summary>Adapte les textes au camp choisi.</summary>
        bool thiefMode;
        Text squadLabel;
        Image[] slotIcon;

        public void SetMode(bool thief)
        {
            thiefMode = thief;
            if (slotIcon != null) SetRunGear(thief, runLv[0], runLv[1], runLv[2], runLv[3]);
            if (squadLabel != null) squadLabel.text = thief ? L("CREW", "BANDE") : L("SQUAD", "ÉQUIPE");
            koNoun = thief ? L("COP", "POLICIER") : L("ROBBER", "VOLEUR");
            koNouns = thief ? L("COPS", "POLICIERS") : L("ROBBERS", "VOLEURS");
            hintT.text = thief
                ? L("<color=#F9D61F>Tap</color> to shove the cops in yellow.\n<color=#F9D61F>Swipe</color> to change lanes, jump or slide.\nRun into your buddies to grow your crew.\nSteal their gear: pistol, then the SMG! Don't hit shoppers.", "<color=#F9D61F>Tape</color> pour bousculer les policiers en jaune.\n<color=#F9D61F>Glisse</color> pour changer de couloir, sauter ou glisser.\nFonce sur tes potes pour agrandir ta bande.\nVole leur équipement : pistolet, puis la mitraillette ! Ne frappe pas les clients.")
                : L("<color=#F9D61F>Tap</color> to knock out the robbers in black.\n<color=#F9D61F>Swipe</color> to change lanes, jump or slide.\nRun into your colleagues to grow your squad.\nGrab your gear: pistol, then the SMG! Don't hit shoppers.", "<color=#F9D61F>Tape</color> pour assommer les voleurs en noir.\n<color=#F9D61F>Glisse</color> pour changer de couloir, sauter ou glisser.\nFonce sur tes collègues pour agrandir ton escouade.\nRamasse ton équipement : pistolet, puis la mitraillette ! Ne frappe pas les clients.");
            tapT.text = "";
            endKoLabel.text = koNouns;
        }

        string koNoun = "ROBBER", koNouns = "ROBBERS"; // traduits dans SetFaction (PlayerPrefs interdit dans un constructeur)
        Text endKoLabel;

        Text StatBox(RectTransform parent, string label, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
        {
            Image b = Img(parent, round, new Color(0.07f, 0.09f, 0.13f, 1f));
            Place(b.rectTransform, aMin, aMax, pos, size, new Vector2(0f, 1f));
            Text l = Label(b.rectTransform, label, 26, Muted, TextAnchor.UpperLeft, true);
            if (endKoLabel == null) endKoLabel = l;
            Place(l.rectTransform, Vector2.zero, Vector2.one, new Vector2(24f, -18f), new Vector2(-48f, -36f));
            Text v = Label(b.rectTransform, "0", 72, Color.white, TextAnchor.LowerLeft, true);
            Place(v.rectTransform, Vector2.zero, Vector2.one, new Vector2(24f, 14f), new Vector2(-48f, -28f));
            return v;
        }

        void MakeButton(RectTransform parent, string label, Color c, Vector2 aMin, Vector2 aMax, Vector2 pos, System.Action act)
        {
            Image b = Img(parent, round, c);
            Place(b.rectTransform, aMin, aMax, pos, new Vector2(-50f, 130f), new Vector2(aMin.x < 0.25f ? 0f : 1f, 0f));
            Text t = Label(b.rectTransform, label, 44, Color.white, TextAnchor.MiddleCenter, true);
            Stretch(t.rectTransform);
            t.rectTransform.sizeDelta = new Vector2(-24f, -8f);
            Fit(t, 24);
            AddButton(b.rectTransform, act);
        }

        // ------------------------------------------------------------------ helpers uGUI

        RectTransform New(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        Image Img(Transform parent, Sprite sprite, Color c)
        {
            RectTransform rt = New("img", parent);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = sprite == round ? Image.Type.Sliced : Image.Type.Simple;
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        Text Label(Transform parent, string s, int size, Color c, TextAnchor anchor, bool bold)
        {
            RectTransform rt = New("text", parent);
            Text t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = s;
            t.fontSize = size;
            t.color = c;
            t.alignment = anchor;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Le texte rétrécit pour tenir dans son cadre (le français est plus long que l'anglais).</summary>
        static void Fit(Text t, int min)
        {
            t.resizeTextForBestFit = true;
            t.resizeTextMaxSize = t.fontSize;
            t.resizeTextMinSize = min;
            t.verticalOverflow = VerticalWrapMode.Truncate;
        }

        static void Shadowed(Text t)
        {
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.6f);
            sh.effectDistance = new Vector2(3f, -4f);
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }

        static void Place(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        void AddButton(RectTransform rt, System.Action act) { buttons.Add(new Btn { rt = rt, act = act }); }

        /// <summary>Renvoie vrai si un bouton visible a été touché.</summary>
        public bool Click(Vector2 screen)
        {
            // juste après l'ouverture d'un écran : on ignore les taps encore en cours (évite de sauter le résultat)
            if (Time.unscaledTime < clickLockUntil) return true;
            for (int i = buttons.Count - 1; i >= 0; i--)
            {
                Btn b = buttons[i];
                if (!b.rt.gameObject.activeInHierarchy) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(b.rt, screen, null))
                {
                    Snd.Play(Snd.I != null ? Snd.I.tick : null, 0.6f, 0f);
                    b.act?.Invoke();
                    return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------ sprites générés

        void MakeSprites()
        {
            star = MakeStar();
            white = Sprite.Create(Solid(4, 4), new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);

            const int n = 64, rad = 22;
            var tr = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, rad, n - rad), cy = Mathf.Clamp(y + 0.5f, rad, n - rad);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                float a = Mathf.Clamp01(rad - d + 0.5f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tr.SetPixels32(px);
            tr.Apply();
            round = Sprite.Create(tr, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(rad, rad, rad, rad));

            var th = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2.6f - 1.3f, v = (y + 0.5f) / n * 2.6f - 1.2f;
                float f = Mathf.Pow(u * u + v * v - 1f, 3f) - u * u * v * v * v;
                float a = Mathf.Clamp01(-f * 60f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            th.SetPixels32(px);
            th.Apply();
            heart = Sprite.Create(th, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);

            const int vn = 128;
            var tv = new Texture2D(vn, vn, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var vp = new Color32[vn * vn];
            for (int y = 0; y < vn; y++)
            for (int x = 0; x < vn; x++)
            {
                float u = (x + 0.5f) / vn * 2f - 1f, v = (y + 0.5f) / vn * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v) / 1.414f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, r));
                vp[y * vn + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tv.SetPixels32(vp);
            tv.Apply();
            vignette = Sprite.Create(tv, new Rect(0, 0, vn, vn), new Vector2(0.5f, 0.5f), 100f);

            var tf = new Texture2D(4, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var fp = new Color32[4 * 64];
            for (int y = 0; y < 64; y++)
            {
                float a = Mathf.SmoothStep(1f, 0f, y / 63f);
                for (int x = 0; x < 4; x++) fp[y * 4 + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tf.SetPixels32(fp);
            tf.Apply();
            fadeUp = Sprite.Create(tf, new Rect(0, 0, 4, 64), new Vector2(0.5f, 0.5f), 100f);

            // disque plein (doigt) et flèche épaisse vers la droite, pour les gestes du tutoriel
            const int dn = 64;
            var td = new Texture2D(dn, dn, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var dp = new Color32[dn * dn];
            for (int y = 0; y < dn; y++)
            for (int x = 0; x < dn; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(dn * 0.5f, dn * 0.5f));
                dp[y * dn + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(dn * 0.5f - 1f - d) * 255f));
            }
            td.SetPixels32(dp);
            td.Apply();
            dot = Sprite.Create(td, new Rect(0, 0, dn, dn), new Vector2(0.5f, 0.5f), 100f);

            const int aw = 128, ah = 64;
            var ta = new Texture2D(aw, ah, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var ap = new Color32[aw * ah];
            for (int y = 0; y < ah; y++)
            for (int x = 0; x < aw; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f - ah * 0.5f;
                // tige de 0 à 80, pointe triangulaire de 72 à 124
                float shaft = Mathf.Min(Mathf.Min(fx - 4f, 82f - fx), 9f - Mathf.Abs(fy));
                float head = Mathf.Min(fx - 70f, (124f - fx) * 0.6f - Mathf.Abs(fy));
                float a = Mathf.Clamp01(Mathf.Max(shaft, head) + 0.5f);
                ap[y * aw + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            ta.SetPixels32(ap);
            ta.Apply();
            arrow = Sprite.Create(ta, new Rect(0, 0, aw, ah), new Vector2(0.5f, 0.5f), 100f);
        }

        static Texture2D Solid(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        // ------------------------------------------------------------------ API jeu

        public void ShowMenu(int bestGuard, int bestThief, int bank, int[] levels, int[] costs)
        {
            demoRoot.SetActive(false);
            HideTip();
            HideMeta();
            bankT.text = L("EQUIPMENT  ·  ", "ÉQUIPEMENT  ·  ") + bank + L(" COINS", " PIÈCES");
            for (int i = 0; i < upBg.Length; i++)
            {
                bool maxed = costs[i] < 0;
                bool afford = !maxed && bank >= costs[i];
                string lv = i == 3 ? "  (" + levels[i] + "/3)" : "";
                upSub[i].text = maxed ? (i == 3 ? "MAX (3/3)" : L("OWNED", "ACQUIS")) : costs[i] + L(" coins", " pièces") + lv;
                upSub[i].color = maxed ? new Color(0.5f, 0.85f, 0.55f) : afford ? Color.white : Muted;
                upBg[i].color = maxed ? new Color(0.12f, 0.22f, 0.16f, 0.95f) : afford ? new Color(0.22f, 0.3f, 0.5f, 0.98f) : new Color(0.13f, 0.16f, 0.21f, 0.95f);
            }
            menu.SetActive(true);
            play.SetActive(false);
            pause.SetActive(false);
            end.SetActive(false);
            bestT.text = bestGuard > 0 || bestThief > 0 ? L("Best · cop ", "Record · policier ") + bestGuard + L(" · robber ", " · voleur ") + bestThief : "";
            gas.color = new Color(1f, 1f, 1f, 0f);
        }

        /// <summary>Démo du lancement : le jeu tourne tout seul, avec légendes.</summary>
        public void ShowDemo()
        {
            ShowPlay();
            hint.SetActive(false);
            tapHint.SetActive(false);
            demoRoot.SetActive(true);
        }

        public void DemoCaption(float t)
        {
            string cap, sub;
            if (t < 3.2f) { cap = L("TAP TO SWING", "TAPE POUR FRAPPER"); sub = L("One swing knocks out the whole gang", "Un coup assomme toute la bande"); }
            else if (t < 6.4f) { cap = thiefMode ? L("BUILD YOUR CREW", "FORME TA BANDE") : L("BUILD YOUR SQUAD", "FORME TON ESCOUADE"); sub = thiefMode ? L("Run into the other rioters to recruit them", "Fonce sur les autres casseurs pour les recruter") : L("Run into your colleagues to recruit them", "Fonce sur tes collègues pour les recruter"); }
            else if (t < 10f) { cap = L("GEAR UP!", "ÉQUIPE-TOI !"); sub = L("Win gear between runs: from the baton to the SMG", "Gagne ton équipement entre les courses : de la matraque à la mitraillette"); }
            else { cap = L("SWIPE TO DODGE", "GLISSE POUR ESQUIVER"); sub = L("Jump the carts, slide under the signs. Never hit a shopper!", "Saute les chariots, glisse sous les panneaux. Ne frappe jamais un client !"); }
            demoCap.text = cap;
            demoSub.text = sub;
            float k = Mathf.Repeat(t, 1f);
            demoCap.transform.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(t * 6f));
            demoDot.transform.localScale = Vector3.one * (0.6f + k * 0.9f);
            demoDot.color = new Color(1f, 1f, 1f, 0.6f * (1f - k));
            demoSkip.color = new Color(1f, 1f, 1f, 0.55f + 0.45f * Mathf.Sin(t * 5f));
        }

        public void ShowPlay()
        {
            HideMeta();
            HideTip();
            demoRoot.SetActive(false);
            menu.SetActive(false);
            play.SetActive(true);
            pause.SetActive(false);
            end.SetActive(false);
            hint.SetActive(false);
            tapHint.SetActive(true);
            toastT.text = "";
        }

        public void ShowPause(bool on) { pause.SetActive(on); }

        public void ShowEnd(string title, string verdict, int ko, int score, string labels, string values)
        {
            HideTip();
            HideMeta();
            demoRoot.SetActive(false);
            end.SetActive(true);
            play.SetActive(false);
            endTitle.text = title;
            endVerdict.text = verdict;
            endKo.text = ko.ToString();
            endScore.text = score.ToString();
            endLabels.text = labels;
            endValues.text = values;
        }

        /// <summary>Barre du haut : avancée vers le boss de l'étape, ou vie du boss pendant le combat.</summary>
        public void SetStage(int stage, float frac, string bossName, float bossFrac)
        {
            bool fight = bossName != null;
            float f = Mathf.Clamp01(fight ? bossFrac : frac);
            RectTransform rt = stageFill.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Max(0.04f, f), 1f);
            rt.sizeDelta = Vector2.zero;
            stageFill.color = fight ? Red : Blue;
            stageT.text = fight ? bossName : L("STAGE ", "ÉTAPE ") + stage;
            stageRight.text = fight ? "BOSS" : frac >= 1f ? "BOSS!" : "BOSS >";
            stageBg.transform.localScale = fight ? Vector3.one * (1.04f + 0.02f * Mathf.Sin(Time.unscaledTime * 10f)) : Vector3.one;
        }

        /// <summary>
        /// Carte de consigne. kind : 0 toucher, 1 glisser gauche/droite, 2 glisser vers le haut, 3 glisser vers le bas, 4 info.
        /// </summary>
        public void ShowTip(int kind, string title, string sub, int dir = 0)
        {
            if (tipKind == kind && tipRoot.activeSelf && tipTitle.text == title) return;
            ShowGesture(kind, dir);
            tipKind = kind;
            tipT = 0f;
            tipTitle.text = title;
            tipSub.text = sub;
            tipArrow.gameObject.SetActive(kind >= 1 && kind <= 3);
            tipArrow.transform.localRotation = Quaternion.Euler(0f, 0f, kind == 2 ? 90f : kind == 3 ? -90f : 0f);
            tipBang.gameObject.SetActive(kind == 4);
            // la main fait la démonstration en grand : dans la carte, on ne garde que la flèche (ou l'onde du toucher)
            tipDot.enabled = kind == 0 || kind == 4;
            tipRing.enabled = kind == 0 || kind == 4;
            if (kind == 1 && dir != 0) tipArrow.transform.localRotation = Quaternion.Euler(0f, 0f, dir < 0 ? 180f : 0f);
            tipRoot.SetActive(true);
            tipRoot.transform.localScale = Vector3.one * 0.6f;
        }

        public void HideTip()
        {
            tipKind = -1;
            if (tipRoot != null) tipRoot.SetActive(false);
            HideGesture();
        }

        void TickTip(float udt)
        {
            if (tipRoot == null || !tipRoot.activeSelf) return;
            tipT += udt;
            // apparition avec rebond
            float k = Mathf.Clamp01(tipT / 0.25f);
            tipRoot.transform.localScale = Vector3.one * (k < 1f ? 0.6f + 0.5f * Mathf.Sin(k * Mathf.PI * 0.75f) / 0.92f * 0.92f : 1f);
            float c = Mathf.Repeat(tipT * 1.4f, 1f);
            Vector2 p = Vector2.zero;
            float s = 1f;
            switch (tipKind)
            {
                case 0: s = c < 0.25f ? 1f - c * 1.2f : 0.7f + (c - 0.25f) * 0.4f; break;     // le doigt appuie
                case 1: p = new Vector2(Mathf.Sin(tipT * 4f) * 70f, 0f); break;              // va-et-vient gauche/droite
                case 2: p = new Vector2(0f, -55f + c * 110f); break;                           // glisse vers le haut
                case 3: p = new Vector2(0f, 55f - c * 110f); break;                            // glisse vers le bas
                default: s = 1f + 0.08f * Mathf.Sin(tipT * 8f); break;
            }
            tipDot.rectTransform.anchoredPosition = p;
            tipDot.transform.localScale = Vector3.one * s;
            tipRing.rectTransform.anchoredPosition = p;
            float r = tipKind == 0 ? c : Mathf.Repeat(tipT * 2f, 1f);
            tipRing.transform.localScale = Vector3.one * (1f + r * 1.2f);
            tipRing.color = new Color(1f, 0.85f, 0.2f, 0.5f * (1f - r));
            TickGesture(tipT);
        }

        public void Hint(bool on) { if (hint != null && hint.activeSelf) hint.SetActive(false); }
        public void TapHint(bool on) { if (tapHint != null && tapHint.activeSelf != on) tapHint.SetActive(on); }

        public void Ticker(string msg, bool alert)
        {
            if (Quiet) return;
            ticker.text = msg;
            ticker.color = alert ? Yellow : Color.white;
            tickerIn = 1f;
        }

        public void Toast(string msg)
        {
            toastT.text = msg;
            toastLife = 1.4f;
        }

        public void Damage(float k = 1f) { damageA = Mathf.Max(damageA, 0.85f * k); }

        /// <summary>Éclair blanc plein écran (impact, boss attrapé).</summary>
        public void Flash(float a) { flashA = Mathf.Max(flashA, a); }
        public void Gas(float a) { gas.color = new Color(0.95f, 0.93f, 0.96f, a); }
        public void ComboPop() { comboPop = 1f; }

        // étiquettes posées dans la scène (valeurs des portes, taille de la bande), redessinées à chaque image
        readonly List<Text> tags = new List<Text>();
        int tagN;

        public void BeginTags() { tagN = 0; }

        /// <summary>Étiquette à une position du monde ; size : hauteur approximative en mètres.</summary>
        public void Tag(Vector3 world, string text, Color c, float size)
        {
            if (cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0.5f) return;
            if (tagN >= tags.Count)
            {
                Text nt = Label(canvasRt, "", 60, c, TextAnchor.MiddleCenter, true);
                Shadowed(nt);
                Place(nt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 200f));
                nt.transform.SetAsFirstSibling();
                tags.Add(nt);
            }
            Text t = tags[tagN++];
            if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
            t.text = text;
            t.color = c;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, sp, null, out Vector2 lp);
            t.rectTransform.anchoredPosition = lp;
            // taille à l'écran : hauteur en pixels d'un objet de « size » mètres à cette distance
            float px = size * Screen.height / (2f * sp.z * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
            float k = canvasRt.rect.height / Mathf.Max(1f, Screen.height);
            t.fontSize = Mathf.Clamp(Mathf.RoundToInt(px * k * 0.5f), 18, 140);
        }

        public void EndTags()
        {
            for (int i = tagN; i < tags.Count; i++) if (tags[i].gameObject.activeSelf) tags[i].gameObject.SetActive(false);
        }

        /// <summary>Mode arcade : pas de textes flottants ni de bandeau défilant, seulement les infos utiles (portes, fusions).</summary>
        public static bool Quiet;
        bool forceFloat;

        /// <summary>Texte flottant qui s'affiche même en mode silencieux (portes, fusions d'armes).</summary>
        public void FloatKey(Vector3 world, string text, Color c, bool big = true)
        {
            forceFloat = true;
            Float(world, text, c, big);
            forceFloat = false;
        }

        public void Float(Vector3 world, string text, Color c, bool big = false)
        {
            if (Quiet && !forceFloat) return;
            Floater f = null;
            foreach (Floater x in floaters) if (x.life <= 0f) { f = x; break; }
            if (f == null) f = floaters[0];
            f.world = world;
            f.life = f.max = big ? 0.8f : 1.2f;
            f.t.text = text;
            f.t.color = c;
            f.t.fontSize = big ? 64 : 38;
            f.t.gameObject.SetActive(true);
        }

        readonly int[] runLv = new int[4];

        /// <summary>Objets gagnés portés pour cette course : icônes des cases tête, corps, arme, bouclier.</summary>
        public void SetRunGear(bool thief, int head, int body, int weapon, int shield)
        {
            runLv[0] = head; runLv[1] = body; runLv[2] = weapon; runLv[3] = shield;
            if (slotIcon == null) return;
            for (int i = 0; i < 4; i++) slotIcon[i].sprite = GearIcons.Upgrade(i, thief, runLv[i]);
            slotIcon[4].sprite = GearIcons.Get("plate");
            slotIcon[5].sprite = GearIcons.Get("charge");
            slotIcon[6].sprite = GearIcons.Get(thief ? "gang" : "crew");
        }

        public void SetPlay(int dist, int score, int heartsLeft, int ko, float comboMult, bool rec, bool[] gear, int squad, int shieldHits = 0, float chargeT = 0f)
        {
            distT.text = dist.ToString();
            scoreT.text = score.ToString();
            for (int i = 0; i < hearts.Length; i++)
                if (hearts[i].gameObject.activeSelf) hearts[i].gameObject.SetActive(false);
            SetHealth(heartsLeft, maxHearts);
            koT.text = ko + " " + (ko == 1 ? koNoun : koNouns);
            if (koT.transform.parent.gameObject.activeSelf == Quiet) koT.transform.parent.gameObject.SetActive(!Quiet);   // arcade : pas de compteur de KO
            bool combo = comboMult > 1f && !Quiet;
            if (comboBg.gameObject.activeSelf != combo) comboBg.gameObject.SetActive(combo);
            if (combo) comboT.text = "COMBO x" + comboMult.ToString("0.#").Replace('.', ',');
            bool recOn = rec && Mathf.Repeat(Time.unscaledTime, 1f) < 0.65f;
            recT.transform.parent.gameObject.SetActive(rec);
            recT.enabled = recOn;
            // cases : 0 tête, 1 corps, 2 arme, 3 bouclier gagné (coups restants), 4 plaque d'armure, 5 charge (temps), 6 équipe
            int next = nextGearSlot;   // indice dans gear[] : 1 plaque, 3 charge
            for (int i = 0; i < 3; i++) Slot(i, runLv[i] > 0, "", false);
            Slot(3, shieldHits > 0, runLv[3] > 0 ? "x" + shieldHits : "", false);
            Slot(4, gear[1], gear[1] ? "" : next == 1 ? L("NEXT", "SUIV.") : "", next == 1 && !gear[1]);
            Slot(5, gear[3], gear[3] && chargeT > 0f ? Mathf.CeilToInt(chargeT) + " s" : next == 3 ? L("NEXT", "SUIV.") : "", next == 3 && !gear[3]);
            Slot(6, squad > 0, squad > 0 ? "x" + squad : "", false);
            if (Quiet) for (int i = 0; i < slotShow.Length; i++) slotShow[i] = false;   // arcade : la taille de la bande est affichée au-dessus du héros
            LayoutSlots();
        }

        void Slot(int i, bool on, string timer, bool next)
        {
            slotShow[i] = on || next;
            slotBg[i].color = on ? new Color(0.16f, 0.26f, 0.5f, 0.95f) : next ? new Color(0.3f, 0.26f, 0.06f, 0.75f) : new Color(0.06f, 0.08f, 0.11f, 0.45f);
            slotTimer[i].text = timer;
            slotTimer[i].color = on ? Color.white : Yellow;
            if (slotIcon != null) slotIcon[i].color = on ? Color.white : next ? new Color(1f, 1f, 1f, 0.8f) : new Color(1f, 1f, 1f, 0.28f);
            slotBg[i].transform.localScale = on || next ? Vector3.one * (1f + 0.03f * Mathf.Sin(Time.unscaledTime * 8f)) : Vector3.one;
        }

        public void Tick(float udt)
        {
            TickTip(udt);
            TickMeta(udt);
            // zone sûre (encoche) et échelle selon l'orientation
            Rect sa = Screen.safeArea;
            if (sa != lastSafe && Screen.width > 0 && Screen.height > 0)
            {
                lastSafe = sa;
                safe.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
                safe.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
                safe.offsetMin = Vector2.zero;
                safe.offsetMax = Vector2.zero;
            }
            scaler.matchWidthOrHeight = Screen.width > Screen.height * 0.75f ? 1f : 0f;

            if (toastLife > 0f)
            {
                toastLife -= udt;
                float a = Mathf.Clamp01(toastLife / 0.3f);
                float s = 1f + Mathf.Max(0f, toastLife - 1.2f) * 2f;
                toastT.color = new Color(1f, 1f, 1f, a);
                toastT.transform.localScale = Vector3.one * s;
            }
            else if (toastT.text.Length > 0) toastT.text = "";

            damageA = Mathf.MoveTowards(damageA, 0f, udt * 1.6f);
            damage.color = new Color(0.9f, 0.1f, 0.08f, damageA);
            flashA = Mathf.MoveTowards(flashA, 0f, udt * 4f);
            flash.color = new Color(1f, 1f, 1f, flashA);
            flash.enabled = flashA > 0.001f;

            comboPop = Mathf.MoveTowards(comboPop, 0f, udt * 5f);
            comboBg.transform.localScale = Vector3.one * (1f + comboPop * 0.35f);

            tickerIn = Mathf.MoveTowards(tickerIn, 0f, udt * 4f);
            ticker.rectTransform.anchoredPosition = new Vector2(118f, -tickerIn * 30f);

            for (int i = 0; i < floaters.Count; i++)
            {
                Floater f = floaters[i];
                if (f.life <= 0f) continue;
                f.life -= udt;
                if (f.life <= 0f || cam == null) { f.t.gameObject.SetActive(false); continue; }
                float k = 1f - f.life / f.max;
                Vector3 sp = cam.WorldToScreenPoint(f.world + Vector3.up * (k * 1.2f));
                if (sp.z < 0f) { f.t.gameObject.SetActive(false); f.life = 0f; continue; }
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, sp, null, out Vector2 lp);
                f.t.rectTransform.anchoredPosition = lp;
                Color c = f.t.color;
                c.a = Mathf.Clamp01(f.life / (f.max * 0.4f));
                f.t.color = c;
                f.t.transform.localScale = Vector3.one * (1f + Mathf.Max(0f, 0.15f - k) * 3f);
            }
        }
    }
}
