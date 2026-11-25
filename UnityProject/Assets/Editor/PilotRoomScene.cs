using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using static Warehouse.Editor.DayOneScenes;

namespace Warehouse.Editor
{
    static class PilotRoomScene
    {
        public const string Path = "Assets/GeneratedPilot/TestRoom.unity";
        const string Art = "Assets/PilotArt/";
        static GameObject Named(string name) => GameObject.Find(name);

        public static InteractionFeedback AddFeedback(GameObject owner, InputActionManager input)
        {
            var feedback = owner.AddComponent<InteractionFeedback>();
            var actions = input.actionAssets[0];
            feedback.leftHaptic = Action(actions, "XRI Left/Haptic Device");
            feedback.rightHaptic = Action(actions, "XRI Right/Haptic Device");
            feedback.click = AssetDatabase.LoadAssetAtPath<AudioClip>(Art + "Click.wav");
            feedback.grow = AssetDatabase.LoadAssetAtPath<AudioClip>(Art + "Grow.wav");
            feedback.shrink = AssetDatabase.LoadAssetAtPath<AudioClip>(Art + "Shrink.wav");
            feedback.dock = AssetDatabase.LoadAssetAtPath<AudioClip>(Art + "Dock.wav");
            return feedback;
        }

        public static void Generate()
        {
            var scene = EditorSceneManager.OpenScene(DayTwoScene.ScenePath);
            var old = Object.FindFirstObjectByType<WarehouseRoute>();
            var room = old.gameObject.AddComponent<PilotRoom>();
            var rig = old.player;
            var input = rig.GetComponent<InputActionManager>();
            room.walker = rig.gameObject.AddComponent<RoomWalker>();
            room.walker.rig = rig;
            room.walker.stick = Action(input.actionAssets[0], "XRI Left/Thumbstick");
            room.feedback = AddFeedback(rig.gameObject, input);
            room.leftHand = old.leftController;
            room.rightHand = old.rightController;
            room.pointer = old.aimLine;
            room.activate = old.activate;
            room.shelfArrival = old.shelfArrival;
            room.dock = old.dockZone;
            room.exit = old.exitZone;
            room.balcony = Named("Balcony").GetComponent<BoxCollider>();
            room.cargo = old.box.attachedRigidbody;
            room.cargo.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ;
            room.cargoSurface = old.box.GetComponent<Renderer>();
            room.button = Named("Pressure Button").transform;
            room.grate = old.grate;
            room.weight = SizeObject(old.weight.gameObject, rig.Camera);
            room.cargoSize = SizeObject(old.box.gameObject, rig.Camera);
            room.cargoSize.keepBaseHeight = false;
            Object.DestroyImmediate(old.pressurePlate);
            Object.DestroyImmediate(rig.GetComponentInChildren<XRRayInteractor>().gameObject);
            Object.DestroyImmediate(old.teleport);
            Object.DestroyImmediate(old.startPad.gameObject);
            Object.DestroyImmediate(old.ventPad.gameObject);
            Object.DestroyImmediate(old.balconyPad.gameObject);
            Object.DestroyImmediate(Named("Weight Sign"));
            Object.DestroyImmediate(Named("Box Sign"));
            Named("Vent Sign").GetComponent<TextMesh>().text = "ВЕНТИЛЯЦИЯ";
            ResizeBlock("Vent Left Frame", new Vector3(-4.245f,.39f,2.45f), new Vector3(.20f,.78f,.58f));
            ResizeBlock("Vent Right Frame", new Vector3(-3.455f,.39f,2.45f), new Vector3(.20f,.78f,.58f));
            ResizeBlock("Vent Top Frame", new Vector3(-3.85f,.81f,2.45f), new Vector3(.99f,.18f,.58f));
            room.grate.transform.localScale = new Vector3(.77f,.9f,1);
            room.entrance = new GameObject("Vent Entrance").AddComponent<BoxCollider>();
            room.entrance.transform.position = new Vector3(-3.85f,.32f,2.38f);
            room.entrance.size = new Vector3(.48f,.7f,.28f);
            room.entrance.isTrigger = true;

            var floor = Surface("Concrete Floor","Concrete",new Color(.47f,.49f,.50f),new Vector2(8,5));
            var walls = Surface("Panel Walls","WallPanel",new Color(.32f,.42f,.47f),new Vector2(7,3));
            var deck = Surface("Steel Deck","Deck",new Color(.38f,.55f,.61f),new Vector2(4,3));
            var edge = Surface("Safety Edge","Deck",new Color(.94f,.64f,.18f),new Vector2(4,1));
            Named("Warehouse Floor").GetComponent<Renderer>().sharedMaterial = floor;
            foreach (string wall in new[]{ "Front Wall","Back Wall","East Wall","West Wall" })
                Named(wall).GetComponent<Renderer>().sharedMaterial = walls;
            Named("Shelf").GetComponent<Renderer>().sharedMaterial = deck;
            room.balcony.GetComponent<Renderer>().sharedMaterial = deck;
            BuildWalkway(room,deck,edge);
            var crate = CreateBlock("Carry Crate",new Vector3(-1.55f,1.79f,1.87f), Vector3.one*.28f,
                Surface("Carry Crate","WallPanel",new Color(.32f,.69f,.67f),Vector2.one));
            crate.AddComponent<Rigidbody>().mass = 4;
            room.crate = SizeObject(crate,rig.Camera);
            room.grab = crate.AddComponent<StrengthGrab>();
            room.grab.player = room.walker;
            room.grab.leftHand = room.leftHand;
            room.grab.feedback = room.feedback;
            room.grab.useDynamicAttach = true;
            room.grab.interactionLayers = 1;
            Object.DestroyImmediate(old);
            if (!EditorSceneManager.SaveScene(scene, Path, true)) throw new IOException(Path);
        }

        static Material Surface(string name, string texture, Color tint, Vector2 tiles)
        {
            var mat = MakeMaterial("Assets/GeneratedPilot", name, tint, "Standard");
            mat.color = tint;
            mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + texture + ".png");
            mat.mainTextureScale = tiles;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static VariableMass SizeObject(GameObject item, Camera eye)
        {
            var size = item.AddComponent<VariableMass>();
            size.keepBaseHeight = true;
            var readout = new GameObject(item.name + " Mass").AddComponent<MassReadout>();
            readout.target = size;
            readout.eye = eye;
            readout.shape = item.GetComponent<Collider>();
            readout.label = readout.gameObject.AddComponent<TextMesh>();
            var label = readout.label;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<Renderer>().sharedMaterial = label.font.material;
            label.anchor = TextAnchor.MiddleCenter;
            label.fontSize = 48;
            label.characterSize = .015f;
            return size;
        }

        static void ResizeBlock(string name, Vector3 position, Vector3 size)
        {
            var block = Named(name).transform;
            block.position = position;
            block.localScale = size;
        }

        static void BuildWalkway(PilotRoom room, Material deck, Material edge)
        {
            const float top = 1.65f;
            room.bridge = new GameObject("Retractable Bridge");
            CreateBlock("Bridge Deck",new Vector3(1.305f,top-.065f,1.75f),new Vector3(3.65f,.13f,1.04f),deck,room.bridge.transform);
            void Rail(string name,float x,float z,float width,float depth, Transform parent = null)
            {
                var rail = CreateBlock(name,new Vector3(x,top+.085f,z),new Vector3(width,.17f,depth),edge,parent);
                // Keep the low lip solid for walking and let the remote aim over it.
                rail.layer = LayerMask.NameToLayer("Ignore Raycast");
            }
            Rail("Bridge North Lip",1.3f,2.25f,3.66f,.05f,room.bridge.transform);
            Rail("Bridge South Lip",1.3f,1.25f,3.66f,.05f,room.bridge.transform);
            room.bridge.SetActive(false);
            room.gate = CreateBlock("Bridge Gate",new Vector3(-.50f,top+.25f,1.75f),new Vector3(.07f,.5f,1.06f),edge);
            Rail("Shelf West Lip",-2.67f,1.5f,.06f,2);
            Rail("Shelf North Lip",-1.6f,2.48f,2.2f,.06f);
            Rail("Shelf South Lip",-1.6f,.52f,2.2f,.06f);
            Rail("Shelf East Lip",-.52f,.87f,.06f,.76f);
            Rail("Shelf Corner Lip",-.52f,2.37f,.06f,.27f);
            Rail("Balcony South Lip",4.02f,-2.36f,1.81f,.08f);
            Rail("Balcony West Lip",3.14f,-.57f,.06f,3.65f);
            Rail("Balcony Corner Lip",3.14f,2.33f,.06f,.20f);
            Rail("Balcony East Lip",4.89f,0,.06f,4.8f);
            Rail("Balcony North Lip",4.02f,2.37f,1.82f,.06f);
        }
    }
}
