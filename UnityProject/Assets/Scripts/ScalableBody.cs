using UnityEngine;

namespace Warehouse
{
    /// <summary>
    /// Changes the size of a Rigidbody in fixed steps with mass = baseMass * k^3.
    /// The bottom of the object stays in place. Growing is refused when there is no room.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class ScalableBody : MonoBehaviour
    {
        [SerializeField] private ScaleStepSettings settings;

        const float RoomTolerance = 0.98f;
        static readonly Collider[] Overlaps = new Collider[8];

        Rigidbody body;
        BoxCollider box;
        Vector3 baseScale;
        float baseMass;
        float baseHalfHeight;
        Vector3 bottom;
        float scale = 1f;
        float target = 1f;
        Collider[] ignored = System.Array.Empty<Collider>();

        public float Scale => scale;
        public float TargetScale => target;
        public bool IsSettled => scale == target;
        public float Mass => body.mass;
        public float BaseMass => baseMass;
        /// <summary>Mass the body will have once the current step finishes.</summary>
        public float TargetMass => baseMass * target * target * target;
        /// <summary>World point the bottom face rests on; moved by a support such as a button.</summary>
        public Vector3 Bottom
        {
            get => bottom;
            set => bottom = value;
        }

        public void Configure(ScaleStepSettings values) => settings = values;

        /// <summary>Colliders that may touch the body when it grows, such as its support.</summary>
        public void IgnoreForRoomCheck(params Collider[] colliders) => ignored = colliders;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            box = GetComponent<BoxCollider>();
            baseScale = transform.localScale;
            baseMass = body.mass;
            baseHalfHeight = box.bounds.extents.y;
            bottom = transform.position - Vector3.up * baseHalfHeight;
        }

        /// <summary>
        /// Requests one size step: -1 smaller, +1 larger. Returns false at a limit or without room to grow.
        /// </summary>
        public bool TryStep(int direction) => TryStep(direction, null, 0f);

        /// <summary>
        /// Same as <see cref="TryStep(int)"/>, but growing is also refused when <paramref name="keepOut"/>,
        /// for example the player's floor point, would end up within <paramref name="clearance"/> of the body.
        /// </summary>
        public bool TryStep(int direction, Vector3? keepOut, float clearance)
        {
            float next = Mathf.Round((target + direction * settings.Step) * 100f) / 100f;
            if (next < settings.MinScale - 0.001f || next > settings.MaxScale + 0.001f)
                return false;
            if (direction > 0 && !CanGrowTo(next, keepOut, clearance))
                return false;

            target = next;
            return true;
        }

        /// <summary>
        /// True when the body has room to grow to <paramref name="value"/> and stays clear of <paramref name="keepOut"/>.
        /// </summary>
        public bool CanGrowTo(float value, Vector3? keepOut, float clearance) =>
            HasRoom(value) && !(keepOut.HasValue && Reaches(value, keepOut.Value, clearance));

        /// <summary>Jumps to a scale without animation, for example when undoing an action.</summary>
        public void SetScaleImmediate(float value)
        {
            target = scale = value;
            Apply();
        }

        void Update()
        {
            if (scale != target)
            {
                float speed = settings.Step / settings.StepDuration;
                scale = Mathf.MoveTowards(scale, target, speed * Time.deltaTime);
            }
            Apply();
        }

        void Apply()
        {
            transform.localScale = baseScale * scale;
            body.mass = baseMass * scale * scale * scale;
            Vector3 center = bottom + Vector3.up * (baseHalfHeight * scale);
            if (body.isKinematic)
                transform.position = center;
            else
                body.position = center;
        }

        bool Reaches(float nextScale, Vector3 point, float clearance)
        {
            Vector3 size = Vector3.Scale(box.size, baseScale) * nextScale;
            Vector3 center = bottom + Vector3.up * (baseHalfHeight * nextScale);
            var bounds = new Bounds(center, size + Vector3.one * (clearance * 2f));
            point.y = Mathf.Clamp(point.y, bounds.min.y, bounds.max.y);
            return bounds.Contains(point);
        }

        bool HasRoom(float nextScale)
        {
            Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, baseScale) * (nextScale * RoomTolerance);
            Vector3 center = bottom + Vector3.up * (baseHalfHeight * nextScale);
            int count = Physics.OverlapBoxNonAlloc(center, halfExtents, Overlaps, transform.rotation,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider other = Overlaps[i];
                if (other == box || System.Array.IndexOf(ignored, other) >= 0)
                    continue;
                return false;
            }
            return true;
        }
    }
}
