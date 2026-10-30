using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace SamediRouge.EditorTools
{
    /// <summary>
    /// Icône de l'application : Assets/SamediRouge/Icon/app_icon.png devient l'icône par défaut (toutes plateformes)
    /// et les icônes Android (adaptatives : fond app_icon_bg + premier plan app_icon_fg)
    /// sont remplies explicitement, sinon le téléphone peut afficher le logo Unity.
    /// Appliqué à chaque rechargement de l'éditeur.
    /// </summary>
    [InitializeOnLoad]
    public static class AppIconSetup
    {
        const string Dir = "Assets/SamediRouge/Icon/";

        static AppIconSetup() { EditorApplication.delayCall += Apply; }

        [MenuItem("Samedi Rouge/Apply app icon")]
        public static void Apply()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "app_icon.png");
            if (tex == null) return;
            var bg = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "app_icon_bg.png") ?? tex;
            var fg = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "app_icon_fg.png");

            Texture2D[] cur = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
            if (cur == null || cur.Length == 0 || cur[0] != tex)
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { tex }, IconKind.Any);

            try
            {
                Fill(AndroidPlatformIconKind.Adaptive, bg, fg);
                AssetDatabase.SaveAssets();
            }
            catch (System.Exception e) { Debug.LogWarning("Samedi Rouge: Android icons not set: " + e.Message); }
        }

        static void Fill(PlatformIconKind kind, Texture2D a, Texture2D b)
        {
            PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            bool changed = false;
            foreach (PlatformIcon icon in icons)
            {
                Texture2D[] t = icon.GetTextures();
                bool ok = t != null && t.Length > 0 && t[0] == a && (icon.maxLayerCount < 2 || (t.Length > 1 && t[1] == b));
                if (ok) continue;
                if (icon.maxLayerCount >= 2 && b != null) icon.SetTextures(a, b);
                else icon.SetTextures(a);
                changed = true;
            }
            if (changed) PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
        }
    }
}
