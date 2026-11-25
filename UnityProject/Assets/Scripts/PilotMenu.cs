using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Warehouse
{
    public sealed class PilotMenu : MonoBehaviour
    {
        public const string SceneName = "MainMenu";
        public Camera eye;
        public Transform hand;
        public InputActionReference activate;
        public LineRenderer pointer;
        public string room;
        public InteractionFeedback feedback;
        static readonly string[] Labels = { "Сюжет", "Песочница", "Тестовая комната", "Настройки" };
        static readonly Color Idle = new(0.13f, 0.26f, 0.32f);
        static readonly Color Selected = new(0.13f, 0.48f, 0.52f);

        readonly Image[] buttons = new Image[4];
        readonly Text[] captions = new Text[4];
        WorldPanel ui;
        RectTransform panel;
        Text title;
        bool placeholder;
        bool loading;

        void Awake()
        {
            ui = new WorldPanel();
            panel = WorldPanel.CreateCanvas("Главное меню", eye, eye.transform, new Vector2(700, 560), 20);
            panel.localScale = Vector3.one * .0016f;
            panel.localPosition = new Vector3(0, -.04f, 1.65f);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ui.AddImage("Background", panel, panel.sizeDelta, Vector2.zero, new Color(.055f, .09f, .12f));
            title = ui.AddText("Title", panel, font, 50, new Vector2(660, 100), new Vector2(0, 196),
                Color.white, TextAnchor.MiddleCenter);
            for (int i = 0; i < buttons.Length; i++)
            {
                var size = new Vector2(530, 78);
                buttons[i] = ui.AddImage(Labels[i], panel, size, new Vector2(0, 85 - i * 95), Idle);
                captions[i] = ui.AddText("Label", buttons[i].rectTransform, font, 34, size, Vector2.zero,
                    Color.white, TextAnchor.MiddleCenter);
            }
            ShowHome();
            pointer.enabled = true;
        }

        void ShowHome()
        {
            placeholder = false;
            title.text = "Склад";
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].gameObject.SetActive(true);
                captions[i].text = Labels[i];
            }
        }

        void LateUpdate()
        {
            if (loading) return;
            var ray = new Ray(hand.position, hand.forward);
            var plane = new Plane(panel.forward, panel.position);
            bool intersects = plane.Raycast(ray, out float distance);
            var end = ray.GetPoint(intersects ? distance : 2f);
            pointer.SetPosition(0, ray.origin);
            pointer.SetPosition(1, end);
            for (int i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                if (!button.gameObject.activeSelf) continue;
                bool hovered = intersects && button.rectTransform.rect.Contains(button.rectTransform.InverseTransformPoint(end));
                button.color = hovered ? Selected : Idle;
                if (!hovered || !activate.action.WasPressedThisFrame()) continue;
                feedback.Click();
                if (placeholder) ShowHome();
                else if (i == 2)
                {
                    loading = true;
                    SceneManager.LoadSceneAsync(room);
                }
                else
                {
                    placeholder = true;
                    title.text = Labels[i] + " · в разработке";
                    captions[0].text = "Назад";
                    for (int j = 1; j < buttons.Length; j++) buttons[j].gameObject.SetActive(false);
                }
                break;
            }
        }

        void OnDestroy() => ui?.Dispose();
    }
}
