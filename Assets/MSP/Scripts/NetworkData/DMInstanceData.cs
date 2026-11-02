using System;
using System.Collections.Generic;
using Unity.Netcode;

namespace POV_Unity
{
	public struct DMInstanceData : INetworkSerializable, IEquatable<DMInstanceData>
	{
		public int m_layerId;
		public int m_DMIndexInLayer;
		public bool m_active;

		public bool Equals(DMInstanceData other)
		{
			return m_layerId.Equals(other.m_layerId) && m_DMIndexInLayer.Equals(other.m_DMIndexInLayer) && m_active.Equals(other.m_active);
		}

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			if (serializer.IsReader)
			{
				var reader = serializer.GetFastBufferReader();
				reader.ReadValueSafe(out m_layerId);
				reader.ReadValueSafe(out m_DMIndexInLayer);
				reader.ReadValueSafe(out m_active);
			}
			else
			{
				var writer = serializer.GetFastBufferWriter();
				writer.WriteValueSafe(m_layerId);
				writer.WriteValueSafe(m_DMIndexInLayer);
				writer.WriteValueSafe(m_active);
			}
		}
	}
}
