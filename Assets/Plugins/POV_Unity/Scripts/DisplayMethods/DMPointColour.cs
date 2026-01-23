using Newtonsoft.Json;
using UnityEngine;

namespace POV_Unity
{
	public class DMPointColour : AVectorDisplayMethod
	{
		public struct PointColorInstanceData
		{
			public Matrix4x4 objectToWorld;
		};

		public class PointColourRenderData : IDisplayMethodRenderData
		{
			public RenderParams renderParams;
			public PointColorInstanceData[] instanceData;
			public ADisplayMethod displayMethod;
			public Transform parentTransform;
			public int instanceCount;
		}

		[JsonConverter(typeof(JsonConverterHexColor))]
		public Color colour;
		public string sprite = null;
		public float size = 5f;
		public float sizeFixed = -1f;

		public string type_colour;
		public string type_sprite;
		public string type_size;
		public string type_sizeFixed;

		public string meta_colour;
		public string meta_sprite;
		public string meta_size;
		public string meta_sizeFixed;

		private Mesh m_mesh;

		protected override IDisplayMethodRenderData DisplayVectorLayer(VectorLayer a_layer, GameObject a_displayMethodRoot)
		{
			int instanceCount = a_layer.data.Length;
			PointColourRenderData data = new PointColourRenderData();

			Material material = AssetManager.GetMaterial("PointColourMaterial");
			
			RenderParams rp = new RenderParams(material);
			PointColorInstanceData[] instData = new PointColorInstanceData[instanceCount];

			m_mesh = CreateQuad();

			Vector4[] colors = new Vector4[instanceCount];

			string pointSprite = "";
			for (int i = 0; i < instanceCount; i++)
			{
				VectorObject point = a_layer.data[i];
				float pointWidth = GetVariable<float>("size", a_layer, point);
				float scale = pointWidth * Mathf.Abs(ImportedConfigRoot.Instance.ConfigToWorldScale) * 6000f; //20 is arbitrary rn

				Matrix4x4 matrix = Matrix4x4.TRS(point.FirstPosition, Quaternion.identity, Vector3.one * scale);
				UnityEngine.Color pointColour = GetColour("colour", a_layer, point);
				instData[i].objectToWorld = matrix;
				colors[i] = pointColour;
				pointSprite = GetVariable<string>("sprite", a_layer, point);
			}

			data.instanceCount = instanceCount;
			data.parentTransform = a_displayMethodRoot.transform;
			data.instanceData = instData;

			MaterialPropertyBlock block = new MaterialPropertyBlock();
			block.SetVectorArray("_Colors", colors);
			block.SetTexture("_MainTex", AssetManager.GetSprite(pointSprite).texture);
			rp.matProps = block;
			
			data.renderParams = rp;
			return data;
		}

		public override void Render(IDisplayMethodRenderData data)
		{
			PointColourRenderData pointColourRenderData = data as PointColourRenderData;

			if (pointColourRenderData != null)
			{
				Bounds worldBounds = new Bounds(ImportedConfigRoot.Instance.transform.position, ImportedConfigRoot.Instance.transform.lossyScale);
				pointColourRenderData.renderParams.worldBounds = worldBounds;
				pointColourRenderData.renderParams.matProps.SetMatrix("_ParentMatrix", pointColourRenderData.parentTransform.localToWorldMatrix);

				Graphics.RenderMeshInstanced( pointColourRenderData.renderParams,
											  m_mesh,
											  submeshIndex: 0,
											  pointColourRenderData.instanceData,
											  pointColourRenderData.instanceCount,
											  startInstance: 0);
			}
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
	}
}
