using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;


namespace POV_Unity
{
	public class DMLineColour : AVectorDisplayMethod
	{
		private const float k_lineScaleFactor = 1000f;

		public enum LineDirection { None, Forward, Backward, Both }

		[JsonConverter(typeof(JsonConverterHexColor))]
		public Color colour;
		public float width = 1f;
		public float widthFixed = -1f;
		public LineDirection direction = LineDirection.None;
		public bool closed = false;

		public string type_colour;
		public string type_width;
		public string type_widthFixed;
		public string type_direction;

		public string meta_colour;
		public string meta_width;
		public string meta_widthFixed;
		public string meta_direction;

		protected override IDisplayMethodRenderData DisplayVectorLayer(VectorLayer a_layer, GameObject a_displayMethodRoot)
		{
			foreach (VectorObject line in a_layer.data)
			{
				if (line.Shapes.Count == 0)
					continue;

				Color lineColour = GetColour("colour", a_layer, line);
				float lineWidth = GetVariable<float>("width", a_layer, line);
				float lineFixedWidth = GetVariable<float>("widthFixed", a_layer, line);
				LineDirection lineDirection = GetVariable<LineDirection>("direction", a_layer, line);

				foreach (VectorShape shape in line.Shapes)
				{
					if (shape.m_points.Count < 2)
						continue;

					CreateLineObject(a_displayMethodRoot, lineColour, lineWidth, lineFixedWidth, lineDirection, shape.m_points);
					if (shape.m_holes != null)
					{
						foreach (var hole in shape.m_holes)
						{
							if (hole.Count < 2)
								continue;
							CreateLineObject(a_displayMethodRoot, lineColour, lineWidth, lineFixedWidth, lineDirection, hole);
						}
					}
				}
			}

			// Group all child line_renderers
			LineRenderer[] lineRenderers = a_displayMethodRoot.GetComponentsInChildren<LineRenderer>();

			CombineInstance[] instances = new CombineInstance[lineRenderers.Length];

			for (int i = 0; i < lineRenderers.Length; i++)
			{
				Mesh lineMesh = new Mesh();
				lineRenderers[i].BakeMesh(lineMesh);

				instances[i] = new CombineInstance
				{
					mesh = lineMesh,
					transform = Matrix4x4.identity * Matrix4x4.Scale(1 / k_lineScaleFactor * Vector3.one),
				};

				lineRenderers[i].gameObject.SetActive(false);
			}
			Mesh combinedMesh = new Mesh();
			combinedMesh.indexFormat = IndexFormat.UInt32;
			combinedMesh.CombineMeshes(instances);

			//gameObject.GetComponent<MeshFilter>().sharedMesh = combinedMesh;
			a_displayMethodRoot.AddComponent<MeshFilter>().mesh = combinedMesh;

			//gameObject.SetActive(true);

			MeshRenderer renderer = a_displayMethodRoot.AddComponent<MeshRenderer>();
			renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

			Material mat = POV_Unity.AssetManager.GetLineMaterial();
			mat.renderQueue = (int)RenderQueue.Geometry;
			//a_layer.RegisterLayerMaterial(mat);
			renderer.sharedMaterial = mat;

			//HARDCODED ROTATION
			a_displayMethodRoot.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
			return null;
		}

		void CreateLineObject(GameObject a_displayMethodRoot, Color a_lineColour, float a_lineWidth, float a_lineFixedWidth, LineDirection a_lineDirection, List<Vector3> a_line)
		{
			GameObject lineObject = new GameObject("Line");
			lineObject.transform.SetParent(a_displayMethodRoot.transform, false);
			LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
			lineRenderer.numCapVertices = 5;
			lineRenderer.numCornerVertices = 5;
			lineRenderer.positionCount = a_line.Count;
			lineRenderer.material = POV_Unity.AssetManager.GetLineMaterial();

			for (int i = 0; i < a_line.Count; i++)
			{
				lineRenderer.SetPosition(i, new Vector3(a_line[i].x * k_lineScaleFactor, a_line[i].z * k_lineScaleFactor, a_line[i].y * k_lineScaleFactor));
			}

			lineRenderer.startColor = a_lineColour;
			lineRenderer.endColor = a_lineColour;

			if (a_lineFixedWidth <= 0)
			{
				lineRenderer.widthCurve = AnimationCurve.Linear(0, a_lineWidth * 0.001f * k_lineScaleFactor, 1, a_lineWidth * 0.001f * k_lineScaleFactor);
			}
			else
			{
				lineRenderer.widthCurve = AnimationCurve.Linear(0, a_lineFixedWidth * ImportedConfigRoot.Instance.ConfigToWorldScale * 1000 * k_lineScaleFactor, 1, a_lineFixedWidth * ImportedConfigRoot.Instance.ConfigToWorldScale * 1000 * k_lineScaleFactor);
			}

			lineRenderer.loop = closed;
			lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			lineRenderer.alignment = LineAlignment.TransformZ;
			lineRenderer.useWorldSpace = false;

			lineRenderer.Simplify(0.001f * k_lineScaleFactor);
			lineObject.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
			lineObject.transform.localScale = Vector3.one;
		}
	}
}
