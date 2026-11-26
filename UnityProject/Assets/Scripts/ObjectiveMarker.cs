using TMPro;
using UnityEngine;

namespace Warehouse
{
    /// <summary>
    /// Floating "go here" marker: a chevron pointing down at the target and a short label above it.
    /// It hangs over the top of the target collider, turns to the player, keeps the same angular size
    /// at any distance and player scale, and fades in and out. The room decides when it is shown.
    /// </summary>
    public sealed class ObjectiveMarker : MonoBehaviour
    {
        [SerializeField] private MarkerSettings settings;
        [SerializeField] private Collider target;
        [SerializeField] private Transform view;
        [SerializeField] private Transform arrow;
        [SerializeField] private TextMeshPro label;
        [Tooltip("Extra height above the target, metres: clears handles, frames and labels on it.")]
        [SerializeField, Min(0f)] private float lift;
        [Tooltip("Shift towards the player, metres: keeps the marker in front of things hanging over the target.")]
        [SerializeField, Min(0f)] private float towardView;

        const float PlayerHeight = 1.7f;

        bool visible;
        float fade;
        float shownFade = -1f;

        public bool Visible => visible;

        public void Configure(MarkerSettings values, Collider aim, float extraHeight, float shift, Transform eyes,
            Transform chevron, TextMeshPro text)
        {
            lift = extraHeight;
            towardView = shift;
            settings = values;
            target = aim;
            view = eyes;
            arrow = chevron;
            label = text;
        }

        public void SetVisible(bool value) => visible = value;

        void Awake()
        {
            ApplyFade(0f);
        }

        void LateUpdate()
        {
            Bounds bounds = target.bounds;
            var top = new Vector3(bounds.center.x, bounds.max.y + lift, bounds.center.z);
            Vector3 eyes = view.position;
            Vector3 toEyes = eyes - top;
            toEyes.y = 0f;
            if (towardView > 0f && toEyes.sqrMagnitude > 0.0001f)
                top += toEyes.normalized * towardView;
            float distance = Vector3.Distance(eyes, top);
            float playerScale = view.lossyScale.y;
            bool near = distance < settings.HideWithin * PlayerHeight * playerScale;

            float goal = visible && !near ? 1f : 0f;
            fade = Mathf.MoveTowards(fade, goal, Time.deltaTime / settings.FadeDuration);
            ApplyFade(fade);
            if (fade <= 0f)
                return;

            Vector2 range = settings.SizeRange * Mathf.Max(playerScale, 0.1f);
            float size = Mathf.Clamp(distance * settings.SizePerMetre, range.x, range.y);
            float bob = (0.5f + 0.5f * Mathf.Sin(Time.time * settings.BobFrequency * 2f * Mathf.PI)) * settings.BobHeight;
            transform.position = top + Vector3.up * (size * (settings.Gap + bob));

            Vector3 away = transform.position - eyes;
            away.y = 0f;
            if (away.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(away, Vector3.up);

            // Ease-out pop while fading in.
            float pop = 1f - (1f - fade) * (1f - fade);
            transform.localScale = Vector3.one * (size * Mathf.Lerp(0.6f, 1f, pop));
        }

        void ApplyFade(float value)
        {
            if (value == shownFade)
                return;

            shownFade = value;
            bool shown = value > 0f;
            if (arrow.gameObject.activeSelf != shown)
                arrow.gameObject.SetActive(shown);
            if (label.gameObject.activeSelf != shown)
                label.gameObject.SetActive(shown);
            label.alpha = value;
        }
    }
}
