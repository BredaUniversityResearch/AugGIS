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
			serializer.SerializeValue(ref m_layerIndex);
			serializer.SerializeValue(ref m_visualizationMode);
			serializer.SerializeValue(ref m_verticalStep);
			serializer.SerializeValue(ref m_indexInStep);
			serializer.SerializeValue(ref m_elementsInStep);
			serializer.SerializeValue(ref m_ownerID);
		}
	}
}
