using UnityEngine;

namespace ConstructVR
{
    /// <summary>Effets de particules : étincelles, poussière d'emboîtement, confettis.</summary>
    public static class Effects
    {
        static Material s_Material;

        public static void Init(Material particleMaterial) => s_Material = particleMaterial;

        static Material ParticleMaterial
        {
            get
            {
                if (s_Material == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                    s_Material = new Material(shader) { name = "Particules" };
                    s_Material.SetFloat("_Surface", 1f);
                    s_Material.SetFloat("_Blend", 0f);
                    s_Material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    s_Material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    s_Material.SetFloat("_ZWrite", 0f);
                    s_Material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    s_Material.renderQueue = 3000;
                }
                return s_Material;
            }
        }

        static ParticleSystem Create(string name, Vector3 position, float lifetime)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = lifetime;
            main.duration = 0.1f;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = ParticleMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Object.Destroy(go, lifetime + 0.5f);
            return ps;
        }

        /// <summary>Petites étincelles quand une brique apparaît dans la main.</summary>
        public static void Sparkle(Vector3 position, Color color)
        {
            var ps = Create("Étincelles", position, 0.4f);
            var main = ps.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.01f);
            main.startColor = new ParticleSystem.MinMaxGradient(color, Color.white);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.02f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
            ps.Emit(16);
        }

        /// <summary>Petit nuage de poussière blanche quand une brique s'emboîte.</summary>
        public static void SnapPuff(Vector3 position)
        {
            var ps = Create("Emboîtement", position, 0.35f);
            var main = ps.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.012f);
            main.startColor = new Color(1f, 1f, 1f, 0.7f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.02f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            ps.Emit(12);
        }

        /// <summary>Petit nuage coloré (fumée du pot d'échappement, flammes de la fusée…).</summary>
        public static void Puff(Vector3 position, Color color, int count = 8, float speed = 0.12f, float size = 0.012f)
        {
            var ps = Create("Fumée", position, 0.6f);
            var main = ps.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.4f);
            main.startColor = color;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.01f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            ps.Emit(count);
        }

        /// <summary>Pluie de confettis pour fêter une construction terminée.</summary>
        public static void Confetti(Vector3 position)
        {
            var ps = Create("Confettis", position, 3f);
            var main = ps.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.025f);
            main.gravityModifier = 0.45f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var grad = new Gradient();
            grad.SetKeys(new[]
                {
                    new GradientColorKey(LegoCatalog.Colors[0].Color, 0f), new GradientColorKey(LegoCatalog.Colors[2].Color, 0.33f),
                    new GradientColorKey(LegoCatalog.Colors[1].Color, 0.66f), new GradientColorKey(LegoCatalog.Colors[3].Color, 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(grad) { mode = ParticleSystemGradientMode.RandomColor };
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.05f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = fade;
            ps.Emit(160);
        }
    }
}
