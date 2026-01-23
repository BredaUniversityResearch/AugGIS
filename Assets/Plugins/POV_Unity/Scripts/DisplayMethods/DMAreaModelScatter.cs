using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;

namespace POV_Unity
{
	public class DMAreaModelScatter: AVectorDisplayMethod
	{
		public string model;
		public int amount;
		public float spacing;
		public float scale;
		public bool seabottom = false;
		public float offset = 0f;
		public string material = null;

		public string type_model;
		public string type_amount;
		public string type_spacing;
		public string type_seabottom;
		public string type_offset;
		public string type_material;

		public string meta_model;
		public string meta_amount;
		public string meta_spacing;
		public string meta_seabottom;
		public string meta_offset;
		public string meta_material;

		protected override IDisplayMethodRenderData DisplayVectorLayer(VectorLayer a_layer, GameObject a_displayMethodRoot)
		{
			foreach (VectorObject poly in a_layer.data)
			{
				CreatePolygon(a_layer, poly, a_displayMethodRoot.transform);
			}
			return null;
		}

		public void CreatePolygon(VectorLayer a_layer, VectorObject a_polygon, Transform a_parent)
		{
			if (a_polygon.Shapes.Count == 0)
				return; 

			//Determine bounding box, surface area and longest edge
			float xMin = float.PositiveInfinity, zMin = float.PositiveInfinity;
			float xMax = float.NegativeInfinity, zMax = float.NegativeInfinity;
			float surfaceArea = 0f;

			Vector3 placementOrigin = Vector3.zero;

			Matrix4x4 rotationMatrix = Matrix4x4.identity;

			Vector3 longestEdgeDir = Vector3.zero;
			Vector3 longestEdgePerpDir = Vector3.zero;

			int validShapeCount = 0;
			foreach (VectorShape shape in a_polygon.Shapes)
			{
				surfaceArea += shape.GetPolygonArea();

				// Find centroid of biggest triangle in shape to use it as placement origin
				{
					// Using unity tools, create a mesh for the shape
					Poly2Mesh.Polygon poly = new Poly2Mesh.Polygon();
					poly.outside = shape.m_points;
					poly.holes = shape.m_holes;

					if (!Util.PolyMeshTryGenerateMesh(poly, out Mesh polyMesh))
					{
						Debug.LogWarningFormat("Shape data is incorrect skipping shape mesh generation for layer: {0} with metadata: {1}", a_layer.name, a_polygon.metadata.ToString(Formatting.Indented));
						continue;
					}

					validShapeCount++;


					int[] triangles = polyMesh.triangles;
					float biggestPolygonArea = 0f;

					// Cycle through triangles of the generated mesh and find the one with the biggest area
					for (int i = 0; i < triangles.Length; i += 3)
					{
						Vector3 p0 = polyMesh.vertices[triangles[i]];
						Vector3 p1 = polyMesh.vertices[triangles[i + 1]];
						Vector3 p2 = polyMesh.vertices[triangles[i + 2]];

						float triangleDoubledArea = Vector3.Cross(p1 - p0, p2 - p0).magnitude;
						if (triangleDoubledArea > biggestPolygonArea)
						{
							biggestPolygonArea = triangleDoubledArea;
							placementOrigin = (p0 + p1 + p2) / 3f;
						}
					}

					Vector3 longestEdgeVector = Vector3.zero;

					// Find the longest edge in the shape
					for (int i = 0; i < shape.m_points.Count; ++i)
					{
						Vector3 p1 = shape.m_points[i];
						Vector3 p2 = shape.m_points[(i + 1) % shape.m_points.Count];
						if (Vector3.Distance(p1, p2) > longestEdgeVector.magnitude)
						{
							longestEdgeVector = p2 - p1;
						}
					}

					longestEdgeDir = longestEdgeVector.normalized;
					longestEdgePerpDir = new Vector3(-longestEdgeDir.z, 0f, longestEdgeDir.x);
				}

				// Make 2D rotation matrix to align longest edge with X axis
				rotationMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.FromToRotation(longestEdgeDir, Vector3.right), Vector3.one);

				foreach (Vector3 v in shape.m_points)
				{
					Vector3 rotatedV = rotationMatrix.MultiplyPoint3x4(v);

					// Use rotated vertices to determine bounding box
					if (rotatedV.x < xMin)
						xMin = rotatedV.x;
					if (rotatedV.x > xMax)
						xMax = rotatedV.x;
					if (rotatedV.z < zMin)
						zMin = rotatedV.z;
					if (rotatedV.z > zMax)
						zMax = rotatedV.z;
				}
			}

			if(validShapeCount == 0)
			{
				Debug.LogWarningFormat("Layer: {0} Contains no valid shape data. skipping", a_layer.name);
				return;
			}

			GameObject areaMesh = new GameObject("Area_" + a_parent.childCount);
			areaMesh.transform.SetParent(a_parent, false);

			//Determine spacing based on amount of objects and surface area
			int numberObjects = GetVariable<int>("amount", a_layer, a_polygon);
			float objectSpacing = GetVariable<float>("spacing", a_layer, a_polygon);
			float modelScale = GetVariable<float>("scale", a_layer, a_polygon);
			if (objectSpacing > 0f)
			{
				objectSpacing *= ImportedConfigRoot.Instance.ConfigToWorldScale;
			}
			else
			{
				if (numberObjects > 0)
				{
					objectSpacing = Mathf.Sqrt(surfaceArea / (numberObjects * 1.03f));
				}
				else
				{
					objectSpacing = Mathf.Sqrt(surfaceArea / 100f);
				}
			}
			
			if (numberObjects <= 0)
				numberObjects = int.MaxValue;

			int gridX = (int)((xMax - xMin) / objectSpacing);
			int gridZ = (int)((zMax - zMin) / objectSpacing);

			int alpha = (int)((rotationMatrix.MultiplyPoint3x4(placementOrigin).x - xMin) / objectSpacing);
			int beta = (int)((rotationMatrix.MultiplyPoint3x4(placementOrigin).z - zMin) / objectSpacing);

			int iMin = -alpha;
			int jMin = -beta;
			int iMax = gridX - alpha;
			int jMax = gridZ - beta;

			Vector3 longestEdgeSpacing = longestEdgeDir * objectSpacing;
			Vector3 longestEdgePerpSpacing = longestEdgePerpDir * objectSpacing;

			for (int i = iMin; i <= iMax; i++)
			{
				for (int j = jMin; j <= jMax; j++)
				{
					Vector3 point = placementOrigin + longestEdgeSpacing * i + longestEdgePerpSpacing * j;
					if (PointInPolygon(point, a_polygon))
					{
						GameObject go = new GameObject("ScatteredObject " + i.ToString() + "_" + j.ToString());
						go.transform.SetParent(areaMesh.transform, false);
						go.AddComponent<ModelObject>().Initialise(a_layer, a_polygon, this);
						go.transform.localPosition = point;
						go.transform.localScale = Vector3.one * modelScale;
						go.transform.localRotation = Quaternion.LookRotation(longestEdgePerpDir, Vector3.up);
						go.transform.localRotation *= Quaternion.Euler(0f, 90f, 0f);
					}
				}
			}
		}

		float GetDistToCornerAlong(Vector3 a_point, Vector3 a_corner, Vector3 a_normal)
		{
			if (Vector3.SqrMagnitude(a_corner - a_point) < 0.001f)
				return 0f;

			Vector3 projection = Vector3.Project(a_corner - a_point, a_normal);
			return Vector3.Distance(a_point + projection, a_corner);
		}

		private bool PointInPolygon(Vector3 a_point, VectorObject a_polygon)
		{
			foreach(VectorShape shape in a_polygon.Shapes)
			{
				if (PointInPolygon(a_point, shape.m_points, shape.m_holes))
					return true;
			}
			return false;
		}

		private bool PointInPolygon(Vector3 a_point, List<Vector3> a_polygon, List<List<Vector3>> a_holes)
		{
			if (!PointInPolygon(a_point, a_polygon))
				return false;

			if(a_holes != null)
			{
				foreach(var hole in a_holes)
				{
					if (PointInPolygon(a_point, hole))
						return false;
				}
			}

			return true;
		}

		private bool PointInPolygon(Vector3 a_point, List<Vector3> a_polygon)
		{
			// algorithm taken from: http://codereview.stackexchange.com/questions/108857/point-inside-polygon-check
			int j = a_polygon.Count - 1;
			bool c = false;
			for (int i = 0; i < a_polygon.Count; j = i++) 
				c ^= a_polygon[i].z > a_point.z ^ a_polygon[j].z > a_point.z && a_point.x < (a_polygon[j].x - a_polygon[i].x) * (a_point.z - a_polygon[i].z) / (a_polygon[j].z - a_polygon[i].z) + a_polygon[i].x;
			return c;
		}

		private bool PointCollidesWithLineLoop(Vector3 a_point, List<Vector3> a_lineLoop, float a_maxDistance)
		{
			for (int i = 0; i < a_lineLoop.Count; ++i)
			{
				if (PointCollidesWithLine(a_point, a_lineLoop[i], a_lineLoop[(i + 1) % a_lineLoop.Count], a_maxDistance)) { return true; }
			}
			return false;
		}

		private bool PointCollidesWithLine(Vector3 a_point, Vector3 a_lineStart, Vector3 a_lineEnd, float a_maxDistance)
		{
			return GetSquaredDistanceToLine(a_point, a_lineStart, a_lineEnd) < a_maxDistance * a_maxDistance;
		}

		public float GetSquaredDistanceToLine(Vector3 a_point, Vector3 a_lineStart, Vector3 a_lineEnd)
		{
			// algorithm based on first answer from http://stackoverflow.com/questions/849211/shortest-distance-between-a-point-and-a-line-segment
			float lineLengthSquared = (a_lineEnd - a_lineStart).sqrMagnitude;
			if (lineLengthSquared == 0f) { return (a_point - a_lineStart).sqrMagnitude; }
			float t = Mathf.Max(0, Mathf.Min(1, Vector3.Dot(a_point - a_lineStart, a_lineEnd - a_lineStart) / lineLengthSquared));
			Vector3 projection = a_lineStart + t * (a_lineEnd - a_lineStart);
			return (projection - a_point).sqrMagnitude;
		}
	}
}
