using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Builds the voice menu prefab out of the standard MSP UI prefabs (panel, toggle, text) and places
/// one in the Quest scene. Editor menu: MSP/Voice. Command line: -executeMethod VoiceMenuPrefabBuilder.BuildAll
/// </summary>
public static class VoiceMenuPrefabBuilder
{
	private const string REGISTRY_PATH = "Assets/MSP/ScriptableObjects/MSPUIPrefabRegistry.asset";
	private const string PREFAB_PATH = "Assets/MSP/Prefabs/Menus/VoiceMenu.prefab";
	private const string QUEST_SCENE_PATH = "Assets/MSP/Scenes/QuestScene.unity";

	// UI is authored in canvas units and scaled down to metres, like the other world-space menus.
	private const float CANVAS_SCALE = 0.001f;
	private const float PANEL_WIDTH = 320f;
	private const float PANEL_HEIGHT = 230f;

	// Where the panel sits relative to the head: a little right and below the eye line, close by.
	private static readonly Vector3 FOLLOW_OFFSET = new Vector3(0.2f, -0.15f, 0.45f);

	public static void BuildAll()
	{
		BuildPrefab();
		AddToQuestScene();
	}

	[MenuItem("MSP/Voice/Build Voice Menu Prefab")]
	public static void BuildPrefab()
	{
		MSPUIPrefabRegistry registry = AssetDatabase.LoadAssetAtPath<MSPUIPrefabRegistry>(REGISTRY_PATH);
		if (registry == null)
		{
			Debug.LogError($"[Voice] No MSPUIPrefabRegistry at {REGISTRY_PATH}");
			return;
		}

		GameObject root = new GameObject("VoiceMenu", typeof(RectTransform));
		Canvas canvas = root.AddComponent<Canvas>();
		root.AddComponent<TrackedDeviceGraphicRaycaster>();
		LazyFollow follow = root.AddComponent<LazyFollow>();
		root.AddComponent<VoiceRecorder>();
		root.AddComponent<VoiceClient>();
		VoiceMenu voiceMenu = root.AddComponent<VoiceMenu>();

		canvas.renderMode = RenderMode.WorldSpace;
		RectTransform rootRect = root.GetComponent<RectTransform>();
		rootRect.sizeDelta = new Vector2(PANEL_WIDTH, PANEL_HEIGHT);
		rootRect.localScale = Vector3.one * CANVAS_SCALE;

		// No target set: LazyFollow follows the main camera by default.
		follow.targetOffset = FOLLOW_OFFSET;
		follow.movementSpeed = 10f;
		follow.snapOnEnable = true;
		follow.positionFollowMode = LazyFollow.PositionFollowMode.Follow;
		follow.rotationFollowMode = LazyFollow.RotationFollowMode.LookAtWithWorldUp;

		GameObject panel = InstantiatePrefab(registry.MSPPanelPrefab, root.transform, "Panel");
		Stretch(panel.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
		Transform headerText = panel.transform.Find("Header/Header_Text");
		headerText.GetComponent<TMP_Text>().text = "Voice";

		GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
		content.transform.SetParent(panel.transform, false);
		Stretch(content.GetComponent<RectTransform>(), 12f, 12f, 44f, 12f);
		VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
		layout.spacing = 6f;
		layout.childAlignment = TextAnchor.UpperCenter;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;

		CustomXRToggle recordToggle = AddToggle(registry, content, "RecordToggle", "Record", 40f);
		TMP_Text statusText = AddText(registry, content, "Status", "Press Record to speak.", 22f);
		TMP_Text transcriptText = AddText(registry, content, "Transcript", "Heard: -", 60f);
		TMP_Text commandsText = AddText(registry, content, "Commands", "Commands: -", 70f);

		// The fields are private, so assign them the way the inspector would.
		SerializedObject menu = new SerializedObject(voiceMenu);
		menu.FindProperty("m_recordToggle").objectReferenceValue = recordToggle;
		menu.FindProperty("m_statusText").objectReferenceValue = statusText;
		menu.FindProperty("m_transcriptText").objectReferenceValue = transcriptText;
		menu.FindProperty("m_commandsText").objectReferenceValue = commandsText;
		menu.ApplyModifiedPropertiesWithoutUndo();

		PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH, out bool saved);
		Object.DestroyImmediate(root);
		AssetDatabase.SaveAssets();

		if (saved)
		{
			Debug.Log($"[Voice] Prefab written to {PREFAB_PATH}");
		}
		else
		{
			Debug.LogError($"[Voice] Failed to save {PREFAB_PATH}");
		}
	}

	[MenuItem("MSP/Voice/Add Voice Menu To Quest Scene")]
	public static void AddToQuestScene()
	{
		GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
		if (prefab == null)
		{
			Debug.LogError($"[Voice] No prefab at {PREFAB_PATH}, build it first.");
			return;
		}

		Scene scene = EditorSceneManager.OpenScene(QUEST_SCENE_PATH, OpenSceneMode.Single);

		foreach (GameObject rootObject in scene.GetRootGameObjects())
		{
			if (rootObject.GetComponent<VoiceMenu>() != null)
			{
				Debug.Log("[Voice] Quest scene already has a voice menu.");
				return;
			}
		}

		PrefabUtility.InstantiatePrefab(prefab, scene);
		EditorSceneManager.MarkSceneDirty(scene);
		EditorSceneManager.SaveScene(scene);
		Debug.Log($"[Voice] Voice menu added to {QUEST_SCENE_PATH}");
	}

	// Linked prefab instances, so a restyle of the MSP prefabs carries into this menu.
	private static GameObject InstantiatePrefab(GameObject a_prefab, Transform a_parent, string a_name)
	{
		GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(a_prefab, a_parent);
		instance.name = a_name;
		return instance;
	}

	private static CustomXRToggle AddToggle(MSPUIPrefabRegistry a_registry, GameObject a_parent, string a_name, string a_label, float a_height)
	{
		GameObject toggle = InstantiatePrefab(a_registry.MSPTogglePrefab, a_parent.transform, a_name);
		LayoutElement toggleLayout = toggle.AddComponent<LayoutElement>();
		toggleLayout.preferredHeight = a_height;
		toggle.GetComponentInChildren<TMP_Text>(true).text = a_label;
		return toggle.GetComponent<CustomXRToggle>();
	}

	private static TMP_Text AddText(MSPUIPrefabRegistry a_registry, GameObject a_parent, string a_name, string a_value, float a_height)
	{
		GameObject textObject = InstantiatePrefab(a_registry.MSPText, a_parent.transform, a_name);
		LayoutElement textLayout = textObject.AddComponent<LayoutElement>();
		textLayout.preferredHeight = a_height;

		TMP_Text text = textObject.GetComponentInChildren<TMP_Text>(true);
		text.text = a_value;
		text.fontSize = 12f;
		text.textWrappingMode = TextWrappingModes.Normal;
		text.overflowMode = TextOverflowModes.Truncate;
		text.alignment = TextAlignmentOptions.TopLeft;
		return text;
	}

	private static void Stretch(RectTransform a_rect, float a_left, float a_right, float a_top, float a_bottom)
	{
		a_rect.anchorMin = Vector2.zero;
		a_rect.anchorMax = Vector2.one;
		a_rect.offsetMin = new Vector2(a_left, a_bottom);
		a_rect.offsetMax = new Vector2(-a_right, -a_top);
	}
}
