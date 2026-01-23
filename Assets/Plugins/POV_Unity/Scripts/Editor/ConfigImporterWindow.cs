#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using Sirenix.Utilities;
using System.IO;
using UnityEngine.Networking;
using Unity.EditorCoroutines.Editor;

namespace POV_Unity
{

	public class ConfigImporterWindow : OdinEditorWindow
	{
		[SerializeField] string m_fileName;
		[SerializeField] TextAsset m_displayMethodConfigFile;

		[MenuItem("Tools/ConfigImporter")]
		private static void OpenWindow()
		{
			var window = GetWindow<ConfigImporterWindow>();
			window.position = GUIHelper.GetEditorWindowRect().AlignCenter(400, 100);
		}

		[Button("Import")]
		public void Import()
		{
			string filePath = Path.Combine(Application.dataPath, "Plugins", "POV_Unity", "Resources", (m_fileName + ".zip"));
			EditorCoroutineUtility.StartCoroutine(ZipUtil.LoadZip(UnityWebRequest.Get(filePath), OnZipLoaded, null), this);
		}

		void OnZipLoaded(string a_config, byte[] a_data)
		{
			ConfigLoadHelper helper = new ConfigLoadHelper();
			DataConfig m_dataConfig = helper.ParseConfig(a_config);
			DisplayMethodConfig m_displayMethodConfig = helper.ParseDisplayMethodConfig(m_displayMethodConfigFile.text);
			helper.Clear();
			ImportedConfigRoot.Instance.Initialise(m_dataConfig, m_displayMethodConfig);
		}
	}
}
#endif