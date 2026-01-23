using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace POV_Unity
{
	public class ZipUtil
	{
		public static IEnumerator LoadZip(UnityWebRequest a_uwr, Action<string, byte[]> a_completeCallback, Action a_failureCallback = null)
		{
			yield return a_uwr.SendWebRequest();

			if (a_uwr.result != UnityWebRequest.Result.Success)
			{
				Debug.Log("WebRequest:" + a_uwr.error);
				a_failureCallback?.Invoke();
			}
			else
			{
				Debug.Log("Zip successfully received, starting to unpack.");
				yield return ParseRawZipConfigFile(a_uwr.downloadHandler.data, a_completeCallback);
			}
		}

		public static IEnumerator ParseRawZipConfigFile(byte[] a_data, Action<string, byte[]> a_completeCallback)
		{
			Dictionary<string, Texture2D> m_loadedImages = new Dictionary<string, Texture2D>();
			string config = null;

			using (ZipArchive archive = new ZipArchive(new MemoryStream(a_data)))
			{
				foreach (ZipArchiveEntry entry in archive.Entries)
				{
					if (!string.IsNullOrEmpty(entry.Name))
					{
						//Directories have empty names
						string[] extSplit = entry.Name.Split('.');
						using (Stream unzippedEntryStream = entry.Open())
						{
							using (var tempMemoryStream = new MemoryStream())
							{
								unzippedEntryStream.CopyTo(tempMemoryStream);
								byte[] rawData = tempMemoryStream.ToArray();

								if (extSplit[1] == "json")
								{
									config = Encoding.Default.GetString(rawData);

#if UNITY_EDITOR
									string path = Path.Combine(Application.dataPath, "ConfigData.json");
									// Write the content to the file, overwriting if it already exists
									File.WriteAllText(path, config);
#endif

									Debug.Log($"Loaded config {entry.Name}");
								}
								else
								{
									Texture2D tex = new Texture2D(2, 2, TextureFormat.Alpha8, false, true);
									ImageConversion.LoadImage(tex, rawData); //Changes format to ARGB32
									tex.filterMode = FilterMode.Point;

									//string[] sizeSplit = extSplit[0].Split('_');
									//Texture2D tex = new Texture2D(int.Parse(sizeSplit[sizeSplit.Length - 2]), int.Parse(sizeSplit[sizeSplit.Length - 1]), TextureFormat.Alpha8, false);
									//tex.filterMode = FilterMode.Point;
									//tex.LoadRawTextureData(rawData);

									m_loadedImages.Add(extSplit[0], tex);
									Debug.Log($"Loaded image {entry.Name}, texture format: {tex.format}");
								}
							}
						}
					}
				}
			}
			AssetManager.SetRasterTextures(m_loadedImages);
			a_completeCallback?.Invoke(config, a_data);
			yield return null;
		}

		//		public static void UnpackZip(byte[] a_zip, string a_destination)
		//		{
		//			//Documentation: http://icsharpcode.github.io/SharpZipLib/api/ICSharpCode.SharpZipLib.Zip.ZipFile.html

		//			using (MemoryStream stream = new MemoryStream(a_zip))
		//			using (ZipFile zFile = new ZipFile(stream))
		//			{
		//				UnityEngine.Debug.Log("Listing of : " + zFile.Name);
		//				UnityEngine.Debug.Log("Raw Size, Size, Date, Time, Name");
		//				foreach (ZipEntry e in zFile)
		//				{
		//					if (e.IsFile)
		//					{
		//						DateTime d = e.DateTime;
		//						UnityEngine.Debug.Log($"{e.Size}, {e.CompressedSize}, {d.ToString("dd-MM-yy")}, {d.ToString("HH:mm")}, {e.Name}");
		//					}
		//				}
		//			}
		//		}

		//		public static void CheckZip()
		//		{
		//			//Documentation: http://icsharpcode.github.io/SharpZipLib/api/ICSharpCode.SharpZipLib.Zip.ZipFile.html
		//			string file = "C:\\Projects\\ProceduralOceanView\\3797867-3136066-4216646-3680971.zip";


		//			using (FileStream stream = new FileStream(file, FileMode.Open))
		//			using (ZipFile zFile = new ZipFile(stream))
		//			{
		//				UnityEngine.Debug.Log("Listing of : " + zFile.Name);
		//				UnityEngine.Debug.Log("Raw Size, Size, Date, Time, Name");
		//				foreach (ZipEntry e in zFile)
		//				{
		//					if (e.IsFile)
		//					{
		//						DateTime d = e.DateTime;
		//						UnityEngine.Debug.Log($"{e.Size}, {e.CompressedSize}, {d.ToString("dd-MM-yy")}, {d.ToString("HH:mm")}, {e.Name}");
		//					}
		//				}
		//			}
		//		}

		//		public static void UnZipFileToPers()
		//		{
		//			//Documentation: http://icsharpcode.github.io/SharpZipLib/api/ICSharpCode.SharpZipLib.Zip.ZipFile.html
		//			string filePath = "C:\\Projects\\ProceduralOceanView\\3797867-3136066-4216646-3680971.zip";
		//			string outputPath = Path.Combine(Application.persistentDataPath, "Config");
		//			if(Directory.Exists(outputPath))
		//			{
		//				Directory.Delete(outputPath, true);
		//			}
		//			Directory.CreateDirectory(outputPath);

		//			using (FileStream stream = new FileStream(filePath, FileMode.Open))
		//			using (ZipInputStream s = new ZipInputStream(stream))
		//			{
		//				ZipEntry theEntry;
		//				while ((theEntry = s.GetNextEntry()) != null)
		//				{
		//					if (!theEntry.IsFile)
		//						continue;
		//					string[] nameSplit = theEntry.Name.Split('/');
		//					using (FileStream streamWriter = File.Create(Path.Combine(outputPath, nameSplit[nameSplit.Length-1])))
		//					{
		//						int size = 2048;
		//						byte[] fdata = new byte[size];
		//						while (true)
		//						{
		//							size = s.Read(fdata, 0, fdata.Length);
		//							if (size > 0)
		//							{
		//								streamWriter.Write(fdata, 0, size);
		//							}
		//							else
		//							{
		//								break;
		//							}
		//						}
		//					}

		//				}
		//			}
		//		}

		//		public static void UnZip(string a_filePath, byte[] a_data)
		//		{
		//			using (ZipInputStream s = new ZipInputStream(new MemoryStream(a_data)))
		//			{
		//				ZipEntry theEntry;
		//				while ((theEntry = s.GetNextEntry()) != null)
		//				{
		//#if UNITY_EDITOR
		//					Debug.LogFormat("Entry Name: {0}", theEntry.Name);
		//#endif

		//					string directoryName = Path.GetDirectoryName(theEntry.Name);
		//					string fileName = Path.GetFileName(theEntry.Name);

		//					// create directory
		//					if (directoryName.Length > 0)
		//					{
		//						var dirPath = Path.Combine(a_filePath, directoryName);

		//#if UNITY_EDITOR
		//						Debug.LogFormat("CreateDirectory: {0}", dirPath);
		//#endif

		//						Directory.CreateDirectory(dirPath);
		//					}

		//					if (fileName != string.Empty)
		//					{
		//						// retrieve directory name only from persistence data path.
		//						var entryFilePath = Path.Combine(a_filePath, theEntry.Name);
		//						using (FileStream streamWriter = File.Create(entryFilePath))
		//						{
		//							int size = 2048;
		//							byte[] fdata = new byte[size];
		//							while (true)
		//							{
		//								size = s.Read(fdata, 0, fdata.Length);
		//								if (size > 0)
		//								{
		//									streamWriter.Write(fdata, 0, size);
		//								}
		//								else
		//								{
		//									break;
		//								}
		//							}
		//						}
		//					}
		//				}
		//			}
		//		}
	}
}
