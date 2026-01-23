using System.Globalization;
using Newtonsoft.Json;
using System.IO;
using MSP.Scripts.Session;
using UnityEngine.Networking;
using UnityEngine;

namespace POV_Unity
{
	public class LoadZipData : MonoBehaviour
	{
		[SerializeField] TextAsset m_displayMethodConfigFile;
		[SerializeField] string m_fileName;

		byte[] m_loadedRawData = null;
		public byte[] LoadedRawData => m_loadedRawData;

		public void LoadZipFromApplicationFolder()
		{
#if UNITY_EDITOR || DEDICATED_SERVER
			string filePath = Path.Combine(Application.dataPath, "Plugins", "POV_Unity", "Resources", (m_fileName + ".zip"));
#elif UNITY_ANDROID
			//string filePath = Path.Combine("File://",Application.persistentDataPath, (m_fileName + ".zip"));
			string filePath = $"File:///storage/emulated/0/Android/data/com.Cradle.POV_Unity/files/{m_fileName}.zip";
#endif
			LoadZipFromFilePath(filePath);
		}

		public void LoadZipFromFilePath(string a_filePath)
		{
			ImportedConfigRoot.Instance.NotifyLoadStarted();
			StartCoroutine(ZipUtil.LoadZip(UnityWebRequest.Get(a_filePath), OnZipLoaded, OnZipFailedToLoad));
		}

		public void LoadZipFromWeb(string a_baseURL, RegionCoords a_regionCoords)
		{
			WWWForm form = new WWWForm();
			form.AddField("region_bottom_left_x", a_regionCoords.BottomLeftX.ToString("G9", CultureInfo.InvariantCulture));
			form.AddField("region_bottom_left_y", a_regionCoords.BottomLeftY.ToString("G9", CultureInfo.InvariantCulture));
			form.AddField("region_top_right_x", a_regionCoords.TopRightX.ToString("G9", CultureInfo.InvariantCulture));
			form.AddField("region_top_right_y", a_regionCoords.TopRightY.ToString("G9", CultureInfo.InvariantCulture));
			form.AddField("output_image_format", "PNG8");
			string url = $"{a_baseURL}api/Game/CreatePOVConfig";
			Debug.Log("========================================");
			Debug.Log($"Loading zip from URL {url} with region: {a_regionCoords}");
			Debug.Log("========================================");
			ImportedConfigRoot.Instance.NotifyLoadStarted();
			StartCoroutine(ZipUtil.LoadZip(UnityWebRequest.Post(url, form), OnZipLoaded, OnZipFailedToLoad));
		}

		public void OnZipLoaded(string a_config, byte[] a_loadedData)
		{
			m_loadedRawData = a_loadedData;
			ImportConfig(a_config, m_displayMethodConfigFile);
			Destroy(gameObject);
		}

		private void OnZipFailedToLoad()
		{
			SessionManager.Instance.DisconnectFromSession(SessionManager.EDisconnectReason.FailedConnection);
		}

		public static void ImportConfig(string a_dataConfig, TextAsset a_displayMethodConfig)
		{
			ConfigLoadHelper helper = new ConfigLoadHelper();
			DataConfig m_dataConfig = helper.ParseConfig(a_dataConfig);
			DisplayMethodConfig m_displayMethodConfig = helper.ParseDisplayMethodConfig(a_displayMethodConfig.text);
			helper.Clear();
			ImportedConfigRoot.Instance.Initialise(m_dataConfig, m_displayMethodConfig);
		}
	}
}

