using System;
using UnityEngine;
using UnityEngine.UI;

namespace Warehouse
{
    /// <summary>Black overlay right in front of the camera for comfortable transitions.</summary>
    public sealed class ViewFade : IDisposable
    {
        const float Distance = 0.2f;

        readonly Image image;
        readonly Material material;

        public ViewFade(Camera view, string name)
        {
            var canvas = new GameObject(name, typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = view;
            canvas.sortingOrder = 10;
            var rect = (RectTransform)canvas.transform;
            rect.SetParent(view.transform, false);
            rect.localPosition = Vector3.forward * Distance;
            rect.localRotation = Quaternion.identity;
            rect.sizeDelta = new Vector2(100f, 100f);
            rect.localScale = Vector3.one * 0.02f;

            material = new Material(Resources.Load<Shader>("WarehouseHintOverlay"));
            image = new GameObject("Darkness", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(rect, false);
            image.rectTransform.sizeDelta = rect.sizeDelta;
            image.material = material;
            image.raycastTarget = false;
            SetAlpha(0f);
        }

        public void SetAlpha(float alpha)
        {
            image.color = new Color(0f, 0f, 0f, alpha);
            image.enabled = alpha > 0.001f;
        }

        public void Dispose()
        {
            if (material != null)
                UnityEngine.Object.Destroy(material);
        }
    }
}
