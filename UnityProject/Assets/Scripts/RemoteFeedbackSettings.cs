using System;
using UnityEngine;

namespace Warehouse
{
    [CreateAssetMenu(menuName = "Warehouse/Remote Feedback Settings", fileName = "RemoteFeedbackSettings")]
    public sealed class RemoteFeedbackSettings : ScriptableObject
    {
        [Serializable]
        public struct Pulse
        {
            [Range(0f, 1f)] public float amplitude;
            [Min(0f)] public float duration;

            public Pulse(float amplitude, float duration)
            {
                this.amplitude = amplitude;
                this.duration = duration;
            }
        }

        [Header("Sound")]
        [SerializeField, Range(0f, 1f)] private float volume = 0.6f;
        [Tooltip("Pitch limits for effects that depend on size or gravity.")]
        [SerializeField] private Vector2 pitchRange = new(0.5f, 2.5f);

        [Header("Controller vibration")]
        [SerializeField] private Pulse hover = new(0.12f, 0.02f);
        [SerializeField] private Pulse modeSwitch = new(0.25f, 0.05f);
        [SerializeField] private Pulse apply = new(0.5f, 0.12f);
        [SerializeField] private Pulse deny = new(0.35f, 0.08f);
        [SerializeField] private Pulse success = new(0.7f, 0.2f);
        [SerializeField] private Pulse undo = new(0.4f, 0.1f);

        [Header("Particles")]
        [SerializeField, Range(1, 64)] private int burstCount = 24;
        [SerializeField] private Color particleColor = new(1f, 0.72f, 0.27f, 1f);
        [SerializeField] private Color denyColor = new(0.95f, 0.25f, 0.2f, 1f);

        public float Volume => volume;
        public Vector2 PitchRange => pitchRange;
        public Pulse Hover => hover;
        public Pulse ModeSwitch => modeSwitch;
        public Pulse Apply => apply;
        public Pulse Deny => deny;
        public Pulse Success => success;
        public Pulse Undo => undo;
        public int BurstCount => burstCount;
        public Color ParticleColor => particleColor;
        public Color DenyColor => denyColor;
    }
}
