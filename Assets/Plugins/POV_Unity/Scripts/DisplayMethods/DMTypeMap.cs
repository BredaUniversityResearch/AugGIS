using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace POV_Unity
{
	public class DMTypeMap : ARasterDisplayMethod
	{
		public string type_texture;
		public string type_colour;
		public bool seabottom;

		protected override IDisplayMethodRenderData DisplayRasterLayer(RasterLayer a_layer, GameObject a_displayMethodRoot)
		{
#if !UNITY_SERVER
			Material mat = new Material(AssetManager.GetTypeMapMaterial());
			Texture2D typeColours = null;
			Texture2DArray textureArray = null;
			JsonSerializer serializer = new JsonSerializer();
			serializer.Converters.Add(new JsonConverterHexColor());

			if (!string.IsNullOrEmpty(type_colour))
			{
				typeColours = new Texture2D(a_layer.types.Length+1, 1, TextureFormat.RGBA32, false, false, true);
				typeColours.filterMode = FilterMode.Point;
				typeColours.SetPixel(0, 0, new Color(0f, 0f, 0f, 0f)); //Set fake '0' type to transparent
				for (int i = 0; i < a_layer.types.Length; i++)
				{
					//Get type colours
					Color typeColour = Color.white;
					if (a_layer.types[i].TryGetValue(type_colour, StringComparison.InvariantCultureIgnoreCase, out var hexColour))
						typeColour = hexColour.ToObject<Color>(serializer);
					typeColours.SetPixel(i+1, 0, typeColour);
				}
			}
			else
			{
				typeColours = Texture2D.whiteTexture;
			}

			//K: using actual textures temporarily disabled because fake 0 texture will not match size of provided textures. 

			//if (!string.IsNullOrEmpty(type_texture))
			//{
			//	for (int i = 0; i < a_layer.types.Length; i++)
			//	{
			//		//Get type textures
			//		string textureName = null;
			//		if (a_layer.types[i].TryGetValue(type_texture, StringComparison.InvariantCultureIgnoreCase, out var tempResult))
			//			textureName = tempResult.ToObject<string>();
			//		Texture2D texture = AssetManager.GetTexture(textureName);

			//		if (textureArray == null)
			//		{
			//			Debug.Log("Creating texture array with format: " + texture.format);
			//			textureArray = new Texture2DArray(texture.width, texture.height, a_layer.types.Length, texture.format, false, false, true);
			//		}
			//		//Debug.Log("Copying texture with format: " + texture.format);
			//		Graphics.CopyTexture(texture, 0, 0, textureArray, i, 0);
			//	}
			//}
			//else
			//{
				Texture2D white = Texture2D.whiteTexture;
				textureArray = new Texture2DArray(white.width, white.height, a_layer.types.Length+1, white.format, false, false, true);
				Graphics.CopyTexture(Texture2D.blackTexture, 0, 0, textureArray, 0, 0); // Fake 0 texture
				for (int i = 0; i < a_layer.types.Length; i++)
				{
					Graphics.CopyTexture(white, 0, 0, textureArray, i+1, 0);
				}
			//}

			typeColours.Apply();
			textureArray.Apply();

			mat.SetTexture("_MappingTexture", a_layer.Raster);
			mat.SetTexture("_TypeTextures", textureArray);
			mat.SetTexture("_TypeColours", typeColours);

			// If seabottom, apply to bathymetry
			if (seabottom)
			{
				foreach (MeshRenderer renderer in ImportedConfigRoot.Instance.m_bathymetry.Meshes)
				{
					renderer.material = mat;
				}
			}
			else
			{
				//Otherwise: apply to new plane
				MeshRenderer renderer = ImportedConfigRoot.Instance.CreateConfigSpaceQuad(a_layer.coordinate0, a_layer.coordinate1, a_displayMethodRoot.transform, "TypeMap quad");
				renderer.material = mat;
			}
#endif
			return null;
		}

		Material GetTypeMaterial(RasterLayer a_layer, int a_type)
		{
			return AssetManager.GetMaterial(a_layer.types[a_type].GetValue(type_texture).ToString());
		}
	}
}
