using System;
using System.Collections.Generic;
using POV_Unity;
using UnityEngine;

/// <summary>
/// Applies the operations the voice helper returned to the session's layers, through the same
/// server RPC the layer menu uses, and describes what it did for the voice panel.
/// The actions are the ones the helper allows: show, hide and toggle.
/// </summary>
public static class VoiceLayerCommands
{
	/// <summary>
	/// Applies every operation in order and returns one line per operation, or a reason when
	/// nothing was applied. <paramref name="a_layers"/> is the set a command may act on, which
	/// is also the set whose names were sent to the helper.
	/// </summary>
	public static string Apply(VoiceResponse a_response, List<ALayer> a_layers)
	{
		if (!a_response.HasLayerOperations)
		{
			return "nothing to apply";
		}

		if (LayerManager.Instance == null || !LayerManager.Instance.IsSpawned)
		{
			// The RPC needs a spawned LayerManager, so say so rather than throwing on the panel.
			Debug.LogWarning("[Voice] No spawned LayerManager, commands not applied.");
			return "no session, nothing applied";
		}

		List<string> lines = new List<string>();
		foreach (VoiceOperation op in a_response.operations)
		{
			if (op.IsNone)
			{
				continue;
			}

			string line = ApplyOperation(op, a_layers);
			if (!string.IsNullOrEmpty(line))
			{
				lines.Add(line);
			}
		}

		if (lines.Count == 0)
		{
			return "nothing to apply";
		}
		return string.Join("\n", lines);
	}

	private static string ApplyOperation(VoiceOperation a_operation, List<ALayer> a_layers)
	{
		List<ALayer> named = Resolve(a_operation.layers, a_layers);
		if (named.Count == 0)
		{
			Debug.LogWarning($"[Voice] No layer in this session matches '{a_operation}', ignored.");
			return null;
		}

		switch (a_operation.action)
		{
			case "show":
				SetAll(named, VisibleMode());
				return $"shown: {NameList(named)}";
			case "hide":
				SetAll(named, LayerVisualizationMode.Off);
				return $"hidden: {NameList(named)}";
			case "toggle":
				return ApplyToggle(named);
		}

		Debug.LogWarning($"[Voice] Unknown action '{a_operation.action}', ignored.");
		return null;
	}

	private static string ApplyToggle(List<ALayer> a_named)
	{
		List<ALayer> shown = new List<ALayer>();
		List<ALayer> hidden = new List<ALayer>();

		foreach (ALayer layer in a_named)
		{
			if (IsOff(layer))
			{
				Set(layer, VisibleMode());
				shown.Add(layer);
			}
			else
			{
				Set(layer, LayerVisualizationMode.Off);
				hidden.Add(layer);
			}
		}

		List<string> parts = new List<string>();
		if (shown.Count > 0)
		{
			parts.Add($"shown: {NameList(shown)}");
		}
		if (hidden.Count > 0)
		{
			parts.Add($"hidden: {NameList(hidden)}");
		}
		return string.Join("\n", parts);
	}

	// The helper only returns names it was sent, but a name is matched without case so a model
	// that recapitalises one does not silently drop the command. Two layers sharing a short name
	// both match, which is what a speaker naming that short name means.
	private static List<ALayer> Resolve(List<string> a_names, List<ALayer> a_layers)
	{
		List<ALayer> found = new List<ALayer>();

		foreach (string name in a_names)
		{
			foreach (ALayer layer in a_layers)
			{
				bool matches = string.Equals(VoiceClient.DisplayName(layer), name, StringComparison.OrdinalIgnoreCase);
				if (matches && !found.Contains(layer))
				{
					found.Add(layer);
				}
			}
		}
		return found;
	}

	private static void SetAll(List<ALayer> a_layers, LayerVisualizationMode a_mode)
	{
		foreach (ALayer layer in a_layers)
		{
			Set(layer, a_mode);
		}
	}

	private static void Set(ALayer a_layer, LayerVisualizationMode a_mode)
	{
		LayerManager.Instance.SetLayerVisualizationServerRPC(a_layer.LayerIndex, a_mode);
	}

	private static bool IsOff(ALayer a_layer)
	{
		LayerInstanceData data = LayerManager.Instance.GetLayerData(a_layer.LayerIndex);
		return data.m_visualizationMode == LayerVisualizationMode.Off;
	}

	// The mode the layer menu switches a layer on with, so voice and the menu agree on "visible".
	private static LayerVisualizationMode VisibleMode()
	{
		return LayerManager.Instance.LayerVisualisationSetting;
	}

	private static string NameList(List<ALayer> a_layers)
	{
		List<string> names = new List<string>();
		foreach (ALayer layer in a_layers)
		{
			names.Add(VoiceClient.DisplayName(layer));
		}
		return string.Join(", ", names);
	}
}
