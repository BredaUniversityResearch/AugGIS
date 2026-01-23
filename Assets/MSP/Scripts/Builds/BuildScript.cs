#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.XR.OpenXR.Features;


public class BuildScript : MonoBehaviour
{
    [MenuItem("Build/SelectSpaces")]
    static void SelectSpaces()
    {
        ToggleQuest(false);
        ToggleSpaces(true);
    }

    [MenuItem("Build/SelectQuest")]
    static void SelectQuest()
    {
        ToggleSpaces(false);
        ToggleQuest(true);
    }

    static void ToggleQuest(bool enable)
    {
        string platformScene = "Scenes/DeviceSpecific/QuestScene";

        BuildTargetGroup buildTargetGroup = BuildTargetGroup.Android;
        FeatureHelpers.RefreshFeatures(buildTargetGroup);

        string featureSetId = "com.unity.openxr.featureset.meta";
        string[] featureIds = new string[] {
            "UnityEngine.XR.OpenXR.Features.MetaQuestSupport.MetaQuestFeature",
            "UnityEngine.XR.OpenXR.Features.Meta.ARSessionFeature",
            "UnityEngine.XR.OpenXR.Features.Meta.ARAnchorFeature",
            "UnityEngine.XR.OpenXR.Features.Meta.ARCameraFeature",
            "UnityEngine.XR.OpenXR.Features.Meta.ARPlaneFeature",
            "UnityEngine.XR.OpenXR.Features.Meta.ARRaycastFeature",
            "UnityEngine.XR.OpenXR.Features.Meta.DisplayUtilitiesFeature"
        };
        string[] interactionProfileIds = new string[] {
            "com.unity.openxr.feature.input.metaquestpro",
            "com.unity.openxr.feature.input.oculustouch"
        };

        ToggleFeatures(featureSetId, featureIds, buildTargetGroup, interactionProfileIds, enable);
        SetPlatformScenes(platformScene, enable);
        SetPlatformScenes("Scenes/DeviceSpecific/PCScene", false);
    }

    static void ToggleSpaces(bool enable)
    {
        string platformScene = "Scenes/DeviceSpecific/SpacesScene";

        BuildTargetGroup buildTargetGroup = BuildTargetGroup.Android;
        FeatureHelpers.RefreshFeatures(buildTargetGroup);

        string featureSetId = "com.qualcomm.snapdragon.spaces";
        string[] featureIds = new string[] {
            "Qualcomm.Snapdragon.Spaces.BaseRuntimeFeature",
            "Qualcomm.Snapdragon.Spaces.SpatialAnchorsFeature",
            "Qualcomm.Snapdragon.Spaces.PlaneDetectionFeature",
            "Qualcomm.Snapdragon.Spaces.ImageTrackingFeature",
            "QCHT.Interactions.Core.HandTrackingFeature",
            "Qualcomm.Snapdragon.Spaces.HitTestingFeature",
            "Qualcomm.Snapdragon.Spaces.CameraAccessFeature",
            "Qualcomm.Snapdragon.Spaces.SpatialMeshingFeature",
            "Qualcomm.Snapdragon.Spaces.FusionFeature"
        };
        string[] interactionProfileIds = new string[] {
            "com.unity.openxr.feature.input.oculustouch",
            "com.unity.openxr.feature.input.microsoftmotioncontroller"
        };

        ToggleFeatures(featureSetId, featureIds, buildTargetGroup, interactionProfileIds, enable);
        SetPlatformScenes(platformScene, enable);
        SetPlatformScenes("Scenes/DeviceSpecific/PCScene", false);
    }

    static void ToggleFeatures(string featureSetId, string[] featureIds, BuildTargetGroup buildTargetGroup, string[] interactionProfileIds, bool enable)
    {
        var featureSet = OpenXRFeatureSetManager.GetFeatureSetWithId(buildTargetGroup, featureSetId);

        var features = FeatureHelpers.GetFeaturesWithIdsForActiveBuildTarget(featureSet.featureIds);
        featureSet.isEnabled = enable;

        foreach (var interactionProfileId in interactionProfileIds)
        {
            var profile = FeatureHelpers.GetFeatureWithIdForActiveBuildTarget(interactionProfileId);
            profile.enabled = enable;
        }

        foreach (var feature in features)
        {
            if (featureIds.Contains(feature.GetType().ToString()))
            {
                feature.enabled = enable;
            }
        }
    }

    static void SetPlatformScenes(string sceneName, bool enable)
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        foreach (EditorBuildSettingsScene scene in scenes)
        {
            if (scene.path.Contains(sceneName))
            {
                scene.enabled = enable;
            }
        }
        EditorBuildSettings.scenes = scenes;
    }
}
#endif
