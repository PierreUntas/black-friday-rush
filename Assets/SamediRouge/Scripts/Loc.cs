using UnityEngine;

namespace SamediRouge
{
    /// <summary>
    /// Langue du jeu : français si le téléphone est en français, anglais sinon.
    /// Le joueur peut forcer la langue avec le bouton EN / FR de la carte (choix mémorisé).
    /// Les textes sont écrits par paires : Loc.T("anglais", "français").
    /// Les noms propres (rues, quartiers, boss, STREET CRED) restent en anglais.
    /// </summary>
    public static class Loc
    {
        const string Key = "sr_lang";
        static bool loaded, fr;

        public static bool Fr
        {
            get
            {
                if (!loaded)
                {
                    loaded = true;
                    int saved = PlayerPrefs.GetInt(Key, -1);
                    fr = saved < 0 ? Application.systemLanguage == SystemLanguage.French : saved == 1;
                }
                return fr;
            }
        }

        public static void Set(bool french)
        {
            loaded = true;
            fr = french;
            PlayerPrefs.SetInt(Key, french ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static string T(string en, string frText) => Fr ? frText : en;
    }
}
