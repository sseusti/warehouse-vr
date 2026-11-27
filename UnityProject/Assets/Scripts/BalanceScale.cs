using System;
using System.Globalization;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace Warehouse
{
    /// <summary>
    /// Lever balance with equal arms: a scalable load on one pan, the player on the other.
    /// The heavier side goes down (m = m0 * k^3 for both the load and the player), the other rises
    /// slowly and carries the player with it behind a comfort vignette.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class BalanceScale : MonoBehaviour
    {
        [SerializeField] private BalanceSettings settings;
        [SerializeField] private ScalableBody load;
        [SerializeField] private Transform loadPan;
        [SerializeField] private Collider riderPan;
        [SerializeField] private Transform beam;
        [SerializeField] private PlayerScaler rider;
        [SerializeField] private XROrigin origin;
        [SerializeField] private TMP_Text readout;

        static readonly int ColorProperty = Shader.PropertyToID("_Color");
        static readonly Color HighlightColor = new(1f, 0.86f, 0.40f);
        static readonly Color RiderHeavierColor = new(0.55f, 0.75f, 1f);
        static readonly Color LoadHeavierColor = new(0.45f, 1f, 0.65f);

        Vector3 loadPanMiddle;
        Vector3 riderPanMiddle;
        float loadPanHalfHeight;
        float riderPanHalfHeight;
        float armLength;
        float position;
        float velocity;
        bool riderOn;
        ComfortVignette vignette;
        Renderer loadRenderer;
        MaterialPropertyBlock loadProperties;
        bool highlighted;
        float shownLoad = -1f;
        float shownRider = -1f;

        /// <summary>Raised when the player steps on or off the pan, or a shown mass changes.</summary>
        public event Action Changed;

        public ScalableBody Load => load;
        public bool RiderOn => riderOn;
        /// <summary>True when the player stands on the pan and it has reached the top.</summary>
        public bool RiderRaised => riderOn && Goal() > 0f && position >= 0.995f;
        public float LoadMass => load.Mass;
        public float RiderMass => settings.PlayerMass * Cube(rider.Scale);
        /// <summary>-1: player pan at the bottom, +1: at the top.</summary>
        public float Position => position;

        public void Configure(BalanceSettings values, ScalableBody body, Transform bodyPan, Collider playerPan,
            Transform lever, PlayerScaler player, XROrigin xrOrigin, TMP_Text label)
        {
            settings = values;
            load = body;
            loadPan = bodyPan;
            riderPan = playerPan;
            beam = lever;
            rider = player;
            origin = xrOrigin;
            readout = label;
        }

        void Awake()
        {
            loadPanMiddle = loadPan.position;
            riderPanMiddle = riderPan.transform.position;
            loadPanHalfHeight = loadPan.lossyScale.y * 0.5f;
            riderPanHalfHeight = riderPan.bounds.extents.y;
            armLength = Mathf.Abs(riderPanMiddle.x - beam.position.x);
            loadRenderer = load.GetComponent<Renderer>();
            loadProperties = new MaterialPropertyBlock();
            vignette = new ComfortVignette(origin.Camera, "Balance Vignette");
        }

        void Start()
        {
            // Settle without motion at start: the empty pan is up, the load pan down.
            riderOn = IsOnPan(rider.Floor);
            position = Goal();
            Apply();
            UpdateReadout(true);
        }

        void OnDestroy() => vignette?.Dispose();

        public void SetHighlighted(bool value)
        {
            if (value == highlighted)
                return;

            highlighted = value;
            loadProperties.Clear();
            if (highlighted)
                loadProperties.SetColor(ColorProperty, HighlightColor);
            loadRenderer.SetPropertyBlock(loadProperties);
        }

        void Update()
        {
            bool on = IsOnPan(rider.Floor);
            if (on != riderOn)
            {
                riderOn = on;
                Changed?.Invoke();
            }

            // While the player changes size the pan waits, so the scaling pivot stays put.
            float previousTop = RiderPanTop;
            if (!rider.IsChanging)
            {
                float speed = settings.MaxSpeed / (settings.Travel * 0.5f);
                position = Mathf.SmoothDamp(position, Goal(), ref velocity, settings.SmoothTime, speed);
                if (Mathf.Abs(position - Goal()) < 0.0005f && Mathf.Abs(velocity) < 0.001f)
                {
                    position = Goal();
                    velocity = 0f;
                }
            }
            else
            {
                velocity = 0f;
            }

            Apply();
            if (riderOn)
                origin.transform.position += Vector3.up * (RiderPanTop - previousTop);

            float motion = Mathf.Abs(velocity) * settings.Travel * 0.5f / settings.MaxSpeed;
            vignette.SetStrength(riderOn ? Mathf.Clamp01(motion) * settings.Vignette : 0f);
            UpdateReadout(false);
        }

        float RiderPanTop => riderPan.transform.position.y + riderPanHalfHeight;

        // The heavier side goes down; with equal arms only the masses matter.
        float Goal()
        {
            float riderMass = riderOn ? RiderMass : 0f;
            float loadMass = LoadMass;
            if (Mathf.Abs(loadMass - riderMass) < 0.0005f)
                return 0f;
            return loadMass > riderMass ? 1f : -1f;
        }

        void Apply()
        {
            float lift = position * settings.Travel * 0.5f;
            riderPan.transform.position = riderPanMiddle + Vector3.up * lift;
            loadPan.position = loadPanMiddle - Vector3.up * lift;
            load.Bottom = loadPan.position + Vector3.up * loadPanHalfHeight;
            float angle = Mathf.Atan2(lift, armLength) * Mathf.Rad2Deg;
            float side = Mathf.Sign(riderPanMiddle.x - beam.position.x);
            beam.localRotation = Quaternion.Euler(0f, 0f, angle * side);
        }

        bool IsOnPan(Vector3 floor)
        {
            Bounds bounds = riderPan.bounds;
            return floor.x > bounds.min.x - 0.05f && floor.x < bounds.max.x + 0.05f &&
                floor.z > bounds.min.z - 0.05f && floor.z < bounds.max.z + 0.05f &&
                Mathf.Abs(floor.y - bounds.max.y) < settings.StandTolerance;
        }

        void UpdateReadout(bool force)
        {
            float loadShown = Round(LoadMass);
            float riderShown = riderOn ? Round(RiderMass) : 0f;
            if (!force && loadShown == shownLoad && riderShown == shownRider)
                return;

            shownLoad = loadShown;
            shownRider = riderShown;
            Changed?.Invoke();
            if (readout == null)
                return;

            if (!riderOn)
            {
                readout.text = $"гиря {Kilograms(loadShown)} кг\nчаша пуста";
                readout.color = LoadHeavierColor;
                return;
            }

            bool loadHeavier = loadShown > riderShown;
            readout.text = $"гиря {Kilograms(loadShown)} кг · ты {Kilograms(riderShown)} кг\n" +
                (loadHeavier ? "перевешивает гиря" : "перевешиваешь ты");
            readout.color = loadHeavier ? LoadHeavierColor : RiderHeavierColor;
        }

        static float Cube(float value) => value * value * value;

        static float Round(float mass) => mass < 10f ? Mathf.Round(mass * 100f) / 100f : Mathf.Round(mass);

        /// <summary>Two decimals below 10 kg, so 0,94 and 0,86 never look equal.</summary>
        public static string Kilograms(float mass) =>
            mass.ToString(mass < 10f ? "0.00" : "0", CultureInfo.InvariantCulture).Replace('.', ',');
    }
}
