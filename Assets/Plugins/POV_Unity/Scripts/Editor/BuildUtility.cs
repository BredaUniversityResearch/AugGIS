using UnityEditor;
using System.Linq;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;
using System;
using UnityEngine.XR.OpenXR.Features.Meta;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;

class BuildUtility
{
	private static void WindowsDevBuilder()
	{
		string fullBinaryBuildPath = GetArg("-customBuildPath");
		BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, fullBinaryBuildPath, BuildTarget.StandaloneWindows64, BuildOptions.Development);
	}

	private static void MacOSDevBuilder()
	{
		string fullBinaryBuildPath = GetArg("-customBuildPath");
		BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, fullBinaryBuildPath, BuildTarget.StandaloneOSX, BuildOptions.Development);
	}

	private static void AndroidDevBuilder()
	{
		string fullBinaryBuildPath = GetArg("-customBuildPath");
		BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, fullBinaryBuildPath, BuildTarget.Android, BuildOptions.Development);
	}

	private static void IOSDevBuilder()
	{
		string fullBinaryBuildPath = GetArg("-customBuildPath");
		BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, fullBinaryBuildPath, BuildTarget.iOS, BuildOptions.Development);
	}

	private static void WindowsBuilder()
	{
		string fullBinaryBuildPath = GetArg("-customBuildPath");
		BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, fullBinaryBuildPath, BuildTarget.StandaloneWindows64, 0);
	}

	private static void MacOSBuilder()
	{
		string fullBinaryBuildPath = GetArg("-customBuildPath");
		BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, fullBinaryBuildPath, BuildTarget.StandaloneOSX, 0);
	}

	private static void AndroidBuilder()
	{
		string fullBinaryBuildPath = GetArg("-customBuildPath");
		BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, fullBinaryBuildPath, BuildTarget.Android, 0);
	}
	private static void MetaQuestBuilder()
	{
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

		ToggleFeatures(featureSetId, featureIds, buildTargetGroup, interactionProfileIds, true);

		string fullBinaryBuildPath = GetArg("-customBuildPath");
		BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, fullBinaryBuildPath, BuildTarget.Android, 0);
	}


	private static void IOSBuilder()
	{
		string fullBinaryBuildPath = GetArg("-customBuildPath");
		BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, fullBinaryBuildPath, BuildTarget.iOS, 0);
	}

	private static void UnityServerBuilder()
	{
		var scenes = EditorBuildSettings.scenes.Select(x => x.path).ToArray();
		var fullBinaryBuildPath = GetArg("-customBuildPath");
		var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions() {
			locationPathName = fullBinaryBuildPath,
			scenes = scenes,
			target = BuildTarget.StandaloneLinux64,
			subtarget = (int)StandaloneBuildSubtarget.Server,
			extraScriptingDefines = new string[] { "DEDICATED_SERVER", "SHAPES_URP" },
		});

		if (report.summary.result == BuildResult.Succeeded)
		{
			Debug.Log("***** Build Success *******");
		}
		else if (report.summary.result == BuildResult.Failed)
		{
			Debug.LogError("******* Build Failed *******");
		}
	}

	private static string GetArg(string name)
	{
		var args = System.Environment.GetCommandLineArgs();
		for (int i = 0; i < args.Length; i++)
		{
			if (args[i] == name && args.Length > i + 1)
			{
				return args[i + 1];
			}
		}
		return null;
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
}
