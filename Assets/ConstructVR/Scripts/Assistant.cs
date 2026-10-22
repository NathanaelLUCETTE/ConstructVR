using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ConstructVR
{
    /// <summary>
    /// « Brico », le petit personnage Lego volant qui aide le joueur : il parle dans une bulle,
    /// fait la fête quand on réussit et peut voler jusqu'à un emplacement pour le montrer du doigt.
    /// </summary>
    public class Assistant : MonoBehaviour
    {
        [Header("Parties du personnage")]
        public Transform Body;
        public Transform ArmLeft;
        public Transform ArmRight;
        public Transform Eyes;
        public Transform Mouth;
        public Transform Propeller;

        [Header("Bulle et pointeur")]
        public GameObject Bubble;
        public TextMeshProUGUI Text;
        public LineRenderer Line;
        public Transform Marker;

        [Header("Réglages")]
        public Transform Home;
        public float CharactersPerSecond = 40f;

        Vector3? m_ShowTarget;
        readonly Queue<string> m_Queue = new Queue<string>();
        string m_Current = "";
        float m_Typed;
        float m_HideAt;
        float m_NextBlink;
        float m_BlinkUntil;
        float m_CelebrateUntil;
        float m_WaveUntil;
        Transform m_Camera;
        Quaternion m_ArmLeftRest, m_ArmRightRest;
        Vector3 m_EyesScale, m_MouthScale;

        public bool IsTalking => Bubble.activeSelf;

        void Awake()
        {
            m_ArmLeftRest = ArmLeft.localRotation;
            m_ArmRightRest = ArmRight.localRotation;
            m_EyesScale = Eyes.localScale;
            m_MouthScale = Mouth.localScale;
            Bubble.SetActive(false);
            Line.enabled = false;
            Marker.gameObject.SetActive(false);
        }

        /// <summary>Affiche une phrase. Si queue = true, elle attend la fin de la phrase en cours.</summary>
        public void Say(string text, bool queue = false)
        {
            if (!queue || !IsTalking)
            {
                m_Queue.Clear();
                StartLine(text);
            }
            else m_Queue.Enqueue(text);
        }

        public void ClearSpeech()
        {
            m_Queue.Clear();
            Bubble.SetActive(false);
        }

        void StartLine(string text)
        {
            m_Current = text;
            m_Typed = 0f;
            Text.text = "";
            Bubble.SetActive(true);
            m_HideAt = Time.time + 2.2f + text.Length / CharactersPerSecond * 1.6f;
            SoundFX.Play(SoundFX.Talk, transform.position, 0.3f);
        }

        public void ShowSpot(Vector3 worldPos) => m_ShowTarget = worldPos;
        public void StopShowing() => m_ShowTarget = null;
        public void Celebrate() => m_CelebrateUntil = Time.time + 2f;
        public void Wave() => m_WaveUntil = Time.time + 2.5f;

        void Update()
        {
            if (m_Camera == null && Camera.main != null) m_Camera = Camera.main.transform;
            float t = Time.time;

            // déplacement : à sa place, ou au-dessus de l'emplacement à montrer
            var target = m_ShowTarget.HasValue ? m_ShowTarget.Value + new Vector3(0.1f, 0.14f, -0.08f) : Home.position;
            transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * 2.5f);

            bool celebrating = t < m_CelebrateUntil;
            float hop = celebrating ? Mathf.Abs(Mathf.Sin(t * 9f)) * 0.05f : 0f;
            Body.localPosition = new Vector3(0f, Mathf.Sin(t * 2.2f) * 0.01f + hop, 0f);
            Body.localRotation = celebrating ? Quaternion.Euler(0f, (m_CelebrateUntil - t) * 360f, 0f) : Quaternion.Slerp(Body.localRotation, Quaternion.identity, Time.deltaTime * 6f);
            Propeller.Rotate(Vector3.up, 900f * Time.deltaTime, Space.Self);

            if (m_Camera != null)
            {
                var look = transform.position - m_Camera.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), Time.deltaTime * 5f);
            }

            // clignement des yeux
            if (t > m_NextBlink)
            {
                m_BlinkUntil = t + 0.12f;
                m_NextBlink = t + Random.Range(2.5f, 5f);
            }
            Eyes.localScale = new Vector3(m_EyesScale.x, t < m_BlinkUntil ? m_EyesScale.y * 0.1f : m_EyesScale.y, m_EyesScale.z);

            // bouche qui bouge quand il parle
            bool typing = Bubble.activeSelf && m_Typed < m_Current.Length;
            float mouthOpen = typing ? 0.6f + 0.6f * Mathf.Abs(Mathf.Sin(t * 18f)) : 1f;
            Mouth.localScale = new Vector3(m_MouthScale.x, m_MouthScale.y * mouthOpen, m_MouthScale.z);

            UpdateArms(t, celebrating);
            UpdatePointer(t);
            UpdateBubble();
        }

        void UpdateArms(float t, bool celebrating)
        {
            var left = m_ArmLeftRest;
            var right = m_ArmRightRest;
            if (celebrating)
            {
                float w = Mathf.Sin(t * 14f) * 15f;
                left = Quaternion.Euler(0f, 0f, -150f + w);
                right = Quaternion.Euler(0f, 0f, 150f - w);
            }
            else if (m_ShowTarget.HasValue)
            {
                var dir = ArmRight.parent.InverseTransformDirection((m_ShowTarget.Value - ArmRight.position).normalized);
                right = Quaternion.FromToRotation(Vector3.down, dir);
            }
            else if (t < m_WaveUntil)
            {
                right = Quaternion.Euler(0f, 0f, 140f + Mathf.Sin(t * 12f) * 25f);
            }
            else
            {
                left = m_ArmLeftRest * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 2.2f) * 6f);
                right = m_ArmRightRest * Quaternion.Euler(0f, 0f, -Mathf.Sin(t * 2.2f) * 6f);
            }
            ArmLeft.localRotation = Quaternion.Slerp(ArmLeft.localRotation, left, Time.deltaTime * 10f);
            ArmRight.localRotation = Quaternion.Slerp(ArmRight.localRotation, right, Time.deltaTime * 10f);
        }

        void UpdatePointer(float t)
        {
            if (m_ShowTarget.HasValue)
            {
                var hand = ArmRight.position + ArmRight.TransformDirection(Vector3.down) * 0.035f;
                Line.enabled = true;
                Line.SetPosition(0, hand);
                Line.SetPosition(1, m_ShowTarget.Value);
                Marker.gameObject.SetActive(true);
                Marker.position = m_ShowTarget.Value;
                Marker.localScale = Vector3.one * (0.02f + 0.008f * Mathf.Sin(t * 9f));
            }
            else
            {
                Line.enabled = false;
                Marker.gameObject.SetActive(false);
            }
        }

        void UpdateBubble()
        {
            if (!Bubble.activeSelf) return;
            if (m_Typed < m_Current.Length)
            {
                m_Typed += Time.deltaTime * CharactersPerSecond;
                Text.text = m_Current.Substring(0, Mathf.Min(m_Current.Length, (int)m_Typed));
            }
            if (Time.time > m_HideAt)
            {
                if (m_Queue.Count > 0) StartLine(m_Queue.Dequeue());
                else Bubble.SetActive(false);
            }
        }
    }
}
