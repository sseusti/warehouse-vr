using UnityEngine;

namespace Warehouse
{
    /// <summary>
    /// Locks a GravityBody once its center comes to rest inside this trigger volume.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class GravitySocket : MonoBehaviour
    {
        [SerializeField] private GravityBody body;
        [Tooltip("Body speed below which it counts as resting in the socket, m/s.")]
        [SerializeField, Min(0f)] private float restSpeed = 0.15f;

        BoxCollider volume;

        public bool IsFilled => body.IsLocked;

        public void Configure(GravityBody target) => body = target;

        void Awake()
        {
            volume = GetComponent<BoxCollider>();
        }

        void FixedUpdate()
        {
            if (body.IsLocked)
                return;

            Rigidbody rigidbody = body.Body;
            if (volume.bounds.Contains(rigidbody.worldCenterOfMass) &&
                rigidbody.linearVelocity.sqrMagnitude < restSpeed * restSpeed)
            {
                body.Lock(volume.bounds.center);
            }
        }
    }
}
