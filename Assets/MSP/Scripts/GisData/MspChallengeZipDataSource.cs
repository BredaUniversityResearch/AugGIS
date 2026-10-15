using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using MSP.Scripts.Session;
using POV_Unity;
using UnityEngine;
using UnityEngine.Networking;

namespace MSP.Scripts.GisData
{
	/// <summary>
	/// IGisDataSource adapter around the existing MSP Challenge zip pipeline. Mirrors
	/// LoadZipData's three load modes for full behavioural parity (web, arbitrary file
	/// path, bundled application folder), but does not reuse
	/// ZipUtil.ParseRawZipConfigFile: that method decodes raster entries straight to
	/// Texture2D and registers them with AssetManager as a side effect, whereas
	/// IGisDataSource needs raw, undecoded bytes. Extraction is therefore duplicated
	/// here rather than modifying the shared POV_Unity/ZipUtil code; once the legacy
	/// path using LoadZipData is fully retired this duplicate can be removed.
	///
	/// ConfigLoadHelper.ParseConfig is reused as-is - it is pure JSON deserialization
	/// with no side effects.
	/// </summary>
	public class MspChallengeZipDataSource : IGisDataSource
	{
		private enum ELoadMode
		{
			Web,
			FilePath,
			ApplicationFolder
		}

		private readonly ELoadMode m_mode;
		private readonly string m_baseURL;
		private readonly RegionCoords m_regionCoords;
		private readonly string m_filePath;
		private readonly string m_fileName;

		private MspChallengeZipDataSource(ELoadMode a_mode, string a_baseURL, RegionCoords a_regionCoords, string a_filePath, string a_fileName)
		{
			m_mode = a_mode;
			m_baseURL = a_baseURL;
			m_regionCoords = a_regionCoords;
			m_filePath = a_filePath;
			m_fileName = a_fileName;
		}

		//Mirrors LoadZipData.LoadZipFromWeb: POSTs the region bounds to MSPC's CreatePOVConfig endpoint.
		public static MspChallengeZipDataSource FromWeb(string a_baseURL, RegionCoords a_regionCoords)
		{
			return new MspChallengeZipDataSource(ELoadMode.Web, a_baseURL, a_regionCoords, null, null);
		}

		//Mirrors LoadZipData.LoadZipFromFilePath: an arbitrary local or http(s) zip path.
		public static MspChallengeZipDataSource FromFilePath(string a_filePath)
		{
			return new MspChallengeZipDataSource(ELoadMode.FilePath, null, null, a_filePath, null);
		}

		//Mirrors LoadZipData.LoadZipFromApplicationFolder: the bundled zip shipped with
		//the Editor/dedicated server build, or the Android app-specific files folder.
		public static MspChallengeZipDataSource FromApplicationFolder(string a_fileName)
		{
			return new MspChallengeZipDataSource(ELoadMode.ApplicationFolder, null, null, null, a_fileName);
		}

		public IEnumerator LoadDataset(Action<GisDataset> a_onComplete, Action a_onFailure)
		{
			UnityWebRequest request = BuildRequest();

			yield return request.SendWebRequest();

			if (request.result != UnityWebRequest.Result.Success)
			{
				Debug.LogError($"[GisData] Failed to load MSP Challenge zip: {request.error}");
				a_onFailure?.Invoke();
				yield break;
			}

			if (!TryExtractZipContents(request.downloadHandler.data, out string configJson, out Dictionary<string, byte[]> rasterImages))
			{
				Debug.LogError("[GisData] MSP Challenge zip did not contain a config json entry.");
				a_onFailure?.Invoke();
				yield break;
			}

			ConfigLoadHelper helper = new ConfigLoadHelper();
			DataConfig config = helper.ParseConfig(configJson);
			helper.Clear();

			a_onComplete?.Invoke(new GisDataset
			{
				Config = config,
				RasterImages = rasterImages
			});
		}

		private UnityWebRequest BuildRequest()
		{
			switch (m_mode)
			{
				case ELoadMode.Web:
					return BuildWebRequest();
				case ELoadMode.FilePath:
					return UnityWebRequest.Get(m_filePath);
				case ELoadMode.ApplicationFolder:
					return UnityWebRequest.Get(ResolveApplicationFolderPath());
				default:
					throw new InvalidOperationException($"Unhandled load mode: {m_mode}");
			}
		}

		private UnityWebRequest BuildWebRequest()
		{
			WWWForm form = new WWWForm();
			form.AddField("region_bottom_left_x", m_regionCoords.BottomLeftX.ToString("G9", CultureInfo.InvariantCulture));
			form.AddField("region_bottom_left_y", m_regionCoords.BottomLeftY.ToString("G9", CultureInfo.InvariantCulture));
			form.AddField("region_top_right_x", m_regionCoords.TopRightX.ToString("G9", CultureInfo.InvariantCulture));
			form.AddField("region_top_right_y", m_regionCoords.TopRightY.ToString("G9", CultureInfo.InvariantCulture));
			form.AddField("output_image_format", "PNG8");
			string url = $"{m_baseURL}api/Game/CreatePOVConfig";
			return UnityWebRequest.Post(url, form);
		}

		private string ResolveApplicationFolderPath()
		{
			//Same platform coverage as LoadZipData.LoadZipFromApplicationFolder (Editor/
			//dedicated server read the bundled zip directly, Android reads it from the
			//app's persistent files folder). Other platforms are not supported by the
			//legacy path either.
#if UNITY_EDITOR || DEDICATED_SERVER
			return Path.Combine(Application.dataPath, "Plugins", "POV_Unity", "Resources", (m_fileName + ".zip"));
#elif UNITY_ANDROID
			return $"File:///storage/emulated/0/Android/data/com.Cradle.POV_Unity/files/{m_fileName}.zip";
#else
			throw new PlatformNotSupportedException("MspChallengeZipDataSource.FromApplicationFolder is only supported in the Editor, dedicated server or Android builds.");
#endif
		}

		//Duplicated, decode-free version of ZipUtil.ParseRawZipConfigFile: extracts the
		//config json text and the raw (still PNG-encoded) bytes of every other entry,
		//without decoding to Texture2D or touching AssetManager.
		private static bool TryExtractZipContents(byte[] a_zipData, out string a_configJson, out Dictionary<string, byte[]> a_rasterImages)
		{
			string configJson = null;
			Dictionary<string, byte[]> rasterImages = new Dictionary<string, byte[]>();

			using (ZipArchive archive = new ZipArchive(new MemoryStream(a_zipData)))
			{
				foreach (ZipArchiveEntry entry in archive.Entries)
				{
					if (string.IsNullOrEmpty(entry.Name))
						continue; //Directories have empty names

					string[] extSplit = entry.Name.Split('.');
					using (Stream unzippedEntryStream = entry.Open())
					using (MemoryStream tempMemoryStream = new MemoryStream())
					{
						unzippedEntryStream.CopyTo(tempMemoryStream);
						byte[] rawData = tempMemoryStream.ToArray();

						if (extSplit[1] == "json")
							configJson = Encoding.Default.GetString(rawData);
						else
							rasterImages[extSplit[0]] = rawData;
					}
				}
			}

			a_configJson = configJson;
			a_rasterImages = rasterImages;
			return configJson != null;
		}
	}
}
