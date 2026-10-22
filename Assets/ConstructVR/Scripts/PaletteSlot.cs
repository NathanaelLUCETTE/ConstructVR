using UnityEngine;

namespace ConstructVR
{
    /// <summary>Une case du tableau : une forme de brique ou une couleur.</summary>
    public class PaletteSlot : MonoBehaviour
    {
        public enum SlotKind { Brick, Color }

        public SlotKind Kind;
        public int Index;
        public Transform Visual;
        public MeshRenderer VisualRenderer;
        public GameObject SelectedFrame;
    }
}
