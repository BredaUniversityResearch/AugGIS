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
			base.OnNetworkDespawn();
		}

		private void OnEntriesChanged(NetworkListEvent<LayerCatalogueEntry> a_change)
		{
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
