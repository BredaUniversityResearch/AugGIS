using System.Collections.Generic;
using UnityEngine;

public static class Util
{
	public static float GetSquaredDistanceToLine(Vector2 point, Vector2 lineStart, Vector2 lineEnd)
	{
		// algorithm based on first answer from http://stackoverflow.com/questions/849211/shortest-distance-between-a-point-and-a-line-segment
		float lineLengthSquared = (lineEnd - lineStart).sqrMagnitude;
		if (lineLengthSquared == 0f) { return (point - lineStart).sqrMagnitude; }
		float t = Mathf.Max(0, Mathf.Min(1, Vector2.Dot(point - lineStart, lineEnd - lineStart) / lineLengthSquared));
		Vector2 projection = lineStart + t * (lineEnd - lineStart);
		return (projection - point).sqrMagnitude;
	}

	public static bool PointInPolygon(Vector2 point, List<Vector3> polygon, List<List<Vector3>> holes)
	{
		if (!pointInPolygon(point, polygon)) { return false; }

		if (holes != null)
		{
			foreach (List<Vector3> hole in holes)
			{
				if (pointInPolygon(point, hole)) { return false; }
			}
		}

		return true;
	}

	public static bool PointCollidesWithPolygon(Vector2 point, List<Vector3> polygon, List<List<Vector3>> holes, float maxDistance)
	{
		if (pointCollidesWithLineLoop(point, polygon, maxDistance)) { return true; }
		if (holes != null)
		{
			foreach (List<Vector3> hole in holes)
			{
				if (pointCollidesWithLineLoop(point, hole, maxDistance)) { return true; }
			}
		}

		return PointInPolygon(point, polygon, holes);
	}

	public static bool PointCollidesWithPoint(Vector2 a, Vector2 b, float maxDistance)
	{
		return (b - a).sqrMagnitude < (maxDistance * maxDistance);
	}

	public static bool PointCollidesWithLineString(Vector2 point, List<Vector3> lineString, float maxDistance)
	{
		for (int i = 0; i < lineString.Count - 1; ++i)
		{
			Vector3 lineStart = lineString[i];
			lineStart = new Vector3(lineStart.x, lineStart.z, 0);
			Vector3 lineEnd = lineString[i + 1];
			lineEnd = new Vector3(lineEnd.x, lineEnd.z, 0);

			if (PointCollidesWithLine(point, lineStart, lineEnd, maxDistance)) { return true; }
		}
		return false;
	}

	private static bool pointInPolygon(Vector2 v, List<Vector3> p)
	{
		// algorithm taken from: http://codereview.stackexchange.com/questions/108857/point-inside-polygon-check
		int j = p.Count - 1;
		bool c = false;
		for (int i = 0; i < p.Count; j = i++) c ^= p[i].z > v.y ^ p[j].z > v.y && v.x < (p[j].x - p[i].x) * (v.y - p[i].z) / (p[j].z - p[i].z) + p[i].x;
		return c;
	}

	private static bool PointCollidesWithLine(Vector2 point, Vector2 lineStart, Vector2 lineEnd, float maxDistance)
	{
		return GetSquaredDistanceToLine(point, lineStart, lineEnd) < maxDistance * maxDistance;
	}

	private static bool pointCollidesWithLineLoop(Vector2 point, List<Vector3> lineLoop, float maxDistance)
	{
		for (int i = 0; i < lineLoop.Count; ++i)
		{
			Vector3 lineStart = lineLoop[i];
			lineStart = new Vector3(lineStart.x,lineStart.z,0);
			Vector3 lineEnd  = lineLoop[(i + 1) % lineLoop.Count];
			lineEnd = new Vector3(lineEnd.x, lineEnd.z, 0);
			if (PointCollidesWithLine(point, lineStart, lineEnd, maxDistance)) { return true; }
		}
		return false;
	}
	public static bool PolyMeshTryGenerateMesh(Poly2Mesh.Polygon a_poly, out Mesh a_mesh)
	{
		a_mesh = null;

		try
		{
			a_mesh = Poly2Mesh.CreateMesh(a_poly);
		}
		catch
		{
			return false;
		}

		if (a_mesh == null)
		{
			return false;
		}

		return true;
	}
}
