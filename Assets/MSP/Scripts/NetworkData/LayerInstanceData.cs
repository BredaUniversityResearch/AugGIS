using System;
using Unity.Netcode;

namespace POV_Unity
{
	public struct LayerInstanceData : INetworkSerializable, IEquatable<LayerInstanceData>
	{
		public int m_layerIndex;
		public int m_verticalStep;
		public int m_indexInStep;
		public int m_elementsInStep;
		public LayerVisualizationMode m_visualizationMode;
		public long m_ownerID;

		public bool Equals(LayerInstanceData other)
		{
			return m_layerIndex.Equals(other.m_layerIndex) &&
					m_verticalStep.Equals(other.m_verticalStep) &&
					m_indexInStep.Equals(other.m_indexInStep) &&
					m_elementsInStep.Equals(other.m_elementsInStep) &&
					m_visualizationMode.Equals(other.m_visualizationMode) &&
					m_ownerID.Equals(other.m_ownerID);
		}

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			if (serializer.IsReader)
			{
				var reader = serializer.GetFastBufferReader();
				reader.ReadValueSafe(out m_layerIndex);
				reader.ReadValueSafe(out m_visualizationMode);
				reader.ReadValueSafe(out m_verticalStep);
				reader.ReadValueSafe(out m_indexInStep);
				reader.ReadValueSafe(out m_elementsInStep);
				reader.ReadValueSafe(out m_ownerID);
			}
			else
			{
				var writer = serializer.GetFastBufferWriter();
				writer.WriteValueSafe(m_layerIndex);
				writer.WriteValueSafe(m_visualizationMode);
				writer.WriteValueSafe(m_verticalStep);
				writer.WriteValueSafe(m_indexInStep);
				writer.WriteValueSafe(m_elementsInStep);
				writer.WriteValueSafe(m_ownerID);
			}
		}
	}
}
