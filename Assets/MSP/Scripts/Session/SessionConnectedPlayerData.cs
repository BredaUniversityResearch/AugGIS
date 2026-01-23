using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public struct SessionConnectedPlayerData : INetworkSerializable, IEquatable<SessionConnectedPlayerData>
{
	public ulong clientID;
	public FixedString128Bytes playerName;
	public Color teamColor;
	public bool isGameMaster;

	public bool Equals(SessionConnectedPlayerData other)
	{
		return  clientID.Equals(other.clientID) &&
				playerName.Equals(other.playerName) &&
				teamColor.Equals(other.teamColor) &&
				isGameMaster.Equals(other.isGameMaster);
	}

	public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
	{
		if (serializer.IsReader)
		{
			var reader = serializer.GetFastBufferReader();
			reader.ReadValueSafe(out clientID);
			reader.ReadValueSafe(out playerName);
			reader.ReadValueSafe(out teamColor);
			reader.ReadValueSafe(out isGameMaster);
		}
		else
		{
			var writer = serializer.GetFastBufferWriter();
			writer.WriteValueSafe(clientID);
			writer.WriteValueSafe(playerName);
			writer.WriteValueSafe(teamColor);
			writer.WriteValueSafe(isGameMaster);
		}
	}
}