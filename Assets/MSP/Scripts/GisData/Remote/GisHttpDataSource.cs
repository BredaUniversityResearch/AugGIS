using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using POV_Unity;
using UnityEngine;
using UnityEngine.Networking;

namespace MSP.Scripts.GisData
{
	/// <summary>
	/// IGisDataSource that assembles a complete GisDataset by fetching the dataset
	/// info and every layer in the given catalogue from the host's GisHttpServer.
	/// The dataset it hands back is identical in shape to the one the other data
	/// sources produce, so it feeds the same texture-loading/Initialise path.
	///
	/// Layers tagged "Invisible" are skipped without fetching (Initialise would
	/// remove them anyway). Layers that fail validation can only be detected after
	/// fetching, so they are still fetched. Any failed request fails the whole
	/// load - no retries, no partial datasets.
	/// </summary>
	public class GisHttpDataSource : IGisDataSource
	{
		private const int RequestTimeoutSeconds = 60;
		private const string InvisibleTag = "Invisible";

		private class FetchResult
		{
			public byte[] Bytes;
			public string Text => Encoding.UTF8.GetString(Bytes);
		}

		private readonly string m_baseUrl;
		private readonly IReadOnlyList<LayerCatalogueEntry> m_catalogue;

		/// <param name="a_baseUrl">e.g. "http://192.168.1.10:50124/"</param>
		public GisHttpDataSource(string a_baseUrl, IReadOnlyList<LayerCatalogueEntry> a_catalogue)
		{
			m_baseUrl = a_baseUrl.EndsWith("/") ? a_baseUrl : a_baseUrl + "/";
			m_catalogue = a_catalogue;
		}

		public IEnumerator LoadDataset(Action<GisDataset> a_onComplete, Action a_onFailure)
		{
			Debug.Log($"[GisHttpDataSource] Loading dataset from {m_baseUrl} ({m_catalogue.Count} catalogue entries)");

			FetchResult datasetResult = new FetchResult();
			yield return Fetch("dataset", datasetResult);
			if (datasetResult.Bytes == null)
			{
				a_onFailure?.Invoke();
				yield break;
			}

			JObject datasetInfo;
			DataConfig config;
			try
			{
				datasetInfo = JObject.Parse(datasetResult.Text);
				config = BuildDataConfig(datasetInfo);
			}
			catch (Exception exception)
			{
				Debug.LogError($"[GisHttpDataSource] Could not parse dataset info: {exception}");
				a_onFailure?.Invoke();
				yield break;
			}
			Debug.Log("[GisHttpDataSource] Dataset info fetched");

			List<RasterLayer> rasterLayers = new List<RasterLayer>();
			List<VectorLayer> vectorLayers = new List<VectorLayer>();
			Dictionary<string, byte[]> rasterImages = new Dictionary<string, byte[]>();

			foreach (LayerCatalogueEntry entry in m_catalogue.OrderBy(a_entry => a_entry.m_sortOrder))
			{
				if (entry.GetTags().Any(a_tag => string.Equals(a_tag, InvisibleTag, StringComparison.InvariantCultureIgnoreCase)))
				{
					Debug.Log($"[GisHttpDataSource] Skipping layer {entry.m_layerId} ({entry.m_name}), tagged {InvisibleTag}");
					continue;
				}

				FetchResult manifestResult = new FetchResult();
				yield return Fetch($"layers/{entry.m_layerId}", manifestResult);
				if (manifestResult.Bytes == null)
				{
					a_onFailure?.Invoke();
					yield break;
				}

				JObject manifest;
				try
				{
					manifest = JObject.Parse(manifestResult.Text);
				}
				catch (Exception exception)
				{
					Debug.LogError($"[GisHttpDataSource] Could not parse manifest of layer {entry.m_layerId}: {exception}");
					a_onFailure?.Invoke();
					yield break;
				}

				//Every asset the manifest lists is fetched, keyed by asset key
				Dictionary<string, byte[]> assets = new Dictionary<string, byte[]>();
				foreach (JToken assetInfo in manifest["assets"] as JArray ?? new JArray())
				{
					string assetKey = (string)assetInfo["key"];
					FetchResult assetResult = new FetchResult();
					yield return Fetch($"layers/{entry.m_layerId}/assets/{assetKey}", assetResult);
					if (assetResult.Bytes == null)
					{
						a_onFailure?.Invoke();
						yield break;
					}
					assets.Add(assetKey, assetResult.Bytes);
				}

				try
				{
					if (entry.m_kind == LayerKind.Raster)
					{
						RasterLayer rasterLayer = LayerManifestReader.BuildRasterLayer(manifest);
						if (!assets.TryGetValue(SqliteLayerPayloadProvider.RasterAssetKey, out byte[] rasterPng))
						{
							throw new InvalidOperationException($"Raster layer {entry.m_layerId} has no \"{SqliteLayerPayloadProvider.RasterAssetKey}\" asset");
						}
						rasterImages[RasterImageKey.Resolve(rasterLayer.data)] = rasterPng;
						rasterLayers.Add(rasterLayer);
					}
					else
					{
						vectorLayers.Add(LayerManifestReader.BuildVectorLayer(manifest));
					}
				}
				catch (Exception exception)
				{
					Debug.LogError($"[GisHttpDataSource] Could not build layer {entry.m_layerId} ({entry.m_name}): {exception}");
					a_onFailure?.Invoke();
					yield break;
				}

				Debug.Log($"[GisHttpDataSource] Layer {entry.m_layerId} ({entry.m_name}) fetched, {assets.Count} asset(s)");
			}

			config.datamodel.raster_layers = rasterLayers.ToArray();
			config.datamodel.vector_layers = vectorLayers.ToArray();

			Debug.Log($"[GisHttpDataSource] Dataset assembled: {rasterLayers.Count} raster, {vectorLayers.Count} vector layers");
			a_onComplete?.Invoke(new GisDataset
			{
				Config = config,
				RasterImages = rasterImages,
				Source = (string)datasetInfo["source"]
			});
		}

		private static DataConfig BuildDataConfig(JObject a_datasetInfo)
		{
			JToken metadata = a_datasetInfo["metadata"];
			return new DataConfig
			{
				metadata = new ConfigMetaData
				{
					data_model_hash = (string)metadata["dataModelHash"],
					date_modified = (string)metadata["dateModified"],
					editor_version = (string)metadata["editorVersion"],
					errors = (int)metadata["errors"]
				},
				datamodel = new DataConfigContent
				{
					projection = (string)a_datasetInfo["projection"],
					coordinate0 = a_datasetInfo["coordinate0"].ToObject<float[]>(),
					coordinate1 = a_datasetInfo["coordinate1"].ToObject<float[]>()
				}
			};
		}

		//On failure the result is left empty (and the error logged)
		private IEnumerator Fetch(string a_path, FetchResult a_result)
		{
			string url = m_baseUrl + a_path;
			using (UnityWebRequest request = UnityWebRequest.Get(url))
			{
				request.timeout = RequestTimeoutSeconds;
				yield return request.SendWebRequest();

				if (request.result != UnityWebRequest.Result.Success)
				{
					Debug.LogError($"[GisHttpDataSource] GET {url} failed: {request.result} ({request.responseCode}) {request.error}");
					yield break;
				}

				a_result.Bytes = request.downloadHandler.data;
			}
		}
	}
}
