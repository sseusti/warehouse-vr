using System;
using System.IO;
using System.Linq;
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

            var floor = Material("Floor", new Color(0.35f, 0.39f, 0.42f));
            var wall = Material("Wall", new Color(0.26f, 0.32f, 0.37f));
            var steel = Material("Platform", new Color(0.35f, 0.51f, 0.64f));
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
            route.ventPad = AddPad("Vent Approach", new Vector3(-3.85f, 0.015f, 0.8f),
                new Vector2(1.35f, 1.05f), pad, route.teleport);
            var plate = CreateBlock("Pressure Plate", new Vector3(-3.85f, 0.055f, -0.38f),
                new Vector3(0.8f, 0.11f, 0.75f), steel);
            route.weight = CreateBlock("Weight", new Vector3(-3.85f, 0.34f, -0.38f),
                new Vector3(0.51f, 0.46f, 0.51f), target).GetComponent<Collider>();
            var weightSign = Sign("Weight Sign", "WEIGHT\nRIGHT TRIGGER", new Vector3(-3.85f, 0.69f, -0.64f));
            ConfigureWeightPuzzle(route, plate, weightSign);

            route.vent = CreateBlock("Vent Recess", new Vector3(-3.85f, 0.44f, 2.72f),
                new Vector3(0.86f, 0.78f, 0.10f), steel).GetComponent<Collider>();
            CreateBlock("Vent Left Frame", new Vector3(-4.37f, 0.45f, 2.45f),
                new Vector3(0.18f, 0.9f, 0.58f), wall);
            CreateBlock("Vent Right Frame", new Vector3(-3.33f, 0.45f, 2.45f),
                new Vector3(0.18f, 0.9f, 0.58f), wall);
            CreateBlock("Vent Top Frame", new Vector3(-3.85f, 0.88f, 2.45f),
                new Vector3(1.2f, 0.18f, 0.58f), wall);
            route.grate = new GameObject("Closed Grate");
            route.grate.transform.position = new Vector3(-3.85f, 0.43f, 2.16f);
            for (var index = -2; index <= 2; index++)
            {
                CreateBlock("Grate Bar", new Vector3(index * 0.18f, 0f, 0f),
                    new Vector3(0.08f, 0.77f, 0.1f), steel, route.grate.transform);
            }
            Sign("Vent Sign", "VENT\nRIGHT TRIGGER", new Vector3(-3.85f, 1.18f, 2.13f));

            CreateBlock("Shelf", new Vector3(-1.6f, PlatformHeight - 0.12f, 1.5f),
                new Vector3(2.2f, 0.24f, 2f), steel);
            route.shelfArrival = new GameObject("Shelf Arrival").transform;
            route.shelfArrival.SetPositionAndRotation(new Vector3(-1.1f, PlatformHeight + 0.01f, 1f),
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
            route.exitSign = Sign("Exit Sign", "EXIT", new Vector3(4.02f, 3.82f, 2.35f));

            if (!EditorSceneManager.SaveScene(scene, ScenePath, saveAsCopy: true))
                throw new InvalidOperationException("Could not save " + ScenePath);

            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ScenePath);
        }

        internal static void ConfigureWeightPuzzle(WarehouseRoute route, GameObject plate, TextMesh weightSign)
        {
            Vector3 center = plate.transform.position;
            plate.transform.position = center - Vector3.up * 0.03f;
            plate.transform.localScale = new Vector3(0.8f, 0.05f, 0.75f);
            var button = CreateBlock("Pressure Button", center + Vector3.up * 0.025f,
                new Vector3(0.68f, 0.06f, 0.63f), Material("Pressure Button", new Color(0.20f, 0.65f, 0.82f)));

            var weight = route.weight.gameObject.AddComponent<Rigidbody>();
            weight.mass = 8f;
            weight.isKinematic = true;
            weight.useGravity = false;
            route.pressurePlate = plate.AddComponent<WeightPressurePlate>();
            route.pressurePlate.Configure(weight, button.transform);
            weightSign.text = "SHRINK\nRIGHT TRIGGER";
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

        static TextMesh Sign(string name, string label, Vector3 position, float yaw = 0f)
        {
            var text = new GameObject(name).AddComponent<TextMesh>();
            text.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 48;
            text.characterSize = 0.028f;
            text.text = label;
            text.color = new Color(0.96f, 0.95f, 0.88f);
            return text;
        }
    }
}
