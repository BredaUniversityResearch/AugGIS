using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Newtonsoft.Json;
using UnityEngine;


namespace POV_Unity
{
	public class DMValueMapCubes : ARasterDisplayMethod
	{
		struct ValueMapCubeInstanceData
		{
			public Matrix4x4 modelMatrix;
			public float value;
		}

		public float valueScaler = 0.1f;
		public float defaultScale = 0.5f;
		public float minScale = 0.1f;
		public float maxScale = 1.0f;
		public bool scaleBasedOnValue = true;
		public float cutoff = 0.01f;
		public float groundPosY = 0.0f;
		public bool posYBasedOnValue = true;
		public float posYScaler = 0.1f;
		public bool useGradientColour = true;
		public string colour_scale = "Gradient_Rainbow";

		[JsonConverter(typeof(JsonConverterHexColor))]
		public Color staticColour = Color.black;

		protected override IDisplayMethodRenderData DisplayRasterLayer(RasterLayer a_layer, GameObject a_displayMethodRoot)
		{
			List<ValueMapCubeInstanceData> instanceData = new List<ValueMapCubeInstanceData>();

			for (int row = 0; row < a_layer.Raster.height; row++)
			{
				for (int col = 0; col < a_layer.Raster.width; col++)
				{
					float texValue = a_layer.Raster.GetPixel(col, -row - 1).r;

					if (texValue < cutoff)
						continue;

					ValueMapCubeInstanceData instance = new ValueMapCubeInstanceData();

					instance.modelMatrix = CalculateMatrix(
						row,
						col,
						texValue,
						new int2(a_layer.Raster.width, a_layer.Raster.height),
						a_layer.coordinate0[0],
						a_layer.coordinate0[1],
						a_layer.coordinate1[0],
						a_layer.coordinate1[1]
					);

					instance.value = texValue;

					instanceData.Add(instance);
				}
			}

			CombineInstance[] meshInstances = new CombineInstance[instanceData.Count];
			Texture2D colourTexture = AssetManager.GetTexture(colour_scale);

			for (int i = 0; i < instanceData.Count; i++)
			{
				Mesh cube = CreateCube();

				if (useGradientColour)
					cube = ColorCube(cube, colourTexture.GetPixelBilinear(instanceData[i].value, 0.5f));
				else
					cube = ColorCube(cube, staticColour);
					

				meshInstances[i] = new CombineInstance
				{
					mesh = cube,
					transform = instanceData[i].modelMatrix,
				};
			}

			Mesh combinedMesh = new Mesh();
			combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; //Support large meshes
			combinedMesh.CombineMeshes(meshInstances);
			combinedMesh.Optimize();
			a_displayMethodRoot.AddComponent<MeshFilter>().mesh = combinedMesh;
			MeshRenderer renderer = a_displayMethodRoot.AddComponent<MeshRenderer>();
			renderer.material = new Material(AssetManager.GetMaterial("ValueMapCubes"));

			return null;
		}

		private Mesh CreateQuad(float width = 1f, float height = 1f)
		{
			// Create a quad mesh.
			var mesh = new Mesh();

			float w = width * .5f;
			float h = height * .5f;
			var vertices = new Vector3[4]
			{
				new Vector3(-w, 0, -h),
				new Vector3(w, 0, -h),
				new Vector3(-w, 0, h),
				new Vector3(w, 0, h)
			};

			var tris = new int[6]
			{
				// lower left tri.
				0, 2, 1,
				// lower right tri
				2, 3, 1
			};

			var normals = new Vector3[4]
			{
				-Vector3.forward,
				-Vector3.forward,
				-Vector3.forward,
				-Vector3.forward,
			};

			var uv = new Vector2[4]
			{
				new Vector2(0, 0),
				new Vector2(1, 0),
				new Vector2(0, 1),
				new Vector2(1, 1),
			};

			mesh.vertices = vertices;
			mesh.triangles = tris;
			mesh.normals = normals;
			mesh.uv = uv;

			return mesh;
		}

		private Mesh CreateCube(float size = 1f)
		{
			// Create a cube mesh.
			var mesh = new Mesh();

			float n = size * .5f;
			var vertices = new Vector3[8]
			{
				new Vector3(-n, -n, -n),
				new Vector3(n, -n, -n),
				new Vector3(n, -n, n),
				new Vector3(-n, -n, n),
				new Vector3(-n, n, -n),
				new Vector3(n, n, -n),
				new Vector3(n, n, n),
				new Vector3(-n, n, n)
			};

			var tris = new int[36]
			{
				// Bottom
				1, 2, 0,
				2, 3, 0,
				// Left
				7, 4, 0,
				3, 7, 0,
				// Front
				6, 7, 3,
				2, 6, 3,
				// Back
				4, 5, 1,
				0, 4, 1,
				// Right
				5, 6, 2,
				1, 5, 2,
				// Top
				6, 5, 4,
				7, 6, 4
			};

			var normals = new Vector3[8]
			{
				Vector3.down,
				Vector3.down,
				Vector3.down,
				Vector3.down,
				Vector3.up,
				Vector3.up,
				Vector3.up,
				Vector3.up
			};

			var uv = new Vector2[8]
			{
				new Vector2(0, 0),
				new Vector2(1, 0),
				new Vector2(1, 1),
				new Vector2(0, 1),
				new Vector2(0, 0),
				new Vector2(1, 0),
				new Vector2(1, 1),
				new Vector2(0, 1)
			};

			mesh.vertices = vertices;
			mesh.triangles = tris;
			mesh.normals = normals;
			mesh.uv = uv;

			return mesh;
		}

		private Mesh CreateTetrahedron(float size = 1f)
		{
			// Create a tetrahedron mesh.
			var mesh = new Mesh();

			float n = size * .5f;
			var vertices = new Vector3[4]
			{
				new Vector3(0, n, 0),
				new Vector3(-n, -n, n),
				new Vector3(n, -n, n),
				new Vector3(0, -n, -n)
			};

			var tris = new int[12]
			{
				0, 1, 2,
				0, 2, 3,
				0, 3, 1,
				1, 3, 2
			};

			var normals = new Vector3[4]
			{
				Vector3.up,
				Vector3.down,
				Vector3.down,
				Vector3.down
			};

			var uv = new Vector2[4]
			{
				new Vector2(0.5f, 1),
				new Vector2(0, 0),
				new Vector2(1, 0),
				new Vector2(0.5f, 0)
			};

			mesh.vertices = vertices;
			mesh.triangles = tris;
			mesh.normals = normals;
			mesh.uv = uv;

			return mesh;
		}

		private Matrix4x4 CalculateMatrix(int row, int col, float value, int2 gridSize, float coord00, float coord01, float coord10, float coord11)
		{
			float stepX = (coord10 - coord00) / gridSize.x;
			float stepZ = (coord01 - coord11) / gridSize.y;

			float3 pos = ImportedConfigRoot.Instance.ConfigToWorldSpace(new float3(coord00 + col * stepX + 0.5f * stepX, groundPosY, coord11 + row * stepZ + 0.5f * stepZ));

			if (posYBasedOnValue)
			{
				pos.y = groundPosY + value * posYScaler; // Raise the cube so that its base is at y=0.
			}

			float3 scale = new float3((ImportedConfigRoot.Instance.ConfigToWorldSpaceX(coord10) - ImportedConfigRoot.Instance.ConfigToWorldSpaceX(coord00)) / gridSize.x,
									 0.0f,
									 (ImportedConfigRoot.Instance.ConfigToWorldSpaceZ(coord11) - ImportedConfigRoot.Instance.ConfigToWorldSpaceZ(coord01)) / gridSize.y); // X and Z scale of each cube.

			scale.y = Math.Min(scale.x, scale.z);

			if (scaleBasedOnValue)
			{
				scale *= Mathf.Lerp(minScale, maxScale, Math.Clamp(value, 0.0f, 1.0f));
			}
			else
			{
				scale *= defaultScale;
			}

			Matrix4x4 translationMat = Matrix4x4.Translate(pos);
			Matrix4x4 scaleMat = Matrix4x4.Scale(new Vector3(scale.x, scale.y, scale.z));

			Matrix4x4 outputMatrix = translationMat * scaleMat;

			return outputMatrix;
		}

		private Mesh ColorCube(Mesh a_mesh, Color a_color)
		{
			Color[] colors = new Color[a_mesh.vertexCount];
			for (int i = 0; i < colors.Length; i++)
			{
				colors[i] = a_color;
			}
			a_mesh.colors = colors;

			return a_mesh;
		}
	}
}
