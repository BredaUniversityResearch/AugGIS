using Newtonsoft.Json.Linq;

namespace MSP.Scripts.GisData
{
	/// <summary>
	/// A binary payload belonging to a layer, served as-is by GisHttpServer.
	/// </summary>
	public class LayerAsset
	{
		public string ContentType;
		public byte[] Bytes;
	}

	/// <summary>
	/// The seam between GisHttpServer's fixed HTTP API and wherever layer content
	/// actually lives. The API is the same for every layer kind:
	///  - a manifest (JObject): common layer fields (id, kind, name, short, tags,
	///    types, sortOrder), a kind-specific "data" object the protocol treats as
	///    opaque, and an "assets" array of { key, contentType, length, meta? }
	///  - assets: raw bytes addressed by key
	/// Implementations are called from thread-pool threads, so they must not touch
	/// Unity API or share unsynchronised state.
	/// </summary>
	public interface ILayerPayloadProvider
	{
		bool TryGetManifest(int a_layerId, out JObject a_manifest);
		bool TryGetAsset(int a_layerId, string a_assetKey, out LayerAsset a_asset);
	}
}
