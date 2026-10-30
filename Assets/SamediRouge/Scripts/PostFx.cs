using UnityEngine;
#if SR_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace SamediRouge
{
    /// <summary>
    /// Post-traitement URP (bloom sur néons et gyrophares, tonemapping ACES, étalonnage nuit, vignette)
    /// et flash rouge quand on encaisse. Sans URP, le jeu tourne quand même, sans ces effets.
    /// </summary>
    public static class PostFx
    {
        public static bool Active { get; private set; }
        static float pulse;
#if SR_URP
        static Vignette vignette;
        static ChromaticAberration aberration;
        const float BaseVignette = 0.2f;
#endif

        public static void Setup(Camera cam)
        {
            Active = false;
            pulse = 0f;
#if SR_URP
            if (GraphicsSettings.currentRenderPipeline == null) return;
            MobileQuality();
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.FastApproximateAntialiasing;

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            Bloom bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.55f);
            bloom.threshold.Override(1.05f);
            bloom.scatter.Override(0.72f);
            // bloom allégé : départ au quart de la résolution, moins de passes
            bloom.highQualityFiltering.Override(false);
            bloom.downscale.Override(BloomDownscaleMode.Quarter);
            bloom.maxIterations.Override(4);
            Tonemapping tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.Neutral);
            ColorAdjustments adj = profile.Add<ColorAdjustments>(true);
            adj.postExposure.Override(0.15f);
            adj.contrast.Override(10f);
            adj.saturation.Override(18f);
            WhiteBalance wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(6f);
            wb.tint.Override(0f);
            vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(BaseVignette);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(Color.black);
            aberration = profile.Add<ChromaticAberration>(true);
            aberration.intensity.Override(0f);

            var go = new GameObject("PostFX");
            Volume volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
            Active = true;
#endif
        }

#if SR_URP
        /// <summary>
        /// Réglages de rendu pour téléphone (pas dans l'éditeur, où l'asset modifié serait sauvegardé) :
        /// résolution de rendu réduite, pas de MSAA (le FXAA de la caméra suffit), ombres plus courtes et plus légères,
        /// peu de lumières ponctuelles par objet.
        /// </summary>
        static void MobileQuality()
        {
#if !UNITY_EDITOR
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null) return;
            urp.renderScale = 0.8f;
            urp.msaaSampleCount = 1;
            urp.shadowDistance = 30f;
            urp.mainLightShadowmapResolution = 1024;
            urp.maxAdditionalLightsCount = 2;
#endif
        }
#endif

        public static void Hit(float k) { pulse = Mathf.Max(pulse, k); }

        public static void Tick(float udt)
        {
            if (!Active) return;
            pulse = Mathf.MoveTowards(pulse, 0f, udt * 2.2f);
#if SR_URP
            if (vignette == null) return;
            vignette.color.Override(Color.Lerp(Color.black, new Color(0.75f, 0.05f, 0.03f), pulse));
            vignette.intensity.Override(BaseVignette + 0.22f * pulse);
            aberration.intensity.Override(pulse * 0.9f);
#endif
        }
    }
}
