using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ConstructVR
{
    /// <summary>Création de l'interface 3D (panneaux pointables avec la manette). Utilisé par le constructeur de scène.</summary>
    public static class UIFactory
    {
        public static TMP_FontAsset Font;
        public static Sprite Rounded;

        public static readonly Color PanelColor = new Color(0.09f, 0.12f, 0.20f, 0.96f);
        public static readonly Color CardColor = new Color(0.16f, 0.20f, 0.31f, 1f);
        public static readonly Color TextDim = new Color(1f, 1f, 1f, 0.7f);

        /// <summary>Canvas dans le monde, utilisable avec le rayon des manettes.</summary>
        public static RectTransform CreateCanvas(string name, Transform parent, Vector2 sizePx, Vector3 localPos, Quaternion localRot, float pixelSize = 0.001f, Color? background = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 3f;
            go.AddComponent<TrackedDeviceGraphicRaycaster>();
            go.AddComponent<GraphicRaycaster>();
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = sizePx;
            rt.localPosition = localPos;
            rt.localRotation = localRot;
            rt.localScale = Vector3.one * pixelSize;
            if (background.HasValue)
            {
                var bg = go.AddComponent<Image>();
                bg.sprite = Rounded;
                bg.type = Image.Type.Sliced;
                bg.pixelsPerUnitMultiplier = 0.5f;
                bg.color = background.Value;
            }
            return rt;
        }

        public static Image CreateImage(RectTransform parent, string name, Vector2 pos, Vector2 size, Color color, Sprite sprite = null, bool sliced = true)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.sprite = sprite ?? Rounded;
            img.type = sprite == null && sliced ? Image.Type.Sliced : Image.Type.Simple;
            img.preserveAspect = sprite != null;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static TextMeshProUGUI CreateText(RectTransform parent, string text, float size, Vector2 pos, Vector2 box,
            TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = false, Color? color = null)
        {
            var go = new GameObject("Texte", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (Font != null) tmp.font = Font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            tmp.color = color ?? Color.white;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = box;
            rt.anchoredPosition = pos;
            return tmp;
        }

        /// <summary>Bouton arrondi coloré, relié à une action du jeu.</summary>
        public static GameObject CreateButton(RectTransform parent, string label, Vector2 pos, Vector2 size, Color color,
            string action, string argument = "", float fontSize = 38f, Color? textColor = null)
        {
            var go = new GameObject("Bouton " + label, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 0.6f;
            img.color = color;
            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = new Color(0.88f, 0.88f, 0.88f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            colors.selectedColor = new Color(0.88f, 0.88f, 0.88f);
            colors.colorMultiplier = 1.15f;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            var nav = btn.navigation;
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;
            var ba = go.AddComponent<ButtonAction>();
            ba.Action = action;
            ba.Argument = argument;
            CreateText(rt, label, fontSize, Vector2.zero, size - new Vector2(16f, 8f), TextAlignmentOptions.Center, true, textColor ?? Color.white);
            return go;
        }

        /// <summary>Texte posé directement dans le monde (sans fond). height = hauteur des lettres en mètres.</summary>
        public static TextMeshPro CreateLabel(Transform parent, string text, Vector3 localPos, float height, Color color, float width = 1f, bool bold = true)
        {
            var go = new GameObject("Texte " + text);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tmp = go.AddComponent<TextMeshPro>();
            if (Font != null) tmp.font = Font;
            tmp.text = text;
            tmp.fontSize = height * 10f;
            tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(width, height * 2f);
            return tmp;
        }
    }
}
