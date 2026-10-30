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
        class Relay { public Transform proxy, bone, code; public Vector3 offset; }

        readonly Transform body, codeHips, hipsBone;
        readonly List<Pair> pairs = new List<Pair>();
        readonly List<Relay> relays = new List<Relay>();
        readonly Vector3 hipsBind, codeHipsRest;
        readonly Transform footL, footR, codeKnL, codeKnR;
        readonly float footH;
        readonly SkinnedMeshRenderer[] skins;
        public readonly Transform model;

        public static bool Available(string name) => Resources.Load<GameObject>("SamediRouge/Characters/" + name) != null;

        public static Skin3D Attach(Rig r, string name)
        {
            GameObject prefab = Resources.Load<GameObject>("SamediRouge/Characters/" + name);
            if (prefab == null) return null;
            try { return new Skin3D(r, prefab, name); }
            catch (System.Exception e) { Debug.LogWarning("[Skin3D] " + e.Message); return null; }
        }

        static Transform Find(Transform t, string bone)
        {
            if (t.name == bone || t.name.EndsWith(":" + bone)) return t;
            for (int i = 0; i < t.childCount; i++) { Transform f = Find(t.GetChild(i), bone); if (f != null) return f; }
            return null;
        }

        Skin3D(Rig r, GameObject prefab, string name)
        {
            body = r.body;
            codeHips = r.hips;
            // le modèle vit sous « body » : il suit déjà les bonds, roulades et écrasements du corps
            var holder = new GameObject("skin3d").transform;
            holder.SetParent(body, false);
            model = Object.Instantiate(prefab, holder, false).transform;
            Quaternion q0 = model.localRotation;
            Vector3 s0 = model.localScale;
            model.localPosition = Vector3.zero;
            foreach (Animator a in model.GetComponentsInChildren<Animator>(true)) Object.Destroy(a);
            foreach (Animation a in model.GetComponentsInChildren<Animation>(true)) Object.Destroy(a);

            Transform hips = Find(model, "Hips"), head = Find(model, "HeadTop_End") ?? Find(model, "Head");
            Transform toe = Find(model, "LeftToe_End") ?? Find(model, "LeftFoot");
            if (hips == null || head == null || toe == null) throw new System.Exception("squelette mixamorig introuvable");
            hipsBone = hips;

            // face avant vers +Z, pieds au sol, taille du personnage du jeu
            Transform la = Find(model, "LeftArm"), ra = Find(model, "RightArm");
            if (la != null && ra != null && holder.InverseTransformPoint(la.position).x > holder.InverseTransformPoint(ra.position).x)
                model.localRotation = Quaternion.Euler(0f, 180f, 0f) * q0;
            float h = holder.InverseTransformPoint(head.position).y - holder.InverseTransformPoint(toe.position).y;
            float k = Height / Mathf.Max(0.01f, h);
            model.localScale = s0 * k;
            float foot = holder.InverseTransformPoint(toe.position).y;
            Vector3 mid = holder.InverseTransformPoint(hips.position);
            model.localPosition = new Vector3(-mid.x, -foot, -mid.z);

            // texture du modèle (matériau du jeu, compatible URP)
            Texture2D tex = Resources.Load<Texture2D>("SamediRouge/Characters/" + name + "_tex");
            Material mat = tex != null ? Art.Tex(tex, Vector2.one, 0.35f) : Art.Mat(Color.white, 0.35f);
            skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (SkinnedMeshRenderer s in skins)
            {
                s.sharedMaterial = mat;
                s.updateWhenOffscreen = true;
                s.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            hipsBind = body.InverseTransformPoint(hips.position);
            footL = Find(model, "LeftFoot"); footR = Find(model, "RightFoot");
            codeKnL = r.knL; codeKnR = r.knR;
            if (footL != null) footH = body.InverseTransformPoint(footL.position).y;
            codeHipsRest = codeHips.localPosition;

            // articulations du jeu → os du modèle (direction de repos des membres : vers le bas, un peu écartés)
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
                // les limbes ont un nœud « joint » intermédiaire dont les pièces sont aussi le corps
            }
            if (r.cap != null) { r.cap.gameObject.SetActive(false); r.cap = null; }

            jointSet = joints;
            Relays(r.handR, Find(model, "RightHand"), true, Vector3.one);
            Relays(r.handL, Find(model, "LeftHand"), true, Vector3.one);
            // tête et buste du modèle plus petits que ceux du personnage en blocs : l'équipement est réduit en proportion
            Transform neckB = Find(model, "Neck"), spineB = Find(model, "Spine"), top = Find(model, "HeadTop_End");
            Transform lArm = Find(model, "LeftArm"), rArm = Find(model, "RightArm");
            System.Func<Transform, Vector3> P = t => body.InverseTransformPoint(t.position);
            float headK = neckB != null && top != null ? Mathf.Clamp((P(top).y - P(neckB).y) / 0.455f, 0.35f, 1.2f) : 0.6f;
            float wideK = lArm != null && rArm != null ? Mathf.Clamp(Mathf.Abs(P(lArm).x - P(rArm).x) / 0.58f, 0.4f, 1.3f) : 0.8f;
            float tallK = neckB != null && spineB != null ? Mathf.Clamp((P(neckB).y - P(spineB).y) / 0.5f, 0.4f, 1.3f) : 0.8f;
            Relays(r.neck, neckB, false, Vector3.one * headK);
            Relays(r.spine, spineB, false, new Vector3(wideK, tallK, wideK));
            Apply();
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
        void Relays(Transform code, Transform bone, bool keepAll, Vector3 k)
        {
            if (code == null || bone == null) return;
            var proxy = new GameObject("relay_" + code.name).transform;
            proxy.SetParent(body, false);
            proxy.localScale = Vector3.Scale(code.localScale, k);
            var move = new List<Transform>();
            for (int c = 0; c < code.childCount; c++)
            {
                Transform t = code.GetChild(c);
                if (!t.gameObject.activeSelf && t.GetComponent<MeshRenderer>() != null) continue;   // pièce du corps masquée
                if (t.GetComponent<MeshRenderer>() != null) continue;
                if (t.name == "joint" || jointSet.Contains(t)) continue;
                move.Add(t);
            }
            foreach (Transform t in move) t.SetParent(proxy, false);
            relays.Add(new Relay { proxy = proxy, bone = bone, code = code, offset = Vector3.zero });
        }

        HashSet<Transform> jointSet;

        /// <summary>Parent à utiliser pour de l'équipement ajouté plus tard sur une articulation (relais s'il existe).</summary>
        public Transform RelayOf(Transform code)
        {
            foreach (Relay r in relays) if (r.code == code) return r.proxy;
            return code;
        }

        public void SetVisible(bool v)
        {
            foreach (SkinnedMeshRenderer s in skins) if (s != null) s.enabled = v;
        }

        /// <summary>Recopie la pose du Rig sur le modèle (à appeler après l'animation du Rig).</summary>
        public void Apply()
        {
            Quaternion bodyRot = body.rotation;
            // bassin : suit la hauteur et les décalages du bassin dessiné
            Vector3 d = codeHips.localPosition - codeHipsRest;
            hipsBone.position = body.TransformPoint(hipsBind + d);
            foreach (Pair p in pairs)
            {
                Quaternion delta = Quaternion.Inverse(bodyRot) * p.code.rotation;
                p.bone.rotation = bodyRot * (delta * p.corr * p.bind);
            }
            // pieds au sol : le modèle a des jambes plus longues que le personnage en blocs ; quand celui-ci
            // plie les genoux (garde, accroupi), on recale le bassin pour que les semelles restent au niveau des siennes
            if (footL != null && footR != null && codeKnL != null && codeKnR != null)
            {
                float codeSole = Mathf.Min(body.InverseTransformPoint(codeKnL.TransformPoint(new Vector3(0f, -0.5f, 0f))).y,
                                           body.InverseTransformPoint(codeKnR.TransformPoint(new Vector3(0f, -0.5f, 0f))).y);
                float modelSole = Mathf.Min(body.InverseTransformPoint(footL.position).y, body.InverseTransformPoint(footR.position).y) - footH;
                hipsBone.position = body.TransformPoint(body.InverseTransformPoint(hipsBone.position) + new Vector3(0f, codeSole - modelSole, 0f));
            }
            foreach (Relay r in relays)
            {
                r.proxy.position = r.bone.position;
                r.proxy.rotation = r.code.rotation;
            }
        }
    }
}
