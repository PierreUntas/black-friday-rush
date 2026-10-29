using UnityEngine;

namespace SamediRouge
{
    /// <summary>Bruitages synthétisés au démarrage : aucun fichier audio à importer.</summary>
    public class Snd : MonoBehaviour
    {
        public static Snd I { get; private set; }
        public AudioClip whoosh, bonk, coin, hurt, pickup, jump, land, glass, tick, crash, siren, shot, perfect;

        AudioSource[] pool;
        int next;
        uint seed = 22222u;
        const int Rate = 44100;

        void Awake()
        {
            I = this;
            pool = new AudioSource[8];
            for (int i = 0; i < pool.Length; i++)
            {
                pool[i] = gameObject.AddComponent<AudioSource>();
                pool[i].playOnAwake = false;
                pool[i].spatialBlend = 0f;
            }
            Build();
        }

        void OnDestroy() { if (I == this) I = null; }

        public static void Play(AudioClip c, float vol = 1f, float pitchVar = 0.08f)
        {
            if (I == null || c == null) return;
            AudioSource s = I.pool[I.next];
            I.next = (I.next + 1) % I.pool.Length;
            s.pitch = 1f + Random.Range(-pitchVar, pitchVar);
            s.PlayOneShot(c, vol);
        }

        public static void PlayPitch(AudioClip c, float vol, float pitch)
        {
            if (I == null || c == null) return;
            AudioSource s = I.pool[I.next];
            I.next = (I.next + 1) % I.pool.Length;
            s.pitch = pitch;
            s.PlayOneShot(c, vol);
        }

        float Noise()
        {
            seed ^= seed << 13;
            seed ^= seed >> 17;
            seed ^= seed << 5;
            return seed / 4294967295f * 2f - 1f;
        }

        AudioClip Make(string name, float dur, System.Func<float, float> f)
        {
            int n = Mathf.CeilToInt(Rate * dur);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate), -1f, 1f);
            AudioClip clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void Build()
        {
            const float TwoPi = Mathf.PI * 2f;

            float lp = 0f;
            whoosh = Make("whoosh", 0.22f, t =>
            {
                float u = t / 0.22f;
                float env = Mathf.Sin(Mathf.PI * u);
                env *= env;
                lp += (0.04f + 0.3f * u) * (Noise() - lp);
                return lp * env * 1.6f;
            });

            float ph = 0f;
            bonk = Make("bonk", 0.26f, t =>
            {
                float f = 190f * Mathf.Exp(-t * 20f) + 65f;
                ph += TwoPi * f / Rate;
                return Mathf.Sin(ph) * Mathf.Exp(-t * 14f) * 0.95f + Noise() * Mathf.Exp(-t * 70f) * 0.6f;
            });

            float ph2 = 0f;
            coin = Make("coin", 0.18f, t =>
            {
                float f = t < 0.06f ? 1320f : 1760f;
                ph2 += TwoPi * f / Rate;
                return (Mathf.Sin(ph2) + 0.25f * Mathf.Sin(ph2 * 3f)) * Mathf.Exp(-t * 14f) * 0.4f;
            });

            float ph3 = 0f;
            hurt = Make("hurt", 0.4f, t =>
            {
                float f = 120f * Mathf.Exp(-t * 4f) + 40f;
                ph3 += TwoPi * f / Rate;
                return Mathf.Sin(ph3) * Mathf.Exp(-t * 7f) * 0.9f + Noise() * Mathf.Exp(-t * 22f) * 0.45f;
            });

            float ph4 = 0f;
            pickup = Make("pickup", 0.32f, t =>
            {
                float f = t < 0.1f ? 660f : t < 0.2f ? 880f : 1320f;
                ph4 += TwoPi * f / Rate;
                float local = t % 0.1f;
                return Mathf.Sin(ph4) * Mathf.Exp(-local * 18f) * 0.4f;
            });

            float ph5 = 0f;
            jump = Make("jump", 0.15f, t =>
            {
                float u = t / 0.15f;
                ph5 += TwoPi * (260f + 700f * u) / Rate;
                return Mathf.Sin(ph5) * (1f - u) * 0.28f;
            });

            float lp2 = 0f;
            land = Make("land", 0.14f, t =>
            {
                lp2 += 0.08f * (Noise() - lp2);
                return lp2 * Mathf.Exp(-t * 30f) * 2.2f;
            });

            float g1 = 0f, g2 = 0f, g3 = 0f;
            glass = Make("glass", 0.45f, t =>
            {
                g1 += TwoPi * 3100f / Rate; g2 += TwoPi * 4270f / Rate; g3 += TwoPi * 5530f / Rate;
                float tone = (Mathf.Sin(g1) + Mathf.Sin(g2) * 0.7f + Mathf.Sin(g3) * 0.5f) * Mathf.Exp(-t * 9f) * 0.18f;
                return tone + Noise() * Mathf.Exp(-t * 25f) * 0.35f;
            });

            tick = Make("tick", 0.05f, t => Noise() * Mathf.Exp(-t * 120f) * 0.5f);

            float lp3 = 0f, ph6 = 0f;
            crash = Make("crash", 0.5f, t =>
            {
                lp3 += 0.15f * (Noise() - lp3);
                ph6 += TwoPi * 55f / Rate;
                return (lp3 * 1.4f + Mathf.Sin(ph6) * 0.5f) * Mathf.Exp(-t * 6f);
            });

            // tir du pistolet à impulsion : « pew » électrique qui descend
            float ps1 = 0f;
            shot = Make("shot", 0.2f, t =>
            {
                float f = 1500f * Mathf.Exp(-t * 14f) + 220f;
                ps1 += TwoPi * f / Rate;
                float sq = Mathf.Sin(ps1) > 0f ? 1f : -1f;
                return (sq * 0.22f + Mathf.Sin(ps1 * 0.5f) * 0.2f) * Mathf.Exp(-t * 12f) + Noise() * Mathf.Exp(-t * 90f) * 0.4f;
            });

            // super coup ninja : boum grave + accord brillant qui monte
            float pb = 0f, pc1 = 0f, pc2 = 0f, pc3 = 0f;
            perfect = Make("perfect", 0.9f, t =>
            {
                float f = 90f * Mathf.Exp(-t * 6f) + 40f;
                pb += TwoPi * f / Rate;
                float boom = Mathf.Sin(pb) * Mathf.Exp(-t * 5f) * 0.8f + Noise() * Mathf.Exp(-t * 30f) * 0.5f;
                float rise = 1f + t * 0.6f;
                pc1 += TwoPi * 880f * rise / Rate; pc2 += TwoPi * 1108.7f * rise / Rate; pc3 += TwoPi * 1318.5f * rise / Rate;
                float env = Mathf.Min(1f, t * 20f) * Mathf.Exp(-t * 3.2f);
                float shine = (Mathf.Sin(pc1) + Mathf.Sin(pc2) * 0.8f + Mathf.Sin(pc3) * 0.6f) * 0.12f * env;
                return boom + shine;
            });

            // carillon d'annonce du centre commercial
            float c1 = 0f, c2 = 0f;
            siren = Make("chime", 1.2f, t =>
            {
                bool second = t >= 0.4f;
                float lt = second ? t - 0.4f : t;
                c1 += 2f * Mathf.PI * (second ? 659.25f : 830.6f) / Rate;
                c2 += 2f * Mathf.PI * (second ? 1318.5f : 1661.2f) / Rate;
                return (Mathf.Sin(c1) * 0.35f + Mathf.Sin(c2) * 0.08f) * Mathf.Exp(-lt * 3.5f) * Mathf.Min(1f, lt * 200f);
            });
        }
    }
}
