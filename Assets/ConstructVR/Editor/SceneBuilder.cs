using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using Object = UnityEngine.Object;

namespace ConstructVR.EditorTools
{
    /// <summary>
    /// Construit toute la scène du jeu (chambre, table, tableau des briques, Brico, menus)
    /// et génère les ressources (maillages, matériaux, textures, police).
    /// Menu : ConstructVR > Construire la scène.
    /// </summary>
    public static class SceneBuilder
    {
        const string Root = "Assets/ConstructVR/Generated";
        const string RootName = "ConstructVR (jeu)";

        // Repère : l'objet du jeu est au pied de la table, le joueur se tient du côté -Z.
        static readonly Vector3 GamePosition = new Vector3(0f, 0f, 0.6f);
        static readonly Vector3 PlayerPosition = new Vector3(0f, 0f, -0.2f);
        const float TableHeight = 0.78f;

        static Material s_White, s_Dark;

        [MenuItem("ConstructVR/Construire la scène", priority = 0)]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("ConstructVR", "Quitte le mode Play avant de construire la scène.", "OK");
                return;
            }
            EnsureFolders();
            var library = BuildLibrary();
            BrickFactory.Init(library);
            UIFactory.Font = BuildFont();
            UIFactory.Rounded = BuildSprite("Arrondi", 96, 96, RoundedRect(96, 96, 36), new Vector4(40, 40, 40, 40));
            var star = BuildSprite("Etoile", 128, 128, StarShape(128), Vector4.zero);
            s_White = Mat("Blanc", new Color(0.95f, 0.95f, 0.93f), 0.3f);
            s_Dark = Mat("Sombre", new Color(0.08f, 0.08f, 0.1f), 0.4f);

            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            CleanTemplate();

            var root = new GameObject(RootName);
            root.transform.position = GamePosition;
            var gm = root.AddComponent<GameManager>();
            gm.Library = library;

            BuildRoom(root.transform);
            BuildLighting(root.transform);
            BuildTable(root.transform, gm, library);
            BuildPalette(root.transform, gm);
            BuildTrash(root.transform, gm);
            BuildModelDisplay(root.transform, gm);
            BuildBrico(root.transform, gm);
            BuildMenus(root.transform, gm, star);
            BuildHud(root.transform, gm);

            var music = new GameObject("Musique", typeof(AudioSource));
            music.transform.SetParent(root.transform, false);
            gm.Music = music.AddComponent<MusicPlayer>();

            var confetti = new GameObject("Point des confettis").transform;
            confetti.SetParent(root.transform, false);
            confetti.localPosition = new Vector3(0f, 1.1f, 0.1f);
            gm.ConfettiPoint = confetti;

            EditorUtility.SetDirty(gm);
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("ConstructVR : scène construite !");
        }

        // ================================================================== ressources

        static void EnsureFolders()
        {
            foreach (var f in new[] { "Assets/ConstructVR", Root, Root + "/Materiaux", Root + "/Textures" })
            {
                if (AssetDatabase.IsValidFolder(f)) continue;
                var parent = Path.GetDirectoryName(f).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(f));
            }
        }

        static LegoLibrary BuildLibrary()
        {
            string path = Root + "/Bibliotheque Lego.asset";
            AssetDatabase.DeleteAsset(path);
            var lib = ScriptableObject.CreateInstance<LegoLibrary>();
            AssetDatabase.CreateAsset(lib, path);

            lib.BrickMeshes = new Mesh[LegoCatalog.Shapes.Length];
            for (int i = 0; i < LegoCatalog.Shapes.Length; i++)
            {
                var mesh = BrickFactory.BuildBrickMesh(LegoCatalog.Shapes[i]);
                AssetDatabase.AddObjectToAsset(mesh, lib);
                lib.BrickMeshes[i] = mesh;
            }
            lib.BaseplateMesh = BrickFactory.BuildBaseplateMesh();
            AssetDatabase.AddObjectToAsset(lib.BaseplateMesh, lib);

            lib.ColorMaterials = new Material[LegoCatalog.Colors.Length];
            for (int i = 0; i < LegoCatalog.Colors.Length; i++)
                lib.ColorMaterials[i] = Mat("Brique " + LegoCatalog.Colors[i].Name, LegoCatalog.Colors[i].Color, 0.78f);

            var ghost = Mat("Fantome", new Color(1f, 1f, 1f, 0.4f), 0.8f);
            MakeTransparent(ghost);
            lib.GhostMaterial = ghost;

            var particles = LoadOrCreateMat("Particules", Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            particles.SetFloat("_Surface", 1f);
            particles.SetFloat("_Blend", 0f);
            particles.SetFloat("_ZWrite", 0f);
            particles.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            particles.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            particles.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            particles.renderQueue = 3000;
            lib.ParticleMaterial = particles;

            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            return lib;
        }

        static TMP_FontAsset BuildFont()
        {
            string path = Root + "/Inter (accents).asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;
            var ttf = AssetDatabase.LoadAssetAtPath<Font>("Assets/VRTemplateAssets/Fonts/Inter/Inter-Regular.ttf");
            var font = TMP_FontAsset.CreateFontAsset(ttf, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            font.name = "Inter (accents)";
            AssetDatabase.CreateAsset(font, path);
            font.atlasTextures[0].name = "Atlas";
            AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);
            font.material.name = "Materiau";
            AssetDatabase.AddObjectToAsset(font.material, font);
            const string chars = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
                                 "àâäçéèêëîïôöùûüÿœæÀÂÄÇÉÈÊËÎÏÔÖÙÛÜŸŒÆ«»’·—–…°";
            font.TryAddCharacters(chars);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            return font;
        }

        static Material LoadOrCreateMat(string name, Shader shader)
        {
            string path = Root + "/Materiaux/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else mat.shader = shader;
            return mat;
        }

        static Material Mat(string name, Color color, float smoothness, Texture2D texture = null, Vector2? tiling = null, Color? emission = null)
        {
            var mat = LoadOrCreateMat(name, Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            mat.SetTexture("_BaseMap", texture);
            mat.SetTextureScale("_BaseMap", tiling ?? Vector2.one);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void MakeTransparent(Material mat)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", 1f);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetShaderPassEnabled("DepthOnly", false);
            mat.SetShaderPassEnabled("ShadowCaster", false);
        }

        static void MakeCutout(Material mat)
        {
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.renderQueue = (int)RenderQueue.AlphaTest;
        }

        // --------------------------------------------------------------- textures

        static Texture2D SaveTexture(string name, int w, int h, Func<int, int, Color> pixel, bool sprite = false, Vector4 border = default, bool repeat = true)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = pixel(x, y);
            tex.SetPixels(px);
            tex.Apply();
            string path = Root + "/Textures/" + name + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = !sprite;
            if (sprite)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spriteBorder = border;
                imp.spritePixelsPerUnit = 100f;
            }
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Sprite BuildSprite(string name, int w, int h, Func<int, int, Color> pixel, Vector4 border)
        {
            SaveTexture(name, w, h, pixel, true, border, false);
            return AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Textures/" + name + ".png");
        }

        static Func<int, int, Color> RoundedRect(int w, int h, float r) => (x, y) =>
        {
            float cx = Mathf.Clamp(x + 0.5f, r, w - r), cy = Mathf.Clamp(y + 0.5f, r, h - r);
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
            return new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 0.5f));
        };

        static Func<int, int, Color> StarShape(int size) => (x, y) =>
        {
            var p = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f) / (size * 0.5f);
            float a = Mathf.Atan2(p.y, p.x) + Mathf.PI / 2f;
            float k = Mathf.Repeat(a, Mathf.PI * 2f / 5f) / (Mathf.PI * 2f / 5f);
            float radius = Mathf.Lerp(0.95f, 0.42f, Mathf.Abs(k - 0.5f) * -2f + 1f);
            radius = Mathf.Lerp(0.42f, 0.95f, Mathf.Abs(k - 0.5f) * 2f);
            float d = (radius - p.magnitude) * size * 0.5f;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(d + 0.5f));
        };

        static float Hash(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / (float)uint.MaxValue;
        }

        static float Noise(float x, float y)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            return Mathf.Lerp(Mathf.Lerp(Hash(ix, iy), Hash(ix + 1, iy), fx), Mathf.Lerp(Hash(ix, iy + 1), Hash(ix + 1, iy + 1), fx), fy);
        }

        static Texture2D WoodTexture(string name, Color light, Color dark, int planks)
        {
            const int size = 512;
            return SaveTexture(name, size, size, (x, y) =>
            {
                int plank = y * planks / size;
                float offset = Hash(plank, 7) * 400f;
                float grain = Noise((x + offset) * 0.02f, y * 0.35f) * 0.6f + Noise((x + offset) * 0.08f, y * 0.9f) * 0.4f;
                float rings = Mathf.Sin((x + offset) * 0.05f + grain * 6f) * 0.5f + 0.5f;
                var c = Color.Lerp(light, dark, rings * 0.45f + grain * 0.35f);
                c *= 0.92f + Hash(plank, 3) * 0.16f;
                float edge = (y * planks % size) / (float)size * planks;
                if (y % (size / planks) < 2) c *= 0.55f;
                int seam = (int)(Hash(plank, 11) * size);
                if (Mathf.Abs(x - seam) < 2) c *= 0.6f;
                c.a = 1f;
                return c;
            });
        }

        // ================================================================== nettoyage du template

        static void CleanTemplate()
        {
            foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                switch (go.name)
                {
                    case "Environment":
                    case "Teleport Area Setup":
                    case "Interactables":
                    case "UI":
                    case "2x2":
                        go.SetActive(false);
                        break;
                    case "Lighting":
                        foreach (Transform c in go.transform)
                            if (c.name == "Directional Light") c.gameObject.SetActive(false);
                        break;
                }
                if (go.name.StartsWith("XR Origin"))
                {
                    go.transform.SetPositionAndRotation(PlayerPosition, Quaternion.identity);
                    foreach (var t in go.GetComponentsInChildren<Transform>(true))
                        if (t.name.StartsWith("Affordance Callouts")) t.gameObject.SetActive(false);
                }
            }
        }

        // ================================================================== primitives

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool collider = false, Quaternion? rot = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, bool collider = false, Quaternion? rot = null) =>
            Prim(PrimitiveType.Cube, name, parent, pos, size, mat, collider, rot);

        static GameObject Cyl(string name, Transform parent, Vector3 pos, float radius, float height, Material mat, bool collider = false) =>
            Prim(PrimitiveType.Cylinder, name, parent, pos, new Vector3(radius * 2f, height * 0.5f, radius * 2f), mat, collider);

        static Transform Group(string name, Transform parent, Vector3 pos, Quaternion? rot = null)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localRotation = rot ?? Quaternion.identity;
            return t;
        }

        static GameObject BrickDecor(Transform parent, int shape, int color, Vector3 pos, float scale, float yaw = 0f, bool collider = false)
        {
            var go = new GameObject("Brique géante " + LegoCatalog.Describe(shape, color));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = BrickFactory.GetMesh(shape);
            go.AddComponent<MeshRenderer>().sharedMaterial = BrickFactory.GetMaterial(color);
            if (collider)
            {
                var s = LegoCatalog.Shapes[shape];
                var bc = go.AddComponent<BoxCollider>();
                bc.size = new Vector3(s.SizeX * LegoDims.Pitch, s.Height, s.SizeZ * LegoDims.Pitch);
                bc.center = new Vector3(0f, s.Height * 0.5f, 0f);
            }
            return go;
        }

        // ================================================================== chambre

        static void BuildRoom(Transform root)
        {
            var room = Group("Chambre", root, new Vector3(0f, 0f, -0.2f));
            const float w = 5.2f, d = 5.2f, h = 2.7f;

            var floorTex = WoodTexture("Parquet", new Color(0.78f, 0.58f, 0.38f), new Color(0.52f, 0.33f, 0.18f), 8);
            var floorMat = Mat("Parquet", Color.white, 0.45f, floorTex, new Vector2(3f, 3f));
            var floor = Box("Sol", room, new Vector3(0f, -0.05f, 0f), new Vector3(w, 0.1f, d), floorMat, true);
            var area = floor.AddComponent<TeleportationArea>();
            area.colliders.Clear();
            area.colliders.Add(floor.GetComponent<Collider>());

            var wallMat = Mat("Mur", new Color(0.93f, 0.89f, 0.80f), 0.1f);
            var accentMat = Mat("Mur bleu", new Color(0.55f, 0.72f, 0.86f), 0.1f);
            Box("Mur du fond", room, new Vector3(0f, h / 2f, d / 2f), new Vector3(w, h, 0.1f), accentMat, true);
            Box("Mur arrière", room, new Vector3(0f, h / 2f, -d / 2f), new Vector3(w, h, 0.1f), wallMat, true);
            Box("Mur gauche", room, new Vector3(-w / 2f, h / 2f, 0f), new Vector3(0.1f, h, d), wallMat, true);
            Box("Mur droit", room, new Vector3(w / 2f, h / 2f, 0f), new Vector3(0.1f, h, d), wallMat, true);
            Box("Plafond", room, new Vector3(0f, h + 0.05f, 0f), new Vector3(w, 0.1f, d), Mat("Plafond", new Color(0.97f, 0.97f, 0.95f), 0.05f));

            // plinthes
            foreach (var (pos, size) in new[]
            {
                (new Vector3(0f, 0.05f, d / 2f - 0.06f), new Vector3(w, 0.1f, 0.02f)),
                (new Vector3(0f, 0.05f, -d / 2f + 0.06f), new Vector3(w, 0.1f, 0.02f)),
                (new Vector3(-w / 2f + 0.06f, 0.05f, 0f), new Vector3(0.02f, 0.1f, d)),
                (new Vector3(w / 2f - 0.06f, 0.05f, 0f), new Vector3(0.02f, 0.1f, d)),
            })
                Box("Plinthe", room, pos, size, s_White);

            // fenêtre avec ciel (mur du fond, à gauche)
            var sky = SaveTexture("Ciel", 64, 256, (x, y) =>
            {
                float t = y / 255f;
                var c = Color.Lerp(new Color(0.85f, 0.93f, 1f), new Color(0.38f, 0.64f, 0.95f), t);
                float cloud = Mathf.Clamp01(Noise(x * 0.08f, y * 0.04f) * 1.6f - 0.85f);
                return Color.Lerp(c, Color.white, cloud * (1f - t * 0.5f));
            }, false, default, false);
            var skyMat = Mat("Ciel", Color.white, 0f, sky, null, new Color(0.9f, 0.95f, 1f));
            skyMat.SetTexture("_EmissionMap", sky);
            var win = Group("Fenêtre", room, new Vector3(-1.1f, 1.55f, d / 2f - 0.06f));
            Box("Ciel", win, Vector3.zero, new Vector3(1.4f, 1.0f, 0.01f), skyMat);
            foreach (var (pos, size) in new[]
            {
                (new Vector3(0f, 0.53f, -0.02f), new Vector3(1.52f, 0.07f, 0.05f)), (new Vector3(0f, -0.53f, -0.03f), new Vector3(1.6f, 0.08f, 0.12f)),
                (new Vector3(-0.73f, 0f, -0.02f), new Vector3(0.07f, 1.1f, 0.05f)), (new Vector3(0.73f, 0f, -0.02f), new Vector3(0.07f, 1.1f, 0.05f)),
                (new Vector3(0f, 0f, -0.02f), new Vector3(0.04f, 1.0f, 0.03f)), (new Vector3(0f, 0.05f, -0.02f), new Vector3(1.4f, 0.04f, 0.03f)),
            })
                Box("Cadre", win, pos, size, s_White);

            // affiche du jeu (mur du fond, à droite)
            var poster = Group("Affiche", room, new Vector3(1.15f, 1.6f, d / 2f - 0.06f));
            Box("Cadre", poster, Vector3.zero, new Vector3(1.0f, 0.7f, 0.03f), Mat("Cadre noir", new Color(0.12f, 0.12f, 0.14f), 0.5f));
            Box("Fond", poster, new Vector3(0f, 0f, -0.02f), new Vector3(0.92f, 0.62f, 0.01f), Mat("Affiche", new Color(0.98f, 0.79f, 0.04f), 0.2f));
            var title = UIFactory.CreateLabel(poster, ColoredTitle(), new Vector3(0f, 0.1f, -0.03f), 0.11f, Color.white, 0.9f);
            var sub = UIFactory.CreateLabel(poster, "Construis tes rêves, brique par brique", new Vector3(0f, -0.08f, -0.03f), 0.035f, new Color(0.15f, 0.15f, 0.2f), 0.9f);
            for (int i = 0; i < 4; i++)
                BrickDecor(poster, 6, i, new Vector3(-0.27f + i * 0.18f, -0.25f, -0.03f), 1.2f).transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            // tapis sous la table
            var rugTex = SaveTexture("Tapis", 256, 256, (x, y) =>
            {
                float r = Vector2.Distance(new Vector2(x, y), new Vector2(128f, 128f)) / 128f;
                var c = r > 0.92f ? new Color(0.85f, 0.25f, 0.2f) : r > 0.86f ? new Color(0.98f, 0.85f, 0.4f) : r > 0.5f ? new Color(0.27f, 0.45f, 0.72f) : new Color(0.32f, 0.52f, 0.8f);
                c *= 0.92f + Noise(x * 0.3f, y * 0.3f) * 0.1f;
                c.a = 1f;
                return c;
            }, false, default, false);
            var rug = Cyl("Tapis", room, new Vector3(0f, 0.004f, 0.15f), 1.25f, 0.008f, Mat("Tapis", Color.white, 0.05f, rugTex));
            rug.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);

            // étagère avec des modèles déjà construits (mur droit)
            var shelfMat = Mat("Bois clair", new Color(0.86f, 0.70f, 0.50f), 0.35f);
            var shelf = Group("Étagère", room, new Vector3(w / 2f - 0.2f, 0f, 0.7f), Quaternion.Euler(0f, -90f, 0f));
            for (int i = 0; i < 2; i++)
                Box("Planche", shelf, new Vector3(0f, 1.05f + i * 0.45f, 0f), new Vector3(1.2f, 0.03f, 0.28f), shelfMat);
            DecorModel(shelf, LegoCatalog.Models[2], new Vector3(-0.35f, 1.065f, 0f), 1.4f, 20f);
            DecorModel(shelf, LegoCatalog.Models[1], new Vector3(0.3f, 1.065f, 0f), 1.4f, -25f);
            DecorModel(shelf, LegoCatalog.Models[3], new Vector3(-0.2f, 1.515f, 0f), 1.3f, 0f);
            DecorModel(shelf, LegoCatalog.Models[0], new Vector3(0.35f, 1.515f, 0f), 1.4f, 30f);

            // briques géantes pour décorer
            BrickDecor(room, 6, 0, new Vector3(-1.8f, 0f, 1.6f), 9f, 20f, true);
            BrickDecor(room, 4, 2, new Vector3(-1.75f, 0.216f, 1.55f), 9f, 35f, true);
            BrickDecor(room, 5, 1, new Vector3(1.85f, 0f, -1.7f), 9f, -15f, true);
            BrickDecor(room, 0, 3, new Vector3(-2.0f, 0f, -1.8f), 9f, 10f, true);

            // plante
            var plant = Group("Plante", room, new Vector3(2.05f, 0f, 2.05f));
            Cyl("Pot", plant, new Vector3(0f, 0.18f, 0f), 0.16f, 0.36f, Mat("Terre cuite", new Color(0.78f, 0.42f, 0.27f), 0.2f));
            var leaf = Mat("Feuilles", new Color(0.22f, 0.55f, 0.25f), 0.35f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                Prim(PrimitiveType.Sphere, "Feuillage", plant, new Vector3(Mathf.Cos(a) * 0.12f, 0.55f + (i % 2) * 0.12f, Mathf.Sin(a) * 0.12f), new Vector3(0.22f, 0.3f, 0.22f), leaf);
            }
            Prim(PrimitiveType.Sphere, "Feuillage", plant, new Vector3(0f, 0.75f, 0f), new Vector3(0.26f, 0.32f, 0.26f), leaf);

            // lampe au plafond
            var lamp = Group("Lampe", room, new Vector3(0f, h, 0.6f));
            Cyl("Fil", lamp, new Vector3(0f, -0.2f, 0f), 0.005f, 0.4f, s_Dark);
            Prim(PrimitiveType.Sphere, "Abat-jour", lamp, new Vector3(0f, -0.45f, 0f), new Vector3(0.32f, 0.2f, 0.32f), Mat("Abat-jour", new Color(0.98f, 0.79f, 0.04f), 0.5f));
            Prim(PrimitiveType.Sphere, "Ampoule", lamp, new Vector3(0f, -0.53f, 0f), Vector3.one * 0.09f, Mat("Ampoule", Color.white, 0.9f, null, null, new Color(2.5f, 2.2f, 1.6f)));
        }

        static string ColoredTitle()
        {
            string[] hex = { "#C91A17", "#0D54BF", "#2E8B57", "#F57A0D", "#C91A17", "#0D54BF", "#2E8B57", "#F57A0D", "#C91A17", "#0D54BF", "#2E8B57" };
            const string word = "ConstructVR";
            var s = "";
            for (int i = 0; i < word.Length; i++) s += "<color=" + hex[i] + ">" + word[i] + "</color>";
            return s;
        }

        static void DecorModel(Transform parent, BuildModel model, Vector3 pos, float scale, float yaw)
        {
            var holder = Group("Modèle " + model.Name, parent, pos, Quaternion.Euler(0f, yaw, 0f));
            holder.localScale = Vector3.one * scale;
            var inner = Group("Pièces", holder, Vector3.zero);
            var bounds = new Bounds();
            bool first = true;
            foreach (var s in model.Steps)
            {
                var v = BrickFactory.CreateVisual(s.Shape, BrickFactory.GetMaterial(s.Color), inner);
                v.transform.localPosition = BrickFactory.LocalPosition(s);
                v.transform.localRotation = BrickFactory.LocalRotation(s.Rot);
                var f = LegoCatalog.Shapes[s.Shape].Footprint(s.Rot);
                var b = new Bounds(v.transform.localPosition, new Vector3(f.x * LegoDims.Pitch, 0.001f, f.y * LegoDims.Pitch));
                if (first) { bounds = b; first = false; } else bounds.Encapsulate(b);
            }
            inner.localPosition = new Vector3(-bounds.center.x, 0f, -bounds.center.z);
        }

        // ================================================================== lumières

        static void BuildLighting(Transform root)
        {
            var sun = new GameObject("Soleil (fenêtre)").AddComponent<Light>();
            sun.transform.SetParent(root, false);
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.93f, 0.82f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.6f;
            sun.transform.rotation = Quaternion.Euler(48f, 160f, 0f);

            var lamp = new GameObject("Lumière de la lampe").AddComponent<Light>();
            lamp.transform.SetParent(root, false);
            lamp.transform.localPosition = new Vector3(0f, 2.05f, 0.4f);
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.85f, 0.65f);
            lamp.intensity = 2.2f;
            lamp.range = 5f;
            lamp.shadows = LightShadows.None;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.50f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.25f, 0.2f);
            RenderSettings.fog = false;
        }

        // ================================================================== table et plaque

        static void BuildTable(Transform root, GameManager gm, LegoLibrary lib)
        {
            var woodTex = WoodTexture("Bois table", new Color(0.62f, 0.42f, 0.25f), new Color(0.38f, 0.22f, 0.11f), 4);
            var wood = Mat("Bois table", Color.white, 0.45f, woodTex);
            var table = Group("Table", root, Vector3.zero);
            Box("Plateau", table, new Vector3(0f, TableHeight - 0.025f, 0f), new Vector3(1.1f, 0.05f, 0.75f), wood, true);
            Box("Ceinture", table, new Vector3(0f, TableHeight - 0.08f, 0f), new Vector3(1.0f, 0.06f, 0.65f), wood);
            foreach (var x in new[] { -0.5f, 0.5f })
            foreach (var z in new[] { -0.32f, 0.32f })
                Box("Pied", table, new Vector3(x, (TableHeight - 0.05f) * 0.5f, z), new Vector3(0.06f, TableHeight - 0.05f, 0.06f), wood, true);

            var areaGo = new GameObject("Plaque de base");
            areaGo.transform.SetParent(root, false);
            areaGo.transform.localPosition = new Vector3(0f, TableHeight + LegoDims.PlateHeight, -0.02f);
            gm.Area = areaGo.AddComponent<BuildArea>();
            var plate = new GameObject("Plaque (visuel)");
            plate.transform.SetParent(areaGo.transform, false);
            plate.transform.localPosition = new Vector3(0f, -LegoDims.PlateHeight, 0f);
            plate.AddComponent<MeshFilter>().sharedMesh = lib.BaseplateMesh;
            plate.AddComponent<MeshRenderer>().sharedMaterial = lib.ColorMaterials[LegoCatalog.BaseplateColor];
            var col = plate.AddComponent<BoxCollider>();
            float side = LegoCatalog.BaseplateSize * LegoDims.Pitch;
            col.size = new Vector3(side, LegoDims.PlateHeight, side);
            col.center = new Vector3(0f, LegoDims.PlateHeight * 0.5f, 0f);
        }

        // ================================================================== tableau des briques

        static void BuildPalette(Transform root, GameManager gm)
        {
            const int columns = 5;
            const float cell = 0.11f;
            var pos = new Vector3(-0.68f, 1.3f, -0.5f);
            var toPlayer = new Vector3(0f, 0f, -0.8f) - pos;
            toPlayer.y = 0f;
            var palette = Group("Tableau des briques", root, pos, Quaternion.LookRotation(-toPlayer.normalized) * Quaternion.Euler(14f, 0f, 0f));
            gm.Palette = palette.gameObject.AddComponent<BrickPalette>();

            var boardMat = Mat("Tableau", new Color(0.13f, 0.17f, 0.27f), 0.3f);
            var cellMat = Mat("Case", new Color(0.21f, 0.27f, 0.40f), 0.3f);
            var frameMat = Mat("Sélection", new Color(1f, 0.82f, 0.15f), 0.4f, null, null, new Color(0.8f, 0.6f, 0.05f));
            float width = columns * cell + 0.07f;
            float height = 0.62f;
            float top = 0.32f;
            Box("Planche", palette, new Vector3(0f, top - height / 2f, 0.015f), new Vector3(width, height, 0.025f), boardMat);
            Box("Bordure", palette, new Vector3(0f, top + 0.005f, 0.015f), new Vector3(width + 0.02f, 0.02f, 0.035f), BrickFactory.GetMaterial(2));
            Box("Bordure", palette, new Vector3(0f, top - height - 0.005f, 0.015f), new Vector3(width + 0.02f, 0.02f, 0.035f), BrickFactory.GetMaterial(2));

            // pied du tableau (chevalet)
            var standMat = Mat("Métal", new Color(0.3f, 0.32f, 0.36f), 0.7f);
            var stand = Group("Pied", palette, Vector3.zero, Quaternion.Inverse(palette.localRotation));
            float bottom = 1.3f + top - height - 0.01f;
            Cyl("Mât", stand, new Vector3(0f, -bottom / 2f - (1.3f - bottom) + 0.0f, 0.02f), 0.015f, bottom, standMat);
            Cyl("Socle", stand, new Vector3(0f, -1.3f + 0.01f, 0.02f), 0.18f, 0.02f, standMat);

            UIFactory.CreateLabel(palette, "Briques", new Vector3(0f, top - 0.035f, -0.002f), 0.035f, Color.white);
            for (int i = 0; i < LegoCatalog.Shapes.Length; i++)
            {
                int r = i / columns, c = i % columns;
                var slotPos = new Vector3((c - (columns - 1) * 0.5f) * cell, top - 0.12f - r * cell, 0f);
                BrickSlot(palette, i, slotPos, cell, cellMat, frameMat);
            }

            float colorsY = top - 0.12f - 2 * cell + 0.01f;
            UIFactory.CreateLabel(palette, "Couleurs", new Vector3(0f, colorsY, -0.002f), 0.03f, Color.white);
            int n = LegoCatalog.Colors.Length;
            float step = (width - 0.07f) / n;
            for (int i = 0; i < n; i++)
                ColorSlot(palette, i, new Vector3((i - (n - 1) * 0.5f) * step, colorsY - 0.065f, -0.005f), step, frameMat);
        }

        static void BrickSlot(Transform parent, int shapeId, Vector3 pos, float cell, Material cellMat, Material frameMat)
        {
            var shape = LegoCatalog.Shapes[shapeId];
            var root = Group("Case " + shape.Name, parent, pos);
            Box("Fond", root, new Vector3(0f, 0f, 0.002f), new Vector3(cell * 0.92f, cell * 0.92f, 0.004f), cellMat);
            var frame = Box("Surbrillance", root, new Vector3(0f, 0f, 0.004f), new Vector3(cell * 0.99f, cell * 0.99f, 0.003f), frameMat);
            frame.SetActive(false);

            var visual = BrickFactory.CreateVisual(shapeId, BrickFactory.GetMaterial(0), root);
            float size = Mathf.Max(shape.SizeX, shape.SizeZ) * LegoDims.Pitch;
            float fit = Mathf.Min(1f, cell * 0.68f / size);
            visual.transform.localScale = Vector3.one * fit;
            visual.transform.localRotation = Quaternion.Euler(-40f, 30f, 0f) * (shape.SizeZ > shape.SizeX ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity);
            visual.transform.localPosition = new Vector3(0f, 0.008f - shape.Height * 0.4f * fit, -0.025f);

            UIFactory.CreateLabel(root, shape.ShortName, new Vector3(0f, -cell * 0.37f, -0.003f), 0.016f, new Color(1f, 1f, 1f, 0.85f));

            var col = root.gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(cell * 0.95f, cell * 0.95f, 0.06f);
            col.center = new Vector3(0f, 0f, -0.02f);
            root.gameObject.AddComponent<XRSimpleInteractable>();
            var slot = root.gameObject.AddComponent<PaletteSlot>();
            slot.Kind = PaletteSlot.SlotKind.Brick;
            slot.Index = shapeId;
            slot.Visual = visual.transform;
            slot.VisualRenderer = visual.GetComponent<MeshRenderer>();
            slot.SelectedFrame = frame;
        }

        static void ColorSlot(Transform parent, int colorId, Vector3 pos, float step, Material frameMat)
        {
            var root = Group("Couleur " + LegoCatalog.Colors[colorId].Name, parent, pos);
            var ring = Prim(PrimitiveType.Cylinder, "Sélection", root, new Vector3(0f, 0f, 0.004f), new Vector3(step * 0.98f, 0.002f, step * 0.98f), frameMat, false, Quaternion.Euler(90f, 0f, 0f));
            ring.SetActive(false);
            var disc = new GameObject("Pastille");
            disc.transform.SetParent(root, false);
            disc.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            disc.transform.localScale = Vector3.one * 0.85f;
            disc.AddComponent<MeshFilter>().sharedMesh = BrickFactory.GetMesh(0);
            disc.AddComponent<MeshRenderer>().sharedMaterial = BrickFactory.GetMaterial(colorId);

            var col = root.gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(step * 0.95f, step * 0.95f, 0.05f);
            col.center = new Vector3(0f, 0f, -0.015f);
            root.gameObject.AddComponent<XRSimpleInteractable>();
            var slot = root.gameObject.AddComponent<PaletteSlot>();
            slot.Kind = PaletteSlot.SlotKind.Color;
            slot.Index = colorId;
            slot.Visual = disc.transform;
            slot.VisualRenderer = disc.GetComponent<MeshRenderer>();
            slot.SelectedFrame = ring;
        }

        // ================================================================== poubelle

        static void BuildTrash(Transform root, GameManager gm)
        {
            var bin = Group("Poubelle", root, new Vector3(0.78f, 0f, -0.3f));
            var red = Mat("Poubelle", new Color(0.82f, 0.14f, 0.12f), 0.6f);
            Cyl("Corps", bin, new Vector3(0f, 0.36f, 0f), 0.17f, 0.72f, red, true);
            Cyl("Bord", bin, new Vector3(0f, 0.725f, 0f), 0.18f, 0.02f, s_White);
            Cyl("Ouverture", bin, new Vector3(0f, 0.731f, 0f), 0.155f, 0.005f, s_Dark);
            var label = UIFactory.CreateLabel(bin, "Poubelle", new Vector3(0f, 0.5f, -0.175f), 0.045f, Color.white);
            var arrow = UIFactory.CreateLabel(bin, "Jette ici les briques\ndont tu ne veux plus", new Vector3(0f, 0.38f, -0.175f), 0.022f, new Color(1f, 1f, 1f, 0.85f), 0.4f, false);
            arrow.textWrappingMode = TextWrappingModes.NoWrap;

            var zone = new GameObject("Zone de la poubelle");
            zone.transform.SetParent(bin, false);
            zone.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            var box = zone.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(0.4f, 0.4f, 0.4f);
            gm.TrashZone = box;
        }

        // ================================================================== présentoir du modèle

        static void BuildModelDisplay(Transform root, GameManager gm)
        {
            var display = Group("Présentoir du modèle", root, new Vector3(-0.4f, TableHeight, 0.24f));
            Cyl("Socle", display, new Vector3(0f, 0.01f, 0f), 0.085f, 0.02f, s_White);
            Cyl("Anneau lumineux", display, new Vector3(0f, 0.021f, 0f), 0.08f, 0.002f, Mat("Lueur bleue", new Color(0.3f, 0.65f, 1f), 0.9f, null, null, new Color(0.3f, 0.7f, 1.6f)));
            var spinner = Group("Modèle (tourne)", display, new Vector3(0f, 0.023f, 0f));
            var label = UIFactory.CreateLabel(display, "Modèle", new Vector3(0f, 0.26f, 0f), 0.03f, Color.white);
            label.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            var caption = UIFactory.CreateLabel(display, "À reproduire", new Vector3(0f, 0.225f, 0f), 0.018f, new Color(1f, 1f, 1f, 0.75f), 1f, false);
            gm.ModelDisplay = spinner;
            gm.ModelDisplayRoot = display.gameObject;
            gm.ModelDisplayLabel = label;
        }

        // ================================================================== Brico

        static void BuildBrico(Transform root, GameManager gm)
        {
            var home = Group("Place de Brico", root, new Vector3(0.62f, 1.18f, 0.05f));
            var brico = Group("Brico (assistant)", root, home.localPosition);
            var a = brico.gameObject.AddComponent<Assistant>();
            a.Home = home;

            var yellow = Mat("Brico peau", new Color(0.98f, 0.80f, 0.12f), 0.65f);
            var shirt = Mat("Brico pull", new Color(0.05f, 0.40f, 0.85f), 0.55f);
            var pants = Mat("Brico pantalon", new Color(0.2f, 0.22f, 0.3f), 0.5f);
            var capMat = Mat("Brico casquette", new Color(0.85f, 0.15f, 0.12f), 0.6f);

            var body = Group("Corps", brico, Vector3.zero);
            a.Body = body;
            // jambes et bassin
            Box("Bassin", body, new Vector3(0f, -0.055f, 0f), new Vector3(0.07f, 0.014f, 0.035f), pants);
            Box("Jambe", body, new Vector3(-0.018f, -0.085f, 0f), new Vector3(0.032f, 0.05f, 0.034f), pants);
            Box("Jambe", body, new Vector3(0.018f, -0.085f, 0f), new Vector3(0.032f, 0.05f, 0.034f), pants);
            // torse (trapèze)
            var torso = new GameObject("Torse");
            torso.transform.SetParent(body, false);
            torso.transform.localPosition = new Vector3(0f, -0.048f, 0f);
            torso.AddComponent<MeshFilter>().sharedMesh = SaveMesh(TrapezoidMesh(0.075f, 0.055f, 0.06f, 0.034f), "Torse de Brico");
            torso.AddComponent<MeshRenderer>().sharedMaterial = shirt;
            // logo brique sur le pull
            var logo = BrickDecor(body, 6, 2, new Vector3(0f, -0.02f, -0.018f), 0.35f);
            logo.transform.localRotation = Quaternion.Euler(-90f, 90f, 0f);
            // cou + tête
            Cyl("Cou", body, new Vector3(0f, 0.018f, 0f), 0.012f, 0.008f, yellow);
            var head = Cyl("Tête", body, new Vector3(0f, 0.045f, 0f), 0.026f, 0.045f, yellow);
            Cyl("Tenon", body, new Vector3(0f, 0.072f, 0f), 0.014f, 0.01f, yellow);
            // casquette à hélice (c'est comme ça qu'il vole !)
            Prim(PrimitiveType.Sphere, "Casquette", body, new Vector3(0f, 0.074f, 0f), new Vector3(0.058f, 0.03f, 0.058f), capMat);
            Box("Visière", body, new Vector3(0f, 0.071f, -0.03f), new Vector3(0.04f, 0.004f, 0.025f), capMat);
            Cyl("Axe", body, new Vector3(0f, 0.095f, 0f), 0.003f, 0.02f, s_Dark);
            var prop = Group("Hélice", body, new Vector3(0f, 0.105f, 0f));
            var propMat = Mat("Hélice", new Color(0.98f, 0.79f, 0.04f), 0.6f);
            Box("Pale", prop, new Vector3(0.025f, 0f, 0f), new Vector3(0.05f, 0.002f, 0.012f), propMat, false, Quaternion.Euler(12f, 0f, 0f));
            Box("Pale", prop, new Vector3(-0.025f, 0f, 0f), new Vector3(0.05f, 0.002f, 0.012f), propMat, false, Quaternion.Euler(-12f, 0f, 0f));
            a.Propeller = prop;

            // visage (textures découpées)
            var eyeTex = SaveTexture("Yeux de Brico", 128, 64, (x, y) =>
            {
                Color c = Color.clear;
                foreach (var cx in new[] { 38f, 90f })
                {
                    var p = new Vector2((x - cx) / 13f, (y - 32f) / 20f);
                    if (p.magnitude < 1f) c = new Color(0.08f, 0.08f, 0.1f, 1f);
                    if (Vector2.Distance(new Vector2(x, y), new Vector2(cx + 4f, 40f)) < 5f) c = Color.white;
                }
                return c;
            }, false, default, false);
            var mouthTex = SaveTexture("Bouche de Brico", 128, 64, (x, y) =>
            {
                var p = new Vector2((x - 64f) / 44f, (y - 52f) / 40f);
                float r = p.magnitude;
                bool arc = r > 0.78f && r < 1f && y < 52;
                return arc ? new Color(0.08f, 0.08f, 0.1f, 1f) : Color.clear;
            }, false, default, false);
            var eyeMat = Mat("Yeux de Brico", Color.white, 0.3f, eyeTex); MakeCutout(eyeMat);
            var mouthMat = Mat("Bouche de Brico", Color.white, 0.3f, mouthTex); MakeCutout(mouthMat);
            var eyes = Prim(PrimitiveType.Quad, "Yeux", body, new Vector3(0f, 0.052f, -0.0262f), new Vector3(0.036f, 0.018f, 1f), eyeMat);
            var mouth = Prim(PrimitiveType.Quad, "Bouche", body, new Vector3(0f, 0.034f, -0.0262f), new Vector3(0.03f, 0.015f, 1f), mouthMat);
            eyes.transform.localRotation = Quaternion.identity;
            mouth.transform.localRotation = Quaternion.identity;
            a.Eyes = eyes.transform;
            a.Mouth = mouth.transform;
            _ = head;

            // bras (pivot à l'épaule) avec mains en C
            a.ArmLeft = Arm(body, -1, shirt, yellow);
            a.ArmRight = Arm(body, 1, shirt, yellow);

            // bulle de dialogue
            var bubble = UIFactory.CreateCanvas("Bulle", brico, new Vector2(480f, 190f), new Vector3(-0.06f, 0.26f, 0f), Quaternion.identity, 0.0009f, Color.white);
            var tail = UIFactory.CreateImage(bubble, "Pointe", new Vector2(80f, -95f), new Vector2(40f, 40f), Color.white, null, false);
            tail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            a.Text = UIFactory.CreateText(bubble, "", 27f, Vector2.zero, new Vector2(445f, 170f), TextAlignmentOptions.Center, false, new Color(0.12f, 0.13f, 0.18f));
            a.Bubble = bubble.gameObject;

            // pointeur
            var line = new GameObject("Pointeur").AddComponent<LineRenderer>();
            line.transform.SetParent(brico, false);
            line.positionCount = 2;
            line.widthMultiplier = 0.005f;
            line.useWorldSpace = true;
            line.sharedMaterial = Mat("Pointeur", new Color(1f, 0.85f, 0.15f), 0.2f, null, null, new Color(1f, 0.8f, 0.1f));
            line.shadowCastingMode = ShadowCastingMode.Off;
            a.Line = line;
            var marker = Prim(PrimitiveType.Sphere, "Repère", brico, Vector3.zero, Vector3.one * 0.02f, line.sharedMaterial);
            a.Marker = marker.transform;
            gm.Brico = a;
        }

        static Transform Arm(Transform body, int side, Material shirt, Material skin)
        {
            var pivot = Group(side < 0 ? "Bras gauche" : "Bras droit", body, new Vector3(side * 0.04f, -0.012f, 0f), Quaternion.Euler(0f, 0f, side * 12f));
            Prim(PrimitiveType.Capsule, "Manche", pivot, new Vector3(0f, -0.022f, 0f), new Vector3(0.018f, 0.022f, 0.018f), shirt);
            Prim(PrimitiveType.Sphere, "Main", pivot, new Vector3(0f, -0.048f, 0f), Vector3.one * 0.018f, skin);
            return pivot;
        }

        static Mesh TrapezoidMesh(float bottomW, float topW, float height, float depth)
        {
            var b = new BrickFactory.MeshBuilder();
            float hd = depth / 2f;
            Vector3 bl0 = new Vector3(-bottomW / 2, 0, -hd), br0 = new Vector3(bottomW / 2, 0, -hd), tl0 = new Vector3(-topW / 2, height, -hd), tr0 = new Vector3(topW / 2, height, -hd);
            Vector3 bl1 = new Vector3(-bottomW / 2, 0, hd), br1 = new Vector3(bottomW / 2, 0, hd), tl1 = new Vector3(-topW / 2, height, hd), tr1 = new Vector3(topW / 2, height, hd);
            b.Quad(bl0, tl0, tr0, br0, Vector3.back);
            b.Quad(bl1, br1, tr1, tl1, Vector3.forward);
            b.Quad(tl0, tl1, tr1, tr0, Vector3.up);
            b.Quad(bl0, br0, br1, bl1, Vector3.down);
            var nl = Vector3.Cross(tl1 - bl1, bl0 - bl1).normalized; if (nl.x > 0) nl = -nl;
            var nr = new Vector3(-nl.x, nl.y, nl.z);
            b.Quad(bl0, bl1, tl1, tl0, nl);
            b.Quad(br0, tr0, tr1, br1, nr);
            return b.ToMesh();
        }

        static Mesh SaveMesh(Mesh mesh, string name)
        {
            mesh.name = name;
            string path = Root + "/" + name + ".asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // ================================================================== menus

        static readonly Color Green = new Color(0.16f, 0.62f, 0.32f);
        static readonly Color Blue = new Color(0.12f, 0.45f, 0.86f);
        static readonly Color Orange = new Color(0.95f, 0.52f, 0.1f);
        static readonly Color Purple = new Color(0.55f, 0.32f, 0.82f);
        static readonly Color Grey = new Color(0.32f, 0.37f, 0.48f);
        static readonly Color Red = new Color(0.82f, 0.2f, 0.18f);

        static readonly Vector3 MenuPosition = new Vector3(0f, 1.42f, 0.05f);
        static readonly Quaternion MenuRotation = Quaternion.Euler(8f, 0f, 0f);

        static RectTransform Panel(Transform root, string name, Vector2 size)
        {
            var p = UIFactory.CreateCanvas(name, root, size, MenuPosition, MenuRotation, 0.00085f, UIFactory.PanelColor);
            // petite rangée de tenons colorés en haut, comme une brique
            for (int i = 0; i < 4; i++)
                UIFactory.CreateImage(p, "Tenon", new Vector2(-size.x / 2f + 60f + i * 46f, size.y / 2f - 34f), new Vector2(34f, 18f), LegoCatalog.Colors[new[] { 0, 1, 2, 3 }[i]].Color);
            return p;
        }

        static void BuildMenus(Transform root, GameManager gm, Sprite star)
        {
            // ---------- menu principal
            var main = Panel(root, "Menu principal", new Vector2(980f, 820f));
            UIFactory.CreateText(main, ColoredTitle(), 96f, new Vector2(0f, 300f), new Vector2(900f, 120f), TextAlignmentOptions.Center, true);
            UIFactory.CreateText(main, "Construis tout ce que tu veux avec des briques Lego !", 32f, new Vector2(0f, 222f), new Vector2(900f, 50f), TextAlignmentOptions.Center, false, UIFactory.TextDim);
            ModeButton(main, "Tutoriel", "Apprends à jouer en 2 minutes", new Vector2(-225f, 105f), Purple, "tutorial");
            ModeButton(main, "Construction libre", "Construis sans limite", new Vector2(225f, 105f), Green, "free");
            ModeButton(main, "Modèle guidé", "Suis la notice avec Brico", new Vector2(-225f, -45f), Blue, "guided");
            ModeButton(main, "Défi", "Reconstruis de mémoire", new Vector2(225f, -45f), Orange, "challenge");
            UIFactory.CreateButton(main, "Mes créations", new Vector2(-260f, -185f), new Vector2(300f, 80f), Grey, "creations", "", 32f);
            UIFactory.CreateButton(main, "Options", new Vector2(60f, -185f), new Vector2(260f, 80f), Grey, "options", "", 32f);
            gm.ResumeButton = UIFactory.CreateButton(main, "Reprendre", new Vector2(330f, -185f), new Vector2(240f, 80f), new Color(0.98f, 0.79f, 0.04f), "resume", "", 32f, new Color(0.12f, 0.12f, 0.16f));
            gm.MenuHint = UIFactory.CreateText(main, "", 26f, new Vector2(0f, -300f), new Vector2(900f, 60f), TextAlignmentOptions.Center, false, UIFactory.TextDim);
            gm.MainMenu = main.gameObject;

            // ---------- choix du modèle
            var select = Panel(root, "Choix du modèle", new Vector2(1060f, 840f));
            gm.ModelSelectTitle = UIFactory.CreateText(select, "Choisis ta construction", 52f, new Vector2(0f, 330f), new Vector2(980f, 80f), TextAlignmentOptions.Center, true);
            var models = LegoCatalog.Models;
            gm.ModelCardInfo = new TextMeshProUGUI[models.Length];
            gm.ModelCardRecord = new TextMeshProUGUI[models.Length];
            gm.ModelCardStars = new Image[models.Length * 3];
            for (int i = 0; i < models.Length; i++)
            {
                var m = models[i];
                var pos = new Vector2((i % 3 - 1) * 335f, 160f - (i / 3) * 255f);
                var card = UIFactory.CreateButton(select, "", pos, new Vector2(315f, 235f), DifficultyColor(m.Difficulty), "model", m.Id).GetComponent<RectTransform>();
                card.name = "Carte " + m.Name;
                UIFactory.CreateText(card, m.Name, 40f, new Vector2(0f, 72f), new Vector2(300f, 56f), TextAlignmentOptions.Center, true);
                gm.ModelCardInfo[i] = UIFactory.CreateText(card, "", 24f, new Vector2(0f, 30f), new Vector2(300f, 36f), TextAlignmentOptions.Center, false, new Color(1f, 1f, 1f, 0.9f));
                for (int s = 0; s < 3; s++)
                    gm.ModelCardStars[i * 3 + s] = UIFactory.CreateImage(card, "Étoile", new Vector2((s - 1) * 52f, -22f), new Vector2(46f, 46f), GameManager.StarOff, star);
                gm.ModelCardRecord[i] = UIFactory.CreateText(card, "", 22f, new Vector2(0f, -78f), new Vector2(300f, 40f), TextAlignmentOptions.Center, false, new Color(1f, 1f, 1f, 0.9f));
            }
            UIFactory.CreateText(select, "<color=#5CD17A>Facile</color>   ·   <color=#FFB347>Moyen</color>   ·   <color=#FF6B6B>Difficile</color>", 26f, new Vector2(-150f, -330f), new Vector2(600f, 50f));
            UIFactory.CreateButton(select, "Retour", new Vector2(330f, -330f), new Vector2(240f, 72f), Grey, "back", "", 30f);
            gm.ModelSelect = select.gameObject;

            // ---------- mes créations
            var creations = Panel(root, "Mes créations", new Vector2(980f, 720f));
            UIFactory.CreateText(creations, "Mes créations", 56f, new Vector2(0f, 270f), new Vector2(900f, 80f), TextAlignmentOptions.Center, true);
            gm.CreationsHint = UIFactory.CreateText(creations, "", 26f, new Vector2(0f, 205f), new Vector2(880f, 50f), TextAlignmentOptions.Center, false, UIFactory.TextDim);
            gm.SlotLabels = new TextMeshProUGUI[SaveSystem.CreationSlots];
            gm.SlotSaveButtons = new GameObject[SaveSystem.CreationSlots];
            gm.SlotLoadButtons = new GameObject[SaveSystem.CreationSlots];
            for (int i = 0; i < SaveSystem.CreationSlots; i++)
            {
                float y = 100f - i * 125f;
                UIFactory.CreateImage(creations, "Ligne", new Vector2(0f, y), new Vector2(880f, 110f), UIFactory.CardColor);
                gm.SlotLabels[i] = UIFactory.CreateText(creations, "", 30f, new Vector2(-200f, y), new Vector2(440f, 100f), TextAlignmentOptions.Left);
                gm.SlotLoadButtons[i] = UIFactory.CreateButton(creations, "Charger", new Vector2(150f, y), new Vector2(190f, 72f), Blue, "load", i.ToString(), 30f);
                gm.SlotSaveButtons[i] = UIFactory.CreateButton(creations, "Enregistrer", new Vector2(345f, y), new Vector2(190f, 72f), Green, "save", i.ToString(), 28f);
            }
            UIFactory.CreateButton(creations, "Retour", new Vector2(0f, -285f), new Vector2(260f, 72f), Grey, "back", "", 30f);
            gm.CreationsPanel = creations.gameObject;

            // ---------- options
            var options = Panel(root, "Options", new Vector2(980f, 760f));
            UIFactory.CreateText(options, "Options", 56f, new Vector2(0f, 290f), new Vector2(900f, 80f), TextAlignmentOptions.Center, true);
            gm.MusicValue = VolumeRow(options, "Musique", 190f, "music");
            gm.EffectsValue = VolumeRow(options, "Effets sonores", 90f, "sfx");
            UIFactory.CreateImage(options, "Commandes", new Vector2(0f, -110f), new Vector2(880f, 270f), UIFactory.CardColor);
            UIFactory.CreateText(options, "<b>Commandes</b>\n" +
                "<b>Gâchette</b> : prendre, tenir et poser une brique · appuyer sur les boutons\n" +
                "<b>Poignet</b> : tourner la brique   ·   <b>Bouton A</b> : appeler Brico\n" +
                "<b>Joystick gauche</b> : marcher   ·   <b>Joystick droit</b> : tourner / se téléporter\n" +
                "<b>Bouton Menu</b> (manette gauche) : ouvrir ce menu",
                26f, new Vector2(0f, -110f), new Vector2(840f, 250f), TextAlignmentOptions.Center, false, new Color(1f, 1f, 1f, 0.9f));
            UIFactory.CreateButton(options, "Retour", new Vector2(0f, -305f), new Vector2(260f, 72f), Grey, "back", "", 30f);
            gm.OptionsPanel = options.gameObject;

            // ---------- bilan
            var end = Panel(root, "Bilan", new Vector2(980f, 760f));
            gm.EndTitle = UIFactory.CreateText(end, "Bravo !", 62f, new Vector2(0f, 270f), new Vector2(900f, 90f), TextAlignmentOptions.Center, true);
            gm.EndStars = new Image[3];
            for (int s = 0; s < 3; s++)
                gm.EndStars[s] = UIFactory.CreateImage(end, "Étoile", new Vector2((s - 1) * 120f, 150f), new Vector2(105f, 105f), GameManager.StarOff, star);
            gm.EndBody = UIFactory.CreateText(end, "", 34f, new Vector2(0f, 10f), new Vector2(880f, 150f));
            gm.EndRecord = UIFactory.CreateText(end, "", 32f, new Vector2(0f, -100f), new Vector2(880f, 50f), TextAlignmentOptions.Center, true, new Color(1f, 0.82f, 0.2f));
            gm.EndReplayButton = UIFactory.CreateButton(end, "Rejouer", new Vector2(-300f, -230f), new Vector2(260f, 84f), Blue, "replay", "", 32f);
            gm.EndNextButton = UIFactory.CreateButton(end, "Modèle suivant", new Vector2(0f, -230f), new Vector2(300f, 84f), Green, "next", "", 30f);
            gm.EndFreeButton = UIFactory.CreateButton(end, "Construction libre", new Vector2(-150f, -230f), new Vector2(360f, 84f), Green, "free", "", 30f);
            UIFactory.CreateButton(end, "Menu", new Vector2(300f, -230f), new Vector2(240f, 84f), Grey, "menu", "", 32f);
            gm.EndPanel = end.gameObject;

            foreach (var p in new[] { select, creations, options, end }) p.gameObject.SetActive(false);
        }

        static void ModeButton(RectTransform parent, string title, string subtitle, Vector2 pos, Color color, string action)
        {
            var b = UIFactory.CreateButton(parent, "", pos, new Vector2(430f, 130f), color, action).GetComponent<RectTransform>();
            b.name = "Bouton " + title;
            UIFactory.CreateText(b, title, 40f, new Vector2(0f, 20f), new Vector2(410f, 56f), TextAlignmentOptions.Center, true);
            UIFactory.CreateText(b, subtitle, 24f, new Vector2(0f, -28f), new Vector2(410f, 36f), TextAlignmentOptions.Center, false, new Color(1f, 1f, 1f, 0.85f));
        }

        static TextMeshProUGUI VolumeRow(RectTransform parent, string label, float y, string action)
        {
            UIFactory.CreateImage(parent, "Ligne", new Vector2(0f, y), new Vector2(880f, 86f), UIFactory.CardColor);
            UIFactory.CreateText(parent, label, 34f, new Vector2(-230f, y), new Vector2(380f, 60f), TextAlignmentOptions.Left, true);
            UIFactory.CreateButton(parent, "–", new Vector2(120f, y), new Vector2(80f, 64f), Grey, action + "-", "", 40f);
            var value = UIFactory.CreateText(parent, "", 34f, new Vector2(240f, y), new Vector2(140f, 60f));
            UIFactory.CreateButton(parent, "+", new Vector2(360f, y), new Vector2(80f, 64f), Grey, action + "+", "", 40f);
            return value;
        }

        static Color DifficultyColor(Difficulty d) => d == Difficulty.Facile ? new Color(0.17f, 0.58f, 0.33f) : d == Difficulty.Moyen ? new Color(0.88f, 0.5f, 0.1f) : new Color(0.8f, 0.22f, 0.22f);

        // ================================================================== bandeau d'infos

        static void BuildHud(Transform root, GameManager gm)
        {
            var hud = UIFactory.CreateCanvas("Bandeau d'infos", root, new Vector2(1000f, 190f), new Vector3(0f, 1.2f, 0.47f), Quaternion.Euler(-5f, 0f, 0f), 0.0008f, UIFactory.PanelColor);
            gm.HudTitle = UIFactory.CreateText(hud, "", 36f, new Vector2(-235f, 45f), new Vector2(500f, 50f), TextAlignmentOptions.Left, true);
            gm.HudInfo = UIFactory.CreateText(hud, "", 26f, new Vector2(-235f, -25f), new Vector2(500f, 100f), TextAlignmentOptions.TopLeft, false, new Color(1f, 1f, 1f, 0.88f));

            var bs = new Vector2(150f, 66f);
            UIFactory.CreateButton(hud, "Aide (A)", new Vector2(250f, 42f), bs, Purple, "help", "", 26f);
            UIFactory.CreateButton(hud, "Menu", new Vector2(410f, 42f), bs, Grey, "menu", "", 26f);

            var free = Group("Boutons construction libre", hud, Vector3.zero);
            gm.HudFreeButtons = free.gameObject;
            var freeRt = free.gameObject.AddComponent<RectTransform>();
            freeRt.sizeDelta = Vector2.zero;
            UIFactory.CreateButton(freeRt, "Annuler", new Vector2(90f, -42f), bs, Blue, "undo", "", 26f);
            UIFactory.CreateButton(freeRt, "Tout effacer", new Vector2(250f, -42f), bs, Red, "clear", "", 24f);
            UIFactory.CreateButton(freeRt, "Sauvegarder", new Vector2(410f, -42f), bs, Green, "creations", "", 24f);

            var challenge = Group("Boutons défi", hud, Vector3.zero);
            gm.HudChallengeButtons = challenge.gameObject;
            var chRt = challenge.gameObject.AddComponent<RectTransform>();
            chRt.sizeDelta = Vector2.zero;
            UIFactory.CreateButton(chRt, "J'ai fini !", new Vector2(330f, -42f), new Vector2(310f, 66f), Orange, "finish", "", 28f);

            var tuto = Group("Boutons tutoriel", hud, Vector3.zero);
            gm.HudTutorialButtons = tuto.gameObject;
            var tuRt = tuto.gameObject.AddComponent<RectTransform>();
            tuRt.sizeDelta = Vector2.zero;
            UIFactory.CreateButton(tuRt, "Passer le tutoriel", new Vector2(330f, -42f), new Vector2(310f, 66f), Grey, "skip", "", 26f);

            gm.Hud = hud.gameObject;
            hud.gameObject.SetActive(false);
        }
    }
}
