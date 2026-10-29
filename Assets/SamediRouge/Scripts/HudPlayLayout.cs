using UnityEngine;
using UnityEngine.UI;

namespace SamediRouge
{
    /// <summary>
    /// Écran de course épuré : une seule rangée en haut (pause, jauge de vie, dollars), la progression vers le boss
    /// juste dessous avec le compteur d'adversaires, et en bas seulement les équipements actifs, centrés.
    /// Le bandeau d'annonces, le score et la distance ne sont plus affichés (la barre d'étape dit où on en est).
    /// </summary>
    public partial class Hud
    {
        RectTransform pauseRt;
        Text distLabel, scoreLabel;
        readonly bool[] slotShow = new bool[7];

        void CompactPlayLayout()
        {
            if (ticker != null) ticker.transform.parent.gameObject.SetActive(false);
            distT.gameObject.SetActive(false);
            scoreT.gameObject.SetActive(false);
            distLabel.gameObject.SetActive(false);
            scoreLabel.gameObject.SetActive(false);

            // rangée du haut : mêmes pastilles sombres, même hauteur (76), espacées régulièrement
            Color chipCol = new Color(0.06f, 0.08f, 0.11f, 0.82f);
            Place(pauseRt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -24f), new Vector2(76f, 76f), new Vector2(0f, 1f));
            pauseRt.GetComponent<Image>().color = chipCol;
            Place(hpGlow.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(118f, -14f), new Vector2(684f, 96f), new Vector2(0f, 1f));
            Place(hpFrame.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(122f, -24f), new Vector2(676f, 76f), new Vector2(0f, 1f));
            hpFrameBase = chipCol;
            hpFrame.color = chipCol;
            hpLabel.gameObject.SetActive(false);
            Place(starChip.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-26f, -24f), new Vector2(222f, 76f), new Vector2(1f, 1f));
            starChip.color = chipCol;
            Place(starIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(58f, 58f), new Vector2(0f, 0.5f));
            starCount.alignment = TextAnchor.MiddleCenter;
            starCount.fontSize = 40;
            RectTransform sc = starCount.rectTransform;
            sc.anchorMin = Vector2.zero; sc.anchorMax = Vector2.one; sc.pivot = new Vector2(0.5f, 0.5f);
            sc.offsetMin = new Vector2(72f, 0f); sc.offsetMax = new Vector2(-10f, 0f);
            pauseRt.SetAsLastSibling();   // le bouton pause n'est plus jamais caché par le médaillon cœur
            HeartArt();

            // progression de l'étape (pleine largeur, à gauche) + adversaires assommés (à droite)
            Place(stageBg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -126f), new Vector2(770f, 50f), new Vector2(0f, 1f));
            RectTransform kb = (RectTransform)koT.transform.parent;
            Place(kb, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-26f, -124f), new Vector2(240f, 54f), new Vector2(1f, 1f));
            koT.fontSize = 26;
            Place(comboBg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -192f), new Vector2(240f, 52f), new Vector2(0f, 1f));
            RectTransform rb = (RectTransform)recT.transform.parent;
            Place(rb, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-26f, -192f), new Vector2(150f, 52f), new Vector2(1f, 1f));
            // le gros message du centre un peu plus haut (il ne recouvre plus le personnage)
            toastT.rectTransform.anchoredPosition = new Vector2(0f, 420f);

            for (int i = 0; i < slotBg.Length; i++)
            {
                slotBg[i].rectTransform.sizeDelta = new Vector2(116f, 92f);
                slotIcon[i].rectTransform.sizeDelta = new Vector2(66f, 66f);
            }
        }

        /// <summary>Cœur illustré (Art/heart) à la place du cœur dessiné, s'il existe.</summary>
        void HeartArt()
        {
            Sprite h = GearIcons.Art("heart");
            if (h == null) return;
            Image badgeIn = hpHeart.transform.parent.GetComponent<Image>();
            Image badge = badgeIn.transform.parent.GetComponent<Image>();
            badge.color = new Color(1f, 1f, 1f, 0f);
            badgeIn.color = new Color(1f, 1f, 1f, 0f);
            hpHeart.sprite = h;
            hpHeart.preserveAspect = true;
            hpHeart.rectTransform.sizeDelta = new Vector2(60f, 60f);
            hpHeart.rectTransform.anchoredPosition = Vector2.zero;
            badge.rectTransform.anchoredPosition = new Vector2(40f, 0f);
            badge.rectTransform.sizeDelta = new Vector2(60f, 60f);
            RectTransform bg = hpBg.rectTransform;
            bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one;
            bg.offsetMin = new Vector2(80f, 12f); bg.offsetMax = new Vector2(-12f, -12f);
        }

        /// <summary>Barre du bas : seulement les équipements actifs (ou le prochain à ramasser), centrés.</summary>
        void LayoutSlots()
        {
            int n = 0;
            for (int i = 0; i < slotBg.Length; i++) if (slotShow[i]) n++;
            const float w = 116f, gap = 12f;
            float x = 540f - (n * w + (n - 1) * gap) * 0.5f;
            for (int i = 0; i < slotBg.Length; i++)
            {
                bool show = slotShow[i];
                if (slotBg[i].gameObject.activeSelf != show) slotBg[i].gameObject.SetActive(show);
                if (!show) continue;
                slotBg[i].rectTransform.anchoredPosition = new Vector2(x, 30f);
                x += w + gap;
            }
        }
    }
}
