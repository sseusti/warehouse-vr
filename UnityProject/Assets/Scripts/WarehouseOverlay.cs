using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Warehouse
{
    sealed class WarehouseOverlay : WorldPanel
    {
        public enum Hit { None, Panel, ToggleHints, Restart, Menu }

        static readonly Color ButtonColor = new(0.13f, 0.27f, 0.36f, 1f);
        static readonly Color HoverColor = new(0.20f, 0.42f, 0.52f, 1f);
        static readonly Vector2 CardSize = new(620f, 230f);
        static readonly Vector3 PanelOffset = new(-0.43f, 0.40f, 1.45f);
        static readonly Vector3 WristOffset = new(0.04f, 0.055f, -0.067f);
        const float ButtonHeight = 48f;
        const float ButtonRight = 290f;

        readonly Image blackout;
        readonly RectTransform panel;
        readonly RectTransform card;
        readonly Image hintButton;
        readonly Image restartButton;
        readonly Text title;
        readonly Text instructions;
        readonly Text hintLabel;
        readonly Text restartLabel;
        readonly RectTransform modePanel;
        readonly Text modeLabel;
        bool hasMenu;
        readonly InputAction shortcut;
        Vector3 heading = Vector3.forward;
        Vector3 offset = PanelOffset;
        bool showMode = true;

        public WarehouseOverlay(Camera camera, Transform parent)
        {
            panel = CreateCanvas("Route Overlay", camera, parent, CardSize, 20);
            panel.localScale = Vector3.one * 0.0012f;

            var curtain = CreateCanvas("Vent Transition", camera, camera.transform, Vector2.one * 4f, 100);
            curtain.localPosition = Vector3.forward * 0.2f;
            blackout = AddImage("Blackout", curtain, curtain.sizeDelta, Vector2.zero, Color.black);
            Fade(0f);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            card = AddImage("Hint Card", panel, CardSize, Vector2.zero,
                new Color(0.06f, 0.11f, 0.15f, 0.97f)).rectTransform;
            title = AddText("Title", card, font, 34, new Vector2(564f, 46f), new Vector2(0f, 69f),
                new Color(0.76f, 0.92f, 1f), TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            instructions = AddText("Instructions", card, font, 28, new Vector2(564f, 96f), new Vector2(0f, -3f),
                Color.white, TextAnchor.UpperLeft);
            hintButton = AddImage("Toggle Hints", panel, new Vector2(174f, ButtonHeight), Vector2.zero, ButtonColor);
            hintLabel = AddText("Button Label", hintButton.rectTransform, font, 24,
                hintButton.rectTransform.sizeDelta, Vector2.zero, Color.white, TextAnchor.MiddleCenter);
            Vector2 restartSize = new(218f, ButtonHeight);
            restartButton = AddImage("Restart Level", panel, restartSize, new Vector2(-181f, -80f), ButtonColor);
            restartLabel = AddText("Button Label", restartButton.rectTransform, font, 24,
                restartSize, Vector2.zero, Color.white, TextAnchor.MiddleCenter);
            restartLabel.text = "Начать заново";
            SetVisible(true);

            Vector2 modeSize = new(330f, 64f);
            modePanel = CreateCanvas("Controller Mode", camera, parent, modeSize, 15);
            AddImage("Mode Background", modePanel, modeSize, Vector2.zero, new Color(0.06f, 0.11f, 0.15f, 0.95f));
            modeLabel = AddText("Mode Label", modePanel, font, 32, new Vector2(300f, 54f),
                Vector2.zero, Color.white, TextAnchor.MiddleCenter);
            modeLabel.resizeTextForBestFit = true;
            modeLabel.resizeTextMinSize = 21;
            modeLabel.resizeTextMaxSize = 32;
            modeLabel.fontStyle = FontStyle.Bold;

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

        public void ShowMode(string mode) => modeLabel.text = mode + " · A";

        public void Fade(float alpha)
        {
            blackout.enabled = alpha > 0f;
            blackout.color = new Color(0f, 0f, 0f, alpha);
        }

        public void Place(Transform camera, Transform leftController, float playerScale)
        {
            Vector3 forward = camera.forward;
            Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (flatForward.sqrMagnitude > 0.001f)
                heading = flatForward.normalized;

            float pitch = Mathf.Atan2(-forward.y, flatForward.magnitude) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.LookRotation(heading, Vector3.up) *
                Quaternion.Euler(Mathf.Clamp(pitch, -65f, 65f), 0f, 0f);
            panel.SetPositionAndRotation(camera.position + rotation * (offset * playerScale), rotation);

            panel.localScale = Vector3.one * (0.0012f * playerScale);

            Vector3 modePosition = leftController.TransformPoint(WristOffset);
            Vector3 eyeToMode = modePosition - camera.position;
            bool modeVisible = showMode && Vector3.Dot(eyeToMode, forward) > 0.22f * playerScale;
            modePanel.gameObject.SetActive(modeVisible);
            if (modeVisible)
            {
                modePanel.SetPositionAndRotation(modePosition, Quaternion.LookRotation(eyeToMode, camera.up));
                modePanel.localScale = Vector3.one * (0.0009f * Mathf.Min(eyeToMode.magnitude, 0.85f * playerScale));
            }
        }

        public Hit Intersect(Ray ray, out Vector3 point)
        {
            var plane = new Plane(panel.forward, panel.position);
            point = default;
            if (!plane.Raycast(ray, out float distance))
                return Hit.None;

            point = ray.GetPoint(distance);
            if (hintButton.gameObject.activeSelf && Contains(hintButton.rectTransform, point))
                return hasMenu ? Hit.Menu : Hit.ToggleHints;
            if (Contains(restartButton.rectTransform, point))
                return Hit.Restart;

            return card.gameObject.activeSelf && Contains(card, point)
                ? Hit.Panel
                : Hit.None;
        }

        static bool Contains(RectTransform bounds, Vector3 point) => bounds.rect.Contains(bounds.InverseTransformPoint(point));

        public void SetHovered(Hit hit)
        {
            hintButton.color = hit == Hit.ToggleHints || hit == Hit.Menu ? HoverColor : ButtonColor;
            restartButton.color = hit == Hit.Restart ? HoverColor : ButtonColor;
        }

        public void Toggle() => SetVisible(!card.gameObject.activeSelf);

        public void ShowCompletion(bool menuAvailable = false)
        {
            showMode = false;
            modePanel.gameObject.SetActive(false);
            hasMenu = menuAvailable;
            hintButton.gameObject.SetActive(hasMenu);
            offset = new Vector3(0f, 0.1f, 1.4f);
            card.sizeDelta = new Vector2(560f, 206f);
            card.gameObject.SetActive(true);
            title.text = "Уровень пройден!";
            title.alignment = TextAnchor.MiddleCenter;
            title.fontSize = 42;
            title.rectTransform.sizeDelta = new Vector2(520f, 58f);
            title.rectTransform.anchoredPosition = new Vector2(0f, 38f);
            instructions.gameObject.SetActive(false);
            SetButton(restartButton, restartLabel, new Vector2(250f, 58f), new Vector2(hasMenu ? -132f : 0f, -39f), "Пройти заново");
            if (hasMenu)
                SetButton(hintButton, hintLabel, new Vector2(250f, 58f), new Vector2(132f, -39f), "В главное меню");
        }

        void SetVisible(bool visible)
        {
            card.gameObject.SetActive(visible);
            float width = visible ? 174f : 325f;
            SetButton(hintButton, hintLabel, new Vector2(width, ButtonHeight), new Vector2(ButtonRight - width * 0.5f, -80f),
                visible ? "Скрыть · X" : "Показать подсказки · X");
        }

        static void SetButton(Image button, Text text, Vector2 size, Vector2 position, string label)
        {
            button.rectTransform.sizeDelta = size;
            button.rectTransform.anchoredPosition = position;
            text.rectTransform.sizeDelta = size;
            text.text = label;
        }

        public override void Dispose()
        {
            shortcut.Dispose();
            base.Dispose();
        }

    }
}
