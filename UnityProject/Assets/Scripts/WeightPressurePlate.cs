using System.Globalization;
using UnityEngine;

namespace Warehouse
{
    public sealed class WeightPressurePlate : MonoBehaviour
    {
        [SerializeField] Rigidbody weight;
        [SerializeField] Transform button;
        [SerializeField] private PressurePlateSettings settings;
        [SerializeField] private TextMesh readout;

        static readonly int ColorProperty = Shader.PropertyToID("_Color");
        static readonly Color HeavyColor = new(1f, 0.45f, 0.38f);
        static readonly Color LightColor = new(0.45f, 1f, 0.65f);

        Vector3 fullScale;
        Vector3 weightStart;
        float fullMass;
        float weightHalfHeight;
        Vector3 buttonStart;
        Vector3 buttonScale;
        float scale = 1f;
        float rise;
        bool shrinkRequested;
        Renderer weightRenderer;
        MaterialPropertyBlock weightProperties;
        bool highlighted;
        float shownMass = -1f;

        public bool IsReleased => weight.mass < settings.ReleaseMass && rise == 1f;
        public float ShrinkScale => settings.ShrinkScale;

        public void Configure(Rigidbody body, Transform plunger)
        {
            weight = body;
            button = plunger;
        }

        public void ConfigureReadout(PressurePlateSettings values, TextMesh label)
        {
            settings = values;
            readout = label;
        }

        void Awake()
        {
            fullScale = weight.transform.localScale;
            fullMass = weight.mass;
            weightStart = weight.position;
            weightHalfHeight = weight.GetComponent<BoxCollider>().bounds.extents.y;
            buttonStart = button.position;
            buttonScale = button.localScale;
            weightRenderer = weight.GetComponent<Renderer>();
            weightProperties = new MaterialPropertyBlock();
            UpdateReadout();
        }

        public void Shrink()
        {
            shrinkRequested = true;
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
            float target = settings.ShrinkScale;
            if (!shrinkRequested || IsReleased && scale == target)
                return;

            scale = Mathf.MoveTowards(scale, target, (1f - target) * Time.deltaTime / settings.ShrinkDuration);
            weight.transform.localScale = fullScale * scale;
            weight.mass = fullMass * scale * scale * scale;
            UpdateReadout();

            if (weight.mass < settings.ReleaseMass)
                rise = Mathf.MoveTowards(rise, 1f, Time.deltaTime / settings.ButtonDuration);

            float lifted = settings.ButtonTravel * rise;
            button.localScale = buttonScale + Vector3.up * lifted;
            button.position = buttonStart + Vector3.up * (lifted * 0.5f);
            weight.position = weightStart + Vector3.up * (lifted - weightHalfHeight * (1f - scale));
        }

        void UpdateReadout()
        {
            if (readout == null)
                return;

            // Text changes only while the shown value changes, so the string is rebuilt rarely.
            float mass = Mathf.Round(weight.mass * 10f) / 10f;
            if (mass == shownMass)
                return;

            shownMass = mass;
            bool light = weight.mass < settings.ReleaseMass;
            readout.text = $"Гиря {Kilograms(mass)} кг\nнужно < {Kilograms(settings.ReleaseMass)} кг";
            readout.color = light ? LightColor : HeavyColor;
        }

        static string Kilograms(float value) =>
            value.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',');
    }
}
