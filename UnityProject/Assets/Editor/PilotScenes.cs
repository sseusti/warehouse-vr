using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Warehouse.Editor
{
    public static class PilotScenes
    {
        const string Folder = "Assets/GeneratedPilot";
        public const string MenuPath = Folder + "/MainMenu.unity";

        [MenuItem("Warehouse/Open Pilot Menu")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var paths = Generate();
            EditorBuildSettings.scenes = paths.Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
            EditorSceneManager.OpenScene(MenuPath);
        }

        public static string[] Generate()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "GeneratedPilot");
            DayTwoScene.EnsureScene();
            var scene = EditorSceneManager.OpenScene(DayTwoScene.ScenePath);
            var source = Object.FindFirstObjectByType<WarehouseRoute>();
            var rig = source.player.gameObject;
            var menu = rig.AddComponent<PilotMenu>();
            menu.eye = source.player.Camera;
            menu.hand = source.rightController;
            menu.activate = source.activate;
            menu.pointer = source.aimLine;
            menu.room = DayTwoScene.ScenePath;
            Object.DestroyImmediate(rig.GetComponentInChildren<XRRayInteractor>().gameObject);
            rig.GetComponent<SingleSnapTurnProvider>().enabled = false;
            rig.transform.position = Vector3.zero;
            foreach (var root in scene.GetRootGameObjects())
                if (root != rig && root.name != "XR Interaction Manager" && root.name != "Directional Light")
                    Object.DestroyImmediate(root);
            if (!EditorSceneManager.SaveScene(scene, MenuPath, true))
                throw new System.IO.IOException(MenuPath);
            EditorSceneManager.OpenScene(MenuPath);
            return new[] { MenuPath, DayTwoScene.ScenePath };
        }
    }
}
