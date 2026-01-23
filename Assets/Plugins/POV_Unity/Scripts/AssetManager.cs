using System.Collections.Generic;
using UnityEngine;

namespace POV_Unity
{
	public class AssetManager : MonoBehaviour
	{
		static AssetManager m_instance;
		public static AssetManager Instance
		{
			get
			{
				if (m_instance == null)
				{
					m_instance = new GameObject("AssetManager").AddComponent<AssetManager>();
					m_instance.m_assetDatabase = Resources.Load<POV_AssetDatabase>("AssetDatabase");
					m_instance.m_sprites = new Dictionary<string, Sprite>();
					foreach (Sprite s in m_instance.m_assetDatabase.Sprites)
					{
						m_instance.m_sprites[s.name] = s;
					}
					m_instance.m_textures = new Dictionary<string, Texture2D>();
					foreach (Texture2D t in m_instance.m_assetDatabase.Textures)
					{
						m_instance.m_textures[t.name] = t;
					}
					m_instance.m_textureMaterialGroups = new Dictionary<string, TextureMaterialGroup>();
					m_instance.m_textureOpaqueMaterialGroups = new Dictionary<string, TextureMaterialGroup>();
				}
				return m_instance;
			}
		}

		POV_AssetDatabase m_assetDatabase;

		Dictionary<string, Sprite> m_sprites;
		Dictionary<string, Texture2D> m_textures;
		Dictionary<string, Texture2D> m_loadedRasterTextures;
		Dictionary<string, TextureMaterialGroup> m_textureMaterialGroups;
		Dictionary<string, TextureMaterialGroup> m_textureOpaqueMaterialGroups;

		private void OnDestroy()
		{
			m_instance = null;
		}

		public static Material GetMaterial(string a_name)
		{
			return Instance.m_assetDatabase.GetMaterial(a_name);
		}

		public static GameObject GetModel(string a_name)
		{
			return Instance.m_assetDatabase.GetModel(a_name);
		}

		public static Texture2D GetTexture(string a_name)
		{
			if (a_name != null && Instance.m_textures.TryGetValue(a_name, out var result))
			{
				return result;
			}
			return Instance.m_assetDatabase.DefaultTexture;
		}

		public static Sprite GetSprite(string a_name)
		{
			Sprite result = null;
			
			if (a_name == null)
            {
                return Instance.m_assetDatabase.DefaultSprite;
            }

			if (Instance.m_sprites.TryGetValue(a_name.ToLower(), out result))
			{
				return result;
			}
			else if (Instance.m_sprites.TryGetValue(a_name.ToLower() + "Sprite", out result))
			{
				return result;
			}
			return Instance.m_assetDatabase.DefaultSprite;
		}

		public static Material GetMaterialForTextureColour(string a_name, Color a_colour)
		{
			if (Instance.m_textureMaterialGroups.TryGetValue(a_name, out var existingMaterial))
			{
				return existingMaterial.GetMaterial(a_colour, Instance.m_assetDatabase.PolygonMaterial);
			}
			if (Instance.m_textures.TryGetValue(a_name, out var texture))
			{
				TextureMaterialGroup group = new TextureMaterialGroup(texture);
				m_instance.m_textureMaterialGroups.Add(a_name, group);
				return group.GetMaterial(a_colour, Instance.m_assetDatabase.PolygonMaterial);
			}
			Debug.LogError("No material texture could be created for " + a_name);
			return null;
		}

		public static Material GetOpaqueMaterialForTextureColour(string a_name, Color a_colour)
		{
			if (Instance.m_textureOpaqueMaterialGroups.TryGetValue(a_name, out var existingMaterial))
			{
				return existingMaterial.GetMaterial(a_colour, Instance.m_assetDatabase.PolygonOpaqueMaterial);
			}
			if (Instance.m_textures.TryGetValue(a_name, out var texture))
			{
				TextureMaterialGroup group = new TextureMaterialGroup(texture);
				m_instance.m_textureOpaqueMaterialGroups.Add(a_name, group);
				return group.GetMaterial(a_colour, Instance.m_assetDatabase.PolygonOpaqueMaterial);
			}
			Debug.LogError("No opaque material texture could be created for " + a_name);
			return null;
		}


		public static Material GetTypeMapMaterial()
		{
			return Instance.m_assetDatabase.TypeMapMaterial;
		}

		public static Material GetHeatMapMaterialTexture(bool a_inverted)
		{
			return a_inverted ? Instance.m_assetDatabase.HeatMapMaterialTextureInverted : Instance.m_assetDatabase.HeatMapMaterialTexture;
		}

		public static Material GetHeatMapMaterialUV()
		{
			return Instance.m_assetDatabase.HeatMapMaterialUV;
		}

		public static Material GetPawnMaterial()
		{
			return Instance.m_assetDatabase.PawnMaterial;
		}

		public static Material GetSurfaceMaterialUV()
		{
			return Instance.m_assetDatabase.SurfaceMaterialUV;
		}

		public static void SetRasterTextures(Dictionary<string, Texture2D> a_textures)
		{
			Instance.m_loadedRasterTextures = a_textures;
		}

		public static Material GetRasterQuadMaterial()
		{
			return Instance.m_assetDatabase.RasterQuadMaterial;
		}

		public static Texture2D GetRasterTexture(string a_name)
		{
			if (Instance.m_loadedRasterTextures.TryGetValue(a_name, out var texture))
			{
				return texture;
			}
			return Instance.m_assetDatabase.DefaultTexture;
		}

		public static Material GetLineMaterial()
		{
			return Instance.m_assetDatabase.LineMaterial;
		}
	}

	public class TextureMaterialGroup
	{
		Texture2D m_pattern;
		Dictionary<Color, Material> m_materials = new Dictionary<Color, Material>();

		public TextureMaterialGroup(Texture2D pattern)
		{
			this.m_pattern = pattern;
		}

		public Material GetMaterial(Color a_color, Material a_materialPrefab)
		{
			if (!m_materials.ContainsKey(a_color))
			{
				Material material = Material.Instantiate<Material>(a_materialPrefab);
				material.SetTexture("_Texture", m_pattern);
				material.SetColor("_Color", a_color);
				material.SetVector("_TilingAndOffset", new Vector4(20f, 20f, 0, 0));
				m_materials.Add(a_color, material);
				return material;
			}

			return m_materials[a_color];
		}
	}
}
