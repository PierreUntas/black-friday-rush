using UnityEngine;

namespace SamediRouge
{
    /// <summary>
    /// Vibrations courtes. Android : impulsion de quelques millisecondes via le Vibrator natif.
    /// iOS : seule la vibration système (forte) existe, réservée aux gros événements.
    /// </summary>
    public static class Haptics
    {
        public static bool Enabled = true;
        static float lastT;

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject vibrator;
        static bool tried;
#endif

        /// <summary>Petit choc (coup donné). ms : durée, strong : autorise la vibration iOS.</summary>
        public static void Pulse(int ms, bool strong = false)
        {
            if (!Enabled) return;
            if (!strong && Time.unscaledTime - lastT < 0.05f) return;
            lastT = Time.unscaledTime;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (!tried)
                {
                    tried = true;
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    {
                        AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                        vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    }
                }
                if (vibrator != null) vibrator.Call("vibrate", (long)ms);
                else if (strong) Handheld.Vibrate();
            }
            catch (System.Exception)
            {
                if (strong) Handheld.Vibrate();
            }
#elif UNITY_IOS && !UNITY_EDITOR
            if (strong) Handheld.Vibrate();
#endif
        }

        /// <summary>Référence à Handheld.Vibrate pour que Unity ajoute la permission VIBRATE au manifeste Android.</summary>
        public static void Heavy()
        {
            if (!Enabled) return;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }
    }
}
