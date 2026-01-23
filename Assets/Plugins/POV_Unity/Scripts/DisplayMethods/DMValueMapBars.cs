using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace POV_Unity
{
	public class DMValueMapBars : ARasterDisplayMethod
	{
		const int MESH_SIZE = 100;

		public string colour_scale = "Gradient_Rainbow"; //Name of colour texture
		public float cutoff = -1f; //Don't generate mesh for values lower than cutoff
		public float height = 0f; //Max height (at value 1)

		protected override IDisplayMethodRenderData DisplayRasterLayer(RasterLayer a_layer, GameObject a_displayMethodRoot)
		{
			List<Mesh> meshes = new List<Mesh>();

			int i = 0, j = 0;
			int nextI, nextJ;
			int pixelsperMesh = MESH_SIZE / 2;
			while (i < a_layer.Raster.width)
			{
				nextI = Math.Min(i + pixelsperMesh, a_layer.Raster.width);
				while (j < a_layer.Raster.height)
				{
					nextJ = Math.Min(j + pixelsperMesh, a_layer.Raster.height);
					meshes.Add(GenerateCubeMeshForPixelsWithSidesAndEdges(a_layer, a_displayMethodRoot.transform, i, nextI, j, nextJ));
					j = nextJ;
				}
				i = nextI;
				j = 0;
			}

			Material mat = new Material(AssetManager.GetHeatMapMaterialUV());
			mat.SetTexture("_ColourScale", AssetManager.GetTexture(colour_scale));

			Mesh combinedMesh = new Mesh();
			combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			CombineInstance[] combineInstances = new CombineInstance[meshes.Count];

			GameObject heatMapGameObject = new GameObject("Heatmap Mesh");
			heatMapGameObject.transform.SetParent(a_displayMethodRoot.transform, false);

			for (int k = 0; k < meshes.Count; k++)
			{
				combineInstances[k].mesh = meshes[k];
				combineInstances[k].transform = heatMapGameObject.transform.localToWorldMatrix * a_displayMethodRoot.transform.transform.worldToLocalMatrix;
			}

			combinedMesh.CombineMeshes(combineInstances);
			combinedMesh.RecalculateBounds();
			combinedMesh.Optimize();

			MeshFilter meshFilter = heatMapGameObject.AddComponent<MeshFilter>();
			meshFilter.sharedMesh = combinedMesh;
			MeshRenderer renderer = heatMapGameObject.AddComponent<MeshRenderer>();
			renderer.material = mat;
			renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

			return null;
		}

		Mesh GenerateCubeMeshForPixels(RasterLayer a_layer, Transform a_parent, int a_xMin, int a_xMax, int a_zMin, int a_zMax)
		{
			int meshSizeX = a_xMax - a_xMin;
			int meshSizeZ = a_zMax - a_zMin;
			List<Vector3> verts = null;
			List<int> tris = null;

			if(a_xMin == 0)
			{
				if (a_zMin == 0)
				{
					//No connection
					verts = new List<Vector3>(meshSizeX * meshSizeZ * 4);
					tris = new List<int>((meshSizeX * meshSizeZ + meshSizeX * (meshSizeZ-1) + (meshSizeX-1) * meshSizeZ) * 6);
				}
				else
				{
					//Bottom connection
					verts = new List<Vector3>(meshSizeX * meshSizeZ * 4 + meshSizeX * 2);
					tris = new List<int>((2 * meshSizeX * meshSizeZ + (meshSizeX - 1) * meshSizeZ) * 6);
				}
			}
			else if(a_zMin == 0)
			{
				//Left connection
				verts = new List<Vector3>(meshSizeX * meshSizeZ * 4 + meshSizeZ * 2);
				tris = new List<int>((2 * meshSizeX * meshSizeZ + meshSizeX * (meshSizeZ - 1)) * 6);
			}
			else
			{
				//Both connection
				verts = new List<Vector3>(meshSizeX * meshSizeZ * 4 + (meshSizeX + meshSizeZ) * 2);
				tris = new List<int>(meshSizeX * meshSizeZ * 18); // 3*w*h*6
			}
			List <Vector2> uvs = new List<Vector2>(verts.Capacity);
			List <Vector2> heightUVs = new List<Vector2>(verts.Capacity);

			float stepX = (a_layer.coordinate1[0] - a_layer.coordinate0[0]) / a_layer.Raster.width;
			float stepZ = (a_layer.coordinate1[1] - a_layer.coordinate0[1]) / a_layer.Raster.height;
			float uvStepX = 1f / a_layer.Raster.width;
			float uvStepZ = 1f / a_layer.Raster.height;
			int rowOffset = meshSizeZ * 4;

			int index = 0;
			for (int i = a_xMin; i < a_xMax; i++)
			{
				for (int j = a_zMin; j < a_zMax; j++)
				{
					float normalHeight = a_layer.Raster.GetPixel(i, j).a;
					float scaledHeight = normalHeight * height;
					Vector3 p0 = ImportedConfigRoot.Instance.ConfigToWorldSpace(new Vector3(a_layer.coordinate0[0] + i * stepX, 0, a_layer.coordinate0[1] + j * stepZ));
					Vector3 p1 = ImportedConfigRoot.Instance.ConfigToWorldSpace(new Vector3(a_layer.coordinate0[0] + (i+1) * stepX, 0, a_layer.coordinate0[1] + (j+1) * stepZ));

					verts.Add(new Vector3(p0.x, scaledHeight, p0.z));
					verts.Add(new Vector3(p0.x, scaledHeight, p1.z));
					verts.Add(new Vector3(p1.x, scaledHeight, p1.z));
					verts.Add(new Vector3(p1.x, scaledHeight, p0.z));
					uvs.Add(new Vector2(i * uvStepX, j * uvStepZ));
					uvs.Add(new Vector2(i * uvStepX, (j + 1) * uvStepZ));
					uvs.Add(new Vector2((i + 1) * uvStepX, (j + 1) * uvStepZ));
					uvs.Add(new Vector2((i + 1) * uvStepX, j * uvStepZ));
					heightUVs.Add(new Vector2(normalHeight, 0f));
					heightUVs.Add(new Vector2(normalHeight, 0f));
					heightUVs.Add(new Vector2(normalHeight, 0f));
					heightUVs.Add(new Vector2(normalHeight, 0f));

					//Tri 1
					tris.Add(index);  
					tris.Add(index+1); 
					tris.Add(index+2);
					//Tri 2
					tris.Add(index+2);
					tris.Add(index+3); 
					tris.Add(index); 			
					
					if(i > a_xMin)
					{
						//Tri 1
						tris.Add(index + 1);
						tris.Add(index);
						tris.Add(index + 3 - rowOffset);
						//Tri 2
						tris.Add(index + 3 - rowOffset);
						tris.Add(index + 2 - rowOffset);
						tris.Add(index + 1);
					}
					if (j > a_zMin)
					{
						//Tri 1
						tris.Add(index - 3);
						tris.Add(index);
						tris.Add(index + 3);
						//Tri 2
						tris.Add(index + 3);
						tris.Add(index - 2);
						tris.Add(index - 3);
					}
					index+=4;
				}
			}
			if (a_xMin > 0)
			{
				//Create connection to prev mesh left
				for (int j = a_zMin; j < a_zMax; j++)
				{
					float normalHeight = a_layer.Raster.GetPixel((a_xMin - 1), j).a;
					float scaledHeight = normalHeight * height;
					Vector3 p0 = ImportedConfigRoot.Instance.ConfigToWorldSpace(new Vector3(a_layer.coordinate0[0] + a_xMin * stepX, 0, a_layer.coordinate0[1] + j * stepZ));
					Vector3 p1 = ImportedConfigRoot.Instance.ConfigToWorldSpace(new Vector3(a_layer.coordinate0[0] + a_xMin * stepX, 0, a_layer.coordinate0[1] + (j + 1) * stepZ));
					verts.Add(new Vector3(p0.x, scaledHeight, p0.z));
					verts.Add(new Vector3(p1.x, scaledHeight, p1.z));
					uvs.Add(new Vector2((a_xMin - 1) * uvStepX, j * uvStepZ));
					uvs.Add(new Vector2((a_xMin - 1) * uvStepX, (j + 1) * uvStepZ));
					heightUVs.Add(new Vector2(normalHeight, 0f));
					heightUVs.Add(new Vector2(normalHeight, 0f));

					int matchingIndex = (j - a_zMin) * 4;
					//Tri 1
					tris.Add(index);
					tris.Add(index + 1);
					tris.Add(matchingIndex + 1);
					//Tri 2
					tris.Add(matchingIndex + 1);
					tris.Add(matchingIndex);
					tris.Add(index);
					index+=2;
				}
			}
			if(a_zMin > 0)
			{
				//Create connection to prev mesh bottom
				for (int i = a_xMin; i < a_xMax; i++)
				{
					float normalHeight = a_layer.Raster.GetPixel(i, (a_zMin - 1)).a;
					float scaledHeight = normalHeight * height;
					Vector3 p0 = ImportedConfigRoot.Instance.ConfigToWorldSpace(new Vector3(a_layer.coordinate0[0] + i * stepX, 0, a_layer.coordinate0[1] + a_zMin * stepZ));
					Vector3 p1 = ImportedConfigRoot.Instance.ConfigToWorldSpace(new Vector3(a_layer.coordinate0[0] + (i+1) * stepX, 0, a_layer.coordinate0[1] + a_zMin * stepZ));
					verts.Add(new Vector3(p0.x, scaledHeight, p0.z));
					verts.Add(new Vector3(p1.x, scaledHeight, p1.z));
					uvs.Add(new Vector2(i * uvStepX, (a_zMin - 1) * uvStepZ));
					uvs.Add(new Vector2((i+1) * uvStepX, (a_zMin - 1) * uvStepZ));
					heightUVs.Add(new Vector2(normalHeight, 0f));
					heightUVs.Add(new Vector2(normalHeight, 0f));

					int matchingIndex = (i - a_xMin) * rowOffset;
					//Tri 1
					tris.Add(matchingIndex);
					tris.Add(matchingIndex + 3);
					tris.Add(index + 1);
					//Tri 2
					tris.Add(index + 1);
					tris.Add(index);
					tris.Add(matchingIndex);
					index += 2;
				}
			}

			//Create mesh and set properties
			Mesh procMesh = new Mesh();
			procMesh.vertices = verts.ToArray();
			procMesh.uv = uvs.ToArray();
			procMesh.uv2 = heightUVs.ToArray();
			procMesh.triangles = tris.ToArray();
			procMesh.RecalculateNormals();

			return procMesh;
		}

		Mesh GenerateCubeMeshForPixelsWithSides(RasterLayer a_layer, Transform a_parent, int a_xMin, int a_xMax, int a_zMin, int a_zMax)
		{
			int meshSizeX = a_xMax - a_xMin;
			int meshSizeZ = a_zMax - a_zMin;
			List<Vector3> verts = null;
			List<int> tris = null;

			if (a_xMin == 0)
			{
				if (a_zMin == 0)
				{
					//No connection
					verts = new List<Vector3>(meshSizeX * meshSizeZ * 8);
					tris = new List<int>((int)((float)verts.Capacity * 1.5f));
				}
				else
				{
					//Bottom connection
					verts = new List<Vector3>(meshSizeX * meshSizeZ * 8 + meshSizeX * 4);
					tris = new List<int>((int)((float)verts.Capacity * 1.5f));
				}
			}
			else if (a_zMin == 0)
			{
				//Left connection
				verts = new List<Vector3>(meshSizeX * meshSizeZ * 8 + meshSizeZ * 4);
				tris = new List<int>((int)((float)verts.Capacity * 1.5f));
			}
			else
			{
				//Both connection
				verts = new List<Vector3>(meshSizeX * meshSizeZ * 8 + (meshSizeX + meshSizeZ) * 4);
				tris = new List<int>((int)((float)verts.Capacity * 1.5f));
			}
			List<Vector2> uvs = new List<Vector2>(verts.Capacity);
			List<Vector2> heightUVs = new List<Vector2>(verts.Capacity);

			float stepX = (a_layer.coordinate1[0] - a_layer.coordinate0[0]) / a_layer.Raster.width;
			float stepZ = (a_layer.coordinate1[1] - a_layer.coordinate0[1]) / a_layer.Raster.height;
			float uvStepX = 1f / a_layer.Raster.width;
			float uvStepZ = 1f / a_layer.Raster.height;
			int rowOffset = meshSizeZ * 4;

			int index = 0;
			for (int i = a_xMin; i < a_xMax; i++)
			{
				for (int j = a_zMin; j < a_zMax; j++)
				{
					float normalHeight = a_layer.Raster.GetPixel(i, j).r; //TODO: should be a for Alpha8 texture
					float scaledHeight = normalHeight * height;
					Vector3 p0 = ImportedConfigRoot.Instance.ConfigToWorldSpace(new Vector3(a_layer.coordinate0[0] + i * stepX, 0, a_layer.coordinate0[1] + j * stepZ));
					Vector3 p1 = ImportedConfigRoot.Instance.ConfigToWorldSpace(new Vector3(a_layer.coordinate0[0] + (i + 1) * stepX, 0, a_layer.coordinate0[1] + (j + 1) * stepZ));

					verts.Add(new Vector3(p0.x, scaledHeight, p0.z));
					verts.Add(new Vector3(p0.x, scaledHeight, p1.z));
					verts.Add(new Vector3(p1.x, scaledHeight, p1.z));
					verts.Add(new Vector3(p1.x, scaledHeight, p0.z));
					uvs.Add(new Vector2(i * uvStepX, j * uvStepZ));
					uvs.Add(new Vector2(i * uvStepX, (j + 1) * uvStepZ));
					uvs.Add(new Vector2((i + 1) * uvStepX, (j + 1) * uvStepZ));
					uvs.Add(new Vector2((i + 1) * uvStepX, j * uvStepZ));
					heightUVs.Add(new Vector2(normalHeight, 1f));
					heightUVs.Add(new Vector2(normalHeight, 1f));
					heightUVs.Add(new Vector2(normalHeight, 1f));
					heightUVs.Add(new Vector2(normalHeight, 1f));

					//Tri 1
					tris.Add(index);
					tris.Add(index + 1);
					tris.Add(index + 2);
					//Tri 2
					tris.Add(index + 2);
					tris.Add(index + 3);
					tris.Add(index);
					index += 4;

					if (i > 0) 
					{
						//Left connecting plane
						float normalHeightOther = a_layer.Raster.GetPixel((i - 1), j).r; //TODO: should be a for Alpha8 texture
						float scaledHeightOther = normalHeightOther * height;

						verts.Add(new Vector3(p0.x, scaledHeight, p0.z));
						verts.Add(new Vector3(p0.x, scaledHeightOther, p0.z));
						verts.Add(new Vector3(p0.x, scaledHeightOther, p1.z));
						verts.Add(new Vector3(p0.x, scaledHeight, p1.z));
						uvs.Add(new Vector2(i * uvStepX, j * uvStepZ));
						uvs.Add(new Vector2(i * uvStepX, j * uvStepZ));
						uvs.Add(new Vector2(i * uvStepX, (j + 1) * uvStepZ));
						uvs.Add(new Vector2(i * uvStepX, (j + 1) * uvStepZ));
						heightUVs.Add(new Vector2(normalHeight, 0f));
						heightUVs.Add(new Vector2(normalHeightOther, 0f));
						heightUVs.Add(new Vector2(normalHeightOther, 0f));
						heightUVs.Add(new Vector2(normalHeight, 0f));

						//Tri 1
						tris.Add(index);
						tris.Add(index + 1);
						tris.Add(index + 2);
						//Tri 2
						tris.Add(index + 2);
						tris.Add(index + 3);
						tris.Add(index);
						index += 4;
					}
					if (j > 0)
					{
						//Bottom connecting plane
						float normalHeightOther = a_layer.Raster.GetPixel(i, (j-1)).r; //TODO: should be a for Alpha8 texture
						float scaledHeightOther = normalHeightOther * height;

						verts.Add(new Vector3(p0.x, scaledHeight, p0.z));
						verts.Add(new Vector3(p1.x, scaledHeight, p0.z));
						verts.Add(new Vector3(p1.x, scaledHeightOther, p0.z));
						verts.Add(new Vector3(p0.x, scaledHeightOther, p0.z));
						uvs.Add(new Vector2(i * uvStepX, j * uvStepZ));
						uvs.Add(new Vector2((i + 1) * uvStepX, j * uvStepZ));
						uvs.Add(new Vector2(i * uvStepX, j * uvStepZ));
						uvs.Add(new Vector2((i + 1) * uvStepX, j * uvStepZ));
						heightUVs.Add(new Vector2(normalHeight, 0f));
						heightUVs.Add(new Vector2(normalHeight, 0f));
						heightUVs.Add(new Vector2(normalHeightOther, 0f));
						heightUVs.Add(new Vector2(normalHeightOther, 0f));

						//Tri 1
						tris.Add(index);
						tris.Add(index + 1);
						tris.Add(index + 2);
						//Tri 2
						tris.Add(index + 2);
						tris.Add(index + 3);
						tris.Add(index);
						index += 4;
					}
				}
			}

			//Create mesh and set properties
			Mesh procMesh = new Mesh();
			procMesh.vertices = verts.ToArray();
			procMesh.uv = uvs.ToArray();
			procMesh.uv2 = heightUVs.ToArray();
			procMesh.triangles = tris.ToArray();
			procMesh.RecalculateNormals();

			//Debug.Log(verts.Count);
			//Debug.Log(string.Join(" ", tris));

			return procMesh;
		}

		Mesh GenerateCubeMeshForPixelsWithSidesAndEdges(RasterLayer a_layer, Transform a_parent, int a_xMin, int a_xMax, int a_zMin, int a_zMax)
		{
			int meshSizeX = a_xMax - a_xMin;
			int meshSizeZ = a_zMax - a_zMin;
			List<Vector3> verts = null;

			if (a_xMax == a_layer.Raster.width)
			{
				if (a_zMax == a_layer.Raster.height)
				{
					//Both connection
					verts = new List<Vector3>((meshSizeX * meshSizeZ + meshSizeX + meshSizeZ) * 8);
				}
				else
				{
					//Right
					verts = new List<Vector3>((meshSizeX * meshSizeZ + meshSizeZ) * 8 + meshSizeX * 4);
				}
			}
			else if (a_zMax == a_layer.Raster.height)
			{
				//Top connection
				verts = new List<Vector3>((meshSizeX * meshSizeZ + meshSizeX) * 8 + meshSizeZ * 4);
			}
			else
			{
				//Both connection
				verts = new List<Vector3>(meshSizeX * meshSizeZ * 8 + (meshSizeX + meshSizeZ) * 4);
			}
			List<int> tris = new List<int>((int)((float)verts.Capacity * 1.5f));
			List<Vector2> heightUVs = new List<Vector2>(verts.Capacity);

			float stepX = (a_layer.coordinate1[0] - a_layer.coordinate0[0]) / a_layer.Raster.width;
			float stepZ = (a_layer.coordinate1[1] - a_layer.coordinate0[1]) / a_layer.Raster.height;

			int index = 0;
			for (int i = a_xMin; i < a_xMax; i++)
			{
				for (int j = a_zMin; j < a_zMax; j++)
				{
					float normalHeight = a_layer.Raster.GetPixel(i, j).r; //TODO: should be a for Alpha8 texture
					Vector3 p0 = new Vector3(
						i == 0 ? Mathf.Max(ImportedConfigRoot.Instance.ConfigToWorldSpaceX(a_layer.coordinate0[0] + i * stepX), ImportedConfigRoot.Instance.AreaCornerBLWorld.x)
							: ImportedConfigRoot.Instance.ConfigToWorldSpaceX(a_layer.coordinate0[0] + i * stepX), 
						0, 
						j == 0 ? Mathf.Max(ImportedConfigRoot.Instance.ConfigToWorldSpaceZ(a_layer.coordinate0[1] + j * stepZ), ImportedConfigRoot.Instance.AreaCornerBLWorld.z)
							: ImportedConfigRoot.Instance.ConfigToWorldSpaceZ(a_layer.coordinate0[1] + j * stepZ));
					Vector3 p1 = new Vector3(
						i == a_layer.Raster.width - 1 ? Mathf.Min(ImportedConfigRoot.Instance.ConfigToWorldSpaceX(a_layer.coordinate0[0] + (i + 1) * stepX), ImportedConfigRoot.Instance.AreaCornerTRWorld.x)
							: ImportedConfigRoot.Instance.ConfigToWorldSpaceX(a_layer.coordinate0[0] + (i + 1) * stepX), 
						0,
						j == a_layer.Raster.height - 1 ? Mathf.Min(ImportedConfigRoot.Instance.ConfigToWorldSpaceZ(a_layer.coordinate0[1] + (j + 1) * stepZ),ImportedConfigRoot.Instance.AreaCornerTRWorld.z)
							: ImportedConfigRoot.Instance.ConfigToWorldSpaceZ(a_layer.coordinate0[1] + (j + 1) * stepZ));

					bool cut = normalHeight < cutoff;
					if (cut)
					{
						normalHeight = 0f;
					}
					else
					{
						AddQuadYPlane(verts, tris, heightUVs, p0.x, p1.x, normalHeight, p0.z, p1.z, index);
						index += 4;
					}

					//Left plane (connecting or edge)
					float normalHeightOther = 0f; 
					if (i != 0)
					{
						normalHeightOther = a_layer.Raster.GetPixel((i - 1), j).r; //TODO: should be a for Alpha8 texture
						if (normalHeightOther < cutoff)
						{
							normalHeightOther = 0f;
						}
					}
					if (!cut || normalHeightOther >= cutoff)
					{
						AddQuadXPlane(verts, tris, heightUVs, p0.x, normalHeight, normalHeightOther, p0.z, p1.z, index);
						index += 4;
					}

					//Bottom plane (connecting or edge)
					normalHeightOther = 0f;
					if (j != 0)
					{
						normalHeightOther = a_layer.Raster.GetPixel(i, (j - 1)).r; //TODO: should be a for Alpha8 texture
						if (normalHeightOther < cutoff)
						{
							normalHeightOther = 0f;
						}
					}
					if (!cut || normalHeightOther >= cutoff)
					{
						AddQuadZPlane(verts, tris, heightUVs, p1.x, p0.x, normalHeight, normalHeightOther, p0.z, index);
						index += 4;
					}

					//Right edge plane
					if(i == a_layer.Raster.width - 1 && !cut)
					{
						AddQuadXPlane(verts, tris, heightUVs, p1.x, normalHeight, 0f, p1.z, p0.z, index);
						index += 4;
					}
					//Top edge plane
					if (j == a_layer.Raster.height - 1 && !cut)
					{ 
						AddQuadZPlane(verts, tris, heightUVs, p0.x, p1.x, normalHeight, 0f, p1.z, index);
						index += 4;
					}
				}
			}

			//Create mesh and set properties
			Mesh procMesh = new Mesh();
			procMesh.vertices = verts.ToArray();
			procMesh.uv2 = heightUVs.ToArray();
			procMesh.triangles = tris.ToArray();
			procMesh.RecalculateNormals();

			return procMesh;
		}

		void AddQuadZPlane(List<Vector3> a_verts, List<int> a_tris, List<Vector2> a_heightUVs, float a_x0, float a_x1, float a_y0, float a_y1, float a_z, int a_index)
		{
			float scaledY0 = a_y0 * height;
			float scaledY1 = a_y1 * height;
			float maxY = Mathf.Max(a_y0, a_y1);

			a_verts.Add(new Vector3(a_x0, scaledY1, a_z));
			a_verts.Add(new Vector3(a_x1, scaledY1, a_z));
			a_verts.Add(new Vector3(a_x1, scaledY0, a_z));
			a_verts.Add(new Vector3(a_x0, scaledY0, a_z));
			a_heightUVs.Add(new Vector2(maxY, 0f));
			a_heightUVs.Add(new Vector2(maxY, 0f));
			a_heightUVs.Add(new Vector2(maxY, 0f));
			a_heightUVs.Add(new Vector2(maxY, 0f));

			AddDefaultQuadIndices(a_tris, a_index);
		}
		void AddQuadYPlane(List<Vector3> a_verts, List<int> a_tris, List<Vector2> a_heightUVs, float a_x0, float a_x1, float a_y, float a_z0, float a_z1, int a_index)
		{
			float scaledY = a_y * height;

			a_verts.Add(new Vector3(a_x0, scaledY, a_z0));
			a_verts.Add(new Vector3(a_x0, scaledY, a_z1));
			a_verts.Add(new Vector3(a_x1, scaledY, a_z1));
			a_verts.Add(new Vector3(a_x1, scaledY, a_z0));
			a_heightUVs.Add(new Vector2(a_y, 1f));
			a_heightUVs.Add(new Vector2(a_y, 1f));
			a_heightUVs.Add(new Vector2(a_y, 1f));
			a_heightUVs.Add(new Vector2(a_y, 1f));

			AddDefaultQuadIndices(a_tris, a_index);
		}

		void AddQuadXPlane(List<Vector3> a_verts, List<int> a_tris, List<Vector2> a_heightUVs, float a_x, float a_y0, float a_y1, float a_z0, float a_z1, int a_index)
		{
			float scaledY0 = a_y0 * height;
			float scaledY1 = a_y1 * height;
			float maxY = Mathf.Max(a_y0, a_y1);

			a_verts.Add(new Vector3(a_x, scaledY0, a_z0));
			a_verts.Add(new Vector3(a_x, scaledY1, a_z0));
			a_verts.Add(new Vector3(a_x, scaledY1, a_z1));
			a_verts.Add(new Vector3(a_x, scaledY0, a_z1));
			a_heightUVs.Add(new Vector2(maxY, 0f));
			a_heightUVs.Add(new Vector2(maxY, 0f));
			a_heightUVs.Add(new Vector2(maxY, 0f));
			a_heightUVs.Add(new Vector2(maxY, 0f));

			AddDefaultQuadIndices(a_tris, a_index);
		}

		void AddDefaultQuadIndices(List<int> a_tris, int a_index)
		{
			//Tri 1
			a_tris.Add(a_index);
			a_tris.Add(a_index + 1);
			a_tris.Add(a_index + 2);
			//Tri 2
			a_tris.Add(a_index + 2);
			a_tris.Add(a_index + 3);
			a_tris.Add(a_index);
		}
	}
}
