using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;


//Chunk based network data messenger based on: https://discussions.unity.com/t/sending-large-data-with-unity-netcode/1692794
public class NetworkDataMessenger : NetworkBehaviour
{
	public event Action DataReceivedCallback;

	[SerializeField]
	private string MessageName = "MessageName";

	[Range(128, 1024)]
	[SerializeField]
	private int m_chunkSize = 1024;

	[SerializeField]
	private bool m_logProgress = false;

	private byte[] m_receivedData;
	private byte[] m_buffer;
	private int m_chunkCountReceived;
	private float m_totalReceived = 0;

	public struct Header
	{
		public int index;
		public int size;
		public int chunkSize;
		public int total;
		public int chunkCount;

		public void Serialize(FastBufferWriter writer)
		{
			writer.WriteValueSafe(index);
			writer.WriteValueSafe(size);
			writer.WriteValueSafe(chunkSize);
			writer.WriteValueSafe(total);
			writer.WriteValueSafe(chunkCount);
		}

		public static Header Deserialize(FastBufferReader messagePayload)
		{
			Header header = new Header();
			messagePayload.ReadValueSafe(out header.index);
			messagePayload.ReadValueSafe(out header.size);
			messagePayload.ReadValueSafe(out header.chunkSize);
			messagePayload.ReadValueSafe(out header.total);
			messagePayload.ReadValueSafe(out header.chunkCount);

			return header;
		}
	}


	public override void OnNetworkSpawn()
	{
		if (IsClient)
		{
			NetworkManager.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, ReceiveMessage);
		}
	}


	public override void OnNetworkDespawn()
	{
		if (IsClient)
		{
			NetworkManager.CustomMessagingManager.UnregisterNamedMessageHandler(MessageName);
		}
	}


	public void SendMessage(ulong clientId, byte[] data)
	{
		CustomMessagingManager customMessagingManager = NetworkManager.CustomMessagingManager;

		int headerSize = Marshal.SizeOf(typeof(Header));


		m_buffer ??= new byte[m_chunkSize];
		int chunkCount = Mathf.CeilToInt(data.Length / (float)m_chunkSize);


		int read = 0;
		for (int i = 0; i < chunkCount; i++)
		{
			int toRead = Mathf.Min(m_chunkSize, data.Length - read);
			Buffer.BlockCopy(data, i * m_chunkSize, m_buffer, 0, toRead);
			read += m_chunkSize;

			Header header = new Header()
			{
				index = i,
				size = toRead,
				chunkSize = m_chunkSize,
				total = data.Length,
				chunkCount = chunkCount,
			};

			FastBufferWriter writer = new FastBufferWriter(headerSize + m_chunkSize, Allocator.Temp);
			header.Serialize(writer);
			writer.WriteBytesSafe(m_buffer);

			customMessagingManager.SendNamedMessage(MessageName, clientId, writer, NetworkDelivery.Reliable);
		}

		m_buffer = null;
	}

	private void ReceiveMessage(ulong senderId, FastBufferReader messagePayload)
	{
		Header header = Header.Deserialize(messagePayload);
		m_buffer ??= new byte[header.chunkSize];

		messagePayload.ReadBytesSafe(ref m_buffer, m_buffer.Length);
		m_receivedData ??= new byte[header.total];

		Buffer.BlockCopy(m_buffer, 0, m_receivedData, header.index * header.chunkSize, header.size);

		m_totalReceived += header.size;

		if (m_logProgress)
		{
			Debug.Log($"Received: {m_totalReceived / 1048576f} MB/{header.total / 1048576f} MB");
		}

		m_chunkCountReceived++;
		if (m_chunkCountReceived == header.chunkCount)
		{
			DataReceivedCallback?.Invoke();
		}
	}



	public byte[] Consume()
	{
		byte[] copy = new byte[m_receivedData.Length];
		Buffer.BlockCopy(m_receivedData, 0, copy, 0, m_receivedData.Length);
		m_chunkCountReceived = 0;
		m_receivedData = null;
		return copy;
	}
}
