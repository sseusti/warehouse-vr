using UnityEngine;

namespace Warehouse
{
    [CreateAssetMenu(menuName = "Warehouse/Marker Settings", fileName = "MarkerSettings")]
    public sealed class MarkerSettings : ScriptableObject
    {
        [Tooltip("Marker size per metre of distance to the eyes, so it keeps the same angular size.")]
        [SerializeField, Min(0.001f)] private float sizePerMetre = 0.16f;
        [Tooltip("Smallest and largest marker size, metres.")]
        [SerializeField] private Vector2 sizeRange = new(0.12f, 0.6f);
        [Tooltip("Gap between the top of the target and the arrow tip, in marker sizes.")]
        [SerializeField, Min(0f)] private float gap = 0.35f;
        [Tooltip("Up and down bob of the arrow, in marker sizes.")]
        [SerializeField, Min(0f)] private float bobHeight = 0.18f;
        [Tooltip("Bob cycles per second; slow enough to stay comfortable in VR.")]
        [SerializeField, Min(0f)] private float bobFrequency = 0.7f;
        [Tooltip("Fade in and out time, seconds.")]
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.35f;
        [Tooltip("Closer than this (in player heights) the marker fades out: the player is already there.")]
        [SerializeField, Min(0f)] private float hideWithin = 0.45f;
        [SerializeField] private Color arrowColor = new(1f, 0.78f, 0.27f, 1f);
        [SerializeField] private Color labelColor = new(1f, 0.97f, 0.88f, 1f);

        public float SizePerMetre => sizePerMetre;
        public Vector2 SizeRange => sizeRange;
        public float Gap => gap;
        public float BobHeight => bobHeight;
        public float BobFrequency => bobFrequency;
        public float FadeDuration => fadeDuration;
        public float HideWithin => hideWithin;
        public Color ArrowColor => arrowColor;
        public Color LabelColor => labelColor;
    }
}
