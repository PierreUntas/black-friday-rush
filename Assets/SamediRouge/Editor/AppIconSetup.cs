using UnityEditor;
using UnityEngine;

namespace SamediRouge.EditorTools
{
    /// <summary>
    /// Icône de l'application : Assets/SamediRouge/Icon/app_icon.png devient l'icône par défaut (toutes plateformes)
    /// à chaque rechargement de l'éditeur, si elle n'est pas déjà en place.
    /// </summary>
    [InitializeOnLoad]
    public static class AppIconSetup
    {
        const string IconPath = "Assets/SamediRouge/Icon/app_icon.png";

        static AppIconSetup() { EditorApplication.delayCall += Apply; }

        [MenuItem("Samedi Rouge/Apply app icon")]
        public static void Apply()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (tex == null) return;
            Texture2D[] cur = PlayerSettings.GetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, IconKind.Any);
            if (cur != null && cur.Length > 0 && cur[0] == tex) return;
            PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { tex }, IconKind.Any);
            Debug.Log("Samedi Rouge: app icon set from " + IconPath);
        }
    }
}
