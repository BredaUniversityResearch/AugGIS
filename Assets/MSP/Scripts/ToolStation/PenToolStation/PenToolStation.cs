using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class PenToolStation : ToolStation
{
	[SerializeField]
	private Color[] m_colorEntries;

	[SerializeField]
	private float[] m_thicknessEntries;

	public override void OnNetworkSpawn()
	{
		if(IsServer)
		{
			SpawnToolStationSelection();
		}

		base.OnNetworkSpawn();
	}

	public override void OnCategoryEntrySelected(int a_categoryIndex, int a_entryIndex, XRBaseInteractor a_interactor)
	{
		base.OnCategoryEntrySelected(a_categoryIndex, a_entryIndex, a_interactor);

		Pen targetPen = null;
		Pen interactorPen = a_interactor?.GetComponent<Pen>();
		if (interactorPen != null)
		{
			targetPen = interactorPen;
		}
		else if (CurrentlySpawnedSelection != null)
		{
			targetPen = CurrentlySpawnedSelection.GetComponent<Pen>();
		}

		if (targetPen == null)
		{
			Debug.Assert(false, "Selecting category failed! Pen Reference could not be found!");
			return;
		}

		ToolStationCategoryEntry entry = GetCategoryEntryAtIndex(GetCategoryAtIndex(a_categoryIndex), a_entryIndex);
		if (entry is PenToolStationColorCategoryEntry)
		{
			targetPen.SetColor((entry as PenToolStationColorCategoryEntry).PenColor);
		}
		else if (entry is PenToolStationThicknessCategoryEntry)
		{
			targetPen.SetWidth((entry as PenToolStationThicknessCategoryEntry).ThicknessValue);
		}
	}
}
