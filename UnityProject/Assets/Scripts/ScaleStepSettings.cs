using UnityEngine;

namespace Warehouse
{
    [CreateAssetMenu(menuName = "Warehouse/Scale Step Settings", fileName = "ScaleStepSettings")]
    public sealed class ScaleStepSettings : ScriptableObject
    {
        [Tooltip("Size change per remote press, as a fraction of the original size.")]
        [SerializeField, Range(0.01f, 1f)] private float step = 0.1f;
        [Tooltip("Smallest scale factor k.")]
        [SerializeField, Range(0.05f, 1f)] private float minScale = 0.1f;
        [Tooltip("Largest scale factor k.")]
        [SerializeField, Range(1f, 10f)] private float maxScale = 4f;
        [Tooltip("Time of one smooth size step, seconds.")]
        [SerializeField, Min(0.01f)] private float stepDuration = 0.4f;

        public float Step => step;
        public float MinScale => minScale;
        public float MaxScale => maxScale;
        public float StepDuration => stepDuration;
    }
}
