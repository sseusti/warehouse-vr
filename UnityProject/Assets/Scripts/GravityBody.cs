using TMPro;
using UnityEngine;

namespace Warehouse
{
    /// <summary>
    /// Replaces the global gravity of one Rigidbody with a preset chosen by the remote.
    /// Only this object is affected; the player's gravity never changes.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class GravityBody : MonoBehaviour
    {
        [SerializeField] private GravityPresets presets;
        [Tooltip("Yaw of the label so it faces the player's viewpoint, degrees.")]
        [SerializeField] private float labelYaw;
        [Tooltip("Distance from the centre to the label on the face turned to the player, metres.")]
        [SerializeField, Min(0f)] private float labelDepth = 0.375f;

        static readonly int ColorProperty = Shader.PropertyToID("_Color");
        static readonly Color HighlightColor = new(1f, 0.86f, 0.40f);

        Rigidbody body;
        int presetIndex;
        Vector3 acceleration;
        TextMeshPro label;
        Renderer surface;
        MaterialPropertyBlock properties;
        bool highlighted;

        public bool IsLocked { get; private set; }
        public Rigidbody Body => body;
        public GravityPresets.Preset Current => presets[presetIndex];
        public int PresetIndex => presetIndex;

        public void Configure(GravityPresets values, float yaw)
        {
            presets = values;
            labelYaw = yaw;
        }

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.useGravity = false;
            surface = GetComponent<Renderer>();
            properties = new MaterialPropertyBlock();
            CreateLabel();
            Apply(0);
        }

        /// <summary>Switches to the next gravity preset, wrapping around.</summary>
        public void NextPreset()
        {
            if (!IsLocked)
                Apply((presetIndex + 1) % presets.Count);
        }

        /// <summary>Puts the body back at rest with an earlier preset, for example when undoing.</summary>
        public void Restore(int index, Vector3 position)
        {
            if (IsLocked)
                return;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = position;
            transform.position = position;
            Apply(index);
        }

        /// <summary>Fixes the body in place, for example inside a socket.</summary>
        public void Lock(Vector3 position)
        {
            IsLocked = true;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.position = position;
            transform.position = position;
            label.text = "Зафиксирован";
            Debug.Log($"Warehouse {name} locked at {position}.");
        }

        public void SetHighlighted(bool value)
        {
            if (value == highlighted)
                return;

            highlighted = value;
            properties.Clear();
            if (highlighted)
                properties.SetColor(ColorProperty, HighlightColor);
            surface.SetPropertyBlock(properties);
        }

        void FixedUpdate()
        {
            if (!IsLocked)
                body.AddForce(acceleration, ForceMode.Acceleration);
        }

        // Stencilled on the face of the body that looks at the player.
        Vector3 LabelPosition => transform.position + label.transform.rotation * Vector3.back * labelDepth;

        void LateUpdate()
        {
            label.transform.position = LabelPosition;
        }

        void Apply(int index)
        {
            presetIndex = index;
            var preset = presets[index];
            acceleration = preset.Vector;
            body.WakeUp();

            float weight = body.mass * preset.acceleration;
            label.text = $"{preset.label}\n{preset.acceleration:0.##} м/с²\nвес {weight:0} Н";
            Debug.Log($"Warehouse {name} gravity {preset.label} {acceleration}.");
        }

        void CreateLabel()
        {
            // TextMeshPro picks the project default font (Noto Sans with Cyrillic and an outline).
            label = new GameObject(name + " Gravity Label").AddComponent<TextMeshPro>();
            label.transform.rotation = Quaternion.Euler(0f, labelYaw, 0f);
            label.transform.position = LabelPosition;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = new Vector2(3f, 1f);
            label.fontSize = 0.95f;
            label.color = new Color(1f, 0.97f, 0.88f);
        }
    }
}
