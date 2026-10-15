namespace MSP.Scripts.GisData
{
	/// <summary>
	/// Resolves the dictionary key used for a raster layer's image bytes
	/// (GisDataset.RasterImages), from its POV_Unity.RasterLayer.data path. This is
	/// the same filename-stem extraction RasterLayer.LoadData() and ZipUtil already
	/// do (e.g. "Rastermaps/NS_Bathymetry_Raster_Cut.png" -&gt;
	/// "NS_Bathymetry_Raster_Cut") - duplicated here, rather than reused from
	/// POV_Unity, so SqliteGisDatasetWriter and SqliteGisDataSource agree on the same
	/// key without depending on legacy code.
	/// </summary>
	public static class RasterImageKey
	{
		public static string Resolve(string a_dataPath)
		{
			string[] nameSplit = a_dataPath.Split('/');
			string[] extSplit = nameSplit[nameSplit.Length - 1].Split('.');
			return extSplit[0];
		}
	}
}
