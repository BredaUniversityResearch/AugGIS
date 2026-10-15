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
	/// </summary>
	[Table("layers")]
	public class LayerRow
	{
		[PrimaryKey, AutoIncrement]
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
