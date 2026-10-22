using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ConstructVR
{
    /// <summary>
    /// Tableau flottant avec tous les types de briques et les couleurs.
    /// On pointe une brique avec la manette + gâchette : elle apparaît directement dans la main.
    /// </summary>
    public class BrickPalette : MonoBehaviour
    {
        class Item
        {
            public PaletteSlot Slot;
            public Vector3 BaseScale;
            public Quaternion BaseRotation;
            public bool Hovered;
        }

        readonly List<Item> m_Bricks = new List<Item>();
        readonly List<Item> m_Colors = new List<Item>();
        int m_HighlightShape = -1;
        int m_HighlightColor = -1;

        public int CurrentColor { get; private set; }

        void Awake()
        {
            foreach (var slot in GetComponentsInChildren<PaletteSlot>(true))
            {
                var item = new Item { Slot = slot, BaseScale = slot.Visual.localScale, BaseRotation = slot.Visual.localRotation };
                (slot.Kind == PaletteSlot.SlotKind.Brick ? m_Bricks : m_Colors).Add(item);
                var simple = slot.GetComponent<XRSimpleInteractable>();
                simple.hoverEntered.AddListener(_ =>
                {
                    if (!item.Hovered) SoundFX.Play(SoundFX.Hover, slot.transform.position, 0.25f);
                    item.Hovered = true;
                });
                simple.hoverExited.AddListener(_ => item.Hovered = simple.isHovered);
                simple.selectEntered.AddListener(args =>
                {
                    if (slot.Kind == PaletteSlot.SlotKind.Brick) StartCoroutine(GiveBrick(slot.Index, args.interactorObject));
                    else SelectColor(slot.Index);
                });
            }
            SelectColor(0, false);
        }

        IEnumerator GiveBrick(int shapeId, IXRSelectInteractor interactor)
        {
            // l'interacteur lâche d'abord la case du tableau, puis reçoit la brique
            var manager = GameManager.Instance.InteractionManager;
            if (interactor.interactablesSelected.Count > 0) manager.SelectExit(interactor, interactor.interactablesSelected[0]);
            yield return null;

            var hand = interactor.transform;
            var pos = hand.position + hand.forward * 0.06f;
            var flat = Vector3.ProjectOnPlane(hand.forward, Vector3.up);
            var rot = flat.sqrMagnitude > 0.001f ? Quaternion.LookRotation(flat, Vector3.up) : Quaternion.identity;
            var brick = LegoBrick.Create(shapeId, CurrentColor, pos, rot);
            SoundFX.Play(SoundFX.Pop, pos, 0.6f);
            Effects.Sparkle(pos, LegoCatalog.Colors[CurrentColor].Color);
            yield return null;

            // la brique reste dans la main même après avoir lâché la gâchette :
            // on la pose (ou la lâche) au prochain appui
            if (brick != null)
            {
                if (interactor is XRBaseInputInteractor input)
                {
                    var previousTrigger = input.selectActionTrigger;
                    input.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.Sticky;
                    UnityEngine.Events.UnityAction<SelectExitEventArgs> restore = null;
                    restore = args =>
                    {
                        if (args.interactorObject != interactor) return;
                        input.selectActionTrigger = previousTrigger;
                        brick.Grab.selectExited.RemoveListener(restore);
                    };
                    brick.Grab.selectExited.AddListener(restore);
                    manager.SelectEnter(interactor, brick.Grab);
                }
                else
                    brick.Drop();
            }
            GameManager.Instance.OnBrickTakenFromPalette(shapeId, CurrentColor);
        }

        public void SelectColor(int colorId, bool notify = true)
        {
            CurrentColor = colorId;
            foreach (var c in m_Colors) c.Slot.SelectedFrame.SetActive(c.Slot.Index == colorId);
            foreach (var b in m_Bricks) b.Slot.VisualRenderer.sharedMaterial = BrickFactory.GetMaterial(colorId);
            if (notify)
            {
                SoundFX.Play(SoundFX.Blip, transform.position, 0.4f);
                GameManager.Instance.OnColorChosen(colorId);
            }
        }

        /// <summary>Fait clignoter une brique et une couleur (mode Modèle guidé). -1 pour arrêter.</summary>
        public void Highlight(int shapeId, int colorId)
        {
            m_HighlightShape = shapeId;
            m_HighlightColor = colorId;
        }

        void Update()
        {
            float blink = 0.5f + 0.5f * Mathf.Sin(Time.time * 7f);
            foreach (var b in m_Bricks)
            {
                float target = b.Hovered ? 1.3f : 1f;
                if (b.Slot.Index == m_HighlightShape) target += 0.3f * blink;
                b.Slot.Visual.localScale = Vector3.Lerp(b.Slot.Visual.localScale, b.BaseScale * target, Time.deltaTime * 14f);
                var rot = b.Hovered ? Quaternion.AngleAxis(Time.time * 90f % 360f, Vector3.up) * b.BaseRotation : b.BaseRotation;
                b.Slot.Visual.localRotation = Quaternion.Slerp(b.Slot.Visual.localRotation, rot, Time.deltaTime * 10f);
                b.Slot.SelectedFrame.SetActive(b.Slot.Index == m_HighlightShape && blink > 0.5f);
            }
            foreach (var c in m_Colors)
            {
                float target = c.Hovered ? 1.3f : 1f;
                if (c.Slot.Index == m_HighlightColor && c.Slot.Index != CurrentColor) target += 0.35f * blink;
                c.Slot.Visual.localScale = Vector3.Lerp(c.Slot.Visual.localScale, c.BaseScale * target, Time.deltaTime * 14f);
            }
        }
    }
}
