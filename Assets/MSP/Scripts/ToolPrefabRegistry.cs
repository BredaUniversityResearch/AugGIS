using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "ToolPrefabRegistry", menuName = "MSP/Tool Prefab Registry")]
public class ToolPrefabRegistry : SerializedScriptableObject
{
	public Dictionary<ToolType, GameObject> prefabs = new Dictionary<ToolType, GameObject>();
}