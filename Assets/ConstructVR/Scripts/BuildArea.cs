using System.Collections.Generic;
using UnityEngine;

namespace ConstructVR
{
    /// <summary>
    /// Plaque de base : grille de tenons, calcul de l'emboîtement et mémoire des briques posées.
    /// Le repère local a son origine au centre du dessus de la plaque.
    /// </summary>
    public class BuildArea : MonoBehaviour
    {
        const int MaxLevel = 240;

        // Cellules occupées : (x, niveau en plaques, z) -> brique
        readonly Dictionary<Vector3Int, LegoBrick> m_Cells = new Dictionary<Vector3Int, LegoBrick>();
        readonly List<LegoBrick> m_Placed = new List<LegoBrick>();

        public IReadOnlyList<LegoBrick> PlacedBricks => m_Placed;

        /// <summary>
        /// Cherche où une brique tenue à cette position s'emboîterait.
        /// Renvoie false si elle est trop loin de la plaque ou d'une autre brique.
        /// </summary>
        public bool ComputePlacement(LegoBrick brick, out BrickPlacement placement)
        {
            placement = default;
            var shape = LegoCatalog.Shapes[brick.ShapeId];
            var t = brick.transform;
            var local = transform.InverseTransformPoint(t.position);

            // rotation arrondie au quart de tour
            var fwd = transform.InverseTransformDirection(t.forward);
            if (Mathf.Abs(Vector3.Dot(transform.InverseTransformDirection(t.up), Vector3.up)) < 0.5f)
                return false; // brique tenue sur le côté : on n'emboîte pas
            float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            int rot = ((Mathf.RoundToInt(yaw / 90f) % 4) + 4) % 4;
            var f = shape.Footprint(rot);

            float p = LegoDims.Pitch;
            float half = LegoCatalog.BaseplateSize * p * 0.5f;
            int x = Mathf.RoundToInt((local.x + half) / p - f.x * 0.5f);
            int z = Mathf.RoundToInt((local.z + half) / p - f.y * 0.5f);
            if (x < 0 || z < 0 || x + f.x > LegoCatalog.BaseplateSize || z + f.y > LegoCatalog.BaseplateSize)
                return false;

            // niveau : sur la plus haute brique sous la position de la main
            float handLevel = local.y / LegoDims.PlateHeight;
            if (handLevel < -1.5f) return false;
            int searchFrom = Mathf.Clamp(Mathf.FloorToInt(handLevel + 1.5f), 0, MaxLevel);
            int level = 0;
            for (int cx = x; cx < x + f.x; cx++)
            for (int cz = z; cz < z + f.y; cz++)
            {
                for (int l = searchFrom - 1; l >= 0; l--)
                {
                    if (m_Cells.ContainsKey(new Vector3Int(cx, l, cz)))
                    {
                        level = Mathf.Max(level, l + 1);
                        break;
                    }
                }
            }

            // trop haut au-dessus du support : pas d'emboîtement
            if (handLevel - level > 4.5f) return false;

            // la place doit être libre
            for (int cx = x; cx < x + f.x; cx++)
            for (int cz = z; cz < z + f.y; cz++)
            for (int l = level; l < level + shape.HeightPlates; l++)
                if (m_Cells.TryGetValue(new Vector3Int(cx, l, cz), out var other) && other != brick)
                    return false;

            placement = new BrickPlacement(brick.ShapeId, brick.ColorId, x, z, level, rot);
            return true;
        }

        public Vector3 WorldPosition(BrickPlacement p) => transform.TransformPoint(BrickFactory.LocalPosition(p));
        public Quaternion WorldRotation(BrickPlacement p) => transform.rotation * BrickFactory.LocalRotation(p.Rot);

        public void Place(LegoBrick brick, BrickPlacement p, bool animate = true)
        {
            Remove(brick);
            var f = LegoCatalog.Shapes[p.Shape].Footprint(p.Rot);
            for (int cx = p.X; cx < p.X + f.x; cx++)
            for (int cz = p.Z; cz < p.Z + f.y; cz++)
            for (int l = p.Level; l < p.Level + LegoCatalog.Shapes[p.Shape].HeightPlates; l++)
                m_Cells[new Vector3Int(cx, l, cz)] = brick;
            m_Placed.Add(brick);
            brick.SetPlaced(this, p, WorldPosition(p), WorldRotation(p), animate);
        }

        /// <summary>Dernière brique posée (pour le bouton Annuler).</summary>
        public LegoBrick LastPlaced => m_Placed.Count > 0 ? m_Placed[m_Placed.Count - 1] : null;

        public List<BrickPlacement> GetPlacements()
        {
            var list = new List<BrickPlacement>();
            foreach (var b in m_Placed) if (b != null) list.Add(b.Placement);
            return list;
        }

        public void Remove(LegoBrick brick)
        {
            if (!m_Placed.Remove(brick)) return;
            var keys = new List<Vector3Int>();
            foreach (var kv in m_Cells)
                if (kv.Value == brick) keys.Add(kv.Key);
            foreach (var k in keys) m_Cells.Remove(k);
        }

        public void Clear()
        {
            foreach (var b in m_Placed.ToArray())
                if (b != null) Destroy(b.gameObject);
            m_Placed.Clear();
            m_Cells.Clear();
            // briques tombées sur la table
            foreach (var b in FindObjectsByType<LegoBrick>(FindObjectsSortMode.None))
                if (!b.Grab.isSelected) Destroy(b.gameObject);
        }
    }
}
