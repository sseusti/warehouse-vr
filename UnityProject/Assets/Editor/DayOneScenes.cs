using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace Warehouse.Editor
{
    static class DayOneScenes
    {
        public const string TrackingScene = "Assets/Day1/TrackingCheck.unity";
        public const string InteractionScene = "Assets/Day1/InteractionCheck.unity";

        const string AssetFolder = "Assets/Day1";
        const string ActionsPath = AssetFolder + "/XRI Default Input Actions.inputactions";
        const int GrabLayer = 1 << 0;
        const int TeleportLayer = 1 << 1;

        public static void EnsureScenes()
        {
            if (!AssetDatabase.IsValidFolder(AssetFolder))
                AssetDatabase.CreateFolder("Assets", "Day1");
            var actions = ImportStarterActions();
            if (!File.Exists(TrackingScene))
                CreateScene(TrackingScene, actions, interactive: false);
            if (!File.Exists(InteractionScene))
                CreateScene(InteractionScene, actions, interactive: true);

            if (EditorBuildSettings.scenes.Length == 0)
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(InteractionScene, true) };
            AssetDatabase.SaveAssets();
        }

        static InputActionAsset ImportStarterActions()
        {
            if (!File.Exists(ActionsPath))
            {
                var toolkit = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(
                    "Packages/com.unity.xr.interaction.toolkit");
                string source = Path.Combine(toolkit.resolvedPath, "Samples~", "Starter Assets",
                    "XRI Default Input Actions.inputactions");
                File.Copy(source, ActionsPath);
                AssetDatabase.ImportAsset(ActionsPath, ImportAssetOptions.ForceSynchronousImport);
            }

            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
        }

        static void CreateScene(string path, InputActionAsset actions, bool interactive)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var originObject = new GameObject("XR Origin");
            var origin = originObject.AddComponent<XROrigin>();
            var offset = new GameObject("Camera Offset").transform;
            offset.SetParent(originObject.transform, false);
            origin.CameraFloorOffsetObject = offset.gameObject;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.SetParent(offset, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.13f, 0.18f, 0.23f);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 30f;
            cameraObject.AddComponent<AudioListener>();
            origin.Camera = camera;
            AddPoseDriver(cameraObject, actions, "XRI Head");

            var inputManager = originObject.AddComponent<InputActionManager>();
            inputManager.actionAssets = new List<InputActionAsset> { actions };
            CreateEnvironment();
            var cube = CreateBlock("Cube", new Vector3(0f, 0.91f, 0.61f), Vector3.one * 0.22f,
                MakeMaterial("Cube", new Color(1f, 0.6f, 0.14f)));

            if (interactive)
                AddInteractions(origin, offset, actions, cube);

            if (!EditorSceneManager.SaveScene(scene, path))
                throw new InvalidOperationException("Could not save " + path);
        }

        static void CreateEnvironment()
        {
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.transform.rotation = Quaternion.Euler(53f, -23f, 0f);
            RenderSettings.ambientLight = Color.white * 0.6f;

            var floor = MakeMaterial("Floor", new Color(0.34f, 0.38f, 0.43f));
            var table = MakeMaterial("Table", new Color(0.18f, 0.27f, 0.34f));
            CreateBlock("Floor", new Vector3(0f, -0.07f, 1.5f),
                new Vector3(6f, 0.14f, 6f), floor);
            CreateBlock("Tabletop", new Vector3(0f, 0.72f, 0.79f),
                new Vector3(0.85f, 0.12f, 0.58f), table);
            CreateBlock("Table base", new Vector3(0f, 0.33f, 0.79f),
                new Vector3(0.44f, 0.66f, 0.42f), table);
            CreateBlock("Back wall", new Vector3(0f, 1.05f, 4.5f),
                new Vector3(6f, 2.1f, 0.12f), table);
        }

        static void AddInteractions(XROrigin origin, Transform offset, InputActionAsset actions,
            GameObject cube)
        {
            new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            cube.AddComponent<Rigidbody>().collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var grabbable = cube.AddComponent<XRGrabInteractable>();
            grabbable.interactionLayers = GrabLayer;
            grabbable.useDynamicAttach = true;

            var body = origin.gameObject.AddComponent<XRBodyTransformer>();
            body.xrOrigin = origin;
            var mediator = origin.gameObject.AddComponent<LocomotionMediator>();
            var teleport = origin.gameObject.AddComponent<TeleportationProvider>();
            teleport.mediator = mediator;
            var turn = origin.gameObject.AddComponent<SingleSnapTurnProvider>();
            turn.mediator = mediator;
            turn.turnAmount = 45f;
            turn.debounceTime = 0f;
            turn.enableTurnAround = false;
            turn.rightHandTurnInput.inputActionReference = Action(actions, "XRI Right/Thumbstick");

            var leftHand = AddHand(offset, actions, "Left", new Color(0.2f, 0.55f, 0.9f));
            AddHand(offset, actions, "Right", new Color(0.8f, 0.4f, 0.25f));
            AddTeleportRay(leftHand, actions);

            var pad = MakeMaterial("Teleport Pad", new Color(0.08f, 0.67f, 0.55f));
            AddPad("Teleport A", new Vector3(0f, 0.015f, 0f), pad, teleport);
            AddPad("Teleport B", new Vector3(1.65f, 0.015f, 2.35f), pad, teleport);
        }

        static Transform AddHand(Transform parent, InputActionAsset actions, string hand, Color color)
        {
            var handObject = new GameObject(hand + " Controller");
            handObject.transform.SetParent(parent, false);
            AddPoseDriver(handObject, actions, "XRI " + hand);
            var collider = handObject.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 0.12f;
            var body = handObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            var direct = handObject.AddComponent<XRDirectInteractor>();
            direct.interactionLayers = GrabLayer;
            direct.selectInput.inputActionReferencePerformed = Action(actions,
                "XRI " + hand + " Interaction/Select");
            direct.selectInput.inputActionReferenceValue = Action(actions,
                "XRI " + hand + " Interaction/Select Value");

            var handle = CreateBlock(hand + " Handle", Vector3.zero, new Vector3(0.055f, 0.05f, 0.12f),
                MakeMaterial(hand + " Controller", color), handObject.transform);
            UnityEngine.Object.DestroyImmediate(handle.GetComponent<Collider>());
            return handObject.transform;
        }

        static void AddPoseDriver(GameObject target, InputActionAsset actions, string map)
        {
            var driver = target.AddComponent<TrackedPoseDriver>();
            driver.positionInput = new InputActionProperty(Action(actions, map + "/Position"));
            driver.rotationInput = new InputActionProperty(Action(actions, map + "/Rotation"));
            driver.trackingStateInput = new InputActionProperty(Action(actions, map + "/Tracking State"));
        }

        static void AddTeleportRay(Transform hand, InputActionAsset actions)
        {
            var rayObject = new GameObject("Teleport Ray");
            rayObject.transform.SetParent(hand, false);
            var ray = rayObject.AddComponent<XRRayInteractor>();
            ray.interactionLayers = TeleportLayer;
            ray.lineType = XRRayInteractor.LineType.ProjectileCurve;
            ray.velocity = 7f;
            ray.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.State;
            ray.keepSelectedTargetValid = false;
            ray.enableUIInteraction = false;
            var aim = Action(actions, "XRI Left Locomotion/Teleport Mode");
            ray.selectInput.inputActionReferencePerformed = aim;

            var line = rayObject.AddComponent<LineRenderer>();
            line.sharedMaterial = MakeMaterial("Teleport Ray", Color.white, "Sprites/Default");
            var visual = rayObject.AddComponent<XRInteractorLineVisual>();
            visual.lineWidth = 0.012f;
            visual.stopLineAtFirstRaycastHit = true;
            rayObject.AddComponent<TeleportRayVisibility>().Aim = aim;
        }

        static void AddPad(string name, Vector3 position, Material material, TeleportationProvider provider)
        {
            AddPad(name, position, new Vector2(1.05f, 1.05f), material, provider);
        }

        internal static TeleportationArea AddPad(string name, Vector3 position, Vector2 size,
            Material material, TeleportationProvider provider)
        {
            var pad = CreateBlock(name, position, new Vector3(size.x, 0.03f, size.y), material);
            var area = pad.AddComponent<TeleportationArea>();
            area.interactionLayers = TeleportLayer;
            area.teleportationProvider = provider;
            area.teleportTrigger = BaseTeleportationInteractable.TeleportTrigger.OnSelectExited;
            area.filterSelectionByHitNormal = true;
            return area;
        }

        internal static InputActionReference Action(InputActionAsset actions, string path)
        {
            var action = actions.FindAction(path, throwIfNotFound: true);
            return AssetDatabase.LoadAllAssetsAtPath(ActionsPath).OfType<InputActionReference>()
                .First(reference => reference.action?.id == action.id);
        }

        internal static GameObject CreateBlock(string name, Vector3 position, Vector3 size, Material material,
            Transform parent = null)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = size;
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
            return block;
        }

        static Material MakeMaterial(string name, Color color, string shaderName = "Standard")
        {
            return MakeMaterial(AssetFolder, name, color, shaderName);
        }

        internal static Material MakeMaterial(string folder, string name, Color color, string shaderName)
        {
            string path = folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(Shader.Find(shaderName)) { color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
