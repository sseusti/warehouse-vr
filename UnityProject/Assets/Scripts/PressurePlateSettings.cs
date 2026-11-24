using UnityEngine;

namespace Warehouse
{
    [CreateAssetMenu(menuName = "Warehouse/Pressure Plate Settings", fileName = "PressurePlateSettings")]
    public sealed class PressurePlateSettings : ScriptableObject
    {
        [Tooltip("Scale factor k the remote applies to the weight. Mass follows m = m0 * k^3.")]
        [SerializeField, Range(0.1f, 1f)] private float shrinkScale = 0.5f;
        [Tooltip("The button rises when the mass on it drops below this value, kg.")]
        [SerializeField, Min(0f)] private float releaseMass = 2f;
        [Tooltip("Duration of the weight shrinking, seconds.")]
        [SerializeField, Min(0.01f)] private float shrinkDuration = 0.4f;
        [Tooltip("How far the button rises, meters.")]
        [SerializeField, Min(0f)] private float buttonTravel = 0.08f;
        [Tooltip("Duration of the button rising, seconds.")]
        [SerializeField, Min(0.01f)] private float buttonDuration = 0.18f;

        public float ShrinkScale => shrinkScale;
        public float ReleaseMass => releaseMass;
        public float ShrinkDuration => shrinkDuration;
        public float ButtonTravel => buttonTravel;
        public float ButtonDuration => buttonDuration;
    }
}
