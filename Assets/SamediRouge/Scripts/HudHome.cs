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
        readonly Image[] homeCard = new Image[3], homeIcon = new Image[3], homeBuy = new Image[3];
        readonly Text[] homeName = new Text[3], homeLv = new Text[3], homeCost = new Text[3];
        readonly bool[] homeAfford = new bool[3];
        public System.Action<int> onHomeBuy;
        public System.Action onHomePlay;

        static readonly Color[] HomeColors = { new Color(0.93f, 0.68f, 0.12f), new Color(0.55f, 0.3f, 0.85f), new Color(0.25f, 0.72f, 0.35f) };

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

            homeSwipe = Label(r, L("SWIPE TO PLAY", "GLISSE POUR JOUER"), 68, Color.white, TextAnchor.MiddleCenter, true);
            Shadowed(homeSwipe);
            Place(homeSwipe.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 640f), new Vector2(1000f, 110f));

            for (int i = 0; i < 3; i++)
            {
                int id = i;
                float x = (i - 1) * 340f;
                Image card = Box(r, HomeColors[i], new Vector2(0.5f, 0f), new Vector2(x, 330f), new Vector2(316f, 420f));
                homeCard[i] = card;
                RectTransform cr = card.rectTransform;
                Image inner = Box(cr, new Color(0f, 0f, 0f, 0.18f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(284f, 250f));
                inner.raycastTarget = false;
                homeName[i] = Label(cr, "", 34, Color.white, TextAnchor.MiddleCenter, true);
                Shadowed(homeName[i]);
                Place(homeName[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(300f, 56f));
                Fit(homeName[i], 20);
                homeIcon[i] = Img(cr, white, Color.white);
                homeIcon[i].preserveAspect = true;
                Place(homeIcon[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(170f, 170f));
                homeLv[i] = Label(cr, "", 28, Color.white, TextAnchor.MiddleCenter, true);
                Shadowed(homeLv[i]);
                Place(homeLv[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(290f, 44f));
                Fit(homeLv[i], 16);
                homeBuy[i] = Box(cr, new Color(0.08f, 0.1f, 0.13f, 0.9f), new Vector2(0.5f, 0f), new Vector2(0f, 56f), new Vector2(270f, 84f));
                homeCost[i] = Label(homeBuy[i].rectTransform, "", 40, StarGold, TextAnchor.MiddleCenter, true);
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
            for (int i = 0; i < 3; i++)
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

        public void PopHome(int i) { if (homeCard[i] != null) homeCard[i].transform.localScale = Vector3.one * 1.12f; }

        void TickHome(float udt)
        {
            if (homeRoot == null || !homeRoot.activeSelf) return;
            float a = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.2f));
            homeSwipe.color = new Color(1f, 1f, 1f, a);
            homeSwipe.transform.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(Time.unscaledTime * 4.4f));
            for (int i = 0; i < 3; i++)
                homeCard[i].transform.localScale = homeAfford[i]
                    ? Vector3.one * BuyPulse(Time.unscaledTime, i * 0.2f) * 0.97f + Vector3.one * 0.03f
                    : Vector3.MoveTowards(homeCard[i].transform.localScale, Vector3.one, udt * 1.5f);
        }
    }
}
