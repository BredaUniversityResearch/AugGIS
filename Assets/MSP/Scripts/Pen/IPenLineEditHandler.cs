using UnityEngine;

/// <summary>
/// Receives point edit requests from a PenLine (e.g. to forward them over the network).
/// When a PenLine has no edit handler it applies edits directly.
/// All positions are in the line's local space.
/// </summary>
public interface IPenLineEditHandler
{
	void RequestHold(ulong a_pointId);
	void RequestMove(ulong a_pointId, Vector3 a_localPosition);
	void RequestInsert(ulong a_afterPointId, ulong a_newPointId, Vector3 a_localPosition);

	/// <param name="a_closedAfterEdit">Closed state of the line as evaluated by the editing client after applying the edit.</param>
	void RequestCommit(ulong a_pointId, Vector3 a_localPosition, bool a_closedAfterEdit);

	/// <param name="a_closedAfterEdit">Closed state of the line as evaluated by the editing client after applying the edit.</param>
	void RequestRemove(ulong a_pointId, bool a_closedAfterEdit);

	/// <summary>
	/// Requests to continue drawing this (finished, open) line. The handler must answer through PenLine.OnContinueResult.
	/// </summary>
	/// <param name="a_fromStart">True to continue from the first point (the line gets reversed), false to continue from the last point.</param>
	void RequestContinue(bool a_fromStart);

	/// <summary>
	/// True if this client may move the line's transform.
	/// </summary>
	bool HasTransformAuthority { get; }

	/// <summary>
	/// Requests the right to move the line's transform. Only granted while this client holds both given points.
	/// </summary>
	void RequestTransformAuthority(ulong a_pointA, ulong a_pointB);
}
