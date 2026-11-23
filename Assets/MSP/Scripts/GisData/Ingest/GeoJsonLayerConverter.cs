using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using POV_Unity;

namespace MSP.Scripts.GisData
{
	/// <summary>
	/// Thrown when a GeoJSON file cannot become a clean single-purpose layer. The
	/// message is meant for the caller of the upload API.
	/// </summary>
	public class GeoJsonConversionException : Exception
	{
		public GeoJsonConversionException(string a_message) : base(a_message) { }
	}

	public class GeoJsonConversionOptions
	{
		/// <summary>Name used for the single type when no TypeProperty is given.</summary>
		public string LayerName;

		/// <summary>
		/// Feature property whose distinct values become the layer's type catalogue.
		/// Null = one type for the whole layer.
		/// </summary>
		public string TypeProperty;

		/// <summary>Overrides the default palette colour of every type (e.g. "#ffb30f").</summary>
		public string Colour;

		/// <summary>
		/// Converts WGS84 lon/lat into the dataset projection. Null means the
		/// coordinates are already in the dataset projection and are passed through.
		/// </summary>
		public ICoordinateConverter Converter;

		/// <summary>Called with the number of features seen so far (progress reporting).</summary>
		public Action<int> OnFeature;
	}

	public class GeoJsonConversionResult
	{
		/// <summary>"Polygon", "Line" or "Point": the layer tag the renderer picks its processing from.</summary>
		public string GeometryTag;
		public int Features;
		/// <summary>Features without (or with empty) geometry, which are left out.</summary>
		public int SkippedFeatures;
		/// <summary>VectorObjects produced (a Multi* geometry yields one per part).</summary>
		public int Objects;
		public long Positions;
		/// <summary>The layer's type catalogue (name, colour), in first-seen order.</summary>
		public List<JObject> Types = new List<JObject>();
		/// <summary>Bounds of the converted coordinates, in the dataset projection.</summary>
		public double MinX = double.MaxValue, MinY = double.MaxValue, MaxX = double.MinValue, MaxY = double.MinValue;
	}

	/// <summary>
	/// Streams a GeoJSON file into runtime VectorObjects, one feature at a time, and
	/// hands each object to a sink so the caller decides what to do with it (S3c will
	/// write rows to the database instead of holding them all).
	///
	/// Mapping (matches what VectorLayer/VectorObject expect):
	///  - Point / MultiPoint -> layer tag "Point", one object per point
	///  - LineString / MultiLineString -> "Line", one object per line
	///  - Polygon / MultiPolygon -> "Polygon", one object per polygon: points = outer
	///    ring without the repeated closing position, gaps = the holes (empty array
	///    when there are none, the renderer iterates it)
	///  - Feature "properties" become the object's metadata; the type index points into
	///    the type catalogue
	/// A layer holds one geometry type only: mixing them, or GeometryCollection, is
	/// rejected. Pure maths and JSON, no Unity API, so safe on thread-pool threads.
	/// </summary>
	public class GeoJsonLayerConverter
	{
		private const string UnknownTypeName = "Unknown";
		private const int BufferSize = 81920;
		private static readonly string[] DefaultPalette =
		{
			"#e6194b", "#3cb44b", "#4363d8", "#f58231", "#911eb4", "#46f0f0", "#f032e6", "#bcf60c"
		};

		private readonly GeoJsonConversionOptions m_options;
		private readonly Action<VectorObject> m_sink;
		private readonly GeoJsonConversionResult m_result = new GeoJsonConversionResult();
		private readonly Dictionary<string, int> m_typeIndices = new Dictionary<string, int>();

		private GeoJsonLayerConverter(GeoJsonConversionOptions a_options, Action<VectorObject> a_sink)
		{
			m_options = a_options;
			m_sink = a_sink;
		}

		public static Task<GeoJsonConversionResult> ConvertAsync(Stream a_stream, GeoJsonConversionOptions a_options, Action<VectorObject> a_sink)
		{
			return new GeoJsonLayerConverter(a_options, a_sink).RunAsync(a_stream);
		}

		private async Task<GeoJsonConversionResult> RunAsync(Stream a_stream)
		{
			try
			{
				using (StreamReader streamReader = new StreamReader(a_stream, new UTF8Encoding(false), true, BufferSize, true))
				using (JsonTextReader reader = new JsonTextReader(streamReader))
				{
					await ReadRootAsync(reader);
				}
			}
			catch (JsonReaderException exception)
			{
				throw new GeoJsonConversionException($"Invalid JSON: {exception.Message}");
			}

			if (m_result.GeometryTag == null)
				throw new GeoJsonConversionException("The file contains no usable geometry.");

			return m_result;
		}

		private async Task ReadRootAsync(JsonTextReader a_reader)
		{
			if (!await a_reader.ReadAsync() || a_reader.TokenType != JsonToken.StartObject)
				throw new GeoJsonConversionException("GeoJSON root must be an object.");

			string rootType = null;
			bool sawFeatures = false;
			//Parts of a root that is a single Feature or bare geometry (key order is free)
			JObject rootParts = new JObject();

			while (await a_reader.ReadAsync())
			{
				if (a_reader.TokenType == JsonToken.EndObject)
					break;
				if (a_reader.TokenType != JsonToken.PropertyName)
					continue;

				string propertyName = (string)a_reader.Value;
				switch (propertyName)
				{
					case "type":
						await a_reader.ReadAsync();
						rootType = a_reader.Value as string;
						break;
					case "features":
						sawFeatures = true;
						await ReadFeaturesAsync(a_reader);
						break;
					case "geometry":
					case "properties":
					case "coordinates":
						await a_reader.ReadAsync();
						rootParts[propertyName] = await JToken.ReadFromAsync(a_reader);
						break;
					default:
						//crs, bbox, name, ... are not used
						await a_reader.SkipAsync();
						break;
				}
			}

			if (sawFeatures)
				return;

			switch (rootType)
			{
				case "Feature":
					ProcessFeature(rootParts, 0);
					break;
				case "Point":
				case "MultiPoint":
				case "LineString":
				case "MultiLineString":
				case "Polygon":
				case "MultiPolygon":
					rootParts["type"] = rootType;
					ProcessFeature(new JObject { ["geometry"] = rootParts }, 0);
					break;
				case "GeometryCollection":
					throw new GeoJsonConversionException("GeometryCollection is not supported, send one geometry type per layer.");
				case "FeatureCollection":
					throw new GeoJsonConversionException("The FeatureCollection has no 'features' array.");
				default:
					throw new GeoJsonConversionException($"Root 'type' must be FeatureCollection, Feature or a geometry type, got '{rootType}'.");
			}
		}

		private async Task ReadFeaturesAsync(JsonTextReader a_reader)
		{
			await a_reader.ReadAsync();
			if (a_reader.TokenType != JsonToken.StartArray)
				throw new GeoJsonConversionException("'features' must be an array.");

			int index = 0;
			while (await a_reader.ReadAsync() && a_reader.TokenType != JsonToken.EndArray)
			{
				if (a_reader.TokenType == JsonToken.Comment)
					continue;
				if (a_reader.TokenType != JsonToken.StartObject)
					throw new GeoJsonConversionException($"Feature #{index} is not an object.");

				//One feature at a time: memory is bounded by the largest single feature
				JObject feature = await JObject.LoadAsync(a_reader);
				ProcessFeature(feature, index);
				index++;
			}
		}

		private void ProcessFeature(JObject a_feature, int a_index)
		{
			m_result.Features++;
			m_options.OnFeature?.Invoke(m_result.Features);

			JObject geometry = a_feature["geometry"] as JObject;
			if (geometry == null)
			{
				m_result.SkippedFeatures++;
				return;
			}

			string type = (string)geometry["type"];
			string tag = TagFor(type, a_index);
			if (m_result.GeometryTag == null)
				m_result.GeometryTag = tag;
			else if (m_result.GeometryTag != tag)
				throw new GeoJsonConversionException($"Feature #{a_index} is a {type}, but the layer already holds {m_result.GeometryTag} geometry. Send one geometry type per layer.");

			JArray coordinates = geometry["coordinates"] as JArray;
			if (coordinates == null || coordinates.Count == 0)
			{
				m_result.SkippedFeatures++;
				return;
			}

			JObject properties = a_feature["properties"] as JObject;
			int typeIndex = ResolveTypeIndex(properties);

			switch (type)
			{
				case "Point":
					EmitPoint(coordinates, typeIndex, properties, a_index);
					break;
				case "MultiPoint":
					foreach (JToken point in coordinates)
						EmitPoint(point as JArray, typeIndex, properties, a_index);
					break;
				case "LineString":
					EmitLine(coordinates, typeIndex, properties, a_index);
					break;
				case "MultiLineString":
					foreach (JToken line in coordinates)
						EmitLine(line as JArray, typeIndex, properties, a_index);
					break;
				case "Polygon":
					EmitPolygon(coordinates, typeIndex, properties, a_index);
					break;
				case "MultiPolygon":
					foreach (JToken polygon in coordinates)
						EmitPolygon(polygon as JArray, typeIndex, properties, a_index);
					break;
			}
		}

		private static string TagFor(string a_type, int a_index)
		{
			switch (a_type)
			{
				case "Point":
				case "MultiPoint":
					return "Point";
				case "LineString":
				case "MultiLineString":
					return "Line";
				case "Polygon":
				case "MultiPolygon":
					return "Polygon";
				case "GeometryCollection":
					throw new GeoJsonConversionException($"Feature #{a_index}: GeometryCollection is not supported, send one geometry type per layer.");
				default:
					throw new GeoJsonConversionException($"Feature #{a_index}: unknown geometry type '{a_type}'.");
			}
		}

		private void EmitPoint(JArray a_position, int a_typeIndex, JObject a_properties, int a_featureIndex)
		{
			Emit(new[] { ToPosition(a_position, a_featureIndex) }, null, a_typeIndex, a_properties);
		}

		private void EmitLine(JArray a_positions, int a_typeIndex, JObject a_properties, int a_featureIndex)
		{
			if (a_positions == null || a_positions.Count == 0)
				return;
			if (a_positions.Count < 2)
				throw new GeoJsonConversionException($"Feature #{a_featureIndex}: a line needs at least 2 positions.");

			Emit(ToPositions(a_positions, a_positions.Count, a_featureIndex), null, a_typeIndex, a_properties);
		}

		private void EmitPolygon(JArray a_rings, int a_typeIndex, JObject a_properties, int a_featureIndex)
		{
			if (a_rings == null || a_rings.Count == 0)
				return;

			float[][] outer = ToRing(a_rings[0] as JArray, a_featureIndex);
			float[][][] gaps = new float[a_rings.Count - 1][][];
			for (int i = 1; i < a_rings.Count; i++)
				gaps[i - 1] = ToRing(a_rings[i] as JArray, a_featureIndex);

			Emit(outer, gaps, a_typeIndex, a_properties);
		}

		private void Emit(float[][] a_points, float[][][] a_gaps, int a_typeIndex, JObject a_properties)
		{
			m_result.Objects++;
			m_sink(new VectorObject
			{
				points = a_points,
				gaps = a_gaps,
				types = new[] { a_typeIndex },
				metadata = a_properties
			});
		}

		//GeoJSON rings repeat the first position at the end; MSP data does not
		private float[][] ToRing(JArray a_ring, int a_featureIndex)
		{
			if (a_ring == null)
				throw new GeoJsonConversionException($"Feature #{a_featureIndex}: a polygon ring is not an array.");

			int count = a_ring.Count;
			if (count >= 2 && PositionsEqual(a_ring[0], a_ring[count - 1]))
				count--;
			if (count < 3)
				throw new GeoJsonConversionException($"Feature #{a_featureIndex}: a polygon ring needs at least 3 distinct positions.");

			return ToPositions(a_ring, count, a_featureIndex);
		}

		private float[][] ToPositions(JArray a_positions, int a_count, int a_featureIndex)
		{
			float[][] result = new float[a_count][];
			for (int i = 0; i < a_count; i++)
				result[i] = ToPosition(a_positions[i] as JArray, a_featureIndex);
			return result;
		}

		private static bool PositionsEqual(JToken a_first, JToken a_second)
		{
			JArray first = a_first as JArray;
			JArray second = a_second as JArray;
			return first != null && second != null && first.Count >= 2 && second.Count >= 2
				&& (double)first[0] == (double)second[0] && (double)first[1] == (double)second[1];
		}

		private float[] ToPosition(JArray a_position, int a_featureIndex)
		{
			if (a_position == null || a_position.Count < 2
				|| (a_position[0].Type != JTokenType.Float && a_position[0].Type != JTokenType.Integer)
				|| (a_position[1].Type != JTokenType.Float && a_position[1].Type != JTokenType.Integer))
				throw new GeoJsonConversionException($"Feature #{a_featureIndex}: a position must be [x, y] numbers.");

			double x = (double)a_position[0];
			double y = (double)a_position[1];

			if (m_options.Converter != null)
			{
				if (x < -180.0 || x > 180.0 || y < -90.0 || y > 90.0)
					throw new GeoJsonConversionException($"Feature #{a_featureIndex}: position ({x}, {y}) is not a valid WGS84 lon/lat. If the data is already in the dataset projection, send crs=dataset.");

				try
				{
					m_options.Converter.FromWgs84(x, y, out double projectedX, out double projectedY);
					x = projectedX;
					y = projectedY;
				}
				catch (ArgumentOutOfRangeException exception)
				{
					throw new GeoJsonConversionException($"Feature #{a_featureIndex}: {exception.Message}");
				}
			}

			m_result.Positions++;
			if (x < m_result.MinX) m_result.MinX = x;
			if (x > m_result.MaxX) m_result.MaxX = x;
			if (y < m_result.MinY) m_result.MinY = y;
			if (y > m_result.MaxY) m_result.MaxY = y;

			//float matches the stored VectorObject format (about 0.25 m resolution at MSP coordinates)
			return new[] { (float)x, (float)y };
		}

		private int ResolveTypeIndex(JObject a_properties)
		{
			string name;
			if (string.IsNullOrEmpty(m_options.TypeProperty))
			{
				name = m_options.LayerName;
			}
			else
			{
				JToken value = a_properties?[m_options.TypeProperty];
				if (value == null || value.Type == JTokenType.Null)
					name = UnknownTypeName;
				else
					name = value.Type == JTokenType.String ? (string)value : value.ToString(Formatting.None);
			}

			if (m_typeIndices.TryGetValue(name, out int index))
				return index;

			index = m_result.Types.Count;
			m_typeIndices[name] = index;
			m_result.Types.Add(new JObject
			{
				["name"] = name,
				["colour"] = m_options.Colour ?? DefaultPalette[index % DefaultPalette.Length]
			});
			return index;
		}
	}
}
