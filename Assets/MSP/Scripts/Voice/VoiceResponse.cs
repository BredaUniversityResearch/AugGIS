using System;
using System.Collections.Generic;
using Newtonsoft.Json;

/// <summary>One change to layers on the map: show, hide or toggle, and the layers it applies to.</summary>
[Serializable]
public class VoiceOperation
{
	public string action;
	public List<string> layers = new List<string>();

	public override string ToString()
	{
		string layerList = string.Join(", ", layers);
		return $"{action}: {layerList}";
	}
}

/// <summary>A layer from the online catalogue that a search found, as the voice panel lists it.</summary>
[Serializable]
public class VoiceFoundLayer
{
	public string id;
	public string spoken;
	public string group;
	public string name;
	public float meaning;
}

/// <summary>The catalogue layers picked for the speaker, by the map server they are on.</summary>
[Serializable]
public class VoicePicks
{
	public List<VoiceFoundLayer> geoserver = new List<VoiceFoundLayer>();
	public List<VoiceFoundLayer> emodnet = new List<VoiceFoundLayer>();
}

/// <summary>A catalogue layer a search returned, and how close its meaning was to the search words (0 to 1).</summary>
[Serializable]
public class VoiceFoundId
{
	public string id;
	public float meaning;
}

/// <summary>
/// What the interface needs back with the next sentence to continue the same conversation.
/// Each headset holds its own, so each person has their own conversation.
/// </summary>
[Serializable]
public class VoiceConversation
{
	public string chat_key = "";
	public List<VoiceFoundId> found = new List<VoiceFoundId>();
	public string version = "";
	// The line of this conversation's last download, so the model is told how it went.
	public string last_download = "";
}

[Serializable]
public class VoiceSearch
{
	public string query;
	public bool nothing_close;
}

/// <summary>
/// The outcome of one spoken download. The layer is downloaded on the helper and thrown away there,
/// so only the counts and the line to show come back, never the layer itself.
/// </summary>
[Serializable]
public class VoiceDownload
{
	public bool ok;
	public string source;
	public string name;
	public string title;
	// "shapes" or "picture", and "" when the download was refused.
	public string kind;
	public int features;
	public string line;
	public int took_ms;
}

/// <summary>What <c>GET /health</c> on the interface returns: one word per service, and whether all are up.</summary>
[Serializable]
public class VoiceHealth
{
	// "interface" is a C# keyword, so this one field cannot carry the JSON's name.
	[JsonProperty("interface")]
	public string interfaceState;
	public string whisper;
	public string llm;
	public string search;
	public bool ready;
}

/// <summary>
/// What <c>POST /voice</c> on the interface returns for one spoken sentence: what was heard, the
/// changes to apply to the map, the catalogue layers found, the downloads tried, and what to say back.
/// <see cref="VoiceLayerCommands"/> applies the operations to the session's layers.
/// </summary>
[Serializable]
public class VoiceResponse
{
	[Serializable]
	public class Timings
	{
		public int stt;
		public int llm;
		public int download;
		public int total;
	}

	public string transcript;
	public List<VoiceOperation> operations = new List<VoiceOperation>();
	public VoicePicks picks = new VoicePicks();
	public string reply;
	public List<VoiceSearch> searches = new List<VoiceSearch>();
	public List<VoiceDownload> downloads = new List<VoiceDownload>();
	public VoiceConversation conversation = new VoiceConversation();
	public Timings timings_ms;
	public string error;

	public static VoiceResponse FromJson(string a_json)
	{
		VoiceResponse response = JsonConvert.DeserializeObject<VoiceResponse>(a_json);
		if (response == null)
		{
			throw new ArgumentException("Not a VoiceResponse JSON object");
		}

		// A member the JSON leaves out keeps its empty default. A member the JSON gives as null does
		// not, so each list the panel walks through is put back.
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
		if (response.picks == null)
		{
			response.picks = new VoicePicks();
		}
		if (response.picks.geoserver == null)
		{
			response.picks.geoserver = new List<VoiceFoundLayer>();
		}
		if (response.picks.emodnet == null)
		{
			response.picks.emodnet = new List<VoiceFoundLayer>();
		}
		if (response.searches == null)
		{
			response.searches = new List<VoiceSearch>();
		}
		if (response.downloads == null)
		{
			response.downloads = new List<VoiceDownload>();
		}
		if (response.conversation == null)
		{
			response.conversation = new VoiceConversation();
		}
		return response;
	}
}
