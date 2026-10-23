using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Jump;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace ConstructVR
{
    /// <summary>
    /// Mode Échelle : le joueur devient minuscule (comme une figurine) et se promène dans sa construction.
    /// On réduit tout le rig XR, ainsi que la vitesse de marche, le saut et la distance d'affichage minimale.
    /// </summary>
    public class ScaleVisit : MonoBehaviour
    {
        public const float Scale = 0.06f; // 1,60 m deviennent 10 cm : à peu près la taille d'une figurine

        XROrigin m_Origin;
        CharacterController m_Controller;
        ContinuousMoveProvider m_Move;
        JumpProvider m_Jump;
        TeleportationProvider m_Teleport;
        Camera m_Camera;

        Vector3 m_SavedPosition;
        Quaternion m_SavedRotation;
        Vector3 m_SavedScale;
        float m_SavedSpeed, m_SavedJump, m_SavedNear, m_SavedMinMove, m_SavedSkin, m_SavedStep;

        public bool Active { get; private set; }

        bool Find()
        {
            if (m_Origin == null) m_Origin = FindAnyObjectByType<XROrigin>();
            if (m_Origin == null) return false;
            m_Controller = m_Origin.GetComponent<CharacterController>();
            m_Move = m_Origin.GetComponentInChildren<ContinuousMoveProvider>(true);
            m_Jump = m_Origin.GetComponentInChildren<JumpProvider>(true);
            m_Teleport = m_Origin.GetComponentInChildren<TeleportationProvider>(true);
            m_Camera = m_Origin.Camera;
            return true;
        }

        /// <summary>Rapetisse le joueur et le pose à <paramref name="feet"/>, tourné vers <paramref name="forward"/>.</summary>
        public bool Enter(Vector3 feet, Vector3 forward)
        {
            if (Active || !Find()) return false;
            var t = m_Origin.transform;
            m_SavedPosition = t.position;
            m_SavedRotation = t.rotation;
            m_SavedScale = t.localScale;
            if (m_Move != null) { m_SavedSpeed = m_Move.moveSpeed; m_Move.moveSpeed *= Scale; }
            if (m_Jump != null) { m_SavedJump = m_Jump.jumpHeight; m_Jump.jumpHeight *= Scale; }
            if (m_Teleport != null) m_Teleport.enabled = false; // les zones de téléportation sont à taille normale
            if (m_Camera != null) { m_SavedNear = m_Camera.nearClipPlane; m_Camera.nearClipPlane = 0.002f; }
            if (m_Controller != null)
            {
                // ces réglages ne suivent pas l'échelle : trop grands, ils rendent le contrôleur invalide (plus de déplacement)
                m_SavedMinMove = m_Controller.minMoveDistance;
                m_SavedSkin = m_Controller.skinWidth;
                m_SavedStep = m_Controller.stepOffset;
                m_Controller.minMoveDistance = 0f;
                m_Controller.skinWidth = Mathf.Max(0.0005f, m_SavedSkin * Scale);
                m_Controller.stepOffset = LegoDims.PlateHeight * 1.5f; // on monte sur une plaque, pas sur une brique
            }

            Teleport(feet, forward, m_SavedScale * Scale);
            Active = true;
            return true;
        }

        public void Exit()
        {
            if (!Active) return;
            Active = false;
            if (m_Move != null) m_Move.moveSpeed = m_SavedSpeed;
            if (m_Jump != null) m_Jump.jumpHeight = m_SavedJump;
            if (m_Teleport != null) m_Teleport.enabled = true;
            if (m_Camera != null) m_Camera.nearClipPlane = m_SavedNear;
            if (m_Controller != null)
            {
                m_Controller.minMoveDistance = m_SavedMinMove;
                m_Controller.skinWidth = m_SavedSkin;
                m_Controller.stepOffset = m_SavedStep;
            }
            var t = m_Origin.transform;
            if (m_Controller != null) m_Controller.enabled = false;
            t.localScale = m_SavedScale;
            t.SetPositionAndRotation(m_SavedPosition, m_SavedRotation);
            if (m_Controller != null) m_Controller.enabled = true;
        }

        void Teleport(Vector3 feet, Vector3 forward, Vector3 scale)
        {
            var t = m_Origin.transform;
            if (m_Controller != null) m_Controller.enabled = false;
            t.localScale = scale;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            t.rotation = Quaternion.LookRotation(forward, Vector3.up);
            // la tête du joueur n'est pas forcément au centre de son espace de jeu : on recentre
            if (m_Camera != null)
            {
                var head = m_Camera.transform.position - t.position;
                head.y = 0f;
                t.position = feet - head;
            }
            else t.position = feet;
            Physics.SyncTransforms();
            if (m_Controller != null) m_Controller.enabled = true;
        }
    }
}
