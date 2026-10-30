using UnityEngine;
using UnityEngine.UI;

namespace SamediRouge
{
    /// <summary>
    /// Écran titre au lancement : illustration plein écran (title_bg), logo (logo) et « TOUCHE POUR JOUER » qui pulse.
    /// Un tap le ferme et découvre la carte. Rien n'est affiché si les illustrations manquent.
    /// </summary>
    public partial class Hud
    {
        GameObject titleRoot;
        RectTransform titleLogo;
        Text titleTap;
        float titleT;
        System.Action titleDone;

        /// <summary>Affiche l'écran titre ; renvoie faux s'il n'y a pas d'illustration.</summary>
        public bool ShowTitle(System.Action onDone)
        {
            Sprite bg = GearIcons.Art("title_bg"), logo = GearIcons.Art("logo");
            if (bg == null || logo == null) return false;
            titleDone = onDone;
            if (titleRoot == null)
            {
                RectTransform root = New("title", canvasRt);
                Stretch(root);
                titleRoot = root.gameObject;
                Image fill = Img(root, white, new Color(0.04f, 0.05f, 0.08f, 1f));
                Stretch(fill.rectTransform);
                // fond « cover » : remplit l'écran en gardant les proportions
                float canvasH = 1080f * Screen.height / Mathf.Max(1f, Screen.width);
                float k = Mathf.Max(1080f / bg.rect.width, canvasH / bg.rect.height);
                Image b = Img(root, bg, Color.white);
                Place(b.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(bg.rect.width * k, bg.rect.height * k));
                Image l = Img(root, logo, Color.white);
                float lw = 1000f, lh = lw * logo.rect.height / logo.rect.width;
                Place(l.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f - lh * 0.5f), new Vector2(lw, lh));
                titleLogo = l.rectTransform;
                titleTap = Label(root, L("TAP TO PLAY", "TOUCHE POUR JOUER"), 64, Color.white, TextAnchor.MiddleCenter, true);
                Shadowed(titleTap);
                Place(titleTap.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 260f), new Vector2(1000f, 120f));
                AddButton(root, CloseTitle);
            }
            titleRoot.transform.SetAsLastSibling();
            titleRoot.SetActive(true);
            titleT = 0f;
            LockClicks(0.6f);
            return true;
        }

        void CloseTitle()
        {
            if (titleRoot == null || !titleRoot.activeSelf) return;
            titleRoot.SetActive(false);
            LockClicks(0.3f);
            titleDone?.Invoke();
        }

        /// <summary>Outil de dev : ferme l'écran titre sans attendre de tap.</summary>
        public void HideTitleNow() { if (titleRoot != null) titleRoot.SetActive(false); }

        public bool TitleOpen => titleRoot != null && titleRoot.activeSelf;

        /// <summary>Animation : le logo arrive en rebondissant, le texte pulse.</summary>
        void TickTitle(float udt)
        {
            if (!TitleOpen) return;
            titleT += udt;
            float t = Mathf.Clamp01(titleT / 0.6f);
            float pop = t < 1f ? 1f + Mathf.Sin(t * Mathf.PI) * 0.12f : 1f + Mathf.Sin(titleT * 2.2f) * 0.015f;
            titleLogo.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, t) * pop;
            float a = 0.55f + 0.45f * Mathf.Sin(titleT * 4f);
            titleTap.color = new Color(1f, 1f, 1f, titleT < 0.8f ? 0f : a);
        }
    }
}
