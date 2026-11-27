using System;
using System.IO;
using System.Linq;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using static Warehouse.Editor.DayOneScenes;

namespace Warehouse.Editor
{
    public static class DayTwoScene
    {
        const string Folder = "Assets/Day2";
        public const string ScenePath = Folder + "/Warehouse.unity";
        const string ScaleSettingsPath = Folder + "/PlayerScaleSettings.asset";
        const string GravityPresetsPath = Folder + "/GravityPresets.asset";
        const string FeedbackSettingsPath = Folder + "/RemoteFeedbackSettings.asset";
        const string PlateSettingsPath = Folder + "/PressurePlateSettings.asset";
        const string ScaleStepSettingsPath = Folder + "/ScaleStepSettings.asset";
        const string UndoSettingsPath = Folder + "/UndoSettings.asset";
        const string MarkerSettingsPath = Folder + "/MarkerSettings.asset";
        const string BalanceSettingsPath = Folder + "/BalanceSettings.asset";
        // Lever balance against the back wall: load pan on the left, player pan on the right under the vent.
        static readonly Vector3 BalancePivot = new(-3.85f, 0.65f, 2.45f);
        static readonly Vector3 LoadPanCenter = new(-4.4f, 0.62f, 2.45f);
        static readonly Vector3 RiderPanCenter = new(-3.3f, 0.635f, 2.45f);
        const float WeightSize = 0.2f;
        const float WeightMass = 0.5f;
        const float BoxYaw = 103f;
        const float BoxMass = 20f;
        const float PlatformHeight = 1.65f;

        [MenuItem("Warehouse/Open Day 2 Warehouse")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureScene();
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Warehouse/Rebuild Day 2 Warehouse")]
        public static void Rebuild()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            // Keep the .meta file so the scene GUID stays the same after a rebuild.
            File.Delete(ScenePath);
            EnsureScene();
        }

        public static void EnsureScene()
        {
            if (File.Exists(ScenePath))
                return;

            EnsureScenes();
            LabText.ImportEssentials();
            if (!AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
                throw new InvalidOperationException("Run Warehouse > Import Text Resources first.");
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets", "Day2");

            var scene = EditorSceneManager.OpenScene(InteractionScene);
            var roots = scene.GetRootGameObjects();
            var rig = roots.Single(root => root.name == "XR Origin");
            foreach (var root in roots)
            {
                if (root != rig && root.name != "XR Interaction Manager" && root.name != "Directional Light")
                    UnityEngine.Object.DestroyImmediate(root);
            }

            rig.transform.position = new Vector3(-3.85f, 0f, -1.75f);
            var route = new GameObject("Warehouse Route").AddComponent<WarehouseRoute>();
            route.player = rig.GetComponent<XROrigin>();
            route.teleport = rig.GetComponent<TeleportationProvider>();
            route.leftController = rig.transform.Find("Camera Offset/Left Controller");
            route.rightController = rig.transform.Find("Camera Offset/Right Controller");
            var input = rig.GetComponent<InputActionManager>().actionAssets[0];
            route.activate = Action(input, "XRI Right Interaction/Activate");
            var teleportRay = rig.GetComponentInChildren<XRRayInteractor>();
            teleportRay.velocity = 8f;

            route.aimLine = new GameObject("Right Controller Aim").AddComponent<LineRenderer>();
            route.aimLine.transform.SetParent(route.rightController, false);
            route.aimLine.sharedMaterial = Material("Aim", Color.white, "Sprites/Default");
            route.aimLine.positionCount = 2;
            route.aimLine.widthMultiplier = 0.008f;
            route.aimLine.enabled = false;

            var haptics = route.rightController.gameObject.AddComponent<HapticImpulsePlayer>();
            haptics.hapticOutput = new XRInputHapticImpulseProvider("Haptic")
            {
                inputActionReference = Action(input, "XRI Right/Haptic Device"),
            };
            var feedback = route.gameObject.AddComponent<RemoteFeedback>();
            feedback.Configure(LoadOrCreate<RemoteFeedbackSettings>(FeedbackSettingsPath), haptics,
                route.aimLine.sharedMaterial);
            route.ConfigureFeedback(feedback);
            var history = route.gameObject.AddComponent<UndoHistory>();
            history.Configure(LoadOrCreate<UndoSettings>(UndoSettingsPath), feedback, route.rightController,
                route.player.Camera);
            route.ConfigureUndo(history);

            var floor = LabDecor.Paint("Floor", LabDecor.FloorColor);
            var wall = LabDecor.Paint("Wall", LabDecor.WallColor);
            var steel = LabDecor.Paint("Platform", LabDecor.PlatformColor);
            var target = Material("Target", new Color(0.94f, 0.57f, 0.18f));
            var pad = Material("Teleport Pad", new Color(0.10f, 0.67f, 0.54f));
            var scaler = rig.AddComponent<PlayerScaler>();
            scaler.Configure(route.player, ScaleSettings(), teleportRay, target);
            route.ConfigureScaler(scaler);

            CreateBlock("Warehouse Floor", new Vector3(0f, -0.1f, 0f), new Vector3(10f, 0.2f, 6f), floor);
            CreateBlock("Back Wall", new Vector3(0f, 2.15f, 3f), new Vector3(10f, 4.3f, 0.15f), wall);
            CreateBlock("Front Wall", new Vector3(0f, 2.15f, -3f), new Vector3(10f, 4.3f, 0.15f), wall);
            CreateBlock("West Wall", new Vector3(-5f, 2.15f, 0f), new Vector3(0.15f, 4.3f, 6f), wall);
            CreateBlock("East Wall", new Vector3(5f, 2.15f, 0f), new Vector3(0.15f, 4.3f, 6f), wall);

            route.startPad = AddPad("Start Pad", new Vector3(-3.85f, 0.015f, -1.75f),
                new Vector2(1.3f, 1.1f), pad, route.teleport);
            CreateBalance(route, steel, target, pad, scaler);

            // Vent high in the back wall, right above the player pan at its top position (1.15 m).
            route.vent = CreateBlock("Vent Recess", new Vector3(-3.3f, 1.45f, 2.88f),
                new Vector3(0.62f, 0.5f, 0.06f), steel).GetComponent<Collider>();
            CreateBlock("Vent Left Frame", new Vector3(-3.66f, 1.45f, 2.86f), new Vector3(0.1f, 0.7f, 0.13f), wall);
            CreateBlock("Vent Right Frame", new Vector3(-2.94f, 1.45f, 2.86f), new Vector3(0.1f, 0.7f, 0.13f), wall);
            CreateBlock("Vent Top Frame", new Vector3(-3.3f, 1.75f, 2.86f), new Vector3(0.82f, 0.1f, 0.13f), wall);
            CreateBlock("Vent Sill", new Vector3(-3.3f, 1.15f, 2.86f), new Vector3(0.82f, 0.1f, 0.13f), wall);
            route.grate = new GameObject("Closed Grate");
            route.grate.transform.position = new Vector3(-3.3f, 1.45f, 2.81f);
            for (var index = 0; index < 4; index++)
            {
                // No colliders: the remote ray passes the bars and hits the vent behind them.
                var bar = CreateBlock("Grate Bar", new Vector3(-0.18f + index * 0.12f, 0f, 0f),
                    new Vector3(0.05f, 0.48f, 0.04f), steel, route.grate.transform);
                UnityEngine.Object.DestroyImmediate(bar.GetComponent<Collider>());
            }

            CreateBlock("Shelf", new Vector3(-1.6f, PlatformHeight - 0.12f, 1.5f),
                new Vector3(2.2f, 0.24f, 2f), steel);
            route.shelfArrival = new GameObject("Shelf Arrival").transform;
            // Close to the shelf edge, so even a small player sees the box below.
            route.shelfArrival.SetPositionAndRotation(new Vector3(-0.8f, PlatformHeight + 0.01f, 0.95f),
                Quaternion.Euler(0f, 103f, 0f));
            route.box = CreateBlock("Lift Box", new Vector3(1.25f, 0.36f, 0.45f),
                Vector3.one * 0.72f, target).GetComponent<Collider>();

            var dock = new Vector3(1.25f, 1.66f, 0.45f);
            var dockZone = Zone("Dock Trigger", dock, Vector3.one * 0.08f);
            ConfigureBoxGravity(route, dockZone);
            CreateBlock("Dock Top", dock + Vector3.up * 0.44f, new Vector3(1.04f, 0.14f, 1.04f), steel);
            CreateBlock("Dock Left", dock + Vector3.back * 0.44f, new Vector3(1.04f, 0.65f, 0.13f), steel);
            CreateBlock("Dock Right", dock + Vector3.forward * 0.44f, new Vector3(1.04f, 0.65f, 0.13f), steel);
            CreateBlock("Dock Hanger", new Vector3(dock.x, 3.2f, dock.z),
                new Vector3(0.18f, 2.08f, 0.18f), wall);

            CreateBlock("Balcony", new Vector3(4.02f, PlatformHeight - 0.12f, 0f),
                new Vector3(1.82f, 0.24f, 4.8f), steel);
            route.balconyPad = AddPad("Balcony Pad", new Vector3(4.02f, PlatformHeight + 0.015f, 0f),
                new Vector2(1.62f, 4.55f), pad, route.teleport);
            route.balconyPad.gameObject.SetActive(false);
            route.exitZone = Zone("Exit Zone", new Vector3(4.02f, PlatformHeight + 0.14f, 1.75f),
                new Vector3(1.72f, 0.38f, 1.12f));
            CreateBlock("Exit Door", new Vector3(4.02f, PlatformHeight + 0.98f, 2.47f),
                new Vector3(1.25f, 1.96f, 0.16f), wall);
            route.exitSign = Sign("Exit Sign", "ВЫХОД", new Vector3(4.02f, 3.82f, 2.35f));

            LabDecor.Build(route);
            CreateMarkers(route);
            LabDecor.MarkStatic(scene, GameObject.Find("Pressure Button"));

            if (!EditorSceneManager.SaveScene(scene, ScenePath, saveAsCopy: true))
                throw new InvalidOperationException("Could not save " + ScenePath);

            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ScenePath);
        }

        static void CreateBalance(WarehouseRoute route, UnityEngine.Material steel, UnityEngine.Material target,
            UnityEngine.Material pad, PlayerScaler scaler)
        {
            var root = new GameObject("Balance");
            var pivot = new GameObject("Beam Pivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.position = BalancePivot;
            var beam = CreateBlock("Beam", Vector3.zero, new Vector3(RiderPanCenter.x - LoadPanCenter.x, 0.05f, 0.06f), steel, pivot);
            UnityEngine.Object.DestroyImmediate(beam.GetComponent<Collider>());
            CreateBlock("Balance Column", new Vector3(BalancePivot.x, BalancePivot.y * 0.5f, BalancePivot.z),
                new Vector3(0.1f, BalancePivot.y, 0.1f), steel);
            CreateBlock("Balance Base", new Vector3(BalancePivot.x, 0.02f, BalancePivot.z), new Vector3(0.4f, 0.04f, 0.3f), steel);

            var loadPan = CreateBlock("Load Pan", LoadPanCenter, new Vector3(0.6f, 0.06f, 0.6f), steel, root.transform);
            var riderPan = AddPad("Player Pan", RiderPanCenter, new Vector2(0.8f, 0.8f), pad, route.teleport);
            riderPan.transform.SetParent(root.transform, true);
            route.ventPad = riderPan;

            float panTop = LoadPanCenter.y + 0.03f;
            route.weight = CreateBlock("Weight", new Vector3(LoadPanCenter.x, panTop + WeightSize * 0.5f, LoadPanCenter.z),
                Vector3.one * WeightSize, target).GetComponent<Collider>();
            var body = route.weight.gameObject.AddComponent<Rigidbody>();
            body.mass = WeightMass;
            body.isKinematic = true;
            body.useGravity = false;
            var scalable = route.weight.gameObject.AddComponent<ScalableBody>();
            scalable.Configure(LoadOrCreate<ScaleStepSettings>(ScaleStepSettingsPath));
            scalable.IgnoreForRoomCheck(loadPan.GetComponent<Collider>());

            var readout = LabText.Create("Balance Readout", "", LabDecor.DialCenter + Vector3.back * 0.02f, 0.7f, Color.white);
            var balance = root.AddComponent<BalanceScale>();
            balance.Configure(LoadOrCreate<BalanceSettings>(BalanceSettingsPath), scalable, loadPan.transform,
                riderPan.GetComponent<Collider>(), pivot, scaler, route.player, readout);
            route.ConfigureBalance(balance);
        }

        static void ConfigureBoxGravity(WarehouseRoute route, BoxCollider dockZone)
        {
            var body = route.box.gameObject.AddComponent<Rigidbody>();
            body.mass = BoxMass;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.linearDamping = 0.05f;

            var gravity = route.box.gameObject.AddComponent<GravityBody>();
            gravity.Configure(LoadOrCreate<GravityPresets>(GravityPresetsPath), BoxYaw);
            var socket = dockZone.gameObject.AddComponent<GravitySocket>();
            socket.Configure(gravity);
            route.ConfigureBox(gravity, socket);
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static PlayerScaleSettings ScaleSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PlayerScaleSettings>(ScaleSettingsPath);
            if (settings != null)
                return settings;

            settings = ScriptableObject.CreateInstance<PlayerScaleSettings>();
            AssetDatabase.CreateAsset(settings, ScaleSettingsPath);
            return settings;
        }

        static UnityEngine.Material Material(string name, Color color, string shader = "Standard")
        {
            return MakeMaterial(Folder, name, color, shader);
        }

        static BoxCollider Zone(string name, Vector3 center, Vector3 size)
        {
            var zone = new GameObject(name).AddComponent<BoxCollider>();
            zone.transform.position = center;
            zone.isTrigger = true;
            zone.size = size;
            return zone;
        }

        static TextMeshPro Sign(string name, string label, Vector3 position, float yaw = 0f)
        {
            return LabText.Create(name, label, position, 1.3f, new Color(0.96f, 0.95f, 0.88f), yaw: yaw);
        }

        // One "go here" marker per goal; WarehouseRoute shows the one for the current stage.
        static void CreateMarkers(WarehouseRoute route)
        {
            var settings = LoadOrCreate<MarkerSettings>(MarkerSettingsPath);
            var arrow = LabDecor.Paint("Marker Arrow", settings.ArrowColor, "Unlit/Color");
            var outline = LabDecor.Paint("Marker Outline", new Color(0.08f, 0.09f, 0.11f), "Unlit/Color");
            Transform eyes = route.player.Camera.transform;
            route.ConfigureMarkers(
                Marker("Weight", route.weight, 0.12f, 0f, "Сделай гирю тяжелее себя", settings, eyes, arrow, outline),
                Marker("Vent", route.vent, 0.35f, 0f, "Решётка — курок", settings, eyes, arrow, outline),
                Marker("Box", route.box, 0.1f, 0.6f, "Ящик — в паз", settings, eyes, arrow, outline),
                Marker("Exit", route.exitZone, 0.35f, 0f, "Выход", settings, eyes, arrow, outline),
                GameObject.Find("Exit Door").GetComponent<Collider>());
        }

        static ObjectiveMarker Marker(string name, Collider target, float lift, float shift, string text, MarkerSettings settings,
            Transform eyes, Material arrow, Material outline)
        {
            var root = new GameObject(name + " Marker").transform;
            var chevron = new GameObject("Arrow").transform;
            chevron.SetParent(root, false);
            Chevron(chevron, arrow, 0f, 0f);
            Chevron(chevron, outline, 0.09f, 0.03f);
            var label = LabText.Create("Label", text, Vector3.zero, 3.2f, settings.LabelColor, root,
                TextAlignmentOptions.Center);
            label.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            label.rectTransform.sizeDelta = new Vector2(12f, 1f);
            var marker = root.gameObject.AddComponent<ObjectiveMarker>();
            marker.Configure(settings, target, lift, shift, eyes, chevron, label);
            return marker;
        }

        // A downward chevron with its tip at the origin, in marker-size units; +Z is away from the player.
        static void Chevron(Transform parent, Material material, float grow, float depth)
        {
            const float length = 0.55f;
            const float thickness = 0.14f;
            const float angle = 42f;
            foreach (float side in new[] { -1f, 1f })
            {
                var direction = new Vector3(side * Mathf.Sin(angle * Mathf.Deg2Rad), Mathf.Cos(angle * Mathf.Deg2Rad), 0f);
                var bar = CreateBlock("Chevron Bar", direction * (length * 0.5f) + Vector3.forward * depth,
                    new Vector3(thickness + grow, length + grow, 0.02f), material, parent);
                bar.transform.localRotation = Quaternion.Euler(0f, 0f, -side * angle);
                UnityEngine.Object.DestroyImmediate(bar.GetComponent<Collider>());
                bar.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
    }
}
