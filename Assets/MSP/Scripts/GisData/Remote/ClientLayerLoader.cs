using System;
using System.Collections;
using System.Collections.Generic;
using POV_Unity;
using UnityEngine;

namespace MSP.Scripts.GisData
{
	/// <summary>
	/// Client side: loads layers the host announces through the LayerCatalogue after
	/// this client's initial load. Compares the catalogue against the ids it has
	/// already handled (loaded, skipped as Invisible, or failed) whenever the initial
	/// import completes or an entry is added, and fetches the unknown ones one at a
	/// time into the running session.
	///
	/// A layer that fails to load is logged, reported to the host and skipped - no
	/// retry and no disconnect.
	/// </summary>
	public class ClientLayerLoader
	{
		private readonly MonoBehaviour m_runner;
		private readonly GisHttpDataSource m_dataSource;
		private readonly Action<int, string> m_onLayerFailed;
		private readonly HashSet<int> m_handledIds = new HashSet<int>();
		private readonly Queue<LayerCatalogueEntry> m_queue = new Queue<LayerCatalogueEntry>();
		private bool m_busy;
		private bool m_stopped;
		private bool m_started;

		/// <param name="a_initialCatalogue">The catalogue snapshot the initial load used;
		/// those layers count as handled.</param>
		/// <param name="a_onLayerFailed">Called with the layer id and a reason when a layer
		/// could not be added on this client.</param>
		public ClientLayerLoader(MonoBehaviour a_runner, string a_baseUrl, IReadOnlyList<LayerCatalogueEntry> a_initialCatalogue, Action<int, string> a_onLayerFailed)
		{
			m_runner = a_runner;
			m_onLayerFailed = a_onLayerFailed;
			m_dataSource = new GisHttpDataSource(a_baseUrl, a_initialCatalogue);

			foreach (LayerCatalogueEntry entry in a_initialCatalogue)
			{
				m_handledIds.Add(entry.m_layerId);
			}
		}

		/// <summary>Call before the initial Initialise; starts working once its import completes.</summary>
		public void Start()
		{
			if (m_started)
			{
				return;
			}
			m_started = true;
			ImportedConfigRoot.Instance.m_onImportComplete += OnInitialImportComplete;
			LayerCatalogue.Instance.OnEntryAdded += OnCatalogueEntryAdded;
		}

		public void Stop()
		{
			m_stopped = true;
			m_queue.Clear();
			if (!m_started)
			{
				return;
			}
			m_started = false;
			if (ImportedConfigRoot.Instance != null)
			{
				ImportedConfigRoot.Instance.m_onImportComplete -= OnInitialImportComplete;
			}
			if (LayerCatalogue.Instance != null)
			{
				LayerCatalogue.Instance.OnEntryAdded -= OnCatalogueEntryAdded;
			}
		}

		private void OnInitialImportComplete()
		{
			Reconcile();
		}

		private void OnCatalogueEntryAdded(LayerCatalogueEntry a_entry)
		{
			Reconcile();
		}

		private void Reconcile()
		{
			if (m_stopped || !ImportedConfigRoot.Instance.ImportComplete)
			{
				return;
			}

			foreach (LayerCatalogueEntry entry in LayerCatalogue.Instance.Entries)
			{
				if (!m_handledIds.Add(entry.m_layerId))
				{
					continue;
				}

				if (GisHttpDataSource.IsInvisible(entry))
				{
					Debug.Log($"[ClientLayerLoader] Skipping layer {entry.m_layerId} ({entry.m_name}), tagged Invisible");
					continue;
				}

				Debug.Log($"[ClientLayerLoader] Queueing layer {entry.m_layerId} ({entry.m_name})");
				m_queue.Enqueue(entry);
			}

			Pump();
		}

		private void Pump()
		{
			if (m_busy || m_stopped || m_queue.Count == 0)
			{
				return;
			}

			m_busy = true;
			m_runner.StartCoroutine(Process(m_queue.Dequeue()));
		}

		private IEnumerator Process(LayerCatalogueEntry a_entry)
		{
			GisHttpDataSource.LayerFetchResult result = null;
			bool fetchFailed = false;
			yield return m_dataSource.LoadLayer(a_entry, a_result => result = a_result, () => fetchFailed = true);

			if (m_stopped)
			{
				yield break;
			}

			if (fetchFailed || result == null)
			{
				Fail(a_entry, "fetch from the host failed");
				yield break;
			}

			ALayer layer;
			Dictionary<string, Texture2D> textures = null;
			if (result.RasterLayer != null)
			{
				layer = result.RasterLayer;
				textures = GisRasterTextureLoader.Decode(new Dictionary<string, byte[]> { { result.RasterImageKey, result.RasterPng } });
			}
			else
			{
				layer = result.VectorLayer;
			}

			ImportedConfigRoot.Instance.AddLayer(layer, textures, a_success =>
			{
				if (m_stopped)
				{
					return;
				}

				if (a_success)
				{
					Debug.Log($"[ClientLayerLoader] Layer {a_entry.m_layerId} ({a_entry.m_name}) added");
					Done();
				}
				else
				{
					Fail(a_entry, "layer was rejected when adding it (invalid or invisible)");
				}
			});
		}

		private void Fail(LayerCatalogueEntry a_entry, string a_reason)
		{
			Debug.LogError($"[ClientLayerLoader] Layer {a_entry.m_layerId} ({a_entry.m_name}) not added: {a_reason}");
			m_onLayerFailed?.Invoke(a_entry.m_layerId, a_reason);
			Done();
		}

		private void Done()
		{
			m_busy = false;
			Pump();
		}
	}
}
