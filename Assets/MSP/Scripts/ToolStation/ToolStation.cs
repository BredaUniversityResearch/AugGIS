using System;
using System.Collections.Generic;
using ColourPalette;
using POV_Unity;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class ToolStation : NetworkBehaviour
{
	public struct IndexData : INetworkSerializable, System.IEquatable<IndexData>
	{
		public int CategoryIndex;
		public int EntryIndex;

		public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
		{
			if (serializer.IsReader)
			{
				var reader = serializer.GetFastBufferReader();
				reader.ReadValueSafe(out CategoryIndex);
				reader.ReadValueSafe(out EntryIndex);
			}
			else
			{
				var writer = serializer.GetFastBufferWriter();
				writer.WriteValueSafe(CategoryIndex);
				writer.WriteValueSafe(EntryIndex);
			}
		}

		public bool Equals(IndexData other)
		{
			return CategoryIndex == other.CategoryIndex && EntryIndex == other.EntryIndex;
		}
	}
	
	[Serializable]
	public struct ArchSettings
	{
		public const float k_segmentsPer360Degrees = 100f;
		public float archRadius;
		public float archThickness;
		public float spacingBetweenEntries;

		public ArchSettings( float a_archRadius, 
							 float a_archThickness, 
							 float a_spacing)
		{
			archRadius = a_archRadius;
			archThickness = a_archThickness;
			spacingBetweenEntries = a_spacing;
		}
	}

	[SerializeField]
	[Required]
	ToolStationElementConfig m_menuStationElementConfig;

	[SerializeField]
	[Required]
	private XRGrabInteractable m_xrGrabInteractable;

	[SerializeField]
	[Required]
	private TransformReference m_rootTransformReference;

	[SerializeField]
	[Required]
	private GameObjectSet m_set;
	
	[SerializeField]
	private ToolStationSelection m_toolStationSelectionPrefab;
	public Type ToolStationSelectionType => m_toolStationSelectionPrefab.GetType();

	[SerializeField]
	private float m_selectionGrabDistanceThreshold = 0.15f;

	public float SelectionGrabDistanceThreshold => m_selectionGrabDistanceThreshold;

	[SerializeField]
	[ReadOnly]
	private List<ToolStationCategory> m_allCategories;

	[Space]
	[Header("Arch Settings")]
	[SerializeField]
	private ArchSettings m_categoryArchSettings = new ArchSettings(0.25f, 0.05f, 1f);

	[SerializeField]
	private ArchSettings m_categoryEntryArchSettings = new ArchSettings(0.35f, 0.1f, 1f);

	[Space]
	[Header("Color Settings")]

	[SerializeField]
	private ColourAsset m_categoryColor;

	[SerializeField]
	private ColourAsset m_categoryEntryColor;

	private bool isAppPlaying => Application.isPlaying;

	private ToolStationSelection m_currentSpawnedSelection = null;
	public ToolStationSelection CurrentlySpawnedSelection => m_currentSpawnedSelection;

	NetworkVariable<IndexData> m_currentSelectionIndexData = new NetworkVariable<IndexData>(new IndexData() { CategoryIndex = -1, EntryIndex = -1 }, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
	public int CurrentlySelectedCategoryIndex => m_currentSelectionIndexData.Value.CategoryIndex;
	public int CurrentlySelectedEntryIndex => m_currentSelectionIndexData.Value.EntryIndex;

	public override void OnNetworkSpawn()
	{
		base.OnNetworkSpawn();

		m_currentSelectionIndexData.OnValueChanged += OnCurrentlySelectedIndexDataChanged;

		if (!IsServer)
		{
			SetCurrentSelectionReferenceServerRPC();
		}

		if (m_allCategories.Count > 0)
		{
			if (IsServer)
			{
				SelectCategoryWithInteractor(0, null);
				SelectCategoryEntryWithInteractor(0, null);
			}
			else
			{
				OnCurrentlySelectedCategoryIndexChanged(-1, m_currentSelectionIndexData.Value.CategoryIndex);
				OnCurrentlySelectedCategoryEntryIndexChanged(m_currentSelectionIndexData.Value.CategoryIndex, -1, m_currentSelectionIndexData.Value.EntryIndex);
			}
		}

		m_set.Add(gameObject);

		if (IsSpawned)
		{
			transform.parent = m_rootTransformReference.TransformRef; 
		}

		m_xrGrabInteractable.selectEntered.AddListener(OnGrabbed);
		m_xrGrabInteractable.selectExited.AddListener(OnReleased);
	}

	public override void OnNetworkDespawn()
	{
		if (IsServer && CurrentlySpawnedSelection != null)
		{
			CurrentlySpawnedSelection.GetComponent<NetworkObject>().Despawn(true);
		}

		base.OnNetworkDespawn();
		m_currentSelectionIndexData.OnValueChanged -= OnCurrentlySelectedIndexDataChanged;

		m_set.Remove(gameObject);

		if (!IsSpawned && !IsServer)
		{
			Destroy(gameObject);
		}
	}

	public virtual void OnSelectionDetached(ToolStationSelection a_selection)
	{
		SpawnToolStationSelection();
	}

	private void OnCurrentlySelectedIndexDataChanged(IndexData previousValue, IndexData newValue)
	{
		if (previousValue.CategoryIndex != newValue.CategoryIndex)
		{
			OnCurrentlySelectedCategoryIndexChanged(previousValue.CategoryIndex, newValue.CategoryIndex);
		}

		if (previousValue.CategoryIndex != newValue.CategoryIndex || previousValue.EntryIndex != newValue.EntryIndex)
		{
			OnCurrentlySelectedCategoryEntryIndexChanged(newValue.CategoryIndex, previousValue.EntryIndex, newValue.EntryIndex);
		}
	}

	private void OnCurrentlySelectedCategoryIndexChanged(int a_previousValue, int a_newValue)
	{
		if (a_previousValue >= 0)
		{
			ToolStationCategory prevSelectedCategory = m_allCategories[a_previousValue];

			prevSelectedCategory.DisableCategoryEntries();
			prevSelectedCategory.DeActivateXRInteractable();
		}

		ToolStationCategory categorySelected = m_allCategories[a_newValue];

		for (int i = 0; i < categorySelected.EntryCount; i++)
		{
			categorySelected.GetEntryAtIndex(i).DeActivateXRInteractable();
		}

		categorySelected.EnableCategoryEntries();
		categorySelected.ActivateXRInteractable();
	}

	private void OnCurrentlySelectedCategoryEntryIndexChanged(int a_categoryIndex, int a_previousEntryIndex, int a_newEntryIndex)
	{
		ToolStationCategory categorySelected = m_allCategories[a_categoryIndex];

		for (int i = 0; i < categorySelected.EntryCount; i++)
		{
			categorySelected.GetEntryAtIndex(i).DeActivateXRInteractable();
		}

		ToolStationCategoryEntry currentlySelectedCategoryEntry = m_allCategories[a_categoryIndex].GetEntryAtIndex(a_newEntryIndex);
		currentlySelectedCategoryEntry.ActivateXRInteractable();
	}

	public void StartSelectForElement(ToolStationElementBase a_element, XRBaseInteractor a_interactor)
	{
		if (a_element as ToolStationCategory != null)
		{
			ToolStationCategory category = a_element as ToolStationCategory;
			SelectCategoryWithInteractor(category.IndexInMenu, a_interactor);
		}
		else if (a_element as ToolStationCategoryEntry != null)
		{
			ToolStationCategoryEntry entry = a_element as ToolStationCategoryEntry;
			SelectCategoryEntryWithInteractor(entry.IndexInMenu, a_interactor);
		}
	}

	public virtual void OnCategorySelected(int a_categoryIndex, XRBaseInteractor a_interactor)
	{

	}

	public virtual void OnCategoryEntrySelected(int a_categoryIndex, int a_categoryEntryIndex, XRBaseInteractor a_interactor)
	{

	}

	public void SelectCategoryWithInteractor(int a_categoryIndex, XRBaseInteractor a_interactor)
	{
		if (a_categoryIndex == m_currentSelectionIndexData.Value.CategoryIndex)
		{
			return;
		}

		OnCategorySelected(a_categoryIndex, a_interactor);
		OnCategoryEntrySelected(a_categoryIndex, 0, a_interactor);

		SetCategoryIndexServerRPC(a_categoryIndex);
	}

	[Rpc(SendTo.Server, RequireOwnership = false)]
	private void SetCategoryIndexServerRPC(int a_categoryIndex)
	{
		IndexData indexData = m_currentSelectionIndexData.Value;
		indexData.CategoryIndex = a_categoryIndex;
		indexData.EntryIndex = 0; // select the first entry when swapping categories 

		m_currentSelectionIndexData.Value = indexData;
	}

	public void SelectCategoryEntryWithInteractor(int a_entryIndex, XRBaseInteractor a_interactor)
	{
		OnCategoryEntrySelected(CurrentlySelectedCategoryIndex, a_entryIndex, a_interactor);

		SetCategoryEntryIndexServerRPC(a_entryIndex);
	}

	[Rpc(SendTo.Server, RequireOwnership = false)]
	private void SetCategoryEntryIndexServerRPC(int a_entryIndex)
	{
		IndexData indexData = m_currentSelectionIndexData.Value;
		indexData.EntryIndex = a_entryIndex;
		m_currentSelectionIndexData.Value = indexData;
	}

	public void SpawnToolStationSelection()
	{
		SpawnToolStationSelectionRPC();
	}

	[Rpc(SendTo.Server, RequireOwnership = false)]
	private void SpawnToolStationSelectionRPC()
	{
		m_currentSpawnedSelection = Instantiate(m_toolStationSelectionPrefab);
		NetworkObject spawnedSelectionNetworkObject = m_currentSpawnedSelection.GetComponent<NetworkObject>();
		m_currentSpawnedSelection.HandleParentingLocally();
		m_currentSpawnedSelection.transform.position = transform.position;
		m_currentSpawnedSelection.transform.rotation = transform.rotation;
		spawnedSelectionNetworkObject.Spawn();
		if (!this.IsOwnedByServer) // If the station is dragged by a client and the user just took the selection out, transfer ownership to the current selection as well
		{
			spawnedSelectionNetworkObject.ChangeOwnership(OwnerClientId);
		}
		m_currentSpawnedSelection.SetEntryIndices(CurrentlySelectedCategoryIndex, CurrentlySelectedEntryIndex);
		m_currentSpawnedSelection.SetToolStation(this);
		SpawnToolStationSelectionClientRPC(this, m_currentSpawnedSelection);
	}

	[Rpc(SendTo.NotServer, RequireOwnership = false)]
	private void SpawnToolStationSelectionClientRPC(NetworkBehaviourReference a_pawnMenuStationReference, NetworkBehaviourReference a_selectionReference)
	{
		if (a_pawnMenuStationReference.TryGet(out ToolStation toolStation) && a_selectionReference.TryGet(out ToolStationSelection selection))
		{
			m_currentSpawnedSelection = selection;
			selection.SetToolStation(toolStation);
		}
	}

	[Rpc(SendTo.Server, RequireOwnership = false)]
	private void SetCurrentSelectionReferenceServerRPC()
	{
		SetCurrentSelectionReferenceClientRPC(m_currentSpawnedSelection);
	}

	[Rpc(SendTo.NotServer, RequireOwnership = false)]
	private void SetCurrentSelectionReferenceClientRPC(NetworkBehaviourReference a_selectionReference)
	{
		if (a_selectionReference.TryGet(out ToolStationSelection selection))
		{
			m_currentSpawnedSelection = selection;
			m_currentSpawnedSelection.SetToolStation(this);
		}
	}

	public void SetCurrentSelection(ToolStationSelection a_newSelection)
	{
		m_currentSpawnedSelection = a_newSelection;
	}

	public ToolStationCategory GetCategoryAtIndex(int a_index)
	{
		return m_allCategories[a_index];
	}

	public ToolStationCategoryEntry GetCategoryEntryAtIndex(ToolStationCategory a_category, int a_entryIndex)
	{
		return a_category.GetEntryAtIndex(a_entryIndex);
	}

	public ToolStationCategory GetCurrentlySelectedCategory()
	{
		return m_allCategories[CurrentlySelectedCategoryIndex];
	}

	public ToolStationCategoryEntry GetCurrentlySelectedCategoryEntry()
	{
		return GetCurrentlySelectedCategory().GetEntryAtIndex(CurrentlySelectedEntryIndex);
	}

	void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.yellow;
		Gizmos.DrawWireSphere(transform.position, m_selectionGrabDistanceThreshold);
	}


	// Own the selection when the station is grabbed 
	private void OnGrabbed(SelectEnterEventArgs a_args)
	{
		RequestSelectionOwnershipServerRPC(NetworkManager.Singleton.LocalClientId);
	}

	[Rpc(SendTo.Server, RequireOwnership = false)]
	private void RequestSelectionOwnershipServerRPC(ulong a_requestingClientId)
	{
		NetworkObject selectionNetworkObject = m_currentSpawnedSelection.GetComponent<NetworkObject>();
		selectionNetworkObject.ChangeOwnership(a_requestingClientId);
	}

	private void OnReleased(SelectExitEventArgs a_args)
	{
		ReleaseSelectionOwnershipServerRPC();	
	}

	[Rpc(SendTo.Server, RequireOwnership = false)]
	private void ReleaseSelectionOwnershipServerRPC()
	{
		NetworkObject selectionNetworkObject = m_currentSpawnedSelection.GetComponent<NetworkObject>();
		selectionNetworkObject.RemoveOwnership();
	}
}
