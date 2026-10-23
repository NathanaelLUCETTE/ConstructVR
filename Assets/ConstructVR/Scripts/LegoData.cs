using System.Collections.Generic;
using UnityEngine;

namespace ConstructVR
{
    /// <summary>Dimensions des briques (proportions Lego réelles, agrandies pour la VR).</summary>
    public static class LegoDims
    {
        public const float Scale = 2.5f;                 // briques 2,5x plus grandes qu'en vrai : plus faciles à attraper
        public const float Pitch = 0.008f * Scale;       // distance entre deux tenons
        public const float PlateHeight = 0.0032f * Scale;
        public const float StudRadius = 0.0024f * Scale;
        public const float StudHeight = 0.0017f * Scale;
        public const float Gap = 0.0001f * Scale;        // petit jeu entre deux briques voisines
        public const float Bevel = 0.00025f * Scale;     // arrondi des arêtes
    }

    /// <summary>Un type de brique : taille en tenons (x, z) et hauteur en plaques (3 = brique, 1 = plaque).</summary>
    public class BrickShape
    {
        public readonly int Id;
        public readonly string Name;
        public readonly int SizeX;
        public readonly int SizeZ;
        public readonly int HeightPlates;

        public BrickShape(int id, string name, int sizeX, int sizeZ, int heightPlates)
        {
            Id = id; Name = name; SizeX = sizeX; SizeZ = sizeZ; HeightPlates = heightPlates;
        }

        public bool IsSquare => SizeX == SizeZ;
        public float Height => HeightPlates * LegoDims.PlateHeight;
        public string ShortName => Name.Replace("brique ", "");

        public Vector2Int Footprint(int rot) => rot % 2 == 0 ? new Vector2Int(SizeX, SizeZ) : new Vector2Int(SizeZ, SizeX);
    }

    public class BrickColor
    {
        public readonly string Name;     // accordé au féminin (« une brique rouge », « une plaque verte »)
        public readonly Color Color;
        public BrickColor(string name, Color color) { Name = name; Color = color; }
    }

    /// <summary>Position d'une brique sur la plaque de base (coordonnées en tenons et en plaques).</summary>
    [System.Serializable]
    public struct BrickPlacement
    {
        public int Shape;
        public int Color;
        public int X;       // tenon minimum en x
        public int Z;       // tenon minimum en z
        public int Level;   // hauteur en plaques
        public int Rot;     // quart de tour 0..3

        public BrickPlacement(int shape, int color, int x, int z, int level, int rot)
        {
            Shape = shape; Color = color; X = x; Z = z; Level = level; Rot = rot;
        }

        public bool SameSpot(BrickPlacement o)
        {
            if (Shape != o.Shape || X != o.X || Z != o.Z || Level != o.Level) return false;
            return LegoCatalog.Shapes[Shape].IsSquare || Rot % 2 == o.Rot % 2;
        }

        public bool Matches(BrickPlacement o) => Color == o.Color && SameSpot(o);
    }

    public enum Difficulty { Facile, Moyen, Difficile, Extreme }

    /// <summary>Animation jouée par la construction terminée.</summary>
    public enum Celebration { Spin, Drive, Castle, Robot, Flower, House, Tower, Pyramid, Rocket }

    public class BuildModel
    {
        public string Id;
        public string Name;
        public Difficulty Difficulty;
        public Celebration Celebration = Celebration.Spin;
        public string Author;              // modèles de la communauté
        public bool IsCommunity => Author != null;
        public List<BrickPlacement> Steps = new List<BrickPlacement>();
    }

    public static class LegoCatalog
    {
        public static readonly BrickShape[] Shapes =
        {
            new BrickShape(0, "brique 1x1", 1, 1, 3),
            new BrickShape(1, "brique 1x2", 1, 2, 3),
            new BrickShape(2, "brique 1x4", 1, 4, 3),
            new BrickShape(3, "brique 1x6", 1, 6, 3),
            new BrickShape(4, "brique 2x2", 2, 2, 3),
            new BrickShape(5, "brique 2x3", 2, 3, 3),
            new BrickShape(6, "brique 2x4", 2, 4, 3),
            new BrickShape(7, "plaque 1x2", 1, 2, 1),
            new BrickShape(8, "plaque 2x2", 2, 2, 1),
            new BrickShape(9, "plaque 2x4", 2, 4, 1),
        };

        public static readonly BrickColor[] Colors =
        {
            new BrickColor("rouge",   new Color(0.79f, 0.10f, 0.09f)),
            new BrickColor("bleue",   new Color(0.05f, 0.33f, 0.75f)),
            new BrickColor("jaune",   new Color(0.98f, 0.79f, 0.04f)),
            new BrickColor("verte",   new Color(0.11f, 0.55f, 0.22f)),
            new BrickColor("blanche", new Color(0.95f, 0.95f, 0.93f)),
            new BrickColor("noire",   new Color(0.11f, 0.11f, 0.12f)),
            new BrickColor("orange",  new Color(0.96f, 0.47f, 0.05f)),
            new BrickColor("grise",   new Color(0.60f, 0.62f, 0.64f)),
            new BrickColor("marron",  new Color(0.40f, 0.24f, 0.12f)),
            new BrickColor("rose",    new Color(0.96f, 0.56f, 0.76f)),
        };

        public const int BaseplateSize = 24;
        public const int BaseplateColor = 3;

        // Indices de formes et de couleurs utilisés dans les modèles
        const int B1x1 = 0, B1x2 = 1, B1x4 = 2, B1x6 = 3, B2x2 = 4, B2x4 = 6, P1x2 = 7, P2x2 = 8, P2x4 = 9;
        const int Rouge = 0, Bleu = 1, Jaune = 2, Vert = 3, Blanc = 4, Noir = 5, Gris = 7, Rose = 9;

        static BuildModel[] s_Models;

        /// <summary>Les modèles du mode guidé et du mode Défi, du plus facile au plus difficile.</summary>
        public static BuildModel[] Models => s_Models ??= new[]
        {
            Make("fleur", "Fleur", Difficulty.Facile, 11, 11, Celebration.Flower,
                P(P2x2, Vert, 0, 0, 0),
                P(B1x1, Vert, 0, 0, 1),
                P(P1x2, Vert, 1, 0, 1),
                P(B1x1, Vert, 0, 0, 4),
                P(P2x4, Rose, -1, 0, 7, 1),
                P(P2x4, Rose, 0, -1, 8),
                P(B1x1, Jaune, 0, 0, 9)),

            Make("voiture", "Voiture", Difficulty.Facile, 11, 10, Celebration.Drive,
                P(P2x4, Gris, 0, 0, 0),
                P(B1x1, Noir, -1, 0, 0), P(B1x1, Noir, 2, 0, 0), P(B1x1, Noir, -1, 3, 0), P(B1x1, Noir, 2, 3, 0),
                P(B2x4, Rouge, 0, 0, 1),
                P(P1x2, Jaune, 0, 0, 4, 1),
                P(B1x2, Bleu, 0, 1, 4, 1),
                P(B1x2, Rouge, 0, 2, 4, 1),
                P(P1x2, Rouge, 0, 3, 4, 1),
                P(P2x2, Rouge, 0, 1, 7)),

            Make("maison", "Maison", Difficulty.Moyen, 10, 10, Celebration.House,
                P(B1x4, Rouge, 0, 0, 0, 1), P(B1x4, Rouge, 0, 3, 0, 1),
                P(B1x2, Rouge, 0, 1, 0), P(B1x2, Rouge, 3, 1, 0),
                P(B1x4, Blanc, 0, 0, 3), P(B1x4, Blanc, 3, 0, 3),
                P(B1x2, Blanc, 1, 0, 3, 1), P(B1x2, Blanc, 1, 3, 3, 1),
                P(P2x4, Bleu, 0, 0, 6, 1), P(P2x4, Bleu, 0, 2, 6, 1),
                P(B1x1, Noir, 3, 3, 7)),

            Make("robot", "Robot", Difficulty.Moyen, 10, 11, Celebration.Robot,
                P(B1x2, Gris, 0, 0, 0), P(B1x2, Gris, 3, 0, 0),
                P(B2x4, Bleu, 0, 0, 3, 1), P(B2x4, Bleu, 0, 0, 6, 1),
                P(B1x6, Gris, -1, 0, 9, 1), P(B1x6, Gris, -1, 1, 9, 1),
                P(B2x2, Jaune, 1, 0, 12),
                P(B1x1, Jaune, -1, 0, 12), P(B1x1, Jaune, 4, 1, 12),
                P(P2x2, Rouge, 1, 0, 15),
                P(B1x1, Noir, 1, 0, 16)),

            Make("tour", "Tour", Difficulty.Moyen, 10, 10, Celebration.Tower,
                P(B2x4, Gris, 0, 0, 0), P(B2x4, Gris, 2, 0, 0),
                P(B2x4, Blanc, 0, 0, 3, 1), P(B2x4, Blanc, 0, 2, 3, 1),
                P(B2x4, Gris, 0, 0, 6), P(B2x4, Gris, 2, 0, 6),
                P(B2x4, Blanc, 0, 0, 9, 1), P(B2x4, Blanc, 0, 2, 9, 1),
                P(B1x1, Rouge, 0, 0, 12), P(B1x1, Rouge, 3, 0, 12), P(B1x1, Rouge, 0, 3, 12), P(B1x1, Rouge, 3, 3, 12)),

            Make("chateau", "Château", Difficulty.Difficile, 9, 9, Celebration.Castle,
                P(B1x6, Gris, 0, 0, 0, 1), P(B1x6, Gris, 0, 5, 0, 1),
                P(B1x4, Gris, 0, 1, 0), P(B1x4, Gris, 5, 1, 0),
                P(B1x6, Gris, 0, 0, 3), P(B1x6, Gris, 5, 0, 3),
                P(B1x4, Gris, 1, 0, 3, 1), P(B1x4, Gris, 1, 5, 3, 1),
                P(B2x2, Blanc, 0, 0, 6), P(B2x2, Blanc, 4, 0, 6), P(B2x2, Blanc, 0, 4, 6), P(B2x2, Blanc, 4, 4, 6),
                P(B1x2, Gris, 2, 0, 6, 1), P(B1x2, Gris, 2, 5, 6, 1), P(B1x2, Gris, 0, 2, 6), P(B1x2, Gris, 5, 2, 6),
                P(P2x2, Bleu, 0, 0, 9), P(P2x2, Bleu, 4, 0, 9), P(P2x2, Bleu, 0, 4, 9), P(P2x2, Bleu, 4, 4, 9)),

            Rocket(),
            Pyramid(),
            BigCastle(),
        };

        // ---------------------------------------------------------------- grands modèles (générés)

        /// <summary>Fusée : 4 ailerons, un corps de 12 étages et une pointe (~38 briques).</summary>
        static BuildModel Rocket()
        {
            var m = new BuildModel { Id = "fusee", Name = "Fusée", Difficulty = Difficulty.Difficile, Celebration = Celebration.Rocket };
            const int o = 10; // corps 4x4 de (10,10) à (13,13)
            m.Steps.Add(P(B1x2, Rouge, o - 1, o + 1, 0)); m.Steps.Add(P(B1x2, Rouge, o + 4, o + 1, 0));
            m.Steps.Add(P(B1x2, Rouge, o + 1, o - 1, 0, 1)); m.Steps.Add(P(B1x2, Rouge, o + 1, o + 4, 0, 1));
            int level = 0;
            for (int floor = 0; floor < 12; floor++, level += 3)
            {
                int c = floor == 3 || floor == 8 ? Bleu : floor % 2 == 0 ? Blanc : Gris;
                if (floor % 2 == 0) { m.Steps.Add(P(B2x4, c, o, o, level)); m.Steps.Add(P(B2x4, c, o + 2, o, level)); }
                else { m.Steps.Add(P(B2x4, c, o, o, level, 1)); m.Steps.Add(P(B2x4, c, o, o + 2, level, 1)); }
            }
            // bandes noires au-dessus des ailerons
            foreach (var x in new[] { o, o + 2 })
            {
                m.Steps.Add(P(B1x2, Noir, x, o - 1, 3, 1)); m.Steps.Add(P(B1x2, Noir, x, o + 4, 3, 1));
            }
            m.Steps.Add(P(B1x2, Noir, o - 1, o, 3)); m.Steps.Add(P(B1x2, Noir, o - 1, o + 2, 3));
            m.Steps.Add(P(B1x2, Noir, o + 4, o, 3)); m.Steps.Add(P(B1x2, Noir, o + 4, o + 2, 3));
            // pointe
            m.Steps.Add(P(B2x2, Rouge, o + 1, o + 1, level));
            m.Steps.Add(P(B2x2, Rouge, o + 1, o + 1, level + 3));
            m.Steps.Add(P(B1x1, Rouge, o + 1, o + 1, level + 6));
            m.Steps.Add(P(B1x1, Jaune, o + 1, o + 1, level + 9));
            SortByLevel(m);
            return m;
        }

        /// <summary>Pyramide en briques 2x2, couleurs sable, avec un mât (100 briques).</summary>
        static BuildModel Pyramid()
        {
            var m = new BuildModel { Id = "pyramide", Name = "Pyramide", Difficulty = Difficulty.Extreme, Celebration = Celebration.Pyramid };
            int[] colors = { 8, 6, Jaune, 6, Jaune, Blanc };
            int level = 0;
            for (int size = 12, layer = 0; size >= 2; size -= 2, layer++, level += 3)
            {
                int start = 6 + (12 - size) / 2;
                for (int x = 0; x < size; x += 2)
                for (int z = 0; z < size; z += 2)
                    m.Steps.Add(P(B2x2, colors[layer], start + x, start + z, level));
            }
            // sommet : 4 tenons dorés puis un mât de 5 briques 1x1
            const int top = 11;
            m.Steps.Add(P(B1x1, Jaune, top, top, level)); m.Steps.Add(P(B1x1, Jaune, top + 1, top, level));
            m.Steps.Add(P(B1x1, Jaune, top, top + 1, level)); m.Steps.Add(P(B1x1, Jaune, top + 1, top + 1, level));
            for (int i = 1; i <= 5; i++) m.Steps.Add(P(B1x1, i == 5 ? Rouge : Blanc, top, top, level + i * 3));
            return m;
        }

        /// <summary>Grand château fort : remparts de 10 rangs, créneaux et 4 tours (200 briques).</summary>
        static BuildModel BigCastle()
        {
            var m = new BuildModel { Id = "grand_chateau", Name = "Grand château", Difficulty = Difficulty.Extreme, Celebration = Celebration.Castle };
            const int o = 4, n = 16; // remparts carrés de 16 tenons, de (4,4) à (19,19)
            int level = 0;
            for (int row = 0; row < 10; row++, level += 3)
            {
                int c = row == 9 ? Blanc : row % 3 == 2 ? 8 : Gris;
                bool even = row % 2 == 0;
                // deux côtés sur 16 tenons (4 briques 1x4), deux côtés sur 14 tenons (3 briques 1x4 + 1 brique 1x2) :
                // on alterne d'un rang à l'autre pour croiser les briques aux angles
                for (int i = 0; i < 4; i++)
                {
                    if (even) { m.Steps.Add(P(B1x4, c, o + i * 4, o, level, 1)); m.Steps.Add(P(B1x4, c, o + i * 4, o + n - 1, level, 1)); }
                    else { m.Steps.Add(P(B1x4, c, o, o + i * 4, level)); m.Steps.Add(P(B1x4, c, o + n - 1, o + i * 4, level)); }
                }
                for (int i = 0; i < 3; i++)
                {
                    if (even) { m.Steps.Add(P(B1x4, c, o, o + 1 + i * 4, level)); m.Steps.Add(P(B1x4, c, o + n - 1, o + 1 + i * 4, level)); }
                    else { m.Steps.Add(P(B1x4, c, o + 1 + i * 4, o, level, 1)); m.Steps.Add(P(B1x4, c, o + 1 + i * 4, o + n - 1, level, 1)); }
                }
                if (even) { m.Steps.Add(P(B1x2, c, o, o + 13, level)); m.Steps.Add(P(B1x2, c, o + n - 1, o + 13, level)); }
                else { m.Steps.Add(P(B1x2, c, o + 13, o, level, 1)); m.Steps.Add(P(B1x2, c, o + 13, o + n - 1, level, 1)); }
            }
            // créneaux : une brique 1x1 sur deux, sauf près des tours
            for (int i = 3; i <= 12; i += 2)
            {
                m.Steps.Add(P(B1x1, Gris, o + i, o, level)); m.Steps.Add(P(B1x1, Gris, o + i, o + n - 1, level));
                m.Steps.Add(P(B1x1, Gris, o, o + i, level)); m.Steps.Add(P(B1x1, Gris, o + n - 1, o + i, level));
            }
            // tours d'angle : 4 briques 2x2, un toit et un drapeau
            foreach (var t in new[] { new Vector2Int(o, o), new Vector2Int(o + n - 2, o), new Vector2Int(o, o + n - 2), new Vector2Int(o + n - 2, o + n - 2) })
            {
                for (int i = 0; i < 4; i++) m.Steps.Add(P(B2x2, i == 3 ? Blanc : Gris, t.x, t.y, level + i * 3));
                m.Steps.Add(P(P2x2, Bleu, t.x, t.y, level + 12));
                m.Steps.Add(P(B1x1, Rouge, t.x, t.y, level + 13));
            }
            SortByLevel(m);
            return m;
        }

        /// <summary>Range les étapes du bas vers le haut (chaque brique a un support quand on la pose).</summary>
        public static void SortByLevel(BuildModel m)
        {
            var indexed = new List<KeyValuePair<int, BrickPlacement>>();
            for (int i = 0; i < m.Steps.Count; i++) indexed.Add(new KeyValuePair<int, BrickPlacement>(i, m.Steps[i]));
            indexed.Sort((a, b) => a.Value.Level != b.Value.Level ? a.Value.Level.CompareTo(b.Value.Level) : a.Key.CompareTo(b.Key));
            m.Steps.Clear();
            foreach (var e in indexed) m.Steps.Add(e.Value);
        }

        public static string DifficultyName(Difficulty d) => d == Difficulty.Extreme ? "Extrême" : d.ToString();

        /// <summary>Tourne une brique d'un quart de tour autour de l'origine de la grille.</summary>
        public static BrickPlacement RotateQuarter(BrickPlacement p)
        {
            var f = Shapes[p.Shape].Footprint(p.Rot);
            return new BrickPlacement(p.Shape, p.Color, p.Z, -p.X - f.x, p.Level, (p.Rot + 1) % 4);
        }

        /// <summary>
        /// Nombre de briques d'une construction qui correspondent au modèle, où qu'elle soit sur la plaque
        /// et quel que soit son sens : on garde le meilleur décalage parmi les 4 orientations.
        /// </summary>
        public static int CountMatches(List<BrickPlacement> model, List<BrickPlacement> built)
        {
            int best = 0;
            var rotated = new List<BrickPlacement>(model);
            for (int r = 0; r < 4; r++)
            {
                var votes = new Dictionary<Vector2Int, int>();
                foreach (var m in rotated)
                foreach (var b in built)
                {
                    if (b.Shape != m.Shape || b.Color != m.Color || b.Level != m.Level) continue;
                    if (!Shapes[m.Shape].IsSquare && b.Rot % 2 != m.Rot % 2) continue;
                    var offset = new Vector2Int(b.X - m.X, b.Z - m.Z);
                    votes.TryGetValue(offset, out int v);
                    votes[offset] = ++v;
                    if (v > best) best = v;
                }
                for (int i = 0; i < rotated.Count; i++) rotated[i] = RotateQuarter(rotated[i]);
            }
            return best;
        }

        public static BuildModel FindModel(string id)
        {
            foreach (var m in Models) if (m.Id == id) return m;
            return null;
        }

        static BuildModel Make(string id, string name, Difficulty difficulty, int ox, int oz, Celebration celebration, params BrickPlacement[] steps)
        {
            var m = new BuildModel { Id = id, Name = name, Difficulty = difficulty, Celebration = celebration };
            foreach (var s in steps)
                m.Steps.Add(new BrickPlacement(s.Shape, s.Color, s.X + ox, s.Z + oz, s.Level, s.Rot));
            return m;
        }

        static BrickPlacement P(int shape, int color, int x, int z, int level, int rot = 0) =>
            new BrickPlacement(shape, color, x, z, level, rot);

        public static string Describe(int shape, int color) => Shapes[shape].Name + " " + Colors[color].Name;
    }
}
