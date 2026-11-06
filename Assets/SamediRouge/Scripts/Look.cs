using UnityEngine;

namespace SamediRouge
{
    /// <summary>
    /// Proportions de la piste (retour de Guillaume : persos trop petits dans une rue trop large).
    /// Quatre réglages à comparer depuis l'accueil (bouton LOOK) : PROTO (d'origine), A rue -30 %,
    /// B persos +25 %, C les deux + caméra rapprochée. Le choix est gardé d'une partie à l'autre.
    /// </summary>
    public static class Look
    {
        public static readonly string[] Names = { "PROTO", "A", "B", "C" };
        const string Key = "sr_look";
        static int preset = -1;

        public static int Preset
        {
            get { if (preset < 0) preset = Mathf.Clamp(PlayerPrefs.GetInt(Key, 3), 0, 3); return preset; }
        }
        /// <summary>Largeur de la rue (couloirs et décor), 1 = d'origine.</summary>
        public static float Width => Preset == 1 || Preset == 3 ? 0.7f : 1f;
        /// <summary>Taille des personnages (bande, héros, adversaires), 1 = d'origine.</summary>
        public static float Size => Preset == 2 || Preset == 3 ? 1.25f : 1f;
        /// <summary>Caméra plus proche et plus basse.</summary>
        public static bool Close => Preset == 3;

        /// <summary>Proportions « jouet » des personnages 3D (grosse tête, mains et pieds forts). Bouton TOY de l'accueil.</summary>
        public static bool Toy
        {
            get { if (toy < 0) toy = PlayerPrefs.GetInt("sr_toy", 1); return toy == 1; }
        }
        static int toy = -1;
        public static void ToggleToy()
        {
            toy = Toy ? 0 : 1;
            PlayerPrefs.SetInt("sr_toy", toy);
            PlayerPrefs.Save();
        }

        /// <summary>Boutiques en lumière douce sur les côtés (au lieu de murs pastel unis). Bouton SHOPS de l'accueil.</summary>
        public static bool SoftShops
        {
            get { if (shops < 0) shops = PlayerPrefs.GetInt("sr_shops", 1); return shops == 1; }
        }
        static int shops = -1;
        public static void ToggleShops()
        {
            shops = SoftShops ? 0 : 1;
            PlayerPrefs.SetInt("sr_shops", shops);
            PlayerPrefs.Save();
        }

        /// <summary>Outil de dev : impose un réglage (commande « look N » de Logs/sr-screen.txt).</summary>
        public static void Set(int p)
        {
            preset = Mathf.Clamp(p, 0, Names.Length - 1);
            PlayerPrefs.SetInt(Key, preset);
        }

        public static void Next()
        {
            preset = (Preset + 1) % Names.Length;
            PlayerPrefs.SetInt(Key, preset);
            PlayerPrefs.Save();
        }
    }
}
