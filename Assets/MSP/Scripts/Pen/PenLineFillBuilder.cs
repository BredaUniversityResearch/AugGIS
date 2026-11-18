using System.Collections.Generic;
using Clipper2Lib;
using UnityEngine;

/// <summary>
/// Builds a triangulated, flat fill mesh for a closed pen line.
/// The outline is flattened onto the line-local XZ plane (y = a_height), self-intersections are
/// resolved with Clipper2 (even-odd), and each resulting shape is triangulated with Poly2Mesh.
/// </summary>
public static class PenLineFillBuilder
{
	private const int CLIPPER_PRECISION = 6;

	private static readonly List<Vector3> ms_vertices = new List<Vector3>();
	private static readonly List<int> ms_triangles = new List<int>();
	private static readonly List<Color> ms_colors = new List<Color>();

	/// <summary>
	/// Fills a_mesh with the polygon described by a_points. Returns false (and clears the mesh) if no valid polygon could be built.
	/// </summary>
	public static bool Build(IReadOnlyList<PenLinePoint> a_points, float a_height, float a_duplicateDistance, Color a_color, Mesh a_mesh)
	{
		a_mesh.Clear();
		ms_vertices.Clear();
		ms_triangles.Clear();
		ms_colors.Clear();

		int count = a_points.Count;
		if (count < 3)
			return false;

		//Drop the last point if it (nearly) duplicates the first, the outline is implicitly closed
		Vector3 first = a_points[0].Position;
		Vector3 last = a_points[count - 1].Position;
		if (new Vector2(last.x - first.x, last.z - first.z).sqrMagnitude <= a_duplicateDistance * a_duplicateDistance)
		{
			count--;
		}
		if (count < 3)
			return false;

		PathD path = new PathD(count);
		for (int i = 0; i < count; i++)
		{
			Vector3 p = a_points[i].Position;
			path.Add(new PointD(p.x, p.z));
		}

		PolyTreeD solution = new PolyTreeD();
		ClipperD clipper = new ClipperD(CLIPPER_PRECISION);
		clipper.AddSubject(new PathsD { path });
		clipper.Execute(ClipType.Union, FillRule.EvenOdd, solution);

		foreach (PolyPathD outer in solution)
		{
			AddShapeRecursive(outer, a_height);
		}

		if (ms_triangles.Count == 0)
			return false;

		for (int i = 0; i < ms_vertices.Count; i++)
		{
			ms_colors.Add(a_color);
		}

		a_mesh.SetVertices(ms_vertices);
		a_mesh.SetColors(ms_colors);
		a_mesh.SetTriangles(ms_triangles, 0);
		a_mesh.RecalculateNormals();
		a_mesh.RecalculateBounds();
		return true;
	}

	private static void AddShapeRecursive(PolyPathD a_outer, float a_height)
	{
		Poly2Mesh.Polygon polygon = new Poly2Mesh.Polygon();
		polygon.outside = ToVector3List(a_outer.Polygon, a_height);
		polygon.planeNormal = Vector3.up;

		foreach (PolyPathD hole in a_outer)
		{
			polygon.holes.Add(ToVector3List(hole.Polygon, a_height));
			foreach (PolyPathD island in hole)
			{
				AddShapeRecursive(island, a_height);
			}
		}

		if (polygon.outside.Count < 3)
			return;

		if (!Util.PolyMeshTryGenerateMesh(polygon, out Mesh shapeMesh) || shapeMesh == null)
		{
			//Retry without holes, a fill that covers a hole is better than no fill
			polygon.holes.Clear();
			polygon.rotation = Quaternion.identity;
			if (!Util.PolyMeshTryGenerateMesh(polygon, out shapeMesh) || shapeMesh == null)
				return;
		}

		int vertexOffset = ms_vertices.Count;
		ms_vertices.AddRange(shapeMesh.vertices);
		int[] triangles = shapeMesh.triangles;
		for (int i = 0; i < triangles.Length; i++)
		{
			ms_triangles.Add(triangles[i] + vertexOffset);
		}
		Object.Destroy(shapeMesh);
	}

	private static List<Vector3> ToVector3List(PathD a_path, float a_height)
	{
		List<Vector3> result = new List<Vector3>(a_path.Count);
		foreach (PointD p in a_path)
		{
			result.Add(new Vector3((float)p.x, a_height, (float)p.y));
		}
		return result;
	}
}
