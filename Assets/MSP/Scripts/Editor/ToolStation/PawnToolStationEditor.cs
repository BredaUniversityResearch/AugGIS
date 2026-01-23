using UnityEditor;

[CustomEditor(typeof(PawnToolStation))]
public class PawnToolStationEditor : ToolStationEditor
{
    protected override void OnCategoryEntryCreated(ToolStationCategoryEntry a_entry, CategorySettings a_categorySettings, CategoryEntrySettings a_entrySettings)
    {
        base.OnCategoryEntryCreated(a_entry, a_categorySettings, a_entrySettings);

		(a_entry as PawnToolStationCategoryEntry).SetVisuals(a_entrySettings.visualPrefab);
    }
}
