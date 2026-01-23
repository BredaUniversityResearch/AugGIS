using System;
using System.Collections.Generic;
using UnityEngine;

namespace POV_Unity
{
	public class DMBathymetry : ARasterDisplayMethod
	{
		const int MESH_SIZE = 120;
		const float SUBDIVISIONS = 1f;

		private List<MeshRenderer> m_meshes;
		private RasterLayer m_layer;

		public List<MeshRenderer> Meshes => m_meshes;
		public RasterLayer Layer => m_layer;

		protected override IDisplayMethodRenderData DisplayRasterLayer(RasterLayer a_layer, GameObject a_displayMethodRoot)
		{
			m_layer = a_layer;
			m_meshes = new List<MeshRenderer>();

			int i = 0, j = 0;
			int nextI, nextJ;
			int pixelsperMesh = (int)(MESH_SIZE / SUBDIVISIONS);
			while (i < m_layer.Raster.width)
			{
				nextI = Math.Min(i + pixelsperMesh, m_layer.Raster.width);
				while (j < m_layer.Raster.height)
				{
					nextJ = Math.Min(j + pixelsperMesh, m_layer.Raster.height);
					m_meshes.Add(GenerateMeshForPixels(a_displayMethodRoot.transform, i, nextI, j, nextJ));
					j = nextJ;
				}
				i = nextI;
				j = 0;
			}

			ImportedConfigRoot.Instance.m_bathymetry = this;
			return null;
		}

		MeshRenderer GenerateMeshForPixels(Transform a_parent, int a_xMin, int a_xMax, int a_zMin, int a_zMax)
		{
			int meshSizeX = (int)((a_xMax - a_xMin) * SUBDIVISIONS);
			int meshSizeZ = (int)((a_zMax - a_zMin) * SUBDIVISIONS);
			List<Vector3> verts = new List<Vector3>((meshSizeX + 1)  * (meshSizeZ + 1));
			List<int> tris = new List<int>(meshSizeX * meshSizeZ * 6);
			Vector2[] uvs = new Vector2[(meshSizeX + 1) * (meshSizeZ + 1)];

			float stepX = (m_layer.coordinate1[0] - m_layer.coordinate0[0]) / m_layer.Raster.width / SUBDIVISIONS;
			float stepZ = (m_layer.coordinate1[1] - m_layer.coordinate0[1]) / m_layer.Raster.height / SUBDIVISIONS;
			int rowOffset = meshSizeZ + 1;
			int startX = (int)(a_xMin * SUBDIVISIONS);
			int startZ = (int)(a_zMin * SUBDIVISIONS);
			float uvStepX = 1 / ((float)m_layer.Raster.width * SUBDIVISIONS);
			float uvStepZ = 1 / ((float)m_layer.Raster.height * SUBDIVISIONS);

			int index = 0;
			for (int i = startX; i <= a_xMax * SUBDIVISIONS; i++)
			{
				for (int j = startZ; j <= a_zMax * SUBDIVISIONS; j++)
				{
					float xpos = i / (float)SUBDIVISIONS - 0.5f;
					float zpos = j / (float)SUBDIVISIONS - 0.5f;

					verts.Add(ImportedConfigRoot.Instance.ConfigToWorldSpace(new Vector3(m_layer.coordinate0[0] + i * stepX,
						GetHeightAtPixelPosition(xpos,zpos), 
						m_layer.coordinate0[1] + j * stepZ)));
					uvs[index] = new Vector2(i * uvStepX, j * uvStepZ);

					if (i != startX && j != startZ)
					{
						//Tri 1
						tris.Add(index);                //Top right
						tris.Add(index - 1);            //Bottom right
						tris.Add(index - rowOffset - 1);//Bottom left 
						//Tri 2
						tris.Add(index - rowOffset - 1);//Bottom left 
						tris.Add(index - rowOffset);    //Top left
						tris.Add(index);                //Top right 
					}
					++index;
				}
			}

			//Create mesh and set properties
			Mesh procMesh = new Mesh();
			procMesh.vertices = verts.ToArray(); 
			procMesh.uv = uvs;
			procMesh.triangles = tris.ToArray();
			procMesh.RecalculateNormals();

			//Create gameobject and add mesh renderer
			GameObject meshObject = new GameObject("Bathymetry Mesh");
			meshObject.transform.SetParent(a_parent, false);
			//meshObject.transform.localPosition = Vector3.zero;
			MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
			meshFilter.mesh = procMesh;

			MeshRenderer renderer = meshObject.AddComponent<MeshRenderer>(); 
			renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
			return renderer;
		}

		public float GetHeightAtPixelPosition(float a_x, float a_z)
		{
			int minX = (int)a_x;
			int maxX = Mathf.CeilToInt(a_x);
			float maxXFraction = a_x - minX;
			float minXFraction = 1f - maxXFraction;
			int minZ = (int)a_z;
			int maxZ = Mathf.CeilToInt(a_z);
			float maxZFraction = a_z - minZ;
			float minZFraction = 1f - maxZFraction;

			if(minX < 0)
			{
				if (minZ < 0)
				{
					return -m_layer.scale.EvaluateOutput(m_layer.Raster.GetPixel(maxX, maxZ).r);
				}
				else if (maxZ == m_layer.Raster.height)
				{
					return -m_layer.scale.EvaluateOutput(m_layer.Raster.GetPixel(maxX, minZ).r);
				}
				else
				{
					return -m_layer.scale.EvaluateOutput(m_layer.Raster.GetPixel(maxX, minZ).r) * minZFraction +
						-m_layer.scale.EvaluateOutput(m_layer.Raster.GetPixel(maxX, maxZ).r) * maxZFraction;
				}
			}
			else if(maxX == m_layer.Raster.width)
			{
				if (minZ < 0)
				{
					return -m_layer.scale.EvaluateOutput(m_layer.Raster.GetPixel(minX, maxZ).r);
				}
				else if (maxZ == m_layer.Raster.height)
				{
					return -m_layer.scale.EvaluateOutput(m_layer.Raster.GetPixel(minX, minZ).r);
				}
				else
				{
					return -m_layer.scale.EvaluateOutput(m_layer.Raster.GetPixel(minX, minZ).r) * minZFraction +
						-m_layer.scale.EvaluateOutput(m_layer.Raster.GetPixel(minX, maxZ).r) * maxZFraction;
				}
			}
			else
			{
				if (minZ < 0)
				{
					return -m_layer.scale.EvaluateOutput(m_layer.Raster.GetPixel(minX, maxZ).r) * minXFraction +
						-m_layer.scale.EvaluateOutput(m_layer.Raster.GetPixel(maxX, maxZ).r) * maxXFraction;
				}
				else if (maxZ == m_layer.Raster.height)
				{
					return -m_layer.scale.EvaluateOutput(m_layer.Raster.GetPixel(minX, minZ).r) * minXFraction +
						-m_layer.scale.EvaluateOutput(m_layer.Raster.GetPixel(maxX, minZ).r) * maxXFraction;
				}
				else
				{
					return -m_layer.scale.EvaluateOutput(m_layer.Raster.GetPixel(minX, minZ).r) * minXFraction * minZFraction +
						-m_layer.scale.EvaluateOutput(m_layer.Raster.GetPixel(minX, maxZ).r) * minXFraction * maxZFraction +
						-m_layer.scale.EvaluateOutput(m_layer.Raster.GetPixel(maxX, minZ).r) * maxXFraction * minZFraction +
						-m_layer.scale.EvaluateOutput(m_layer.Raster.GetPixel(maxX, maxZ).r) * maxXFraction * maxZFraction;
				}
			}
		}

		public float GetHeightAtWorldPosition(Vector3 a_position)
		{
			//World -> config space
			Vector3 configPos = ImportedConfigRoot.Instance.WorldToConfigSpace(a_position);

			//Config -> pixel space
			float pixelX = (configPos.x - m_layer.coordinate0[0]) / (m_layer.coordinate1[0] - m_layer.coordinate0[0]) * m_layer.Raster.width;
			float pixelY = (configPos.z - m_layer.coordinate0[1]) / (m_layer.coordinate1[1] - m_layer.coordinate0[1]) * m_layer.Raster.height;
			pixelX = Mathf.Clamp(pixelX, 0f, m_layer.Raster.width);
			pixelY = Mathf.Clamp(pixelY, 0f, m_layer.Raster.height);
			return GetHeightAtPixelPosition(pixelX, pixelY);
		}
	}
}
