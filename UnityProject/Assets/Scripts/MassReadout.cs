using UnityEngine;

namespace Warehouse
{
    public sealed class MassReadout : MonoBehaviour
    {
        public VariableMass target;
        public Camera eye;
        public TextMesh label;
        public Collider shape;
        float lastMass = -1f;

        void LateUpdate()
        {
            float kg = Mathf.Round(target.Body.mass * 10f) * .1f;
            if (kg != lastMass)
            {
                label.text = kg.ToString("0.0") + " кг";
                lastMass = kg;
            }
            Bounds box = shape.bounds;
            transform.position = box.center + Vector3.up * (box.extents.y + .09f);
            transform.rotation = eye.transform.rotation;
        }
    }
}
