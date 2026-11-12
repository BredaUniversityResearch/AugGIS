using System;
using System.Collections.Generic;
using MSP.Scripts.GisData;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace POV_Unity
{
	/// <summary>
	/// Server-authoritative, synced list of which layers exist in the current dataset
	/// (metadata only, no layer payloads). Late-joining clients receive the full list
	/// automatically through NetworkList, and layers appended later raise
	/// OnEntryAdded on every client.
	/// Lives on the same GameObject as LayerManager (needs a NetworkObject).
	/// </summary>
	public class LayerCatalogue : NetworkBehaviour
	{
		private static LayerCatalogue ms_instance;
		public static LayerCatalogue Instance
		{
			get
			{
				if (ms_instance == null)
				{
					ms_instance = FindAnyObjectByType<LayerCatalogue>();
				}
				return ms_instance;
			}
		}

		public delegate void OnCatalogueEntryAdded(LayerCatalogueEntry a_entry);
		public event OnCatalogueEntryAdded OnEntryAdded;

		private NetworkList<LayerCatalogueEntry> m_entries;

		//-1 until the server has published the initial set of layers, then the number
		//of entries published. Clients are "ready" once they have received that many
		//entries, regardless of the order the two syncs arrive in.
		private NetworkVariable<int> m_publishedCount = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
		private bool m_isReady;
		private event Action m_onReady;
		public bool IsReady => m_isReady;
		public IReadOnlyList<LayerCatalogueEntry> Entries
		{
			get
			{
				List<LayerCatalogueEntry> copy = new List<LayerCatalogueEntry>(m_entries.Count);
				foreach (LayerCatalogueEntry entry in m_entries)
				{
					copy.Add(entry);
				}
				return copy;
			}
		}
		public int Count => m_entries.Count;

		private void Awake()
		{
			if (ms_instance != null && ms_instance != this)
			{
				Debug.LogError("instance of LayerCatalogue already exists, removing new instance");
				Destroy(this);
				return;
			}
			ms_instance = this;

			m_entries = new NetworkList<LayerCatalogueEntry>();
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			m_entries.OnListChanged += OnEntriesChanged;
			m_publishedCount.OnValueChanged += OnPublishedCountChanged;
			TryRaiseReady();

			//Entries synced before spawn (late join) arrive without Add events
			Debug.Log($"[LayerCatalogue] Spawned with {m_entries.Count} entries (IsServer: {IsServer})");
			foreach (LayerCatalogueEntry entry in m_entries)
			{
				LogEntry(entry);
			}
		}

		public override void OnNetworkDespawn()
		{
			m_entries.OnListChanged -= OnEntriesChanged;
			m_publishedCount.OnValueChanged -= OnPublishedCountChanged;
			m_isReady = false;
			m_onReady = null;
			base.OnNetworkDespawn();
		}

		/// <summary>
		/// Runs the callback once the initial set of layers is fully available on this
		/// process: immediately if it already is, otherwise as soon as it becomes so.
		/// </summary>
		public void WhenReady(Action a_callback)
		{
			if (m_isReady)
			{
				a_callback();
			}
			else
			{
				m_onReady += a_callback;
			}
		}

		/// <summary>
		/// Server only. Signals that the initial set of layers has been fully added and
		/// the server's own import of them is complete, so clients may start loading.
		/// </summary>
		public void MarkInitialLayersPublished()
		{
			if (!IsServer)
			{
				Debug.LogError("LayerCatalogue.MarkInitialLayersPublished can only be called on the server");
				return;
			}

			m_publishedCount.Value = m_entries.Count;
			TryRaiseReady();
		}

		private void OnPublishedCountChanged(int a_previous, int a_current)
		{
			TryRaiseReady();
		}

		private void TryRaiseReady()
		{
			if (m_isReady || m_publishedCount.Value < 0 || m_entries.Count < m_publishedCount.Value)
			{
				return;
			}

			m_isReady = true;
			Debug.Log($"[LayerCatalogue] Ready with {m_entries.Count} entries");
			Action callbacks = m_onReady;
			m_onReady = null;
			callbacks?.Invoke();
		}

		private void OnEntriesChanged(NetworkListEvent<LayerCatalogueEntry> a_change)
		{
			TryRaiseReady();

			if (a_change.Type == NetworkListEvent<LayerCatalogueEntry>.EventType.Add)
			{
				LogEntry(a_change.Value);
				OnEntryAdded?.Invoke(a_change.Value);
			}
		}

		private static void LogEntry(LayerCatalogueEntry a_entry)
		{
			Debug.Log($"[LayerCatalogue] id={a_entry.m_layerId} kind={a_entry.m_kind} sort={a_entry.m_sortOrder} name=\"{a_entry.m_name}\" short=\"{a_entry.m_short}\" tags=[{string.Join(", ", a_entry.GetTags())}]");
		}

		/// <summary>
		/// Server only. Appends the layer to the catalogue, using the id the layer
		/// already carries (ALayer.LayerId, assigned when the dataset was written).
		/// </summary>
		public void AddLayer(ALayer a_layer, LayerKind a_kind, int a_sortOrder)
		{
			if (!IsServer)
			{
				Debug.LogError("LayerCatalogue.AddLayer can only be called on the server");
				return;
			}

			foreach (LayerCatalogueEntry existing in m_entries)
			{
				if (existing.m_layerId == a_layer.LayerId)
				{
					Debug.LogError($"Layer id {a_layer.LayerId} is already in the catalogue");
					return;
				}
			}

			m_entries.Add(BuildEntry(a_layer, a_kind, a_sortOrder));
		}

		private static LayerCatalogueEntry BuildEntry(ALayer a_layer, LayerKind a_kind, int a_sortOrder)
		{
			LayerCatalogueEntry entry = new LayerCatalogueEntry
			{
				m_layerId = a_layer.LayerId,
				m_kind = a_kind,
				m_sortOrder = a_sortOrder
			};

			if (entry.m_name.CopyFromTruncated(a_layer.name ?? string.Empty) != CopyError.None)
			{
				Debug.LogWarning($"Layer {a_layer.LayerId} name was truncated in the catalogue: \"{a_layer.name}\"");
			}
			if (entry.m_short.CopyFromTruncated(a_layer.@short ?? string.Empty) != CopyError.None)
			{
				Debug.LogWarning($"Layer {a_layer.LayerId} short name was truncated in the catalogue: \"{a_layer.@short}\"");
			}

			string tags = a_layer.tags != null ? string.Join(LayerCatalogueEntry.TagSeparator, a_layer.tags) : string.Empty;
			if (entry.m_tags.CopyFromTruncated(tags) != CopyError.None)
			{
				Debug.LogWarning($"Layer {a_layer.LayerId} tags were truncated in the catalogue");
			}

			return entry;
		}
	}
}
