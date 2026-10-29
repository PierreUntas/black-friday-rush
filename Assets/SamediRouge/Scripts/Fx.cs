using UnityEngine;
using UnityEngine.Rendering;

namespace SamediRouge
{
    /// <summary>Particules générées par code : impacts, poussière, pièces, verre, feu, fumée, lacrymo, neige.</summary>
    public static class Fx
    {
        static ParticleSystem hit, dust, coin, glass, ring, debris, puff, confetti, flash, step, sparkle;
        static Transform root;

        public static void Init(Transform parent)
        {
            root = Art.Node(parent, "FX", Vector3.zero);

            hit = Burst("Hit", Art.HardDot, 0.25f, 0.4f, 5f, 10f, 0.07f, 0.16f, 1.2f, new Color(1f, 0.95f, 0.6f), new Color(1f, 0.7f, 0.2f));
            dust = Burst("Dust", Art.SmokeTex, 0.5f, 0.9f, 0.6f, 2f, 0.4f, 0.9f, -0.05f, new Color(0.6f, 0.6f, 0.62f, 0.55f), new Color(0.45f, 0.45f, 0.48f, 0.4f));
            coin = Burst("Coin", Art.SoftDot, 0.3f, 0.5f, 2f, 4.5f, 0.08f, 0.18f, 0.3f, new Color(1f, 0.92f, 0.45f), new Color(1f, 0.75f, 0.2f));
            glass = Burst("Glass", Art.HardDot, 0.6f, 1.1f, 3f, 7f, 0.04f, 0.1f, 2.2f, new Color(0.75f, 0.9f, 1f), new Color(0.95f, 0.98f, 1f));
            ring = Burst("Ring", Art.SoftDot, 0.35f, 0.55f, 4f, 6f, 0.12f, 0.24f, 0f, new Color(1f, 0.9f, 0.3f), new Color(1f, 1f, 0.8f));
            debris = Burst("Debris", Art.HardDot, 0.5f, 0.9f, 3f, 6f, 0.05f, 0.12f, 2f, new Color(0.45f, 0.33f, 0.22f), new Color(0.3f, 0.3f, 0.32f));
            puff = Burst("Puff", Art.SmokeTex, 0.6f, 1.2f, 1f, 3f, 0.7f, 1.4f, -0.1f, new Color(0.92f, 0.9f, 0.95f, 0.7f), new Color(0.8f, 0.8f, 0.85f, 0.5f));

            confetti = Burst("Confetti", Art.HardDot, 1f, 1.8f, 4f, 8f, 0.08f, 0.16f, 0.8f, new Color(1f, 0.3f, 0.35f), new Color(0.3f, 0.6f, 1f));
            var cmain = confetti.main;
            cmain.startColor = Rainbow();
            var crot = confetti.rotationOverLifetime;
            crot.enabled = true;
            crot.z = new ParticleSystem.MinMaxCurve(-10f, 10f);
            var csh = confetti.shape;
            csh.shapeType = ParticleSystemShapeType.Cone;
            csh.angle = 35f;
            confetti.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            // éclair blanc à l'impact, petite poussière de pas, étincelles dorées
            flash = Burst("Flash", Art.SoftDot, 0.09f, 0.12f, 0f, 0f, 2.4f, 3f, 0f, new Color(1f, 1f, 0.95f, 0.95f), new Color(1f, 0.95f, 0.8f, 0.9f));
            var fsz = flash.sizeOverLifetime;
            fsz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.3f));
            step = Burst("Step", Art.SmokeTex, 0.3f, 0.5f, 0.3f, 1.1f, 0.18f, 0.34f, -0.02f, new Color(0.78f, 0.77f, 0.76f, 0.5f), new Color(0.7f, 0.7f, 0.72f, 0.35f));
            sparkle = Burst("Sparkle", Art.HardDot, 0.25f, 0.45f, 1.5f, 3.5f, 0.04f, 0.09f, -0.2f, new Color(1f, 1f, 0.85f), new Color(1f, 0.85f, 0.35f));

            var rot = glass.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
            var sh = glass.shape;
            sh.radius = 0.4f;
        }

        static ParticleSystem Create(string name, Transform parent, Texture2D tex)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Art.Particle(tex, Color.white);
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        static ParticleSystem Burst(string name, Texture2D tex, float lifeMin, float lifeMax, float speedMin, float speedMax,
                                    float sizeMin, float sizeMax, float gravity, Color c0, Color c1)
        {
            ParticleSystem ps = Create(name, root, tex);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(c0, c1);
            main.gravityModifier = gravity;
            main.maxParticles = 400;
            var em = ps.emission;
            em.enabled = false;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = Fade(Color.white, Color.white);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));
            return ps;
        }

        static Gradient Fade(Color a, Color b, float startAlpha = 1f)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
                      new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(startAlpha, 0.5f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        static void Emit(ParticleSystem ps, Vector3 p, int n)
        {
            if (ps == null) return;
            ps.transform.position = p;
            ps.Emit(n);
        }

        public static void Hit(Vector3 p) { Emit(hit, p, 16); Emit(ring, p, 6); Emit(flash, p, 1); }
        public static void Step(Vector3 p) { Emit(step, p, 2); }
        /// <summary>Impact du coup ninja : éclair, anneau doré, débris, poussière et confettis dorés.</summary>
        public static void Ninja(Vector3 p)
        {
            Emit(flash, p + Vector3.up * 0.8f, 2);
            Emit(ring, p, 40);
            Emit(sparkle, p + Vector3.up * 0.5f, 30);
            Emit(debris, p, 30);
            Emit(dust, p, 14);
            Emit(hit, p + Vector3.up, 24);
        }
        public static void Skid(Vector3 p) { Emit(step, p, 5); }
        public static void Sparkle(Vector3 p) { Emit(sparkle, p, 6); }
        public static void Dust(Vector3 p) { Emit(dust, p, 5); }
        public static void Coin(Vector3 p) { Emit(coin, p, 10); }
        public static void Glass(Vector3 p) { Emit(glass, p, 26); }
        public static void Pickup(Vector3 p) { Emit(ring, p, 20); Emit(coin, p, 12); }
        public static void Debris(Vector3 p) { Emit(debris, p, 22); Emit(dust, p, 6); }
        public static void Puff(Vector3 p) { Emit(puff, p, 8); }
        public static void Confetti(Vector3 p) { Emit(confetti, p, 40); Emit(coin, p, 8); }

        static ParticleSystem.MinMaxGradient Rainbow()
        {
            var g = new Gradient();
            g.SetKeys(new[]
                {
                    new GradientColorKey(new Color(1f, 0.3f, 0.35f), 0f), new GradientColorKey(new Color(1f, 0.85f, 0.2f), 0.33f),
                    new GradientColorKey(new Color(0.3f, 0.85f, 0.45f), 0.66f), new GradientColorKey(new Color(0.3f, 0.6f, 1f), 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            var mg = new ParticleSystem.MinMaxGradient(g);
            mg.mode = ParticleSystemGradientMode.RandomColor;
            return mg;
        }

        // ---------------------------------------------------------------- systèmes en boucle

        /// <summary>Feu (flammes + fumée + lumière qui vacille), attaché à un objet.</summary>
        public static void Fire(Transform parent, Vector3 local, float scale, bool light)
        {
            Transform t = Art.Node(parent, "fire", local);
            t.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            ParticleSystem flames = Create("flames", t, Art.SoftDot);
            var main = flames.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f * scale, 2.6f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f * scale, 0.75f * scale);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.35f), new Color(1f, 0.5f, 0.12f));
            main.maxParticles = 120;
            var em = flames.emission;
            em.rateOverTime = 34f;
            var sh = flames.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 12f;
            sh.radius = 0.3f * scale;
            var col = flames.colorOverLifetime;
            col.enabled = true;
            col.color = Fade(new Color(1f, 0.9f, 0.5f), new Color(0.9f, 0.18f, 0.05f));
            var size = flames.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.15f));
            flames.Play();

            ParticleSystem smoke = Create("smoke", t, Art.SmokeTex);
            var sm = smoke.main;
            sm.loop = true;
            sm.simulationSpace = ParticleSystemSimulationSpace.World;
            sm.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            sm.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            sm.startSize = new ParticleSystem.MinMaxCurve(0.6f * scale, 1.1f * scale);
            sm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.18f, 0.18f, 0.2f, 0.55f), new Color(0.3f, 0.3f, 0.32f, 0.45f));
            sm.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            sm.maxParticles = 60;
            var sem = smoke.emission;
            sem.rateOverTime = 9f;
            var ssh = smoke.shape;
            ssh.shapeType = ParticleSystemShapeType.Cone;
            ssh.angle = 18f;
            ssh.radius = 0.25f * scale;
            var scol = smoke.colorOverLifetime;
            scol.enabled = true;
            scol.color = Fade(Color.white, Color.white, 0.8f);
            var ssize = smoke.sizeOverLifetime;
            ssize.enabled = true;
            ssize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.6f));
            smoke.Play();

            if (light)
            {
                Transform lt = Art.Node(parent, "fireLight", local + new Vector3(0f, 1f, 0f));
                var l = lt.gameObject.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.55f, 0.2f);
                l.range = 7f * scale;
                l.intensity = 2.5f;
                l.shadows = LightShadows.None;
                lt.gameObject.AddComponent<Flicker>().baseIntensity = 2.5f;
            }
        }

        /// <summary>Nuage de lacrymo persistant.</summary>
        public static void Gas(Transform parent, Vector3 local)
        {
            Transform t = Art.Node(parent, "gas", local);
            ParticleSystem ps = Create("gas", t, Art.SmokeTex);
            var main = ps.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.95f, 0.92f, 0.96f, 0.55f), new Color(0.85f, 0.9f, 0.85f, 0.45f));
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.maxParticles = 50;
            var em = ps.emission;
            em.rateOverTime = 16f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.9f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = Fade(Color.white, Color.white, 0.9f);
            ps.Play();
        }

        /// <summary>
        /// Traits de vitesse : des traînées blanches naissent sur le pourtour de l'écran et filent vers la caméra.
        /// Attaché à la caméra (espace local) ; le jeu règle le débit selon la vitesse.
        /// </summary>
        public static ParticleSystem SpeedLines(Transform cam)
        {
            ParticleSystem ps = Create("speedlines", cam, Art.SoftDot);
            ps.transform.localPosition = new Vector3(0f, 0f, 14f);
            ps.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // émet vers la caméra
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.32f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(38f, 52f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.07f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.55f), new Color(1f, 0.97f, 0.9f, 0.3f));
            main.maxParticles = 200;
            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = 0f;
            var sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Cone; // base du cône dans le plan de l'écran, émission droit devant
            sh.angle = 1f;
            sh.radius = 7.2f;
            sh.radiusThickness = 0.12f;
            sh.scale = new Vector3(0.62f, 1f, 1f); // écran en portrait : ellipse haute
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = Fade(Color.white, Color.white);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.09f;
            r.lengthScale = 2f;
            ps.Play();
            return ps;
        }

        /// <summary>Neige qui tombe autour de la caméra (accentue la vitesse).</summary>
        public static ParticleSystem Snow(Transform parent)
        {
            ParticleSystem ps = Create("snow", parent, Art.SoftDot);
            var main = ps.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 5f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.75f), new Color(0.85f, 0.9f, 1f, 0.5f));
            main.gravityModifier = 0.1f;
            main.maxParticles = 1500;
            var em = ps.emission;
            em.rateOverTime = 260f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(22f, 1f, 70f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.4f;
            ps.Play();
            return ps;
        }
    }

    /// <summary>Fait tourner un objet sur lui-même (faisceau de gyrophare).</summary>
    public class Spinner : MonoBehaviour
    {
        public float speed = 360f;
        void Update() { transform.Rotate(0f, speed * Time.deltaTime, 0f, Space.Self); }
    }

    /// <summary>Lumière de feu qui vacille.</summary>
    public class Flicker : MonoBehaviour
    {
        public float baseIntensity = 2f;
        Light l;
        float seed;

        void Start()
        {
            l = GetComponent<Light>();
            seed = Random.value * 100f;
        }

        void Update()
        {
            if (l != null) l.intensity = baseIntensity * (0.7f + 0.6f * Mathf.PerlinNoise(seed, Time.time * 9f));
        }
    }
}
