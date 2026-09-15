using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>One step returned by the voice helper: an action and the layers it applies to.</summary>
[Serializable]
public class VoiceOperation
{
	public string action;
	public List<string> layers = new List<string>();

	/// <summary>True when there is nothing to apply: no action, the action "none", or no layers.</summary>
	public bool IsNone
	{
		get
		{
			bool hasAction = !string.IsNullOrEmpty(action) && action != "none";
			bool hasLayers = layers != null && layers.Count > 0;
			return !hasAction || !hasLayers;
		}
	}

	public override string ToString()
	{
		if (IsNone)
		{
			return "none";
		}
		string layerList = string.Join(", ", layers);
		return $"{action}: {layerList}";
	}
}

/// <summary>
/// What <c>POST /command</c> on the voice helper returns: the transcript and a list of operations.
/// The operations are shown, not applied; applying them to layers is a later step.
/// </summary>
[Serializable]
public class VoiceResponse
{
	[Serializable]
	public class Timings
	{
		public int stt;
		public int llm;
		public int total;
	}

	public string transcript;
	public List<VoiceOperation> operations = new List<VoiceOperation>();
	public Timings timings_ms;
	public string error;

	public bool HasLayerOperations
	{
		get
		{
			foreach (VoiceOperation op in operations)
			{
				if (!op.IsNone)
				{
					return true;
				}
			}
			return false;
		}
	}

	public static VoiceResponse FromJson(string a_json)
	{
		VoiceResponse response = JsonUtility.FromJson<VoiceResponse>(a_json);
		if (response == null)
		{
			throw new ArgumentException("Not a VoiceResponse JSON object");
		}
		if (response.operations == null)
		{
			response.operations = new List<VoiceOperation>();
		}
		foreach (VoiceOperation op in response.operations)
		{
			if (op.layers == null)
			{
				op.layers = new List<string>();
			}
		}
		return response;
	}

	/// <summary>One line per operation, for display. "no layer command" when there is none.</summary>
	public string DescribeOperations()
	{
		if (!HasLayerOperations)
		{
			return "no layer command";
		}

		StringBuilder sb = new StringBuilder();
		foreach (VoiceOperation op in operations)
		{
			if (op.IsNone)
			{
				continue;
			}
			if (sb.Length > 0)
			{
				sb.Append('\n');
			}
			sb.Append(op);
		}
		return sb.ToString();
	}
}
