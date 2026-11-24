using UnityEngine;

namespace Warehouse
{
    public sealed class WeightPressurePlate : MonoBehaviour
    {
        [SerializeField] Rigidbody weight;
        [SerializeField] Transform button;

        public const float SmallScale = 0.5f;
        const float ReleaseMass = 2f;
        const float ShrinkDuration = 0.4f;
        const float ButtonTravel = 0.08f;
        const float ButtonDuration = 0.18f;
        static readonly int ColorProperty = Shader.PropertyToID("_Color");

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

        public bool IsReleased => weight.mass < ReleaseMass && rise == 1f;

        public void Configure(Rigidbody body, Transform plunger)
        {
            weight = body;
            button = plunger;
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
            if (!shrinkRequested || IsReleased && scale == SmallScale)
                return;

            scale = Mathf.MoveTowards(scale, SmallScale, (1f - SmallScale) * Time.deltaTime / ShrinkDuration);
            weight.transform.localScale = fullScale * scale;
            weight.mass = fullMass * scale * scale * scale;

            if (weight.mass < ReleaseMass)
                rise = Mathf.MoveTowards(rise, 1f, Time.deltaTime / ButtonDuration);

            float lifted = ButtonTravel * rise;
            button.localScale = buttonScale + Vector3.up * lifted;
            button.position = buttonStart + Vector3.up * (lifted * 0.5f);
            weight.position = weightStart + Vector3.up * (lifted - weightHalfHeight * (1f - scale));
        }
    }
}
