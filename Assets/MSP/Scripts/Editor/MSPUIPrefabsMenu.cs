using UnityEditor;
using UnityEngine;

public static class MSPUIPrefabsMenu
{
	private const int MenuPriority = 1;

	private const string PrefabRegistryPath = "Assets/MSP/ScriptableObjects/MSPUIPrefabRegistry.asset";

	private static MSPUIPrefabRegistry MSPUIPrefabRegistry => AssetDatabase.LoadAssetAtPath<MSPUIPrefabRegistry>(PrefabRegistryPath);

	[MenuItem("GameObject/MSP/UI/Button", priority = MenuPriority)]
	private static void CreateButton()
	{
		MSPUIPrefabRegistry registry = MSPUIPrefabRegistry;
		SpawnUIGameObject(registry,registry.MSPButtonPrefab);
	}

	[MenuItem("GameObject/MSP/UI/Toggle", priority = MenuPriority)]
	private static void CreateToggle()
	{
		MSPUIPrefabRegistry registry = MSPUIPrefabRegistry;
		SpawnUIGameObject(registry, registry.MSPTogglePrefab);
	}

	[MenuItem("GameObject/MSP/UI/Panel", priority = MenuPriority)]
	private static void CreatePanel()
	{
		MSPUIPrefabRegistry registry = MSPUIPrefabRegistry;
		SpawnUIGameObject(registry, registry.MSPPanelPrefab);
	}

	[MenuItem("GameObject/MSP/UI/Text Input Field", priority = MenuPriority)]
	private static void CreateInputField()
	{
		MSPUIPrefabRegistry registry = MSPUIPrefabRegistry;
		SpawnUIGameObject(registry, registry.MSPTextInputFieldPrefab);
	}

	[MenuItem("GameObject/MSP/UI/Heading", priority = MenuPriority)]
	private static void CreateHeading()
	{
		MSPUIPrefabRegistry registry = MSPUIPrefabRegistry;
		SpawnUIGameObject(registry, registry.MSPHeading);
	}

	[MenuItem("GameObject/MSP/UI/Sub Heading", priority = MenuPriority)]
	private static void CreateSubHeading()
	{
		MSPUIPrefabRegistry registry = MSPUIPrefabRegistry;
		SpawnUIGameObject(registry, registry.MSPSubHeading);
	}

	[MenuItem("GameObject/MSP/UI/Text Box", priority = MenuPriority)]
	private static void CreateTextBox()
	{
		MSPUIPrefabRegistry registry = MSPUIPrefabRegistry;
		SpawnUIGameObject(registry, registry.MSPText);
	}

	[MenuItem("GameObject/MSP/UI/Selection Carousel", priority = MenuPriority)]
	private static void CreateSelectionCarousel()
	{
		MSPUIPrefabRegistry registry = MSPUIPrefabRegistry;
		SpawnUIGameObject(registry, registry.MSPSelectionCarousel);
	}

	[MenuItem("GameObject/MSP/UI/Toggleable List", priority = MenuPriority)]
	private static void CreateToggleList()
	{
		MSPUIPrefabRegistry registry = MSPUIPrefabRegistry;
		SpawnUIGameObject(registry, registry.MSPToggleList);
	}

	[MenuItem("GameObject/MSP/UI/Text Field", priority = MenuPriority)]
	private static void CreateTextField()
	{
		MSPUIPrefabRegistry registry = MSPUIPrefabRegistry;
		SpawnUIGameObject(registry, registry.MSPTextField);
	}

	[MenuItem("GameObject/MSP/UI/Loading Bar", priority = MenuPriority)]
	private static void CreateLoadingBar()
	{
		MSPUIPrefabRegistry registry = MSPUIPrefabRegistry;
		SpawnUIGameObject(registry, registry.MSPLoadingBar);
	}

	private static void SpawnUIGameObject(MSPUIPrefabRegistry registry, GameObject gameObject)
	{	
		if(registry == null)
		{
			EditorUtility.DisplayDialog("Error!", "Can not spawn MSP UI element, MSPUIPrefabRegistry Not Found!", "Ok");
			return;
		}

		if (gameObject == null)
		{
			EditorUtility.DisplayDialog("Error!", "Can not spawn MSP UI element, Prefab reference was not assigned to MSPUIPrefabRegistry!", "Ok");
			return;
		}

		bool selectionHasCanvas = Selection.activeGameObject && Selection.activeGameObject.GetComponentInParent<Canvas>();
		
		if(!selectionHasCanvas)
		{
			EditorUtility.DisplayDialog("Error!","Can not spawn MSP UI elements on a object without canvas component attached", "Ok");
			return;
		}

		Object objectInstance = PrefabUtility.InstantiatePrefab(gameObject, Selection.activeTransform);
		PrefabUtility.UnpackPrefabInstance((GameObject)objectInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
		Undo.RegisterCreatedObjectUndo(objectInstance, $"Create {objectInstance.name}");
		Selection.activeObject = objectInstance;
	}
}
