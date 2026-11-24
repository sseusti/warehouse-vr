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
        public TeleportationArea balconyPad;
        public BoxCollider exitZone;
        public TextMesh exitSign;

        [Header("Remote effects")]
        [SerializeField] private PlayerScaler scaler;
        [SerializeField] private GravityBody boxGravity;
        [SerializeField] private GravitySocket boxSocket;
        [SerializeField] private RemoteFeedback feedback;

        const float AimRange = 7f;
        static readonly Color AimColor = new(0.73f, 0.81f, 0.9f, 0.55f);
        static readonly Color TargetColor = new(1f, 0.78f, 0.27f);
        static readonly Color ForbiddenColor = new(0.95f, 0.25f, 0.2f);

        enum Stage { Weight, Vent, Arriving, Box, Balcony, Complete }
        enum RemoteMode { Size, Gravity }
        Stage stage = Stage.Weight;
        RemoteMode mode;
        WarehouseOverlay overlay;
        InputAction switchMode;
        bool restarting;
        float aimWidth;
        bool wasAiming;

        Vector3 PlayerFloor => scaler.Floor;

        bool SelfTargetAvailable => mode == RemoteMode.Size && !scaler.IsChanging &&
            stage is Stage.Vent or Stage.Box or Stage.Balcony;

        public void ConfigureScaler(PlayerScaler value) => scaler = value;

        public void ConfigureFeedback(RemoteFeedback value) => feedback = value;

        public void ConfigureBox(GravityBody gravity, GravitySocket socket)
        {
            boxGravity = gravity;
            boxSocket = socket;
        }

        void Awake()
        {
            balconyPad.gameObject.SetActive(false);
            aimLine.enabled = false;
            aimWidth = aimLine.widthMultiplier;
            if (leftController == null)
                leftController = player.CameraFloorOffsetObject.transform.Find("Left Controller");
            overlay = new WarehouseOverlay(player.Camera, transform);
            switchMode = new InputAction("Switch remote mode", InputActionType.Button,
                "<XRController>{RightHand}/{PrimaryButton}");
            SetMode(RemoteMode.Size);
        }

        void OnEnable()
        {
            scaler.Changed += RefreshHint;
            overlay.EnableShortcut();
            switchMode.Enable();
        }

        void OnDisable()
        {
            scaler.Changed -= RefreshHint;
            overlay.DisableShortcut();
            switchMode.Disable();
        }

        void OnDestroy()
        {
            overlay.Dispose();
            switchMode.Dispose();
        }

        void SetMode(RemoteMode selected)
        {
            mode = selected;
            overlay.ShowMode(mode == RemoteMode.Size ? "Размер" : "Гравитация");
            RefreshHint();
        }

        void LateUpdate()
        {
            if (restarting)
                return;
            if (stage != Stage.Complete && switchMode.WasPressedThisFrame())
            {
                SetMode(mode == RemoteMode.Size ? RemoteMode.Gravity : RemoteMode.Size);
                feedback.ModeSwitched(rightController.position);
            }

            Collider target = stage switch
            {
                Stage.Weight => weight,
                Stage.Vent => vent,
                Stage.Box => box,
                _ => null,
            };
            overlay.Place(player.Camera.transform, leftController);
            AimAndActivate(target);

            switch (stage)
            {
                case Stage.Weight:
                    if (pressurePlate.IsReleased)
                    {
                        grate.SetActive(false);
                        feedback.Succeeded(grate.transform.position);
                        SetStage(Stage.Vent);
                        Debug.Log("Warehouse pressure plate released.");
                    }
                    break;
                case Stage.Arriving:
                    if (Mathf.Abs(PlayerFloor.y - shelfArrival.position.y) < 0.05f)
                        SetStage(Stage.Box);
                    break;
                case Stage.Box:
                    if (boxSocket.IsFilled)
                    {
                        balconyPad.gameObject.SetActive(true);
                        feedback.Succeeded(box.transform.position);
                        SetStage(Stage.Balcony);
                        Debug.Log("Warehouse box docked.");
                    }
                    break;
                case Stage.Balcony:
                    if (scaler.IsNormalSize && exitZone.bounds.Contains(PlayerFloor))
                    {
                        balconyPad.enabled = false;
                        exitSign.text = "COMPLETE";
                        exitSign.color = new Color(0.4f, 1f, 0.7f);
                        feedback.Succeeded(player.Camera.transform.position + player.Camera.transform.forward);
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
            var overlayHit = overlay.Intersect(new Ray(origin, direction), out Vector3 overlayPoint);
            bool onHintToggle = overlayHit == WarehouseOverlay.Hit.ToggleHints;
            bool onRestart = overlayHit == WarehouseOverlay.Hit.Restart;
            bool onButton = onHintToggle || onRestart;
            overlay.SetHovered(overlayHit);

            bool complete = stage == Stage.Complete;
            bool pressed = activate.action.WasPressedThisFrame();
            if (onButton && pressed)
                feedback.Pressed(overlayPoint);
            if (onRestart && pressed)
            {
                restarting = true;
                SceneManager.LoadSceneAsync(gameObject.scene.path, LoadSceneMode.Single);
                return;
            }

            bool shortcut = !complete && overlay.ShortcutPressed;
            if (shortcut || (onHintToggle && pressed))
                overlay.Toggle();

            bool selfAvailable = SelfTargetAvailable;
            aimLine.enabled = target != null || onButton || complete || selfAvailable;
            aimLine.widthMultiplier = aimWidth * scaler.Scale;
            var ray = new Ray(origin, direction);
            bool hitScene = Physics.Raycast(ray, out RaycastHit result, AimRange,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float selfDistance = 0f;
            bool onSelf = overlayHit == WarehouseOverlay.Hit.None && selfAvailable &&
                scaler.RayHitsSelf(ray, out selfDistance) && selfDistance <= AimRange &&
                (!hitScene || result.distance > selfDistance - 0.05f);
            scaler.ShowSelfTarget(selfAvailable, onSelf);

            bool modeMatches = stage switch
            {
                Stage.Weight => mode == RemoteMode.Size,
                Stage.Box => mode == RemoteMode.Gravity,
                _ => true,
            };
            bool onCollider = overlayHit == WarehouseOverlay.Hit.None && !onSelf && hitScene && result.collider == target;
            bool forbidden = onCollider && stage == Stage.Vent && !scaler.FitsVent;
            bool onTarget = onCollider && modeMatches && !forbidden && !scaler.IsChanging;
            pressurePlate.SetHighlighted(stage == Stage.Weight && onTarget);
            boxGravity.SetHighlighted(stage == Stage.Box && onTarget);

            Vector3 end = hitScene ? result.point : origin + direction * AimRange;
            if (onSelf)
                end = ray.GetPoint(selfDistance);
            if (overlayHit != WarehouseOverlay.Hit.None)
                end = overlayPoint;
            aimLine.SetPosition(0, origin);
            aimLine.SetPosition(1, end);
            aimLine.startColor = aimLine.endColor = forbidden ? ForbiddenColor
                : onTarget || onButton || onSelf ? TargetColor : AimColor;

            bool aiming = onTarget || onSelf || onButton;
            if (aiming && !wasAiming)
                feedback.Hover();
            wasAiming = aiming;

            if (!pressed || shortcut)
                return;
            if (onSelf)
            {
                scaler.ChangeToNextStep();
                feedback.Applied(end, feedback.PitchForScale(scaler.TargetScale));
            }
            else if (onTarget)
            {
                ActivateTarget(result.point);
            }
            else if (onCollider)
            {
                // Right object, but the remote cannot act on it now: wrong mode or too big for the vent.
                feedback.Denied(result.point);
            }
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
                Stage.Vent when !scaler.FitsVent && mode != RemoteMode.Size =>
                    ("Стань меньше", "Нажми A для режима «Размер». Наведи луч на кольцо у себя под ногами и нажми курок."),
                Stage.Vent when !scaler.FitsVent =>
                    ("Стань меньше", "Подойди к решётке. Наведи луч на кольцо у себя под ногами и нажми курок."),
                Stage.Vent => ("Пройди через вентиляцию", "Подойди к решётке. Наведи правый луч на проём и нажми курок."),
                Stage.Arriving => ("Переход на полку", ""),
                Stage.Box or Stage.Balcony when !scaler.IsNormalSize && mode != RemoteMode.Size =>
                    ("Верни свой размер", "Нажми A для режима «Размер». Наведи луч на кольцо под ногами и нажми курок."),
                Stage.Box or Stage.Balcony when !scaler.IsNormalSize =>
                    ("Верни свой размер", "Наведи луч на кольцо у себя под ногами и нажимай курок, пока не станешь прежним."),
                Stage.Box when mode != RemoteMode.Gravity =>
                    ("Подними ящик", "Нажми A для режима «Гравитация». Наведи луч на ящик и нажми курок."),
                Stage.Box => ("Подними ящик в паз",
                    $"Курок по ящику меняет его гравитацию. Сейчас: {boxGravity.Current.label}. Найди ту, что тянет вверх."),
                _ => ("Доберись до выхода", "Левым стиком телепортируйся на балкон к двери с надписью EXIT."),
            };
            overlay.Show(title, instructions);
        }

        void ActivateTarget(Vector3 point)
        {
            switch (stage)
            {
                case Stage.Weight:
                    pressurePlate.Shrink();
                    feedback.Applied(point, feedback.PitchForScale(WeightPressurePlate.SmallScale));
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
                    feedback.Applied(point, 1.2f);
                    SetStage(Stage.Arriving);
                    break;
                case Stage.Box:
                    boxGravity.NextPreset();
                    feedback.Applied(point, feedback.PitchForGravity(boxGravity.Current));
                    RefreshHint();
                    break;
            }
        }
    }
}
