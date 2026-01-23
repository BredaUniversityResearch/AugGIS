using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

namespace POV_Unity
{
	[CreateAssetMenu(fileName ="AssetDatabase", menuName ="POV asset database", order =0)]
	public class POV_AssetDatabase : SerializedScriptableObject
	{
		[SerializeField] Dictionary<string, Material> m_materials;
		[SerializeField] Material m_defaultMaterial;
        [SerializeField] Material m_polygonMaterial;
        [SerializeField] Material m_polygonOpaqueMaterial;
		[SerializeField] Material m_typeMapMaterial;
		[SerializeField] Material m_heatMapMaterialTexture;
		[SerializeField] Material m_heatMapMaterialTextureInverted;
		[SerializeField] Material m_heatMapMaterialUV;
		[SerializeField] Material m_surfaceMaterialUV;
		[SerializeField] Material m_lineMaterial;
		[SerializeField] Material m_pawnMaterial;
		[SerializeField] Material m_rasterQuadMaterial;
		[SerializeField] Dictionary<string, GameObject> m_models;
		[SerializeField] GameObject m_defaultModel;
		[SerializeField] List<Texture2D> m_textures;
		[SerializeField] Texture2D m_defaultTexture;
		[SerializeField] List<Sprite> m_sprites;
		[SerializeField] Sprite m_defaultSprite;
		[SerializeField] float m_defaultSpriteMultiplier;


		public List<Texture2D> Textures => m_textures;
		public Texture2D DefaultTexture => m_defaultTexture;
		public List<Sprite> Sprites => m_sprites;
		public Sprite DefaultSprite => m_defaultSprite;
		public float DefaultSpriteMultiplier => m_defaultSpriteMultiplier;

		public Material PolygonMaterial => m_polygonMaterial;
		public Material PolygonOpaqueMaterial => m_polygonOpaqueMaterial;
		public Material TypeMapMaterial => m_typeMapMaterial;
		public Material HeatMapMaterialTexture => m_heatMapMaterialTexture;
		public Material HeatMapMaterialTextureInverted => m_heatMapMaterialTextureInverted;
		public Material HeatMapMaterialUV => m_heatMapMaterialUV;
		public Material SurfaceMaterialUV => m_surfaceMaterialUV;
		public Material LineMaterial => m_lineMaterial;
		public Material PawnMaterial => m_pawnMaterial;
		public Material RasterQuadMaterial => m_rasterQuadMaterial;


		public Material GetMaterial(string a_name)
		{
			if (string.IsNullOrEmpty(a_name))
				return m_defaultMaterial;
			if (m_materials.TryGetValue(a_name, out var result))
			{
				return result;
			}
			return m_defaultMaterial;
		}

		public GameObject GetModel(string a_name)
		{
			if(string.IsNullOrEmpty(a_name))
				return m_defaultModel;
			if (m_models.TryGetValue(a_name, out var result))
			{
				return result;
			}
			return m_defaultModel;
		}
	}
}
