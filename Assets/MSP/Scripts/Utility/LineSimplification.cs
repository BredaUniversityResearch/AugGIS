using System.Collections.Generic;
using UnityEngine;

public static class LineSimplification
{
	/// <summary>
	/// Shortest distance from a_point to the segment a_lineStart -> a_lineEnd.
	/// Falls back to point distance when the segment is degenerate.
	/// </summary>
	public static float PerpendicularDistance(Vector3 a_point, Vector3 a_lineStart, Vector3 a_lineEnd)
	{
		Vector3 segment = a_lineEnd - a_lineStart;
		float lengthSqr = segment.sqrMagnitude;
		if (lengthSqr < 1e-12f)
		{
			return Vector3.Distance(a_point, a_lineStart);
		}

		float t = Mathf.Clamp01(Vector3.Dot(a_point - a_lineStart, segment) / lengthSqr);
		Vector3 projection = a_lineStart + segment * t;
		return Vector3.Distance(a_point, projection);
	}

	/// <summary>
	/// Iterative Douglas-Peucker simplification. Always keeps the first and last point.
	/// </summary>
	public static List<Vector3> DouglasPeucker(IReadOnlyList<Vector3> a_points, float a_tolerance)
	{
		List<Vector3> result = new List<Vector3>();
		int count = a_points.Count;
		if (count <= 2)
		{
			for (int i = 0; i < count; i++)
			{
				result.Add(a_points[i]);
			}
			return result;
		}

		bool[] keep = new bool[count];
		keep[0] = true;
		keep[count - 1] = true;

		Stack<(int start, int end)> ranges = new Stack<(int start, int end)>();
		ranges.Push((0, count - 1));

		while (ranges.Count > 0)
		{
			(int start, int end) = ranges.Pop();
			if (end - start < 2)
				continue;

			float maxDistance = 0f;
			int maxIndex = -1;
			for (int i = start + 1; i < end; i++)
			{
				float distance = PerpendicularDistance(a_points[i], a_points[start], a_points[end]);
				if (distance > maxDistance)
				{
					maxDistance = distance;
					maxIndex = i;
				}
			}

			if (maxIndex >= 0 && maxDistance > a_tolerance)
			{
				keep[maxIndex] = true;
				ranges.Push((start, maxIndex));
				ranges.Push((maxIndex, end));
			}
		}

		for (int i = 0; i < count; i++)
		{
			if (keep[i])
			{
				result.Add(a_points[i]);
			}
		}
		return result;
	}
}
