using System.Collections.Generic;
using UnityEngine;

namespace SamediRouge
{
    public enum Kind { Guard, Thief, Boss, Shopper }
    public enum Anim { Idle, Run, Jump, Slide, Cheer, Smash, Stand, Down, Walk, Charge }

    /// <summary>
    /// Personnage low-poly articulé (hanches, colonne, cou, épaules, coudes, hanches, genoux)
    /// animé par code : course, saut, glissade, coup de matraque, lancer, chute en physique.
    /// </summary>
    public class Rig
    {
        public Kind kind;
        public Transform root, body, hips, spine, neck, shL, shR, elL, elR, hipL, hipR, knL, knR, handR, handL;
        public Transform baton, shield, gilet, helmet, pistol, holster, smg, cap, stars, sign, pave, bar;
        public TrailRenderer trail;
        public Anim anim = Anim.Run;
        public float speed = 10f;
        public float swingT = -1f, throwT = -1f, shootT = -1f;
        /// <summary>Style du prochain coup : 0 coup droit, 1 revers, 2 coup par-dessus la tête.</summary>
        public int swingStyle;
        /// <summary>Variante de casseur membre de ta bande (batte, brassard rouge).</summary>
        public const int Crew = 100;
        /// <summary>Variante de boss : BossLook + numéro de tenue (0-5 boss voleurs, 6-11 boss vigiles).</summary>
        public const int BossLook = 200;
        /// <summary>Taille des personnages (plus petits pour que les groupes tiennent à l'écran).</summary>
        public static float Scale = 0.8f;
        bool aiming;
        float recoil, spinT = -1f;

        /// <summary>Vrille complète du coup ninja.</summary>
        public void Spin() { spinT = 0f; }
        public bool ko;
        public Rigidbody rb;
        /// <summary>Inclinaison dans les virages (degrés, + = vers la droite) et vitesse verticale, fournies par le jeu.</summary>
        public float lean, vy;
        /// <summary>Passe à vrai quand un pied touche le sol (poussière de pas).</summary>
        public bool stepped;

        float phase, seed, t, squash, stretch, rollT = -1f, koT, blinkT, flashT, prevS;
        MeshRenderer[] renderers, skin;
        TrailRenderer batonTrail, fistTrail;
        // équipement permanent par niveau : casque (visière, crête dorée), tenue renforcée, trois matraques
        Transform helmetVisor, helmetCrest, vestPerm, padL, padR, vestTrim;
        readonly Transform[] batons = new Transform[3];
        Transform heavyBaton, heavyLv2, heavyLv3;
        TrailRenderer heavyTrail;
        /// <summary>Matraque renforcée (map 2) : coups plus amples et plus lourds.</summary>
        public bool heavy;
        /// <summary>TEST : remplace le corps du policier joué par le modèle 3D Resources/SamediRouge/Characters/policier.</summary>
        public static bool Use3DHero = true;
        /// <summary>TEST : pillards (et bande du voleur) en personnages 3D.</summary>
        public static bool Use3DEnemies = true;
        /// <summary>Modèle 3D propre à chaque tenue de boss (Resources/SamediRouge/Characters/…), dans l'ordre des tenues.</summary>
        static readonly string[] BossModels =
        {
            "boss_voleur", "boss_brute", "boss_chef_bande", "boss_pere_noel", "boss_roller", "boss_ours",
            "boss_chef_securite", "boss_videur", "boss_flic", "boss_directeur", "boss_gyropode", "boss_chien",
            "boss_voleur", "boss_flic"
        };
        Skin3D skin3d;
        /// <summary>Orientation de la mitraillette tenue à la main par un modèle 3D, par rapport au buste.</summary>
        /// <summary>Position de la poignée de la mitraillette dans son propre repère (unités du personnage).</summary>
        public static Vector3 SmgGrip = new Vector3(0f, -0.1f, -0.06f);
        public static Quaternion SmgTilt = Quaternion.Euler(40f, 0f, 0f);   // canon vers l'avant, un peu baissé (prêt à tirer)
        /// <summary>Héros 3D : nom de base du modèle ; « &lt;base&gt;_niv1..3 » = le même personnage portant sa tenue achetée.</summary>
        string outfitBase;
        readonly TrailRenderer[] batonTrails = new TrailRenderer[3];
        readonly List<Transform> armor = new List<Transform>();
        readonly List<Transform> eyes = new List<Transform>(), whites = new List<Transform>();
        MaterialPropertyBlock mpb;
        bool visible = true;
        const float HipY = 1.0f;

        // ------------------------------------------------------------------ construction

        /// <summary>Contour coloré à appliquer aux prochains personnages construits (rouge ennemi, bleu allié) ; null = noir.</summary>
        public static Material OutlineOverride;

        public static Rig Build(Kind k, Transform parent, Vector3 pos, float yaw, int variant = 0, bool outline = true, int armorLevel = 0)
        {
            double pt0 = Perf.Begin();
            Rig r = BuildIn(k, parent, pos, yaw, variant, outline, armorLevel);
            Perf.End("·build", pt0);
            return r;
        }

        static Rig BuildIn(Kind k, Transform parent, Vector3 pos, float yaw, int variant, bool outline, int armorLevel)
        {
            double q0 = Perf.Begin();
            var r = new Rig { kind = k };
            r.phase = Random.value * 6f;
            r.seed = Random.value * 6f;
            r.blinkT = Random.Range(1f, 4f);
            r.root = Art.Node(parent, k.ToString(), pos);
            r.root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            r.body = Art.Node(r.root, "body", Vector3.zero);
            r.root.localScale = Vector3.one * Scale * (k == Kind.Boss ? 1.22f : 1f);

            Color skin = Pal.Skin();
            Material mSkin = Art.Mat(skin, 0.35f);
            Material torso, sleeve, pants, shoes, gloves;
            switch (k)
            {
                case Kind.Guard:
                    // le policier joué : uniforme bleu vif (comme les icônes) ; les policiers ennemis restent marine
                    torso = Art.Mat(variant < 0 ? new Color(0.16f, 0.32f, 0.78f) : new Color(0.12f, 0.16f, 0.3f), 0.35f);
                    sleeve = torso;
                    pants = Art.Mat(variant < 0 ? new Color(0.09f, 0.12f, 0.25f) : new Color(0.08f, 0.09f, 0.12f), 0.25f);
                    shoes = Art.Mat(Pal.Black, 0.6f);
                    gloves = mSkin;
                    break;
                case Kind.Shopper:
                {
                    Color[] coats =
                    {
                        new Color(0.95f, 0.45f, 0.2f), new Color(0.3f, 0.75f, 0.85f), new Color(0.9f, 0.35f, 0.6f),
                        new Color(0.45f, 0.8f, 0.35f), new Color(0.98f, 0.85f, 0.3f), new Color(0.6f, 0.45f, 0.9f)
                    };
                    torso = Art.Mat(coats[((variant % coats.Length) + coats.Length) % coats.Length], 0.25f);
                    sleeve = torso;
                    pants = Art.Mat(Pal.Pants(), 0.2f);
                    shoes = Art.Mat(new Color(0.92f, 0.92f, 0.9f), 0.4f);
                    gloves = mSkin;
                    break;
                }
                case Kind.Boss:
                    torso = Art.Mat(new Color(0.07f, 0.07f, 0.08f), 0.3f);
                    sleeve = torso;
                    pants = Art.Mat(new Color(0.09f, 0.09f, 0.1f), 0.3f);
                    shoes = variant >= BossLook ? Art.Mat(new Color(0.08f, 0.08f, 0.09f), 0.5f) : Art.Mat(new Color(0.85f, 0.65f, 0.2f), 0.8f, 0.6f);
                    gloves = Art.Mat(Pal.Black, 0.3f);
                    break;
                default:
                    // casseur tout en noir : sweat, pantalon, chaussures, gants
                    torso = Art.Mat(new Color(0.07f, 0.07f, 0.08f), 0.22f);
                    if (variant < 0 && k == Kind.Thief) torso = Art.Mat(new Color(0.88f, 0.16f, 0.13f), 0.3f);   // le voleur joué : sweat rouge
                    if (variant >= 0 && variant != Crew)
                    {
                        // pillard adverse : blouson / sweat rouge (rouge = ennemi), cagoule noire conservée
                        Color[] reds = { new Color(0.62f, 0.07f, 0.07f), new Color(0.78f, 0.12f, 0.09f), new Color(0.48f, 0.05f, 0.06f), new Color(0.82f, 0.22f, 0.1f) };
                        torso = Art.Mat(reds[(variant / 2 + variant) % reds.Length], 0.3f);
                    }
                    sleeve = torso;
                    pants = Art.Mat(variant < 0 && k == Kind.Thief ? new Color(0.17f, 0.2f, 0.3f) : new Color(0.09f, 0.09f, 0.1f), 0.2f);
                    shoes = Art.Mat(variant < 0 && k == Kind.Thief ? new Color(0.95f, 0.95f, 0.93f) : new Color(0.05f, 0.05f, 0.06f), 0.5f);
                    gloves = Art.Mat(new Color(0.04f, 0.04f, 0.05f), 0.3f);
                    break;
            }

            r.hips = Art.Node(r.body, "hips", new Vector3(0f, HipY, 0f));
            Art.Part(r.hips, Art.Cube, pants, new Vector3(0f, -0.03f, 0f), new Vector3(0.36f, 0.22f, 0.24f));
            r.spine = Art.Node(r.hips, "spine", new Vector3(0f, 0.08f, 0f));
            Art.Part(r.spine, Art.Cube, torso, new Vector3(0f, 0.25f, 0f), new Vector3(0.44f, 0.44f, 0.26f));
            Art.Part(r.spine, Art.Sphere, torso, new Vector3(0f, 0.44f, 0f), new Vector3(0.52f, 0.2f, 0.28f));
            Art.Part(r.spine, Art.Cube, torso, new Vector3(0f, 0.05f, 0f), new Vector3(0.38f, 0.16f, 0.24f));
            r.neck = Art.Node(r.spine, "neck", new Vector3(0f, 0.5f, 0f));
            Art.Part(r.neck, Art.Cylinder, mSkin, new Vector3(0f, 0.03f, 0f), new Vector3(0.12f, 0.05f, 0.12f));
            Art.Part(r.neck, Art.Sphere, mSkin, new Vector3(0f, 0.19f, 0.01f), new Vector3(0.3f, 0.32f, 0.3f));
            // yeux : lisibilité du visage à petite taille
            Material eye = Art.Mat(new Color(0.05f, 0.05f, 0.06f), 0.6f);
            Material eyeWhite = Art.Mat(new Color(0.97f, 0.97f, 0.95f), 0.5f);
            for (int i = -1; i <= 1; i += 2)
            {
                r.whites.Add(Art.Part(r.neck, Art.Sphere, eyeWhite, new Vector3(i * 0.065f, 0.215f, 0.148f), new Vector3(0.075f, 0.085f, 0.04f), default, false).transform);
                r.eyes.Add(Art.Part(r.neck, Art.Sphere, eye, new Vector3(i * 0.063f, 0.215f, 0.168f), new Vector3(0.042f, 0.052f, 0.02f), default, false).transform);
            }
            // nez et oreilles : volumes du visage
            Art.Part(r.neck, Art.Sphere, mSkin, new Vector3(0f, 0.17f, 0.15f), new Vector3(0.07f, 0.08f, 0.07f), default, false);
            Art.Part(r.neck, Art.Sphere, mSkin, new Vector3(-0.15f, 0.19f, 0f), new Vector3(0.05f, 0.08f, 0.06f), default, false);
            Art.Part(r.neck, Art.Sphere, mSkin, new Vector3(0.15f, 0.19f, 0f), new Vector3(0.05f, 0.08f, 0.06f), default, false);

            // bras
            r.shL = Art.Node(r.spine, "shL", new Vector3(-0.29f, 0.43f, 0f));
            r.shR = Art.Node(r.spine, "shR", new Vector3(0.29f, 0.43f, 0f));
            r.elL = Limb(r.shL, sleeve, 0.3f, 0.13f);
            r.elR = Limb(r.shR, sleeve, 0.3f, 0.13f);
            Art.Part(r.elL, Art.Capsule, k == Kind.Guard ? mSkin : sleeve, new Vector3(0f, -0.14f, 0f), new Vector3(0.11f, 0.14f, 0.11f));
            Art.Part(r.elR, Art.Capsule, k == Kind.Guard ? mSkin : sleeve, new Vector3(0f, -0.14f, 0f), new Vector3(0.11f, 0.14f, 0.11f));
            r.handL = Art.Node(r.elL, "handL", new Vector3(0f, -0.3f, 0f));
            r.handR = Art.Node(r.elR, "handR", new Vector3(0f, -0.3f, 0f));
            Art.Part(r.handL, Art.Sphere, gloves, Vector3.zero, new Vector3(0.11f, 0.12f, 0.11f));
            Art.Part(r.handR, Art.Sphere, gloves, Vector3.zero, new Vector3(0.11f, 0.12f, 0.11f));

            // jambes
            r.hipL = Art.Node(r.hips, "hipL", new Vector3(-0.11f, -0.06f, 0f));
            r.hipR = Art.Node(r.hips, "hipR", new Vector3(0.11f, -0.06f, 0f));
            r.knL = Limb(r.hipL, pants, 0.44f, 0.17f);
            r.knR = Limb(r.hipR, pants, 0.44f, 0.17f);
            Art.Part(r.knL, Art.Capsule, pants, new Vector3(0f, -0.2f, 0f), new Vector3(0.14f, 0.21f, 0.14f));
            Art.Part(r.knR, Art.Capsule, pants, new Vector3(0f, -0.2f, 0f), new Vector3(0.14f, 0.21f, 0.14f));
            Art.Part(r.knL, Art.Cube, shoes, new Vector3(0f, -0.45f, 0.05f), new Vector3(0.15f, 0.1f, 0.28f));
            Art.Part(r.knR, Art.Cube, shoes, new Vector3(0f, -0.45f, 0.05f), new Vector3(0.15f, 0.1f, 0.28f));

            switch (k)
            {
                case Kind.Guard: DressGuard(r, variant < 0); break;
                case Kind.Thief: DressThief(r, variant, false); break;
                case Kind.Boss:
                    if (variant >= BossLook) DressBoss(r, variant - BossLook);
                    else DressThief(r, variant, true);
                    break;
                case Kind.Shopper: DressShopper(r, variant); break;
            }

            if (armorLevel > 0) AddArmor(r, armorLevel);
            if (variant < 0 && (k == Kind.Guard || k == Kind.Thief)) HeroStyle(r, k == Kind.Thief);

            // étoiles des K.-O.
            r.stars = Art.Node(r.root, "stars", new Vector3(0f, 2.15f, 0f));
            Material star = Art.Glow(Pal.Jaune, Pal.Jaune * 2.2f, 0.4f);
            for (int i = 0; i < 3; i++)
            {
                float a = i / 3f * Mathf.PI * 2f;
                Art.Part(r.stars, Art.Sphere, star, new Vector3(Mathf.Sin(a) * 0.38f, 0f, Mathf.Cos(a) * 0.38f), Vector3.one * 0.13f, default, false);
            }
            r.stars.gameObject.SetActive(false);

            Perf.End("··body", q0); q0 = Perf.Begin();
            r.skin = r.root.GetComponentsInChildren<MeshRenderer>(true);
            if (outline) Art.Outline(r.root, OutlineOverride != null ? 0.032f : k == Kind.Shopper ? 0.018f : 0.022f, OutlineOverride);
            r.renderers = r.root.GetComponentsInChildren<MeshRenderer>(true);
            Perf.End("··outline", q0); q0 = Perf.Begin();
            // TEST : policier joué en personnage 3D complet (Meshy), animé par ce squelette
            if (Use3DHero && (k == Kind.Guard || k == Kind.Thief) && variant < 0)
            {
                r.outfitBase = k == Kind.Thief ? "voleur" : "policier";
                r.skin3d = Skin3D.Attach(r, r.outfitBase);
            }
            // TEST : pillards adverses en 3D (un seul modèle partagé), ta bande de voleurs avec le modèle du héros
            else if (Use3DEnemies && k == Kind.Thief && variant >= 0) r.skin3d = Skin3D.Attach(r, variant == Crew ? (Skin3D.Available("voleur_lo") ? "voleur_lo" : "voleur") : "pillard");
            // policiers adverses et recrues du policier : modèle « flic » (uniforme marine)
            else if (Use3DEnemies && k == Kind.Guard && variant >= 0) r.skin3d = Skin3D.Attach(r, "flic");
            // boss : un modèle costaud par camp (tenues 6-11 et 13 = boss policiers, les autres = boss voleurs)
            else if (Use3DEnemies && k == Kind.Boss)
            {
                int look = variant >= BossLook ? variant - BossLook : -1;
                bool copBoss = look >= 6 && look != 12;
                // modèle propre au boss s'il existe (boss_brute, boss_pere_noel…), sinon le modèle costaud de son camp
                string own = look >= 0 && look < BossModels.Length ? BossModels[look] : null;
                if (own != null && Skin3D.Available(own)) r.skin3d = Skin3D.Attach(r, own);
                else r.skin3d = Skin3D.Attach(r, copBoss ? "boss_flic" : "boss_voleur");
            }
            // passants : deux modèles (homme / femme) répartis selon la variante
            else if (Use3DEnemies && k == Kind.Shopper) r.skin3d = Skin3D.Attach(r, (variant & 1) == 0 ? "passant_1" : "passant_2");
            // seuls le héros et le boss gardent ombre portée et boîte englobante recalculée
            if (r.skin3d != null && k != Kind.Boss && !(variant < 0 && (k == Kind.Guard || k == Kind.Thief))) r.skin3d.SetSecondary();
            Perf.End("··skin3d", q0); q0 = Perf.Begin();
            if (r.skin3d != null) ArmorModels(r, k == Kind.Guard);
            Perf.End("··armor", q0);
            // personnage 3D : la mitraillette se tient dans la main droite (comme le pistolet dans la gauche),
            // et non plus devant la poitrine, où elle flottait quand les bras du modèle pendent
            if (r.skin3d != null && r.smg != null && r.handR != null)
            {
                r.smg.SetParent(r.skin3d.RelayOf(r.handR), false);
                r.smg.localPosition = Vector3.zero;
                r.smg.localRotation = Quaternion.identity;
            }
            r.Tick(0f);
            return r;
        }

        /// <summary>Segment de membre : capsule sous l'articulation, renvoie l'articulation suivante.</summary>
        static Transform Limb(Transform joint, Material mat, float len, float width)
        {
            Art.Part(joint, Art.Capsule, mat, new Vector3(0f, -len * 0.5f, 0f), new Vector3(width, len * 0.5f, width));
            return Art.Node(joint, "joint", new Vector3(0f, -len, 0f));
        }

        static TrailRenderer Trail(Transform at, Color tail, float width)
        {
            TrailRenderer tr = at.gameObject.AddComponent<TrailRenderer>();
            tr.time = 0.12f;
            tr.minVertexDistance = 0.03f;
            tr.widthMultiplier = width;
            tr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            tr.material = Art.Particle(null, Color.white);
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(tail, 1f) },
                      new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = g;
            tr.emitting = false;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return tr;
        }

        /// <summary>
        /// Vigile : polo marine, casquette, ceinture. Les vigiles ennemis sont tout équipés (gilet jaune, matraque) ;
        /// le vigile joué part sans rien et ramasse son équipement en route.
        /// </summary>
        static void DressGuard(Rig r, bool player)
        {
            Material navy = Art.Mat(new Color(0.1f, 0.13f, 0.25f), 0.35f);
            Material black = Art.Mat(Pal.Black, 0.5f);
            Material gold = Art.Glow(Pal.Gold, Pal.Gold * 0.4f, 0.85f);

            Art.Part(r.spine, Art.Cube, gold, new Vector3(-0.12f, 0.36f, 0.135f), new Vector3(0.08f, 0.09f, 0.02f), default, false);
            Art.Part(r.hips, Art.Cube, black, new Vector3(0f, 0.07f, 0f), new Vector3(0.46f, 0.07f, 0.28f));
            Art.Part(r.shL, Art.Sphere, navy, new Vector3(0f, -0.04f, 0f), new Vector3(0.17f, 0.16f, 0.17f));
            Art.Part(r.shR, Art.Sphere, navy, new Vector3(0f, -0.04f, 0f), new Vector3(0.17f, 0.16f, 0.17f));

            // casquette (masquée sous le casque)
            r.cap = Art.Node(r.neck, "cap", Vector3.zero);
            Art.Part(r.cap, Art.Cylinder, navy, new Vector3(0f, 0.32f, -0.01f), new Vector3(0.33f, 0.07f, 0.33f));
            Art.Part(r.cap, Art.Sphere, navy, new Vector3(0f, 0.34f, -0.01f), new Vector3(0.32f, 0.14f, 0.32f));
            Art.Part(r.cap, Art.Cube, navy, new Vector3(0f, 0.29f, 0.19f), new Vector3(0.26f, 0.03f, 0.16f), new Vector3(-8f, 0f, 0f));
            Art.Part(r.cap, Art.Cube, gold, new Vector3(0f, 0.34f, 0.15f), new Vector3(0.07f, 0.06f, 0.02f), default, false);

            if (player)
            {
                AddGear(r);
                return;
            }

            // vigile ennemi : gilet jaune "SECURITY" et matraque
            Material yellow = Art.Glow(Pal.Jaune, Pal.Jaune * 0.25f, 0.5f);
            Material band = Art.Glow(Color.white, new Color(0.35f, 0.35f, 0.35f), 0.6f);
            Art.Part(r.spine, Art.Cube, yellow, new Vector3(0f, 0.27f, 0f), new Vector3(0.47f, 0.4f, 0.29f));
            Art.Part(r.spine, Art.Cube, Art.Mat(new Color(0.1f, 0.1f, 0.12f), 0.3f), new Vector3(0f, 0.28f, -0.148f), new Vector3(0.36f, 0.1f, 0.01f), default, false);
            Art.Part(r.spine, Art.Cube, band, new Vector3(0f, 0.14f, 0f), new Vector3(0.48f, 0.04f, 0.3f), default, false);
            Art.Part(r.spine, Art.Cube, gold, new Vector3(-0.12f, 0.36f, 0.15f), new Vector3(0.08f, 0.09f, 0.02f), default, false);
            r.baton = Art.Node(r.handR, "baton", Vector3.zero);
            Art.Part(r.baton, Art.Cylinder, black, new Vector3(0f, 0f, 0.24f), new Vector3(0.055f, 0.25f, 0.055f), new Vector3(90f, 0f, 0f));
            r.trail = Trail(Art.Node(r.baton, "tip", new Vector3(0f, 0f, 0.48f)), new Color(1f, 0.85f, 0.3f), 0.16f);
        }

        /// <summary>
        /// Équipement à ramasser en course (vigile comme voleur, mêmes pièces) : gilet pare-coups, casque à visière,
        /// matraque, bouclier, pistolet à impulsion. Tout est masqué au départ ; SetGear l'affiche pièce par pièce.
        /// </summary>
        static void AddGear(Rig r, bool thief = false)
        {
            r.gearThief = thief;
            r.gearPreview = true;
            Material navy = Art.Mat(new Color(0.12f, 0.15f, 0.28f), 0.45f);
            Material dark = Art.Mat(new Color(0.16f, 0.18f, 0.22f), 0.5f);
            Material black = Art.Mat(Pal.Black, 0.5f);
            Material yellow = Art.Glow(Pal.Jaune, Pal.Jaune * 0.6f, 0.5f);
            Material visor = Art.Mat(new Color(0.55f, 0.75f, 0.92f), 0.95f, 0.2f);

            // gilet tactique
            r.gilet = Art.Node(r.spine, "gilet", Vector3.zero);
            Art.Part(r.gilet, Art.Cube, dark, new Vector3(0f, 0.27f, 0f), new Vector3(0.53f, 0.44f, 0.36f));
            Art.Part(r.gilet, Art.Cube, yellow, new Vector3(0f, 0.22f, 0f), new Vector3(0.54f, 0.06f, 0.37f), default, false);
            Art.Part(r.gilet, Art.Cube, dark, new Vector3(-0.13f, 0.12f, 0.19f), new Vector3(0.12f, 0.12f, 0.06f));
            Art.Part(r.gilet, Art.Cube, dark, new Vector3(0.13f, 0.12f, 0.19f), new Vector3(0.12f, 0.12f, 0.06f));
            r.gilet.gameObject.SetActive(false);

            Material goldH = Art.Glow(Pal.Gold, Pal.Gold * 0.7f, 0.9f);
            if (thief)
            {
                // voleur : il porte déjà sa cagoule ; les améliorations s'y ajoutent sans cacher les yeux :
                // bandeau rouge (niv. 1), lunettes de ski relevées sur le front (niv. 2), pompon et bandeau dorés (niv. 3)
                Material redBand = Art.Glow(Pal.Rouge, Pal.Rouge * 0.5f, 0.4f);
                r.helmet = Art.Node(r.neck, "helmet", Vector3.zero);
                Art.Part(r.helmet, Art.Cylinder, redBand, new Vector3(0f, 0.3f, -0.005f), new Vector3(0.3f, 0.028f, 0.3f), default, false);
                r.helmetVisor = Art.Node(r.helmet, "visor", Vector3.zero);
                Material lens = Art.Glow(new Color(1f, 0.55f, 0.1f), new Color(1.4f, 0.6f, 0.1f), 0.95f);
                Art.Part(r.helmetVisor, Art.Cube, black, new Vector3(0f, 0.325f, 0.115f), new Vector3(0.24f, 0.075f, 0.05f), new Vector3(-20f, 0f, 0f));
                Art.Part(r.helmetVisor, Art.Sphere, lens, new Vector3(-0.058f, 0.328f, 0.138f), new Vector3(0.085f, 0.06f, 0.035f), new Vector3(-20f, 0f, 0f), false);
                Art.Part(r.helmetVisor, Art.Sphere, lens, new Vector3(0.058f, 0.328f, 0.138f), new Vector3(0.085f, 0.06f, 0.035f), new Vector3(-20f, 0f, 0f), false);
                r.helmetCrest = Art.Node(r.helmet, "crest", Vector3.zero);
                // niveau 3 : casque de moto noir brillant, visière orange, bande rouge (il recouvre bandeau et lunettes)
                Material moto = Art.Mat(new Color(0.1f, 0.11f, 0.14f), 0.9f, 0.3f);
                Art.Part(r.helmetCrest, Art.Sphere, moto, new Vector3(0f, 0.25f, -0.005f), new Vector3(0.41f, 0.42f, 0.42f));
                Art.Part(r.helmetCrest, Art.Sphere, lens, new Vector3(0f, 0.23f, 0.12f), new Vector3(0.32f, 0.16f, 0.2f), default, false);
                Art.Part(r.helmetCrest, Art.Cube, redBand, new Vector3(0f, 0.4f, -0.02f), new Vector3(0.06f, 0.08f, 0.36f), default, false);
            }
            else
            {
                // casque anti-émeute à visière
                r.helmet = Art.Node(r.neck, "helmet", Vector3.zero);
                Art.Part(r.helmet, Art.Sphere, navy, new Vector3(0f, 0.25f, -0.01f), new Vector3(0.38f, 0.36f, 0.38f));
                Art.Part(r.helmet, Art.Cube, yellow, new Vector3(0f, 0.36f, 0f), new Vector3(0.06f, 0.06f, 0.36f), default, false);
                Art.Part(r.helmet, Art.Cylinder, navy, new Vector3(0f, 0.1f, -0.06f), new Vector3(0.36f, 0.04f, 0.32f));
                r.helmetVisor = Art.Node(r.helmet, "visor", Vector3.zero);
                Art.Part(r.helmetVisor, Art.Sphere, visor, new Vector3(0f, 0.2f, 0.125f), new Vector3(0.34f, 0.24f, 0.2f));
                r.helmetCrest = Art.Node(r.helmet, "crest", Vector3.zero);
                Art.Part(r.helmetCrest, Art.Cube, goldH, new Vector3(0f, 0.44f, -0.02f), new Vector3(0.07f, 0.1f, 0.3f));
                Art.Part(r.helmetCrest, Art.Sphere, goldH, new Vector3(0f, 0.3f, 0.19f), Vector3.one * 0.07f, default, false);
            }
            r.helmet.gameObject.SetActive(false);

            // tenue renforcée permanente (gilet niveaux 1 à 3), sous la plaque d'armure temporaire
            r.vestPerm = Art.Node(r.spine, "vestPerm", Vector3.zero);
            // policier : gilet gris à bande blanche ; voleur : sweat épais à bande rouge
            Art.Part(r.vestPerm, Art.Cube, Art.Mat(thief ? new Color(0.17f, 0.18f, 0.21f) : new Color(0.3f, 0.32f, 0.36f), 0.4f), new Vector3(0f, 0.26f, 0f), new Vector3(0.5f, 0.42f, 0.32f));
            Art.Part(r.vestPerm, Art.Cube, thief ? Art.Glow(Pal.Rouge, Pal.Rouge * 0.5f, 0.5f) : Art.Glow(Color.white, new Color(0.45f, 0.45f, 0.45f), 0.6f), new Vector3(0f, 0.12f, 0f), new Vector3(0.51f, 0.04f, 0.33f), default, false);
            if (thief) Art.Part(r.vestPerm, Art.Cube, Art.Mat(new Color(0.11f, 0.12f, 0.14f), 0.3f), new Vector3(0f, 0.04f, 0.17f), new Vector3(0.26f, 0.1f, 0.03f));
            r.vestPerm.gameObject.SetActive(false);
            Material pad = Art.Mat(new Color(0.22f, 0.24f, 0.28f), 0.5f);
            r.padL = Art.Node(r.shL, "pad", Vector3.zero);
            Art.Part(r.padL, Art.Sphere, pad, new Vector3(-0.02f, 0.01f, 0f), new Vector3(0.24f, 0.17f, 0.24f));
            r.padL.gameObject.SetActive(false);
            r.padR = Art.Node(r.shR, "pad", Vector3.zero);
            Art.Part(r.padR, Art.Sphere, pad, new Vector3(0.02f, 0.01f, 0f), new Vector3(0.24f, 0.17f, 0.24f));
            r.padR.gameObject.SetActive(false);
            r.vestTrim = Art.Node(r.spine, "trim", Vector3.zero);
            Art.Part(r.vestTrim, Art.Cube, goldH, new Vector3(0f, 0.4f, 0f), new Vector3(0.52f, 0.04f, 0.34f), default, false);
            Art.Part(r.vestTrim, Art.Cube, goldH, new Vector3(-0.12f, 0.3f, 0.17f), new Vector3(0.09f, 0.1f, 0.02f), default, false);
            r.vestTrim.gameObject.SetActive(false);

            // matraque en main droite
            // trois matraques : simple, lourde à embout métallique, électrique qui brille
            Material steel = Art.Mat(new Color(0.6f, 0.62f, 0.66f), 0.8f, 0.8f);
            Material shock = Art.Glow(new Color(0.3f, 0.7f, 1f), new Color(0.6f, 1.6f, 3f), 0.6f);
            Material rust = Art.Mat(new Color(0.7f, 0.13f, 0.11f), 0.55f, 0.5f);
            Material hot = Art.Glow(new Color(1f, 0.5f, 0.1f), new Color(2.4f, 0.9f, 0.1f), 0.6f);
            for (int v = 0; v < 3 && thief; v++)
            {
                // voleur : batte de baseball (arme de mêlée) ; les variantes 2 et 3 (pied-de-biche) restent pour la barre de fer
                Transform b = Art.Node(r.handR, "baton" + (v + 1), Vector3.zero);
                if (v == 0)
                {
                    Material wood = Art.Mat(new Color(0.79f, 0.55f, 0.31f), 0.45f);
                    Material tape = Art.Mat(new Color(0.18f, 0.19f, 0.22f), 0.3f);
                    Art.Part(b, Art.Cylinder, tape, new Vector3(0f, 0f, 0.08f), new Vector3(0.05f, 0.1f, 0.05f), new Vector3(90f, 0f, 0f));
                    Art.Part(b, Art.Cylinder, wood, new Vector3(0f, 0f, 0.3f), new Vector3(0.065f, 0.14f, 0.065f), new Vector3(90f, 0f, 0f));
                    Art.Part(b, Art.Cylinder, wood, new Vector3(0f, 0f, 0.5f), new Vector3(0.1f, 0.12f, 0.1f), new Vector3(90f, 0f, 0f));
                    Art.Part(b, Art.Sphere, wood, new Vector3(0f, 0f, 0.62f), Vector3.one * 0.1f, default, false);
                    r.batonTrails[0] = Trail(Art.Node(b, "tip", new Vector3(0f, 0f, 0.64f)), new Color(1f, 0.85f, 0.4f), 0.2f);
                    b.gameObject.SetActive(false);
                    r.batons[0] = b;
                    continue;
                }
                float len = v == 0 ? 0.27f : 0.32f, w = v == 0 ? 0.045f : 0.065f;
                Art.Part(b, Art.Cylinder, rust, new Vector3(0f, 0f, len - 0.01f), new Vector3(w, len, w), new Vector3(90f, 0f, 0f));
                Art.Part(b, Art.Cylinder, v == 2 ? hot : rust, new Vector3(0f, 0.05f, len * 2f + 0.01f), new Vector3(w, 0.07f, w), new Vector3(35f, 0f, 0f));
                if (v >= 1)
                    for (int k = 0; k < 2; k++)
                        Art.Part(b, Art.Cylinder, steel, new Vector3(0f, 0f, 0.08f + k * 0.08f), new Vector3(w + 0.015f, 0.025f, w + 0.015f), new Vector3(90f, 0f, 0f));
                if (v == 2) Art.Part(b, Art.Sphere, hot, new Vector3(0f, 0.09f, len * 2f + 0.06f), Vector3.one * (w + 0.04f), default, false);
                Color tailT = v == 2 ? new Color(1f, 0.55f, 0.15f) : new Color(1f, 0.35f, 0.25f);
                r.batonTrails[v] = Trail(Art.Node(b, "tip", new Vector3(0f, 0f, len * 2f)), tailT, v == 2 ? 0.24f : 0.18f);
                b.gameObject.SetActive(false);
                r.batons[v] = b;
            }
            for (int v = 0; v < 3 && !thief; v++)
            {
                Transform b = Art.Node(r.handR, "baton" + (v + 1), Vector3.zero);
                float len = v == 0 ? 0.25f : 0.3f, w = v == 0 ? 0.055f : 0.075f;
                Art.Part(b, Art.Cylinder, black, new Vector3(0f, 0f, len - 0.01f), new Vector3(w, len, w), new Vector3(90f, 0f, 0f));
                Art.Part(b, Art.Cylinder, black, new Vector3(0.07f, 0f, 0.06f), new Vector3(0.04f, 0.06f, 0.04f), new Vector3(0f, 0f, 90f));
                if (v >= 1) Art.Part(b, Art.Cylinder, v == 2 ? shock : steel, new Vector3(0f, 0f, len * 2f - 0.04f), new Vector3(w + 0.02f, 0.06f, w + 0.02f), new Vector3(90f, 0f, 0f));
                if (v == 2)
                    for (int k = 0; k < 3; k++)
                        Art.Part(b, Art.Cylinder, shock, new Vector3(0f, 0f, 0.18f + k * 0.12f), new Vector3(w + 0.012f, 0.012f, w + 0.012f), new Vector3(90f, 0f, 0f), false);
                Color tail = v == 2 ? new Color(0.4f, 0.8f, 1f) : v == 1 ? new Color(1f, 0.7f, 0.25f) : new Color(1f, 0.85f, 0.3f);
                r.batonTrails[v] = Trail(Art.Node(b, "tip", new Vector3(0f, 0f, len * 2f)), tail, v == 2 ? 0.24f : 0.18f);
                b.gameObject.SetActive(false);
                r.batons[v] = b;
            }
            r.baton = r.batons[0];
            r.batonTrail = r.batonTrails[0];

            // matraque renforcée (map 2) : plus longue et plus épaisse, cerclée d'acier, poignée rouge, clous lumineux
            {
                Transform hb = Art.Node(r.handR, "batonHeavy", Vector3.zero);
                Material steel2 = Art.Mat(new Color(0.62f, 0.65f, 0.7f), 0.85f, 0.85f);
                Material grip = Art.Mat(new Color(0.7f, 0.1f, 0.1f), 0.4f);
                Material stud = Art.Glow(new Color(1f, 0.6f, 0.15f), new Color(2.2f, 1f, 0.2f), 0.7f);
                Art.Part(hb, Art.Cylinder, thief ? rust : black, new Vector3(0f, 0f, 0.4f), new Vector3(0.11f, 0.4f, 0.11f), new Vector3(90f, 0f, 0f));
                if (thief) Art.Part(hb, Art.Cylinder, rust, new Vector3(0f, 0.08f, 0.84f), new Vector3(0.1f, 0.1f, 0.1f), new Vector3(40f, 0f, 0f));
                Art.Part(hb, Art.Cylinder, grip, new Vector3(0f, 0f, 0.04f), new Vector3(0.085f, 0.1f, 0.085f), new Vector3(90f, 0f, 0f));
                Art.Part(hb, Art.Cylinder, black, new Vector3(0.08f, 0f, 0.1f), new Vector3(0.045f, 0.07f, 0.045f), new Vector3(0f, 0f, 90f));
                for (int k = 0; k < 3; k++)
                    Art.Part(hb, Art.Cylinder, steel2, new Vector3(0f, 0f, 0.3f + k * 0.2f), new Vector3(0.13f, 0.025f, 0.13f), new Vector3(90f, 0f, 0f));
                Art.Part(hb, Art.Sphere, steel2, new Vector3(0f, 0f, 0.8f), new Vector3(0.14f, 0.14f, 0.14f));
                for (int k = 0; k < 4; k++)
                {
                    float a = k * Mathf.PI * 0.5f;
                    Art.Part(hb, Art.Sphere, stud, new Vector3(Mathf.Cos(a) * 0.06f, Mathf.Sin(a) * 0.06f, 0.62f), Vector3.one * 0.035f, default, false);
                }
                r.heavyTrail = Trail(Art.Node(hb, "tip", new Vector3(0f, 0f, 0.82f)), new Color(1f, 0.45f, 0.1f), 0.32f);
                // améliorations visibles de la matraque renforcée : bagues dorées (niv. 2), noyau électrique (niv. 3)
                Material goldBand = Art.Glow(Pal.Gold, Pal.Gold * 0.8f, 0.9f);
                r.heavyLv2 = Art.Node(hb, "lv2", Vector3.zero);
                for (int k = 0; k < 3; k++)
                    Art.Part(r.heavyLv2, Art.Cylinder, goldBand, new Vector3(0f, 0f, 0.4f + k * 0.2f), new Vector3(0.135f, 0.03f, 0.135f), new Vector3(90f, 0f, 0f), false);
                r.heavyLv3 = Art.Node(hb, "lv3", Vector3.zero);
                Material volt = Art.Glow(new Color(0.4f, 0.85f, 1f), new Color(0.8f, 2.4f, 3.2f), 0.6f);
                Art.Part(r.heavyLv3, Art.Sphere, volt, new Vector3(0f, 0f, 0.8f), Vector3.one * 0.19f, default, false);
                Art.Part(r.heavyLv3, Art.Cylinder, volt, new Vector3(0f, 0f, 0.55f), new Vector3(0.05f, 0.25f, 0.05f), new Vector3(90f, 0f, 0f), false);
                r.heavyLv2.gameObject.SetActive(false);
                r.heavyLv3.gameObject.SetActive(false);
                hb.gameObject.SetActive(false);
                r.heavyBaton = hb;
                // arme renforcée 3D (matraque cloutée / barre de fer) : remplace les formes et les bagues de niveau
                if (ReplaceSlot(hb, thief ? "arme_heavy_voleur" : "arme_heavy_police", 0.95f, 1, new Vector3(0f, 0f, 0.4f), 90f, 0f) != null)
                {
                    if (r.heavyLv2 != null) { Object.Destroy(r.heavyLv2.gameObject); r.heavyLv2 = null; }
                    if (r.heavyLv3 != null) { Object.Destroy(r.heavyLv3.gameObject); r.heavyLv3 = null; }
                }
            }
            r.fistTrail = Trail(Art.Node(r.handR, "fist", Vector3.zero), new Color(1f, 0.6f, 0.4f), 0.2f);
            r.trail = r.fistTrail;

            // bouclier devant le buste
            r.shield = Art.Node(r.spine, "shield", new Vector3(-0.08f, 0.18f, 0.4f));
            Art.Part(r.shield, Art.Cube, Art.Mat(new Color(0.7f, 0.85f, 0.95f), 0.92f, 0.1f), Vector3.zero, new Vector3(0.66f, 0.98f, 0.045f));
            Art.Part(r.shield, Art.Cube, yellow, new Vector3(0f, 0.28f, 0.03f), new Vector3(0.52f, 0.08f, 0.02f), default, false);
            Art.Part(r.shield, Art.Cube, black, new Vector3(0f, 0f, -0.04f), new Vector3(0.08f, 0.3f, 0.04f));
            r.shield.gameObject.SetActive(false);

            // pistolet à impulsion : en main gauche, pointe jaune
            // policier : taser noir et jaune ; voleur : pistolet à peinture rose avec réservoir vert
            r.pistol = Art.Node(r.handL, "pistol", Vector3.zero);
            Material pBody = thief ? Art.Mat(new Color(1f, 0.31f, 0.64f), 0.5f) : black;
            Material pTip = thief ? Art.Glow(new Color(0.25f, 0.85f, 0.45f), new Color(0.3f, 1.2f, 0.5f), 0.5f) : yellow;
            Art.Part(r.pistol, Art.Cube, pBody, new Vector3(0f, -0.02f, 0.1f), new Vector3(0.07f, 0.09f, 0.26f));
            Art.Part(r.pistol, Art.Cube, pBody, new Vector3(0f, -0.1f, 0.02f), new Vector3(0.06f, 0.14f, 0.07f), new Vector3(-15f, 0f, 0f));
            Art.Part(r.pistol, Art.Cube, pTip, new Vector3(0f, -0.02f, 0.235f), new Vector3(0.075f, 0.075f, 0.03f), default, false);
            if (thief) Art.Part(r.pistol, Art.Sphere, pTip, new Vector3(0f, 0.07f, 0.1f), Vector3.one * 0.1f, default, false);
            r.pistol.gameObject.SetActive(false);
            r.holster = Art.Node(r.hips, "holster", new Vector3(-0.24f, -0.02f, 0f));
            Art.Part(r.holster, Art.Cube, black, Vector3.zero, new Vector3(0.07f, 0.18f, 0.12f));
            r.holster.gameObject.SetActive(false);

            // mitraillette tenue à deux mains devant le buste
            // policier : mitraillette à mousse orange et bleue ; voleur : mitrailleuse à peinture violette, réservoir vert
            r.smg = Art.Node(r.spine, "smg", new Vector3(0.06f, 0.2f, 0.36f));
            Material gBody = Art.Mat(thief ? new Color(0.54f, 0.24f, 1f) : new Color(1f, 0.54f, 0.16f), 0.5f);
            Material gAcc = thief ? Art.Glow(new Color(0.25f, 0.85f, 0.45f), new Color(0.3f, 1.2f, 0.5f), 0.5f) : Art.Mat(new Color(0.24f, 0.49f, 1f), 0.5f);
            Art.Part(r.smg, Art.Cube, gBody, new Vector3(0f, 0f, 0.05f), new Vector3(0.1f, 0.13f, 0.5f));
            Art.Part(r.smg, Art.Cube, gAcc, new Vector3(0f, -0.13f, 0.08f), new Vector3(0.06f, 0.18f, 0.08f), new Vector3(12f, 0f, 0f));
            Art.Part(r.smg, Art.Cube, gBody, new Vector3(0f, -0.08f, -0.14f), new Vector3(0.07f, 0.12f, 0.07f), new Vector3(-20f, 0f, 0f));
            if (thief) Art.Part(r.smg, Art.Sphere, gAcc, new Vector3(0f, 0.13f, -0.05f), Vector3.one * 0.15f, default, false);
            Art.Part(r.smg, Art.Cylinder, Art.Mat(new Color(0.25f, 0.27f, 0.3f), 0.7f, 0.6f), new Vector3(0f, 0.01f, 0.36f), new Vector3(0.05f, 0.08f, 0.05f), new Vector3(90f, 0f, 0f));
            Art.Part(r.smg, Art.Cube, Art.Glow(new Color(1f, 0.55f, 0.1f), new Color(1.6f, 0.7f, 0.1f)), new Vector3(0f, 0.075f, 0.05f), new Vector3(0.105f, 0.02f, 0.3f), default, false);
            r.smg.gameObject.SetActive(false);

            // bouclier gagné (3 niveaux par camp), sanglé dans le dos
            r.permShields = new Transform[3];
            Material nail = Art.Mat(new Color(0.72f, 0.75f, 0.8f), 0.8f, 0.7f);
            for (int v = 0; v < 3; v++)
            {
                Transform s = Art.Node(r.spine, "permShield" + (v + 1), new Vector3(0f, 0.3f, -0.27f));
                if (!thief)
                {
                    if (v == 0)
                    {
                        Material wood = Art.Mat(new Color(0.69f, 0.48f, 0.27f), 0.35f), dk = Art.Mat(new Color(0.49f, 0.32f, 0.19f), 0.35f);
                        Art.Part(s, Art.Cube, wood, Vector3.zero, new Vector3(0.48f, 0.6f, 0.05f));
                        Art.Part(s, Art.Cube, dk, new Vector3(0f, 0.04f, -0.03f), new Vector3(0.5f, 0.07f, 0.02f), default, false);
                        Art.Part(s, Art.Cube, dk, new Vector3(-0.08f, 0f, -0.03f), new Vector3(0.02f, 0.6f, 0.01f), default, false);
                        Art.Part(s, Art.Cube, dk, new Vector3(0.08f, 0f, -0.03f), new Vector3(0.02f, 0.6f, 0.01f), default, false);
                    }
                    else if (v == 1)
                    {
                        Art.Part(s, Art.Cube, Art.Mat(new Color(0.7f, 0.85f, 0.95f), 0.92f, 0.1f), Vector3.zero, new Vector3(0.5f, 0.68f, 0.04f));
                        Art.Part(s, Art.Cube, Art.Mat(new Color(0.17f, 0.23f, 0.42f), 0.5f), new Vector3(0f, 0.12f, -0.025f), new Vector3(0.51f, 0.1f, 0.02f), default, false);
                    }
                    else
                    {
                        Art.Part(s, Art.Cube, Art.Mat(new Color(0.17f, 0.21f, 0.31f), 0.7f, 0.5f), Vector3.zero, new Vector3(0.52f, 0.7f, 0.06f));
                        Art.Part(s, Art.Cube, Art.Mat(new Color(0.56f, 0.83f, 1f), 0.95f, 0.2f), new Vector3(0f, 0.22f, -0.035f), new Vector3(0.3f, 0.1f, 0.02f), default, false);
                        Art.Part(s, Art.Cube, goldH, new Vector3(0f, 0f, -0.035f), new Vector3(0.53f, 0.06f, 0.02f), default, false);
                        Art.Part(s, Art.Sphere, goldH, new Vector3(0f, -0.18f, -0.04f), Vector3.one * 0.1f, default, false);
                    }
                }
                else
                {
                    if (v == 0)
                    {
                        // couvercle de poubelle
                        Material lid = Art.Mat(new Color(0.6f, 0.64f, 0.68f), 0.75f, 0.6f);
                        Art.Part(s, Art.Cylinder, lid, Vector3.zero, new Vector3(0.56f, 0.025f, 0.56f), new Vector3(90f, 0f, 0f));
                        Art.Part(s, Art.Cylinder, nail, new Vector3(0f, 0f, -0.03f), new Vector3(0.36f, 0.02f, 0.36f), new Vector3(90f, 0f, 0f), false);
                        Art.Part(s, Art.Cylinder, lid, new Vector3(0f, 0f, -0.05f), new Vector3(0.14f, 0.03f, 0.14f), new Vector3(90f, 0f, 0f), false);
                    }
                    else if (v == 1)
                    {
                        // panneau STOP arraché
                        Art.Part(s, Art.Cylinder, Art.Mat(Color.white, 0.4f), Vector3.zero, new Vector3(0.6f, 0.02f, 0.6f), new Vector3(90f, 0f, 0f));
                        Art.Part(s, Art.Cylinder, Art.Glow(Pal.Rouge, Pal.Rouge * 0.3f, 0.4f), new Vector3(0f, 0f, -0.015f), new Vector3(0.52f, 0.02f, 0.52f), new Vector3(90f, 0f, 0f), false);
                        Art.Part(s, Art.Cube, Art.Mat(Color.white, 0.4f), new Vector3(0f, 0f, -0.03f), new Vector3(0.34f, 0.08f, 0.01f), default, false);
                        Art.Part(s, Art.Cylinder, nail, new Vector3(0f, -0.38f, 0f), new Vector3(0.04f, 0.12f, 0.04f));
                    }
                    else
                    {
                        // bouclier de police volé, barré de scotch rouge
                        Art.Part(s, Art.Cube, Art.Mat(new Color(0.7f, 0.85f, 0.95f), 0.92f, 0.1f), Vector3.zero, new Vector3(0.5f, 0.68f, 0.04f));
                        Material tapeR = Art.Glow(Pal.Rouge, Pal.Rouge * 0.4f, 0.4f);
                        Art.Part(s, Art.Cube, tapeR, new Vector3(0f, 0f, -0.03f), new Vector3(0.08f, 0.8f, 0.01f), new Vector3(0f, 0f, 35f), false);
                        Art.Part(s, Art.Cube, tapeR, new Vector3(0f, 0f, -0.03f), new Vector3(0.08f, 0.8f, 0.01f), new Vector3(0f, 0f, -35f), false);
                    }
                }
                s.gameObject.SetActive(false);
                r.permShields[v] = s;
            }
            WeaponModels(r, thief);
            // boucliers 3D sanglés dans le dos : face décorée vers l'arrière
            float[] shieldW = thief ? new[] { 0.56f, 0.6f, 0.5f } : new[] { 0.56f, 0.5f, 0.52f };
            for (int v = 0; v < 3; v++)
                ReplaceSlot(r.permShields[v], "bouclier_" + (thief ? "voleur" : "police") + "_niv" + (v + 1), shieldW[v], 0, Vector3.zero, 0f, 180f);
        }

        // ------------------------------------------------------------------ équipement en 3D (modèles Meshy)
        bool gearThief;
        /// <summary>TEST TEMPORAIRE : force ce niveau de casque 3D sur le policier joueur (0 = désactivé).</summary>
        const int PreviewHelmetLv = 0;
        bool gearPreview;
        readonly Transform[] helmetModels = new Transform[4];

        /// <summary>
        /// Casque illustré en 3D (Resources/SamediRouge/Models/casque_&lt;camp&gt;_niv&lt;n&gt;) : s'il existe pour ce niveau,
        /// il remplace les formes simples du casque ; sinon on garde le casque dessiné en code.
        /// </summary>
        void HelmetModel(int lv)
        {
            if (helmet == null) return;
            string name = (gearThief ? "casque_voleur_niv" : "casque_police_niv") + lv;
            Transform model = null;
            if (lv >= 1 && lv <= 3)
            {
                if (helmetModels[lv] == null && GlbModel.Exists(name))
                {
                    helmetModels[lv] = GlbModel.Spawn(name, helmet);
                    HelmetFit(helmetModels[lv], gearThief, lv);
                }
                model = helmetModels[lv];
                if (model != null)
                {
                    HelmetFit(model, gearThief, lv);
                    // policier en tenue 3D : sa casquette descendait sur les yeux, on la remonte sur le crâne
                    if (!gearThief && lv == 1 && skin3d != null && skin3d.Name != null && skin3d.Name.StartsWith("policier_niv"))
                        model.localPosition += new Vector3(0f, 0.07f, -0.01f);
                }
            }
            for (int i = 0; i < helmetModels.Length; i++)
                if (helmetModels[i] != null) helmetModels[i].gameObject.SetActive(helmetModels[i] == model);
            for (int c = 0; c < helmet.childCount; c++)
            {
                Transform t = helmet.GetChild(c);
                if (System.Array.IndexOf(helmetModels, t) >= 0) continue;
                if (model != null) t.gameObject.SetActive(false);
                else if (t == helmetVisor) t.gameObject.SetActive(lv >= 2);
                else if (t == helmetCrest) t.gameObject.SetActive(lv >= 3);
                else t.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Place chaque casque 3D sur la tête : largeur, hauteur du centre, avancée, inclinaison (X) et orientation (Y).
        /// Policier : 1 casquette, 2 casque à visière, 3 casque intégral. Voleur (par-dessus la cagoule) :
        /// 1 bandana (nœud derrière), 2 lunettes de ski relevées sur le front, 3 casque de moto.
        /// </summary>
        static void HelmetFit(Transform m, bool thief, int lv)
        {
            // policier (corps 3D) : casquette enfoncée sur les cheveux, casques remontés pour dégager les yeux
            if (!thief && lv == 1) Fit(m, 0.37f, 0, new Vector3(0f, 0.31f, -0.01f), 0f, 0f);
            else if (!thief) Fit(m, 0.39f, 0, new Vector3(0f, lv == 2 ? 0.25f : 0.23f, -0.01f), 0f, 0f);
            // voleur (corps 3D, cagoule) : bandana sur le front, lunettes relevées, casque de moto
            else if (thief && lv == 1) Fit(m, 0.27f, 0, new Vector3(0f, 0.20f, 0f), 0f, 180f);
            else if (thief && lv == 2) Fit(m, 0.31f, 0, new Vector3(0f, 0.31f, 0.07f), -25f, 0f);
            else Fit(m, 0.33f, 0, new Vector3(0f, 0.22f, 0f), 0f, 0f);
        }

        /// <summary>
        /// Met un modèle à l'échelle (taille voulue le long de l'axe donné : 0 = largeur X, 1 = hauteur Y),
        /// le tourne (X puis Y) et place son centre au point voulu du repère parent.
        /// </summary>
        static void Fit(Transform m, float size, int axis, Vector3 center, float rotX, float rotY)
        {
            Bounds b = new Bounds();
            bool first = true;
            foreach (MeshFilter f in m.GetComponentsInChildren<MeshFilter>())
            {
                if (first) { b = f.sharedMesh.bounds; first = false; } else b.Encapsulate(f.sharedMesh.bounds);
            }
            float k = size / Mathf.Max(0.001f, axis == 1 ? b.size.y : b.size.x);
            Quaternion q = Quaternion.Euler(0f, rotY, 0f) * Quaternion.Euler(rotX, 0f, 0f);
            m.localScale = Vector3.one * k;
            m.localRotation = q;
            m.localPosition = center - q * (b.center * k);
        }

        /// <summary>Remplace les formes simples d'un emplacement (matraque, pistolet…) par le modèle 3D s'il existe.</summary>
        static Transform ReplaceSlot(Transform slot, string name, float size, int axis, Vector3 center, float rotX, float rotY)
        {
            if (slot == null || !GlbModel.Exists(name)) return null;
            for (int c = 0; c < slot.childCount; c++)
            {
                Transform t = slot.GetChild(c);
                if (t.name == "Cube" || t.name == "Sphere" || t.name == "Cylinder" || t.name == "Capsule") t.gameObject.SetActive(false);
            }
            Transform m = GlbModel.Spawn(name, slot);
            Fit(m, size, axis, center, rotX, rotY);
            return m;
        }

        /// <summary>Armes 3D : 1 matraque / batte en main droite, 2 pistolet en main gauche, 3 mitraillette à deux mains.</summary>
        static void WeaponModels(Rig r, bool thief)
        {
            string f = thief ? "voleur" : "police";
            if (r.batons[0] != null)
            {
                // tenue par la poignée (bas du modèle) : le modèle debout (Y) est couché vers l'avant (Z)
                float len = thief ? 0.7f : 0.55f;
                ReplaceSlot(r.batons[0], "arme_" + f + "_niv1", len, 1, new Vector3(0f, 0f, len * 0.5f - 0.08f), 90f, 0f);
            }
            // le pistolet à peinture du voleur est modélisé canon vers l'autre côté
            ReplaceSlot(r.pistol, "arme_" + f + "_niv2", 0.3f, 0, new Vector3(0f, -0.03f, 0.1f), 0f, thief ? 90f : -90f);
            ReplaceSlot(r.smg, "arme_" + f + "_niv3", 0.55f, 0, new Vector3(0f, -0.02f, 0.05f), 0f, -90f);
        }

        readonly Transform[] vestModels = new Transform[4];

        /// <summary>Tenue à la taille du buste : largeur des épaules, bas sur les hanches, épaisseur du torse.</summary>
        static void FitTorso(Transform m)
        {
            Bounds b = new Bounds();
            bool first = true;
            foreach (MeshFilter f in m.GetComponentsInChildren<MeshFilter>())
            {
                if (first) { b = f.sharedMesh.bounds; first = false; } else b.Encapsulate(f.sharedMesh.bounds);
            }
            float k = 0.74f / Mathf.Max(0.001f, b.size.x);
            float kz = k * Mathf.Clamp(0.5f / Mathf.Max(0.001f, b.size.z * k), 0.8f, 1.9f);
            m.localRotation = Quaternion.identity;
            m.localScale = new Vector3(k, k, kz);
            m.localPosition = new Vector3(-b.center.x * k, -0.04f - b.min.y * k, -b.center.z * kz);
        }

        /// <summary>Tenue 3D (gilet / sweat / blouson) du niveau acheté, portée sur le buste ; sinon la tenue dessinée en code.</summary>
        void VestModel(int lv)
        {
            if (spine == null) return;
            Transform model = null;
            // héros 3D : un modèle complet par tenue (la tenue fait partie du personnage, elle suit ses mouvements)
            bool outfit = false;
            if (skin3d != null && outfitBase != null)
            {
                string want = lv >= 1 && lv <= 3 ? outfitBase + "_niv" + lv : outfitBase;
                outfit = lv >= 1 && lv <= 3 && Skin3D.Available(want) && skin3d.Swap(want);
                if (!outfit) skin3d.Swap(outfitBase);
            }
            if (outfit)
            {
                // la tenue est dans le modèle : plus de gilet, d'épaulières ni de liserés dessinés en code
                if (vestPerm != null) vestPerm.gameObject.SetActive(false);
                if (padL != null) { padL.gameObject.SetActive(false); padR.gameObject.SetActive(false); }
                if (vestTrim != null) vestTrim.gameObject.SetActive(false);
            }
            if (lv >= 1 && lv <= 3 && !outfit)
            {
                string name = (gearThief ? "gilet_voleur_niv" : "gilet_police_niv") + lv;
                if (vestModels[lv] == null && GlbModel.Exists(name))
                {
                    vestModels[lv] = GlbModel.Spawn(name, skin3d != null ? skin3d.RelayOf(spine) : spine);
                    FitTorso(vestModels[lv]);
                    // voleur 3D : son sweat est épais, la tenue doit l'englober (sinon elle disparaît dedans)
                    if (skin3d != null && gearThief)
                    {
                        Transform vm = vestModels[lv];
                        Vector3 f = lv == 3 ? new Vector3(1.25f, 1.45f, 1.6f) : new Vector3(1.4f, 1.15f, 1.5f), lp = vm.localPosition;
                        vm.localScale = Vector3.Scale(vm.localScale, f);
                        vm.localPosition = new Vector3(lp.x * f.x, (lv == 3 ? -0.43f : -0.08f) + (lp.y + 0.04f) * f.y, lp.z * f.z + (lv == 3 ? 0.07f : 0f));
                    }
                }
                model = vestModels[lv];
            }
            for (int i = 0; i < vestModels.Length; i++)
                if (vestModels[i] != null) vestModels[i].gameObject.SetActive(vestModels[i] == model);
            // la tenue 3D remplace le buste dessiné en code (sinon elle disparaît dedans)
            for (int c = 0; c < spine.childCount; c++)
            {
                Transform t = spine.GetChild(c);
                if (t.name == "Cube" || t.name == "Sphere" || t.name == "Capsule" || t.name == "Cylinder") t.gameObject.SetActive(model == null && skin3d == null);
            }
            if (model == null) return;
            if (vestPerm != null) vestPerm.gameObject.SetActive(false);
            if (padL != null) { padL.gameObject.SetActive(false); padR.gameObject.SetActive(false); }
            if (vestTrim != null) vestTrim.gameObject.SetActive(false);
        }

        Transform[] permShields;
        float shieldPunch;

        /// <summary>Bouclier gagné porté dans le dos (niveau 0 : aucun).</summary>
        public void SetPermShield(int level)
        {
            if (permShields == null) return;
            for (int v = 0; v < 3; v++) if (permShields[v] != null) permShields[v].gameObject.SetActive(v == level - 1);
        }

        /// <summary>Le bouclier encaisse un coup : il tressaute, et disparaît au dernier.</summary>
        public void ShieldHit(int remaining)
        {
            if (permShields == null) return;
            shieldPunch = 1f;
            if (remaining <= 0) foreach (Transform t in permShields) if (t != null) t.gameObject.SetActive(false);
        }

        /// <summary>
        /// Équipement permanent visible : casque (1 coque, 2 + visière, 3 + crête dorée), tenue (1 gilet renforcé,
        /// 2 + épaulières, 3 + liserés dorés), arme (1 matraque, 2 lourde, 3 électrique). À appeler avant SetGear.
        /// </summary>
        public void SetLevels(int helmetLv, int vestLv, int weaponLv)
        {
            if (helmetVisor != null) helmetVisor.gameObject.SetActive(helmetLv >= 2);
            if (helmetCrest != null) helmetCrest.gameObject.SetActive(helmetLv >= 3);
            if (vestPerm != null) vestPerm.gameObject.SetActive(vestLv >= 1);
            if (padL != null) { padL.gameObject.SetActive(vestLv >= 2); padR.gameObject.SetActive(vestLv >= 2); }
            if (vestTrim != null) vestTrim.gameObject.SetActive(vestLv >= 3);
            VestModel(vestLv);   // d'abord la tenue (elle peut changer le modèle 3D), puis le casque ajusté à sa tête
            HelmetModel(PreviewHelmetLv > 0 && !gearThief ? PreviewHelmetLv : helmetLv);
            if (batons[0] != null)
            {
                foreach (Transform b in batons) b.gameObject.SetActive(false);
                int w = 0; // l'arme de mêlée reste la même (matraque / batte) : les niveaux 2 et 3 ajoutent une arme à feu
                if (trail != null) trail.emitting = false;
                baton = batons[w];
                batonTrail = batonTrails[w];
            }
        }

        /// <summary>Niveau d'arme sur la matraque renforcée : plus grosse, bagues dorées, puis noyau électrique et traînée bleue.</summary>
        public void SetHeavyLevel(int lv)
        {
            if (heavyBaton == null) return;
            heavyBaton.localScale = Vector3.one * (1f + 0.1f * Mathf.Clamp(lv, 0, 3));
            if (heavyLv2 != null) heavyLv2.gameObject.SetActive(lv >= 2);
            if (heavyLv3 != null) heavyLv3.gameObject.SetActive(lv >= 3);
            if (heavyTrail != null)
            {
                Color c = lv >= 3 ? new Color(0.4f, 0.85f, 1f) : lv >= 2 ? new Color(1f, 0.8f, 0.2f) : new Color(1f, 0.45f, 0.1f);
                heavyTrail.startColor = c;
                heavyTrail.endColor = new Color(c.r, c.g, c.b, 0f);
                heavyTrail.widthMultiplier = 0.32f * (1f + 0.15f * lv);
            }
        }

        /// <summary>Arme de la map 2 : la matraque renforcée remplace les autres. À appeler après SetLevels.</summary>
        public void SetHeavy(bool on)
        {
            heavy = on;
            if (heavyBaton == null) return;
            if (trail != null) trail.emitting = false;
            if (on)
            {
                foreach (Transform b in batons) if (b != null) b.gameObject.SetActive(false);
                baton = heavyBaton;
                batonTrail = heavyTrail;
            }
            else
            {
                heavyBaton.gameObject.SetActive(false);
                if (baton == heavyBaton) { baton = batons[0]; batonTrail = batonTrails[0]; }
            }
        }

        /// <summary>Protections des adversaires résistants : 1 casque gris, 2 + plastron. Elles sautent une à une sous les coups.</summary>
        static void AddArmor(Rig r, int level)
        {
            Material steel = Art.Mat(new Color(0.55f, 0.58f, 0.62f), 0.75f, 0.7f);
            Material dark = Art.Mat(new Color(0.3f, 0.32f, 0.36f), 0.6f, 0.5f);
            Transform h = Art.Node(r.neck, "armorHelmet", Vector3.zero);
            Art.Part(h, Art.Sphere, steel, new Vector3(0f, 0.27f, -0.01f), new Vector3(0.39f, 0.32f, 0.39f));
            Art.Part(h, Art.Cube, dark, new Vector3(0f, 0.2f, 0.17f), new Vector3(0.05f, 0.14f, 0.04f));
            r.armor.Add(h);
            if (level >= 2)
            {
                Transform c = Art.Node(r.spine, "armorChest", Vector3.zero);
                Art.Part(c, Art.Cube, steel, new Vector3(0f, 0.27f, 0.02f), new Vector3(0.56f, 0.46f, 0.38f));
                Art.Part(c, Art.Sphere, dark, new Vector3(-0.3f, 0.45f, 0f), new Vector3(0.2f, 0.14f, 0.24f));
                Art.Part(c, Art.Sphere, dark, new Vector3(0.3f, 0.45f, 0f), new Vector3(0.2f, 0.14f, 0.24f));
                r.armor.Add(c);
            }
        }

        /// <summary>Une protection saute (coup qui ne met pas encore K.-O.).</summary>
        /// <summary>
        /// Protections des adversaires en 3D : au lieu de la boule grise, un vrai casque intégral (policier : casque d'élite,
        /// pillard : casque de moto) et un gilet (pare-balles / gilet tagué). Elles sautent toujours sous les coups.
        /// </summary>
        static void ArmorModels(Rig r, bool guard)
        {
            foreach (Transform a in r.armor)
            {
                if (a == null) continue;
                if (a.name == "armorHelmet")
                    ReplaceSlot(a, guard ? "casque_police_niv3" : "casque_voleur_niv3", 0.39f, 0, new Vector3(0f, 0.23f, 0f), 0f, 0f);
                else if (a.name == "armorChest")
                {
                    Transform m = ReplaceSlot(a, guard ? "gilet_police_niv2" : "gilet_voleur_niv3", 0.74f, 0, new Vector3(0f, 0.27f, 0f), 0f, 0f);
                    if (m != null) FitTorso(m);
                }
            }
        }

        public void ShedArmor(Vector3 push)
        {
            if (armor.Count == 0) return;
            Transform a = armor[armor.Count - 1];
            armor.RemoveAt(armor.Count - 1);
            if (a == null) return;
            if (Tuning.Arcade) { Fx.Sparkle(a.position); Object.Destroy(a.gameObject); return; }   // arcade : pas de physique (à-coups)
            a.SetParent(root.parent, true);
            var col = a.gameObject.AddComponent<SphereCollider>();
            col.radius = 0.2f;
            var rbA = a.gameObject.AddComponent<Rigidbody>();
            rbA.mass = 3f;
            rbA.AddForce(push + Vector3.up * 4f, ForceMode.VelocityChange);
            rbA.AddTorque(new Vector3(Random.Range(-10f, 10f), Random.Range(-10f, 10f), Random.Range(-10f, 10f)), ForceMode.VelocityChange);
            Object.Destroy(a.gameObject, 2.5f);
        }

        /// <summary>Petit recul du buste à chaque tir de mitraillette.</summary>
        public void Recoil() { recoil = 1f; }

        /// <summary>Affiche l'équipement : 0 casque, 1 gilet, 2 matraque, 3 bouclier, 4 pistolet, 5 mitraillette.</summary>
        /// <summary>
        /// Arcade : un membre de la bande prend la même arme que le héros. 0 batte, 1 pistolet (en plus de la batte),
        /// 2 et plus mitraillette (la batte est rangée). Les armes sont créées à la première demande.
        /// </summary>
        public void CrewWeapon(int tier)
        {
            Transform handL = skin3d != null ? skin3d.RelayOf(this.handL) : this.handL;
            Transform handR = skin3d != null ? skin3d.RelayOf(this.handR) : spine;
            if (tier >= 1 && pistol == null && handL != null)
            {
                pistol = Art.Node(handL, "pistol", Vector3.zero);
                Material pBody = Art.Mat(new Color(1f, 0.31f, 0.64f), 0.5f);
                Material pTip = Art.Glow(new Color(0.25f, 0.85f, 0.45f), new Color(0.3f, 1.2f, 0.5f), 0.5f);
                Art.Part(pistol, Art.Cube, pBody, new Vector3(0f, -0.02f, 0.1f), new Vector3(0.07f, 0.09f, 0.26f), default, false);
                Art.Part(pistol, Art.Cube, pBody, new Vector3(0f, -0.1f, 0.02f), new Vector3(0.06f, 0.14f, 0.07f), new Vector3(-15f, 0f, 0f), false);
                Art.Part(pistol, Art.Sphere, pTip, new Vector3(0f, 0.07f, 0.1f), Vector3.one * 0.1f, default, false);
            }
            if (tier >= 2 && smg == null && handR != null)
            {
                smg = Art.Node(handR, "smg", Vector3.zero);
                Material gBody = Art.Mat(new Color(0.54f, 0.24f, 1f), 0.5f);
                Material gAcc = Art.Glow(new Color(0.25f, 0.85f, 0.45f), new Color(0.3f, 1.2f, 0.5f), 0.5f);
                Art.Part(smg, Art.Cube, gBody, new Vector3(0f, 0f, 0.05f), new Vector3(0.1f, 0.13f, 0.5f), default, false);
                Art.Part(smg, Art.Cube, gAcc, new Vector3(0f, -0.13f, 0.08f), new Vector3(0.06f, 0.18f, 0.08f), new Vector3(12f, 0f, 0f), false);
                Art.Part(smg, Art.Sphere, gAcc, new Vector3(0f, 0.13f, -0.05f), Vector3.one * 0.15f, default, false);
            }
            bool s = tier >= 2;
            if (baton != null) baton.gameObject.SetActive(!s);
            if (pistol != null) pistol.gameObject.SetActive(tier == 1);
            if (smg != null) smg.gameObject.SetActive(s);
            aiming = s;
        }

        public void SetGear(bool[] g)
        {
            bool smgOn = g.Length > 5 && g[5];
            bool hOn = g[0] || (PreviewHelmetLv > 0 && !gearThief && helmet != null && gearPreview);
            if (helmet != null) helmet.gameObject.SetActive(hOn);
            if (cap != null) cap.gameObject.SetActive(!hOn);
            if (gilet != null) gilet.gameObject.SetActive(g[1]);
            if (baton != null && batonTrail != null)
            {
                foreach (Transform b in batons) if (b != null && b != baton) b.gameObject.SetActive(false);
                if (heavyBaton != null && heavyBaton != baton) heavyBaton.gameObject.SetActive(false);
                baton.gameObject.SetActive(g[2] && !smgOn);
            }
            if (shield != null) shield.gameObject.SetActive(g[3] && !smgOn);
            if (pistol != null) pistol.gameObject.SetActive(g[4] && !smgOn);
            if (holster != null) holster.gameObject.SetActive(g[4]);
            if (smg != null) smg.gameObject.SetActive(smgOn);
            aiming = smgOn;
            if (batonTrail != null)
            {
                if (trail != null) trail.emitting = false;
                trail = g[2] && !smgOn ? batonTrail : fistTrail;
            }
        }

        /// <summary>Casseur cagoulé tout en noir, sac à dos noir. Le chef : plus grand, chaîne en or, gros sac.</summary>
        static void DressThief(Rig r, int variant, bool boss)
        {
            Material black = Art.Mat(new Color(0.05f, 0.05f, 0.06f), 0.3f);
            Material knit = Art.Mat(new Color(0.08f, 0.08f, 0.09f), 0.12f);
            Material gold = Art.Glow(Pal.Gold, Pal.Gold * 0.6f, 0.9f);
            Material skin = Art.Mat(Pal.Skin(), 0.35f);

            if (!boss && variant < 0)
            {
                // le voleur joué : visage découvert, bonnet noir à revers, capuche rouge dans le dos, cordons blancs
                Material red = Art.Mat(new Color(0.88f, 0.16f, 0.13f), 0.3f);
                Material cord = Art.Mat(new Color(0.95f, 0.95f, 0.93f), 0.4f);
                Art.Part(r.neck, Art.Sphere, knit, new Vector3(0f, 0.27f, -0.01f), new Vector3(0.33f, 0.24f, 0.33f));
                Art.Part(r.neck, Art.Cylinder, knit, new Vector3(0f, 0.27f, -0.005f), new Vector3(0.335f, 0.035f, 0.335f));
                Art.Part(r.neck, Art.Sphere, knit, new Vector3(0f, 0.39f, -0.02f), Vector3.one * 0.07f, default, false);
                Art.Part(r.spine, Art.Sphere, red, new Vector3(0f, 0.47f, -0.15f), new Vector3(0.38f, 0.18f, 0.22f));
                Art.Part(r.spine, Art.Cube, cord, new Vector3(-0.06f, 0.33f, 0.135f), new Vector3(0.02f, 0.15f, 0.02f), default, false);
                Art.Part(r.spine, Art.Cube, cord, new Vector3(0.06f, 0.33f, 0.135f), new Vector3(0.02f, 0.15f, 0.02f), default, false);
                // poche kangourou, sac à dos gris foncé
                Art.Part(r.spine, Art.Cube, Art.Mat(new Color(0.72f, 0.12f, 0.1f), 0.3f), new Vector3(0f, 0.1f, 0.13f), new Vector3(0.3f, 0.12f, 0.02f), default, false);
                Art.Part(r.spine, Art.Cube, Art.Mat(new Color(0.16f, 0.16f, 0.19f), 0.35f), new Vector3(0f, 0.28f, -0.22f), new Vector3(0.34f, 0.4f, 0.16f));
                AddGear(r, true);
                return;
            }
            // cagoule : tête et cou couverts, seule une fente laisse voir les yeux
            Art.Part(r.neck, Art.Sphere, knit, new Vector3(0f, 0.2f, 0.005f), new Vector3(0.335f, 0.35f, 0.335f));
            Art.Part(r.neck, Art.Cylinder, knit, new Vector3(0f, 0.04f, 0f), new Vector3(0.17f, 0.07f, 0.17f));
            Art.Part(r.neck, Art.Cube, skin, new Vector3(0f, 0.215f, 0.155f), new Vector3(0.22f, 0.075f, 0.04f), default, false);
            Art.Part(r.neck, Art.Sphere, knit, new Vector3(0f, 0.37f, -0.01f), new Vector3(0.12f, 0.06f, 0.12f), default, false);
            Art.Part(r.neck, Art.Sphere, knit, new Vector3(0f, 0.17f, 0.157f), new Vector3(0.085f, 0.09f, 0.08f), default, false);
            Art.Part(r.neck, Art.Sphere, knit, new Vector3(-0.155f, 0.19f, 0f), new Vector3(0.065f, 0.095f, 0.075f), default, false);
            Art.Part(r.neck, Art.Sphere, knit, new Vector3(0.155f, 0.19f, 0f), new Vector3(0.065f, 0.095f, 0.075f), default, false);
            foreach (Transform w in r.whites) w.localPosition += new Vector3(0f, 0f, 0.03f);
            foreach (Transform e in r.eyes) e.localPosition += new Vector3(0f, 0f, 0.03f);
            // capuche rabattue dans le dos, cordons
            Art.Part(r.spine, Art.Sphere, knit, new Vector3(0f, 0.47f, -0.14f), new Vector3(0.36f, 0.16f, 0.2f));
            Art.Part(r.spine, Art.Cube, Art.Mat(new Color(0.2f, 0.2f, 0.22f), 0.3f), new Vector3(-0.06f, 0.33f, 0.135f), new Vector3(0.015f, 0.14f, 0.015f), default, false);
            Art.Part(r.spine, Art.Cube, Art.Mat(new Color(0.2f, 0.2f, 0.22f), 0.3f), new Vector3(0.06f, 0.33f, 0.135f), new Vector3(0.015f, 0.14f, 0.015f), default, false);

            if (!boss)
            {
                // sac à dos noir
                Art.Part(r.spine, Art.Cube, black, new Vector3(0f, 0.28f, -0.22f), new Vector3(0.36f, 0.42f, 0.18f));
                Art.Part(r.spine, Art.Cube, black, new Vector3(0f, 0.18f, -0.32f), new Vector3(0.28f, 0.16f, 0.06f));
            }
            else
            {
                // le chef : chaîne en or, gros sac noir marqué d'un $ doré
                Art.Part(r.spine, Art.Cube, gold, new Vector3(0f, 0.36f, 0.17f), new Vector3(0.22f, 0.04f, 0.02f), default, false);
                Art.Part(r.spine, Art.Sphere, gold, new Vector3(0f, 0.28f, 0.18f), Vector3.one * 0.09f, default, false);
                Art.Part(r.spine, Art.Sphere, black, new Vector3(0f, 0.4f, -0.38f), new Vector3(0.8f, 0.85f, 0.6f));
                Art.Part(r.spine, Art.Cube, gold, new Vector3(0f, 0.42f, -0.69f), new Vector3(0.18f, 0.24f, 0.02f), default, false);
            }
            if (!boss && variant < 0)
            {
                // le casseur joué : il vole l'équipement des vigiles en route
                AddGear(r, true);
            }
            if (!boss && variant == Crew)
            {
                // casseur de ta bande : batte de baseball en main, bandeau rouge sur le bras
                r.baton = Art.Node(r.handR, "bat", Vector3.zero);
                Material wood = Art.Mat(new Color(0.72f, 0.52f, 0.3f), 0.45f);
                Art.Part(r.baton, Art.Cylinder, wood, new Vector3(0f, 0f, 0.2f), new Vector3(0.05f, 0.2f, 0.05f), new Vector3(90f, 0f, 0f));
                Art.Part(r.baton, Art.Cylinder, wood, new Vector3(0f, 0f, 0.5f), new Vector3(0.085f, 0.16f, 0.085f), new Vector3(90f, 0f, 0f));
                r.trail = Trail(Art.Node(r.baton, "tip", new Vector3(0f, 0f, 0.66f)), new Color(1f, 0.45f, 0.35f), 0.16f);
                ReplaceSlot(r.baton, "arme_voleur_niv1", 0.7f, 1, new Vector3(0f, 0f, 0.27f), 90f, 0f);
                Art.Part(r.shL, Art.Cylinder, Art.Glow(new Color(0.2f, 0.5f, 1f), new Color(0.1f, 0.35f, 1f)), new Vector3(0f, -0.14f, 0f), new Vector3(0.16f, 0.04f, 0.16f), default, false);
            }
            if (!boss && variant % 2 == 1)
            {
                // boîte de produit à lancer (chaussures, téléphone…)
                r.bar = Art.Node(r.handR, "box", Vector3.zero);
                Art.Part(r.bar, Art.Cube, Art.Mat(new Color(0.95f, 0.55f, 0.15f), 0.3f), new Vector3(0f, -0.05f, 0.08f), new Vector3(0.28f, 0.16f, 0.2f));
                ReplaceSlot(r.bar, "obs_caisse", 0.26f, 0, new Vector3(0f, -0.05f, 0.08f), 0f, 0f);
            }
        }

        /// <summary>
        /// Le héros (policier ou voleur joué) : grosse tête façon figurine, sourcils et sourire, mains et chaussures plus
        /// généreuses, petits détails d'uniforme. Les équipements accrochés à la tête suivent la nouvelle taille.
        /// </summary>
        static void HeroStyle(Rig r, bool thief)
        {
            r.neck.localScale = Vector3.one * 1.3f;
            r.neck.localPosition += new Vector3(0f, -0.02f, 0f);
            Material dark = Art.Mat(new Color(0.12f, 0.08f, 0.06f), 0.4f);
            for (int i = -1; i <= 1; i += 2)
                Art.Part(r.neck, Art.Cube, dark, new Vector3(i * 0.066f, 0.268f, 0.15f), new Vector3(0.07f, 0.018f, 0.02f), new Vector3(0f, 0f, i * (thief ? 12f : -6f)), false);
            // sourire (de biais pour le voleur : air malicieux)
            Art.Part(r.neck, Art.Cube, dark, new Vector3(thief ? 0.02f : 0f, 0.118f, 0.152f), new Vector3(0.09f, 0.016f, 0.02f), new Vector3(0f, 0f, thief ? 10f : 0f), false);
            Art.Part(r.neck, Art.Cube, dark, new Vector3(-0.045f, 0.126f, 0.15f), new Vector3(0.025f, 0.014f, 0.02f), new Vector3(0f, 0f, -35f), false);
            Art.Part(r.neck, Art.Cube, dark, new Vector3(thief ? 0.07f : 0.045f, thief ? 0.135f : 0.126f, 0.15f), new Vector3(0.025f, 0.014f, 0.02f), new Vector3(0f, 0f, 35f), false);
            // joues
            Material blush = Art.Mat(new Color(0.95f, 0.55f, 0.5f), 0.3f);
            for (int i = -1; i <= 1; i += 2)
                Art.Part(r.neck, Art.Sphere, blush, new Vector3(i * 0.095f, 0.15f, 0.12f), new Vector3(0.05f, 0.03f, 0.03f), default, false);
            // mains et pieds plus généreux
            foreach (Transform h in new[] { r.handL, r.handR })
                for (int c = 0; c < h.childCount; c++) if (h.GetChild(c).name == "Sphere") h.GetChild(c).localScale *= 1.25f;
            foreach (Transform kn in new[] { r.knL, r.knR })
                for (int c = 0; c < kn.childCount; c++)
                {
                    Transform t = kn.GetChild(c);
                    if (t.name == "Cube") { t.localScale = Vector3.Scale(t.localScale, new Vector3(1.25f, 1.3f, 1.2f)); t.localPosition += new Vector3(0f, -0.005f, 0.01f); }
                }
            if (thief)
            {
                // semelles rouges des baskets
                Material sole = Art.Mat(new Color(0.88f, 0.16f, 0.13f), 0.4f);
                foreach (Transform kn in new[] { r.knL, r.knR })
                    Art.Part(kn, Art.Cube, sole, new Vector3(0f, -0.5f, 0.06f), new Vector3(0.19f, 0.03f, 0.35f), default, false);
            }
            else
            {
                // étoile dorée sur la poitrine, épaulettes, ceinturon
                Material gold = Art.Glow(Pal.Gold, Pal.Gold * 0.6f, 0.9f);
                Art.Part(r.spine, Art.Cube, gold, new Vector3(-0.12f, 0.36f, 0.137f), new Vector3(0.09f, 0.09f, 0.02f), new Vector3(0f, 0f, 45f), false);
                Art.Part(r.spine, Art.Cube, gold, new Vector3(-0.12f, 0.36f, 0.137f), new Vector3(0.09f, 0.09f, 0.02f), default, false);
                Material navy = Art.Mat(new Color(0.08f, 0.1f, 0.22f), 0.4f);
                Art.Part(r.shL, Art.Cube, navy, new Vector3(0f, 0.03f, 0f), new Vector3(0.18f, 0.03f, 0.14f), default, false);
                Art.Part(r.shR, Art.Cube, navy, new Vector3(0f, 0.03f, 0f), new Vector3(0.18f, 0.03f, 0.14f), default, false);
            }
        }

        // ------------------------------------------------------------------ tenues des boss

        static void Torso(Rig r, Material m, float bulk = 1f)
        {
            Art.Part(r.spine, Art.Cube, m, new Vector3(0f, 0.26f, 0f), new Vector3(0.5f * bulk, 0.5f, 0.32f * bulk));
            Art.Part(r.spine, Art.Sphere, m, new Vector3(0f, 0.45f, 0f), new Vector3(0.58f * bulk, 0.22f, 0.34f * bulk));
        }

        static void Sleeves(Rig r, Material upper, Material lower, float bulk = 1f)
        {
            foreach (Transform sh in new[] { r.shL, r.shR })
            {
                Art.Part(sh, Art.Sphere, upper, new Vector3(0f, -0.03f, 0f), Vector3.one * 0.2f * bulk);
                Art.Part(sh, Art.Capsule, upper, new Vector3(0f, -0.15f, 0f), new Vector3(0.17f * bulk, 0.17f, 0.17f * bulk));
            }
            foreach (Transform el in new[] { r.elL, r.elR })
                Art.Part(el, Art.Capsule, lower, new Vector3(0f, -0.14f, 0f), new Vector3(0.15f * bulk, 0.16f, 0.15f * bulk));
        }

        static void Shades(Rig r)
        {
            Material black = Art.Mat(new Color(0.03f, 0.03f, 0.04f), 0.9f, 0.3f);
            Art.Part(r.neck, Art.Cube, black, new Vector3(0f, 0.22f, 0.17f), new Vector3(0.28f, 0.07f, 0.05f), default, false);
        }

        static void Mascot(Rig r, Color fur, Color belly)
        {
            Material f = Art.Mat(fur, 0.15f), b = Art.Mat(belly, 0.15f), black = Art.Mat(new Color(0.05f, 0.05f, 0.06f), 0.8f);
            Torso(r, f, 1.25f);
            Art.Part(r.spine, Art.Sphere, b, new Vector3(0f, 0.22f, 0.13f), new Vector3(0.42f, 0.42f, 0.2f));
            Sleeves(r, f, f, 1.2f);
            Art.Part(r.handL, Art.Sphere, f, Vector3.zero, Vector3.one * 0.16f);
            Art.Part(r.handR, Art.Sphere, f, Vector3.zero, Vector3.one * 0.16f);
            Art.Part(r.neck, Art.Sphere, f, new Vector3(0f, 0.26f, 0f), new Vector3(0.64f, 0.6f, 0.6f));
            Art.Part(r.neck, Art.Sphere, f, new Vector3(-0.25f, 0.52f, 0f), new Vector3(0.2f, 0.2f, 0.12f));
            Art.Part(r.neck, Art.Sphere, f, new Vector3(0.25f, 0.52f, 0f), new Vector3(0.2f, 0.2f, 0.12f));
            Art.Part(r.neck, Art.Sphere, b, new Vector3(0f, 0.17f, 0.27f), new Vector3(0.28f, 0.2f, 0.14f));
            Art.Part(r.neck, Art.Sphere, black, new Vector3(0f, 0.22f, 0.34f), new Vector3(0.08f, 0.06f, 0.05f), default, false);
            Art.Part(r.neck, Art.Sphere, black, new Vector3(-0.11f, 0.34f, 0.26f), new Vector3(0.08f, 0.1f, 0.05f), default, false);
            Art.Part(r.neck, Art.Sphere, black, new Vector3(0.11f, 0.34f, 0.26f), new Vector3(0.08f, 0.1f, 0.05f), default, false);
            foreach (Transform e in r.eyes) e.gameObject.SetActive(false);
            foreach (Transform w in r.whites) w.gameObject.SetActive(false);
        }

        /// <summary>
        /// Tenues des boss, toutes différentes. Voleurs : 0 le parrain, 1 la brute, 2 le chef de bande, 3 le faux Père Noël,
        /// 4 le roller, 5 la mascotte ours. Vigiles : 6 le chef de la sécurité, 7 le videur, 8 le capitaine anti-émeute,
        /// 9 le directeur, 10 le vigile en gyropode, 11 la mascotte chien.
        /// </summary>
        static void DressBoss(Rig r, int look)
        {
            Material gold = Art.Glow(Pal.Gold, Pal.Gold * 0.6f, 0.9f);
            Material white = Art.Mat(new Color(0.95f, 0.95f, 0.93f), 0.3f);
            Material skin = Art.Mat(Pal.Skin(), 0.35f);
            switch (look)
            {
                case 0:
                {
                    Material suit = Art.Mat(new Color(0.13f, 0.13f, 0.15f), 0.45f);
                    Torso(r, suit);
                    Sleeves(r, suit, suit);
                    Art.Part(r.spine, Art.Cube, white, new Vector3(0f, 0.38f, 0.165f), new Vector3(0.14f, 0.2f, 0.01f), default, false);
                    Art.Part(r.spine, Art.Cube, gold, new Vector3(0f, 0.32f, 0.17f), new Vector3(0.26f, 0.04f, 0.02f), default, false);
                    Art.Part(r.spine, Art.Sphere, gold, new Vector3(0f, 0.24f, 0.18f), Vector3.one * 0.1f, default, false);
                    Shades(r);
                    Material hat = Art.Mat(new Color(0.18f, 0.16f, 0.15f), 0.3f);
                    Art.Part(r.neck, Art.Cylinder, hat, new Vector3(0f, 0.33f, 0f), new Vector3(0.46f, 0.015f, 0.46f));
                    Art.Part(r.neck, Art.Cylinder, hat, new Vector3(0f, 0.41f, 0f), new Vector3(0.28f, 0.08f, 0.28f));
                    Art.Part(r.neck, Art.Cylinder, Art.Mat(new Color(0.7f, 0.1f, 0.12f), 0.3f), new Vector3(0f, 0.36f, 0f), new Vector3(0.285f, 0.025f, 0.285f), default, false);
                    break;
                }
                case 1:
                case 7:
                {
                    // brute / videur : torse énorme, bras nus musclés, crâne rasé
                    Material top = look == 1 ? Art.Mat(new Color(0.8f, 0.14f, 0.12f), 0.3f) : Art.Mat(new Color(0.08f, 0.08f, 0.09f), 0.3f);
                    Torso(r, top, 1.3f);
                    Sleeves(r, skin, skin, 1.35f);
                    Art.Part(r.hips, Art.Cube, look == 1 ? gold : Art.Mat(new Color(0.15f, 0.15f, 0.17f), 0.5f), new Vector3(0f, 0.07f, 0f), new Vector3(0.5f, 0.12f, 0.32f));
                    if (look == 1) Art.Part(r.neck, Art.Cube, Art.Mat(new Color(0.05f, 0.05f, 0.06f), 0.3f), new Vector3(0f, 0.22f, 0.155f), new Vector3(0.3f, 0.08f, 0.04f), default, false);
                    else
                    {
                        Shades(r);
                        Art.Part(r.spine, Art.Cube, Art.Glow(Color.white, new Color(0.5f, 0.5f, 0.5f)), new Vector3(0f, 0.36f, 0.21f), new Vector3(0.4f, 0.08f, 0.01f), default, false);
                    }
                    break;
                }
                case 2:
                {
                    // chef de bande : blouson de cuir, crête verte, foulard rouge sur le visage
                    Material leather = Art.Mat(new Color(0.32f, 0.18f, 0.1f), 0.6f);
                    Torso(r, leather, 1.1f);
                    Sleeves(r, leather, leather, 1.1f);
                    Material red = Art.Mat(new Color(0.85f, 0.12f, 0.12f), 0.3f);
                    Art.Part(r.neck, Art.Cube, red, new Vector3(0f, 0.13f, 0.14f), new Vector3(0.28f, 0.1f, 0.06f), default, false);
                    Art.Part(r.neck, Art.Sphere, Art.Mat(new Color(0.15f, 0.12f, 0.1f), 0.2f), new Vector3(0f, 0.27f, -0.01f), new Vector3(0.31f, 0.2f, 0.31f));
                    Material mohawk = Art.Glow(new Color(0.2f, 0.9f, 0.3f), new Color(0.1f, 0.5f, 0.15f));
                    for (int i = 0; i < 4; i++)
                        Art.Part(r.neck, Art.Cube, mohawk, new Vector3(0f, 0.4f - Mathf.Abs(i - 1.5f) * 0.02f, 0.1f - i * 0.07f), new Vector3(0.05f, 0.14f, 0.06f), new Vector3(-20f + i * 12f, 0f, 0f));
                    Art.Part(r.spine, Art.Cube, gold, new Vector3(0f, 0.32f, 0.2f), new Vector3(0.24f, 0.04f, 0.02f), default, false);
                    break;
                }
                case 3:
                {
                    // faux Père Noël : costume rouge bordé de blanc, barbe, bonnet, hotte pleine de cadeaux volés
                    Material red = Art.Mat(new Color(0.85f, 0.1f, 0.12f), 0.3f);
                    Torso(r, red, 1.2f);
                    Sleeves(r, red, red, 1.1f);
                    Art.Part(r.spine, Art.Cube, white, new Vector3(0f, 0.26f, 0.19f), new Vector3(0.1f, 0.5f, 0.02f), default, false);
                    Art.Part(r.hips, Art.Cube, Art.Mat(new Color(0.05f, 0.05f, 0.06f), 0.5f), new Vector3(0f, 0.08f, 0f), new Vector3(0.56f, 0.09f, 0.36f));
                    Art.Part(r.hips, Art.Cube, gold, new Vector3(0f, 0.08f, 0.185f), new Vector3(0.12f, 0.08f, 0.02f), default, false);
                    Art.Part(r.neck, Art.Sphere, white, new Vector3(0f, 0.1f, 0.11f), new Vector3(0.3f, 0.26f, 0.18f));
                    Art.Part(r.neck, Art.Cylinder, white, new Vector3(0f, 0.31f, 0f), new Vector3(0.34f, 0.04f, 0.34f));
                    Art.Part(r.neck, Art.Cylinder, red, new Vector3(0f, 0.42f, -0.04f), new Vector3(0.26f, 0.1f, 0.26f), new Vector3(-25f, 0f, 0f));
                    Art.Part(r.neck, Art.Sphere, white, new Vector3(0f, 0.5f, -0.16f), Vector3.one * 0.1f);
                    Art.Part(r.spine, Art.Sphere, Art.Mat(new Color(0.55f, 0.4f, 0.25f), 0.15f), new Vector3(0f, 0.42f, -0.42f), new Vector3(0.8f, 0.85f, 0.6f));
                    Color[] gifts = { new Color(0.2f, 0.6f, 0.95f), new Color(0.3f, 0.85f, 0.4f), new Color(0.98f, 0.82f, 0.2f) };
                    for (int i = 0; i < 3; i++)
                        Art.Part(r.spine, Art.Cube, Art.Mat(gifts[i], 0.4f), new Vector3((i - 1) * 0.2f, 0.85f, -0.4f + (i % 2) * 0.08f), Vector3.one * 0.2f, new Vector3(0f, i * 25f, 10f));
                    break;
                }
                case 4:
                {
                    // roller : survêtement fluo, bandeau, patins à roulettes
                    Material track = Art.Mat(new Color(0.1f, 0.8f, 0.85f), 0.4f);
                    Torso(r, track);
                    Sleeves(r, track, track);
                    Art.Part(r.spine, Art.Cube, white, new Vector3(-0.2f, 0.26f, 0.16f), new Vector3(0.04f, 0.5f, 0.01f), default, false);
                    Art.Part(r.spine, Art.Cube, white, new Vector3(0.2f, 0.26f, 0.16f), new Vector3(0.04f, 0.5f, 0.01f), default, false);
                    Art.Part(r.neck, Art.Cylinder, Art.Mat(new Color(0.95f, 0.2f, 0.5f), 0.3f), new Vector3(0f, 0.27f, 0f), new Vector3(0.32f, 0.03f, 0.32f));
                    Shades(r);
                    foreach (Transform kn in new[] { r.knL, r.knR })
                    {
                        Art.Part(kn, Art.Cube, Art.Mat(new Color(0.95f, 0.2f, 0.5f), 0.5f), new Vector3(0f, -0.52f, 0.05f), new Vector3(0.14f, 0.04f, 0.32f));
                        for (int w = 0; w < 2; w++)
                            Art.Part(kn, Art.Cylinder, Art.Mat(new Color(0.98f, 0.85f, 0.2f), 0.6f), new Vector3(0f, -0.57f, -0.07f + w * 0.24f), new Vector3(0.07f, 0.08f, 0.07f), new Vector3(0f, 0f, 90f));
                    }
                    break;
                }
                case 5: Mascot(r, new Color(0.55f, 0.35f, 0.2f), new Color(0.92f, 0.82f, 0.62f)); break;
                case 11: Mascot(r, new Color(0.35f, 0.55f, 0.95f), new Color(0.95f, 0.95f, 0.95f)); break;
                case 6:
                case 9:
                {
                    // chef de la sécurité (costume marine, oreillette) / directeur (costume gris, cravate, porte-bloc)
                    Material suit = look == 6 ? Art.Mat(new Color(0.1f, 0.13f, 0.25f), 0.45f) : Art.Mat(new Color(0.45f, 0.46f, 0.5f), 0.4f);
                    Torso(r, suit);
                    Sleeves(r, suit, suit);
                    Art.Part(r.spine, Art.Cube, white, new Vector3(0f, 0.38f, 0.165f), new Vector3(0.14f, 0.2f, 0.01f), default, false);
                    Art.Part(r.spine, Art.Cube, look == 6 ? Art.Mat(new Color(0.05f, 0.05f, 0.06f), 0.4f) : Art.Mat(new Color(0.85f, 0.12f, 0.14f), 0.4f),
                             new Vector3(0f, 0.33f, 0.172f), new Vector3(0.05f, 0.24f, 0.01f), default, false);
                    Art.Part(r.neck, Art.Sphere, Art.Mat(look == 6 ? new Color(0.12f, 0.1f, 0.08f) : new Color(0.7f, 0.7f, 0.72f), 0.2f), new Vector3(0f, 0.28f, -0.02f), new Vector3(0.32f, 0.2f, 0.32f));
                    if (look == 6)
                    {
                        Shades(r);
                        Art.Part(r.spine, Art.Cube, gold, new Vector3(-0.15f, 0.36f, 0.17f), new Vector3(0.08f, 0.09f, 0.02f), default, false);
                        Art.Part(r.neck, Art.Cylinder, Art.Mat(new Color(0.05f, 0.05f, 0.06f), 0.5f), new Vector3(0.16f, 0.2f, 0f), new Vector3(0.04f, 0.03f, 0.04f), new Vector3(0f, 0f, 90f), false);
                    }
                    else
                    {
                        Art.Part(r.neck, Art.Cube, Art.Mat(new Color(0.05f, 0.05f, 0.06f), 0.5f), new Vector3(0f, 0.22f, 0.16f), new Vector3(0.26f, 0.025f, 0.03f), default, false);
                        Art.Part(r.handL, Art.Cube, Art.Mat(new Color(0.55f, 0.38f, 0.2f), 0.3f), new Vector3(0f, -0.06f, 0.12f), new Vector3(0.04f, 0.3f, 0.22f));
                        Art.Part(r.handL, Art.Cube, white, new Vector3(0.025f, -0.06f, 0.12f), new Vector3(0.01f, 0.26f, 0.19f), default, false);
                    }
                    break;
                }
                case 8:
                {
                    // capitaine anti-émeute : armure, casque à visière, grand bouclier
                    Material navy = Art.Mat(new Color(0.12f, 0.15f, 0.28f), 0.5f);
                    Torso(r, navy, 1.25f);
                    Sleeves(r, navy, navy, 1.2f);
                    Material yellow = Art.Glow(Pal.Jaune, Pal.Jaune * 0.6f, 0.5f);
                    Art.Part(r.spine, Art.Cube, yellow, new Vector3(0f, 0.2f, 0f), new Vector3(0.64f, 0.06f, 0.42f), default, false);
                    Art.Part(r.neck, Art.Sphere, navy, new Vector3(0f, 0.25f, -0.01f), new Vector3(0.4f, 0.38f, 0.4f));
                    Art.Part(r.neck, Art.Sphere, Art.Mat(new Color(0.55f, 0.75f, 0.92f), 0.95f, 0.2f), new Vector3(0f, 0.2f, 0.13f), new Vector3(0.35f, 0.25f, 0.2f));
                    Transform sh = Art.Node(r.elL, "riot", new Vector3(-0.05f, -0.15f, 0.18f));
                    Art.Part(sh, Art.Cube, Art.Mat(new Color(0.7f, 0.85f, 0.95f), 0.92f, 0.1f), Vector3.zero, new Vector3(0.05f, 1.1f, 0.62f));
                    Art.Part(sh, Art.Cube, yellow, new Vector3(-0.03f, 0.3f, 0f), new Vector3(0.02f, 0.08f, 0.5f), default, false);
                    ReplaceSlot(sh, "bouclier_police_niv2", 1.1f, 1, Vector3.zero, 0f, -90f);
                    foreach (Transform e in r.eyes) e.gameObject.SetActive(true);
                    break;
                }
                case 12:
                case 13:
                {
                    // BIG BAT (12) : sweat rouge, bandana, casquette à l'envers, chaîne, batte géante cloutée.
                    // RIOT TITAN (13) : armure anti-émeute massive, casque, matraque géante cerclée d'acier.
                    bool titan = look == 13;
                    Material top = titan ? Art.Mat(new Color(0.12f, 0.15f, 0.28f), 0.5f) : Art.Mat(new Color(0.75f, 0.12f, 0.12f), 0.3f);
                    Torso(r, top, 1.35f);
                    Sleeves(r, top, titan ? top : skin, 1.35f);
                    if (titan)
                    {
                        Material yellowT = Art.Glow(Pal.Jaune, Pal.Jaune * 0.6f, 0.5f);
                        Art.Part(r.spine, Art.Cube, yellowT, new Vector3(0f, 0.2f, 0f), new Vector3(0.7f, 0.06f, 0.46f), default, false);
                        Art.Part(r.neck, Art.Sphere, top, new Vector3(0f, 0.25f, -0.01f), new Vector3(0.42f, 0.4f, 0.42f));
                        Art.Part(r.neck, Art.Sphere, Art.Mat(new Color(0.15f, 0.15f, 0.18f), 0.95f, 0.3f), new Vector3(0f, 0.2f, 0.14f), new Vector3(0.36f, 0.24f, 0.2f));
                        Art.Part(r.shL, Art.Sphere, top, new Vector3(-0.03f, 0.02f, 0f), new Vector3(0.3f, 0.22f, 0.3f));
                        Art.Part(r.shR, Art.Sphere, top, new Vector3(0.03f, 0.02f, 0f), new Vector3(0.3f, 0.22f, 0.3f));
                    }
                    else
                    {
                        Material blackB = Art.Mat(new Color(0.06f, 0.06f, 0.07f), 0.4f);
                        Art.Part(r.neck, Art.Cube, blackB, new Vector3(0f, 0.13f, 0.14f), new Vector3(0.3f, 0.1f, 0.06f), default, false);
                        Art.Part(r.neck, Art.Sphere, blackB, new Vector3(0f, 0.3f, -0.02f), new Vector3(0.33f, 0.16f, 0.33f));
                        Art.Part(r.neck, Art.Cube, blackB, new Vector3(0f, 0.28f, -0.2f), new Vector3(0.24f, 0.03f, 0.14f), new Vector3(8f, 0f, 0f));
                        Art.Part(r.spine, Art.Cube, gold, new Vector3(0f, 0.34f, 0.23f), new Vector3(0.3f, 0.05f, 0.02f), default, false);
                        Art.Part(r.spine, Art.Sphere, gold, new Vector3(0f, 0.25f, 0.24f), Vector3.one * 0.12f, default, false);
                    }
                    // l'arme géante, tenue en main droite
                    Transform bat = Art.Node(r.handR, "bigBat", Vector3.zero);
                    Material wood = titan ? Art.Mat(new Color(0.08f, 0.08f, 0.09f), 0.6f) : Art.Mat(new Color(0.62f, 0.42f, 0.24f), 0.45f);
                    Material nail = Art.Mat(new Color(0.65f, 0.67f, 0.7f), 0.85f, 0.9f);
                    Art.Part(bat, Art.Cylinder, wood, new Vector3(0f, 0f, 0.3f), new Vector3(0.09f, 0.3f, 0.09f), new Vector3(90f, 0f, 0f));
                    Art.Part(bat, Art.Cylinder, wood, new Vector3(0f, 0f, 0.95f), new Vector3(0.2f, 0.42f, 0.2f), new Vector3(90f, 0f, 0f));
                    for (int k = 0; k < 6; k++)
                    {
                        float a = k * Mathf.PI / 3f;
                        Transform nn = Art.Node(bat, "nail", new Vector3(Mathf.Cos(a) * 0.1f, Mathf.Sin(a) * 0.1f, 0.85f + (k % 2) * 0.25f));
                        Art.Part(nn, titan ? Art.Cylinder : Art.Sphere, nail, Vector3.zero, titan ? new Vector3(0.22f, 0.02f, 0.22f) : Vector3.one * 0.06f, titan ? new Vector3(90f, 0f, 0f) : default, false);
                    }
                    r.trail = Trail(Art.Node(bat, "tip", new Vector3(0f, 0f, 1.35f)), titan ? new Color(0.5f, 0.8f, 1f) : new Color(1f, 0.4f, 0.2f), 0.4f);
                    // arme géante 3D : batte (Big Bat) ou matraque cloutée (Riot Titan), à l'échelle
                    if (ReplaceSlot(bat, titan ? "arme_heavy_police" : "arme_voleur_niv1", 1.4f, 1, new Vector3(0f, 0f, 0.6f), 90f, 0f) != null)
                        for (int c = 0; c < bat.childCount; c++) if (bat.GetChild(c).name == "nail") bat.GetChild(c).gameObject.SetActive(false);
                    break;
                }
                case 10:
                {
                    // vigile en gyropode : gilet jaune, casquette, plateforme à deux roues sous les pieds
                    Material yellow = Art.Glow(Pal.Jaune, Pal.Jaune * 0.25f, 0.5f);
                    Material navy = Art.Mat(new Color(0.1f, 0.13f, 0.25f), 0.35f);
                    Torso(r, yellow);
                    Sleeves(r, navy, skin);
                    Art.Part(r.spine, Art.Cube, Art.Glow(Color.white, new Color(0.35f, 0.35f, 0.35f), 0.6f), new Vector3(0f, 0.14f, 0f), new Vector3(0.52f, 0.05f, 0.34f), default, false);
                    Art.Part(r.neck, Art.Sphere, navy, new Vector3(0f, 0.32f, -0.01f), new Vector3(0.33f, 0.15f, 0.33f));
                    Art.Part(r.neck, Art.Cube, navy, new Vector3(0f, 0.29f, 0.18f), new Vector3(0.26f, 0.03f, 0.16f), new Vector3(-8f, 0f, 0f));
                    Shades(r);
                    Material dark = Art.Mat(new Color(0.12f, 0.12f, 0.14f), 0.6f), tire = Art.Mat(new Color(0.05f, 0.05f, 0.06f), 0.4f);
                    Art.Part(r.root, Art.Cube, dark, new Vector3(0f, 0.12f, 0.02f), new Vector3(0.6f, 0.07f, 0.34f));
                    Art.Part(r.root, Art.Cylinder, tire, new Vector3(-0.38f, 0.16f, 0.02f), new Vector3(0.32f, 0.05f, 0.32f), new Vector3(0f, 0f, 90f));
                    Art.Part(r.root, Art.Cylinder, tire, new Vector3(0.38f, 0.16f, 0.02f), new Vector3(0.32f, 0.05f, 0.32f), new Vector3(0f, 0f, 90f));
                    Art.Part(r.root, Art.Cylinder, dark, new Vector3(0f, 0.62f, 0.28f), new Vector3(0.05f, 0.5f, 0.05f), new Vector3(-12f, 0f, 0f));
                    Art.Part(r.root, Art.Cylinder, dark, new Vector3(0f, 1.1f, 0.38f), new Vector3(0.05f, 0.25f, 0.05f), new Vector3(0f, 0f, 90f));
                    Art.Part(r.root, Art.Cube, Art.Glow(new Color(0.2f, 0.5f, 1f), new Color(0.4f, 0.9f, 2f)), new Vector3(0f, 0.12f, 0.2f), new Vector3(0.3f, 0.03f, 0.01f), default, false);
                    break;
                }
            }
        }

        /// <summary>Client pressé : manteau coloré, cheveux ou bonnet, sacs de courses à la main.</summary>
        static void DressShopper(Rig r, int variant)
        {
            if (variant % 3 == 0)
                Art.Part(r.neck, Art.Sphere, Art.Mat(variant % 2 == 0 ? new Color(0.9f, 0.25f, 0.3f) : new Color(0.25f, 0.4f, 0.8f), 0.15f), new Vector3(0f, 0.31f, -0.01f), new Vector3(0.32f, 0.2f, 0.32f));
            else
                Art.Part(r.neck, Art.Sphere, Art.Mat(Pal.Hair(), 0.25f), new Vector3(0f, 0.28f, -0.03f), new Vector3(0.31f, 0.2f, 0.31f));
            Color[] bags = { new Color(0.95f, 0.2f, 0.3f), new Color(0.98f, 0.82f, 0.15f), new Color(0.2f, 0.6f, 0.95f), new Color(0.95f, 0.95f, 0.95f) };
            for (int i = 0; i < 2; i++)
            {
                Transform hand = i == 0 ? r.handL : r.handR;
                Material bm = Art.Mat(bags[(variant + i) % bags.Length], 0.3f);
                Art.Part(hand, Art.Cube, bm, new Vector3(0f, -0.24f, 0f), new Vector3(0.12f, 0.36f, 0.32f));
                Art.Part(hand, Art.Cube, Art.Mat(new Color(0.15f, 0.15f, 0.15f), 0.3f), new Vector3(0f, -0.04f, 0f), new Vector3(0.02f, 0.1f, 0.14f), default, false);
            }
            r.sign = r.handL;
        }

        // ------------------------------------------------------------------ animation

        public void Land(float amount) { squash = Mathf.Max(squash, amount); stretch = 0f; }

        /// <summary>Impulsion du saut : le corps s'étire vers le haut.</summary>
        public void Takeoff() { stretch = 0.24f; squash = 0f; }

        /// <summary>Roulade avant au début d'une glissade.</summary>
        public void Roll() { rollT = 0f; }

        /// <summary>Flash de couleur sur tout le corps (impact, dégât), sans toucher aux matériaux partagés.</summary>
        public void Flash(Color c, float dur)
        {
            if (skin == null) return;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            mpb.Clear();
            mpb.SetColor("_BaseColor", c);
            mpb.SetColor("_Color", c);
            foreach (MeshRenderer mr in skin) if (mr != null) mr.SetPropertyBlock(mpb);
            flashT = dur;
        }

        void Unflash()
        {
            foreach (MeshRenderer mr in skin) if (mr != null) mr.SetPropertyBlock(null);
        }

        public void SetVisible(bool v)
        {
            if (v == visible || renderers == null) return;
            visible = v;
            foreach (MeshRenderer mr in renderers) if (mr != null) mr.enabled = v;
            if (skin3d != null) skin3d.SetVisible(v);
        }

        public void Tick(float dt)
        {
            TickCore(dt);
            if (skin3d != null && !skin3d.Hidden)
            {
                skin3d.Apply();
                // mitraillette tenue dans la main du modèle 3D : canon toujours vers l'avant du buste,
                // quelle que soit l'orientation de la main (bras ballants, modèles sans os de doigts…)
                if (smg != null && smg.gameObject.activeInHierarchy && smg.parent != null)
                {
                    smg.rotation = body.rotation * SmgTilt;
                    // la poignée (sous et derrière le centre de l'arme) vient dans la paume, pas le milieu de l'arme
                    smg.localPosition = Quaternion.Inverse(smg.parent.rotation) * (smg.rotation * -SmgGrip);
                }
            }
        }

        void TickCore(float dt)
        {
            t += dt;
            if (shieldPunch > 0f && permShields != null)
            {
                shieldPunch = Mathf.Max(0f, shieldPunch - dt * 4f);
                foreach (Transform ps in permShields) if (ps != null && ps.gameObject.activeSelf) ps.localScale = Vector3.one * (1f + 0.3f * shieldPunch);
            }
            if (stars.gameObject.activeSelf)
            {
                stars.rotation = Quaternion.Euler(0f, t * 400f, 0f);
                if (ko) stars.position = root.position + Vector3.up * 1.2f;
            }
            if (flashT > 0f)
            {
                flashT -= dt;
                if (flashT <= 0f) Unflash();
            }
            if (ko) { TickKO(dt); return; }

            // clignement des yeux
            blinkT -= dt;
            float lid = blinkT < 0.12f ? 0.15f : 1f;
            if (blinkT <= 0f) blinkT = Random.Range(1.8f, 4.5f);
            foreach (Transform e in eyes) e.localScale = new Vector3(0.042f, 0.052f * lid, 0.02f);

            float rate = anim == Anim.Run || anim == Anim.Charge ? 8f + speed * 0.33f : anim == Anim.Walk ? 4.4f + speed * 0.6f : 6f;
            phase += dt * rate;
            float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
            // bras et tête en léger retard sur les jambes (mouvement secondaire)
            float sa = Mathf.Sin(phase - 0.35f), sh = Mathf.Sin(phase * 2f - 0.6f);

            Vector3 hp = new Vector3(0f, HipY, 0f);
            float hipsX = 0f, hipsZ = 0f, spineX = 0f, spineY = 0f, spineZ = 0f, neckX = 0f, neckZ = 0f;
            float hL = -4f, hR = 4f, kL = 8f, kR = 8f;
            float sL = 4f, sR = 4f, eL = -15f, eR = -15f, sLz = -6f, sRz = 6f;
            stepped = false;

            switch (anim)
            {
                case Anim.Run:
                    hL = 46f * s; hR = -46f * s;
                    kL = 12f + 88f * Mathf.Max(0f, -c);
                    kR = 12f + 88f * Mathf.Max(0f, c);
                    sL = -52f * sa; sR = 52f * sa;
                    eL = -84f - 14f * sa; eR = -84f + 14f * sa;
                    spineX = 15f + 3f * sh; spineY = 10f * s; neckX = -13f - 3f * sh;
                    hipsZ = 5f * c; spineZ = -3f * c; neckZ = -2f * c;
                    hp.y = HipY - 0.07f + 0.1f * Mathf.Abs(s);
                    if ((s < 0f) != (prevS < 0f)) stepped = true;
                    break;
                case Anim.Charge:
                    // course de charge : bras levés vers l'avant, buste penché, regard fixé
                    hL = 50f * s; hR = -50f * s;
                    kL = 14f + 90f * Mathf.Max(0f, -c);
                    kR = 14f + 90f * Mathf.Max(0f, c);
                    sL = -118f - 18f * sa; sR = -118f + 18f * sa;
                    eL = -40f; eR = -40f; sLz = -18f; sRz = 18f;
                    spineX = 24f; spineY = 6f * s; neckX = -24f;
                    hipsZ = 5f * c;
                    hp.y = HipY - 0.09f + 0.11f * Mathf.Abs(s);
                    if ((s < 0f) != (prevS < 0f)) stepped = true;
                    break;
                case Anim.Walk:
                    hL = 24f * s; hR = -24f * s;
                    kL = 6f + 34f * Mathf.Max(0f, -c);
                    kR = 6f + 34f * Mathf.Max(0f, c);
                    sL = -24f * sa; sR = 24f * sa;
                    eL = -18f - 6f * sa; eR = -18f + 6f * sa;
                    spineX = 3f; spineY = 5f * s; hipsZ = 3f * c; neckX = -2f + 2f * sh;
                    hp.y = HipY - 0.02f + 0.035f * Mathf.Abs(s);
                    break;
                case Anim.Jump:
                {
                    // montée : jambes repliées, bras levés ; descente : jambes tendues vers le sol, bras écartés
                    float up = Mathf.Clamp01((vy + 5f) / 14f);
                    hL = Mathf.Lerp(-22f, -72f, up); kL = Mathf.Lerp(26f, 112f, up);
                    hR = Mathf.Lerp(14f, -28f, up); kR = Mathf.Lerp(38f, 96f, up);
                    sL = Mathf.Lerp(-70f, -155f, up); sR = Mathf.Lerp(-60f, -145f, up);
                    sLz = Mathf.Lerp(-55f, -12f, up); sRz = Mathf.Lerp(55f, 12f, up);
                    eL = Mathf.Lerp(-20f, -30f, up); eR = Mathf.Lerp(-25f, -35f, up);
                    spineX = Mathf.Lerp(16f, 4f, up); neckX = Mathf.Lerp(-14f, -6f, up);
                    break;
                }
                case Anim.Slide:
                    hipsX = -64f; hp.y = 0.42f;
                    hL = -28f; kL = 8f; hR = 14f; kR = 85f;
                    sL = 40f; sR = 28f; eL = -30f; eR = -40f; sLz = -25f; sRz = 25f;
                    spineX = 22f; neckX = 30f;
                    break;
                case Anim.Cheer:
                    hp.y = HipY - 0.02f + 0.06f * Mathf.Abs(Mathf.Sin(t * 7f + seed));
                    sL = -158f + 14f * Mathf.Sin(t * 7f + seed); sR = -142f + 14f * Mathf.Sin(t * 7f + seed + 1f);
                    eL = -20f; eR = -35f; neckX = -10f + 6f * Mathf.Sin(t * 7f + seed);
                    neckZ = 8f * Mathf.Sin(t * 3.5f + seed);
                    hL = -6f; hR = 6f; kL = 6f; kR = 6f;
                    break;
                case Anim.Smash:
                {
                    float k = Mathf.Repeat(t * 1.8f + seed, 1f);
                    sR = k < 0.62f ? Mathf.Lerp(-30f, -172f, Smooth(k / 0.62f)) : Mathf.Lerp(-172f, -25f, EaseIn((k - 0.62f) / 0.38f));
                    eR = -35f; sL = -40f; eL = -70f;
                    spineX = k < 0.62f ? -6f : 20f; spineY = k < 0.62f ? -14f : 10f;
                    hL = -18f; hR = 16f; kL = 22f; kR = 12f;
                    hp.y = HipY - 0.05f;
                    break;
                }
                case Anim.Stand:
                    // debout bien droit : jambes presque tendues, buste et tête relevés
                    hL = -2f; hR = 3f; kL = 1f; kR = 1f; sL = 0f; eL = -2f; sR = 0f; eR = -3f;
                    sLz = -1f; sRz = 1f;   // bras le long du corps
                    spineX = -7f; neckX = -11f;   // buste redressé, tête haute
                    hp.y = HipY - 0.004f + 0.006f * Mathf.Sin(t * 2.2f + seed);
                    neckZ = 6f * Mathf.Sin(t * 0.9f + seed);
                    break;
                case Anim.Idle:
                    hp.y = HipY - 0.01f + 0.01f * Mathf.Sin(t * 2f + seed);
                    spineX = 1.5f * Mathf.Sin(t * 2f + seed);
                    spineY = 4f * Mathf.Sin(t * 0.7f + seed);
                    break;
                case Anim.Down:
                    sL = -40f; sR = -20f; sLz = -80f; sRz = 80f; eL = -30f; eR = -50f;
                    hL = -25f; hR = 15f; kL = 35f; kR = 20f; neckX = 20f; spineX = -10f;
                    break;
            }
            prevS = s;

            // virage : le corps penche, les bras s'écartent pour l'équilibre
            float lz = Mathf.Clamp(lean, -30f, 30f);
            hipsZ -= lz * 0.35f; spineZ -= lz * 0.45f; neckZ += lz * 0.5f;
            sLz -= Mathf.Abs(lz) * 1.3f; sRz += Mathf.Abs(lz) * 1.3f;

            // mitraillette : les deux bras tiennent l'arme devant, recul à chaque tir
            if (aiming && (anim == Anim.Run || anim == Anim.Jump))
            {
                recoil = Mathf.MoveTowards(recoil, 0f, dt * 12f);
                sL = -62f; eL = -62f; sLz = 22f;
                sR = -58f; eR = -70f; sRz = -8f;
                spineX -= recoil * 7f; neckX -= recoil * 3f; spineY *= 0.4f;
            }

            // roulade avant au début de la glissade
            float roll = 0f;
            if (rollT >= 0f)
            {
                rollT += dt;
                float u = rollT / 0.36f;
                if (u >= 1f) rollT = -1f;
                else
                {
                    roll = Smooth(u) * 360f;
                    float tuck = Mathf.Sin(u * Mathf.PI);
                    hipsX = Mathf.Lerp(hipsX, 0f, tuck); hp.y = Mathf.Lerp(hp.y, 0.62f, tuck);
                    hL = Mathf.Lerp(hL, -115f, tuck); kL = Mathf.Lerp(kL, 140f, tuck);
                    hR = Mathf.Lerp(hR, -110f, tuck); kR = Mathf.Lerp(kR, 140f, tuck);
                    sL = Mathf.Lerp(sL, -70f, tuck); sR = Mathf.Lerp(sR, -70f, tuck);
                    eL = Mathf.Lerp(eL, -110f, tuck); eR = Mathf.Lerp(eR, -110f, tuck);
                    spineX = Mathf.Lerp(spineX, 50f, tuck); neckX = Mathf.Lerp(neckX, 40f, tuck);
                }
            }

            // coup de matraque : armé, frappe, accompagnement. Trois styles qui s'enchaînent
            // (coup droit, revers, coup par-dessus la tête), avec bascule du buste.
            if (swingT >= 0f)
            {
                swingT += dt;
                float dur = (swingStyle == 2 ? 0.36f : 0.3f) * (heavy ? 1.12f : 1f);
                float u = swingT / dur;
                if (u >= 1f)
                {
                    swingT = -1f;
                    if (trail != null) trail.emitting = false;
                }
                else
                {
                    // armé (w) puis frappe (k) : épaule avant, épaule latérale, coude, torsion, bascule
                    float wA, wZ, wE, wY, wX, kA, kZ, kE, kY, kX;
                    if (swingStyle == 1)      { wA = -95f;  wZ = -80f; wE = -40f; wY = 36f;  wX = 0f;   kA = -95f; kZ = 88f; kE = -8f;  kY = -40f; kX = 14f; }
                    else if (swingStyle == 2) { wA = -205f; wZ = 8f;   wE = -95f; wY = -6f;  wX = -18f; kA = -15f; kZ = 8f;  kE = -8f;  kY = 6f;   kX = 42f; }
                    else                      { wA = -178f; wZ = 16f;  wE = -75f; wY = -34f; wX = -8f;  kA = -12f; kZ = 16f; kE = -6f;  kY = 36f;  kX = 22f; }
                    if (heavy) { wY *= 1.35f; kY *= 1.35f; wX *= 1.3f; kX *= 1.3f; }
                    float a, az, el, sp, fx;
                    if (u < 0.32f)
                    {
                        float q = Smooth(u / 0.32f);
                        a = Mathf.Lerp(sR, wA, q); az = Mathf.Lerp(sRz, wZ, q); el = Mathf.Lerp(eR, wE, q); sp = Mathf.Lerp(spineY, wY, q); fx = wX * q;
                    }
                    else if (u < 0.56f)
                    {
                        float q = (u - 0.32f) / 0.24f;
                        q = q * (2f - q);
                        a = Mathf.Lerp(wA, kA, q); az = Mathf.Lerp(wZ, kZ, q); el = Mathf.Lerp(wE, kE, q); sp = Mathf.Lerp(wY, kY, q); fx = Mathf.Lerp(wX, kX, q);
                        if (trail != null) trail.emitting = true;
                        if (swingStyle == 2) hp.y -= 0.12f * q;
                    }
                    else
                    {
                        float q = Smooth((u - 0.56f) / 0.44f);
                        a = Mathf.Lerp(kA, sR, q); az = Mathf.Lerp(kZ, sRz, q); el = Mathf.Lerp(kE, eR, q); sp = Mathf.Lerp(kY, spineY, q); fx = Mathf.Lerp(kX, 0f, q);
                        if (trail != null) trail.emitting = false;
                        if (swingStyle == 2) hp.y -= 0.12f * (1f - q);
                    }
                    sR = a; sRz = az; eR = el; spineY = sp; spineX += fx;
                    // matraque renforcée : petit bond à l'armé, appui lourd à l'impact
                    if (heavy) hp.y += u < 0.32f ? 0.06f * Mathf.Sin(u / 0.32f * Mathf.PI) : u < 0.6f ? -0.08f * Mathf.Sin((u - 0.32f) / 0.28f * Mathf.PI) : 0f;
                    if (swingStyle == 2) { sL = a + 10f; eL = el; sLz = -az; } // à deux mains
                    else sL = Mathf.Lerp(sL, 30f, Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI)); // bras gauche en contrepoids
                }
            }

            // lancer de pavé
            if (throwT >= 0f)
            {
                throwT += dt;
                float u = throwT / 0.5f;
                if (u >= 1f) throwT = -1f;
                else if (u < 0.55f) { float q = Smooth(u / 0.55f); sR = Mathf.Lerp(sR, -195f, q); eR = Mathf.Lerp(eR, -80f, q); spineY = -25f * q; spineX = -8f * q; }
                else { float q = (u - 0.55f) / 0.45f; sR = Mathf.Lerp(-195f, -40f, Mathf.Min(1f, q * 2f)); eR = Mathf.Lerp(-80f, -10f, q); spineY = Mathf.Lerp(-25f, 20f, q); spineX = Mathf.Lerp(-8f, 18f, q); }
            }

            // tir : bras gauche tendu vers l'avant, petit recul du buste
            if (shootT >= 0f)
            {
                shootT += dt;
                float u = shootT / 0.32f;
                if (u >= 1f) shootT = -1f;
                else
                {
                    float hold = u < 0.15f ? Smooth(u / 0.15f) : u > 0.7f ? 1f - Smooth((u - 0.7f) / 0.3f) : 1f;
                    float kick = u < 0.25f ? Mathf.Sin(u / 0.25f * Mathf.PI) : 0f;
                    sL = Mathf.Lerp(sL, -88f + kick * -14f, hold); eL = Mathf.Lerp(eL, -4f, hold); sLz = Mathf.Lerp(sLz, 8f, hold);
                    spineY = Mathf.Lerp(spineY, 18f, hold); spineX -= kick * 6f; neckX -= kick * 4f;
                }
            }

            hips.localPosition = hp;
            hips.localRotation = Quaternion.Euler(hipsX, -spineY * 0.5f, hipsZ);
            spine.localRotation = Quaternion.Euler(spineX, spineY, spineZ);
            neck.localRotation = Quaternion.Euler(neckX, -spineY * 0.6f, neckZ);
            shL.localRotation = Quaternion.Euler(sL, 0f, sLz);
            shR.localRotation = Quaternion.Euler(sR, 0f, sRz);
            elL.localRotation = Quaternion.Euler(eL, 0f, 0f);
            elR.localRotation = Quaternion.Euler(eR, 0f, 0f);
            hipL.localRotation = Quaternion.Euler(hL, 0f, 0f);
            hipR.localRotation = Quaternion.Euler(hR, 0f, 0f);
            knL.localRotation = Quaternion.Euler(kL, 0f, 0f);
            knR.localRotation = Quaternion.Euler(kR, 0f, 0f);

            // étirement au décollage, écrasement à l'atterrissage (volume conservé)
            squash = Mathf.MoveTowards(squash, 0f, dt * 2.5f);
            stretch = Mathf.MoveTowards(stretch, 0f, dt * 1.2f);
            float sy = 1f - squash + stretch, sxz = 1f + squash * 0.6f - stretch * 0.45f;
            body.localScale = new Vector3(sxz, sy, sxz);

            // la roulade tourne autour du centre du corps replié
            float spin = 0f;
            if (spinT >= 0f)
            {
                spinT += dt;
                float u = spinT / 0.32f;
                if (u >= 1f) spinT = -1f;
                else spin = Smooth(u) * 360f;
            }
            Vector3 pivot = new Vector3(0f, 0.62f, 0f);
            Quaternion rq = Quaternion.Euler(roll, spin, 0f);
            body.localRotation = rq;
            body.localPosition = pivot - rq * pivot;
        }

        /// <summary>Pendant le vol après un K.-O. : bras et jambes qui moulinent, puis le corps devient mou.</summary>
        void TickKO(float dt)
        {
            koT += dt;
            float f = Mathf.Clamp01(1f - koT / 1.3f);
            float w = koT * 26f;
            foreach (Transform e in eyes) e.localScale = new Vector3(0.05f, 0.012f, 0.02f); // yeux fermés
            shL.localRotation = Quaternion.Euler(-70f + 85f * Mathf.Sin(w) * f, 0f, -75f);
            shR.localRotation = Quaternion.Euler(-70f + 85f * Mathf.Sin(w + 2.1f) * f, 0f, 75f);
            elL.localRotation = Quaternion.Euler(-35f - 35f * Mathf.Sin(w + 1f) * f, 0f, 0f);
            elR.localRotation = Quaternion.Euler(-35f - 35f * Mathf.Sin(w + 3f) * f, 0f, 0f);
            hipL.localRotation = Quaternion.Euler(-30f + 55f * Mathf.Sin(w + 0.7f) * f, 0f, -8f);
            hipR.localRotation = Quaternion.Euler(-20f + 55f * Mathf.Sin(w + 2.9f) * f, 0f, 8f);
            knL.localRotation = Quaternion.Euler(40f + 35f * Mathf.Sin(w * 1.3f) * f, 0f, 0f);
            knR.localRotation = Quaternion.Euler(30f + 35f * Mathf.Sin(w * 1.3f + 1.5f) * f, 0f, 0f);
            neck.localRotation = Quaternion.Euler(25f + 18f * Mathf.Sin(w * 0.5f) * f, 0f, 10f * Mathf.Sin(w * 0.4f));
            body.localScale = Vector3.MoveTowards(body.localScale, Vector3.one, dt * 3f);
        }

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
        static float EaseIn(float x) { x = Mathf.Clamp01(x); return x * x; }

        /// <summary>Mise au tapis : pose molle + corps physique projeté (culbute cartoon).</summary>
        public void KO(Vector3 impulse, Vector3 torque)
        {
            if (ko) return;
            double pt0 = Perf.Begin();
            KOIn(impulse, torque);
            Perf.End("·ko", pt0);
        }

        void KOIn(Vector3 impulse, Vector3 torque)
        {
            anim = Anim.Down;
            swingT = -1f;
            throwT = -1f;
            shootT = -1f;
            rollT = -1f;
            Tick(0f);
            koT = 0f;
            Flash(Color.white, 0.09f);
            ko = true;
            if (trail != null) trail.emitting = false;
            stars.gameObject.SetActive(true);
            var col = root.gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.9f, 0f);
            col.height = 1.8f;
            col.radius = 0.3f;
            rb = root.gameObject.AddComponent<Rigidbody>();
            rb.mass = 70f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.AddForce(impulse, ForceMode.VelocityChange);
            rb.AddTorque(torque, ForceMode.VelocityChange);
        }
    }
}
