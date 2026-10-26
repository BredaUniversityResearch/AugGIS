using System.Collections.Generic;
using POV_Unity;
using UnityEngine;

namespace MSP.Scripts.GisData
{
	/// <summary>
	/// Decodes a GisDataset's raw (still PNG-encoded) raster image bytes into
	/// Texture2D instances and registers them with AssetManager. This replaces the
	/// decoding side-effect that used to happen inside
	/// ZipUtil.ParseRawZipConfigFile - every process (host and every client)
	/// continues to decode its own textures independently and locally, only the
	/// source of the raw bytes has changed (a GisDataset instead of a freshly
	/// unzipped one).
	/// </summary>
	public static class GisRasterTextureLoader
	{
		public static void Load(GisDataset a_dataset)
		{
			Dictionary<string, Texture2D> loadedImages = new Dictionary<string, Texture2D>();

			foreach (KeyValuePair<string, byte[]> rasterImage in a_dataset.RasterImages)
			{
				//Same settings ZipUtil.ParseRawZipConfigFile used: Alpha8, LoadImage
				//(which internally changes the format to ARGB32), point filtering.
				Texture2D tex = new Texture2D(2, 2, TextureFormat.Alpha8, false, true);
				ImageConversion.LoadImage(tex, rasterImage.Value);
				tex.filterMode = FilterMode.Point;

				loadedImages.Add(rasterImage.Key, tex);
			}

			AssetManager.SetRasterTextures(loadedImages);
		}
	}
}
