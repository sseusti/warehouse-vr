using System.Collections;
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
        public Transform leftController;
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

        const float SmallScale = 0.35f;
        const float FadeSeconds = 0.14f;
        const float AimRange = 7f;
        static readonly Color AimColor = new(0.73f, 0.81f, 0.9f, 0.55f);
        static readonly Color TargetColor = new(1f, 0.78f, 0.27f);

        enum Stage { Weight, Vent, Arriving, Box, Lifting, Balcony, Complete }
        enum RemoteMode { Size, Gravity }
        Stage stage = Stage.Weight;
        RemoteMode mode;
        WarehouseOverlay overlay;
        Rigidbody boxBody;
        InputAction switchMode;
        InputAction resizeSelf;
        Vector3 normalScale;
        float scaleFactor = 1f;
        bool restarting;

        bool AtPad(TeleportationArea pad)
        {
            Bounds bounds = pad.GetComponent<Collider>().bounds;
            Vector3 feet = PlayerFloor;
            return Mathf.Abs(feet.y - bounds.max.y) < 0.12f &&
                feet.x >= bounds.min.x && feet.x <= bounds.max.x &&
                feet.z >= bounds.min.z && feet.z <= bounds.max.z;
        }

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
            boxBody = box.attachedRigidbody;
            aimLine.enabled = false;
            if (leftController == null)
                leftController = player.CameraFloorOffsetObject.transform.Find("Left Controller");
            overlay = new WarehouseOverlay(player.Camera, transform);
            switchMode = new InputAction("Switch remote mode", InputActionType.Button,
                "<XRController>{RightHand}/{PrimaryButton}");
            normalScale = player.Origin.transform.localScale;
            resizeSelf = new InputAction("Resize player", InputActionType.Button,
                "<XRController>{RightHand}/{SecondaryButton}");
            SetMode(RemoteMode.Size);
        }

        void OnEnable()
        {
            overlay.EnableShortcut();
            switchMode.Enable();
            resizeSelf.Enable();
        }

        void OnDisable()
        {
            overlay.DisableShortcut();
            switchMode.Disable();
            resizeSelf.Disable();
        }

        void OnDestroy()
        {
            overlay.Dispose();
            switchMode.Dispose();
            resizeSelf.Dispose();
        }

        void SetMode(RemoteMode selected)
        {
            mode = selected;
            overlay.ShowMode(mode == RemoteMode.Size ? "Размер" : "Гравитация");
            RefreshHint();
        }

        void LateUpdate()
        {
            if (restarting || stage == Stage.Arriving)
                return;
            if (stage != Stage.Complete && switchMode.WasPressedThisFrame())
                SetMode(mode == RemoteMode.Size ? RemoteMode.Gravity : RemoteMode.Size);

            if (resizeSelf.WasPressedThisFrame() && mode == RemoteMode.Size &&
                (stage == Stage.Vent || (stage == Stage.Balcony && AtPad(balconyPad))))
            {
                Vector3 floor = PlayerFloor;
                scaleFactor = scaleFactor == 1f ? SmallScale : 1f;
                player.Origin.transform.localScale = normalScale * scaleFactor;
                player.Origin.transform.position += floor - PlayerFloor;
                RefreshHint();
            }

            Collider target = stage switch
            {
                Stage.Weight => weight,
                Stage.Vent => vent,
                Stage.Box => box,
                _ => null,
            };
            overlay.Place(player.Camera.transform, leftController, scaleFactor);
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
                case Stage.Balcony:
                    if (scaleFactor == 1f && exitZone.bounds.Contains(PlayerFloor))
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
            Vector3 origin = rightController.position + direction * (0.07f * scaleFactor);
            var overlayHit = overlay.Intersect(new Ray(origin, direction), out Vector3 overlayPoint);
            bool onHintToggle = overlayHit == WarehouseOverlay.Hit.ToggleHints;
            bool onRestart = overlayHit == WarehouseOverlay.Hit.Restart;
            bool onButton = onHintToggle || onRestart;
            overlay.SetHovered(overlayHit);

            bool complete = stage == Stage.Complete;
            bool pressed = activate.action.WasPressedThisFrame();
            if (onRestart && pressed)
            {
                restarting = true;
                SceneManager.LoadSceneAsync(gameObject.scene.path, LoadSceneMode.Single);
                return;
            }

            bool shortcut = !complete && overlay.ShortcutPressed;
            if (shortcut || (onHintToggle && pressed))
                overlay.Toggle();

            aimLine.enabled = target != null || onButton || complete;
            bool hitScene = Physics.Raycast(origin, direction, out RaycastHit result, AimRange,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            bool modeMatches = stage switch
            {
                Stage.Weight => mode == RemoteMode.Size,
                Stage.Box => mode == RemoteMode.Gravity,
                Stage.Vent => scaleFactor == SmallScale && AtPad(ventPad),
                _ => true,
            };
            bool onTarget = overlayHit == WarehouseOverlay.Hit.None && modeMatches && hitScene && result.collider == target;
            pressurePlate.SetHighlighted(stage == Stage.Weight && onTarget);

            Vector3 end = hitScene ? result.point : origin + direction * AimRange;
            if (overlayHit != WarehouseOverlay.Hit.None)
                end = overlayPoint;
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
                overlay.ShowCompletion();
                return;
            }

            RefreshHint();
        }

        void RefreshHint()
        {
            var (title, instructions) = stage switch
            {
                Stage.Weight when mode != RemoteMode.Size =>
                    ("Уменьши гирю", "Нажми A для режима «Размер». Наведи луч на гирю и нажми курок."),
                Stage.Weight => ("Уменьши гирю", "Наведи правый луч на гирю и нажми курок."),
                Stage.Vent when scaleFactor == 1f && mode != RemoteMode.Size =>
                    ("Уменьши себя", "Нажми A для режима «Размер», затем B на правом контроллере."),
                Stage.Vent when scaleFactor == 1f =>
                    ("Уменьши себя", "Нажми B на правом контроллере. Левым стиком переместись на площадку у решётки."),
                Stage.Vent => ("Пройди через вентиляцию", "С площадки у решётки наведи луч в проём и нажми правый курок."),
                Stage.Arriving => ("Переход на полку", ""),
                Stage.Box when mode != RemoteMode.Gravity =>
                    ("Подними ящик", "Нажми A для режима «Гравитация». Наведи луч на ящик и нажми курок."),
                Stage.Box => ("Подними ящик", "Наведи правый луч на ящик внизу и нажми курок."),
                Stage.Lifting => ("Ящик поднимается", "Дождись фиксации ящика в пазу."),
                Stage.Balcony when scaleFactor == SmallScale =>
                    ("Верни обычный рост", "Левым стиком перейди на балкон. Выбери «Размер» кнопкой A и нажми B."),
                _ => ("Доберись до выхода", "Переместись к двери с надписью EXIT."),
            };
            overlay.Show(title, instructions);
        }

        void ActivateTarget()
        {
            switch (stage)
            {
                case Stage.Weight:
                    pressurePlate.Shrink();
                    break;
                case Stage.Vent:
                    StartCoroutine(EnterVent());
                    break;
                case Stage.Box:
                    boxBody.useGravity = false;
                    boxBody.WakeUp();
                    SetStage(Stage.Lifting);
                    aimLine.enabled = false;
                    break;
            }
        }

        IEnumerator EnterVent()
        {
            SetStage(Stage.Arriving);
            aimLine.enabled = false;
            startPad.enabled = false;
            ventPad.enabled = false;
            yield return Fade(0f, 1f);
            yield return null;
            bool queued = teleport.QueueTeleportRequest(new TeleportRequest
            {
                destinationPosition = shelfArrival.position,
                destinationRotation = shelfArrival.rotation,
                matchOrientation = MatchOrientation.TargetUpAndForward,
            });
            float deadline = Time.unscaledTime + 2f;
            while (queued && Mathf.Abs(PlayerFloor.y - shelfArrival.position.y) > 0.05f &&
                Time.unscaledTime < deadline)
                yield return null;

            bool arrived = Mathf.Abs(PlayerFloor.y - shelfArrival.position.y) <= 0.05f;
            startPad.enabled = ventPad.enabled = !arrived;
            overlay.Place(player.Camera.transform, leftController, scaleFactor);
            yield return Fade(1f, 0f);
            SetStage(arrived ? Stage.Box : Stage.Vent);
        }

        IEnumerator Fade(float from, float to)
        {
            float elapsed = 0f;
            while (elapsed < FadeSeconds)
            {
                overlay.Fade(Mathf.Lerp(from, to, elapsed / FadeSeconds));
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            overlay.Fade(to);
        }

        void FixedUpdate()
        {
            if (stage != Stage.Lifting)
                return;

            if (!dockZone.bounds.Contains(boxBody.worldCenterOfMass))
            {
                boxBody.AddForce(-Physics.gravity, ForceMode.Acceleration);
                return;
            }

            boxBody.linearVelocity = Vector3.zero;
            boxBody.angularVelocity = Vector3.zero;
            boxBody.isKinematic = true;
            boxBody.position = dockZone.bounds.center;
            balconyPad.gameObject.SetActive(true);
            SetStage(Stage.Balcony);
        }
    }
}
