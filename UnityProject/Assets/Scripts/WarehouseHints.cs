using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Warehouse
{
    sealed class WarehouseHints : IDisposable
    {
        public enum Hit { None, Panel, Button }

        static readonly Color ButtonColor = new(0.13f, 0.27f, 0.36f, 1f);
        static readonly Color HoverColor = new(0.20f, 0.42f, 0.52f, 1f);
        static readonly Vector2 CardSize = new(620f, 230f);
        static readonly Vector3 PanelOffset = new(-0.15f, 0.27f, 1.4f);
        const float ButtonHeight = 48f;
        const float ButtonRight = 290f;

        readonly RectTransform panel;
        readonly RectTransform card;
        readonly Image button;
        readonly Text title;
        readonly Text instructions;
        readonly Text buttonLabel;
        readonly Material overlay;
        readonly InputAction shortcut;
        Vector3 heading = Vector3.forward;
        Vector3 offset = PanelOffset;

        public WarehouseHints(Camera camera, Transform parent)
        {
            var canvas = new GameObject("Route Hints", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvas.sortingOrder = 20;

            panel = (RectTransform)canvas.transform;
            panel.SetParent(parent, false);
            panel.sizeDelta = CardSize;
            panel.localScale = Vector3.one * 0.0012f;

            overlay = new Material(Resources.Load<Shader>("WarehouseHintOverlay"));
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            card = AddImage("Hint Card", panel, CardSize, Vector2.zero,
                new Color(0.06f, 0.11f, 0.15f, 0.97f)).rectTransform;
            title = AddText("Title", card, font, 34, new Vector2(564f, 46f), new Vector2(0f, 69f),
                new Color(0.76f, 0.92f, 1f), TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            instructions = AddText("Instructions", card, font, 28, new Vector2(564f, 96f), new Vector2(0f, -3f),
                Color.white, TextAnchor.UpperLeft);
            button = AddImage("Toggle Hints", panel, new Vector2(174f, ButtonHeight), Vector2.zero, ButtonColor);
            buttonLabel = AddText("Button Label", button.rectTransform, font, 24,
                button.rectTransform.sizeDelta, Vector2.zero, Color.white, TextAnchor.MiddleCenter);
            SetVisible(true);

            shortcut = new InputAction("Toggle Hints", InputActionType.Button,
                "<XRController>{LeftHand}/{PrimaryButton}");
        }

        public void EnableShortcut() => shortcut.Enable();
        public void DisableShortcut() => shortcut.Disable();
        public bool ShortcutPressed => shortcut.WasPressedThisFrame();

        public void Show(string headingText, string instructionsText)
        {
            title.text = headingText;
            instructions.text = instructionsText;
        }

        public void Place(Transform camera)
        {
            Vector3 forward = camera.forward;
            Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (flatForward.sqrMagnitude > 0.001f)
                heading = flatForward.normalized;

            float pitch = Mathf.Atan2(-forward.y, flatForward.magnitude) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.LookRotation(heading, Vector3.up) *
                Quaternion.Euler(Mathf.Clamp(pitch, -65f, 65f), 0f, 0f);
            panel.SetPositionAndRotation(camera.position + rotation * offset, rotation);
        }

        public Hit Intersect(Ray ray, out Vector3 point)
        {
            var plane = new Plane(panel.forward, panel.position);
            point = default;
            if (!plane.Raycast(ray, out float distance))
                return Hit.None;

            point = ray.GetPoint(distance);
            var buttonRect = button.rectTransform;
            if (buttonRect.rect.Contains(buttonRect.InverseTransformPoint(point)))
                return Hit.Button;

            return card.gameObject.activeSelf && card.rect.Contains(card.InverseTransformPoint(point))
                ? Hit.Panel
                : Hit.None;
        }

        public void SetHovered(bool hovered) => button.color = hovered ? HoverColor : ButtonColor;

        public void Toggle() => SetVisible(!card.gameObject.activeSelf);

        public void ShowCompletion()
        {
            offset = new Vector3(0f, 0.1f, 1.4f);
            card.sizeDelta = new Vector2(560f, 206f);
            card.gameObject.SetActive(true);
            title.text = "Уровень пройден!";
            title.alignment = TextAnchor.MiddleCenter;
            title.fontSize = 42;
            title.rectTransform.sizeDelta = new Vector2(520f, 58f);
            title.rectTransform.anchoredPosition = new Vector2(0f, 38f);
            instructions.gameObject.SetActive(false);
            SetButton(new Vector2(304f, 58f), new Vector2(0f, -39f), "Пройти заново");
        }

        void SetVisible(bool visible)
        {
            card.gameObject.SetActive(visible);
            float width = visible ? 174f : 325f;
            SetButton(new Vector2(width, ButtonHeight), new Vector2(ButtonRight - width * 0.5f, -80f),
                visible ? "Скрыть · X" : "Показать подсказки · X");
        }

        void SetButton(Vector2 size, Vector2 position, string label)
        {
            button.rectTransform.sizeDelta = size;
            button.rectTransform.anchoredPosition = position;
            buttonLabel.rectTransform.sizeDelta = size;
            buttonLabel.text = label;
        }

        public void Dispose()
        {
            shortcut.Dispose();
            UnityEngine.Object.Destroy(overlay);
        }

        Image AddImage(string name, RectTransform parent, Vector2 size, Vector2 position, Color color)
        {
            var image = AddGraphic<Image>(name, parent, size, position);
            image.color = color;
            return image;
        }

        Text AddText(string name, RectTransform parent, Font font, int size, Vector2 bounds,
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
