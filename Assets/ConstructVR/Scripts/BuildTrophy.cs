using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ConstructVR
{
    /// <summary>
    /// Construction terminée, d'un seul bloc : elle joue une petite animation (la voiture roule,
    /// le château lance un feu d'artifice…) puis on peut l'attraper entière avec la manette.
    /// </summary>
    public class BuildTrophy : MonoBehaviour
    {
        XRGrabInteractable m_Grab;
        Rigidbody m_Body;
        Transform m_Visual;      // enfant animé (le parent reste là où il a été créé)
        Vector3 m_HomePosition;
        Quaternion m_HomeRotation;
        Vector3 m_Size;          // taille de la construction en mètres

        public bool IsAnimating { get; private set; }

        /// <summary>Remplace les briques posées par un objet unique, au même endroit.</summary>
        public static BuildTrophy Create(BuildArea area, List<BrickPlacement> placements)
        {
            if (placements.Count == 0) return null;

            // boîte englobante en coordonnées de la plaque
            var bounds = new Bounds();
            for (int i = 0; i < placements.Count; i++)
            {
                var p = placements[i];
                var shape = LegoCatalog.Shapes[p.Shape];
                var f = shape.Footprint(p.Rot);
                var b = new Bounds(BrickFactory.LocalPosition(p) + Vector3.up * shape.Height * 0.5f,
                    new Vector3(f.x * LegoDims.Pitch, shape.Height, f.y * LegoDims.Pitch));
                if (i == 0) bounds = b; else bounds.Encapsulate(b);
            }

            var go = new GameObject("Ma construction");
            var pivot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            go.transform.SetPositionAndRotation(area.transform.TransformPoint(pivot), area.transform.rotation);
            var visual = new GameObject("Briques").transform;
            visual.SetParent(go.transform, false);

            foreach (var p in placements)
            {
                var shape = LegoCatalog.Shapes[p.Shape];
                var v = BrickFactory.CreateVisual(p.Shape, BrickFactory.GetMaterial(p.Color), visual);
                v.transform.localPosition = BrickFactory.LocalPosition(p) - pivot;
                v.transform.localRotation = BrickFactory.LocalRotation(p.Rot);
                // une boîte par brique : on peut marcher entre les murs en mode Échelle
                var col = v.AddComponent<BoxCollider>();
                float g = LegoDims.Gap * 2f;
                col.size = new Vector3(shape.SizeX * LegoDims.Pitch - g, shape.Height - g, shape.SizeZ * LegoDims.Pitch - g);
                col.center = new Vector3(0f, shape.Height * 0.5f, 0f);
            }

            var trophy = go.AddComponent<BuildTrophy>();
            trophy.m_Visual = visual;
            trophy.m_Size = bounds.size;
            trophy.m_HomePosition = go.transform.position;
            trophy.m_HomeRotation = go.transform.rotation;

            var body = go.AddComponent<Rigidbody>();
            body.mass = Mathf.Clamp(placements.Count * 0.02f, 0.2f, 3f);
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            trophy.m_Body = body;

            // ajouté après les briques : il récupère toutes leurs boîtes de collision
            var grab = go.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.throwOnDetach = false;
            grab.useDynamicAttach = true;
            grab.farAttachMode = InteractableFarAttachMode.Near;
            grab.smoothPosition = true;
            grab.smoothRotation = true;
            grab.enabled = false;
            grab.selectExited.AddListener(_ => { body.isKinematic = false; body.useGravity = true; });
            trophy.m_Grab = grab;
            return trophy;
        }

        public void SetGrabbable(bool grabbable)
        {
            m_Grab.enabled = grabbable;
            if (!grabbable) { m_Body.isKinematic = true; m_Body.useGravity = false; }
        }

        /// <summary>Remet la construction à sa place sur la plaque, immobile (mode Échelle).</summary>
        public void ReturnHome()
        {
            SetGrabbable(false);
            transform.SetPositionAndRotation(m_HomePosition, m_HomeRotation);
            m_Visual.localPosition = Vector3.zero;
            m_Visual.localRotation = Quaternion.identity;
            m_Visual.localScale = Vector3.one;
        }

        Vector3 Top => m_Visual.TransformPoint(Vector3.up * m_Size.y);

        // ---------------------------------------------------------------- animations de fin

        public IEnumerator Celebrate(Celebration kind)
        {
            IsAnimating = true;
            SetGrabbable(false);
            switch (kind)
            {
                case Celebration.Drive: yield return Drive(); break;
                case Celebration.Castle: yield return Castle(); break;
                case Celebration.Robot: yield return Robot(); break;
                case Celebration.Flower: yield return Flower(); break;
                case Celebration.House: yield return House(); break;
                case Celebration.Tower: yield return Tower(); break;
                case Celebration.Pyramid: yield return Pyramid(); break;
                case Celebration.Rocket: yield return Rocket(); break;
                default: yield return Spin(); break;
            }
            m_Visual.localPosition = Vector3.zero;
            m_Visual.localRotation = Quaternion.identity;
            m_Visual.localScale = Vector3.one;
            IsAnimating = false;
            SetGrabbable(true);
        }

        /// <summary>La voiture démarre, avance, klaxonne et revient en marche arrière.</summary>
        IEnumerator Drive()
        {
            SoundFX.Play(SoundFX.Engine, transform.position, 0.9f);
            yield return Wait(0.3f);
            float distance = 0.22f;
            // l'avant de la voiture est le côté +Z de la plaque (le long de la brique 2x4)
            yield return Move(0f, distance, 1.1f, true);
            SoundFX.Play(SoundFX.Horn, Top, 0.8f);
            yield return Wait(0.6f);
            SoundFX.Play(SoundFX.Engine, transform.position, 0.7f);
            yield return Move(distance, 0f, 1.1f, true);
            SoundFX.Play(SoundFX.Horn, Top, 0.6f);
        }

        IEnumerator Move(float from, float to, float duration, bool exhaust)
        {
            float t = 0f, nextPuff = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / duration);
                float z = Mathf.Lerp(from, to, k);
                float bump = Mathf.Abs(Mathf.Sin(t * 28f)) * 0.0025f; // la carrosserie tressaute
                m_Visual.localPosition = new Vector3(0f, bump, z);
                m_Visual.localRotation = Quaternion.Euler((to - from) * -6f * Mathf.Sin(Mathf.PI * t / duration), 0f, 0f);
                if (exhaust && Time.time > nextPuff)
                {
                    nextPuff = Time.time + 0.08f;
                    Effects.Puff(m_Visual.TransformPoint(new Vector3(0f, 0.01f, -m_Size.z * 0.5f - 0.01f)), new Color(0.6f, 0.6f, 0.6f, 0.6f), 3, 0.06f);
                }
                yield return null;
            }
        }

        /// <summary>Le château gronde, sonne la fanfare et tire un feu d'artifice depuis ses tours.</summary>
        IEnumerator Castle()
        {
            SoundFX.Play(SoundFX.Rumble, transform.position, 0.7f);
            yield return Shake(1.2f, 0.004f);
            SoundFX.Play(SoundFX.Fanfare, Top, 1f);
            var corners = new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(1f, 1f), new Vector2(-1f, 1f) };
            foreach (var c in corners)
            {
                yield return Wait(0.35f);
                var pos = m_Visual.TransformPoint(new Vector3(c.x * m_Size.x * 0.45f, m_Size.y, c.y * m_Size.z * 0.45f));
                Effects.Confetti(pos);
                SoundFX.Play(SoundFX.Boom, pos, 0.6f);
            }
            yield return Hop(2, 0.02f, 0.3f);
        }

        /// <summary>Le robot danse en bipant.</summary>
        IEnumerator Robot()
        {
            SoundFX.Play(SoundFX.Beeps, Top, 0.9f);
            float t = 0f;
            while (t < 2.2f)
            {
                t += Time.deltaTime;
                m_Visual.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 9f) * 30f, Mathf.Sin(t * 18f) * 6f);
                m_Visual.localPosition = Vector3.up * Mathf.Abs(Mathf.Sin(t * 9f)) * 0.03f;
                yield return null;
            }
            SoundFX.Play(SoundFX.Beeps, Top, 0.6f);
        }

        /// <summary>La fleur grandit en tournant, avec des étincelles.</summary>
        IEnumerator Flower()
        {
            SoundFX.Play(SoundFX.Chime, Top, 0.9f);
            float t = 0f;
            while (t < 2f)
            {
                t += Time.deltaTime;
                float s = 1f + 0.35f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 2f));
                m_Visual.localScale = Vector3.one * s;
                m_Visual.localRotation = Quaternion.Euler(0f, t * 180f, 0f);
                if (Random.value < 0.2f) Effects.Sparkle(Top, LegoCatalog.Colors[9].Color);
                yield return null;
            }
        }

        /// <summary>On sonne à la porte et la cheminée fume.</summary>
        IEnumerator House()
        {
            SoundFX.Play(SoundFX.DingDong, transform.position, 0.9f);
            yield return Hop(2, 0.02f, 0.35f);
            float t = 0f;
            while (t < 1.6f)
            {
                t += 0.12f;
                Effects.Puff(Top + Vector3.up * 0.01f, new Color(0.35f, 0.35f, 0.38f, 0.7f), 4, 0.08f, 0.02f);
                yield return Wait(0.12f);
            }
        }

        /// <summary>La tour sonne les cloches en tournant.</summary>
        IEnumerator Tower()
        {
            SoundFX.Play(SoundFX.Chime, Top, 0.9f);
            yield return SpinFor(1.6f, 360f);
            SoundFX.Play(SoundFX.DingDong, Top, 0.6f);
            Effects.Confetti(Top);
        }

        /// <summary>La pyramide tremble, puis son sommet s'illumine.</summary>
        IEnumerator Pyramid()
        {
            SoundFX.Play(SoundFX.Rumble, transform.position, 0.8f);
            yield return Shake(1.5f, 0.003f);
            SoundFX.Play(SoundFX.Chime, Top, 1f);
            for (int i = 0; i < 12; i++)
            {
                Effects.Sparkle(Top, LegoCatalog.Colors[2].Color);
                yield return Wait(0.08f);
            }
            Effects.Confetti(Top);
        }

        /// <summary>La fusée décolle dans un nuage de flammes et redescend doucement.</summary>
        IEnumerator Rocket()
        {
            SoundFX.Play(SoundFX.Rumble, transform.position, 0.6f);
            yield return Shake(0.8f, 0.003f);
            SoundFX.Play(SoundFX.Rocket, transform.position, 1f);
            float t = 0f;
            while (t < 1.6f)
            {
                t += Time.deltaTime;
                float h = 0.5f * t * t * 0.5f;
                m_Visual.localPosition = Vector3.up * h;
                Effects.Puff(m_Visual.position, new Color(1f, 0.55f, 0.1f, 0.9f), 3, 0.15f);
                yield return null;
            }
            Effects.Confetti(Top);
            SoundFX.Play(SoundFX.Boom, Top, 0.5f);
            float top = m_Visual.localPosition.y;
            t = 0f;
            while (t < 1.4f)
            {
                t += Time.deltaTime;
                m_Visual.localPosition = Vector3.up * Mathf.Lerp(top, 0f, Mathf.SmoothStep(0f, 1f, t / 1.4f));
                yield return null;
            }
        }

        /// <summary>Animation par défaut : un tour sur elle-même et deux petits sauts.</summary>
        IEnumerator Spin()
        {
            SoundFX.Play(SoundFX.Chime, Top, 0.7f);
            yield return SpinFor(1.2f, 360f);
            yield return Hop(2, 0.03f, 0.3f);
        }

        // ---------------------------------------------------------------- petits mouvements

        IEnumerator SpinFor(float duration, float degrees)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                m_Visual.localRotation = Quaternion.Euler(0f, degrees * Mathf.SmoothStep(0f, 1f, t / duration), 0f);
                yield return null;
            }
        }

        IEnumerator Hop(int count, float height, float duration)
        {
            for (int i = 0; i < count; i++)
            {
                float t = 0f;
                while (t < duration)
                {
                    t += Time.deltaTime;
                    m_Visual.localPosition = Vector3.up * Mathf.Sin(Mathf.PI * t / duration) * height;
                    yield return null;
                }
                SoundFX.Play(SoundFX.Click, transform.position, 0.6f);
            }
            m_Visual.localPosition = Vector3.zero;
        }

        IEnumerator Shake(float duration, float amplitude)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                m_Visual.localPosition = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * amplitude;
                yield return null;
            }
            m_Visual.localPosition = Vector3.zero;
        }

        static IEnumerator Wait(float seconds)
        {
            yield return new WaitForSeconds(seconds);
        }
    }
}
