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
        [SerializeField, Min(0f)] private float labelHeight = 0.6f;

        static readonly int ColorProperty = Shader.PropertyToID("_Color");
        static readonly Color HighlightColor = new(1f, 0.86f, 0.40f);

        Rigidbody body;
        int presetIndex;
        Vector3 acceleration;
        TextMesh label;
        Renderer surface;
        MaterialPropertyBlock properties;
        bool highlighted;

        public bool IsLocked { get; private set; }
        public Rigidbody Body => body;
        public GravityPresets.Preset Current => presets[presetIndex];

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

        void LateUpdate()
        {
            label.transform.position = transform.position + Vector3.up * labelHeight;
        }

        void Apply(int index)
        {
            presetIndex = index;
            var preset = presets[index];
            acceleration = preset.Vector;
            body.WakeUp();

            float weight = body.mass * preset.acceleration;
            string direction = preset.upward ? " ↑" : "";
            label.text = $"{preset.label}{direction}\n{preset.acceleration:0.##} м/с² · вес {weight:0} Н";
            Debug.Log($"Warehouse {name} gravity {preset.label} {acceleration}.");
        }

        void CreateLabel()
        {
            label = new GameObject(name + " Gravity Label").AddComponent<TextMesh>();
            label.transform.SetPositionAndRotation(transform.position + Vector3.up * labelHeight,
                Quaternion.Euler(0f, labelYaw, 0f));
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 48;
            label.characterSize = 0.028f;
            label.color = new Color(0.96f, 0.95f, 0.88f);
        }
    }
}
