using UnityEngine;

namespace Warehouse
{
    [CreateAssetMenu(menuName = "Warehouse/Pressure Plate Settings", fileName = "PressurePlateSettings")]
    public sealed class PressurePlateSettings : ScriptableObject
    {
        [Tooltip("The lock opens when the settled mass on the plate is at least this, kg.")]
        [SerializeField, Min(0f)] private float minMass = 1.5f;
        [Tooltip("The lock opens when the settled mass on the plate is at most this, kg.")]
        [SerializeField, Min(0f)] private float maxMass = 2.5f;
        [Tooltip("Mass that presses the spring button fully down, kg. Lighter loads let it rise proportionally.")]
        [SerializeField, Min(0.01f)] private float springMass = 4f;
        [Tooltip("Rise of the button with nothing on it, meters.")]
        [SerializeField, Min(0f)] private float buttonTravel = 0.1f;
        [Tooltip("Button speed, meters per second.")]
        [SerializeField, Min(0.01f)] private float buttonSpeed = 0.25f;

        public float MinMass => minMass;
        public float MaxMass => maxMass;
        public float SpringMass => springMass;
        public float ButtonTravel => buttonTravel;
        public float ButtonSpeed => buttonSpeed;

        /// <summary>Spring rise of the button under the given mass, meters.</summary>
        public float RiseFor(float mass) => buttonTravel * Mathf.Clamp01((springMass - mass) / springMass);
    }
}
