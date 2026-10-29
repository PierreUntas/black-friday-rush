using UnityEngine;

namespace SamediRouge
{
    /// <summary>
    /// Point d'entrée : un GameObject avec ce composant suffit, tout le reste est généré.
    /// Si la scène ouverte n'en contient pas, il est créé automatiquement au Play.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class SRBootstrap : MonoBehaviour
    {
        [Tooltip("Rempli par le menu Samedi Rouge > Configurer le projet (évite le rose en build URP)")]
        public Material litMaterial;
        public Material emissiveMaterial;
        public Material particleMaterial;

        static bool started;

        void Awake()
        {
            if (started)
            {
                Destroy(gameObject);
                return;
            }
            started = true;
#if UNITY_EDITOR
            // erreurs de l'éditeur recopiées dans Logs/sr-errors.txt (lisible sans ouvrir la console)
            string logPath = System.IO.Path.Combine(Application.dataPath, "../Logs/sr-errors.txt");
            try { System.IO.File.WriteAllText(logPath, ""); } catch (System.Exception) { }
            Application.logMessageReceived += (msg, stack, type) =>
            {
                if (type != LogType.Error && type != LogType.Exception) return;
                try { System.IO.File.AppendAllText(logPath, type + ": " + msg + "\n" + stack + "\n"); } catch (System.Exception) { }
            };
#endif
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
#if !UNITY_EDITOR
            Screen.orientation = ScreenOrientation.Portrait;
#endif
            if (Loc.Fr) Debug.Log("Samedi Rouge: FR"); // langue lue ici une fois (PlayerPrefs interdit dans les constructeurs)
            Art.Init(litMaterial, emissiveMaterial, particleMaterial);
            var game = new GameObject("Game").AddComponent<Game>();
            game.transform.SetParent(transform, false);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { started = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            if (FindAnyObjectByType<SRBootstrap>() == null)
                new GameObject("SamediRouge").AddComponent<SRBootstrap>();
        }
    }
}
