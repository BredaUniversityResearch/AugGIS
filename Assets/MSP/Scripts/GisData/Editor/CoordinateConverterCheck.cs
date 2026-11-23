#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace MSP.Scripts.GisData.Editor
{
	/// <summary>
	/// Editor-only sanity check for the WGS84 -> dataset projection converter, run from
	/// the Tools menu. Uses the MSP dataset projection and logs PASS/FAIL per check.
	/// There is no independent PROJ available here, so the checks are an exact anchor
	/// (the projection origin maps to the false origin), coarse real-world distances,
	/// a forward/inverse round trip, and that unsupported projection strings are refused.
	/// </summary>
	public static class CoordinateConverterCheck
	{
		private const string MspProjection = "+proj=laea +lat_0=52 +lon_0=10 +x_0=4321000 +y_0=3210000 +ellps=GRS80 +units=m +no_defs";

		private const string DownloadedProjection = "+proj=laea +lat_0=52 +lon_0=10 +x_0=4321000 +y_0=3210000 +ellps=GRS80 +towgs84=0,0,0,0,0,0,0 +units=m +no_defs +type=crs";

		[MenuItem("Tools/GIS Data/Check Coordinate Converter")]
		private static void Run()
		{
			int failures = 0;
			ICoordinateConverter converter = CoordinateConverterFactory.Create(MspProjection);

			//1. The origin must land exactly on the false origin
			converter.FromWgs84(10.0, 52.0, out double x, out double y);
			failures += Report("origin -> false origin", Math.Abs(x - 4321000.0) < 1e-6 && Math.Abs(y - 3210000.0) < 1e-6, $"got ({x}, {y})");

			//2. One degree north / east of the origin is roughly 111.3 km / 68.7 km
			converter.FromWgs84(10.0, 53.0, out double northX, out double northY);
			double dy = northY - 3210000.0;
			failures += Report("1 deg north ~ 111.3 km", Math.Abs(dy - 111277.0) < 500.0 && Math.Abs(northX - 4321000.0) < 1e-3, $"dy = {dy:F1} m, dx = {northX - 4321000.0:F3} m");

			converter.FromWgs84(11.0, 52.0, out double eastX, out double eastY);
			double dx = eastX - 4321000.0;
			failures += Report("1 deg east ~ 68.7 km", Math.Abs(dx - 68676.0) < 500.0, $"dx = {dx:F1} m, dy = {eastY - 3210000.0:F1} m");

			//3. Round trip over a grid around the dataset (North Sea / western Europe)
			double worstMetres = 0.0;
			for (double lon = 0.0; lon <= 20.0; lon += 2.5)
			{
				for (double lat = 48.0; lat <= 60.0; lat += 2.0)
				{
					converter.FromWgs84(lon, lat, out double px, out double py);
					converter.ToWgs84(px, py, out double backLon, out double backLat);
					double errorMetres = Math.Max(Math.Abs(backLon - lon) * 71000.0, Math.Abs(backLat - lat) * 111000.0);
					worstMetres = Math.Max(worstMetres, errorMetres);
				}
			}
			failures += Report("round trip", worstMetres < 0.05, $"worst error {worstMetres:F5} m");

			//4. Info only: where the sample dataset's coordinate0 lands on the globe
			converter.ToWgs84(4048536.0, 3470232.0, out double areaLon, out double areaLat);
			Debug.Log($"[CoordinateConverterCheck] INFO sample dataset coordinate0 (4048536, 3470232) = lon {areaLon:F4}, lat {areaLat:F4} (expected somewhere in the North Sea, roughly 5.8E 54.3N)");

			//4b. The string of freshly downloaded MSP data (EPSG:3035 style) must give the same result
			ICoordinateConverter downloadedConverter = CoordinateConverterFactory.Create(DownloadedProjection);
			downloadedConverter.FromWgs84(5.0, 54.0, out double downloadedX, out double downloadedY);
			converter.FromWgs84(5.0, 54.0, out double plainX, out double plainY);
			failures += Report("downloaded projection string (+towgs84=0..., +type=crs)", downloadedX == plainX && downloadedY == plainY, $"({downloadedX}, {downloadedY}) vs ({plainX}, {plainY})");

			//5. Unsupported strings must be refused, not guessed
			failures += ReportRefused("refuses utm", "+proj=utm +zone=31 +ellps=GRS80 +units=m");
			failures += ReportRefused("refuses a real +towgs84 shift", "+proj=laea +lat_0=52 +lon_0=10 +ellps=GRS80 +towgs84=1,2,3,0,0,0,0");
			failures += ReportRefused("refuses other ellipsoid", "+proj=laea +lat_0=52 +lon_0=10 +ellps=bessel");
			failures += ReportRefused("refuses units=ft", "+proj=laea +lat_0=52 +lon_0=10 +units=ft");
			failures += ReportRefused("refuses polar aspect", "+proj=laea +lat_0=90 +lon_0=0");
			failures += ReportRefused("refuses empty string", "");

			Debug.Log(failures == 0
				? "[CoordinateConverterCheck] ALL CHECKS PASSED"
				: $"[CoordinateConverterCheck] {failures} CHECK(S) FAILED");
		}

		private static int Report(string a_name, bool a_passed, string a_detail)
		{
			string message = $"[CoordinateConverterCheck] {(a_passed ? "PASS" : "FAIL")} {a_name} ({a_detail})";
			if (a_passed)
				Debug.Log(message);
			else
				Debug.LogError(message);
			return a_passed ? 0 : 1;
		}

		private static int ReportRefused(string a_name, string a_projString)
		{
			try
			{
				CoordinateConverterFactory.Create(a_projString);
				return Report(a_name, false, "no exception was thrown");
			}
			catch (NotSupportedException exception)
			{
				return Report(a_name, true, exception.Message);
			}
		}
	}
}
#endif
