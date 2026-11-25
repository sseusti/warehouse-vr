using UnityEngine;

namespace Warehouse
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class VariableMass : MonoBehaviour
    {
        public float minScale = .45f;
        public float maxScale = 1.4f;
        public float speed = .55f;
        public bool keepBaseHeight;
        public float Scale { get; private set; } = 1f;
        public Rigidbody Body { get; private set; }
        Vector3 fullSize;
        float fullMass;
        float initialHalfHeight;
        Renderer surface;
        MaterialPropertyBlock tint;
        bool highlighted;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            surface = GetComponent<Renderer>();
            tint = new MaterialPropertyBlock();
            fullMass = Body.mass;
            fullSize = transform.localScale;
            initialHalfHeight = GetComponent<Collider>().bounds.extents.y;
        }

        public bool Resize(int direction, float elapsed)
        {
            float next = Mathf.Clamp(Scale + direction * speed * elapsed, minScale, maxScale);
            if (next == Scale) return false;
            if (keepBaseHeight)
                Body.position += Vector3.up * (initialHalfHeight * (next - Scale));
            Scale = next;
            transform.localScale = fullSize * next;
            Body.mass = fullMass * next * next * next;
            Body.WakeUp();
            return true;
        }

        public void Highlight(bool value)
        {
            if (value == highlighted) return;
            highlighted = value;
            tint.Clear();
            if (value) tint.SetColor("_Color", new Color(1f, .85f, .42f));
            surface.SetPropertyBlock(tint);
        }
    }
}
