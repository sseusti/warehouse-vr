using System.Globalization;
using TMPro;
using UnityEngine;

namespace Warehouse
{
    /// <summary>
    /// Spring scale under the weight. The button rises the lighter the load is,
    /// and the lock opens once the settled mass falls inside the window.
    /// </summary>
    public sealed class WeightPressurePlate : MonoBehaviour
    {
        [SerializeField] Rigidbody weight;
        [SerializeField] Transform button;
        [SerializeField] private PressurePlateSettings settings;
        [SerializeField] private TMP_Text readout;
        [SerializeField] private ScalableBody scalable;

        static readonly int ColorProperty = Shader.PropertyToID("_Color");
        static readonly Color HeavyColor = new(1f, 0.45f, 0.38f);
        static readonly Color GoodColor = new(0.45f, 1f, 0.65f);
        static readonly Color LightColor = new(0.55f, 0.75f, 1f);

        Vector3 buttonStart;
        Vector3 buttonScale;
        Vector3 restBottom;
        float rise;
        bool unlocked;
        Renderer weightRenderer;
        MaterialPropertyBlock weightProperties;
        bool highlighted;
        float shownScale = -1f;
        float shownMass = -1f;

        public enum Load { Heavy, Good, Light }

        /// <summary>True once the mass has settled inside the window; stays true afterwards.</summary>
        public bool IsUnlocked => unlocked;
        public ScalableBody Scalable => scalable;
        public PressurePlateSettings Settings => settings;

        public Load Current
        {
            get
            {
                float mass = weight.mass;
                if (mass > settings.MaxMass + 0.0005f)
                    return Load.Heavy;
                return mass < settings.MinMass - 0.0005f ? Load.Light : Load.Good;
            }
        }

        public void Configure(Rigidbody body, Transform plunger)
        {
            weight = body;
            button = plunger;
        }

        public void ConfigureReadout(PressurePlateSettings values, TMP_Text label, ScalableBody body)
        {
            settings = values;
            readout = label;
            scalable = body;
        }

        void Awake()
        {
            buttonStart = button.position;
            buttonScale = button.localScale;
            weightRenderer = weight.GetComponent<Renderer>();
            weightProperties = new MaterialPropertyBlock();
        }

        void Start()
        {
            restBottom = scalable.Bottom;
            rise = settings.RiseFor(weight.mass);
            ApplyRise();
            UpdateReadout();
        }

        public void SetHighlighted(bool value)
        {
            if (value == highlighted)
                return;

            highlighted = value;
            weightProperties.Clear();
            if (highlighted)
                weightProperties.SetColor(ColorProperty, new Color(1f, 0.86f, 0.40f));
            weightRenderer.SetPropertyBlock(weightProperties);
        }

        void Update()
        {
            float targetRise = settings.RiseFor(weight.mass);
            rise = Mathf.MoveTowards(rise, targetRise, settings.ButtonSpeed * Time.deltaTime);
            ApplyRise();
            UpdateReadout();

            if (!unlocked && scalable.IsSettled && rise == targetRise && Current == Load.Good)
            {
                unlocked = true;
                Debug.Log($"Warehouse pressure plate unlocked at {weight.mass:0.###} kg.");
            }
        }

        void ApplyRise()
        {
            button.localScale = buttonScale + Vector3.up * rise;
            button.position = buttonStart + Vector3.up * (rise * 0.5f);
            scalable.Bottom = restBottom + Vector3.up * rise;
        }

        void UpdateReadout()
        {
            if (readout == null)
                return;

            // The text is rebuilt only when the shown numbers change.
            float shownK = Mathf.Round(scalable.Scale * 100f);
            float mass = Mathf.Round(weight.mass * 10f) / 10f;
            if (shownK == shownScale && mass == shownMass)
                return;

            shownScale = shownK;
            shownMass = mass;
            Load load = Current;
            string state = load switch
            {
                Load.Heavy => "тяжело",
                Load.Light => "слишком легко",
                _ => "в норме",
            };
            readout.text = $"Размер {shownK:0} % · масса {Kilograms(mass)} кг\n" +
                $"нужно {Kilograms(settings.MinMass)}–{Kilograms(settings.MaxMass)} кг · {state}";
            readout.color = load switch
            {
                Load.Heavy => HeavyColor,
                Load.Light => LightColor,
                _ => GoodColor,
            };
        }

        static string Kilograms(float value) =>
            value.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',');
    }
}
