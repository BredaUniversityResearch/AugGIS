#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using MSP.Scripts.GisData;
using POV_Unity;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;

namespace MSP.Scripts.GisData.Editor
{
	/// <summary>
	/// Standalone proof that a real MSP Challenge dataset survives a round trip
	/// through SQLite unchanged: loads a locally bundled zip via
	/// MspChallengeZipDataSource (the same mechanism ConfigImporterWindow already
	/// uses for in-Editor testing - no network access), writes it with
	/// SqliteGisDatasetWriter, reads it back with SqliteGisDataSource, and logs a
	/// comparison. Entirely independent of WorldManager/ImportedConfigRoot/the live
	/// host-client session - this does not wire SQLite into the real data flow, it
	/// only proves the mechanism works.
	/// </summary>
	public class GisSqliteImportProofWindow : OdinEditorWindow
	{
		[ValueDropdown(nameof(GetAvailableZipFileNames))]
		[SerializeField] private string m_fileName;

		[MenuItem("Tools/GIS Data/SQLite Import Proof")]
		private static void OpenWindow()
		{
			var window = GetWindow<GisSqliteImportProofWindow>();
			window.position = GUIHelper.GetEditorWindowRect().AlignCenter(420, 120);
		}

		//Lists the zip files bundled under Plugins/POV_Unity/Resources - the same
		//folder/naming convention MspChallengeZipDataSource.FromApplicationFolder and
		//ConfigImporterWindow already use, just surfaced here as a dropdown for
		//convenience instead of free text.
		private static IEnumerable<string> GetAvailableZipFileNames()
		{
			string resourcesPath = Path.Combine(Application.dataPath, "Plugins", "POV_Unity", "Resources");
			if (!Directory.Exists(resourcesPath))
				yield break;

			foreach (string filePath in Directory.GetFiles(resourcesPath, "*.zip"))
				yield return Path.GetFileNameWithoutExtension(filePath);
		}

		[Button("Run Proof")]
		public void RunProof()
		{
			//RuntimeInitializeOnLoadMethod (the normal path) only fires on entering Play
			//mode/builds, not for this Editor-only tool - so it's ensured explicitly here
			//instead, rather than changing when the automatic bootstrap runs.
			SqliteRuntimeBootstrap.EnsureInitialised();

			MspChallengeZipDataSource dataSource = MspChallengeZipDataSource.FromApplicationFolder(m_fileName);
			EditorCoroutineUtility.StartCoroutine(dataSource.LoadDataset(OnDatasetLoaded, OnLoadFailure), this);
		}

		private void OnLoadFailure()
		{
			Debug.LogError("[GisData Proof] Failed to load the source zip.");
		}

		private void OnDatasetLoaded(GisDataset a_original)
		{
			Debug.Log($"[GisData Proof] Loaded source dataset: {a_original.Config.datamodel.raster_layers.Length} raster layers, " +
				$"{a_original.Config.datamodel.vector_layers.Length} vector layers, {a_original.RasterImages.Count} raster images.");

			//Distinct, disposable file - kept separate from whatever network-transport-swap
			//later treats as "the real" production database.
			string databasePath = Path.Combine(Application.persistentDataPath, "gisdata_import_proof.db");
			if (File.Exists(databasePath))
				File.Delete(databasePath);

			GisDatabaseService database = new GisDatabaseService(databasePath);
			database.Open();

			try
			{
				new SqliteGisDatasetWriter(database).Write(a_original);
				Debug.Log($"[GisData Proof] Wrote dataset to {databasePath}");

				EditorCoroutineUtility.StartCoroutine(
					new SqliteGisDataSource(database).LoadDataset(
						a_readBack => Compare(a_original, a_readBack, database),
						() => OnReadBackFailure(database)),
					this);
			}
			catch (Exception ex)
			{
				Debug.LogError($"[GisData Proof] Failed: {ex}");
				database.Dispose();
			}
		}

		private void OnReadBackFailure(GisDatabaseService a_database)
		{
			Debug.LogError("[GisData Proof] Failed to read the dataset back from SQLite.");
			a_database.Dispose();
		}

		private void Compare(GisDataset a_original, GisDataset a_readBack, GisDatabaseService a_database)
		{
			try
			{
				bool success = true;

				success &= CheckEqual("raster layer count", a_original.Config.datamodel.raster_layers.Length, a_readBack.Config.datamodel.raster_layers.Length);
				success &= CheckEqual("vector layer count", a_original.Config.datamodel.vector_layers.Length, a_readBack.Config.datamodel.vector_layers.Length);
				success &= CheckEqual("raster image count", a_original.RasterImages.Count, a_readBack.RasterImages.Count);
				success &= CheckEqual("source", a_original.Source, a_readBack.Source);

				int rasterLayerCount = Math.Min(a_original.Config.datamodel.raster_layers.Length, a_readBack.Config.datamodel.raster_layers.Length);
				for (int i = 0; i < rasterLayerCount; i++)
				{
					RasterLayer originalLayer = a_original.Config.datamodel.raster_layers[i];
					RasterLayer readBackLayer = a_readBack.Config.datamodel.raster_layers[i];
					success &= CheckEqual($"raster layer [{i}] short", originalLayer.@short, readBackLayer.@short);

					string key = RasterImageKey.Resolve(originalLayer.data);
					int originalLength = a_original.RasterImages.TryGetValue(key, out byte[] originalBytes) ? originalBytes.Length : -1;
					int readBackLength = a_readBack.RasterImages.TryGetValue(key, out byte[] readBackBytes) ? readBackBytes.Length : -1;
					success &= CheckEqual($"raster layer [{i}] image byte length", originalLength, readBackLength);
				}

				int vectorLayerCount = Math.Min(a_original.Config.datamodel.vector_layers.Length, a_readBack.Config.datamodel.vector_layers.Length);
				for (int i = 0; i < vectorLayerCount; i++)
				{
					VectorObject[] originalData = a_original.Config.datamodel.vector_layers[i].data;
					VectorObject[] readBackData = a_readBack.Config.datamodel.vector_layers[i].data;
					success &= CheckEqual($"vector layer [{i}] object count", originalData.Length, readBackData.Length);
				}

				Debug.Log(success
					? "[GisData Proof] SUCCESS - dataset round-tripped through SQLite without loss."
					: "[GisData Proof] FAILED - see warnings above for mismatches.");
			}
			finally
			{
				a_database.Dispose();
			}
		}

		private static bool CheckEqual<T>(string a_label, T a_expected, T a_actual)
		{
			bool equal = Equals(a_expected, a_actual);
			if (!equal)
				Debug.LogWarning($"[GisData Proof] Mismatch on {a_label}: expected \"{a_expected}\", got \"{a_actual}\".");
			return equal;
		}
	}
}
#endif
