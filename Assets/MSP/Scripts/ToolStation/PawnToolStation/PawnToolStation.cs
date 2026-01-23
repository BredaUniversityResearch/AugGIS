using Unity.Netcode;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class PawnToolStation : ToolStation
{
	public override void OnCategoryEntrySelected(int a_categoryIndex, int a_categoryEntryIndex, XRBaseInteractor a_interactor)
	{
		base.OnCategoryEntrySelected(a_categoryIndex ,a_categoryEntryIndex, a_interactor);
		UpdateCurrentPawnSelectionServerRPC(a_categoryIndex, a_categoryEntryIndex);
	}

	public override void OnSelectionDetached(ToolStationSelection a_selection)
	{
		SetCurrentSelection(null);
		UpdateCurrentPawnSelectionServerRPC(CurrentlySelectedCategoryIndex, CurrentlySelectedEntryIndex);
	}
 
	[Rpc(SendTo.Server, RequireOwnership = false)]
	protected void UpdateCurrentPawnSelectionServerRPC(int a_categoryIndex, int a_entryIndex)
	{
		if(CurrentlySpawnedSelection == null)
		{
			SpawnToolStationSelection();
		}

		PawnToolStationCategoryEntry entry = (PawnToolStationCategoryEntry)GetCategoryEntryAtIndex(GetCategoryAtIndex(a_categoryIndex), a_entryIndex);
		CurrentlySpawnedSelection.SetEntryIndices(a_categoryIndex, a_entryIndex);
		(CurrentlySpawnedSelection as PawnToolStationSelection).SetVisuals(entry.VisualsPrefab);
		UpdateCurrentPawnVisualsClientRPC(CurrentlySpawnedSelection, a_categoryIndex, a_entryIndex);
	}

	[Rpc(SendTo.NotServer, RequireOwnership = false)]
	private void UpdateCurrentPawnVisualsClientRPC( NetworkBehaviourReference a_selectionReference, int a_categoryIndex, int a_entryIndex)
	{
		if (a_selectionReference.TryGet(out ToolStationSelection currentSelection))
		{
			SetCurrentSelection(currentSelection);
			PawnToolStationCategoryEntry entry = (PawnToolStationCategoryEntry)GetCategoryEntryAtIndex(GetCategoryAtIndex(a_categoryIndex), a_entryIndex);
			(CurrentlySpawnedSelection as PawnToolStationSelection).SetVisuals(entry.VisualsPrefab);
		}
	}
}
