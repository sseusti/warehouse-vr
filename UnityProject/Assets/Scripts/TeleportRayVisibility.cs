using UnityEngine;
using UnityEngine.InputSystem;

namespace Warehouse
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class TeleportRayVisibility : MonoBehaviour
    {
        [SerializeField] InputActionReference aim;
        LineRenderer line;

        public InputActionReference Aim
        {
            set => aim = value;
        }

        void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.forceRenderingOff = true;
        }

        void LateUpdate()
        {
            line.forceRenderingOff = aim.action.phase != InputActionPhase.Performed;
        }
    }
}
