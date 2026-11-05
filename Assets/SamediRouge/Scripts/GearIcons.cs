using System.Collections.Generic;
using UnityEngine;

namespace SamediRouge
{
    /// <summary>
    /// Icônes d'équipement dessinées par code (même dessin que la maquette « Icônes d'équipement ») :
    /// formes vectorielles (chemins, rectangles arrondis, cercles, traits) dans un repère 64 × 64 (y vers le bas),
    /// rendues par distance signée avec anticrénelage dans une texture 128 × 128.
    /// Policiers : casque (3 niveaux), gilet, matraque (3 niveaux), équipe. Voleurs : cagoule, sweat, barre de fer, bande.
    /// En course : bouclier, pistolet paralysant, mitraillette.
    /// </summary>
    public static class GearIcons
    {
        const int N = 128;
        const float S = N / 64f;
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        // ---------------------------------------------------------------- choix de l'icône

        /// <summary>Icône d'une amélioration permanente (0 casque, 1 gilet, 2 arme, 3 équipe) au niveau donné (0 = aperçu du niveau 1).</summary>
        /// <summary>
        /// Icône d'un objet gagné : ligne 0 tête, 1 corps, 2 arme, 3 bouclier ; niveau 1 à 3 = l'objet (0 = aperçu du premier).
        /// Policiers : casque → à visière → d'élite ; gilet → pare-balles → armure ; matraque → taser → mitraillette à mousse ;
        /// bouclier en bois → anti-émeute → balistique. Voleurs : bandeau → lunettes de ski → casque de moto ; sweat → blouson →
        /// gilet volé ; batte → pistolet à peinture → mitrailleuse à peinture ; couvercle de poubelle → panneau STOP → bouclier volé.
        /// </summary>
        public static Sprite Upgrade(int gear, bool thief, int level)
        {
            int lv = Mathf.Clamp(level, 1, 3);
            string[] line = { "h", "b", "w", "s" };
            return Get((thief ? "t_" : "c_") + line[Mathf.Clamp(gear, 0, 3)] + lv);
        }

        /// <summary>Icône par défaut d'une case de la barre en course (0 tête, 1 corps, 2 arme, 3 bouclier, 4 plaque, 5 charge, 6 équipe).</summary>
        public static Sprite Slot(int slot, bool thief)
        {
            if (slot <= 3) return Upgrade(slot, thief, 1);
            if (slot == 4) return Get("plate");
            if (slot == 5) return Get("charge");
            return Get(thief ? "gang" : "crew");
        }

        static Sprite SlotOld(int slot, bool thief)
        {
            switch (slot)
            {
                case 0: return Get(thief ? "mask" : "helmet2");
                case 1: return Get(thief ? "hoodie" : "vest");
                case 2: return Get(thief ? "bar" : "baton2");
                case 3: return Get("shield");
                case 4: return Get("taser");
                case 5: return Get("smg");
                default: return Get(thief ? "gang" : "crew");
            }
        }

        /// <summary>Illustration dessinée Resources/SamediRouge/Art/&lt;id&gt;.png, ou null s'il n'y en a pas.</summary>
        public static Sprite Art(string id)
        {
            Sprite s;
            if (artCache.TryGetValue(id, out s)) return s;
            Texture2D t = Resources.Load<Texture2D>("SamediRouge/Art/" + id);
            s = t != null ? Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f) : null;
            artCache[id] = s;
            return s;
        }
        static readonly Dictionary<string, Sprite> artCache = new Dictionary<string, Sprite>();

        public static Sprite Get(string id)
        {
            Sprite s;
            if (cache.TryGetValue(id, out s)) return s;
            // illustration dessinée (Resources/SamediRouge/Art/<id>.png) si elle existe, sinon l'icône vectorielle
            Texture2D art = Resources.Load<Texture2D>("SamediRouge/Art/" + id);
            if (art != null)
            {
                s = Sprite.Create(art, new Rect(0, 0, art.width, art.height), new Vector2(0.5f, 0.5f), 100f);
                cache[id] = s;
                return s;
            }
            var shapes = new List<Shape>();
            Build(id, shapes);
            s = Render(shapes);
            cache[id] = s;
            return s;
        }

        // ---------------------------------------------------------------- couleurs

        static Color H(string hex)
        {
            Color c;
            ColorUtility.TryParseHtmlString(hex, out c);
            return c;
        }

        static readonly Color Ink = new Color(0.067f, 0.078f, 0.106f);
        static readonly Color Navy = H("#2a3b6b"), NavyDark = H("#1d2a4f"), Gold = H("#ffd84a"), VisorBlue = H("#8fd3ff");
        static readonly Color Steel = H("#2b2f38"), SteelMid = H("#3a3f4b"), SteelLight = H("#6b7180"), Ring = H("#b9c0cc");
        static readonly Color Cyan = H("#7ff3ff"), Blue = H("#3d7cff"), Red = H("#e23b3b"), Skin = H("#f0c9a0"), Dark = H("#23262e"), Darker = H("#1b1d23");

        // ---------------------------------------------------------------- dessins

        static void Build(string id, List<Shape> o)
        {
            switch (id)
            {
                case "helmet1": Dome(o, Ink, 3f); Badge(o); Brim(o, NavyDark); break;
                case "helmet2": Dome(o, Ink, 3f); Visor(o); Badge(o); Brim(o, NavyDark); break;
                case "helmet3":
                    Dome(o, Gold, 3.5f); Visor(o);
                    o.Add(Poly(Star(32f, 17f, 8f, 3.6f), Gold, Ink, 1.8f));
                    Brim(o, Gold);
                    break;
                case "vest":
                    o.Add(Poly(new P(19, 8).L(26, 8).C(27, 14, 37, 14, 38, 8).L(45, 8).L(52, 17).L(52, 56).L(12, 56).L(12, 17).Pts, Navy, Ink, 3f));
                    o.Add(Box(12, 32, 40, 7, 0, H("#f2f2f2"), Ink, 2f));
                    o.Add(Box(17, 43, 11, 9, 2, NavyDark, Ink, 2f));
                    o.Add(Box(36, 43, 11, 9, 2, NavyDark, Ink, 2f));
                    o.Add(Box(16, 20, 9, 6, 1.5f, Gold, Ink, 1.8f));
                    break;
                case "baton1":
                    o.Add(Box(28, 5, 8, 44, 4, Steel, Ink, 3f).Rot(-40f));
                    o.Add(Box(26, 45, 12, 14, 4, SteelLight, Ink, 3f).Rot(-40f));
                    break;
                case "baton2":
                    o.Add(Box(26, 4, 12, 44, 6, SteelMid, Ink, 3f).Rot(-40f));
                    o.Add(Box(25, 12, 14, 4, 0, Ring, Ink, 1.5f).Rot(-40f));
                    o.Add(Box(25, 22, 14, 4, 0, Ring, Ink, 1.5f).Rot(-40f));
                    o.Add(Box(18, 36, 10, 6, 2, SteelMid, Ink, 2.5f).Rot(-40f));
                    o.Add(Box(25, 44, 14, 15, 4, SteelLight, Ink, 3f).Rot(-40f));
                    break;
                case "baton3":
                    o.Add(Box(26, 4, 12, 24, 6, Cyan, Ink, 3f).Rot(-40f));
                    o.Add(Box(26, 24, 12, 24, 3, Steel, Ink, 3f).Rot(-40f));
                    o.Add(Box(25, 44, 14, 15, 4, SteelLight, Ink, 3f).Rot(-40f));
                    o.Add(Line(new P(44, 6).L(40, 13).L(45, 13).L(41, 21).Pts, Cyan, 3f));
                    o.Add(Line(new P(54, 16).L(49, 19).L(53, 22).L(48, 25).Pts, Cyan, 3f));
                    break;
                case "crew":
                    Person(o, 16, 26, 7, new P(5, 50).C(5, 40, 10, 36, 16, 36).C(22, 36, 27, 40, 27, 50).Pts, Navy);
                    Person(o, 48, 26, 7, new P(37, 50).C(37, 40, 42, 36, 48, 36).C(54, 36, 59, 40, 59, 50).Pts, Navy);
                    Person(o, 32, 22, 9, new P(18, 54).C(18, 42, 24, 36, 32, 36).C(40, 36, 46, 42, 46, 54).Pts, Blue);
                    o.Add(Box(28.5f, 11, 7, 5, 1, Gold, Ink, 1.5f));
                    break;
                case "shield":
                    o.Add(Box(14, 6, 36, 52, 8, H("#bfe3ff"), Ink, 3f));
                    o.Add(Box(14, 22, 36, 9, 0, Navy, Ink, 2f));
                    o.Add(Line(new P(20, 40).L(26, 36).Pts, Color.white, 3f));
                    o.Add(Line(new P(20, 48).L(32, 40).Pts, Color.white, 3f));
                    break;
                case "taser":
                    o.Add(Poly(new P(8, 20).L(50, 20).C(54, 20, 56, 22, 56, 26).L(56, 32).L(30, 32).L(26, 50).C(25, 53, 23, 54, 20, 54).L(14, 54).L(18, 32).L(8, 32).Pts, Gold, Ink, 3f));
                    o.Add(Box(8, 20, 10, 12, 0, Steel, Ink, 3f));
                    o.Add(Line(new P(4, 22).L(1, 18).Pts, Cyan, 3f));
                    o.Add(Line(new P(4, 30).L(1, 34).Pts, Cyan, 3f));
                    break;
                case "smg":
                    o.Add(Poly(new P(6, 22).L(46, 22).L(46, 18).L(52, 18).L(52, 22).L(60, 22).L(60, 30).L(40, 30).L(38, 36).L(30, 36).L(30, 52).L(22, 52).L(22, 36).L(18, 36).L(16, 30).L(6, 30).Pts, SteelMid, Ink, 3f));
                    o.Add(Box(32, 36, 7, 14, 0, Steel, Ink, 2.5f));
                    o.Add(Box(10, 24, 20, 3, 0, SteelLight, Ink, 0f));
                    break;
                case "mask":
                    o.Add(Poly(new P(14, 46).C(12, 24, 20, 8, 32, 8).C(44, 8, 52, 24, 50, 46).C(48, 54, 40, 58, 32, 58).C(24, 58, 16, 54, 14, 46).Pts, Dark, Ink, 3f));
                    o.Add(Box(18, 26, 28, 10, 5, Skin, Ink, 2.5f));
                    o.Add(Circ(26, 31, 2.6f, Ink, Ink, 0f));
                    o.Add(Circ(38, 31, 2.6f, Ink, Ink, 0f));
                    o.Add(Line(new P(18, 18).C(24, 15, 40, 15, 46, 18).Pts, Red, 3.5f));
                    break;
                case "hoodie":
                    o.Add(Poly(new P(22, 8).C(26, 4, 38, 4, 42, 8).L(52, 16).L(58, 34).L(50, 37).L(48, 30).L(48, 56).L(16, 56).L(16, 30).L(14, 37).L(6, 34).L(12, 16).Pts, Steel, Ink, 3f));
                    o.Add(Poly(new P(24, 9).C(26, 18, 38, 18, 40, 9).Pts, Darker, Ink, 2.5f));
                    o.Add(Box(22, 40, 20, 9, 3, Darker, Ink, 2f));
                    o.Add(Line(new P(16, 30).L(48, 30).Pts, Red, 3.5f));
                    break;
                case "bar":
                    {
                        var pts = new P(14, 56).L(46, 12).C(48, 9, 52, 8, 55, 10).C(57, 12, 57, 15, 54, 17).Pts;
                        o.Add(Line(pts, Ink, 10f));
                        o.Add(Line(pts, Red, 5f));
                    }
                    break;
                // ------------------------------------------------ objets gagnés : policiers
                case "c_h1": Build("helmet1", o); break;
                case "c_h2": Build("helmet2", o); break;
                case "c_h3": Build("helmet3", o); break;
                case "c_b1": Build("vest", o); break;
                case "c_b2":
                    Build("vest", o);
                    o.Add(Box(6, 12, 15, 12, 6, SteelLight, Ink, 3f));
                    o.Add(Box(43, 12, 15, 12, 6, SteelLight, Ink, 3f));
                    o.Add(Box(24, 42, 16, 12, 2, SteelMid, Ink, 2f));
                    break;
                case "c_b3":
                    Build("vest", o);
                    o.Add(Box(6, 12, 15, 12, 6, Gold, Ink, 3f));
                    o.Add(Box(43, 12, 15, 12, 6, Gold, Ink, 3f));
                    o.Add(Box(12, 32, 40, 7, 0, Gold, Ink, 2f));
                    o.Add(Poly(Star(32f, 22f, 6f, 2.6f), Gold, Ink, 1.5f));
                    break;
                case "c_w1": Build("baton1", o); break;
                case "c_w2": Build("taser", o); break;
                case "c_w3":
                    // mitraillette à mousse : corps orange, canon bleu, chargeur jaune, fléchettes
                    o.Add(Poly(new P(6, 24).L(44, 24).L(44, 20).L(50, 20).L(50, 24).L(60, 24).L(60, 32).L(40, 32).L(38, 38).L(30, 38).L(30, 52).L(22, 52).L(22, 38).L(18, 38).L(16, 32).L(6, 32).Pts, H("#ff8a2a"), Ink, 3f));
                    o.Add(Box(44, 25, 18, 6, 2, Blue, Ink, 2.5f));
                    o.Add(Box(31, 38, 9, 14, 2, Gold, Ink, 2.5f));
                    o.Add(Box(8, 26, 18, 3, 1, H("#ffc58f"), Ink, 0f));
                    break;
                case "c_s1":
                    // bouclier en bois : planches, clous
                    o.Add(Box(13, 6, 38, 52, 10, H("#b07a45"), Ink, 3f));
                    o.Add(Line(new P(25, 9).L(25, 55).Pts, H("#7d5230"), 2.5f));
                    o.Add(Line(new P(38, 9).L(38, 55).Pts, H("#7d5230"), 2.5f));
                    o.Add(Box(13, 26, 38, 6, 0, H("#7d5230"), Ink, 2f));
                    foreach (var c in new[] { new Vector2(19, 29), new Vector2(31, 29), new Vector2(45, 29) }) o.Add(Circ(c.x, c.y, 1.8f, Ring, Ink, 1f));
                    break;
                case "c_s2": Build("shield", o); break;
                case "c_s3":
                    // bouclier balistique : acier sombre, hublot, liseré doré
                    o.Add(Box(12, 5, 40, 54, 9, H("#2b3550"), Gold, 3.5f));
                    o.Add(Box(20, 11, 24, 10, 3, VisorBlue, Ink, 2.5f));
                    o.Add(Box(12, 30, 40, 8, 0, H("#f2f2f2"), Ink, 2f));
                    o.Add(Poly(Star(32f, 48f, 6.5f, 2.8f), Gold, Ink, 1.5f));
                    break;

                // ------------------------------------------------ objets gagnés : voleurs
                case "t_h1": Head(o); o.Add(Box(13, 17, 38, 7, 3, Red, Ink, 2.5f)); break;
                case "t_h2":
                    Head(o); o.Add(Box(13, 17, 38, 7, 3, Red, Ink, 2.5f));
                    o.Add(Box(15, 9, 34, 11, 5, Ink, Ink, 0f));
                    o.Add(Box(17, 10.5f, 13, 8, 4, H("#ff8a1a"), Ink, 1.5f));
                    o.Add(Box(34, 10.5f, 13, 8, 4, H("#ff8a1a"), Ink, 1.5f));
                    break;
                case "t_h3":
                    // casque de moto : coque noire brillante, visière orange, bande rouge
                    o.Add(Poly(new P(10, 44).C(9, 20, 20, 7, 32, 7).C(44, 7, 55, 20, 54, 44).C(54, 52, 48, 56, 40, 56).L(24, 56).C(16, 56, 10, 52, 10, 44).Pts, H("#1d2028"), Ink, 3f));
                    o.Add(Poly(new P(16, 30).C(18, 22, 26, 20, 34, 20).C(44, 20, 50, 24, 52, 32).L(52, 38).L(16, 38).Pts, H("#ff8a1a"), Ink, 3f));
                    o.Add(Line(new P(22, 27).C(26, 24, 30, 23, 34, 23).Pts, Color.white, 2.5f));
                    o.Add(Line(new P(32, 8).C(32, 12, 32, 16, 32, 19).Pts, Red, 4f));
                    break;
                case "t_b1": Build("hoodie", o); break;
                case "t_b2":
                    // blouson de cuir : marron, col, fermeture éclair
                    o.Add(Poly(new P(20, 8).L(44, 8).L(54, 16).L(58, 36).L(50, 38).L(48, 32).L(48, 56).L(16, 56).L(16, 32).L(14, 38).L(6, 36).L(10, 16).Pts, H("#6b3f26"), Ink, 3f));
                    o.Add(Poly(new P(20, 8).L(32, 22).L(26, 26).L(18, 14).Pts, H("#8a5634"), Ink, 2f));
                    o.Add(Poly(new P(44, 8).L(32, 22).L(38, 26).L(46, 14).Pts, H("#8a5634"), Ink, 2f));
                    o.Add(Line(new P(32, 22).L(32, 54).Pts, Ring, 2.5f));
                    o.Add(Box(19, 40, 8, 3, 1, Ring, Ink, 1f));
                    o.Add(Box(37, 40, 8, 3, 1, Ring, Ink, 1f));
                    break;
                case "t_b3":
                    Build("vest", o);
                    o.Add(Line(new P(14, 14).L(50, 54).Pts, Red, 5f));
                    o.Add(Line(new P(50, 14).L(14, 54).Pts, Red, 5f));
                    break;
                case "t_w1":
                    // batte de baseball en bois : manche fin scotché, tête épaisse arrondie
                    o.Add(Poly(new List<Vector2> { new Vector2(12.2f, 58.1f), new Vector2(32.9f, 37.8f), new Vector2(57.8f, 17.5f), new Vector2(46.2f, 6.5f), new Vector2(27.1f, 32.2f), new Vector2(7.8f, 53.9f) }, H("#c98c4f"), Ink, 3f));
                    o.Add(Circ(52, 12, 8f, H("#c98c4f"), Ink, 3f));
                    o.Add(Line(new P(13, 55).L(21, 47).Pts, H("#3a3f4b"), 6.5f));
                    o.Add(Circ(9, 57, 4f, H("#3a3f4b"), Ink, 2f));
                    o.Add(Line(new P(44, 13).L(50, 8).Pts, H("#e8bd8a"), 2.5f));
                    break;
                case "t_w2":
                    // pistolet à peinture : corps rose, réservoir vert
                    o.Add(Poly(new P(8, 24).L(52, 24).C(56, 24, 58, 26, 58, 30).L(58, 34).L(30, 34).L(26, 52).C(25, 55, 23, 56, 20, 56).L(14, 56).L(18, 34).L(8, 34).Pts, H("#ff4fa3"), Ink, 3f));
                    o.Add(Circ(36, 15, 9f, H("#3fd17a"), Ink, 3f));
                    o.Add(Box(33, 21, 6, 5, 0, H("#3fd17a"), Ink, 2f));
                    o.Add(Circ(3, 29, 3f, H("#3fd17a"), Ink, 1.5f));
                    break;
                case "t_w3":
                    // mitrailleuse à peinture : trois canons, réservoir vert, poignée
                    o.Add(Box(6, 22, 34, 16, 5, H("#8a3cff"), Ink, 3f));
                    o.Add(Box(38, 20, 22, 6, 3, SteelMid, Ink, 2.5f));
                    o.Add(Box(38, 27, 22, 6, 3, SteelMid, Ink, 2.5f));
                    o.Add(Box(38, 34, 22, 6, 3, SteelMid, Ink, 2.5f));
                    o.Add(Circ(20, 13, 9f, H("#3fd17a"), Ink, 3f));
                    o.Add(Box(14, 38, 9, 16, 2, H("#5a2aa8"), Ink, 2.5f));
                    o.Add(Circ(62, 23, 2.5f, H("#ff4fa3"), Ink, 1f));
                    o.Add(Circ(62, 37, 2.5f, H("#3fd17a"), Ink, 1f));
                    break;
                case "t_s1":
                    // couvercle de poubelle : rond, nervures, poignée
                    o.Add(Circ(32, 32, 26f, H("#9aa3ad"), Ink, 3f));
                    o.Add(Circ(32, 32, 18f, H("#b9c0cc"), Ink, 2f));
                    o.Add(Circ(32, 32, 10f, H("#9aa3ad"), Ink, 2f));
                    o.Add(Box(24, 29, 16, 6, 3, SteelMid, Ink, 2f));
                    break;
                case "t_s2":
                    // panneau STOP arraché : octogone rouge, liseré blanc
                    {
                        var oct = new List<Vector2>();
                        for (int k = 0; k < 8; k++) { float a = Mathf.PI / 8f + k * Mathf.PI / 4f; oct.Add(new Vector2(32 + Mathf.Cos(a) * 28f, 32 + Mathf.Sin(a) * 28f)); }
                        o.Add(Poly(oct, Color.white, Ink, 3f));
                        var oct2 = new List<Vector2>();
                        for (int k = 0; k < 8; k++) { float a = Mathf.PI / 8f + k * Mathf.PI / 4f; oct2.Add(new Vector2(32 + Mathf.Cos(a) * 23f, 32 + Mathf.Sin(a) * 23f)); }
                        o.Add(Poly(oct2, Red, Ink, 0f));
                        o.Add(Box(16, 28, 32, 8, 2, Color.white, Ink, 0f));
                    }
                    break;
                case "t_s3":
                    Build("shield", o);
                    o.Add(Line(new P(16, 10).L(48, 54).Pts, Red, 5f));
                    o.Add(Line(new P(48, 10).L(16, 54).Pts, Red, 5f));
                    break;

                // ------------------------------------------------ bonus de course
                case "plate":
                    // plaque d'armure (encaisse un coup)
                    o.Add(Poly(new P(14, 10).L(50, 10).L(54, 18).L(50, 54).C(42, 60, 22, 60, 14, 54).L(10, 18).Pts, Ring, Ink, 3f));
                    o.Add(Line(new P(18, 22).L(46, 22).Pts, SteelLight, 3f));
                    foreach (var c in new[] { new Vector2(16, 14), new Vector2(48, 14), new Vector2(18, 48), new Vector2(46, 48) }) o.Add(Circ(c.x, c.y, 2f, SteelLight, Ink, 1f));
                    break;
                case "rate":
                    // bonus de cadence : éclair dans un disque bleu
                    o.Add(Circ(32, 32, 28f, Blue, Ink, 3f));
                    o.Add(Poly(new P(37, 7).L(15, 36).L(29, 36).L(24, 58).L(50, 26).L(35, 26).L(43, 7).Pts, Gold, Ink, 3f));
                    break;
                case "dmg":
                    // bonus de dégâts : explosion rouge et or
                    o.Add(Poly(Star(32f, 32f, 30f, 17f), Red, Ink, 3f));
                    o.Add(Poly(Star(32f, 32f, 17f, 9f), Gold, Ink, 2f));
                    break;
                case "charge":
                    // charge : on fonce à travers tout (double chevron dans un disque)
                    o.Add(Circ(32, 32, 27f, Red, Ink, 3f));
                    o.Add(Poly(new P(14, 18).L(26, 18).L(38, 32).L(26, 46).L(14, 46).L(26, 32).Pts, Gold, Ink, 2.5f));
                    o.Add(Poly(new P(30, 18).L(42, 18).L(54, 32).L(42, 46).L(30, 46).L(42, 32).Pts, Gold, Ink, 2.5f));
                    break;
                case "gang":
                    o.Add(Poly(new P(5, 50).C(5, 40, 9, 34, 16, 34).C(23, 34, 27, 40, 27, 50).Pts, Steel, Ink, 3f));
                    o.Add(Poly(new P(9, 30).C(9, 20, 23, 20, 23, 30).C(23, 35, 9, 35, 9, 30).Pts, Steel, Ink, 3f));
                    o.Add(Poly(new P(37, 50).C(37, 40, 41, 34, 48, 34).C(55, 34, 59, 40, 59, 50).Pts, Steel, Ink, 3f));
                    o.Add(Poly(new P(41, 30).C(41, 20, 55, 20, 55, 30).C(55, 35, 41, 35, 41, 30).Pts, Steel, Ink, 3f));
                    o.Add(Poly(new P(18, 56).C(18, 42, 24, 36, 32, 36).C(40, 36, 46, 42, 46, 56).Pts, Red, Ink, 3f));
                    o.Add(Poly(new P(22, 26).C(22, 14, 42, 14, 42, 26).C(42, 33, 22, 33, 22, 26).Pts, Red, Ink, 3f));
                    o.Add(Box(26, 23, 12, 5, 2.5f, Skin, Ink, 1.5f));
                    break;
            }
        }

        static void Dome(List<Shape> o, Color stroke, float w) =>
            o.Add(Poly(new P(11, 42).C(11, 22, 21, 11, 32, 11).C(43, 11, 53, 22, 53, 42).Pts, Navy, stroke, w));

        static void Visor(List<Shape> o)
        {
            o.Add(Poly(new P(17, 32).C(19, 26, 25, 23, 32, 23).C(39, 23, 45, 26, 47, 32).L(47, 46).C(40, 50, 24, 50, 17, 46).Pts, VisorBlue, Ink, 3f));
            o.Add(Line(new P(22, 30).C(25, 27, 29, 26, 32, 26).Pts, Color.white, 2.5f));
        }

        /// <summary>Tête de voleur cagoulée (base des icônes de tête des voleurs).</summary>
        static void Head(List<Shape> o)
        {
            o.Add(Poly(new P(14, 46).C(12, 24, 20, 8, 32, 8).C(44, 8, 52, 24, 50, 46).C(48, 54, 40, 58, 32, 58).C(24, 58, 16, 54, 14, 46).Pts, Dark, Ink, 3f));
            o.Add(Box(18, 28, 28, 9, 4.5f, Skin, Ink, 2.5f));
            o.Add(Circ(26, 32.5f, 2.4f, Ink, Ink, 0f));
            o.Add(Circ(38, 32.5f, 2.4f, Ink, Ink, 0f));
        }

        static void Badge(List<Shape> o) => o.Add(Box(28.5f, 13, 7, 8, 1.5f, Gold, Ink, 2f));
        static void Brim(List<Shape> o, Color c) => o.Add(Box(7, 40, 50, 9, 4, c, Ink, 3f));

        static void Person(List<Shape> o, float hx, float hy, float hr, List<Vector2> body, Color c)
        {
            o.Add(Circ(hx, hy, hr, c, Ink, 3f));
            o.Add(Poly(body, c, Ink, 3f));
        }

        static List<Vector2> Star(float cx, float cy, float ro, float ri)
        {
            var p = new List<Vector2>();
            for (int i = 0; i < 10; i++)
            {
                float a = -Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? ro : ri;
                p.Add(new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r));
            }
            return p;
        }

        // ---------------------------------------------------------------- formes

        /// <summary>Constructeur de chemin (M, L, C) façon SVG.</summary>
        class P
        {
            public readonly List<Vector2> Pts = new List<Vector2>();
            public P(float x, float y) { Pts.Add(new Vector2(x, y)); }
            public P L(float x, float y) { Pts.Add(new Vector2(x, y)); return this; }
            public P C(float x1, float y1, float x2, float y2, float x, float y)
            {
                Vector2 a = Pts[Pts.Count - 1], b = new Vector2(x1, y1), c = new Vector2(x2, y2), d = new Vector2(x, y);
                for (int i = 1; i <= 12; i++)
                {
                    float t = i / 12f, u = 1f - t;
                    Pts.Add(a * (u * u * u) + b * (3f * u * u * t) + c * (3f * u * t * t) + d * (t * t * t));
                }
                return this;
            }
        }

        abstract class Shape
        {
            public Color fill, stroke;
            public float width;          // épaisseur du contour (unités 64)
            public bool lineOnly;        // trait seul (pas de remplissage séparé)
            public float rot, rcx = 32f, rcy = 32f;
            public Shape Rot(float deg) { rot = deg; return this; }
            /// <summary>Distance signée au bord (négative à l'intérieur), point déjà ramené dans le repère de la forme.</summary>
            public abstract float Dist(Vector2 p);
            public float D(Vector2 p)
            {
                if (rot != 0f)
                {
                    float a = -rot * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
                    float x = p.x - rcx, y = p.y - rcy;
                    p = new Vector2(rcx + x * c - y * s, rcy + x * s + y * c);
                }
                return Dist(p);
            }
        }

        class PolyShape : Shape
        {
            public List<Vector2> pts;
            public override float Dist(Vector2 p)
            {
                float best = float.MaxValue;
                bool inside = false;
                int n = pts.Count;
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    Vector2 a = pts[j], b = pts[i];
                    best = Mathf.Min(best, SegDist(p, a, b));
                    if ((b.y > p.y) != (a.y > p.y) && p.x < (a.x - b.x) * (p.y - b.y) / (a.y - b.y) + b.x) inside = !inside;
                }
                return inside ? -best : best;
            }
        }

        class LineShape : Shape
        {
            public List<Vector2> pts;
            public override float Dist(Vector2 p)
            {
                float best = float.MaxValue;
                for (int i = 0; i < pts.Count - 1; i++) best = Mathf.Min(best, SegDist(p, pts[i], pts[i + 1]));
                return best - width * 0.5f;
            }
        }

        class BoxShape : Shape
        {
            public float x, y, w, h, r;
            public override float Dist(Vector2 p)
            {
                float cx = x + w * 0.5f, cy = y + h * 0.5f;
                float qx = Mathf.Abs(p.x - cx) - w * 0.5f + r, qy = Mathf.Abs(p.y - cy) - h * 0.5f + r;
                float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
                return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
            }
        }

        class CircShape : Shape
        {
            public float cx, cy, r;
            public override float Dist(Vector2 p) => Vector2.Distance(p, new Vector2(cx, cy)) - r;
        }

        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a, ap = p - a;
            float l2 = ab.x * ab.x + ab.y * ab.y;
            float t = l2 > 0f ? Mathf.Clamp01((ap.x * ab.x + ap.y * ab.y) / l2) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        static Shape Poly(List<Vector2> pts, Color fill, Color stroke, float w) => new PolyShape { pts = pts, fill = fill, stroke = stroke, width = w };
        static Shape Box(float x, float y, float w, float h, float r, Color fill, Color stroke, float sw) => new BoxShape { x = x, y = y, w = w, h = h, r = Mathf.Min(r, Mathf.Min(w, h) * 0.5f), fill = fill, stroke = stroke, width = sw };
        static Shape Circ(float cx, float cy, float r, Color fill, Color stroke, float sw) => new CircShape { cx = cx, cy = cy, r = r, fill = fill, stroke = stroke, width = sw };
        static Shape Line(List<Vector2> pts, Color c, float w) => new LineShape { pts = pts, fill = c, stroke = c, width = w, lineOnly = true };

        // ---------------------------------------------------------------- rendu

        static Sprite Render(List<Shape> shapes)
        {
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[N * N];
            float aa = 1f / S;   // un pixel en unités 64
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    // centre du pixel dans le repère SVG (y vers le bas ; la texture a y vers le haut)
                    Vector2 p = new Vector2((x + 0.5f) / S, (N - 1 - y + 0.5f) / S);
                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    foreach (Shape s in shapes)
                    {
                        float d = s.D(p);
                        if (s.lineOnly)
                        {
                            Over(ref r, ref g, ref b, ref a, s.fill, Mathf.Clamp01(0.5f - d / aa));
                            continue;
                        }
                        Over(ref r, ref g, ref b, ref a, s.fill, Mathf.Clamp01(0.5f - d / aa));
                        if (s.width > 0f) Over(ref r, ref g, ref b, ref a, s.stroke, Mathf.Clamp01(0.5f - (Mathf.Abs(d) - s.width * 0.5f) / aa));
                    }
                    px[y * N + x] = new Color32((byte)(Mathf.Clamp01(r) * 255f), (byte)(Mathf.Clamp01(g) * 255f), (byte)(Mathf.Clamp01(b) * 255f), (byte)(Mathf.Clamp01(a) * 255f));
                }
            t.SetPixels32(px);
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        }

        static void Over(ref float r, ref float g, ref float b, ref float a, Color c, float cov)
        {
            float sa = c.a * cov;
            if (sa <= 0f) return;
            float oa = sa + a * (1f - sa);
            r = (c.r * sa + r * a * (1f - sa)) / oa;
            g = (c.g * sa + g * a * (1f - sa)) / oa;
            b = (c.b * sa + b * a * (1f - sa)) / oa;
            a = oa;
        }
    }
}
