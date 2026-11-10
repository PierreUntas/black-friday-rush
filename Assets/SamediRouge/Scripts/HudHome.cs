using UnityEngine;
using UnityEngine.UI;

namespace SamediRouge
{
    /// <summary>
    /// Écran d'accueil arcade (à la Hell Escape) : le héros, le niveau, « GLISSE POUR JOUER »
    /// et trois grosses cartes d'amélioration (Bande, Arme, Revenus). Pas de carte du monde ni de pages d'équipement.
    /// </summary>
    public partial class Hud
    {
        GameObject homeRoot;
        Text homeLevel, homeCoins, homeSwipe;
        readonly Image[] homeCard = new Image[4], homeIcon = new Image[4], homeBuy = new Image[4];
        readonly Text[] homeName = new Text[4], homeLv = new Text[4], homeCost = new Text[4];
        readonly bool[] homeAfford = new bool[4];
        public System.Action<int> onHomeBuy;
        public System.Action onHomePlay, onHomeReset, onHomeLook, onHomeToy, onHomeShops;
        Text homeResetLabel, homeLookLabel, homeToyLabel, homeShopsLabel;
        float homeResetArm = -1f;

        static readonly Color[] HomeColors = { new Color(0.93f, 0.68f, 0.12f), new Color(0.55f, 0.3f, 0.85f), new Color(0.25f, 0.72f, 0.35f), new Color(0.2f, 0.5f, 0.92f) };

        void BuildHome()
        {
            homeRoot = New("home", canvasRt).gameObject;
            RectTransform r = (RectTransform)homeRoot.transform;
            Stretch(r);
            // toute la zone libre de l'écran lance la partie (les cartes passent devant)
            Image tap = Img(r, white, new Color(0f, 0f, 0f, 0f));
            Stretch(tap.rectTransform);
            AddButton(tap.rectTransform, () => onHomePlay?.Invoke());
            Image fade = Img(r, fadeUp, new Color(0.05f, 0.07f, 0.1f, 0.8f));
            Place(fade.rectTransform, Vector2.zero, new Vector2(1f, 0.32f), Vector2.zero, Vector2.zero);

            Image lvBg = Box(r, new Color(0.06f, 0.08f, 0.11f, 0.85f), new Vector2(0.5f, 1f), new Vector2(-60f, -110f), new Vector2(360f, 96f));
            homeLevel = Label(lvBg.rectTransform, "", 50, Color.white, TextAnchor.MiddleCenter, true);
            Stretch(homeLevel.rectTransform);
            Image cBg = Box(r, new Color(0.06f, 0.08f, 0.11f, 0.85f), new Vector2(1f, 1f), new Vector2(-170f, -110f), new Vector2(270f, 84f));
            homeCoins = Label(cBg.rectTransform, "", 42, StarGold, TextAnchor.MiddleCenter, true);
            Stretch(homeCoins.rectTransform);
            // remise à zéro de la partie (deux touchers : le premier demande confirmation)
            Image rs = Button(r, "RESET", new Color(0.2f, 0.22f, 0.27f, 0.85f), new Vector2(0f, 1f), new Vector2(110f, -110f), new Vector2(170f, 76f), 30, () =>
            {
                if (homeResetArm > 0f) { homeResetArm = -1f; homeResetLabel.text = "RESET"; onHomeReset?.Invoke(); }
                else { homeResetArm = 3f; homeResetLabel.text = L("SURE?", "SÛR ?"); }
            }, out homeResetLabel);
            // (boutons LOOK / TOY / SHOPS retirés : réglages retenus C, jouet, vitrines — voir Look.cs)

            homeSwipe = Label(r, L("SWIPE TO PLAY", "GLISSE POUR JOUER"), 68, Color.white, TextAnchor.MiddleCenter, true);
            Shadowed(homeSwipe);
            Place(homeSwipe.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 640f), new Vector2(1000f, 110f));

            for (int i = 0; i < 4; i++)
            {
                int id = i;
                // quatre cartes : Bande, Arme, Armure, Revenus (l'armure, ajoutée après, s'affiche en 3e)
                int slot = i == 2 ? 3 : i == 3 ? 2 : i;
                float x = (slot - 1.5f) * 256f;
                Image card = Box(r, HomeColors[i], new Vector2(0.5f, 0f), new Vector2(x, 330f), new Vector2(242f, 400f));
                homeCard[i] = card;
                RectTransform cr = card.rectTransform;
                Image inner = Box(cr, new Color(0f, 0f, 0f, 0.18f), new Vector2(0.5f, 0.5f), new Vector2(0f, 28f), new Vector2(216f, 236f));
                inner.raycastTarget = false;
                homeName[i] = Label(cr, "", 30, Color.white, TextAnchor.MiddleCenter, true);
                Shadowed(homeName[i]);
                Place(homeName[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(228f, 52f));
                Fit(homeName[i], 20);
                homeIcon[i] = Img(cr, white, Color.white);
                homeIcon[i].preserveAspect = true;
                Place(homeIcon[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 42f), new Vector2(140f, 140f));
                homeLv[i] = Label(cr, "", 24, Color.white, TextAnchor.MiddleCenter, true);
                Shadowed(homeLv[i]);
                Place(homeLv[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -62f), new Vector2(222f, 40f));
                Fit(homeLv[i], 16);
                homeBuy[i] = Box(cr, new Color(0.08f, 0.1f, 0.13f, 0.9f), new Vector2(0.5f, 0f), new Vector2(0f, 54f), new Vector2(210f, 80f));
                homeCost[i] = Label(homeBuy[i].rectTransform, "", 36, StarGold, TextAnchor.MiddleCenter, true);
                Stretch(homeCost[i].rectTransform);
                AddButton(cr, () => onHomeBuy?.Invoke(id));
            }
            homeRoot.SetActive(false);
        }

        /// <summary>Affiche l'accueil. costs[i] &lt; 0 : amélioration au maximum.</summary>
        public void ShowHome(int level, int coins, string[] names, Sprite[] icons, string[] levels, int[] costs)
        {
            HideGameScreens();
            HideMeta();
            homeRoot.SetActive(true);
            LockClicks(0.4f);
            homeLevel.text = L("LEVEL ", "NIVEAU ") + level;
            homeCoins.text = "$ " + coins;
            for (int i = 0; i < 4; i++)
            {
                homeName[i].text = names[i];
                homeIcon[i].sprite = icons[i] != null ? icons[i] : white;
                homeIcon[i].color = icons[i] != null ? Color.white : new Color(1f, 1f, 1f, 0f);
                homeLv[i].text = levels[i];
                bool maxed = costs[i] < 0;
                homeAfford[i] = !maxed && coins >= costs[i];
                homeCost[i].text = maxed ? "MAX" : "$ " + costs[i];
                homeCost[i].color = maxed ? Color.white : homeAfford[i] ? StarGold : new Color(1f, 1f, 1f, 0.45f);
                homeCard[i].color = homeAfford[i] || maxed ? HomeColors[i] : Color.Lerp(HomeColors[i], new Color(0.3f, 0.32f, 0.36f), 0.65f);
            }
        }

        public void HideHome() { if (homeRoot != null) homeRoot.SetActive(false); }

        // ---------------------------------------------------------------- carte de progression (à la Hell Escape)
        // Un quartier = 5 niveaux. Bande sous le badge du niveau : nom du quartier, 5 étapes reliées
        // (faites en or, en cours qui pulse, à venir en gris), et le quartier suivant cadenassé.
        public const int ZoneLen = 5;
        RectTransform zoneRoot;
        Text zoneTitle, zoneNext;
        readonly Image[] zoneNode = new Image[ZoneLen], zoneLink = new Image[ZoneLen - 1];
        readonly Text[] zoneNum = new Text[ZoneLen];
        int zoneCur = -1;
        float zoneAnim = -1f;

        void BuildZone()
        {
            zoneRoot = New("zone", homeRoot.transform);
            // même largeur que la rangée du haut : du bord gauche de RESET au bord droit de la pastille des dollars
            Place(zoneRoot, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-5f, -172f), new Vector2(-60f, 190f), new Vector2(0.5f, 1f));
            Image bg = Img(zoneRoot, round, new Color(0.06f, 0.08f, 0.11f, 0.82f));
            Stretch(bg.rectTransform);
            zoneTitle = Label(zoneRoot, "", 30, Yellow, TextAnchor.MiddleCenter, true);
            Shadowed(zoneTitle);
            Place(zoneTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -26f), new Vector2(-30f, 44f));
            for (int i = 0; i < ZoneLen; i++)
            {
                float x = (i - (ZoneLen - 1) * 0.5f) * 170f;
                if (i < ZoneLen - 1)
                {
                    zoneLink[i] = Img(zoneRoot, white, new Color(0.3f, 0.32f, 0.38f));
                    Place(zoneLink[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x + 85f, -4f), new Vector2(104f, 12f));
                }
            }
            for (int i = 0; i < ZoneLen; i++)
            {
                float x = (i - (ZoneLen - 1) * 0.5f) * 170f;
                zoneNode[i] = Img(zoneRoot, round, new Color(0.3f, 0.32f, 0.38f));
                Place(zoneNode[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -4f), new Vector2(76f, 76f));
                zoneNum[i] = Label(zoneNode[i].rectTransform, "", 32, Color.white, TextAnchor.MiddleCenter, true);
                Shadowed(zoneNum[i]);
                Stretch(zoneNum[i].rectTransform);
            }
            zoneNext = Label(zoneRoot, "", 24, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleCenter, true);
            Place(zoneNext.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 22f), new Vector2(-30f, 36f));
        }

        /// <summary>Progression : level = niveau à jouer (1…), zoneName / nextName = quartier actuel et suivant ; advanced : on vient de gagner un niveau (animation).</summary>
        public void SetZone(int level, string zoneName, string nextName, bool advanced)
        {
            if (zoneRoot == null) BuildZone();
            int z = (level - 1) / ZoneLen, cur = (level - 1) % ZoneLen;
            zoneTitle.text = L("DISTRICT ", "QUARTIER ") + (z + 1) + "  ·  " + zoneName;
            zoneNext.text = nextName != null ? L("Next: DISTRICT ", "Ensuite : QUARTIER ") + (z + 2) + " · " + nextName + L("  (locked)", "  (verrouillé)") : "";
            zoneCur = cur;
            for (int i = 0; i < ZoneLen; i++)
            {
                zoneNum[i].text = (z * ZoneLen + i + 1).ToString();
                zoneNode[i].color = i < cur ? StarGold : i == cur ? new Color(0.95f, 0.3f, 0.25f) : new Color(0.3f, 0.32f, 0.38f);
                zoneNum[i].color = i <= cur ? Color.white : new Color(1f, 1f, 1f, 0.5f);
                zoneNode[i].transform.localScale = Vector3.one;
                if (i < ZoneLen - 1) zoneLink[i].color = i < cur ? StarGold : new Color(0.3f, 0.32f, 0.38f);
            }
            // victoire : l'étape gagnée passe en or, puis la suivante « saute » (début de quartier : toute la bande s'allume)
            zoneAnim = advanced ? 0f : -1f;
        }

        void TickZone(float udt)
        {
            if (zoneRoot == null || zoneCur < 0) return;
            float t = Time.unscaledTime;
            if (zoneAnim >= 0f)
            {
                zoneAnim += udt;
                int prev = zoneCur - 1;
                if (prev >= 0) zoneNode[prev].transform.localScale = Vector3.one * (1f + 0.4f * Mathf.Max(0f, Mathf.Sin(Mathf.Clamp01(zoneAnim / 0.5f) * Mathf.PI)));
                float k = Mathf.Clamp01((zoneAnim - 0.45f) / 0.5f);
                zoneNode[zoneCur].transform.localScale = Vector3.one * (k <= 0f ? 0.6f : 1f + 0.5f * Mathf.Sin(k * Mathf.PI));
                if (zoneAnim > 1f) zoneAnim = -1f;
                return;
            }
            zoneNode[zoneCur].transform.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(t * 5f));
        }

        public void PopHome(int i) { if (homeCard[i] != null) homeCard[i].transform.localScale = Vector3.one * 1.04f; }

        // cartes un peu réduites : même gonflées (achat possible), elles ne se chevauchent plus
        const float CardK = 0.9f;

        void TickHome(float udt)
        {
            if (homeRoot == null || !homeRoot.activeSelf) return;
            if (homeResetArm > 0f) { homeResetArm -= udt; if (homeResetArm <= 0f) homeResetLabel.text = "RESET"; }
            float a = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.2f));
            homeSwipe.color = new Color(1f, 1f, 1f, a);
            homeSwipe.transform.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(Time.unscaledTime * 4.4f));
            TickZone(udt);
            for (int i = 0; i < 4; i++)
                homeCard[i].transform.localScale = homeAfford[i]
                    ? (Vector3.one * BuyPulse(Time.unscaledTime, i * 0.2f) * 0.97f + Vector3.one * 0.03f) * CardK
                    : Vector3.MoveTowards(homeCard[i].transform.localScale, Vector3.one * CardK, udt * 1.5f);
        }
    }
}
