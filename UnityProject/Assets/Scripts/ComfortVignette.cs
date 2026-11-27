using System;
using UnityEngine;
using UnityEngine.UI;

namespace Warehouse
{
    /// <summary>
    /// Dark edges around the view (a "tunnel") that narrow peripheral motion while the player is carried.
    /// The centre stays clear, so the player still sees where they are going.
    /// </summary>
    public sealed class ComfortVignette : IDisposable
    {
        const float Distance = 0.2f;
        const int TextureSize = 128;
        // Radius of the clear centre and of the fully dark ring, as a fraction of the quad width.
        const float ClearRadius = 0.17f;
        const float DarkRadius = 0.3f;

        readonly RawImage image;
        readonly Material material;
        readonly Texture2D texture;

        public ComfortVignette(Camera view, string name)
        {
            var canvas = new GameObject(name, typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = view;
            canvas.sortingOrder = 9;
            var rect = (RectTransform)canvas.transform;
            rect.SetParent(view.transform, false);
            rect.localPosition = Vector3.forward * Distance;
            rect.localRotation = Quaternion.identity;
            rect.sizeDelta = new Vector2(60f, 60f);
            rect.localScale = Vector3.one * 0.02f;

            texture = CreateTexture();
            material = new Material(Resources.Load<Shader>("WarehouseHintOverlay"));
            image = new GameObject("Vignette", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            image.rectTransform.SetParent(rect, false);
            image.rectTransform.sizeDelta = rect.sizeDelta;
            image.texture = texture;
            image.material = material;
            image.raycastTarget = false;
            SetStrength(0f);
        }

        /// <summary>0 hides the vignette, 1 shows the full darkness ring.</summary>
        public void SetStrength(float value)
        {
            image.color = new Color(0f, 0f, 0f, value);
            image.enabled = value > 0.001f;
        }

        public void Dispose()
        {
            if (material != null)
                UnityEngine.Object.Destroy(material);
            if (texture != null)
                UnityEngine.Object.Destroy(texture);
        }

        static Texture2D CreateTexture()
        {
            var result = new Texture2D(TextureSize, TextureSize, TextureFormat.Alpha8, false)
            {
                name = "Comfort Vignette",
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float u = (x + 0.5f) / TextureSize - 0.5f;
                    float v = (y + 0.5f) / TextureSize - 0.5f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ClearRadius, DarkRadius, r));
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            result.SetPixels32(pixels);
            result.Apply(false, true);
            return result;
        }
    }
}
