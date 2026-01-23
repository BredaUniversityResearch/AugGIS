using System;
using UnityEngine;

[CreateAssetMenu(fileName = "MSPUIPrefabRegistry", menuName = "MSP/UI Prefab Registry")]
public class MSPUIPrefabRegistry : ScriptableObject
{
#if UNITY_EDITOR
	public GameObject MSPButtonPrefab;
	public GameObject MSPTogglePrefab;
	public GameObject MSPPanelPrefab;
	public GameObject MSPTextInputFieldPrefab;
	public GameObject MSPHeading;
	public GameObject MSPSubHeading;
	public GameObject MSPText;
	public GameObject MSPSelectionCarousel;
	public GameObject MSPToggleList;
	public GameObject MSPTextField;
	public GameObject MSPLoadingBar;
#endif
}
