using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace POV_Unity
{
	public class DMValueMapQuad : ARasterDisplayMethod
	{
		const int MESH_SIZE = 100;

		//Core
		public string colour_scale = "Gradient_Rainbow"; //Name of colour texture
		[JsonConverter(typeof(JsonConverterHexColor))]
		public Color outbounds_colour_and_alpha = Color.clear;
		public float alpha = 1f;
        public bool invert;

		//Side
		public bool generate_side;
		public float side_y_1 = 0f;
		public float side_y_0 = -0.1f;
		public float side_y_base = -0.07f;
		public string side_mat = "BathymetrySide";

		protected override IDisplayMethodRenderData DisplayRasterLayer(RasterLayer a_layer, GameObject a_displayMethodRoot)
		{
			Material mat = new Material(AssetManager.GetHeatMapMaterialTexture(invert));
			mat.SetTexture("_ValueMap", a_layer.Raster);
			mat.SetTexture("_ColourScale", AssetManager.GetTexture(colour_scale));
			mat.SetFloat("_Alpha", alpha);
			mat.SetColor("_OutOfBoundsColourAndAlpha", outbounds_colour_and_alpha);

			Vector3 p0 = ImportedConfigRoot.Instance.ConfigToWorldSpaceXZ(a_layer.coordinate0);
			Vector3 p1 = ImportedConfigRoot.Instance.ConfigToWorldSpaceXZ(a_layer.coordinate1);
			Vector3 area0 = ImportedConfigRoot.Instance.AreaCornerBLWorld;
			Vector3 area1 = ImportedConfigRoot.Instance.AreaCornerTRWorld;
			Vector2 uv0 = new Vector2((area0.x - p0.x) / (p1.x - p0.x), (area0.z - p0.z) / (p1.z - p0.z));
			Vector2 uv1 = new Vector2((area1.x - p0.x) / (p1.x - p0.x), (area1.z - p0.z) / (p1.z - p0.z));

			Mesh procMesh = new Mesh();
			procMesh.vertices = new Vector3[] {
				new Vector3(area0.x, side_y_1, area0.z),
				new Vector3(area0.x, side_y_1, area1.z),
				new Vector3(area1.x, side_y_1, area1.z),
				new Vector3(area1.x, side_y_1, area0.z)
			};
			procMesh.uv = new Vector2[] {
				new Vector2(uv0.x,uv0.y),
				new Vector2(uv0.x,uv1.y),
				new Vector2(uv1.x,uv1.y),
				new Vector2(uv1.x,uv0.y)
			};
			procMesh.triangles = new int[] { 0, 1, 2, 2, 3, 0 };
			procMesh.RecalculateNormals();

			//Add mesh renderer
			MeshFilter meshFilter = a_displayMethodRoot.AddComponent<MeshFilter>();
			meshFilter.mesh = procMesh;
			MeshRenderer renderer = a_displayMethodRoot.AddComponent<MeshRenderer>();
			renderer.material = mat;
			renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

			if (generate_side)
				GenerateSideMeshSquare(a_layer, a_displayMethodRoot.transform);

			return null;
		}

        void GenerateSideMesh(RasterLayer a_layer, Transform a_parent)
        {
            List<Vector3> verts = new List<Vector3>((a_layer.Raster.width + a_layer.Raster.height + 2) * 8);
            List<int> tris = new List<int>((a_layer.Raster.width + a_layer.Raster.height) * 24);
            List<Vector2> uvs = new List<Vector2>(verts.Capacity);

            Vector3 pos0 = ImportedConfigRoot.Instance.ConfigToWorldSpaceXZ(a_layer.coordinate0);
            Vector3 pos1 = ImportedConfigRoot.Instance.ConfigToWorldSpaceXZ(a_layer.coordinate1);
            Vector3 area0 = ImportedConfigRoot.Instance.AreaCornerBLWorld;
            Vector3 area1 = ImportedConfigRoot.Instance.AreaCornerTRWorld;
            int index = 0;
            float step = (pos1.x - pos0.x) / a_layer.Raster.width;
            int max = a_layer.Raster.height - 1;

            AddSideVerts(verts, uvs, area0.x, area0.z, GetValueAt(a_layer, 0, 0));
            AddSideVerts(verts, uvs, area0.x, area1.z, GetValueAt(a_layer, 0, max));
            index += 8;
            for (int i = 1; i < a_layer.Raster.width; i++)
            {
                //Horizontal (top and bottom)
                AddSideVerts(verts, uvs, pos0.x + i * step, area0.z, GetValueAt(a_layer, i - 1, 0));
                ConnectSideVerts(tris, index);
                index += 4;
                AddSideVerts(verts, uvs, pos0.x + i * step, area1.z, GetValueAt(a_layer, i - 1, max));
                ConnectSideVertsReverse(tris, index);
                index += 4;
            }
            AddSideVerts(verts, uvs, area1.x, area0.z, GetValueAt(a_layer, a_layer.Raster.width - 1, 0));
            ConnectSideVerts(tris, index);
            index += 4;
            AddSideVerts(verts, uvs, area1.x, area1.z, GetValueAt(a_layer, a_layer.Raster.width - 1, max));
            ConnectSideVertsReverse(tris, index);
            index += 4;

            max = a_layer.Raster.width - 1;
			step = (pos1.z - pos0.z) / a_layer.Raster.height;
			AddSideVerts(verts, uvs, area0.x, area0.z, GetValueAt(a_layer, 0, 0));
			AddSideVerts(verts, uvs, area1.x, area0.z, GetValueAt(a_layer, max, 0));
            index += 8;
			for (int i = 1; i < a_layer.Raster.height; i++)
            {
				//Vertical (left and right)
				AddSideVerts(verts, uvs, area0.x, pos0.z + i * step, GetValueAt(a_layer, 0, i - 1));
				ConnectSideVertsReverse(tris, index);
                index += 4;
				AddSideVerts(verts, uvs, area1.x, pos0.z + i * step, GetValueAt(a_layer, max, i - 1));
                ConnectSideVerts(tris, index);
				index += 4;
			}
            AddSideVerts(verts, uvs, area0.x, area1.z, GetValueAt(a_layer, 0, a_layer.Raster.height - 1));
            ConnectSideVertsReverse(tris, index);
            index += 4;
            AddSideVerts(verts, uvs, area1.x, area1.z, GetValueAt(a_layer, max, a_layer.Raster.height - 1));
            ConnectSideVerts(tris, index);
            index += 4;

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

		void GenerateSideMeshSquare(RasterLayer a_layer, Transform a_parent)
		{
			List<Vector3> verts = new List<Vector3>((a_layer.Raster.width + a_layer.Raster.height + 2) * 8);
			List<int> tris = new List<int>((a_layer.Raster.width + a_layer.Raster.height) * 24);
			List<Vector2> uvs = new List<Vector2>(verts.Capacity);

			Vector3 pos0 = ImportedConfigRoot.Instance.ConfigToWorldSpaceXZ(a_layer.coordinate0);
			Vector3 pos1 = ImportedConfigRoot.Instance.ConfigToWorldSpaceXZ(a_layer.coordinate1);
			Vector3 area0 = ImportedConfigRoot.Instance.AreaCornerBLWorld;
			Vector3 area1 = ImportedConfigRoot.Instance.AreaCornerTRWorld;
			int index = 0;
			float step = (pos1.x - pos0.x) / a_layer.Raster.width;
			int max = a_layer.Raster.height - 1;

			AddSideQuads(verts, uvs, tris, area0.x, area0.z, pos0.x + step, area0.z, GetValueAt(a_layer, 0, 0), index);
			index += 8;
			AddSideQuads(verts, uvs, tris, pos0.x + step, area1.z, area0.x, area1.z, GetValueAt(a_layer, 0, max), index);
			index += 8;
			for (int i = 1; i < a_layer.Raster.width-1; i++)
			{
				//Horizontal (top and bottom)
				AddSideQuads(verts, uvs, tris, pos0.x + i * step, area0.z, pos0.x + (i+1) * step, area0.z, GetValueAt(a_layer, i, 0), index);
				index += 8;
				AddSideQuads(verts, uvs, tris, pos0.x + (i + 1) * step, area1.z, pos0.x + i * step, area1.z, GetValueAt(a_layer, i, max), index);
				index += 8;
			}
			AddSideQuads(verts, uvs, tris, area1.x, area0.z, pos0.x + (a_layer.Raster.width - 1) * step, area0.z, GetValueAt(a_layer, a_layer.Raster.width - 1, 0), index);
			index += 8;
			AddSideQuads(verts, uvs, tris, pos0.x + (a_layer.Raster.width - 1) * step, area1.z, area1.x, area1.z, GetValueAt(a_layer, a_layer.Raster.width - 1, max), index);
			index += 8;

			max = a_layer.Raster.width - 1;
			step = (pos1.z - pos0.z) / a_layer.Raster.height;
			AddSideQuads(verts, uvs, tris, area0.x, pos0.z + step, area0.x, area0.z, GetValueAt(a_layer, 0, 0), index);
			AddSideQuads(verts, uvs, tris, area1.x, area0.z, area1.x, pos0.z + step, GetValueAt(a_layer, max, 0), index);
			index += 8;
			for (int i = 1; i < a_layer.Raster.height-1; i++)
			{
				//Vertical (left and right)
				AddSideQuads(verts, uvs, tris, area0.x, pos0.z + (i + 1) * step, area0.x, pos0.z + i * step, GetValueAt(a_layer, 0, i), index);
				index += 8;
				AddSideQuads(verts, uvs, tris, area1.x, pos0.z + i * step, area1.x, pos0.z + (i+1) * step, GetValueAt(a_layer, max, i), index);
				index += 8;
			}
			AddSideQuads(verts, uvs, tris, area0.x, area1.z, area0.x, pos0.z + (a_layer.Raster.height - 1) * step, GetValueAt(a_layer, 0, a_layer.Raster.height - 1), index);
			index += 8;
			AddSideQuads(verts, uvs, tris, area1.x, pos0.z + (a_layer.Raster.height - 1) * step, area1.x, area1.z, GetValueAt(a_layer, max, a_layer.Raster.height - 1), index);
			index += 8;

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

		void AddSideQuads(List<Vector3> a_verts, List<Vector2> a_UVs, List<int> a_tris, float a_x1, float a_z1, float a_x2, float a_z2, float a_pixelvalue, int a_index)
		{
			a_verts.Add(new Vector3(a_x1, side_y_1, a_z1));
			a_verts.Add(new Vector3(a_x1, (side_y_1 - side_y_0) * a_pixelvalue + side_y_0, a_z1));
			a_verts.Add(new Vector3(a_x1, (side_y_1 - side_y_0) * a_pixelvalue + side_y_0, a_z1));
			a_verts.Add(new Vector3(a_x1, side_y_base, a_z1));

			a_verts.Add(new Vector3(a_x2, side_y_1, a_z2));
			a_verts.Add(new Vector3(a_x2, (side_y_1 - side_y_0) * a_pixelvalue + side_y_0, a_z2));
			a_verts.Add(new Vector3(a_x2, (side_y_1 - side_y_0) * a_pixelvalue + side_y_0, a_z2));
			a_verts.Add(new Vector3(a_x2, side_y_base, a_z2));

			a_UVs.Add(new Vector2(1f, a_pixelvalue));
			a_UVs.Add(new Vector2(1f, a_pixelvalue));
			a_UVs.Add(new Vector2(0f, a_pixelvalue));
			a_UVs.Add(new Vector2(0f, a_pixelvalue));
			a_UVs.Add(new Vector2(1f, a_pixelvalue));
			a_UVs.Add(new Vector2(1f, a_pixelvalue));
			a_UVs.Add(new Vector2(0f, a_pixelvalue));
			a_UVs.Add(new Vector2(0f, a_pixelvalue));

			//Tri 1
			a_tris.Add(a_index);
			a_tris.Add(a_index + 4);
			a_tris.Add(a_index + 1);
			//Tri 2
			a_tris.Add(a_index + 1);
			a_tris.Add(a_index + 4);
			a_tris.Add(a_index + 5);
			//Tri 3
			a_tris.Add(a_index + 2);
			a_tris.Add(a_index + 6);
			a_tris.Add(a_index + 3);
			//Tri 4
			a_tris.Add(a_index + 3);
			a_tris.Add(a_index + 6);
			a_tris.Add(a_index + 7);
		}

		void AddSideVerts(List<Vector3> a_verts, List<Vector2> a_UVs, float a_x, float a_z, float a_pixelvalue)
        {
            a_verts.Add(new Vector3(a_x, side_y_1, a_z));
            a_verts.Add(new Vector3(a_x, (side_y_1 - side_y_0) * a_pixelvalue + side_y_0, a_z));
            a_verts.Add(new Vector3(a_x, (side_y_1 - side_y_0) * a_pixelvalue + side_y_0, a_z));
            a_verts.Add(new Vector3(a_x, side_y_base, a_z));

            a_UVs.Add(new Vector2(1f, a_pixelvalue));
            a_UVs.Add(new Vector2(1f, a_pixelvalue));
            a_UVs.Add(new Vector2(0f, a_pixelvalue));
            a_UVs.Add(new Vector2(0f, a_pixelvalue));
        }

        void ConnectSideVerts(List<int> a_tris, int a_index)
		{
            //Offset by -4 to ignore the opposide side in between
            //Tri 1
            a_tris.Add(a_index);
            a_tris.Add(a_index + 1);
            a_tris.Add(a_index - 7);
            //Tri 2
            a_tris.Add(a_index - 7);
            a_tris.Add(a_index - 8);
            a_tris.Add(a_index);
            //Tri 3
            a_tris.Add(a_index + 2);
            a_tris.Add(a_index + 3);
            a_tris.Add(a_index - 5);
            //Tri 4
            a_tris.Add(a_index - 5);
            a_tris.Add(a_index - 6);
            a_tris.Add(a_index + 2);
        }

        void ConnectSideVertsReverse(List<int> a_tris, int a_index)
        {
            //As above, but reversed winding order
			//Tri 1
			a_tris.Add(a_index);
			a_tris.Add(a_index - 7);
			a_tris.Add(a_index + 1);
			//Tri 2
			a_tris.Add(a_index - 7);
			a_tris.Add(a_index);
			a_tris.Add(a_index - 8);
			//Tri 3
			a_tris.Add(a_index + 2);
			a_tris.Add(a_index - 5);
			a_tris.Add(a_index + 3);
			//Tri 4
			a_tris.Add(a_index - 5);
			a_tris.Add(a_index + 2);
			a_tris.Add(a_index - 6);
		}
    }
}
