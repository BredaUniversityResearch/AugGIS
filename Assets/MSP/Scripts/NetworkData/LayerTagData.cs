using System;
using Unity.Netcode;

namespace POV_Unity
{
	public struct LayerTagData : INetworkSerializable, IEquatable<LayerTagData>
	{
		public float m_newLocalZValue;
		public float m_deletionProgress;
		public int m_layerIndex;

		public bool Equals(LayerTagData other)
		{
			return m_layerIndex.Equals(other.m_layerIndex) &&
					m_deletionProgress.Equals(other.m_deletionProgress) &&
					m_newLocalZValue.Equals(other.m_newLocalZValue);
		}

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			if (serializer.IsReader)
			{
				var reader = serializer.GetFastBufferReader();
				reader.ReadValueSafe(out m_layerIndex);
				reader.ReadValueSafe(out m_deletionProgress);
				reader.ReadValueSafe(out m_newLocalZValue);
			}
			else
			{
				var writer = serializer.GetFastBufferWriter();
				writer.WriteValueSafe(m_layerIndex);
				writer.WriteValueSafe(m_deletionProgress);
				writer.WriteValueSafe(m_newLocalZValue);
			}
		}
	}
}
