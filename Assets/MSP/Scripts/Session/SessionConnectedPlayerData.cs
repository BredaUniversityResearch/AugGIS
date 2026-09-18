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
		serializer.SerializeValue(ref clientID);
		serializer.SerializeValue(ref playerName);
		serializer.SerializeValue(ref teamColor);
		serializer.SerializeValue(ref isGameMaster);
	}
}