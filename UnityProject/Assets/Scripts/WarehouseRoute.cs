using TMPro;
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
        public TMP_Text exitSign;

        [Header("Remote effects")]
        [SerializeField] private PlayerScaler scaler;
        [SerializeField] private GravityBody boxGravity;
        [SerializeField] private GravitySocket boxSocket;
        [SerializeField] private RemoteFeedback feedback;
        [SerializeField] private UndoHistory history;

        [Header("Objective markers")]
        [SerializeField] private ObjectiveMarker weightMarker;
        [SerializeField] private ObjectiveMarker ventMarker;
        [SerializeField] private ObjectiveMarker boxMarker;
        [SerializeField] private ObjectiveMarker exitMarker;
        [Tooltip("Aiming at this collider shows the exit marker.")]
        [SerializeField] private Collider exitDoor;

        const float AimRange = 7f;
        const float PlayerClearance = 0.3f;
        static readonly Color AimColor = new(0.73f, 0.81f, 0.9f, 0.55f);
        static readonly Color TargetColor = new(1f, 0.78f, 0.27f);
        static readonly Color ForbiddenColor = new(0.95f, 0.25f, 0.2f);

        enum Stage { Weight, Vent, Arriving, Box, Balcony, Complete }
        enum RemoteMode { Size, Gravity }
        Stage stage = Stage.Weight;
        RemoteMode mode;
        WarehouseOverlay overlay;
        InputAction switchMode;
        InputAction grow;
        bool restarting;
        float aimWidth;
        bool wasAiming;
        Collider aimed;

        Vector3 PlayerFloor => scaler.Floor;

        bool SelfTargetAvailable => mode == RemoteMode.Size && !scaler.IsChanging &&
            stage is Stage.Vent or Stage.Box or Stage.Balcony;

        public void ConfigureScaler(PlayerScaler value) => scaler = value;

        public void ConfigureFeedback(RemoteFeedback value) => feedback = value;

        public void ConfigureUndo(UndoHistory value) => history = value;

        public void ConfigureMarkers(ObjectiveMarker weightTarget, ObjectiveMarker ventTarget,
            ObjectiveMarker boxTarget, ObjectiveMarker exitTarget, Collider door)
        {
            exitDoor = door;
            weightMarker = weightTarget;
            ventMarker = ventTarget;
            boxMarker = boxTarget;
            exitMarker = exitTarget;
        }

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
            // Right stick up enlarges the aimed object; left/right stays snap turn.
            grow = new InputAction("Grow aimed object", InputActionType.Button,
                "<XRController>{RightHand}/{Primary2DAxis}/up");
            SetMode(RemoteMode.Size);
            RefreshUndoHint();
        }

        void OnEnable()
        {
            scaler.Changed += RefreshHint;
            history.Changed += RefreshUndoHint;
            overlay.EnableShortcut();
            switchMode.Enable();
            grow.Enable();
        }

        void OnDisable()
        {
            scaler.Changed -= RefreshHint;
            history.Changed -= RefreshUndoHint;
            overlay.DisableShortcut();
            switchMode.Disable();
            grow.Disable();
        }

        void OnDestroy()
        {
            overlay.Dispose();
            switchMode.Dispose();
            grow.Dispose();
        }

        void SetMode(RemoteMode selected)
        {
            mode = selected;
            overlay.ShowMode(mode == RemoteMode.Size ? "Размер" : "Гравитация");
            RefreshHint();
        }

        void LateUpdate()
        {
            UpdateUndoBlock();
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
                    if (pressurePlate.IsUnlocked)
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
                        // The docked box stays in its slot; its gravity changes cannot be undone any more.
                        history.Forget(boxGravity);
                        feedback.Succeeded(box.transform.position);
                        SetStage(Stage.Balcony);
                        Debug.Log("Warehouse box docked.");
                    }
                    break;
                case Stage.Balcony:
                    if (scaler.IsNormalSize && exitZone.bounds.Contains(PlayerFloor))
                    {
                        balconyPad.enabled = false;
                        exitSign.text = "ГОТОВО";
                        exitSign.color = new Color(0.4f, 1f, 0.7f);
                        feedback.Succeeded(player.Camera.transform.position + player.Camera.transform.forward);
                        SetStage(Stage.Complete);
                        Debug.Log("Warehouse route complete.");
                    }
                    break;
            }
            UpdateUndoBlock();
            UpdateMarkers();
        }

        // The current goal's marker appears while the right ray points at that object and hints are shown.
        void UpdateMarkers()
        {
            bool hints = overlay.HintsVisible && !restarting;
            bool normal = scaler.IsNormalSize;
            weightMarker.SetVisible(hints && stage == Stage.Weight && aimed == weight);
            ventMarker.SetVisible(hints && stage == Stage.Vent && aimed == vent);
            boxMarker.SetVisible(hints && stage == Stage.Box && normal && aimed == box);
            exitMarker.SetVisible(hints && stage == Stage.Balcony && normal && aimed == exitDoor);
        }

        // Called before and after the frame's actions, so B never lands in the middle of a transition.
        void UpdateUndoBlock() =>
            history.Blocked = restarting || stage is Stage.Arriving or Stage.Complete || scaler.IsChanging;

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
            aimed = overlayHit == WarehouseOverlay.Hit.None && hitScene ? result.collider : null;
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

            if (stage == Stage.Weight && onCollider && grow.WasPressedThisFrame())
            {
                if (onTarget)
                    ResizeWeight(+1, result.point);
                else
                    feedback.Denied(result.point);
            }

            if (!pressed || shortcut)
                return;
            if (onSelf)
            {
                ChangeOwnSize(end);
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

        void ChangeOwnSize(Vector3 point)
        {
            int before = scaler.StepIndex;
            scaler.ChangeToNextStep();
            history.Record(new UndoStep("свой рост", scaler, () => scaler.ChangeToStep(before), fadesItself: true));
            feedback.Applied(point, feedback.PitchForScale(scaler.TargetScale));
        }

        void ResizeWeight(int direction, Vector3 point)
        {
            ScalableBody body = pressurePlate.Scalable;
            float before = body.TargetScale;
            if (!body.TryStep(direction, PlayerFloor, PlayerClearance))
            {
                feedback.Denied(point);
                return;
            }

            // Undoing a shrink makes the weight grow back, so it needs room again.
            System.Func<bool> canRevert = direction < 0
                ? () => body.CanGrowTo(before, PlayerFloor, PlayerClearance)
                : null;
            history.Record(new UndoStep("размер гири", body, () => body.SetScaleImmediate(before), canRevert));
            feedback.Applied(point, feedback.PitchForScale(body.TargetScale));
        }

        /// <summary>Undo of the vent crossing: back to the spot in front of the grate.</summary>
        void ReturnToVent(Vector3 floor, Vector3 forward)
        {
            startPad.enabled = true;
            ventPad.enabled = true;
            balconyPad.gameObject.SetActive(false);
            teleport.QueueTeleportRequest(new TeleportRequest
            {
                destinationPosition = floor,
                destinationRotation = Quaternion.LookRotation(forward, Vector3.up),
                matchOrientation = MatchOrientation.TargetUpAndForward,
            });
            SetStage(Stage.Vent);
        }

        void RefreshUndoHint() => overlay.ShowUndo(history.Last?.Label);

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
                    ("Подбери массу гири", "Нажми A для режима «Размер». Затем наведи луч на гирю."),
                Stage.Weight => ("Подбери массу гири",
                    "Луч на гирю: курок — меньше на 10 %, правый стик вверх — больше. Весам нужно 1,5–2,5 кг."),
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
                _ => ("Доберись до выхода", "Левым стиком телепортируйся на балкон к двери с надписью «ВЫХОД»."),
            };
            overlay.Show(title, instructions);
        }

        void ActivateTarget(Vector3 point)
        {
            switch (stage)
            {
                case Stage.Weight:
                    ResizeWeight(-1, point);
                    break;
                case Stage.Vent:
                    Vector3 floor = PlayerFloor;
                    Vector3 forward = Vector3.ProjectOnPlane(player.Camera.transform.forward, Vector3.up);
                    if (forward.sqrMagnitude < 0.001f)
                        forward = player.transform.forward;
                    history.Record(new UndoStep("переход через вентиляцию", vent,
                        () => ReturnToVent(floor, forward.normalized)));
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
                    int preset = boxGravity.PresetIndex;
                    Vector3 position = boxGravity.Body.position;
                    boxGravity.NextPreset();
                    history.Record(new UndoStep("гравитация ящика", boxGravity, () =>
                    {
                        boxGravity.Restore(preset, position);
                        RefreshHint();
                    }));
                    feedback.Applied(point, feedback.PitchForGravity(boxGravity.Current));
                    RefreshHint();
                    break;
            }
        }
    }
}
