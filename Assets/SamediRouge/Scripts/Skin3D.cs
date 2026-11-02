using System.Collections.Generic;
using UnityEngine;

namespace SamediRouge
{
    /// <summary>
    /// Personnage 3D (Meshy, squelette « mixamorig ») animé par le squelette dessiné en code :
    /// chaque frame, les os du modèle recopient les rotations des articulations du Rig (course, coups, chutes…),
    /// si bien que toutes les animations existantes servent telles quelles. Le corps en formes simples est masqué ;
    /// l'équipement suit des relais placés sur les os du modèle (mains, tête, buste).
    /// </summary>
    public class Skin3D
    {
        /// <summary>Taille totale visée (unités du Rig, avant l'échelle des personnages).</summary>
        const float Height = 1.95f;

        class Pair { public Transform code, bone; public Quaternion bind, corr; }
        class Relay { public Transform proxy, bone, code, tip, fore; public string boneName, tipName; public int kind; public Vector3 baseScale, offset; public Quaternion rotOff; public bool needOff; }
        class Finger { public Transform bone; public Quaternion rest; public Vector3 axis; public bool right; public float angle; }
        readonly List<Finger> fingers = new List<Finger>();

        readonly Rig rig;
        readonly Transform body, codeHips, holder;
        readonly List<Pair> pairs = new List<Pair>();
        readonly List<Relay> relays = new List<Relay>();
        readonly Vector3 codeHipsRest;
        float soleRest = float.NaN, floor0, groundCorr, soleOff = float.NaN;
        readonly Transform codeKnL, codeKnR;
        Transform hipsBone, footL, footR, toeL, toeR;
        float restL, restR;

        /// <summary>Inclinaison (radians, vers le bas) de l'axe cheville → orteil, dans le repère du corps.</summary>
        float Pitch(Transform foot, Transform toe)
        {
            if (foot == null || toe == null) return 0f;
            Vector3 d = body.InverseTransformVector(toe.position - foot.position);
            return Mathf.Atan2(-d.y, Mathf.Sqrt(d.x * d.x + d.z * d.z));
        }

        /// <summary>Remet le pied à plat : l'axe cheville → orteil reprend l'inclinaison de la pose de repos.</summary>
        void FlatFoot(Transform foot, Transform toe, float rest)
        {
            if (foot == null || toe == null) return;
            Vector3 d = body.InverseTransformVector(toe.position - foot.position);
            Vector3 flat = new Vector3(d.x, 0f, d.z);
            if (flat.sqrMagnitude < 1e-6f) return;
            Vector3 want = flat.normalized * Mathf.Cos(rest) + Vector3.down * Mathf.Sin(rest);
            foot.rotation = Quaternion.FromToRotation(body.TransformDirection(d), body.TransformDirection(want)) * foot.rotation;
        }
        Vector3 hipsBind;
        float footH, headK, wideK, tallK;
        SkinnedMeshRenderer[] skins;
        bool visibleFlag = true;
        public Transform model;
        public string Name { get; private set; }

        public static bool Available(string name) => Resources.Load<GameObject>("SamediRouge/Characters/" + name) != null;

        public static Skin3D Attach(Rig r, string name)
        {
            GameObject prefab = Resources.Load<GameObject>("SamediRouge/Characters/" + name);
            if (prefab == null) return null;
            try { return new Skin3D(r, prefab, name); }
            catch (System.Exception e) { Debug.LogWarning("[Skin3D] " + e.Message); return null; }
        }

        // index des os du dernier modèle interrogé : une seule traversée de la hiérarchie au lieu d'une par os
        // (les recherches récursives coûtaient ~10 ms par personnage créé, d'où des saccades à chaque groupe d'ennemis)
        static Transform boneRoot;
        static readonly Dictionary<string, Transform> boneIndex = new Dictionary<string, Transform>();

        static Transform Find(Transform t, string bone)
        {
            if (t == null) return null;
            if (!ReferenceEquals(t, boneRoot) || boneRoot == null)
            {
                boneRoot = t;
                boneIndex.Clear();
                Index(t);
            }
            return boneIndex.TryGetValue(bone, out Transform f) ? f : null;
        }

        static void Index(Transform t)
        {
            string n = t.name;
            if (!boneIndex.ContainsKey(n)) boneIndex[n] = t;
            int c = n.LastIndexOf(':');
            if (c >= 0)
            {
                string s = n.Substring(c + 1);
                if (!boneIndex.ContainsKey(s)) boneIndex[s] = t;
            }
            for (int i = 0; i < t.childCount; i++) Index(t.GetChild(i));
        }

        Skin3D(Rig r, GameObject prefab, string name)
        {
            rig = r;
            body = r.body;
            codeHips = r.hips;
            codeKnL = r.knL; codeKnR = r.knR;
            codeHipsRest = codeHips.localPosition;
            // le modèle vit sous « body » : il suit déjà les bonds, roulades et écrasements du corps
            holder = new GameObject("skin3d").transform;
            holder.SetParent(body, false);
            Load(prefab, name);

            // le corps dessiné en code disparaît (ses pièces directes) ; l'équipement reste et suit le modèle
            var joints = new HashSet<Transform> { r.hips, r.spine, r.neck, r.shL, r.shR, r.elL, r.elR, r.hipL, r.hipR, r.knL, r.knR, r.handL, r.handR };
            foreach (Transform j in joints)
            {
                if (j == null) continue;
                for (int c = 0; c < j.childCount; c++)
                {
                    Transform t = j.GetChild(c);
                    if (t.GetComponent<MeshRenderer>() != null) t.gameObject.SetActive(false);
                }
            }
            if (r.cap != null) { r.cap.gameObject.SetActive(false); r.cap = null; }

            jointSet = joints;
            double q1 = Perf.Begin();
            Relays(r.handR, "RightHand", "RightHandMiddle1", 0);
            Relays(r.handL, "LeftHand", "LeftHandMiddle1", 0);
            // tête et buste du modèle plus petits que ceux du personnage en blocs : l'équipement est réduit en proportion
            Relays(r.neck, "Neck", null, 1);
            Relays(r.spine, "Spine", null, 2);
            Apply();
            Perf.End("···relays", q1);
        }

        /// <summary>Remplace le modèle 3D (ex. le voleur avec sa tenue achetée) en gardant l'équipement et les relais.</summary>
        public bool Swap(string name)
        {
            if (name == Name) return true;
            GameObject prefab = Resources.Load<GameObject>("SamediRouge/Characters/" + name);
            if (prefab == null) return false;
            Transform old = model;
            try { Load(prefab, name); }
            catch (System.Exception e) { Debug.LogWarning("[Skin3D] " + e.Message); return false; }
            if (old != null) { old.gameObject.SetActive(false); Object.Destroy(old.gameObject); }
            foreach (Relay rl in relays) Bind(rl);
            SetVisible(visibleFlag);
            SetShadows(shadowsOn);
            Apply();
            return true;
        }

        void Load(GameObject prefab, string name)
        {
            double pq = Perf.Begin();
            Transform m = Object.Instantiate(prefab, holder, false).transform;
            Perf.End("···inst", pq); pq = Perf.Begin();
            Quaternion q0 = m.localRotation;
            Vector3 s0 = m.localScale;
            m.localPosition = Vector3.zero;
            foreach (Animator a in m.GetComponentsInChildren<Animator>(true)) Object.Destroy(a);
            foreach (Animation a in m.GetComponentsInChildren<Animation>(true)) Object.Destroy(a);

            Transform hips = Find(m, "Hips"), head = Find(m, "HeadTop_End") ?? Find(m, "Head");
            Transform toe = Find(m, "LeftToe_End") ?? Find(m, "LeftFoot");
            if (hips == null || head == null || toe == null) { Object.Destroy(m.gameObject); throw new System.Exception("squelette mixamorig introuvable"); }
            model = m;
            Name = name;
            soleOff = float.NaN;
            hipsBone = hips;

            // face avant vers +Z, pieds au sol, taille du personnage du jeu
            Transform la = Find(m, "LeftArm"), ra = Find(m, "RightArm");
            if (la != null && ra != null && holder.InverseTransformPoint(la.position).x > holder.InverseTransformPoint(ra.position).x)
                m.localRotation = Quaternion.Euler(0f, 180f, 0f) * q0;
            float h = holder.InverseTransformPoint(head.position).y - holder.InverseTransformPoint(toe.position).y;
            float k = Height / Mathf.Max(0.01f, h);
            m.localScale = s0 * k;
            float foot = holder.InverseTransformPoint(toe.position).y;
            // les modèles Meshy ont le dessous des semelles à leur origine (y = 0), sous l'os de l'orteil
            // (semelles épaisses, rollers…) : c'est ce point qu'on pose au sol
            float low = holder.InverseTransformPoint(m.position).y;
            if (low < foot) foot = low;
            Vector3 mid = holder.InverseTransformPoint(hips.position);
            m.localPosition = new Vector3(-mid.x, -foot, -mid.z);

            // texture du modèle (matériau du jeu, compatible URP)
            Perf.End("···fit", pq); pq = Perf.Begin();
            Texture2D tex = Resources.Load<Texture2D>("SamediRouge/Characters/" + name + "_tex");
            Material mat = tex != null ? Art.Tex(tex, Vector2.one, 0.35f) : Art.Mat(Color.white, 0.35f);
            skins = m.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (SkinnedMeshRenderer s in skins)
            {
                s.sharedMaterial = mat;
                s.updateWhenOffscreen = true;
                s.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;   // son ombre au sol : sans elle, le personnage semble flotter
            }

            Perf.End("···mat", pq); pq = Perf.Begin();
            hipsBind = body.InverseTransformPoint(hips.position);
            footL = Find(m, "LeftFoot"); footR = Find(m, "RightFoot");
            if (footL != null) footH = body.InverseTransformPoint(footL.position).y;
            // orteils : pour remettre les pieds à plat quand le personnage est debout (pose de repos du modèle = pieds à plat)
            toeL = Find(m, "LeftToe_End") ?? Find(m, "LeftToeBase");
            toeR = Find(m, "RightToe_End") ?? Find(m, "RightToeBase");
            restL = Pitch(footL, toeL);
            restR = Pitch(footR, toeR);

            // articulations du jeu → os du modèle (direction de repos des membres : vers le bas, un peu écartés)
            pairs.Clear();
            Rig r = rig;
            Map(r.hips, "Hips", null, Vector3.zero);
            Map(r.spine, "Spine", null, Vector3.zero);
            Map(r.neck, "Neck", null, Vector3.zero);
            Map(r.shL, "LeftArm", "LeftForeArm", new Vector3(-0.17f, -1f, 0f));
            Map(r.elL, "LeftForeArm", "LeftHand", new Vector3(-0.08f, -1f, 0.04f));
            Map(r.shR, "RightArm", "RightForeArm", new Vector3(0.17f, -1f, 0f));
            Map(r.elR, "RightForeArm", "RightHand", new Vector3(0.08f, -1f, 0.04f));
            Map(r.hipL, "LeftUpLeg", "LeftLeg", new Vector3(-0.03f, -1f, 0f));
            Map(r.knL, "LeftLeg", "LeftFoot", new Vector3(0f, -1f, 0f));
            Map(r.hipR, "RightUpLeg", "RightLeg", new Vector3(0.03f, -1f, 0f));
            Map(r.knR, "RightLeg", "RightFoot", new Vector3(0f, -1f, 0f));

            // proportions de la tête et du buste, pour mettre l'équipement à l'échelle
            Transform neckB = Find(m, "Neck"), spineB = Find(m, "Spine"), top = Find(m, "HeadTop_End");
            Transform lArm = Find(m, "LeftArm"), rArm = Find(m, "RightArm");
            System.Func<Transform, Vector3> P = t => body.InverseTransformPoint(t.position);
            headK = neckB != null && top != null ? Mathf.Clamp((P(top).y - P(neckB).y) / 0.455f, 0.35f, 1.2f) : 0.6f;
            wideK = lArm != null && rArm != null ? Mathf.Clamp(Mathf.Abs(P(lArm).x - P(rArm).x) / 0.58f, 0.4f, 1.3f) : 0.8f;
            tallK = neckB != null && spineB != null ? Mathf.Clamp((P(neckB).y - P(spineB).y) / 0.5f, 0.4f, 1.3f) : 0.8f;
            Perf.End("···map", pq); pq = Perf.Begin();
            fingers.Clear();
            Fingers(m, "Right", true);
            Fingers(m, "Left", false);
            Perf.End("···fingers", pq);
        }

        void Map(Transform code, string bone, string child, Vector3 restDir)
        {
            Transform b = Find(model, bone);
            if (code == null || b == null) return;
            var p = new Pair { code = code, bone = b, bind = Quaternion.Inverse(body.rotation) * b.rotation, corr = Quaternion.identity };
            Transform c = child != null ? Find(model, child) : null;
            if (c != null)
            {
                Vector3 dir = body.InverseTransformDirection(c.position - b.position);
                p.corr = Quaternion.FromToRotation(dir, restDir.normalized);
            }
            pairs.Add(p);
        }

        /// <summary>Déplace l'équipement porté par une articulation sur un relais qui suit l'os du modèle.</summary>
        void Relays(Transform code, string boneName, string tipName, int kind)
        {
            if (code == null || Find(model, boneName) == null) return;
            var proxy = new GameObject("relay_" + code.name).transform;
            proxy.SetParent(body, false);
            var move = new List<Transform>();
            for (int c = 0; c < code.childCount; c++)
            {
                Transform t = code.GetChild(c);
                if (t.GetComponent<MeshRenderer>() != null) continue;   // pièce du corps (masquée)
                if (t.name == "joint" || jointSet.Contains(t)) continue;
                move.Add(t);
            }
            foreach (Transform t in move) t.SetParent(proxy, false);
            var rl = new Relay { proxy = proxy, code = code, boneName = boneName, tipName = tipName, kind = kind, baseScale = code.localScale };
            Bind(rl);
            relays.Add(rl);
        }

        /// <summary>Accroche un relais aux os du modèle courant (et le met à l'échelle de sa tête / son buste).</summary>
        /// <summary>Doigts d'une main : axe de flexion vers la paume, pour refermer le poing sur l'objet tenu.</summary>
        void Fingers(Transform m, string side, bool right)
        {
            Transform hand = Find(m, side + "Hand"), idx = Find(m, side + "HandIndex1"), mid = Find(m, side + "HandMiddle1"),
                      pk = Find(m, side + "HandPinky1"), th = Find(m, side + "HandThumb2");
            if (hand == null || idx == null || mid == null || pk == null) return;
            Vector3 n = Vector3.Cross(idx.position - pk.position, mid.position - hand.position).normalized;
            // pose en A : les paumes regardent les cuisses, donc le bassin donne le côté de la paume
            Transform hp = Find(m, "Hips");
            if (hp != null) { if (Vector3.Dot(hp.position - hand.position, n) < 0f) n = -n; }
            else if (th != null && Vector3.Dot(th.position - hand.position, n) < 0f) n = -n;
            foreach (string f in new[] { "Index", "Middle", "Ring", "Pinky", "Thumb" })
                for (int i = 1; i <= 3; i++)
                {
                    Transform b = Find(m, side + "Hand" + f + i), c = Find(m, side + "Hand" + f + (i + 1));
                    if (b == null || c == null) continue;
                    Vector3 dir = (c.position - b.position).normalized;
                    Vector3 ax = Vector3.Cross(dir, n);
                    if (ax.sqrMagnitude < 1e-6f) continue;
                    fingers.Add(new Finger { bone = b, rest = b.localRotation, axis = Quaternion.Inverse(b.rotation) * ax.normalized, right = right,
                                             angle = f == "Thumb" ? 30f : i == 1 ? 75f : 90f });
                }
        }

        bool Holding(Transform code)
        {
            foreach (Relay r in relays)
                if (r.code == code)
                    for (int c = 0; c < r.proxy.childCount; c++) if (r.proxy.GetChild(c).gameObject.activeInHierarchy) return true;
            return false;
        }

        void Bind(Relay rl)
        {
            rl.bone = Find(model, rl.boneName);
            rl.needOff = rl.tip != null || rl.tipName != null;
            rl.tip = rl.tipName != null ? Find(model, rl.tipName) : null;
            // modèle sans os de doigts : la paume est estimée dans le prolongement de l'avant-bras
            rl.fore = rl.tipName != null && rl.tip == null ? Find(model, rl.boneName.Replace("Hand", "ForeArm")) : null;
            Vector3 k = rl.kind == 1 ? Vector3.one * headK : rl.kind == 2 ? new Vector3(wideK, tallK, wideK) : Vector3.one;
            // têtes des tenues du voleur : un peu plus grosses et plus en avant que celle du modèle de base
            bool big = rl.kind == 1 && Name != null && Name.StartsWith("voleur_niv");
            if (big) k *= 1.12f;
            // tenues du policier : tête plus petite que celle du modèle de base, casque réduit
            bool cop = rl.kind == 1 && Name != null && Name.StartsWith("policier_niv");
            if (cop) k *= 0.94f;
            rl.offset = big ? new Vector3(0f, 0f, 0.05f) : cop ? new Vector3(0f, -0.03f, 0.02f) : Vector3.zero;
            rl.proxy.localScale = Vector3.Scale(rl.baseScale, k);
        }

        HashSet<Transform> jointSet;

        /// <summary>Parent à utiliser pour de l'équipement ajouté plus tard sur une articulation (relais s'il existe).</summary>
        public Transform RelayOf(Transform code)
        {
            foreach (Relay r in relays) if (r.code == code) return r.proxy;
            return code;
        }

        /// <summary>
        /// Personnage secondaire (ennemi, passant, coéquipier) : pas d'ombre portée et pas de recalcul de la boîte
        /// englobante à chaque image (seuls le héros et le boss gardent ces réglages coûteux).
        /// </summary>
        bool secondary;

        /// <summary>Personnage secondaire hors de l'écran : inutile de recopier sa pose (économise le processeur).</summary>
        public bool Hidden
        {
            get
            {
                if (!secondary) return false;
                foreach (SkinnedMeshRenderer s in skins) if (s != null && s.isVisible) return false;
                return true;
            }
        }

        public void SetSecondary()
        {
            secondary = true;
            SetShadows(false);
            foreach (SkinnedMeshRenderer s in skins)
                if (s != null) { s.updateWhenOffscreen = false; s.localBounds = new Bounds(new Vector3(0f, 1f, 0f), new Vector3(3f, 3f, 3f)); }
        }

        bool shadowsOn = true;
        /// <summary>Ombre portée au sol (coupée pour la foule de passants, pour ménager le téléphone).</summary>
        public void SetShadows(bool on)
        {
            shadowsOn = on;
            foreach (SkinnedMeshRenderer s in skins)
                if (s != null) s.shadowCastingMode = on ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void SetVisible(bool v)
        {
            visibleFlag = v;
            foreach (SkinnedMeshRenderer s in skins) if (s != null) s.enabled = v;
        }

        /// <summary>Recopie la pose du Rig sur le modèle (à appeler après l'animation du Rig).</summary>
        /// <summary>
        /// Écart (unités du corps) entre l'os du pied le plus bas et le point le plus bas du maillage, mesuré sur le modèle
        /// déformé par la pose courante. Le repère du maillage cuit dépend de l'échelle : on garde l'interprétation
        /// qui place la semelle juste sous l'os du pied.
        /// </summary>
        float MeasureSole()
        {
            // BakeMesh(…, false) : sommets dans le repère du renderer sans son échelle (vérifié dans l'éditeur)
            float foot = Mathf.Min(footL.position.y, footR.position.y);
            float low = float.MaxValue;
            if (bakeTmp == null) bakeTmp = new Mesh();
            foreach (SkinnedMeshRenderer sk in skins)
            {
                if (sk == null || !sk.enabled) continue;
                sk.BakeMesh(bakeTmp, false);
                Matrix4x4 b = Matrix4x4.TRS(sk.transform.position, sk.transform.rotation, Vector3.one);
                foreach (Vector3 v in bakeTmp.vertices) { float y = b.MultiplyPoint3x4(v).y; if (y < low) low = y; }
            }
            if (low > 1e8f) return float.NaN;
            return (foot - low) / Mathf.Max(0.01f, body.lossyScale.y);
        }

        Mesh bakeTmp;

        /// <summary>Accueil : le héros seul à l'écran, on mesure son vrai point le plus bas à chaque image.</summary>
        public static bool PreciseGround;
        public static float DebugMinGap = 9f, DebugMaxGap = -9f;

        float MeshLow()
        {
            float low = float.MaxValue;
            if (bakeTmp == null) bakeTmp = new Mesh();
            foreach (SkinnedMeshRenderer sk in skins)
            {
                if (sk == null || !sk.enabled) continue;
                sk.BakeMesh(bakeTmp, false);
                Matrix4x4 b = Matrix4x4.TRS(sk.transform.position, sk.transform.rotation, Vector3.one);
                foreach (Vector3 v in bakeTmp.vertices) { float y = b.MultiplyPoint3x4(v).y; if (y < low) low = y; }
            }
            return low;
        }
        int soleN;

        public void Apply()
        {
            // héros (et boss) : les semelles posées exactement sur le sol, quelle que soit la pose ou l'échelle.
            // On mesure une fois (modèle « cuit ») l'écart entre l'os du pied et le dessous de la semelle,
            // puis à chaque image on recale le modèle pour que la semelle la plus basse touche le sol.
            if (!secondary && footL != null && footR != null && rig.root != null)
            {
                // re-mesure régulière : l'écart pied/semelle dépend de la pose (pointe, talon)
                if (float.IsNaN(soleOff) || --soleN <= 0) { float m = MeasureSole(); if (!float.IsNaN(m)) soleOff = float.IsNaN(soleOff) ? m : Mathf.Lerp(soleOff, m, 0.3f); soleN = 20; }
                if (!float.IsNaN(soleOff))
                {
                    float k = Mathf.Max(0.01f, body.lossyScale.y);
                    float meshLow = Mathf.Min(footL.position.y, footR.position.y) - soleOff * k;
                    // accueil : mesure exacte du point le plus bas du maillage à chaque image (un seul personnage à l'écran)
                    if (PreciseGround) { float lowNow = MeshLow(); if (lowNow < 1e8f) { meshLow = lowNow; DebugMinGap = Mathf.Min(DebugMinGap, lowNow - rig.root.position.y); DebugMaxGap = Mathf.Max(DebugMaxGap, lowNow - rig.root.position.y); } }
                    float gap = (meshLow - (PreciseGround ? 0.04f : 0f) - rig.root.position.y) / k;   // accueil : la semelle la plus basse vise 4 cm, le retard du lissage la ramène à fleur de sol
                    // debout : recalage franc ; en course, les pieds quittent le sol à chaque foulée : on moyenne
                    // très doucement (sinon le corps tressaute de haut en bas)
                    // en course : on remonte vite si un pied s'enfonce, on redescend très lentement → la semelle la plus basse
                    // de la foulée effleure le sol, sans tressautement
                    float gain = rig.anim == Anim.Stand || rig.anim == Anim.Idle ? 0.35f : gap < 0f ? 0.15f : 0.004f;
                    groundCorr = Mathf.Clamp(groundCorr + gap * gain, -0.8f, 0.8f);
                }
            }
            Quaternion bodyRot = body.rotation;
            // bassin : suit la hauteur et les décalages du bassin dessiné
            Vector3 d = codeHips.localPosition - codeHipsRest;
            hipsBone.position = body.TransformPoint(hipsBind + d);
            foreach (Pair p in pairs)
            {
                Quaternion delta = Quaternion.Inverse(bodyRot) * p.code.rotation;
                p.bone.rotation = bodyRot * (delta * p.corr * p.bind);
            }
            // poings fermés sur l'arme tenue, mains ouvertes sinon
            if (fingers.Count > 0)
            {
                bool hr = Holding(rig.handR), hl = Holding(rig.handL);
                foreach (Finger f in fingers)
                    f.bone.localRotation = (f.right ? hr : hl) ? f.rest * Quaternion.AngleAxis(f.angle, f.axis) : f.rest;
            }
            // pieds au sol : le modèle a des jambes plus longues que le personnage en blocs ; quand celui-ci
            // plie les genoux (garde, accroupi), on recale le bassin pour que les semelles restent au niveau des siennes
            if (footL != null && footR != null && codeKnL != null && codeKnR != null)
            {
                float codeSole = Mathf.Min(body.InverseTransformPoint(codeKnL.TransformPoint(new Vector3(0f, -0.5f, 0f))).y,
                                           body.InverseTransformPoint(codeKnR.TransformPoint(new Vector3(0f, -0.5f, 0f))).y);
                // la semelle du personnage en blocs n'est pas exactement au sol : on garde seulement ses variations
                // (genoux pliés, accroupi) et on pose la semelle du modèle sur le sol réel au repos
                if (float.IsNaN(soleRest)) { soleRest = codeSole; floor0 = body.InverseTransformPoint(rig.root.position).y; }
                codeSole = codeSole - soleRest + floor0;
                float modelSole = Mathf.Min(body.InverseTransformPoint(footL.position).y, body.InverseTransformPoint(footR.position).y) - footH;
                hipsBone.position = body.TransformPoint(body.InverseTransformPoint(hipsBone.position) + new Vector3(0f, codeSole - modelSole, 0f));
            }
            // recalage au sol mesuré (héros) : appliqué au bassin, racine du squelette
            if (!secondary && groundCorr != 0f) hipsBone.position += body.TransformVector(new Vector3(0f, -groundCorr, 0f));
            foreach (Relay r in relays)
            {
                if (r.bone == null) continue;
                // mains : l'objet tenu se place au creux de la paume (entre le poignet et la base des doigts), pas au poignet
                r.proxy.position = r.tip != null ? Vector3.Lerp(r.bone.position, r.tip.position, 0.75f)
                                 : r.fore != null ? r.bone.position + (r.bone.position - r.fore.position) * 0.18f
                                 : r.bone.position;
                // mains : l'arme suit l'orientation de la main du modèle (calée une fois sur celle du personnage en blocs)
                if (r.needOff) { r.rotOff = Quaternion.Inverse(r.bone.rotation) * r.code.rotation; r.needOff = false; }
                r.proxy.rotation = r.tipName != null ? r.bone.rotation * r.rotOff : r.code.rotation;
                if (r.offset != Vector3.zero) r.proxy.position += r.proxy.TransformVector(r.offset);
            }
        }
    }
}
