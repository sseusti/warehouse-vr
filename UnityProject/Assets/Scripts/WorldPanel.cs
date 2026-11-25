using UnityEngine;
using UnityEngine.UI;

namespace Warehouse
{
    class WorldPanel : System.IDisposable
    {
        readonly Material overlay = new Material(Resources.Load<Shader>("WarehouseHintOverlay"));
        public virtual void Dispose() => Object.Destroy(overlay);

        public static RectTransform CreateCanvas(string name, Camera camera, Transform parent, Vector2 size, int order)
        {
            var canvas = new GameObject(name, typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvas.sortingOrder = order;
            var rect = (RectTransform)canvas.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            return rect;
        }

        public Image AddImage(string name, RectTransform parent, Vector2 size, Vector2 position, Color color)
        {
            var image = AddGraphic<Image>(name, parent, size, position);
            image.color = color;
            return image;
        }

        public Text AddText(string name, RectTransform parent, Font font, int size, Vector2 bounds,
            Vector2 position, Color color, TextAnchor alignment)
        {
            var text = AddGraphic<Text>(name, parent, bounds, position);
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            return text;
        }

        T AddGraphic<T>(string name, RectTransform parent, Vector2 size, Vector2 position) where T : Graphic
        {
            var graphic = new GameObject(name, typeof(RectTransform), typeof(T)).GetComponent<T>();
            graphic.rectTransform.SetParent(parent, false);
            graphic.rectTransform.sizeDelta = size;
            graphic.rectTransform.anchoredPosition = position;
            graphic.material = overlay;
            graphic.raycastTarget = false;
            return graphic;
        }
    }
}
