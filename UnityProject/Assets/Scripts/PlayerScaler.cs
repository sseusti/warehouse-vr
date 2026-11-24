using System;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

namespace Warehouse
{
    /// <summary>
    /// Smoothly changes the size of the XR Origin in steps behind a darkened view.
    /// Teleport reach, near clip plane and ray widths follow the player scale.
    /// </summary>
    public sealed class PlayerScaler : MonoBehaviour
    {
        [SerializeField] private XROrigin origin;
        [SerializeField] private PlayerScaleSettings settings;
        [SerializeField] private XRRayInteractor teleportRay;
        [SerializeField] private Material selfTargetMaterial;

        const float SelfTargetRadius = 0.3f;
        const float SelfTargetLift = 0.02f;
        const float FadeDistance = 0.2f;
        static readonly int ColorProperty = Shader.PropertyToID("_Color");
        static readonly Color SelfTargetColor = new(0.94f, 0.57f, 0.18f, 1f);
        static readonly Color SelfTargetHighlight = new(1f, 0.86f, 0.40f, 1f);

        int stepIndex;
        float scale = 1f;
        float startScale;
        float targetScale;
        float elapsed = -1f;
        Vector3 pivot;
        Vector3 startOrigin;

        Camera view;
        float baseNearClip;
        float baseRayVelocity;
        XRInteractorLineVisual teleportLine;
        float baseLineWidth;
        Image fade;
        Material fadeMaterial;
        Transform selfTarget;
        Renderer selfTargetRenderer;
        MaterialPropertyBlock selfTargetProperties;
        bool selfTargetHighlighted = true;

        /// <summary>Raised when a size change has finished.</summary>
        public event Action Changed;

        public float Scale => scale;
        public bool IsChanging => elapsed >= 0f;
        public bool IsNormalSize => stepIndex == 0 && !IsChanging;
        public bool FitsVent => !IsChanging && scale <= settings.VentMaxScale + 0.001f;

        /// <summary>Player floor point right under the head.</summary>
        public Vector3 Floor
        {
            get
            {
                Vector3 floor = view.transform.position;
                floor.y = origin.Origin.transform.position.y;
                return floor;
            }
        }

        float SelfRadius => SelfTargetRadius * Mathf.Sqrt(scale);

        public void Configure(XROrigin player, PlayerScaleSettings values, XRRayInteractor ray, Material targetMaterial)
        {
            origin = player;
            settings = values;
            teleportRay = ray;
            selfTargetMaterial = targetMaterial;
        }

        void Awake()
        {
            view = origin.Camera;
            baseNearClip = view.nearClipPlane;
            if (teleportRay != null)
            {
                baseRayVelocity = teleportRay.velocity;
                teleportLine = teleportRay.GetComponent<XRInteractorLineVisual>();
                if (teleportLine != null)
                    baseLineWidth = teleportLine.lineWidth;
            }

            CreateFade();
            CreateSelfTarget();
        }

        void OnDestroy()
        {
            if (fadeMaterial != null)
                Destroy(fadeMaterial);
        }

        /// <summary>Starts the change to the next size step, wrapping back to normal size.</summary>
        public void ChangeToNextStep()
        {
            if (IsChanging)
                return;

            stepIndex = (stepIndex + 1) % settings.StepCount;
            startScale = scale;
            targetScale = settings.Step(stepIndex);
            pivot = Floor;
            startOrigin = origin.transform.position;
            elapsed = 0f;
            if (teleportRay != null)
                teleportRay.enabled = false;
        }

        /// <summary>Checks whether the ray points at the ring under the player's feet.</summary>
        public bool RayHitsSelf(Ray ray, out float distance)
        {
            var plane = new Plane(Vector3.up, Floor + Vector3.up * SelfTargetLift);
            if (!plane.Raycast(ray, out distance))
                return false;

            Vector3 offset = ray.GetPoint(distance) - Floor;
            offset.y = 0f;
            return offset.sqrMagnitude <= SelfRadius * SelfRadius;
        }

        /// <summary>Shows the ring under the player's feet; call every frame after moving.</summary>
        public void ShowSelfTarget(bool visible, bool highlighted)
        {
            selfTarget.gameObject.SetActive(visible);
            if (!visible)
                return;

            float radius = SelfRadius;
            selfTarget.position = Floor + Vector3.up * SelfTargetLift;
            selfTarget.localScale = new Vector3(radius * 2f, 0.002f, radius * 2f);
            if (highlighted == selfTargetHighlighted)
                return;

            selfTargetHighlighted = highlighted;
            selfTargetProperties.SetColor(ColorProperty, highlighted ? SelfTargetHighlight : SelfTargetColor);
            selfTargetRenderer.SetPropertyBlock(selfTargetProperties);
        }

        void Update()
        {
            if (!IsChanging)
                return;

            float fadeTime = settings.FadeDuration;
            float scaleTime = settings.ScaleDuration;
            elapsed += Time.deltaTime;

            float darkness = elapsed < fadeTime
                ? elapsed / fadeTime
                : 1f - Mathf.Clamp01((elapsed - fadeTime - scaleTime) / fadeTime);
            SetFade(darkness * settings.FadeAlpha);

            float progress = Mathf.Clamp01((elapsed - fadeTime) / scaleTime);
            ApplyScale(Mathf.Lerp(startScale, targetScale, Mathf.SmoothStep(0f, 1f, progress)));

            if (elapsed < fadeTime * 2f + scaleTime)
                return;

            elapsed = -1f;
            SetFade(0f);
            if (teleportRay != null)
                teleportRay.enabled = true;
            Debug.Log($"Warehouse player scale {scale:0.##}.");
            Changed?.Invoke();
        }

        void ApplyScale(float value)
        {
            scale = value;
            origin.transform.localScale = Vector3.one * value;
            origin.transform.position = pivot + (startOrigin - pivot) * (value / startScale);
            view.nearClipPlane = baseNearClip * value;
            if (teleportRay != null)
                teleportRay.velocity = baseRayVelocity * Mathf.Sqrt(value);
            if (teleportLine != null)
                teleportLine.lineWidth = baseLineWidth * value;
        }

        void CreateFade()
        {
            var canvas = new GameObject("Scale Fade", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = view;
            canvas.sortingOrder = 10;
            var rect = (RectTransform)canvas.transform;
            rect.SetParent(view.transform, false);
            rect.localPosition = Vector3.forward * FadeDistance;
            rect.localRotation = Quaternion.identity;
            rect.sizeDelta = new Vector2(100f, 100f);
            rect.localScale = Vector3.one * 0.02f;

            fadeMaterial = new Material(Resources.Load<Shader>("WarehouseHintOverlay"));
            fade = new GameObject("Darkness", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            fade.rectTransform.SetParent(rect, false);
            fade.rectTransform.sizeDelta = rect.sizeDelta;
            fade.material = fadeMaterial;
            fade.raycastTarget = false;
            SetFade(0f);
        }

        void SetFade(float alpha)
        {
            fade.color = new Color(0f, 0f, 0f, alpha);
            fade.enabled = alpha > 0.001f;
        }

        void CreateSelfTarget()
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Self Target";
            Destroy(ring.GetComponent<Collider>());
            ring.transform.SetParent(transform, false);
            selfTarget = ring.transform;
            selfTargetRenderer = ring.GetComponent<Renderer>();
            if (selfTargetMaterial != null)
                selfTargetRenderer.sharedMaterial = selfTargetMaterial;
            selfTargetProperties = new MaterialPropertyBlock();
            ShowSelfTarget(false, false);
        }
    }
}
