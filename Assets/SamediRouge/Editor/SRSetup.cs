using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
#if SR_URP
using UnityEngine.Rendering.Universal;
#endif

namespace SamediRouge.EditorTools
{
    /// <summary>
    /// Configuration automatique au premier lancement :
    /// 1) installe URP (bloom, tonemapping) ; 2) crée l'asset URP et l'active ;
    /// 3) génère les matériaux de base ; 4) crée la scène, l'ajoute au build ; 5) iOS en portrait.
    /// Relançable à la main : menu Samedi Rouge > Configurer le projet.
    /// </summary>
    [InitializeOnLoad]
    public static class SRSetup
    {
        const string Root = "Assets/SamediRouge";
        const string GenDir = Root + "/Generated";
        const string ScenePath = Root + "/Scenes/SamediRouge.unity";
        const string UrpPackage = "com.unity.render-pipelines.universal";

        static AddRequest addRequest;

        static string Key => "SamediRouge.Setup." + Application.dataPath;

        static SRSetup()
        {
            EditorApplication.delayCall += Auto;
        }

        static void Auto()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            int stage = EditorPrefs.GetInt(Key, 0);
#if SR_URP
            if (stage < 4)
            {
                EditorPrefs.SetInt(Key, 4);
                Configure();
            }
#else
            if (stage == 0)
            {
                EditorPrefs.SetInt(Key, 1);
                Configure();
                InstallUrp();
            }
#endif
        }

        [MenuItem("Samedi Rouge/Configurer le projet")]
        public static void Configure()
        {
            Directory.CreateDirectory(GenDir);
            Directory.CreateDirectory(Root + "/Scenes");
            AssetDatabase.Refresh();
#if SR_URP
            SetupUrp();
#endif
            Material lit, emis, part;
            CreateMaterials(out lit, out emis, out part);
            EnsureScene(lit, emis, part);
            SetupPlayer();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Samedi Rouge] Projet configuré (" + (GraphicsSettings.defaultRenderPipeline != null ? "URP" : "Built-in") + "). Ouvre " + ScenePath + " et appuie sur Play.");
        }

        [MenuItem("Samedi Rouge/Ouvrir la scène de jeu")]
        static void OpenScene()
        {
            if (!File.Exists(ScenePath)) Configure();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Samedi Rouge/Installer URP (bloom, éclairage)")]
        static void InstallUrp()
        {
            if (addRequest != null) return;
            Debug.Log("[Samedi Rouge] Installation de l'Universal Render Pipeline… (Unity va recompiler, puis finir la configuration tout seul)");
            addRequest = Client.Add(UrpPackage);
            EditorApplication.update += PollInstall;
        }

        static void PollInstall()
        {
            if (addRequest == null || !addRequest.IsCompleted) return;
            if (addRequest.Status == StatusCode.Success) Debug.Log("[Samedi Rouge] URP installé : " + addRequest.Result.version);
            else Debug.LogWarning("[Samedi Rouge] URP non installé (" + (addRequest.Error != null ? addRequest.Error.message : "erreur inconnue") + "). Le jeu marche quand même, sans bloom.");
            EditorApplication.update -= PollInstall;
            addRequest = null;
        }

#if SR_URP
        static void SetupUrp()
        {
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset) return;

            string rendererPath = GenDir + "/SR_Renderer.asset";
            string assetPath = GenDir + "/SR_URP.asset";
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssignPostProcessData(rendererData);
                AssetDatabase.CreateAsset(rendererData, rendererPath);
            }
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (urp == null)
            {
                urp = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(urp, assetPath);
            }
            urp.shadowDistance = 60f;
            urp.supportsHDR = true;
            urp.msaaSampleCount = 4;
            EditorUtility.SetDirty(urp);

            GraphicsSettings.defaultRenderPipeline = urp;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = null;
            }
            QualitySettings.SetQualityLevel(current, false);
            Debug.Log("[Samedi Rouge] URP activé.");
        }

        /// <summary>Branche les données de post-traitement d'URP (par réflexion, pour rester compatible entre versions).</summary>
        static void AssignPostProcessData(ScriptableObject rendererData)
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            System.Type t = rendererData.GetType();
            System.Reflection.FieldInfo field = t.GetField("postProcessData", flags);
            System.Reflection.PropertyInfo prop = field == null ? t.GetProperty("postProcessData", flags) : null;
            System.Type dataType = field != null ? field.FieldType : prop != null ? prop.PropertyType : null;
            if (dataType == null) return;

            Object data = AssetDatabase.LoadAssetAtPath("Packages/" + UrpPackage + "/Runtime/Data/PostProcessData.asset", dataType);
            if (data == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:" + dataType.Name, new[] { "Packages/" + UrpPackage });
                if (guids.Length > 0) data = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guids[0]), dataType);
            }
            if (data == null) return;
            if (field != null) field.SetValue(rendererData, data);
            else if (prop != null && prop.CanWrite) prop.SetValue(rendererData, data);
        }
#endif

        /// <summary>Matériaux de base de la pipeline courante, sauvegardés en asset (inclus dans les builds).</summary>
        static void CreateMaterials(out Material lit, out Material emis, out Material part)
        {
            RenderPipelineAsset rp = GraphicsSettings.currentRenderPipeline;
            Material baseMat = rp != null ? rp.defaultMaterial : AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");

            var litSrc = new Material(baseMat);
            lit = Save(litSrc, GenDir + "/M_Lit.mat");

            var emSrc = new Material(baseMat);
            emSrc.EnableKeyword("_EMISSION");
            emSrc.SetColor("_EmissionColor", Color.white);
            emSrc.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            emis = Save(emSrc, GenDir + "/M_Emissive.mat");
            emis.EnableKeyword("_EMISSION");

            Shader sprite = Shader.Find("Sprites/Default");
            var pSrc = sprite != null ? new Material(sprite) : new Material(baseMat);
            part = Save(pSrc, GenDir + "/M_Particle.mat");
        }

        static Material Save(Material src, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(src, path);
                return src;
            }
            existing.shader = src.shader;
            existing.CopyPropertiesFromMaterial(src);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        static void EnsureScene(Material lit, Material emis, Material part)
        {
            if (!File.Exists(ScenePath))
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var boot = new GameObject("SamediRouge").AddComponent<SRBootstrap>();
                boot.litMaterial = lit;
                boot.emissiveMaterial = emis;
                boot.particleMaterial = part;
                // brouillard activé dans la scène : sinon Unity retire ses variantes de shader des builds
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            else if (EditorSceneManager.GetActiveScene().path != ScenePath)
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
            }

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void SetupPlayer()
        {
            PlayerSettings.productName = "Black Friday Rush";
            PlayerSettings.companyName = "Pierre Untas";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.pierreuntas.blackfridayguard");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.pierreuntas.blackfridayguard");
        }
    }
}
