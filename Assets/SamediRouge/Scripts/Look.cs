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
            // réglage retenu : C (boutons de test retirés de l'accueil). Seule la commande de dev « look N » peut en imposer un autre.
            get { return preset < 0 ? 3 : preset; }
        }
        /// <summary>Largeur de la rue (couloirs et décor), 1 = d'origine.</summary>
        public static float Width => Preset == 1 || Preset == 3 ? 0.7f : 1f;
        /// <summary>Taille des personnages (bande, héros, adversaires), 1 = d'origine.</summary>
        public static float Size => Preset == 2 ? 1.25f : Preset == 3 ? 1.12f : 1f;   // C : +12 % (retour : +25 % c'était un peu grand avec la rue resserrée)
        /// <summary>Caméra plus proche et plus basse.</summary>
        public static bool Close => Preset == 3;

        /// <summary>Proportions « jouet » des personnages 3D (grosse tête, mains et pieds forts). Bouton TOY de l'accueil.</summary>
        public static bool Toy
        {
            get { return toy != 0; }   // retenu : toujours actif (bouton retiré)
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
            get { return shops != 0; }   // retenu : toujours actif (bouton retiré ; sur le téléphone il était resté sur OFF)
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
        }

        public static void Next()
        {
            preset = (Preset + 1) % Names.Length;
            PlayerPrefs.SetInt(Key, preset);
            PlayerPrefs.Save();
        }
    }
}
