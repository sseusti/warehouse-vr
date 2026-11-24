using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.XR.PXR;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace Warehouse.Editor
{
    public static class PicoAndroidBuild
    {
        const BuildTargetGroup Android = BuildTargetGroup.Android;
        const string SettingsPath = "Assets/XR/Android XR Settings.asset";
        const string ApplicationId = "com.warehouse.vrpilot";

        [MenuItem("Warehouse/Prepare Pico 4 Test Scenes")]
        public static void Prepare()
        {
            if (!BuildPipeline.IsBuildTargetSupported(Android, BuildTarget.Android))
                throw new BuildFailedException("Install Unity Android Build Support before configuring Pico 4.");

            ConfigurePlayer();
            ConfigureOpenXR();
            var platform = PXR_PlatformSetting.Instance;
            if (platform.appID == null)
            {
                platform.appID = string.Empty;
                EditorUtility.SetDirty(platform);
            }
            DayOneScenes.EnsureScenes();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Warehouse/Build Tracking Check APK")]
        public static void BuildTrackingCheck()
        {
            Build(DayOneScenes.TrackingScene, "Warehouse-Tracking.apk");
        }

        [MenuItem("Warehouse/Build Interaction Check APK")]
        public static void BuildInteractionCheck()
        {
            Build(DayOneScenes.InteractionScene, "Warehouse-Day1.apk");
        }

        [MenuItem("Warehouse/Build Day 2 Warehouse APK")]
        public static void BuildWarehouse()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode && !Application.isBatchMode &&
                !UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            DayTwoScene.EnsureScene();
            Build(DayTwoScene.ScenePath, "Warehouse-Day2.apk", ApplicationId + ".day2");
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.productName = "Warehouse VR";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Android,
                Il2CppCompilerConfiguration.Debug);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            EditorUserBuildSettings.buildAppBundle = false;

            var settings = Resources.FindObjectsOfTypeAll<PlayerSettings>().First();
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("activeInputHandler").intValue = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigureOpenXR()
        {
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey,
                    out XRGeneralSettingsPerBuildTarget general))
            {
                Directory.CreateDirectory("Assets/XR");
                AssetDatabase.Refresh();
                general = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(general, SettingsPath);
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, general, true);
            }

            if (!general.HasManagerSettingsForBuildTarget(Android))
                general.CreateDefaultManagerSettingsForBuildTarget(Android);
            var settings = general.SettingsForBuildTarget(Android);
            settings.InitManagerOnStart = true;
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(general);

            if (!settings.Manager.activeLoaders.Any(loader => loader is OpenXRLoader) &&
                !XRPackageMetadataStore.AssignLoader(settings.Manager, typeof(OpenXRLoader).FullName, Android))
            {
                throw new BuildFailedException("Could not enable the Android OpenXR loader.");
            }

            FeatureHelpers.RefreshFeatures(Android);
            EnableFeature("com.unity.openxr.feature.pico");
            EnableFeature("com.unity.openxr.pico.features");
            EnableFeature("com.unity.openxr.feature.input.PICO4touch");
            var openXR = OpenXRSettings.GetSettingsForBuildTargetGroup(Android);
            openXR.renderMode = OpenXRSettings.RenderMode.MultiPass;
            EditorUtility.SetDirty(openXR);
        }

        static void EnableFeature(string id)
        {
            var feature = FeatureHelpers.GetFeatureWithIdForBuildTarget(Android, id);
            if (feature == null)
                throw new BuildFailedException("Missing OpenXR feature: " + id);

            feature.enabled = true;
            EditorUtility.SetDirty(feature);
        }

        static void Build(string scene, string filename, string applicationId = ApplicationId)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new BuildFailedException("Switch to Android before building, or pass -buildTarget Android.");

            Prepare();
            ValidateOpenXR();
            Directory.CreateDirectory("Builds");
            var oldProductName = PlayerSettings.productName;
            var oldApplicationId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            BuildReport report;
            try
            {
                if (applicationId != ApplicationId)
                    PlayerSettings.productName = "Warehouse VR Day 2";
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, applicationId);
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { scene },
                    locationPathName = Path.GetFullPath(Path.Combine("Builds", filename)),
                    target = BuildTarget.Android,
                    options = BuildOptions.Development,
                });
            }
            finally
            {
                PlayerSettings.productName = oldProductName;
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, oldApplicationId);
            }

            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("APK build ended with " + report.summary.result);
            Debug.Log($"APK: {report.summary.outputPath} ({report.summary.totalSize} bytes)");
        }

        static void ValidateOpenXR()
        {
            var issues = new List<OpenXRFeature.ValidationRule>();
            OpenXRProjectValidation.GetCurrentValidationIssues(issues, Android);
            foreach (var issue in issues.Where(issue => !issue.error))
                Debug.LogWarning("OpenXR: " + issue.message);

            var errors = issues.Where(issue => issue.error).Select(issue => issue.message).ToArray();
            if (errors.Length != 0)
                throw new BuildFailedException("OpenXR validation: " + string.Join("; ", errors));
        }
    }
}
