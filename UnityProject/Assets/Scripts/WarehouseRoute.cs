using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace Warehouse
{
    public sealed class WarehouseRoute : MonoBehaviour
    {
        [Header("Player")]
        public XROrigin player;
        public Transform rightController;
        public InputActionReference activate;
        public TeleportationProvider teleport;
        public LineRenderer aimLine;

        [Header("Ground floor")]
        public Collider weight;
        public GameObject grate;
        public Collider vent;
        public TeleportationArea startPad;
        public TeleportationArea ventPad;

        [Header("Shelf and exit")]
        public Transform shelfArrival;
        public Collider box;
        public BoxCollider dockZone;
        public TeleportationArea balconyPad;
        public BoxCollider exitZone;
        public TextMesh exitSign;

        const float AimRange = 7f;
        const float LiftSpeed = 1.4f;
        static readonly Color AimColor = new(0.73f, 0.81f, 0.9f, 0.55f);
        static readonly Color TargetColor = new(1f, 0.78f, 0.27f);

        enum Stage { Weight, Vent, Arriving, Box, Lifting, Balcony, Complete }
        Stage stage;

        Vector3 PlayerFloor
        {
            get
            {
                Vector3 floor = player.Camera.transform.position;
                floor.y = player.Origin.transform.position.y;
                return floor;
            }
        }

        void Awake()
        {
            balconyPad.gameObject.SetActive(false);
            aimLine.enabled = false;
        }

        void LateUpdate()
        {
            Collider target = stage switch
            {
                Stage.Weight => weight,
                Stage.Vent => vent,
                Stage.Box => box,
                _ => null,
            };
            aimLine.enabled = target != null;
            if (target != null && AimAt(target) && activate.action.WasPressedThisFrame())
                ActivateTarget();

            switch (stage)
            {
                case Stage.Arriving:
                    if (Mathf.Abs(PlayerFloor.y - shelfArrival.position.y) < 0.05f)
                        stage = Stage.Box;
                    break;
                case Stage.Lifting:
                    LiftBox();
                    break;
                case Stage.Balcony:
                    if (exitZone.bounds.Contains(PlayerFloor))
                    {
                        exitSign.text = "COMPLETE";
                        exitSign.color = new Color(0.4f, 1f, 0.7f);
                        stage = Stage.Complete;
                        Debug.Log("Warehouse route complete.");
                    }
                    break;
            }
        }

        bool AimAt(Collider target)
        {
            Vector3 direction = rightController.forward;
            Vector3 origin = rightController.position + direction * 0.07f;
            bool hit = Physics.Raycast(origin, direction, out RaycastHit result, AimRange,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            bool onTarget = hit && result.collider == target;
            aimLine.SetPosition(0, origin);
            aimLine.SetPosition(1, hit ? result.point : origin + direction * AimRange);
            aimLine.startColor = aimLine.endColor = onTarget ? TargetColor : AimColor;
            return onTarget;
        }

        void ActivateTarget()
        {
            switch (stage)
            {
                case Stage.Weight:
                    grate.SetActive(false);
                    stage = Stage.Vent;
                    break;
                case Stage.Vent:
                    startPad.enabled = false;
                    ventPad.enabled = false;
                    teleport.QueueTeleportRequest(new TeleportRequest
                    {
                        destinationPosition = shelfArrival.position,
                        destinationRotation = shelfArrival.rotation,
                        matchOrientation = MatchOrientation.TargetUpAndForward,
                    });
                    stage = Stage.Arriving;
                    break;
                case Stage.Box:
                    stage = Stage.Lifting;
                    aimLine.enabled = false;
                    break;
            }
        }

        void LiftBox()
        {
            Vector3 destination = dockZone.bounds.center;
            Vector3 position = Vector3.MoveTowards(box.transform.position, destination,
                LiftSpeed * Time.deltaTime);
            box.transform.position = position;
            if (dockZone.bounds.Contains(position))
            {
                box.transform.position = destination;
                balconyPad.gameObject.SetActive(true);
                stage = Stage.Balcony;
            }
        }
    }
}
