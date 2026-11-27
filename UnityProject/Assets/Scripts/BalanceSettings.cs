using UnityEngine;

namespace Warehouse
{
    [CreateAssetMenu(menuName = "Warehouse/Balance Settings", fileName = "BalanceSettings")]
    public sealed class BalanceSettings : ScriptableObject
    {
        [Tooltip("Player mass at normal size, kg. At scale k the mass is this value times k cubed.")]
        [SerializeField, Min(1f)] private float playerMass = 60f;
        [Tooltip("Vertical distance between the lowest and the highest pan position, metres.")]
        [SerializeField, Min(0.1f)] private float travel = 1f;
        [Tooltip("Top speed of a pan, metres per second; slow enough to ride comfortably.")]
        [SerializeField, Min(0.05f)] private float maxSpeed = 0.4f;
        [Tooltip("Time to speed up and slow down, seconds.")]
        [SerializeField, Min(0.05f)] private float smoothTime = 0.6f;
        [Tooltip("Darkness of the comfort vignette at full pan speed while the player rides, 0..1.")]
        [SerializeField, Range(0f, 1f)] private float vignette = 0.6f;
        [Tooltip("How far the floor point may be from the pan top and still count as standing on it, metres.")]
        [SerializeField, Min(0.01f)] private float standTolerance = 0.12f;

        public float PlayerMass => playerMass;
        public float Travel => travel;
        public float MaxSpeed => maxSpeed;
        public float SmoothTime => smoothTime;
        public float Vignette => vignette;
        public float StandTolerance => standTolerance;
    }
}
