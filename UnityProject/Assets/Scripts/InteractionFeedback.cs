using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.OpenXR.Input;

namespace Warehouse
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class InteractionFeedback : MonoBehaviour
    {
        public InputActionReference leftHaptic, rightHaptic;
        public AudioClip click, grow, shrink, dock;
        AudioSource speaker;
        int lastDirection;
        float nextPulse;

        void Awake()
        {
            speaker = GetComponent<AudioSource>();
            speaker.playOnAwake = false;
        }

        static void Pulse(InputActionReference output, float amplitude, float duration)
        {
            if (output != null && output.action.controls.Count > 0)
                OpenXRInput.SendHapticImpulse(output, amplitude, duration);
        }

        public void Click(bool left = false)
        {
            Pulse(left ? leftHaptic : rightHaptic, .28f, .045f);
            speaker.PlayOneShot(click, .4f);
        }

        public void Resize(int direction)
        {
            if (direction == 0) { lastDirection = 0; return; }
            if (direction != lastDirection)
            {
                speaker.PlayOneShot(direction > 0 ? grow : shrink, .38f);
                nextPulse = 0;
            }
            if (Time.unscaledTime >= nextPulse)
            {
                Pulse(rightHaptic, .16f, .044f);
                nextPulse = Time.unscaledTime + .055f;
            }
            lastDirection = direction;
        }

        public void Dock(Vector3 position) => AudioSource.PlayClipAtPoint(dock, position, .75f);
    }
}
