using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Warehouse
{
    public sealed class PilotRoom : MonoBehaviour
    {
        enum Step { Plate, Vent, Entering, Shelf, Lifting, Bridge, Complete }
        enum Tool { Shrink, Grow, Gravity }
        public RoomWalker walker;
        public Transform leftHand, rightHand, button, shelfArrival;
        public InputActionReference activate;
        public LineRenderer pointer;
        public InteractionFeedback feedback;
        public VariableMass weight, crate, cargoSize;
        public GameObject grate, bridge, gate;
        public BoxCollider entrance, dock, exit, balcony;
        public Rigidbody cargo;
        public Renderer cargoSurface;
        public StrengthGrab grab;
        InputAction modeKey, selfKey;
        WarehouseOverlay overlay;
        Step step;
        Tool tool;
        VariableMass highlighted;
        MaterialPropertyBlock cargoTint;
        bool cargoLit, loading, consumeTrigger;
        Vector3 buttonRest;
        float lift, shownCapacity = -1;
        string lastHint, lastInstruction;

        void Awake()
        {
            overlay = new WarehouseOverlay(walker.rig.Camera, transform);
            modeKey = new InputAction("Режим", InputActionType.Button, "<XRController>{RightHand}/{PrimaryButton}");
            selfKey = new InputAction("Рост", InputActionType.Button, "<XRController>{RightHand}/{SecondaryButton}");
            cargoTint = new MaterialPropertyBlock();
            buttonRest = button.position;
            pointer.enabled = true;
        }

        void OnEnable() { modeKey.Enable(); selfKey.Enable(); overlay.EnableShortcut(); }
        void OnDisable() { modeKey.Disable(); selfKey.Disable(); overlay.DisableShortcut(); }
        void OnDestroy() { modeKey.Dispose(); selfKey.Dispose(); overlay.Dispose(); }

        void LateUpdate()
        {
            if (loading || step == Step.Entering) return;
            Vector3 feet = walker.Feet;
            bool onBalcony = Above(balcony.bounds, feet);
            bool playing = step != Step.Complete;
            if (playing && modeKey.WasPressedThisFrame())
            {
                tool = (Tool)(((int)tool + 1) % 3);
                feedback.Click();
                shownCapacity = -1;
            }
            if (playing && selfKey.WasPressedThisFrame() && tool != Tool.Gravity &&
                (step == Step.Plate || step == Step.Vent || (step == Step.Bridge && onBalcony)))
            {
                walker.ToggleSize();
                feedback.Click();
            }
            float capacity = Mathf.Round(walker.CapacityKg * 10) / 10;
            if (shownCapacity != capacity)
            {
                string name = tool == Tool.Gravity ? "Гравитация" : tool == Tool.Shrink ? "Меньше" : "Больше";
                overlay.ShowMode(name + " · " + capacity.ToString("0.#") + " кг");
                shownCapacity = capacity;
            }
            overlay.Place(walker.rig.Camera.transform, leftHand, walker.Scale);
            Interact();
            if (step == Step.Plate || step == Step.Vent) UpdatePlate();
            if (step == Step.Vent && walker.IsSmall && Above(entrance.bounds, feet))
                StartCoroutine(EnterVent());
            if (step == Step.Bridge && walker.Scale == 1f && exit.bounds.Contains(feet))
            {
                step = Step.Complete;
                walker.Suspended = true;
                overlay.ShowCompletion(true);
            }
            if (playing && step != Step.Complete) ShowHint(onBalcony);
        }

        static bool Above(Bounds volume, Vector3 feet) =>
            feet.x >= volume.min.x && feet.x <= volume.max.x &&
            feet.z >= volume.min.z && feet.z <= volume.max.z &&
            feet.y >= volume.min.y - .1f && feet.y <= volume.max.y + .1f;

        void Interact()
        {
            var ray = new Ray(rightHand.position + rightHand.forward * (.065f * walker.Scale), rightHand.forward);
            var ui = overlay.Intersect(ray, out Vector3 uiPoint);
            bool pressed = activate.action.WasPressedThisFrame();
            bool held = activate.action.IsPressed();
            if (!held) consumeTrigger = false;
            overlay.SetHovered(ui);
            bool hit = Physics.Raycast(ray, out var surface, 7f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            VariableMass target = null;
            bool gravityTarget = false;
            if (hit && ui == WarehouseOverlay.Hit.None && step != Step.Complete)
            {
                target = tool == Tool.Gravity ? null : surface.collider.GetComponent<VariableMass>();
                if (target == cargoSize && (step == Step.Bridge || step == Step.Complete)) target = null;
                if (target == crate && grab.isSelected) target = null;
                if (target == weight && step != Step.Plate && step != Step.Vent) target = null;
                gravityTarget = step == Step.Shelf && tool == Tool.Gravity && surface.rigidbody == cargo;
            }
            if (target != highlighted)
            {
                if (highlighted != null) highlighted.Highlight(false);
                highlighted = target;
                if (highlighted != null) highlighted.Highlight(true);
            }
            if (gravityTarget != cargoLit)
            {
                cargoLit = gravityTarget;
                cargoTint.Clear();
                if (cargoLit) cargoTint.SetColor("_Color", new Color(1f, .85f, .42f));
                cargoSurface.SetPropertyBlock(cargoTint);
            }
            bool buttonHit = ui == WarehouseOverlay.Hit.Menu || ui == WarehouseOverlay.Hit.Restart || ui == WarehouseOverlay.Hit.ToggleHints;
            pointer.SetPosition(0, ray.origin);
            pointer.SetPosition(1, ui != WarehouseOverlay.Hit.None ? uiPoint : hit ? surface.point : ray.GetPoint(7));
            pointer.startColor = pointer.endColor = target || gravityTarget || buttonHit ? Color.yellow : Color.white;

            bool shortcut = step != Step.Complete && overlay.ShortcutPressed;
            if (shortcut || (pressed && buttonHit))
            {
                feedback.Click(shortcut);
                consumeTrigger = held;
                if (shortcut || ui == WarehouseOverlay.Hit.ToggleHints) overlay.Toggle();
                else
                {
                    loading = true;
                    SceneManager.LoadSceneAsync(ui == WarehouseOverlay.Hit.Menu ? PilotMenu.SceneName : gameObject.scene.path);
                }
            }

            int direction = tool == Tool.Grow ? 1 : -1;
            bool resizing = held && !consumeTrigger && target != null && target.Resize(direction, Time.deltaTime);
            feedback.Resize(resizing ? direction : 0);
            if (gravityTarget && pressed && !consumeTrigger)
            {
                cargo.useGravity = false;
                cargo.WakeUp();
                step = Step.Lifting;
                feedback.Click();
            }
        }

        void UpdatePlate()
        {
            float next = Mathf.MoveTowards(lift, weight.Body.mass < 2f ? .08f : 0f, .35f * Time.deltaTime);
            weight.Body.position += Vector3.up * (next - lift);
            button.position = buttonRest + Vector3.up * next;
            lift = next;
            bool open = lift >= .08f;
            if (grate.activeSelf == open) grate.SetActive(!open);
            step = open ? Step.Vent : Step.Plate;
        }

        IEnumerator EnterVent()
        {
            step = Step.Entering;
            walker.Suspended = true;
            pointer.enabled = false;
            feedback.Resize(0);
            Vector3 from = walker.Feet;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / .42f)
            {
                float eased = Mathf.SmoothStep(0f, 1f, t);
                walker.PlaceFeet(from + entrance.transform.forward * (.33f * eased));
                overlay.Fade(eased);
                yield return null;
            }
            overlay.Fade(1);
            yield return null;
            walker.rig.MatchOriginUpCameraForward(Vector3.up, shelfArrival.forward);
            walker.PlaceFeet(shelfArrival.position);
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / .18f)
            {
                overlay.Fade(1f - t);
                yield return null;
            }
            overlay.Fade(0);
            pointer.enabled = true;
            walker.Suspended = false;
            step = Step.Shelf;
        }

        void FixedUpdate()
        {
            if (step != Step.Lifting) return;
            Bounds socket = dock.bounds;
            Vector3 center = cargo.worldCenterOfMass;
            bool linedUp = Mathf.Abs(center.x - socket.center.x) <= socket.extents.x &&
                Mathf.Abs(center.z - socket.center.z) <= socket.extents.z;
            if (!linedUp || cargoSize.Scale > 1.02f || center.y < socket.min.y)
            {
                cargo.AddForce(-Physics.gravity, ForceMode.Acceleration);
                return;
            }
            cargo.linearVelocity = cargo.angularVelocity = Vector3.zero;
            cargo.isKinematic = true;
            cargo.position = dock.bounds.center;
            bridge.SetActive(true);
            gate.SetActive(false);
            feedback.Dock(cargo.position);
            step = Step.Bridge;
        }

        void ShowHint(bool onBalcony)
        {
            var (heading, instruction) = step switch
            {
                Step.Plate => ("Открой вентиляцию", "A — режим пульта. Выбери «Меньше» и держи курок на гире до массы ниже 2 кг."),
                Step.Vent => ("Войди в вентиляцию", "B в режиме размера меняет рост. Уменьшись и войди в проём левым стиком."),
                Step.Shelf when tool != Tool.Gravity => ("Подними ящик", "Кнопкой A выбери «Гравитация». Наведи луч на оранжевый ящик внизу."),
                Step.Shelf => ("Подними ящик", "Нажми курок, чтобы ящик поднялся в паз и открыл мост."),
                Step.Lifting => ("Ящик поднимается", "Если ящик упёрся в раму, уменьши его. Дождись щелчка фиксатора."),
                Step.Bridge when !onBalcony => ("Пройди по мосту", "Левым стиком перейди на балкон."),
                _ => ("Выход", "Выбери режим размера, верни рост кнопкой B и подойди к EXIT."),
            };
            if (heading == lastHint && instruction == lastInstruction) return;
            overlay.Show(heading, instruction);
            lastHint = heading;
            lastInstruction = instruction;
        }
    }
}
