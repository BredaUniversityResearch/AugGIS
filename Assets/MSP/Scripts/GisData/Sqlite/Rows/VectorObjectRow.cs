using SQLite;

namespace MSP.Scripts.GisData
{
	/// <summary>
	/// A single feature belonging to a LayerRow where Kind == LayerKind.Vector,
	/// mirroring POV_Unity.VectorObject. ObjectIndex preserves the original array
	/// order from the source config.
	/// </summary>
	[Table("vector_objects")]
	public class VectorObjectRow
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }

		[Indexed]
		public int LayerId { get; set; }

		public int ObjectIndex { get; set; }

		//JSON-encoded float[][] (POV_Unity.VectorObject.points)
		public string PointsJson { get; set; }

		//JSON-encoded float[][][] (POV_Unity.VectorObject.gaps), null when not a polygon
		public string GapsJson { get; set; }

		//JSON-encoded int[] - indices into the owning layer's TypesJson catalog
		public string TypesJson { get; set; }

		//JSON-encoded JObject (POV_Unity.VectorObject.metadata)
		public string MetadataJson { get; set; }
	}
}
