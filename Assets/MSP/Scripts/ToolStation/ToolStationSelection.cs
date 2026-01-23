using System;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ToolStationSelection : NetworkBehaviour
{
	[Serializable]
	public enum EState
	{
		Idle,
		Grabbed,
		Released,
	}

	public event Action Grabbed;
	public event Action Released;

	[SerializeField]
	[Required]
	private TransformReference m_defaultRootTransformReference;
	public Transform DefaultRootTransform => m_defaultRootTransformReference.TransformRef;

	[SerializeField]
	private GameObjectSet m_set;
	public int SetCount => m_set.Count;

	[SerializeField]
	[Required]
	private XRGrabInteractable m_xrGrabInteractable;

	private EState m_defaultState = EState.Idle;
	[SerializeField]
	[ReadOnly]
	private NetworkVariable<EState> m_currentState = new NetworkVariable<EState>(EState.Idle, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

	public EState CurrentState => m_currentState.Value;

	protected ToolStation.IndexData m_defaultIndexData = new ToolStation.IndexData() { CategoryIndex = -1, EntryIndex = -1 };
	protected NetworkVariable<ToolStation.IndexData> m_stationIndexData = new NetworkVariable<ToolStation.IndexData>(new ToolStation.IndexData() { CategoryIndex = -1, EntryIndex = -1 }, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
	public ToolStation.IndexData StationIndexData => m_stationIndexData.Value;

	protected ToolStation m_toolStation;

	protected NetworkVariable<bool> m_trashState = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

	[SerializeField]
	protected InteractableMaterialHandler m_interactableVisualHandler;

	[SerializeField]
	private LayerMask m_destroyLayerMask;

    public void Start()
    {
#if UNITY_EDITOR
		if (m_interactableVisualHandler == null)
		{
			Debug.LogError("No pawn visual root assigned. We can't handle pawn material (especially trash state). \nThis might throw exceptions at runtime.", this);
		}
#endif
    }

	public void SetDefaultEntryIndices(int a_categoryIndex, int a_entryIndex)
	{
#if UNITY_EDITOR
		if (IsSpawned)
		{
			Debug.LogWarning("Not allowed to set a pre-spawn entry indices on a spawned object.", this);
			return;
		}
#endif

		m_defaultIndexData.CategoryIndex = a_categoryIndex;
		m_defaultIndexData.EntryIndex = a_entryIndex;
	}

	public void SetDefaultState(EState a_state)
	{
#if UNITY_EDITOR
		if (IsSpawned)
		{
			Debug.LogWarning("Not allowed to set a pre-spawn state on a spawned object.", this);
			return;
		}
#endif
		m_defaultState = a_state;
	}

	public override void OnNetworkSpawn()
	{
		if (NetworkManager.Singleton.IsServer)
		{
			m_currentState.Value = m_defaultState;
			m_stationIndexData.Value = m_defaultIndexData;
		}

		base.OnNetworkSpawn();

		m_xrGrabInteractable.selectEntered.AddListener(OnGrabbed);
		m_xrGrabInteractable.selectExited.AddListener(OnReleased);

		m_trashState.OnValueChanged += OnTrashStateChanged;


		if(m_interactableVisualHandler != null)
		{
			OnTrashStateChanged(!m_trashState.Value, m_trashState.Value);
		}
		// Else, it should be assigned in the child class (where the visuals are most likely handled as well).
		// OnTrashStateChanged must also be called there to properly update the visuals on spawn.

		m_set.Add(gameObject);
	}

    protected override void OnNetworkPostSpawn()
    {
        base.OnNetworkPostSpawn();
		if (!IsServer)
        {
			HandleParentingLocally();
        }
    }

	public override void OnNetworkDespawn()
	{
		base.OnNetworkDespawn();

		m_set.Remove(gameObject);
	}

	private void OnGrabbed(SelectEnterEventArgs a_args)
	{
		SetState(EState.Grabbed);
		Grabbed?.Invoke();
	}

	private void OnReleased(SelectExitEventArgs a_args)
	{
		if (m_currentState.Value != EState.Grabbed)
		{
			return;
		}

		SetState(EState.Released);
		Released?.Invoke();
	}

	public void SetState(EState a_newState)
	{
		SetStateServerRPC(a_newState);
	}

	[Rpc(SendTo.Server, RequireOwnership = false)]
	private void SetStateServerRPC(EState a_newState)
	{
		m_currentState.Value = a_newState;
	}

	public void SetToolStation(ToolStation a_toolStation)
	{
		m_toolStation = a_toolStation;
	}

	public void SetEntryIndices(int a_categoryIndex, int a_entryIndex)
	{
		SetEntryIndicesServerRPC(a_categoryIndex, a_entryIndex);
	}

	[Rpc(SendTo.Server, RequireOwnership = false)]
	private void SetEntryIndicesServerRPC(int a_categoryIndex, int a_entryIndex)
	{
		ToolStation.IndexData indexData = new ToolStation.IndexData();
		indexData.CategoryIndex = a_categoryIndex;
		indexData.EntryIndex = a_entryIndex;
		m_stationIndexData.Value = indexData;
	}

	void Update()
	{
		bool stationIsDragged = IsOwner && m_toolStation != null && m_currentState.Value == EState.Idle;
		if (stationIsDragged)
        {
            transform.position = m_toolStation.transform.position;
			transform.rotation = m_toolStation.transform.rotation;
        }

		if (!IsServer)
		{
			return;
		}

		if (m_toolStation == null || m_toolStation.CurrentlySpawnedSelection != this || m_currentState.Value == EState.Idle)
		{
			return;
		}

		if (Vector3.Distance(transform.position, m_toolStation.transform.position) <= m_toolStation.SelectionGrabDistanceThreshold)
		{
			if (m_currentState.Value == EState.Released)
			{
				SetState(EState.Idle);
			}
		}
		else
		{
			m_toolStation.OnSelectionDetached(this);
			OnToolStationDetachedRPC();
		}
	}

	[Rpc(SendTo.Everyone, RequireOwnership = false)]
	private void OnToolStationDetachedRPC()
    {
        m_toolStation = null;
		HandleParentingLocally();
    }

	[Rpc(SendTo.Everyone, RequireOwnership = false)]
	void HandleParentingRPC()
	{
		HandleParentingLocally();
	}

	public void HandleParentingLocally()
	{
		transform.SetParent(m_defaultRootTransformReference.TransformRef, true);
	}

	void OnCollisionStay(Collision collision)
	{
		if (!IsServer || m_currentState.Value != EState.Released)
		{
			return;
		}

		if (IsLayerInDestroyLayerMask(collision.gameObject.layer) || m_trashState.Value)
		{
			if (m_toolStation != null && m_toolStation.CurrentlySpawnedSelection == this)
			{
				m_toolStation.SetCurrentSelection(null);
				m_toolStation.SpawnToolStationSelection();
			}

			NetworkObject networkObject = GetComponent<NetworkObject>();
			if (networkObject.IsSpawned)
			{
				networkObject.Despawn();
			}
		}
	}


	void OnCollisionEnter(Collision collision)
	{
		if (!IsOwner || m_toolStation != null || m_currentState.Value != EState.Grabbed)
		{
			return;
		}

		if (IsLayerInDestroyLayerMask(collision.gameObject.layer))
		{
			TrashStateServerRpc(true);
		}
	}

	void OnCollisionExit(Collision collision)
	{
		if (!IsOwner || m_currentState.Value != EState.Grabbed)
		{
			return;
		}

		if (IsLayerInDestroyLayerMask(collision.gameObject.layer))
		{
			TrashStateServerRpc(false);
		}
	}

	[ServerRpc(RequireOwnership = false)]
	void TrashStateServerRpc(bool trashState)
	{
		m_trashState.Value = trashState;
	}

	protected void OnTrashStateChanged(bool a_prevValue, bool a_newValue)
	{
		m_interactableVisualHandler.UpdateTrashVisuals(a_newValue);
	}

	private bool IsLayerInDestroyLayerMask(int a_layer)
	{
		return (m_destroyLayerMask.value & (1 << a_layer)) != 0;
	}
}