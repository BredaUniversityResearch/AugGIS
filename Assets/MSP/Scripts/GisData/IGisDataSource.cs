using System;
using System.Collections;
using System.Collections.Generic;
using POV_Unity;

namespace MSP.Scripts.GisData
{
	/// <summary>
	/// Abstracts where a GIS dataset comes from, so renderer code (ImportedConfigRoot,
	/// AssetManager, ALayer and subclasses) does not need to care whether it was
	/// loaded from the legacy MSP Challenge zip pipeline or from the local SQLite
	/// database. Implementations hand back the existing POV_Unity.DataConfig/ALayer
	/// types unchanged - no parallel data model is introduced.
	///
	/// This is a Phase A contract: it intentionally only supports loading a whole
	/// dataset at once (matching today's behaviour), not partial/streamed fetches.
	/// It is expected to change, possibly in breaking ways, once that becomes
	/// relevant.
	/// </summary>
	public interface IGisDataSource
	{
		/// <summary>
		/// Loads the dataset. Returns an IEnumerator so callers can run it as a
		/// coroutine (e.g. StartCoroutine(dataSource.LoadDataset(...))), matching the
		/// existing ZipUtil.LoadZip/ALayer.LoadData conventions elsewhere in the
		/// codebase.
		/// </summary>
		IEnumerator LoadDataset(Action<GisDataset> a_onComplete, Action a_onFailure);
	}

	/// <summary>
	/// Thin envelope around a loaded dataset - not a new data model. Config is the
	/// existing POV_Unity.DataConfig as consumed by ImportedConfigRoot.Initialise().
	/// RasterImages holds raw (still-encoded) image bytes keyed the same way
	/// ZipUtil/AssetManager key them today (filename stem, e.g.
	/// "NS_Bathymetry_Raster_Cut") - decoding to Texture2D is deliberately left to the
	/// caller, not performed by a data source.
	/// </summary>
	public class GisDataset
	{
		public DataConfig Config;
		public IReadOnlyDictionary<string, byte[]> RasterImages;

		//Identifies which IGisDataSource implementation produced this dataset (e.g.
		//"msp_challenge_zip"), so it can be persisted and later inspected/debugged.
		public string Source;
	}
}
