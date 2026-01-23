using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRGrabInteractable))]
public class NetworkedXRGrabInteractable : NetworkBehaviour
{
	private MSPXRGrabInteractable m_xrGrabInteractable;

	[SerializeField]
	[Tooltip("Network object whose ownership will be controlled.\nIf not set, the component will look for a NetworkObject on the same GameObject.")]
	private NetworkObject m_networkObject;

	private NetworkVariable<int> m_grabOwnerId = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

	private bool CanInteract => (int)NetworkManager.Singleton.LocalClientId == m_grabOwnerId.Value || m_grabOwnerId.Value == -1;

	private void Awake()
	{
		m_xrGrabInteractable = GetComponent<MSPXRGrabInteractable>();
		if (m_networkObject == null)
        {   
			m_networkObject = GetComponent<NetworkObject>();
        }

		if (m_networkObject == null)
		{
			Debug.LogError("NetworkedXRGrabInteractable requires a NetworkObject component.", this);
		}

		m_xrGrabInteractable.selectEntered.AddListener(OnGrabInteractableSelectEntered);
		m_xrGrabInteractable.selectExited.AddListener(OnGrabInteractableSelectExit);

		m_grabOwnerId.OnValueChanged += OnGrabOwnerIDValueChanged;
	}

	private void OnGrabOwnerIDValueChanged(int previousValue, int newValue)
	{
		if (CanInteract)
		{
			m_xrGrabInteractable.AllowInteraction();
		}
		else
		{
			m_xrGrabInteractable.BlockInteraction();
		}
	}

	private void OnGrabInteractableSelectEntered(SelectEnterEventArgs a_args)
	{
		if (!CanInteract)
		{
			return;
		}

		TakeNetworkOwnership();
	}

	private void OnGrabInteractableSelectExit(SelectExitEventArgs a_args)
	{
		if (!CanInteract)
		{
			return;
		}
		
		RemoveNetworkOwnership();
	}

	[Button()]
	[HideInEditorMode]
	public void TakeNetworkOwnership()
	{
		ChangeOwnerServerRPC(new RpcParams());
	}

	[Button()]
	[HideInEditorMode]
	public void RemoveNetworkOwnership()
	{
		RemoveOwnershipServerRPC();
	}

	[Rpc(SendTo.Server, RequireOwnership = false)]
	private void ChangeOwnerServerRPC(RpcParams rpcParams)
	{
		if (m_networkObject.OwnerClientId != rpcParams.Receive.SenderClientId)
		{
			m_networkObject.ChangeOwnership(rpcParams.Receive.SenderClientId);
		}

		m_grabOwnerId.Value = (int)rpcParams.Receive.SenderClientId;
	}

	[Rpc(SendTo.Server, RequireOwnership = false)]
	private void RemoveOwnershipServerRPC()
	{
		m_networkObject.RemoveOwnership();
		m_grabOwnerId.Value = -1;
	}
}
