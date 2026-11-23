namespace MSP.Scripts.GisData
{
	/// <summary>
	/// Converts between WGS84 longitude/latitude (what GeoJSON uses) and the dataset's
	/// own projected coordinates (the PROJ string stored in DatasetInfoRow.Projection).
	/// Implementations are pure maths: no Unity API, no shared mutable state, so they
	/// can be used from thread-pool threads (e.g. during upload handling).
	/// </summary>
	public interface ICoordinateConverter
	{
		/// <summary>
		/// a_lon/a_lat in degrees, result in the dataset projection's units (metres).
		/// </summary>
		void FromWgs84(double a_lon, double a_lat, out double a_x, out double a_y);

		/// <summary>
		/// Inverse of FromWgs84. Result in degrees.
		/// </summary>
		void ToWgs84(double a_x, double a_y, out double a_lon, out double a_lat);
	}
}
