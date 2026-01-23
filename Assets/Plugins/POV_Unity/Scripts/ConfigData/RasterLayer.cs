using Sirenix.Utilities;
using System;
using System.Collections;
using UnityEngine;


namespace POV_Unity
{
	public class RasterLayer : ALayer
	{

		private const float LAYER_VALUE_CUTOFF = 0.01f;

		public float[] coordinate0;
		public float[] coordinate1;
		public string data;
		public TypeMapping[] mapping;
		public ScaleConfig scale;

		Texture2D m_raster;
		public Texture2D Raster => m_raster;

		protected override IEnumerator LoadData(Action a_completeCallback)
		{
			yield return null;

			//Raster data is the path in the zip, for example "Rastermaps/NS_Bathymetry_Raster_Cut.png"
			//We store rasters in the assetmanager by just their name
			string[] nameSplit = data.Split('/');
			nameSplit = nameSplit[nameSplit.Length - 1].Split('.');
			m_raster = AssetManager.GetRasterTexture(nameSplit[0]);

			bool typeMap = false;
			foreach(string tag in tags)
			{ 
				if(tag.Equals("TypeMap", StringComparison.InvariantCultureIgnoreCase))
				{
					typeMap = true;
					break;
				}
			}

			if (typeMap)
			{
				if (mapping != null)
				{
					//Remap texture
					mapping.Sort((a, b) => a.min.CompareTo(b.min));
                    TypeMapping[] newMapping = new TypeMapping[mapping.Length];
                    float interval = 1f / (types.Length+1); //A fake extra '0' type is added
                    float offset = interval * 0.5f;
                    for (int k = 0; k < mapping.Length; k++)
                    {
                        newMapping[k] = new TypeMapping() { type = k, max = (int)((k + 1) * interval * 256f) - 1, min = (int)(k * interval * 256f) };
                    }

					byte[] newData = null;
                    if (m_raster.format == TextureFormat.Alpha8)
					{
						newData = m_raster.GetRawTextureData();
						int index = 0;
						for (int j = 0; j < m_raster.height; j++)
						{
							for (int i = 0; i < m_raster.width; i++)
							{
								if(newData[index] == 0)
									newData[index] = (byte)(offset * 256f); //fake 0 type
								else
									newData[index] = (byte)(((FindTypeIndex(newData[index])+1) * interval + offset) * 256f);
								index++;
							}
						}
					}
					else if (m_raster.format == TextureFormat.ARGB32)
					{
						newData = new byte[m_raster.height * m_raster.width];
						byte[] data = m_raster.GetRawTextureData();
						int fromIndex = 1; //Start at 1 to get the R value
						int toIndex = 0;
						for (int j = 0; j < m_raster.height; j++)
						{
							for (int i = 0; i < m_raster.width; i++)
							{
								if (data[fromIndex] == 0)
									newData[toIndex] = (byte)(offset * 256f); //fake 0 type
								else
									newData[toIndex] = (byte)(((FindTypeIndex(data[fromIndex])+1) * interval + offset) * 256f);
								fromIndex += 4;
								toIndex += 1;
							}
						}
					}
					else
						Debug.LogError($"Typemap for layer \"{@short}\" does not have expected format. Expected: Alpha8 or ARGB32, actual: {m_raster.format}");
					
					Texture2D newText = new Texture2D(m_raster.width, m_raster.height, TextureFormat.Alpha8, false);
					newText.filterMode = FilterMode.Point;
					newText.LoadRawTextureData(newData);
					newText.Apply();
					m_raster = newText;
					mapping = newMapping;
				}
				else
				{
					Debug.LogError($"Typemap for layer \"{@short}\" does not have a mapping defined.");
				}
			}
			else if(scale == null)
			{
				//Value map raster without scale
				Debug.LogWarning($"Valuemap layer \"{@short}\" does not have a scale defined. Using the default Lin(0,1000) scale.");
				scale = new ScaleConfig() 
				{
					interpolation = ScaleConfig.DensitymapInterpolation.Lin, 
					min_value = 0f,
					max_value = 1000f
				};
			}

			a_completeCallback();
			yield break;
		}

		int FindTypeIndex(int a_value)
		{
			if(mapping.Length == 0)
			{
				Debug.LogWarning("Typemap mapping does not cover value: " + a_value);
				return 0;
			}

			int left = 0;
			int right = mapping.Length - 1;

			while (left <= right)
			{
				int middle = left + (right - left) / 2;

				if(mapping[middle].min <= a_value && mapping[middle].max >= a_value)
				{
					return mapping[middle].type;
				}
				if (mapping[middle].min > a_value)
				{
					right = middle - 1; // Search in the left half
				}
				else
				{
					left = middle + 1; // Search in the right half
				}
			}

			Debug.LogWarning("Typemap mapping does not cover value: " + a_value);
			return 0;
		}

		public float GetValueAtPixelPositionScaled(float a_x, float a_z)
		{
			return scale.EvaluateOutput(GetValueAtPixelPositionUnscaled(a_x, a_z));
		}

		public float GetValueAtPixelPositionUnscaled(float a_x, float a_z)
		{
			int minX = (int)a_x;
			int maxX = Mathf.CeilToInt(a_x);
			float maxXFraction = a_x - minX;
			float minXFraction = 1f - maxXFraction;
			int minZ = (int)a_z;
			int maxZ = Mathf.CeilToInt(a_z);
			float maxZFraction = a_z - minZ;
			float minZFraction = 1f - maxZFraction;

			//TODO: should be a for Alpha8 texture
			if (minX < 0)
			{
				if (minZ < 0)
				{
					return Raster.GetPixel(maxX, maxZ).r;
				}
				else if (maxZ == Raster.height)
				{
					return Raster.GetPixel(maxX, minZ).r;
				}
				else
				{
					return Raster.GetPixel(maxX, minZ).r * minZFraction +
						Raster.GetPixel(maxX, maxZ).r * maxZFraction;
				}
			}
			else if (maxX == Raster.width)
			{
				if (minZ < 0)
				{
					return Raster.GetPixel(minX, maxZ).r;
				}
				else if (maxZ == Raster.height)
				{
					return Raster.GetPixel(minX, minZ).r;
				}
				else
				{
					return Raster.GetPixel(minX, minZ).r * minZFraction +
						Raster.GetPixel(minX, maxZ).r * maxZFraction;
				}
			}
			else
			{
				if (minZ < 0)
				{
					return Raster.GetPixel(minX, maxZ).r * minXFraction +
						Raster.GetPixel(maxX, maxZ).r * maxXFraction;
				}
				else if (maxZ == Raster.height)
				{
					return Raster.GetPixel(minX, minZ).r * minXFraction +
						Raster.GetPixel(maxX, minZ).r * maxXFraction;
				}
				else
				{
					return Raster.GetPixel(minX, minZ).r * minXFraction * minZFraction +
						Raster.GetPixel(minX, maxZ).r * minXFraction * maxZFraction +
						Raster.GetPixel(maxX, minZ).r * maxXFraction * minZFraction +
						Raster.GetPixel(maxX, maxZ).r * maxXFraction * maxZFraction;
				}
			}
		}

		public override bool IsPointInsideLayer(Vector2 a_point, float a_maxDistance, out string outTypeData)
		{
			Vector3 worldMin = ImportedConfigRoot.Instance.ConfigToWorldSpaceXY(coordinate0);
			Vector3 worldMax = ImportedConfigRoot.Instance.ConfigToWorldSpaceXY(coordinate1);

			Vector2 textureScale = new Vector2();
			textureScale.x = worldMax.x - worldMin.x;
			textureScale.y = worldMax.y - worldMin.y;

			Vector2 texturePos = new Vector2(a_point.x - worldMin.x, a_point.y - worldMin.y);

			int pixelX = Mathf.FloorToInt(texturePos.x / textureScale.x * m_raster.width);
			int pixelY = Mathf.FloorToInt(texturePos.y / textureScale.y * m_raster.height);

			pixelX = Math.Clamp(pixelX, 0, m_raster.width);
			pixelY = Math.Clamp(pixelY, 0, m_raster.height);

			//Typemaps will have an Alpha8 texture, so read a different channel
			float pixelValue = scale == null ? m_raster.GetPixel(pixelX, pixelY).a : m_raster.GetPixel(pixelX, pixelY).r;

			if (pixelValue < LAYER_VALUE_CUTOFF)
			{
				outTypeData = string.Empty;
				return false;
			}

			//type mapping is in range (0-255)
			//Note: Here we can for rasters that have a scale config defines display the evaluated output with scale.evaulateOutput and if there is not scale config we can use the type index
			float scaledPixelValue = Mathf.Clamp(pixelValue * 255f, 0f, 255f);
			int typeIndex = FindTypeIndex(Mathf.CeilToInt(scaledPixelValue));
			
			if(scale == null)
			{
				if (typeIndex == 0)
				{
					//We've hit the fake '0' type
					outTypeData = "No Type Data";
					return false;
				}
				typeIndex--; //Offset to get the actual type
			}

			if(types.Length > 0)
			{
				types[typeIndex].TryGetValue("name", out var token);
				outTypeData = token.ToString();
			}
			else
			{
				outTypeData = "No Type Data";
			}
		
			return true;
		}
	}

	public class TypeMapping
	{
		public int min;
		public int max;
		public int type;
	}
}
