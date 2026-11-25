using UnityEngine;

namespace Warehouse
{
    [CreateAssetMenu(menuName = "Warehouse/Undo Settings", fileName = "UndoSettings")]
    public sealed class UndoSettings : ScriptableObject
    {
        [Tooltip("How many remote actions can be undone; the oldest is forgotten first.")]
        [SerializeField, Range(1, 64)] private int capacity = 16;
        [Tooltip("Time to darken the view before and to clear it after the undo, seconds.")]
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.15f;
        [Tooltip("Darkness of the view at the moment of undo, 0..1.")]
        [SerializeField, Range(0f, 1f)] private float fadeAlpha = 0.85f;

        public int Capacity => capacity;
        public float FadeDuration => fadeDuration;
        public float FadeAlpha => fadeAlpha;
    }
}
