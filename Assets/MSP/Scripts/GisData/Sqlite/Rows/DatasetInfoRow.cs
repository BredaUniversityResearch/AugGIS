using SQLite;

namespace MSP.Scripts.GisData
{
	/// <summary>
	/// Singleton row describing the currently imported dataset (header fields from
	/// POV_Unity.ConfigMetaData/DataConfigContent, plus import bookkeeping). There is
	/// always exactly one row, with Id == SingletonId - callers should upsert
	/// (InsertOrReplace) rather than insert additional rows.
	/// </summary>
	[Table("dataset_info")]
	public class DatasetInfoRow
	{
		public const int SingletonId = 1;

		[PrimaryKey]
		public int Id { get; set; } = SingletonId;

		//From POV_Unity.ConfigMetaData
		public string DataModelHash { get; set; }
		public string DateModified { get; set; }
		public string EditorVersion { get; set; }
		public int Errors { get; set; }

		//From POV_Unity.DataConfigContent
		public string Projection { get; set; }
		public float Coordinate0X { get; set; }
		public float Coordinate0Y { get; set; }
		public float Coordinate1X { get; set; }
		public float Coordinate1Y { get; set; }

		//Import bookkeeping
		public string Source { get; set; }
		public int SchemaVersion { get; set; }
		public string ImportedAt { get; set; }
	}
}
