using System;
using UnityEngine;

namespace Warehouse
{
    [CreateAssetMenu(menuName = "Warehouse/Gravity Presets", fileName = "GravityPresets")]
    public sealed class GravityPresets : ScriptableObject
    {
        [Serializable]
        public struct Preset
        {
            [Tooltip("Name shown above the object.")]
            public string label;
            [Tooltip("Free fall acceleration, m/s².")]
            [Min(0f)] public float acceleration;
            [Tooltip("Pull up instead of down.")]
            public bool upward;

            public Vector3 Vector => (upward ? Vector3.up : Vector3.down) * acceleration;
        }

        [Tooltip("Presets cycled by the remote. The first one is the starting gravity.")]
        [SerializeField] private Preset[] presets =
        {
            new() { label = "Земля", acceleration = 9.81f },
            new() { label = "Луна", acceleration = 1.62f },
            new() { label = "Марс", acceleration = 3.71f },
            new() { label = "Юпитер", acceleration = 24.79f },
            new() { label = "Невесомость", acceleration = 0f },
            new() { label = "Вверх", acceleration = 9.81f, upward = true },
        };

        public int Count => presets.Length;
        public Preset this[int index] => presets[index];
    }
}
