using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Clipper2Lib;
using UnityEngine;

namespace POV_Unity
{
	public class VectorObject
	{
		public float[][] points;
		public float[][][] gaps;
		public int[] types;
		public JObject metadata;

		List<VectorShape> m_shapes;
		public List<VectorShape> Shapes => m_shapes;
		public Vector3 FirstPosition => m_shapes[0].m_points[0];
		//List<List<Vector3>> m_clippedPoints;
		//public List<List<Vector3>> ClippedPoints => m_clippedPoints;

		public void ProcessDataPolygon()
		{
			//Convert to paths
			PathsD paths = new PathsD(1);
			PathD path = new PathD(points.Length);
			for (int i = 0; i < points.Length; i++)
			{
				path.Add(new PointD(points[i][0], points[i][1]));
			}
			//TODO: might have to add first point again
			paths.Add(path);
			for (int j = 0; j < gaps.Length; j++)
			{
				path = new PathD();
				for (int i = 0; i < gaps[j].Length; i++)
				{
					path.Add(new PointD(gaps[j][i][0], gaps[j][i][1]));
				}
				paths.Add(path);
			}

			//Clip to area
			PolyTreeD solution = new PolyTreeD();
			ClipperD clip = new ClipperD();
			clip.AddSubject(paths);
			clip.AddClip(ImportedConfigRoot.Instance.AreaPath);
			clip.Execute(ClipType.Intersection, FillRule.EvenOdd, solution);

			//Apply result
			m_shapes = new List<VectorShape>(solution.Count);
			foreach (PolyPathD newPath in solution)
			{
				AddPolygonRecursive(newPath);
			}

			////Apply result
			//m_clippedPoints = new List<List<Vector3>>();
			//foreach (PolyPathD newPath in solution)
			//{
			//	List<Vector3> points = new List<Vector3>(newPath.Polygon.Count);
			//	foreach (PointD p in newPath.Polygon)
			//	{
			//		points.Add(ImportedConfigRoot.Instance.ConfigToWorldSpace(new Vector3((float)p.x, 0f, (float)p.y)));
			//	}
			//	//TODO: Add holes back as holes
			//	m_clippedPoints.Add(points);
			//}

			//PathsD solution = Clipper.RectClip(ImportedConfigRoot.Instance.AreaRect, paths);
			//m_clippedPoints = new List<List<Vector3>>();
			//if (solution.Count > 0)
			//	Debug.Log("Found poly in area");
			//foreach (PathD newPath in solution)
			//{
			//	List<Vector3> points = new List<Vector3>(newPath.Count);
			//	foreach (PointD p in newPath)
			//	{
			//		points.Add(ImportedConfigRoot.Instance.ConfigToWorldSpace(new Vector3((float)p.x, 0f, (float)p.y)));
			//	}
			//	//TODO: Add holes back as holes
			//	m_clippedPoints.Add(points);
			//}

			points = null;
			gaps = null;
		}

		void AddPolygonRecursive(PolyPathD a_path)
		{
			List<Vector3> points = new List<Vector3>(a_path.Polygon.Count);
			List<List<Vector3>> holes = new List<List<Vector3>>(a_path.Count);
			foreach (PointD p in a_path.Polygon)
			{
				points.Add(ImportedConfigRoot.Instance.ConfigToWorldSpace(new Vector3((float)p.x, 0f, (float)p.y)));
			}
			foreach (PolyPathD hole in a_path)
			{
				List<Vector3> holePoints = new List<Vector3>(hole.Polygon.Count);
				foreach (PointD p in hole.Polygon)
				{
					holePoints.Add(ImportedConfigRoot.Instance.ConfigToWorldSpace(new Vector3((float)p.x, 0f, (float)p.y)));
				}
				holes.Add(holePoints);
				foreach(PolyPathD subPoly in hole)
				{
					AddPolygonRecursive(subPoly);
				}
			}
			m_shapes.Add(new VectorShape() { m_holes = holes, m_points = points });
		}

		public void ProcessDataLine()
		{
			PathD path = new PathD(points.Length);
			for (int i = 0; i < points.Length; i++)
			{
				path.Add(new PointD(points[i][0], points[i][1]));
			}

			PathsD solution = Clipper.RectClipLines(ImportedConfigRoot.Instance.AreaRect, path);
			m_shapes = new List<VectorShape>(solution.Count);
			foreach (PathD newPath in solution)
			{
				List<Vector3> points = new List<Vector3>(newPath.Count);
				foreach (PointD p in newPath)
				{
					points.Add(ImportedConfigRoot.Instance.ConfigToWorldSpace(new Vector3((float)p.x, 0f, (float)p.y)));
				}
				m_shapes.Add(new VectorShape() { m_points = points });
			}

			points = null;
		}

		public void ProcessDataPoint()
		{
			//Point doesnt have to be clipped, this should already be done by the server
			m_shapes = new List<VectorShape>() { 
				new VectorShape() { 
					m_points = new List<Vector3>() { 
						ImportedConfigRoot.Instance.ConfigToWorldSpaceXZ(points[0]) 
					} 
				} 
			};
			points = null;
		}
	}
}
