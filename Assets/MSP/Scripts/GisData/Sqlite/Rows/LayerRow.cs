using SQLite;

namespace MSP.Scripts.GisData
{
	/// <summary>
	/// Discriminates which extension table (RasterLayerRow or VectorObjectRow) relates
	/// to a given LayerRow.
	/// </summary>
	public enum LayerKind
	{
		Raster,
		Vector
	}

	/// <summary>
	/// Common fields shared by raster and vector layers, mirroring POV_Unity.ALayer.
	/// SortOrder must be preserved explicitly (not inferred from row id) since
	/// ImportedConfigRoot relies on a continuous raster-then-vector index for vertical
	/// offsets and render order.
	///
	/// Id is NOT auto-increment: SqliteGisDatasetWriter assigns it explicitly (equal
	/// to SortOrder at write time) so the exact same value is known immediately by
	/// the host's in-memory ALayer.LayerId, without requiring a DB read-back. This is
	/// the stable, data-sourced layer identity used anywhere layer identity crosses a
	/// network/lookup boundary (e.g. LayerManager's NetworkLists) - as opposed to
	/// ALayer.LayerIndex, which stays a purely render-time positional value (vertical
	/// offset/material render-queue ordering), assigned locally by
	/// ImportedConfigRoot.LoadNextLayer and unrelated to this id.
	/// </summary>
	[Table("layers")]
	public class LayerRow
	{
		[PrimaryKey]
		public int Id { get; set; }

		public LayerKind Kind { get; set; }
		public int SortOrder { get; set; }

		public string Name { get; set; }
		public string Short { get; set; }

		//JSON-encoded string[] (POV_Unity.ALayer.tags)
		public string TagsJson { get; set; }

		//JSON-encoded JObject[] (POV_Unity.ALayer.types)
		public string TypesJson { get; set; }
	}
}
