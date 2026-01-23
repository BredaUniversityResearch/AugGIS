using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace POV_Unity
{
	public class DMAreaPattern : AVectorDisplayMethod
	{
		[JsonConverter(typeof(JsonConverterHexColor))]
		public Color colour = Color.white;
		public string texture = "0";

		public string type_colour;
		public string type_texture;

		public string meta_colour;
		public string meta_texture;

		protected override IDisplayMethodRenderData DisplayVectorLayer(VectorLayer a_layer, GameObject a_displayMethodRoot)
		{
			// Old call
			foreach (VectorObject poly in a_layer.data)
			{
				CreatePolygon(a_layer, poly, a_displayMethodRoot.transform);
			}

			string polyPattern = GetVariable<string>("texture", a_layer, a_layer.data[0]);

			// Group all child meshes
			MeshFilter[] meshFilters = a_displayMethodRoot.GetComponentsInChildren<MeshFilter>();
			CombineInstance[] instances = new CombineInstance[meshFilters.Length];

			for (int i = 0; i < meshFilters.Length; i++)
			{
				var meshFilter = meshFilters[i];
				instances[i] = new CombineInstance
				{
					mesh = meshFilter.mesh,
					transform = Matrix4x4.identity,
				};

				meshFilter.gameObject.SetActive(false);
			}

			Mesh combinedMesh = new Mesh();
			combinedMesh.CombineMeshes(instances);
			a_displayMethodRoot.AddComponent<MeshFilter>().mesh = combinedMesh;

			MeshRenderer renderer = a_displayMethodRoot.AddComponent<MeshRenderer>();
			renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
			Material mat = GetMaterialBasedOnPattern(polyPattern);

            // Setting transparency related variables for proper alpha rendering
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
			mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
			mat.SetInt("_SrcBlendAlpha", (int)BlendMode.SrcAlpha);
			mat.SetInt("_DstBlendAlpha", (int)BlendMode.DstAlpha);

			a_layer.RegisterLayerMaterial(mat);
			renderer.sharedMaterial = mat;

			return null;
		}

		Material GetMaterialBasedOnPattern(string pattern)
		{
            Material mat = AssetManager.GetMaterial("PolygonSolidMaterial");

			switch (pattern)
			{
				case "bacteria":
                    mat = AssetManager.GetMaterial("PolygonBacteriaMaterial");
                    break;
                case "cube":
                    mat = AssetManager.GetMaterial("PolygonCubesMaterial");
                    break;
                case "dots":
                    mat = AssetManager.GetMaterial("PolygonDotsMaterial");
                    break;
                case "raster":
                    mat = AssetManager.GetMaterial("PolygonRasterMaterial");
                    break;
                case "solid":
                    mat = AssetManager.GetMaterial("PolygonSolidMaterial");
                    break;
                case "stripe":
                    mat = AssetManager.GetMaterial("PolygonStripeMaterial");
                    break;
                case "zigzag":
                    mat = AssetManager.GetMaterial("PolygonZigZagMaterial");
                    break;
                default:
                    mat = AssetManager.GetMaterial("PolygonSolidMaterial");
                    break;
			}

			return mat;
        }

        public void CreatePolygon(VectorLayer a_layer, VectorObject a_polygon, Transform a_parent)
		{
			if (a_polygon.Shapes.Count == 0)
				return; 

			string polyPattern = GetVariable<string>("texture", a_layer, a_polygon);
			Color polyColour = GetColour("colour", a_layer, a_polygon);

			foreach (VectorShape shape in a_polygon.Shapes)
			{
				if (shape.m_points.Count < 3)
					continue;

				GameObject areaMesh = new GameObject("AreaColourMesh_" + a_parent.childCount);
				areaMesh.transform.SetParent(a_parent, false);

				Poly2Mesh.Polygon poly = new Poly2Mesh.Polygon();
				poly.outside = shape.m_points;
				poly.holes = shape.m_holes;

				//Create polygon mesh
				MeshFilter meshFilter = areaMesh.AddComponent<MeshFilter>();

				try
				{
					Mesh mesh = Poly2Mesh.CreateMesh(poly);

					if(mesh == null)
					{
						Debug.LogWarningFormat("Shape data is incorrect skipping shape mesh generation for layer: {0}", a_layer.name);
						continue;
					}

					meshFilter.mesh = mesh;
				}
				catch (Exception e)
				{
					Mesh mesh = Poly2Mesh.CreateMesh(poly);

					if (mesh == null)
					{
						Debug.LogWarningFormat("Shape data is incorrect skipping shape mesh generation for layer: {0}", a_layer.name);
						continue;
					}

					poly.holes = new List<List<Vector3>>();
					meshFilter.mesh = mesh;
				}

				Color[] colors = Enumerable.Repeat(polyColour, meshFilter.mesh.vertexCount).ToArray();
				meshFilter.mesh.SetColors(colors);
				MeshRenderer renderer = areaMesh.AddComponent<MeshRenderer>();
				renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
				renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                Material mat = GetMaterialBasedOnPattern(polyPattern);
                a_layer.RegisterLayerMaterial(mat);
				renderer.sharedMaterial = mat;
			}
		}
	}
}
