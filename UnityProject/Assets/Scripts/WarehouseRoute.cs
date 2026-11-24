using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
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
        public WeightPressurePlate pressurePlate;
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
        WarehouseHints hints;
        bool restarting;

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
            hints = new WarehouseHints(player.Camera, transform);
            SetStage(Stage.Weight);
        }

        void OnEnable() => hints.EnableShortcut();
        void OnDisable() => hints.DisableShortcut();
        void OnDestroy() => hints.Dispose();

        void LateUpdate()
        {
            Collider target = stage switch
            {
                Stage.Weight => weight,
                Stage.Vent => vent,
                Stage.Box => box,
                _ => null,
            };
            hints.Place(player.Camera.transform);
            AimAndActivate(target);

            switch (stage)
            {
                case Stage.Weight:
                    if (pressurePlate.IsReleased)
                    {
                        grate.SetActive(false);
                        SetStage(Stage.Vent);
                        Debug.Log("Warehouse pressure plate released.");
                    }
                    break;
                case Stage.Arriving:
                    if (Mathf.Abs(PlayerFloor.y - shelfArrival.position.y) < 0.05f)
                        SetStage(Stage.Box);
                    break;
                case Stage.Lifting:
                    LiftBox();
                    break;
                case Stage.Balcony:
                    if (exitZone.bounds.Contains(PlayerFloor))
                    {
                        balconyPad.enabled = false;
                        exitSign.text = "COMPLETE";
                        exitSign.color = new Color(0.4f, 1f, 0.7f);
                        SetStage(Stage.Complete);
                        Debug.Log("Warehouse route complete.");
                    }
                    break;
            }
        }

        void AimAndActivate(Collider target)
        {
            Vector3 direction = rightController.forward;
            Vector3 origin = rightController.position + direction * 0.07f;
            var hintHit = hints.Intersect(new Ray(origin, direction), out Vector3 hintPoint);
            bool onButton = hintHit == WarehouseHints.Hit.Button;
            hints.SetHovered(onButton);

            bool complete = stage == Stage.Complete;
            bool pressed = activate.action.WasPressedThisFrame();
            if (complete && onButton && pressed)
            {
                if (!restarting)
                {
                    restarting = true;
                    SceneManager.LoadSceneAsync(gameObject.scene.path, LoadSceneMode.Single);
                }
                return;
            }

            bool shortcut = !complete && hints.ShortcutPressed;
            if (shortcut || (onButton && pressed))
                hints.Toggle();

            aimLine.enabled = target != null || onButton || complete;
            bool hitScene = Physics.Raycast(origin, direction, out RaycastHit result, AimRange,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            bool onTarget = hintHit == WarehouseHints.Hit.None && hitScene && result.collider == target;
            pressurePlate.SetHighlighted(stage == Stage.Weight && onTarget);

            Vector3 end = hitScene ? result.point : origin + direction * AimRange;
            if (hintHit != WarehouseHints.Hit.None)
                end = hintPoint;
            aimLine.SetPosition(0, origin);
            aimLine.SetPosition(1, end);
            aimLine.startColor = aimLine.endColor = onTarget || onButton ? TargetColor : AimColor;
            if (onTarget && pressed && !shortcut)
                ActivateTarget();
        }

        void SetStage(Stage next)
        {
            stage = next;
            if (stage == Stage.Complete)
            {
                hints.ShowCompletion();
                return;
            }

            var (title, instructions) = stage switch
            {
                Stage.Weight => ("Уменьши гирю", "Наведи луч правой рукой на гирю и нажми курок."),
                Stage.Vent => ("Пройди через вентиляцию", "Левым стиком переместись к решётке. Наведи правый луч на проём и нажми курок."),
                Stage.Arriving => ("Переход на полку", ""),
                Stage.Box => ("Подними ящик", "С полки наведи правый луч на ящик внизу и нажми курок."),
                Stage.Lifting => ("Ящик поднимается", "Дождись фиксации ящика в пазу."),
                _ => ("Доберись до выхода", "Левым стиком телепортируйся на балкон к двери с надписью EXIT."),
            };
            hints.Show(title, instructions);
        }

        void ActivateTarget()
        {
            switch (stage)
            {
                case Stage.Weight:
                    pressurePlate.Shrink();
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
                    SetStage(Stage.Arriving);
                    break;
                case Stage.Box:
                    SetStage(Stage.Lifting);
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
                SetStage(Stage.Balcony);
            }
        }
    }
}
