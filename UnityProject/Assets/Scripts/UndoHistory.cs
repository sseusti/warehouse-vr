using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Warehouse
{
    /// <summary>
    /// Remembers the last remote actions and takes them back one by one on the right B button.
    /// The view briefly darkens while the state jumps back.
    /// </summary>
    public sealed class UndoHistory : MonoBehaviour
    {
        [SerializeField] private UndoSettings settings;
        [SerializeField] private RemoteFeedback feedback;
        [SerializeField] private Transform controller;
        [SerializeField] private Camera view;

        readonly List<UndoStep> steps = new();
        InputAction undo;
        ViewFade fade;
        UndoStep pending;
        float elapsed = -1f;

        /// <summary>Raised when a step is recorded, undone or forgotten.</summary>
        public event Action Changed;

        /// <summary>Set by the room while undo must wait, for example during a transition.</summary>
        public bool Blocked { get; set; }
        public bool IsBusy => elapsed >= 0f;
        public int Count => steps.Count;
        public UndoStep Last => steps.Count > 0 ? steps[^1] : null;

        public void Configure(UndoSettings values, RemoteFeedback remoteFeedback, Transform remote, Camera camera)
        {
            settings = values;
            feedback = remoteFeedback;
            controller = remote;
            view = camera;
        }

        void Awake()
        {
            undo = new InputAction("Undo remote action", InputActionType.Button,
                "<XRController>{RightHand}/{SecondaryButton}");
            fade = new ViewFade(view, "Undo Fade");
        }

        void OnEnable() => undo.Enable();

        void OnDisable() => undo.Disable();

        void OnDestroy()
        {
            undo.Dispose();
            fade.Dispose();
        }

        public void Record(UndoStep step)
        {
            if (steps.Count >= settings.Capacity)
                steps.RemoveAt(0);
            steps.Add(step);
            Changed?.Invoke();
        }

        /// <summary>Drops the steps of an object that can no longer go back, for example a docked box.</summary>
        public void Forget(object owner)
        {
            if (steps.RemoveAll(step => step.Owner == owner) > 0)
                Changed?.Invoke();
        }

        public void Clear()
        {
            steps.Clear();
            Changed?.Invoke();
        }

        /// <summary>Takes back the last action. Returns false and plays "not allowed" when it cannot.</summary>
        public bool TryUndo()
        {
            UndoStep step = Last;
            if (Blocked || IsBusy || step == null || !step.CanRevert)
            {
                feedback.Denied(controller.position);
                return false;
            }

            steps.RemoveAt(steps.Count - 1);
            feedback.Undone(controller.position);
            if (step.FadesItself)
                Revert(step);
            else
            {
                pending = step;
                elapsed = 0f;
            }
            Changed?.Invoke();
            return true;
        }

        void Update()
        {
            if (undo.WasPressedThisFrame())
                TryUndo();
            if (!IsBusy)
                return;

            float duration = settings.FadeDuration;
            elapsed += Time.deltaTime;
            if (pending != null && elapsed >= duration)
            {
                UndoStep step = pending;
                pending = null;
                Revert(step);
            }

            float darkness = elapsed < duration
                ? elapsed / duration
                : 1f - Mathf.Clamp01((elapsed - duration) / duration);
            fade.SetAlpha(darkness * settings.FadeAlpha);
            if (elapsed < duration * 2f)
                return;

            elapsed = -1f;
            fade.SetAlpha(0f);
        }

        static void Revert(UndoStep step)
        {
            step.Revert();
            Debug.Log($"Warehouse undo: {step.Label}.");
        }
    }
}
