using System;
using System.Collections.Generic;
using System.Globalization;

namespace MSP.Scripts.GisData
{
	/// <summary>
	/// Builds an ICoordinateConverter from a dataset's PROJ.4 string. Deliberately
	/// minimal: only "+proj=laea" on the GRS80/WGS84 ellipsoid in metres is supported.
	/// Anything else throws NotSupportedException with a message naming what was
	/// refused, rather than silently producing coordinates in the wrong place.
	/// </summary>
	public static class CoordinateConverterFactory
	{
		private const double Grs80Flattening = 1.0 / 298.257222101;
		private const double Wgs84Flattening = 1.0 / 298.257223563;
		private const double SemiMajorAxis = 6378137.0;

		//Parameters that change the result in ways this implementation ignores.
		private static readonly string[] UnsupportedParameters =
		{
			"datum", "nadgrids", "geoidgrids", "pm", "axis", "to_meter", "a", "b", "rf", "f", "es", "R"
		};

		public static ICoordinateConverter Create(string a_projString)
		{
			if (string.IsNullOrWhiteSpace(a_projString))
				throw new NotSupportedException("The dataset has no projection string.");

			Dictionary<string, string> parameters = Parse(a_projString);

			parameters.TryGetValue("proj", out string projection);
			if (projection != "laea")
				throw new NotSupportedException($"Unsupported projection '{projection}' in \"{a_projString}\". Only +proj=laea is supported.");

			foreach (string unsupported in UnsupportedParameters)
			{
				if (parameters.ContainsKey(unsupported))
					throw new NotSupportedException($"Unsupported parameter +{unsupported} in \"{a_projString}\".");
			}

			//Strings for the standard European LAEA (EPSG:3035) carry +towgs84=0,0,0,0,0,0,0,
			//a datum shift of nothing, so it is accepted. A real shift is refused.
			if (parameters.TryGetValue("towgs84", out string datumShift))
				RequireZeroDatumShift(datumShift, a_projString);

			if (parameters.TryGetValue("units", out string units) && units != "m")
				throw new NotSupportedException($"Unsupported units '{units}' in \"{a_projString}\". Only metres are supported.");

			parameters.TryGetValue("ellps", out string ellipsoid);
			double flattening;
			if (ellipsoid == null || ellipsoid == "GRS80")
				flattening = Grs80Flattening;
			else if (ellipsoid == "WGS84")
				flattening = Wgs84Flattening;
			else
				throw new NotSupportedException($"Unsupported ellipsoid '{ellipsoid}' in \"{a_projString}\". Only GRS80 and WGS84 are supported.");

			return new LaeaCoordinateConverter(
				SemiMajorAxis,
				flattening,
				GetNumber(parameters, "lat_0", a_projString),
				GetNumber(parameters, "lon_0", a_projString),
				GetNumber(parameters, "x_0", a_projString),
				GetNumber(parameters, "y_0", a_projString));
		}

		private static void RequireZeroDatumShift(string a_shift, string a_projString)
		{
			string[] values = a_shift.Split(',');
			bool valid = values.Length == 3 || values.Length == 7;
			foreach (string value in values)
			{
				if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) || number != 0.0)
					valid = false;
			}

			if (!valid)
				throw new NotSupportedException($"Unsupported parameter +towgs84={a_shift} in \"{a_projString}\". Only a zero datum shift is supported.");
		}

		private static Dictionary<string, string> Parse(string a_projString)
		{
			Dictionary<string, string> parameters = new Dictionary<string, string>();
			foreach (string token in a_projString.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
			{
				string trimmed = token.TrimStart('+');
				int equals = trimmed.IndexOf('=');
				if (equals < 0)
					parameters[trimmed] = string.Empty; //flag such as +no_defs
				else
					parameters[trimmed.Substring(0, equals)] = trimmed.Substring(equals + 1);
			}
			return parameters;
		}

		//Missing numbers default to 0, like PROJ
		private static double GetNumber(Dictionary<string, string> a_parameters, string a_key, string a_projString)
		{
			if (!a_parameters.TryGetValue(a_key, out string text))
				return 0.0;

			if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
				throw new NotSupportedException($"Parameter +{a_key}={text} is not a number in \"{a_projString}\".");
			return value;
		}
	}
}
