using SQLite;

namespace MSP.Scripts.GisData
{
	/// <summary>
	/// 1:1 extension of a LayerRow where Kind == LayerKind.Raster, mirroring
	/// POV_Unity.RasterLayer. RasterPng stores the original PNG bytes as found in the
	/// MSPC zip, unmodified - any TypeMap pixel remapping stays runtime-only logic in
	/// RasterLayer.LoadData() and is not baked into the stored bytes.
	/// </summary>
	[Table("raster_layers")]
	public class RasterLayerRow
	{
		//Shared primary key with the owning LayerRow (not auto-increment).
		[PrimaryKey]
		public int LayerId { get; set; }

		public float Coordinate0X { get; set; }
		public float Coordinate0Y { get; set; }
		public float Coordinate1X { get; set; }
		public float Coordinate1Y { get; set; }

		public byte[] RasterPng { get; set; }

		//From POV_Unity.ScaleConfig (null when the layer has no scale, e.g. TypeMaps)
		public string ScaleInterpolation { get; set; }
		public float ScaleMinValue { get; set; }
		public float ScaleMaxValue { get; set; }

		//JSON-encoded ScaleConfigGrouping[] (only used for LinGrouped interpolation)
		public string ScaleGroupsJson { get; set; }

		//JSON-encoded POV_Unity.TypeMapping[] (only used for TypeMap layers)
		public string MappingJson { get; set; }
	}
}
