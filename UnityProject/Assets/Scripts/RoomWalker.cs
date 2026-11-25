using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Warehouse
{
    // The capsule stays in world units while the XR Origin scales around the tracked feet.
    public sealed class RoomWalker : MonoBehaviour
    {
        public XROrigin rig;
        public InputActionReference stick;
        public float speed = 1.65f;
        public float smallScale = .35f;
        public float resizeSpeed = .85f;
        public float baseCapacityKg = 12f;
        public float Scale { get; private set; } = 1f;
        public float CapacityKg => baseCapacityKg * Scale * Scale;
        public bool IsSmall => Scale == smallScale;
        public bool Suspended { get; set; }
        public Vector3 Feet => new(rig.Camera.transform.position.x, rig.Origin.transform.position.y, rig.Camera.transform.position.z);
        CharacterController capsule;
        float targetScale = 1f;
        float verticalSpeed;

        void Start()
        {
            var body = new GameObject("Walking capsule");
            body.layer = LayerMask.NameToLayer("Ignore Raycast");
            body.transform.position = Feet;
            capsule = body.AddComponent<CharacterController>();
            capsule.skinWidth = .006f;
            capsule.minMoveDistance = 0f;
            capsule.stepOffset = .025f;
            FitCapsule();
        }

        public void ToggleSize() => targetScale = targetScale == 1f ? smallScale : 1f;

        public void PlaceFeet(Vector3 at)
        {
            rig.Origin.transform.position += at - Feet;
            capsule.enabled = false;
            capsule.transform.position = at;
            capsule.enabled = true;
            verticalSpeed = 0f;
        }

        void FitCapsule()
        {
            capsule.radius = .17f * Scale;
            capsule.height = Mathf.Max(.65f * Scale, rig.Camera.transform.position.y - Feet.y + .035f * Scale);
            capsule.center = Vector3.up * (capsule.height * .5f);
        }

        void Update()
        {
            if (Suspended) return;
            Vector3 feet = Feet;
            Scale = Mathf.MoveTowards(Scale, targetScale, resizeSpeed * Time.deltaTime);
            rig.Origin.transform.localScale = Vector3.one * Scale;
            rig.Origin.transform.position += feet - Feet;
            FitCapsule();
            Vector3 forward = Vector3.ProjectOnPlane(rig.Camera.transform.forward, Vector3.up).normalized;
            Vector2 input = Vector2.ClampMagnitude(stick.action.ReadValue<Vector2>(), 1f);
            Vector3 walk = forward * input.y + Vector3.Cross(Vector3.up, forward) * input.x;
            Vector3 trackedMotion = Vector3.ProjectOnPlane(feet - capsule.transform.position, Vector3.up);
            verticalSpeed = capsule.isGrounded ? -1f : Mathf.Max(-8f, verticalSpeed + Physics.gravity.y * Time.deltaTime);
            capsule.Move(trackedMotion + Time.deltaTime * (walk * (speed * Mathf.Sqrt(Scale)) + Vector3.up * verticalSpeed));
            rig.Origin.transform.position += capsule.transform.position - Feet;
        }

        void OnDestroy()
        {
            if (capsule != null) Destroy(capsule.gameObject);
        }
    }
}
