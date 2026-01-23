using System.Collections.Generic;
using UnityEngine;

namespace POV_Unity
{
	public class VectorShape
	{
		public List<Vector3> m_points;
		public List<List<Vector3>> m_holes;

		public float GetPolygonArea()
		{
			float area = GetPolygonArea(m_points);

			if (m_holes != null)
			{
				foreach (List<Vector3> hole in m_holes)
					area -= GetPolygonArea(hole);
			}
			return area;
		}

		float GetPolygonArea(List<Vector3> a_polygon)
		{
			float area = 0;
			for (int i = 0; i < a_polygon.Count; ++i)
			{
				int j = (i + 1) % a_polygon.Count;
				area += a_polygon[i].x * a_polygon[j].z - a_polygon[i].z * a_polygon[j].x;
			}
			return Mathf.Abs(area * 0.5f);
		}
	}
}
