using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// A single point of a PenLine. Points are addressed by a stable Id so that concurrent
/// edits (inserts/removals by different users) do not shift each other's targets.
/// Position is in the line's local space.
/// </summary>
public struct PenLinePoint : IEquatable<PenLinePoint>, INetworkSerializeByMemcpy
{
	public const ulong NOT_HELD = ulong.MaxValue;

	public ulong Id;
	public Vector3 Position;
	public ulong HeldBy;

	private static uint ms_idCounter = 0;

	public PenLinePoint(ulong a_id, Vector3 a_position, ulong a_heldBy = NOT_HELD)
	{
		Id = a_id;
		Position = a_position;
		HeldBy = a_heldBy;
	}

	public bool IsHeld => HeldBy != NOT_HELD;

	/// <summary>
	/// Generates an Id that is unique across clients without a round trip: the upper 32 bits are the local client id.
	/// </summary>
	public static ulong NewId()
	{
		ulong clientId = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening ? NetworkManager.Singleton.LocalClientId : 0;
		ms_idCounter++;
		return (clientId << 32) | ms_idCounter;
	}

	public static ulong LocalClientId => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening ? NetworkManager.Singleton.LocalClientId : 0;

	public bool Equals(PenLinePoint a_other)
	{
		return Id == a_other.Id && Position == a_other.Position && HeldBy == a_other.HeldBy;
	}

	public override bool Equals(object a_obj)
	{
		return a_obj is PenLinePoint other && Equals(other);
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(Id, Position, HeldBy);
	}
}
