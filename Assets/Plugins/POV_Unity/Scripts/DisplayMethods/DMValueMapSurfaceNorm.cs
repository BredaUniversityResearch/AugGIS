using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace POV_Unity
{
	public class DMValueMapSurfaceNorm : ARasterDisplayMethod
	{
		const int MESH_SIZE = 100;

		public string colour_scale = "Gradient_Rainbow"; //Name of colour texture
		public float min_range = 0.1f; 
		public float alpha = 1f; 
		public bool uv_colour = true;
		public bool invert = false;
		public float y_1 = 0f;
		public float y_max_frac = 0.5f;
		public float y_min = -0.05f;

		//Side
		public bool generate_side;
		public float side_y_base = -0.07f;
		public string side_mat = "BathymetrySide";
		public bool generate_water_cube;
		public float water_cube_offset = 0.0001f;

		protected override IDisplayMethodRenderData DisplayRasterLayer(RasterLayer a_layer, GameObject a_displayMethodRoot)
		{
			List<MeshRenderer> meshes = new List<MeshRenderer>();

			int i = 0, j = 0;
			int nextI, nextJ;
			float min = 2f, max = -1f;
			while (i < a_layer.Raster.width)
			{
				nextI = Math.Min(i + MESH_SIZE, a_layer.Raster.width);
				while (j < a_layer.Raster.height)
				{
					nextJ = Math.Min(j + MESH_SIZE, a_layer.Raster.height);
					meshes.Add(GenerateSmoothMeshForPixels(a_layer, a_displayMethodRoot.transform, i, nextI, j, nextJ, ref min, ref max));
					j = nextJ;
				}
				i = nextI;
				j = 0;
			}

			Material mat = null;
			if (uv_colour)
			{
				mat = new Material(AssetManager.GetSurfaceMaterialUV());
			}
			else
			{
				mat = new Material(AssetManager.GetHeatMapMaterialTexture(invert));
				mat.SetTexture("_ValueMap", a_layer.Raster);
			}
			mat.SetTexture("_ColourScale", AssetManager.GetTexture(colour_scale));
			mat.SetFloat("_Alpha", alpha);

			//Rescale meshes based on min & max
			if (max - min < min_range)
			{
				max = min + min_range;
				if (max > 1f)
				{
					max = 1f;
					min = 1f - min_range;
				}
			}
			float height = (y_1 - y_min) * (1f - y_max_frac * (1f - max));
			float scale = height / (max - min);

			foreach (MeshRenderer renderer in meshes)
			{
				renderer.transform.localScale = new Vector3(1f, scale, 1f);
				renderer.transform.localPosition = new Vector3(0f, -min * scale + y_min, 0f);
				renderer.material = mat;
				renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
				renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
			}

			if (generate_side)
				GenerateSideMesh(a_layer, a_displayMethodRoot.transform, min, max, height);
			if (generate_water_cube)
				GenerateWaterCube(a_layer, a_displayMethodRoot.transform);

			return null;
		}

		MeshRenderer GenerateSmoothMeshForPixels(RasterLayer a_layer, Transform a_parent, int a_xMin, int a_xMax, int a_zMin, int a_zMax, ref float a_min, ref float a_max)
		{
			//Ignore subdivisions, but do keep to mesh size
			int meshSizeX = a_xMax - a_xMin;
			int meshSizeZ = a_zMax - a_zMin;
			List<Vector3> verts = new List<Vector3>(meshSizeX  * meshSizeZ);
			List<int> tris = new List<int>((meshSizeX-1) * (meshSizeZ-1) * 6);
			Vector2[] uvs = new Vector2[meshSizeX * meshSizeZ];

			Vector3 pos0 = ImportedConfigRoot.Instance.ConfigToWorldSpaceXZ(a_layer.coordinate0);
			Vector3 pos1 = ImportedConfigRoot.Instance.ConfigToWorldSpaceXZ(a_layer.coordinate1);
			float stepX = (pos1.x - pos0.x) / (a_layer.Raster.width-1);
			float stepZ = (pos1.z - pos0.z) / (a_layer.Raster.height-1);
			float uvStepX = 1 / ((float)a_layer.Raster.width-1);
			float uvStepZ = 1 / ((float)a_layer.Raster.height-1);

			int index = 0;
			for (int i = a_xMin; i < a_xMax; i++)
			{
				for (int j = a_zMin; j < a_zMax; j++)
				{
					float pixelValue = GetValueAt(a_layer, i, j);
					verts.Add((new Vector3(
						i == 0 ? ImportedConfigRoot.Instance.AreaCornerBLWorld.x : i == a_layer.Raster.width - 1 ? ImportedConfigRoot.Instance.AreaCornerTRWorld.x : pos0.x + i * stepX,
						pixelValue,
						j == 0 ? ImportedConfigRoot.Instance.AreaCornerBLWorld.z : j == a_layer.Raster.height - 1 ? ImportedConfigRoot.Instance.AreaCornerTRWorld.z : pos0.z + j * stepZ)));
					if(uv_colour)
						uvs[index] = new Vector2(pixelValue, 0f);
					else
						uvs[index] = new Vector2(i * uvStepX, j * uvStepZ);
					if (pixelValue < a_min)
						a_min = pixelValue;
					else if (pixelValue > a_max)
						a_max = pixelValue;

					if (i != a_xMin && j != a_zMin)
					{
						//Tri 1
						tris.Add(index);                //Top right
						tris.Add(index - 1);            //Bottom right
						tris.Add(index - meshSizeZ - 1);//Bottom left 
						//Tri 2
						tris.Add(index - meshSizeZ - 1);//Bottom left 
						tris.Add(index - meshSizeZ);    //Top left
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
			GameObject meshObject = new GameObject("Heatmap Mesh");
			meshObject.transform.SetParent(a_parent, false);
			MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
			meshFilter.mesh = procMesh;
			MeshRenderer renderer = meshObject.AddComponent<MeshRenderer>();
			return renderer;
		}

		void GenerateSideMesh(RasterLayer a_layer, Transform a_parent, float a_min, float a_max, float a_height)
		{
			List<Vector3> verts = new List<Vector3>((a_layer.Raster.width + a_layer.Raster.height + 2) * 8);
			List<int> tris = new List<int>((a_layer.Raster.width + a_layer.Raster.height) * 24);
			List<Vector2> uvs = new List<Vector2>(verts.Capacity);

			Vector3 pos0 = ImportedConfigRoot.Instance.ConfigToWorldSpaceXZ(a_layer.coordinate0);
			Vector3 pos1 = ImportedConfigRoot.Instance.ConfigToWorldSpaceXZ(a_layer.coordinate1);
			Vector3 area0 = ImportedConfigRoot.Instance.AreaCornerBLWorld;
			Vector3 area1 = ImportedConfigRoot.Instance.AreaCornerTRWorld;
			int index = 0;
			float step = (pos1.x - pos0.x) / (a_layer.Raster.width-1);
			int max = a_layer.Raster.height - 1;
			float range = a_max - a_min;

			AddSideVerts(verts, uvs, area0.x, area0.z, (GetValueAt(a_layer, 0, 0) - a_min) / range * a_height + y_min);
			AddSideVerts(verts, uvs, area0.x, area1.z, (GetValueAt(a_layer, 0, max) - a_min) / range * a_height + y_min);
			index += 4;
			for (int i = 1; i < a_layer.Raster.width -1; i++)
			{
				//Horizontal (top and bottom)
				AddSideVerts(verts, uvs, pos0.x + i * step, area0.z, (GetValueAt(a_layer, i, 0) - a_min) / range * a_height + y_min);
				ConnectSideVerts(tris, index);
				index += 2;
				AddSideVerts(verts, uvs, pos0.x + i * step, area1.z, (GetValueAt(a_layer, i, max) - a_min) / range * a_height + y_min);
				ConnectSideVertsReverse(tris, index);
				index += 2;
			}
			AddSideVerts(verts, uvs, area1.x, area0.z, (GetValueAt(a_layer, a_layer.Raster.width - 1, 0) - a_min) / range * a_height + y_min);
			ConnectSideVerts(tris, index);
			index += 2;
			AddSideVerts(verts, uvs, area1.x, area1.z, (GetValueAt(a_layer, a_layer.Raster.width - 1, max) - a_min) / range * a_height + y_min);
			ConnectSideVertsReverse(tris, index);
			index += 2;

			max = a_layer.Raster.width - 1;
			step = (pos1.z - pos0.z) / (a_layer.Raster.height-1);
			AddSideVerts(verts, uvs, area0.x, area0.z, (GetValueAt(a_layer, 0, 0) - a_min) / range * a_height + y_min);
			AddSideVerts(verts, uvs, area1.x, area0.z, (GetValueAt(a_layer, max, 0) - a_min) / range * a_height + y_min);
			index += 4;
			for (int i = 1; i < a_layer.Raster.height -1; i++)
			{
				//Vertical (left and right)
				AddSideVerts(verts, uvs, area0.x, pos0.z + i * step, (GetValueAt(a_layer, 0, i) - a_min) / range * a_height + y_min);
				ConnectSideVertsReverse(tris, index);
				index += 2;
				AddSideVerts(verts, uvs, area1.x, pos0.z + i * step, (GetValueAt(a_layer, max, i) - a_min) / range * a_height + y_min);
				ConnectSideVerts(tris, index);
				index += 2;
			}
			AddSideVerts(verts, uvs, area0.x, area1.z, (GetValueAt(a_layer, 0, a_layer.Raster.height - 1) - a_min) / range * a_height + y_min);
			ConnectSideVertsReverse(tris, index);
			index += 2;
			AddSideVerts(verts, uvs, area1.x, area1.z, (GetValueAt(a_layer, max, a_layer.Raster.height - 1) - a_min) / range * a_height + y_min);
			ConnectSideVerts(tris, index);
			index += 2;

			//Create mesh and set properties
			Mesh procMesh = new Mesh();
			procMesh.vertices = verts.ToArray();
			procMesh.uv = uvs.ToArray();
			procMesh.triangles = tris.ToArray();
			procMesh.RecalculateNormals();

			//Create gameobject and add mesh renderer
			GameObject meshObject = new GameObject("Side Mesh");
			meshObject.transform.SetParent(a_parent, false);
			MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
			meshFilter.mesh = procMesh;
			MeshRenderer renderer = meshObject.AddComponent<MeshRenderer>();
			renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
			Material mat = new Material(AssetManager.GetMaterial(side_mat));
			mat.SetTexture("_ColourScale", AssetManager.GetTexture(colour_scale));
			renderer.material = mat;
		}

		float GetValueAt(RasterLayer a_layer, int a_x, int a_y)
		{
			return invert ? 1f - a_layer.Raster.GetPixel(a_x, a_y).r : a_layer.Raster.GetPixel(a_x, a_y).r; //TODO: should be a for Alpha8 texture
		}

		void AddSideVerts(List<Vector3> a_verts, List<Vector2> a_UVs, float a_x, float a_z, float a_y)
		{
			a_verts.Add(new Vector3(a_x, a_y, a_z));
			a_verts.Add(new Vector3(a_x, side_y_base, a_z));

			a_UVs.Add(new Vector2(0f, a_y));
			a_UVs.Add(new Vector2(0f, a_y));
		}

		void ConnectSideVerts(List<int> a_tris, int a_index)
		{
			//Offset by -2 to ignore the opposide side in between
			//Tri 1
			a_tris.Add(a_index);
			a_tris.Add(a_index + 1);
			a_tris.Add(a_index - 3);
			//Tri 2
			a_tris.Add(a_index - 3);
			a_tris.Add(a_index - 4);
			a_tris.Add(a_index);
		}

		void ConnectSideVertsReverse(List<int> a_tris, int a_index)
		{
			//As above, but reversed winding order
			//Tri 1
			a_tris.Add(a_index);
			a_tris.Add(a_index - 3);
			a_tris.Add(a_index + 1);
			//Tri 2
			a_tris.Add(a_index - 3);
			a_tris.Add(a_index);
			a_tris.Add(a_index - 4);
		}

		void GenerateWaterCube(RasterLayer a_layer, Transform a_parent)
		{
			Vector3 pos0 = ImportedConfigRoot.Instance.AreaCornerBLWorld + new Vector3(water_cube_offset, side_y_base + water_cube_offset, water_cube_offset);
			Vector3 pos1 = ImportedConfigRoot.Instance.AreaCornerTRWorld + new Vector3(-water_cube_offset, y_1 + water_cube_offset, -water_cube_offset);

			//Create mesh and set properties
			Mesh procMesh = new Mesh();
			procMesh.vertices = new Vector3[]
			{
			//Top
			new Vector3(pos0.x, pos1.y, pos0.z),
			new Vector3(pos1.x, pos1.y, pos0.z),
			pos1,
			new Vector3(pos0.x, pos1.y, pos1.z),

			//Left
			pos0,
			new Vector3(pos0.x, pos0.y, pos1.z),
			new Vector3(pos0.x, pos1.y, pos1.z),
			new Vector3(pos0.x, pos1.y, pos0.z),

			//Right
			new Vector3(pos1.x, pos0.y, pos0.z),
			new Vector3(pos1.x, pos0.y, pos1.z),
			pos1,
			new Vector3(pos1.x, pos1.y, pos0.z),

			//Front
			pos0,
			new Vector3(pos1.x, pos0.y, pos0.z),
			new Vector3(pos1.x, pos1.y, pos0.z),
			new Vector3(pos0.x, pos1.y, pos0.z),

			//Back
			new Vector3(pos0.x, pos0.y, pos1.z),
			new Vector3(pos1.x, pos0.y, pos1.z),
			pos1,
			new Vector3(pos0.x, pos1.y, pos1.z),
			};
			procMesh.triangles = new int[]
			{
				0,3,2,
				2,1,0,
				5,6,7,
				7,4,5,
				8,11,10,
				10,9,8,
				12,15,14,
				14,13,12,
				17,18,19,
				19,16,17
			};
			procMesh.uv = new Vector2[]
			{
			new Vector2(0,0),
			new Vector2(1,0),
			new Vector2(1,1),
			new Vector2(0,1),
			new Vector2(0,0),
			new Vector2(1,0),
			new Vector2(1,1),
			new Vector2(0,1),
			new Vector2(0,0),
			new Vector2(1,0),
			new Vector2(1,1),
			new Vector2(0,1),
			new Vector2(0,0),
			new Vector2(1,0),
			new Vector2(1,1),
			new Vector2(0,1),
			new Vector2(0,0),
			new Vector2(1,0),
			new Vector2(1,1),
			new Vector2(0,1),
			};

			procMesh.Optimize();
			procMesh.RecalculateNormals();

			//Create gameobject and add mesh renderer
			GameObject meshObject = new GameObject("WaterCube");
			meshObject.transform.SetParent(a_parent, false);
			MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
			meshFilter.mesh = procMesh;
			MeshRenderer renderer = meshObject.AddComponent<MeshRenderer>();
			renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
			renderer.material = AssetManager.GetMaterial("PrettyWater");
		}
	}
}
