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
}
