using System;
using MSP.Scripts.GisData;
using Unity.Collections;
using Unity.Netcode;

namespace POV_Unity
{
	/// <summary>
	/// Metadata-only description of one layer, mirroring LayerRow's common fields
	/// (Id/Kind/Name/Short/Tags/SortOrder). Deliberately knows nothing about
	/// MSPC-specific layer content, so layers from other origins can use it as well.
	/// Strings are fixed-size because NetworkList requires unmanaged element types;
	/// longer values get truncated (see LayerCatalogue.BuildEntry).
	/// </summary>
	public struct LayerCatalogueEntry : INetworkSerializable, IEquatable<LayerCatalogueEntry>
	{
		public const char TagSeparator = '\n';

		public int m_layerId;
		public LayerKind m_kind;
		public int m_sortOrder;
		public FixedString128Bytes m_name;
		public FixedString128Bytes m_short;
		public FixedString512Bytes m_tags; //Tags joined by TagSeparator

		public string[] GetTags()
		{
			if (m_tags.Length == 0)
			{
				return Array.Empty<string>();
			}
			return m_tags.ToString().Split(TagSeparator);
		}

		public bool Equals(LayerCatalogueEntry other)
		{
			return m_layerId == other.m_layerId &&
					m_kind == other.m_kind &&
					m_sortOrder == other.m_sortOrder &&
					m_name.Equals(other.m_name) &&
					m_short.Equals(other.m_short) &&
					m_tags.Equals(other.m_tags);
		}

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			serializer.SerializeValue(ref m_layerId);
			serializer.SerializeValue(ref m_kind);
			serializer.SerializeValue(ref m_sortOrder);
			serializer.SerializeValue(ref m_name);
			serializer.SerializeValue(ref m_short);
			serializer.SerializeValue(ref m_tags);
		}
	}
}
