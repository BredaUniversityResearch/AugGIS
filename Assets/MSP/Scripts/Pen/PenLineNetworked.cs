using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using POV_Unity;

/// <summary>
/// Syncs a PenLine. The point list is server-authoritative: the drawer streams its stroke to the server while drawing,
/// and any client can edit points of a finished line through server RPCs (addressed by stable point id).
/// </summary>
[RequireComponent(typeof(PenLine))]
public class PenLineNetworked : NetworkBehaviour, IPenLineEditHandler
{
	private const float AUTHORITY_REQUEST_INTERVAL = 0.5f;
	private PenLine m_lineScript;

	private NetworkList<PenLinePoint> m_syncedPoints = new NetworkList<PenLinePoint>(new List<PenLinePoint>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
	private NetworkVariable<Color> m_syncedLineColor = new NetworkVariable<Color>(Color.white,NetworkVariableReadPermission.Everyone,NetworkVariableWritePermission.Owner);
	private NetworkVariable<float> m_syncedLineWidth = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
	private NetworkVariable<bool> m_lineIsFinished = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
	private NetworkVariable<bool> m_isClosed = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

	//True on the drawer while it is drawing: its local stroke is authoritative and incoming list changes are ignored
	private bool m_isDrawingLocally = false;
	private bool m_drawingEventsSubscribed = false;

	//Server only: client currently drawing (or continuing) the line, used to release it when that client disconnects
	private ulong m_serverDrawerClientId = ulong.MaxValue;

	private float m_nextAuthorityRequestTime = 0f;
	private readonly List<PenLinePoint> m_pointBuffer = new List<PenLinePoint>();
	private bool m_colliderDirty = false;

	private void Awake()
	{
		m_lineScript = GetComponent<PenLine>();
	}

	public override void OnNetworkSpawn()
	{
		base.OnNetworkSpawn();

		m_lineScript.EditHandler = this;

		if (IsOwner && !m_lineIsFinished.Value)
		{
			m_isDrawingLocally = true;
			SubscribeDrawingEvents();
			m_lineScript.LineColorChangedEvent += OnLineColorChanged;
			m_lineScript.LineWidthChangedEvent += OnLineWidthChanged;

			//Send the points the line already has (its origin)
			if (m_syncedPoints.Count == 0)
			{
				SendReplacePoints();
			}
		}
		else
		{
			ApplySyncedPoints();
			m_lineScript.ChangeLineColorWithoutNotify(m_syncedLineColor.Value);
			m_lineScript.ChangeLineWidthWithoutNotify(m_syncedLineWidth.Value);
			m_lineScript.SetClosedWithoutNotify(m_isClosed.Value);
		}

		if (IsServer)
		{
			NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
			if (!m_lineIsFinished.Value)
			{
				m_serverDrawerClientId = OwnerClientId;
			}
		}

		m_syncedPoints.OnListChanged += OnPointListChanged;
		m_lineIsFinished.OnValueChanged += OnLineFinishedChanged;
		m_isClosed.OnValueChanged += OnClosedChanged;

		m_lineScript.LineErasedEvent += OnLineErased;
		m_lineScript.LineFinishedEvent += OnLineFinished;

		m_syncedLineColor.OnValueChanged += OnSyncedColorValueChanged;
		m_syncedLineWidth.OnValueChanged += OnSyncedWidthValueChanged;

		if (ImportedConfigRoot.Instance.ImportComplete)
		{
			OnConfigImportComplete();
		}
		else
		{
			ImportedConfigRoot.Instance.m_onImportComplete += OnConfigImportComplete;
		}
	}

	void OnConfigImportComplete()
	{
		if (m_lineIsFinished.Value)
		{
			ApplySyncedPoints();
			m_lineScript.SetClosedWithoutNotify(m_isClosed.Value);
			m_lineScript.FinishLineWithoutNotify();
		}
	}

	public override void OnNetworkDespawn()
	{
		base.OnNetworkDespawn();
		if (m_lineScript.EditHandler == (IPenLineEditHandler)this)
		{
			m_lineScript.EditHandler = null;
		}
		if (ImportedConfigRoot.Instance != null)
		{
			ImportedConfigRoot.Instance.m_onImportComplete -= OnConfigImportComplete;
		}
		if (NetworkManager != null)
		{
			NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
		}
		m_syncedPoints.OnListChanged -= OnPointListChanged;
		m_lineIsFinished.OnValueChanged -= OnLineFinishedChanged;
		m_isClosed.OnValueChanged -= OnClosedChanged;
		m_syncedLineColor.OnValueChanged -= OnSyncedColorValueChanged;
		m_syncedLineWidth.OnValueChanged -= OnSyncedWidthValueChanged;
		UnsubscribeDrawingEvents();
		m_lineScript.LineColorChangedEvent -= OnLineColorChanged;
		m_lineScript.LineWidthChangedEvent -= OnLineWidthChanged;
		m_lineScript.LineErasedEvent -= OnLineErased;
		m_lineScript.LineFinishedEvent -= OnLineFinished;
	}

	private void SubscribeDrawingEvents()
	{
		if (m_drawingEventsSubscribed)
			return;

		m_drawingEventsSubscribed = true;
		m_lineScript.LinePointAddedEvent += OnLineNewPointAdded;
		m_lineScript.LastPointUpdatedEvent += OnLineLastPointUpdated;
		m_lineScript.LinePointsReplacedEvent += OnLinePointsReplaced;
	}

	private void UnsubscribeDrawingEvents()
	{
		if (!m_drawingEventsSubscribed)
			return;

		m_drawingEventsSubscribed = false;
		m_lineScript.LinePointAddedEvent -= OnLineNewPointAdded;
		m_lineScript.LastPointUpdatedEvent -= OnLineLastPointUpdated;
		m_lineScript.LinePointsReplacedEvent -= OnLinePointsReplaced;
	}

	private void ApplySyncedPoints()
	{
		m_pointBuffer.Clear();
		foreach (PenLinePoint point in m_syncedPoints)
		{
			m_pointBuffer.Add(point);
		}
		m_lineScript.SetPoints(m_pointBuffer);
	}

	#region Erase / Finish

	private void OnLineErased()
	{
		EraseServerRpc();
	}

	[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
	void EraseServerRpc()
	{
		DespawnLine();
	}

	private void DespawnLine()
	{
		if (NetworkObject.IsSpawned)
		{
			NetworkObject.Despawn(true);
		}
	}

	private void OnLineFinished()
	{
		m_isDrawingLocally = false;
		UnsubscribeDrawingEvents();
		FinishLineServerRpc(NetworkManager.Singleton.LocalClientId, m_lineScript.EvaluateClosed());
	}

	[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
	void FinishLineServerRpc(ulong a_clientId, bool a_closed)
	{
		m_serverDrawerClientId = ulong.MaxValue;
		m_isClosed.Value = a_closed;
		m_lineIsFinished.Value = true;
		FinishLineClientRPC(a_clientId);
	}

	[Rpc(SendTo.ClientsAndHost)]
	void FinishLineClientRPC(ulong a_clientId)
	{
		if (a_clientId != NetworkManager.Singleton.LocalClientId)
		{
			ApplySyncedPoints();
			m_lineScript.FinishLineWithoutNotify();
		}
	}

	private void OnLineFinishedChanged(bool a_previousValue, bool a_newValue)
	{
		if (a_newValue)
		{
			m_lineScript.FinishLineWithoutNotify();
		}
		else if (!m_isDrawingLocally)
		{
			//Someone continues drawing the line
			m_lineScript.SetFinishedWithoutNotify(false);
		}
	}

	#endregion

	#region Continue drawing

	public void RequestContinue(bool a_fromStart)
	{
		ContinueLineServerRpc(a_fromStart);
	}

	[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
	void ContinueLineServerRpc(bool a_fromStart, RpcParams a_rpcParams = default)
	{
		ulong sender = a_rpcParams.Receive.SenderClientId;
		//Closed lines may be continued too: they reopen while being drawn and are re-evaluated on finish
		bool granted = m_lineIsFinished.Value && m_syncedPoints.Count > 0 && !IsAnySyncedPointHeld();

		if (granted)
		{
			if (OwnerClientId != sender)
			{
				NetworkObject.ChangeOwnership(sender);
			}
			m_serverDrawerClientId = sender;
			m_isClosed.Value = false;
			m_lineIsFinished.Value = false;
		}

		ContinueResultRpc(granted, a_fromStart, RpcTarget.Single(sender, RpcTargetUse.Temp));
	}

	[Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Everyone)]
	void ContinueResultRpc(bool a_granted, bool a_fromStart, RpcParams a_rpcParams = default)
	{
		if (a_granted)
		{
			//Start from the authoritative points; the server did not reverse them, the drawer does and sends the result
			ApplySyncedPoints();
			m_isDrawingLocally = true;
			SubscribeDrawingEvents();
			m_lineScript.SetFinishedWithoutNotify(false);
			//OnClosedChanged is ignored while drawing locally, so reopen the line explicitly
			m_lineScript.SetClosedWithoutNotify(false);

			if (a_fromStart && m_lineScript.PointCount > 1)
			{
				m_lineScript.ReversePointsWithoutNotify();
				SendReplacePoints();
			}
		}

		m_lineScript.OnContinueResult(a_granted);
	}

	#endregion

	#region Moving

	public bool HasTransformAuthority => IsOwner;

	public void RequestTransformAuthority(ulong a_pointA, ulong a_pointB)
	{
		if (IsOwner || Time.unscaledTime < m_nextAuthorityRequestTime)
			return;

		m_nextAuthorityRequestTime = Time.unscaledTime + AUTHORITY_REQUEST_INTERVAL;
		TransformAuthorityServerRpc(a_pointA, a_pointB);
	}

	[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
	void TransformAuthorityServerRpc(ulong a_pointA, ulong a_pointB, RpcParams a_rpcParams = default)
	{
		ulong sender = a_rpcParams.Receive.SenderClientId;
		if (!m_lineIsFinished.Value || OwnerClientId == sender || a_pointA == a_pointB)
			return;

		int indexA = ServerIndexOf(a_pointA);
		int indexB = ServerIndexOf(a_pointB);
		if (indexA < 0 || indexB < 0)
			return;

		//Only the client holding both points may move the line
		if (m_syncedPoints[indexA].HeldBy != sender || m_syncedPoints[indexB].HeldBy != sender)
			return;

		NetworkObject.ChangeOwnership(sender);
	}

	#endregion

	#region Drawing (owner -> server)

	private void OnLineNewPointAdded(PenLinePoint a_point)
	{
		AddPointServerRpc(a_point.Id, a_point.Position);
	}

	private void OnLineLastPointUpdated(PenLinePoint a_point)
	{
		DrawMovePointServerRpc(a_point.Id, a_point.Position);
	}

	private void OnLinePointsReplaced()
	{
		SendReplacePoints();
	}

	private void SendReplacePoints()
	{
		IReadOnlyList<PenLinePoint> points = m_lineScript.Points;
		ulong[] ids = new ulong[points.Count];
		Vector3[] positions = new Vector3[points.Count];
		for (int i = 0; i < points.Count; i++)
		{
			ids[i] = points[i].Id;
			positions[i] = points[i].Position;
		}
		ReplacePointsServerRpc(ids, positions);
	}

	[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
	void AddPointServerRpc(ulong a_id, Vector3 a_position, RpcParams a_rpcParams = default)
	{
		if (a_rpcParams.Receive.SenderClientId != OwnerClientId || m_lineIsFinished.Value)
			return;

		m_syncedPoints.Add(new PenLinePoint(a_id, a_position));
	}

	[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
	void DrawMovePointServerRpc(ulong a_id, Vector3 a_position, RpcParams a_rpcParams = default)
	{
		if (a_rpcParams.Receive.SenderClientId != OwnerClientId || m_lineIsFinished.Value)
			return;

		int index = ServerIndexOf(a_id);
		if (index < 0)
		{
			m_syncedPoints.Add(new PenLinePoint(a_id, a_position));
			return;
		}

		PenLinePoint point = m_syncedPoints[index];
		point.Position = a_position;
		m_syncedPoints[index] = point;
	}

	[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
	void ReplacePointsServerRpc(ulong[] a_ids, Vector3[] a_positions, RpcParams a_rpcParams = default)
	{
		if (a_rpcParams.Receive.SenderClientId != OwnerClientId || m_lineIsFinished.Value)
			return;
		if (a_ids == null || a_positions == null || a_ids.Length != a_positions.Length)
			return;

		//Update in place where possible to keep the delta small
		for (int i = 0; i < a_ids.Length; i++)
		{
			PenLinePoint point = new PenLinePoint(a_ids[i], a_positions[i]);
			if (i < m_syncedPoints.Count)
			{
				if (!m_syncedPoints[i].Equals(point))
				{
					m_syncedPoints[i] = point;
				}
			}
			else
			{
				m_syncedPoints.Add(point);
			}
		}

		while (m_syncedPoints.Count > a_ids.Length)
		{
			m_syncedPoints.RemoveAt(m_syncedPoints.Count - 1);
		}
	}

	#endregion

	#region Editing (any client -> server)

	public void RequestHold(ulong a_pointId)
	{
		HoldPointServerRpc(a_pointId);
	}

	public void RequestMove(ulong a_pointId, Vector3 a_localPosition)
	{
		EditMovePointServerRpc(a_pointId, a_localPosition);
	}

	public void RequestInsert(ulong a_afterPointId, ulong a_newPointId, Vector3 a_localPosition)
	{
		InsertPointServerRpc(a_afterPointId, a_newPointId, a_localPosition);
	}

	public void RequestCommit(ulong a_pointId, Vector3 a_localPosition, bool a_closedAfterEdit)
	{
		CommitPointServerRpc(a_pointId, a_localPosition, a_closedAfterEdit);
	}

	public void RequestRemove(ulong a_pointId, bool a_closedAfterEdit)
	{
		RemovePointServerRpc(a_pointId, a_closedAfterEdit);
	}

	[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
	void HoldPointServerRpc(ulong a_id, RpcParams a_rpcParams = default)
	{
		int index = ServerIndexOf(a_id);
		if (index < 0)
			return;

		PenLinePoint point = m_syncedPoints[index];
		point.HeldBy = a_rpcParams.Receive.SenderClientId;
		m_syncedPoints[index] = point;
	}

	[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone, Delivery = RpcDelivery.Unreliable)]
	void EditMovePointServerRpc(ulong a_id, Vector3 a_position, RpcParams a_rpcParams = default)
	{
		int index = ServerIndexOf(a_id);
		if (index < 0)
			return;

		//Only the client holding the point may move it. This also drops unreliable moves that arrive after the commit.
		PenLinePoint point = m_syncedPoints[index];
		if (point.HeldBy != a_rpcParams.Receive.SenderClientId)
			return;

		point.Position = a_position;
		m_syncedPoints[index] = point;
	}

	[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
	void InsertPointServerRpc(ulong a_afterId, ulong a_newId, Vector3 a_position, RpcParams a_rpcParams = default)
	{
		int afterIndex = ServerIndexOf(a_afterId);
		if (afterIndex < 0 || ServerIndexOf(a_newId) >= 0)
			return;

		m_syncedPoints.Insert(afterIndex + 1, new PenLinePoint(a_newId, a_position, a_rpcParams.Receive.SenderClientId));
	}

	[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
	void CommitPointServerRpc(ulong a_id, Vector3 a_position, bool a_closedAfterEdit)
	{
		int index = ServerIndexOf(a_id);
		if (index < 0)
			return;

		bool isEndpoint = index == 0 || index == m_syncedPoints.Count - 1;

		PenLinePoint point = m_syncedPoints[index];
		point.Position = a_position;
		point.HeldBy = PenLinePoint.NOT_HELD;
		m_syncedPoints[index] = point;

		if (isEndpoint)
		{
			m_isClosed.Value = a_closedAfterEdit && m_syncedPoints.Count >= 3;
		}
	}

	[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
	void RemovePointServerRpc(ulong a_id, bool a_closedAfterEdit)
	{
		int index = ServerIndexOf(a_id);
		if (index < 0)
			return;

		bool wasEndpoint = index == 0 || index == m_syncedPoints.Count - 1;
		m_syncedPoints.RemoveAt(index);

		//A single remaining point is a dot, removing the last point deletes the line
		if (m_syncedPoints.Count == 0)
		{
			DespawnLine();
			return;
		}

		if (wasEndpoint)
		{
			m_isClosed.Value = a_closedAfterEdit && m_syncedPoints.Count >= 3;
		}
		else if (m_syncedPoints.Count < 3)
		{
			m_isClosed.Value = false;
		}
	}

	private void OnClientDisconnected(ulong a_clientId)
	{
		//Release points that the disconnected client was still holding
		for (int i = 0; i < m_syncedPoints.Count; i++)
		{
			PenLinePoint point = m_syncedPoints[i];
			if (point.HeldBy == a_clientId)
			{
				point.HeldBy = PenLinePoint.NOT_HELD;
				m_syncedPoints[i] = point;
			}
		}

		//Finish a line the disconnected client was still drawing, so others can edit or continue it
		if (m_serverDrawerClientId == a_clientId && !m_lineIsFinished.Value)
		{
			m_serverDrawerClientId = ulong.MaxValue;
			if (m_syncedPoints.Count == 0)
			{
				DespawnLine();
				return;
			}
			m_lineIsFinished.Value = true;
			FinishLineClientRPC(a_clientId);
		}
	}

	private bool IsAnySyncedPointHeld()
	{
		foreach (PenLinePoint point in m_syncedPoints)
		{
			if (point.IsHeld)
				return true;
		}
		return false;
	}

	private int ServerIndexOf(ulong a_id)
	{
		for (int i = 0; i < m_syncedPoints.Count; i++)
		{
			if (m_syncedPoints[i].Id == a_id)
				return i;
		}
		return -1;
	}

	#endregion

	#region Sync (server -> clients)

	private void OnPointListChanged(NetworkListEvent<PenLinePoint> a_changeEvent)
	{
		if (m_isDrawingLocally)
			return;

		ApplySyncedPoints();

		//Rebuild the collider once the line has settled (nobody is dragging a point) instead of on every drag update
		if (m_lineIsFinished.Value && !IsAnyPointHeld())
		{
			m_colliderDirty = true;
		}
	}

	private bool IsAnyPointHeld()
	{
		return m_lineScript.HasLocalHolds() || IsAnySyncedPointHeld();
	}

	private void LateUpdate()
	{
		if (m_colliderDirty)
		{
			m_colliderDirty = false;
			m_lineScript.RebuildCollider();
		}
	}

	private void OnClosedChanged(bool a_previousValue, bool a_newValue)
	{
		if (m_isDrawingLocally)
			return;

		m_lineScript.SetClosedWithoutNotify(a_newValue);
		if (m_lineIsFinished.Value)
		{
			m_colliderDirty = true;
		}
	}

	private void OnLineColorChanged()
	{
		m_syncedLineColor.Value = m_lineScript.LineColor;
	}

	private void OnLineWidthChanged()
	{
		m_syncedLineWidth.Value = m_lineScript.LineWidth;
	}

	private void OnSyncedWidthValueChanged(float a_previousValue, float a_newValue)
	{
		m_lineScript.ChangeLineWidthWithoutNotify(a_newValue);
	}

	private void OnSyncedColorValueChanged(Color a_previousValue, Color a_newValue)
	{
		m_lineScript.ChangeLineColorWithoutNotify(a_newValue);
	}

	#endregion
}
