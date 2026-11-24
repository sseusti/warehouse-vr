using UnityEngine;

namespace Warehouse
{
    [CreateAssetMenu(menuName = "Warehouse/Player Scale Settings", fileName = "PlayerScaleSettings")]
    public sealed class PlayerScaleSettings : ScriptableObject
    {
        [Tooltip("Player size steps cycled by the remote. The first step is normal size.")]
        [SerializeField] private float[] steps = { 1f, 0.25f, 0.1f };
        [Tooltip("Largest player scale that still fits through the vent.")]
        [SerializeField, Range(0.05f, 1f)] private float ventMaxScale = 0.25f;
        [Tooltip("Time to darken the view before and after the size change, seconds.")]
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.25f;
        [Tooltip("Time of the smooth size change while the view is dark, seconds.")]
        [SerializeField, Min(0.01f)] private float scaleDuration = 0.5f;
        [Tooltip("Darkness of the view during the size change, 0..1.")]
        [SerializeField, Range(0f, 1f)] private float fadeAlpha = 0.85f;

        public int StepCount => steps.Length;
        public float Step(int index) => steps[index];
        public float VentMaxScale => ventMaxScale;
        public float FadeDuration => fadeDuration;
        public float ScaleDuration => scaleDuration;
        public float FadeAlpha => fadeAlpha;
    }
}
