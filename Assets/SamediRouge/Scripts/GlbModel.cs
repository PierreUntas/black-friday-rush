using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SamediRouge
{
    /// <summary>
    /// Petit lecteur de modèles 3D au format GLB (sortie Meshy), sans paquet externe.
    /// Les fichiers sont rangés dans Resources/SamediRouge/Models/&lt;nom&gt;.bytes (un .glb renommé).
    /// Lit chaque primitive (positions, normales, UV, indices) et la texture de couleur ;
    /// passe du repère glTF (main droite) à celui de Unity (main gauche).
    /// </summary>
    public static class GlbModel
    {
        class Part { public Mesh mesh; public Material mat; }
        static readonly Dictionary<string, List<Part>> cache = new Dictionary<string, List<Part>>();

        public static bool Exists(string name) => Load(name) != null;

        /// <summary>Instancie le modèle sous parent (null si le fichier manque ou est illisible).</summary>
        public static Transform Spawn(string name, Transform parent)
        {
            List<Part> parts = Load(name);
            if (parts == null) return null;
            var root = new GameObject("glb_" + name).transform;
            root.SetParent(parent, false);
            foreach (Part p in parts)
            {
                var go = new GameObject("mesh");
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = p.mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = p.mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return root;
        }

        static List<Part> Load(string name)
        {
            if (cache.TryGetValue(name, out List<Part> c)) return c;
            List<Part> parts = null;
            try
            {
                TextAsset ta = Resources.Load<TextAsset>("SamediRouge/Models/" + name);
                if (ta != null) parts = Parse(ta.bytes, name);
            }
            catch (Exception e) { Debug.LogWarning("[GLB] " + name + " : " + e.Message); parts = null; }
            cache[name] = parts;
            return parts;
        }

        // ------------------------------------------------------------------ lecture du conteneur GLB

        [Serializable] class JRoot { public JMesh[] meshes; public JAccessor[] accessors; public JView[] bufferViews; public JMaterial[] materials; public JTexture[] textures; public JImage[] images; }
        [Serializable] class JMesh { public JPrim[] primitives; }
        [Serializable] class JPrim { public JAttr attributes; public int indices = -1; public int material = -1; }
        [Serializable] class JAttr { public int POSITION = -1; public int NORMAL = -1; public int TEXCOORD_0 = -1; }
        [Serializable] class JAccessor { public int bufferView = -1; public int byteOffset; public int componentType; public int count; public string type; }
        [Serializable] class JView { public int byteOffset; public int byteLength; public int byteStride; }
        [Serializable] class JMaterial { public JPbr pbrMetallicRoughness; }
        [Serializable] class JPbr { public JTexRef baseColorTexture; public float[] baseColorFactor; public float roughnessFactor = 1f; }
        [Serializable] class JTexRef { public int index = -1; }
        [Serializable] class JTexture { public int source = -1; }
        [Serializable] class JImage { public int bufferView = -1; }

        static List<Part> Parse(byte[] b, string name)
        {
            if (b.Length < 20 || BitConverter.ToUInt32(b, 0) != 0x46546C67) throw new Exception("pas un GLB");
            int off = 12;
            string json = null;
            int binStart = -1;
            while (off + 8 <= b.Length)
            {
                int len = BitConverter.ToInt32(b, off);
                uint type = BitConverter.ToUInt32(b, off + 4);
                if (type == 0x4E4F534A) json = Encoding.UTF8.GetString(b, off + 8, len);
                else if (type == 0x004E4942) binStart = off + 8;
                off += 8 + len;
            }
            if (json == null || binStart < 0) throw new Exception("GLB incomplet");
            JRoot j = JsonUtility.FromJson<JRoot>(json);

            var texCache = new Dictionary<int, Texture2D>();
            var parts = new List<Part>();
            foreach (JMesh m in j.meshes)
                foreach (JPrim p in m.primitives)
                {
                    Vector3[] pos = ReadVec(j, b, binStart, p.attributes.POSITION, 3);
                    Vector3[] nor = p.attributes.NORMAL >= 0 ? ReadVec(j, b, binStart, p.attributes.NORMAL, 3) : null;
                    Vector3[] uv3 = p.attributes.TEXCOORD_0 >= 0 ? ReadVec(j, b, binStart, p.attributes.TEXCOORD_0, 2) : null;
                    int[] idx = p.indices >= 0 ? ReadIdx(j, b, binStart, p.indices) : Seq(pos.Length);
                    // glTF (main droite) → Unity (main gauche) : x inversé, triangles retournés, v des UV inversé
                    for (int i = 0; i < pos.Length; i++) pos[i].x = -pos[i].x;
                    if (nor != null) for (int i = 0; i < nor.Length; i++) nor[i].x = -nor[i].x;
                    for (int i = 0; i + 2 < idx.Length; i += 3) { int t = idx[i + 1]; idx[i + 1] = idx[i + 2]; idx[i + 2] = t; }
                    var mesh = new Mesh { name = name, indexFormat = pos.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
                    mesh.vertices = pos;
                    if (uv3 != null)
                    {
                        var uv = new Vector2[uv3.Length];
                        for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(uv3[i].x, 1f - uv3[i].y);
                        mesh.uv = uv;
                    }
                    mesh.triangles = idx;
                    if (nor != null) mesh.normals = nor; else mesh.RecalculateNormals();
                    mesh.RecalculateBounds();

                    Material mat = null;
                    if (p.material >= 0 && j.materials != null && p.material < j.materials.Length)
                    {
                        JPbr pbr = j.materials[p.material].pbrMetallicRoughness;
                        int ti = pbr != null && pbr.baseColorTexture != null ? pbr.baseColorTexture.index : -1;
                        if (ti >= 0)
                        {
                            if (!texCache.TryGetValue(ti, out Texture2D tex))
                            {
                                tex = ReadImage(j, b, binStart, j.textures[ti].source, name + "_" + ti);
                                texCache[ti] = tex;
                            }
                            if (tex != null) mat = Art.Tex(tex, Vector2.one, 0.55f);
                        }
                    }
                    if (mat == null) mat = Art.Mat(Color.white, 0.4f);
                    parts.Add(new Part { mesh = mesh, mat = mat });
                }
            return parts;
        }

        static Texture2D ReadImage(JRoot j, byte[] b, int bin, int img, string name)
        {
            if (img < 0 || j.images == null || img >= j.images.Length || j.images[img].bufferView < 0) return null;
            JView v = j.bufferViews[j.images[img].bufferView];
            var data = new byte[v.byteLength];
            Buffer.BlockCopy(b, bin + v.byteOffset, data, 0, v.byteLength);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp };
            if (!tex.LoadImage(data, true)) return null;
            return tex;
        }

        static int Comp(string type) => type == "SCALAR" ? 1 : type == "VEC2" ? 2 : type == "VEC3" ? 3 : 4;

        static Vector3[] ReadVec(JRoot j, byte[] b, int bin, int acc, int dim)
        {
            JAccessor a = j.accessors[acc];
            JView v = j.bufferViews[a.bufferView];
            int n = Comp(a.type);
            int stride = v.byteStride > 0 ? v.byteStride : n * 4;
            int start = bin + v.byteOffset + a.byteOffset;
            var r = new Vector3[a.count];
            for (int i = 0; i < a.count; i++)
            {
                int o = start + i * stride;
                float x = BitConverter.ToSingle(b, o), y = BitConverter.ToSingle(b, o + 4);
                float z = dim >= 3 ? BitConverter.ToSingle(b, o + 8) : 0f;
                r[i] = new Vector3(x, y, z);
            }
            return r;
        }

        static int[] ReadIdx(JRoot j, byte[] b, int bin, int acc)
        {
            JAccessor a = j.accessors[acc];
            JView v = j.bufferViews[a.bufferView];
            int start = bin + v.byteOffset + a.byteOffset;
            var r = new int[a.count];
            for (int i = 0; i < a.count; i++)
                r[i] = a.componentType == 5125 ? (int)BitConverter.ToUInt32(b, start + i * 4)
                     : a.componentType == 5123 ? BitConverter.ToUInt16(b, start + i * 2)
                     : b[start + i];
            return r;
        }

        static int[] Seq(int n) { var r = new int[n]; for (int i = 0; i < n; i++) r[i] = i; return r; }
    }
}
