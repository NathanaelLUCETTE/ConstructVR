using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ConstructVR
{
    /// <summary>Une brique qu'on peut prendre, poser (emboîtement sur les tenons) ou jeter.</summary>
    public class LegoBrick : MonoBehaviour
    {
        const float SnapDuration = 0.09f;

        public int ShapeId { get; private set; }
        public int ColorId { get; private set; }
        public bool IsPlaced { get; private set; }
        public BrickPlacement Placement { get; private set; }

        XRGrabInteractable m_Grab;
        Rigidbody m_Body;
        GameObject m_Preview;
        MeshRenderer m_PreviewRenderer;
        BuildArea m_Area;
        float m_DropTime;

        Vector3 m_SnapFromPos, m_SnapToPos;
        Quaternion m_SnapFromRot, m_SnapToRot;
        float m_SnapT = 1f;

        public XRGrabInteractable Grab => m_Grab;

        public static LegoBrick Create(int shapeId, int colorId, Vector3 position, Quaternion rotation)
        {
            var shape = LegoCatalog.Shapes[shapeId];
            var go = new GameObject(LegoCatalog.Describe(shapeId, colorId));
            go.transform.SetPositionAndRotation(position, rotation);
            go.AddComponent<MeshFilter>().sharedMesh = BrickFactory.GetMesh(shapeId);
            go.AddComponent<MeshRenderer>().sharedMaterial = BrickFactory.GetMaterial(colorId);

            var col = go.AddComponent<BoxCollider>();
            float g = LegoDims.Gap * 2f;
            col.size = new Vector3(shape.SizeX * LegoDims.Pitch - g, shape.Height - g, shape.SizeZ * LegoDims.Pitch - g);
            col.center = new Vector3(0f, shape.Height * 0.5f, 0f);

            var body = go.AddComponent<Rigidbody>();
            body.mass = 0.05f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            var grab = go.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.throwOnDetach = false;
            grab.useDynamicAttach = true;
            grab.farAttachMode = InteractableFarAttachMode.Near;
            grab.smoothPosition = false;
            grab.smoothRotation = false;

            var brick = go.AddComponent<LegoBrick>();
            brick.ShapeId = shapeId;
            brick.ColorId = colorId;
            brick.m_Grab = grab;
            brick.m_Body = body;
            brick.m_DropTime = Time.time;
            return brick;
        }

        void OnEnable()
        {
            if (m_Grab == null) m_Grab = GetComponent<XRGrabInteractable>();
            m_Grab.selectEntered.AddListener(OnGrabbed);
            m_Grab.selectExited.AddListener(OnReleased);
        }

        void OnDisable()
        {
            m_Grab.selectEntered.RemoveListener(OnGrabbed);
            m_Grab.selectExited.RemoveListener(OnReleased);
            HidePreview();
        }

        void OnGrabbed(SelectEnterEventArgs args)
        {
            m_SnapT = 1f;
            if (IsPlaced && m_Area != null) m_Area.Remove(this);
            IsPlaced = false;
            GameManager.Instance?.OnBrickGrabbed(this);
        }

        void OnReleased(SelectExitEventArgs args)
        {
            HidePreview();
            if (args.isCanceled || GameManager.Instance == null)
            {
                Drop();
                return;
            }
            GameManager.Instance.OnBrickReleased(this, args.interactorObject as XRBaseInputInteractor);
        }

        void Update()
        {
            if (m_Grab.isSelected && GameManager.Instance != null)
            {
                if (GameManager.Instance.TryGetPreview(this, out var p, out var color))
                    ShowPreview(p, color);
                else
                    HidePreview();
            }

            // petite animation d'emboîtement
            if (m_SnapT < 1f)
            {
                m_SnapT = Mathf.Min(1f, m_SnapT + Time.deltaTime / SnapDuration);
                float k = 1f - (1f - m_SnapT) * (1f - m_SnapT);
                transform.SetPositionAndRotation(Vector3.Lerp(m_SnapFromPos, m_SnapToPos, k), Quaternion.Slerp(m_SnapFromRot, m_SnapToRot, k));
            }

            // brique tombée par terre : on la supprime
            if (!IsPlaced && !m_Grab.isSelected && Time.time - m_DropTime > 1f && transform.position.y < 0.05f)
                Destroy(gameObject);
        }

        /// <summary>Appelé par la plaque quand la brique est emboîtée.</summary>
        public void SetPlaced(BuildArea area, BrickPlacement p, Vector3 position, Quaternion rotation, bool animate)
        {
            m_Area = area;
            Placement = p;
            IsPlaced = true;
            m_Body.isKinematic = true;
            m_Body.useGravity = false;
            if (animate)
            {
                m_SnapFromPos = transform.position;
                m_SnapFromRot = transform.rotation;
                m_SnapToPos = position;
                m_SnapToRot = rotation;
                m_SnapT = 0f;
            }
            else
            {
                m_SnapT = 1f;
                transform.SetPositionAndRotation(position, rotation);
            }
        }

        /// <summary>La brique n'est pas emboîtée : elle tombe.</summary>
        public void Drop()
        {
            IsPlaced = false;
            m_SnapT = 1f;
            m_Body.isKinematic = false;
            m_Body.useGravity = true;
            m_DropTime = Time.time;
        }

        public void ShowPreview(BrickPlacement p, Color color)
        {
            var area = GameManager.Instance.Area;
            if (m_Preview == null)
            {
                m_Preview = BrickFactory.CreateVisual(ShapeId, BrickFactory.GetGhostMaterial(color), null, "Aperçu");
                m_PreviewRenderer = m_Preview.GetComponent<MeshRenderer>();
                m_PreviewRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            m_PreviewRenderer.sharedMaterial = BrickFactory.GetGhostMaterial(color);
            m_Preview.SetActive(true);
            m_Preview.transform.SetPositionAndRotation(area.WorldPosition(p), area.WorldRotation(p));
        }

        public void HidePreview()
        {
            if (m_Preview != null) m_Preview.SetActive(false);
        }

        void OnDestroy()
        {
            if (m_Preview != null) Destroy(m_Preview);
            if (IsPlaced && m_Area != null) m_Area.Remove(this);
        }
    }
}
