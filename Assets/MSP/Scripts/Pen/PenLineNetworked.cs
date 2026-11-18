using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using POV_Unity;

[RequireComponent(typeof(PenLine))]
public class PenLineNetworked : NetworkBehaviour
{
	private PenLine m_lineScript;

	private NetworkList<Vector3> m_syncedLinePositions = new NetworkList<Vector3>(new List<Vector3>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
	private NetworkVariable<Color> m_syncedLineColor = new NetworkVariable<Color>(Color.white,NetworkVariableReadPermission.Everyone,NetworkVariableWritePermission.Owner);
	private NetworkVariable<float> m_syncedLineWidth = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
	private NetworkVariable<bool> m_lineIsFinished = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

	private void Awake()
	{
		m_lineScript = GetComponent<PenLine>();
	}

	public override void OnNetworkSpawn()
	{
		base.OnNetworkSpawn();
		
		if (IsOwner)
		{
			m_lineScript.LinePointAddedEvent += OnLineNewPointAdded;
			m_lineScript.LastPointUpdatedEvent += OnLineLastPointUpdated;
			m_lineScript.LinePointsReplacedEvent += OnLinePointsReplaced;
			m_lineScript.LineColorChangedEvent += OnLineColorChanged;
			m_lineScript.LineWidthChangedEvent += OnLineWidthChanged;
		}
		else
		{
			foreach (Vector3 linePoint in m_syncedLinePositions)
			{
				m_lineScript.AddLocalPointWithoutNotify(linePoint);
			}	

			m_lineScript.ChangeLineColorWithoutNotify(m_syncedLineColor.Value);
			m_lineScript.ChangeLineWidthWithoutNotify(m_syncedLineWidth.Value);
		}

		m_syncedLinePositions.OnListChanged += OnPositionListChanged;

		m_lineIsFinished.OnValueChanged += OnLineFinishedChanged;

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
			SyncPointPositions();
			m_lineScript.FinishLineWithoutNotify();
		}
    }

    public override void OnNetworkDespawn()
	{
		base.OnNetworkDespawn();
		m_syncedLineColor.OnValueChanged -= OnSyncedColorValueChanged;
		m_syncedLineWidth.OnValueChanged -= OnSyncedWidthValueChanged;
		m_lineScript.LinePointAddedEvent -= OnLineNewPointAdded;
		m_lineScript.LastPointUpdatedEvent -= OnLineLastPointUpdated;
		m_lineScript.LinePointsReplacedEvent -= OnLinePointsReplaced;
    }

	private void OnLineErased()
	{
		EraseServerRpc();
	}

	[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
	void EraseServerRpc()
	{
		if(NetworkObject.IsSpawned)
		{
			NetworkObject.Despawn(true);
		}
	}

	private void OnLineFinished()
	{
		FinishLineServerRpc(NetworkManager.Singleton.LocalClientId);
	}

	[ServerRpc(InvokePermission = RpcInvokePermission.Everyone)]
	void FinishLineServerRpc(ulong a_clientId)
	{
		m_lineIsFinished.Value = true;
		FinishLineClientRPC(a_clientId);
	}

	[ClientRpc]
	void FinishLineClientRPC(ulong a_clientId)
	{
		if (a_clientId != NetworkManager.Singleton.LocalClientId)
		{
			m_lineScript.FinishLineWithoutNotify();
		}
	}

	private void OnLineNewPointAdded(Vector3 a_newPosition)
	{
		m_syncedLinePositions.Add(a_newPosition);
	}

	private void OnLineLastPointUpdated(Vector3 a_newPosition)
	{
		if (m_syncedLinePositions.Count == 0)
		{
			m_syncedLinePositions.Add(a_newPosition);
		}
		else
		{
			m_syncedLinePositions[m_syncedLinePositions.Count - 1] = a_newPosition;
		}
	}

	private void OnLinePointsReplaced()
	{
		//Renderer index 0 is the line origin and is not part of the synced list
		LineRenderer lineRenderer = m_lineScript.LineRenderer;
		int syncedCount = Mathf.Max(0, lineRenderer.positionCount - 1);

		for (int i = 0; i < syncedCount; i++)
		{
			Vector3 position = lineRenderer.GetPosition(i + 1);
			if (i < m_syncedLinePositions.Count)
			{
				if (m_syncedLinePositions[i] != position)
				{
					m_syncedLinePositions[i] = position;
				}
			}
			else
			{
				m_syncedLinePositions.Add(position);
			}
		}

		while (m_syncedLinePositions.Count > syncedCount)
		{
			m_syncedLinePositions.RemoveAt(m_syncedLinePositions.Count - 1);
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

	private void SyncPointPositions()
	{		
		//the first position is set to 0,0,0 abd the synced positions do not contain that so skip syncing the first one
		m_lineScript.LineRenderer.positionCount = m_syncedLinePositions.Count + 1;

		for(int i = 0; i < m_syncedLinePositions.Count; i++)
		{
			m_lineScript.LineRenderer.SetPosition(i + 1, m_syncedLinePositions[i]);
		}
	}

	private void OnLineFinishedChanged(bool previousValue, bool newValue)
	{
		if (newValue)
		{
			m_lineScript.FinishLineWithoutNotify();
		}
	}

	private void OnPositionListChanged(NetworkListEvent<Vector3> changeEvent)
	{
		SyncPointPositions();
		if (m_lineIsFinished.Value)
		{
			m_lineScript.FinishLineWithoutNotify();
		}
	}
}
