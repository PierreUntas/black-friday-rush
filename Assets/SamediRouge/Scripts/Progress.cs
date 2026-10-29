using UnityEngine;

namespace SamediRouge
{
    /// <summary>
    /// Sauvegarde locale (PlayerPrefs) : pièces, équipement permanent, rues conquises par camp, tutoriel.
    /// Aucune donnée en ligne.
    /// </summary>
    public static class Progress
    {
        const string KCoins = "sr_bank", KTuto = "sr_tuto_done", KFaction = "sr_faction";

        public static int Coins
        {
            get => PlayerPrefs.GetInt(KCoins, 0);
            set { PlayerPrefs.SetInt(KCoins, Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        public static bool TutorialDone
        {
            get => PlayerPrefs.GetInt(KTuto, 0) == 1;
            set { PlayerPrefs.SetInt(KTuto, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Dernier camp choisi (vrai = voleurs).</summary>
        public static bool Thief
        {
            get => PlayerPrefs.GetInt(KFaction, 0) == 1;
            set { PlayerPrefs.SetInt(KFaction, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        // ---------------------------------------------------------------- équipement (propre à chaque camp)

        static string GearKey(bool thief, int gear) => "sr_gear_" + (thief ? "t" : "g") + "_" + gear;

        /// <summary>
        /// Ancienne sauvegarde : un seul équipement pour les deux camps. On le donne au camp le plus avancé
        /// (les policiers en cas d'égalité) ; l'autre camp repart de zéro.
        /// </summary>
        static void MigrateGear()
        {
            if (PlayerPrefs.GetInt("sr_gear_split", 0) == 1) return;
            PlayerPrefs.SetInt("sr_gear_split", 1);
            bool owner = ConqueredCount(true) > ConqueredCount(false);
            for (int g = 0; g < Tuning.GearCount; g++)
            {
                string old = "sr_gear_" + g;
                if (!PlayerPrefs.HasKey(old)) continue;
                PlayerPrefs.SetInt(GearKey(owner, g), PlayerPrefs.GetInt(old, 0));
                PlayerPrefs.DeleteKey(old);
            }
            PlayerPrefs.Save();
        }

        public static int GearLevel(bool thief, int gear)
        {
            MigrateGear();
            return Mathf.Clamp(PlayerPrefs.GetInt(GearKey(thief, gear), 0), 0, Tuning.MaxLevel);
        }

        public static void SetGearLevel(bool thief, int gear, int level)
        {
            MigrateGear();
            PlayerPrefs.SetInt(GearKey(thief, gear), Mathf.Clamp(level, 0, Tuning.MaxLevel));
            PlayerPrefs.Save();
        }

        /// <summary>Coût du prochain niveau, ou -1 si l'équipement est au maximum.</summary>
        public static int NextCost(bool thief, int gear)
        {
            int lv = GearLevel(thief, gear);
            return lv >= Tuning.MaxLevel ? -1 : Tuning.UpgradeCost(gear, lv);
        }

        public static bool Buy(bool thief, int gear)
        {
            int cost = NextCost(thief, gear);
            if (cost < 0 || Coins < cost) return false;
            Coins -= cost;
            SetGearLevel(thief, gear, GearLevel(thief, gear) + 1);
            return true;
        }

        static string StreetKey(bool thief, int i) => "sr_street_" + (thief ? "t" : "g") + i;

        public static bool Conquered(bool thief, int street) => PlayerPrefs.GetInt(StreetKey(thief, street), 0) == 1;

        public static void SetConquered(bool thief, int street, bool on)
        {
            PlayerPrefs.SetInt(StreetKey(thief, street), on ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static int ConqueredCount(bool thief)
        {
            int n = 0;
            for (int i = 0; i < Tuning.Streets; i++) if (Conquered(thief, i)) n++;
            return n;
        }

        public static int ConqueredIn(bool thief, int map)
        {
            int n = 0;
            for (int i = 0; i < Tuning.MapCount[map]; i++) if (Conquered(thief, Tuning.MapFirst[map] + i)) n++;
            return n;
        }

        /// <summary>Une map est ouverte quand toutes les rues de la précédente sont conquises.</summary>
        public static bool MapUnlocked(bool thief, int map)
        {
            if (map <= 0) return true;
            return ConqueredIn(thief, map - 1) >= Tuning.MapCount[map - 1];
        }

        /// <summary>Prochaine rue à prendre (la première non conquise), ou -1 si tout le quartier est pris.</summary>
        public static int NextStreet(bool thief)
        {
            for (int i = 0; i < Tuning.Streets; i++) if (!Conquered(thief, i)) return i;
            return -1;
        }

        /// <summary>Une rue est jouable si elle est conquise (on la rejoue) ou si c'est la prochaine cible.</summary>
        public static bool Unlocked(bool thief, int street) => Conquered(thief, street) || street == NextStreet(thief);

        /// <summary>Remise à zéro complète (debug).</summary>
        /// <summary>Nombre de tentatives sur une rue (choisit la variante de parcours suivante).</summary>
        public static int Tries(bool thief, int street) => PlayerPrefs.GetInt("sr_try_" + (thief ? "t" : "g") + street, 0);
        public static void AddTry(bool thief, int street) => PlayerPrefs.SetInt("sr_try_" + (thief ? "t" : "g") + street, Tries(thief, street) + 1);

        public static void ResetAll()
        {
            for (int f = 0; f < 2; f++)
                for (int i = 0; i < Tuning.Streets; i++) PlayerPrefs.DeleteKey("sr_try_" + (f == 1 ? "t" : "g") + i);
            for (int f = 0; f < 2; f++)
                for (int i = 0; i < Tuning.Streets; i++) SetConquered(f == 1, i, false);
            for (int f = 0; f < 2; f++)
                for (int g = 0; g < Tuning.GearCount; g++) SetGearLevel(f == 1, g, 0);
            Coins = 0;
            TutorialDone = false;
        }
    }
}
