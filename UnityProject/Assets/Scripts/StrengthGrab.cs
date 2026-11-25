using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Warehouse
{
    public sealed class StrengthGrab : XRGrabInteractable
    {
        public RoomWalker player;
        public InteractionFeedback feedback;
        public Transform leftHand;
        Rigidbody body;

        protected override void Awake()
        {
            base.Awake();
            body = GetComponent<Rigidbody>();
        }

        public override bool IsSelectableBy(IXRSelectInteractor hand) =>
            body.mass <= player.CapacityKg && base.IsSelectableBy(hand);

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            var hand = args.interactorObject.transform;
            feedback.Click(hand == leftHand || hand.IsChildOf(leftHand));
        }

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase phase)
        {
            base.ProcessInteractable(phase);
            if (phase == XRInteractionUpdateOrder.UpdatePhase.Dynamic && isSelected && body.mass > player.CapacityKg)
                interactionManager.CancelInteractableSelection((IXRSelectInteractable)this);
        }
    }
}
