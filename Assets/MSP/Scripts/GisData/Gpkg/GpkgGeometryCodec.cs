using System;
using System.IO;
using POV_Unity;

namespace MSP.Scripts.GisData
{
	/// <summary>
	/// Encodes/decodes a single VectorObject geometry as a GeoPackage geometry BLOB
	/// (StandardGeoPackageBinary, GeoPackage spec 1.4, clause 2.1.3): an 8 byte header
	/// (magic "GP", version 0, flags, srs_id), an XY envelope, then OGC WKB. All
	/// multi-byte values are little endian. Only 2D Point, LineString and Polygon are
	/// produced: a Multi* feature is stored as one row per part.
	///
	/// Two conversions between the runtime and the stored form:
	///  - polygon rings: WKB rings are closed (first position repeated at the end),
	///    runtime rings are open, so the closing position is added on write and
	///    dropped on read
	///  - coordinates are float at runtime and double in WKB (floats convert exactly)
	/// The WKB layout itself comes from OGC 06-103r4, which the GeoPackage spec only
	/// references; it is not part of the spec text.
	/// </summary>
	public static class GpkgGeometryCodec
	{
		public const string TypePoint = "POINT";
		public const string TypeLineString = "LINESTRING";
		public const string TypePolygon = "POLYGON";

		//flags byte: reserved(2) X(1) Y(1) E(3) B(1). Standard type, not empty,
		//envelope [minx, maxx, miny, maxy] (E = 1), little endian (B = 1)
		private const byte FlagsEnvelopeLittleEndian = (1 << 1) | 1;

		private const uint WkbPoint = 1;
		private const uint WkbLineString = 2;
		private const uint WkbPolygon = 3;

		public static byte[] Encode(VectorObject a_object, string a_geometryTypeName, int a_srsId)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream))
			{
				WriteHeader(writer, a_object.points, a_srsId);

				writer.Write((byte)1); //WKB byte order: little endian
				switch (a_geometryTypeName)
				{
					case TypePoint:
						writer.Write(WkbPoint);
						WritePosition(writer, a_object.points[0]);
						break;
					case TypeLineString:
						writer.Write(WkbLineString);
						WritePositions(writer, a_object.points, false);
						break;
					case TypePolygon:
						int gapCount = a_object.gaps != null ? a_object.gaps.Length : 0;
						writer.Write(WkbPolygon);
						writer.Write((uint)(1 + gapCount));
						WriteRing(writer, a_object.points);
						for (int i = 0; i < gapCount; i++)
							WriteRing(writer, a_object.gaps[i]);
						break;
					default:
						throw new ArgumentException($"Unsupported geometry type '{a_geometryTypeName}'.", nameof(a_geometryTypeName));
				}

				return stream.ToArray();
			}
		}

		/// <summary>
		/// Reads a blob written by Encode. gaps is null for points and lines, and an
		/// empty array for polygons without holes (what the renderer expects).
		/// </summary>
		public static void Decode(byte[] a_blob, out float[][] a_points, out float[][][] a_gaps)
		{
			using (MemoryStream stream = new MemoryStream(a_blob))
			using (BinaryReader reader = new BinaryReader(stream))
			{
				if (reader.ReadByte() != (byte)'G' || reader.ReadByte() != (byte)'P')
					throw new InvalidDataException("Not a GeoPackage geometry blob (missing 'GP' magic).");
				if (reader.ReadByte() != 0)
					throw new InvalidDataException("Unsupported GeoPackage geometry version.");

				byte flags = reader.ReadByte();
				if ((flags & 1) == 0)
					throw new InvalidDataException("Big endian GeoPackage headers are not supported.");
				if ((flags & (1 << 4)) != 0)
					throw new InvalidDataException("Empty geometries are not supported.");

				reader.ReadInt32(); //srs_id
				int envelopeBytes = EnvelopeSize((flags >> 1) & 0x7);
				stream.Seek(envelopeBytes, SeekOrigin.Current);

				if (reader.ReadByte() != 1)
					throw new InvalidDataException("Big endian WKB is not supported.");

				uint type = reader.ReadUInt32();
				switch (type)
				{
					case WkbPoint:
						a_points = new[] { ReadPosition(reader) };
						a_gaps = null;
						break;
					case WkbLineString:
						a_points = ReadPositions(reader, (int)reader.ReadUInt32(), false);
						a_gaps = null;
						break;
					case WkbPolygon:
						int ringCount = (int)reader.ReadUInt32();
						a_points = ReadRing(reader);
						a_gaps = new float[ringCount - 1][][];
						for (int i = 1; i < ringCount; i++)
							a_gaps[i - 1] = ReadRing(reader);
						break;
					default:
						throw new InvalidDataException($"Unsupported WKB geometry type {type}.");
				}
			}
		}

		private static void WriteHeader(BinaryWriter a_writer, float[][] a_points, int a_srsId)
		{
			double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
			foreach (float[] position in a_points)
			{
				if (position[0] < minX) minX = position[0];
				if (position[0] > maxX) maxX = position[0];
				if (position[1] < minY) minY = position[1];
				if (position[1] > maxY) maxY = position[1];
			}

			a_writer.Write((byte)'G');
			a_writer.Write((byte)'P');
			a_writer.Write((byte)0);
			a_writer.Write(FlagsEnvelopeLittleEndian);
			a_writer.Write(a_srsId);
			//envelope order is [minx, maxx, miny, maxy]; holes lie inside the outer ring
			a_writer.Write(minX);
			a_writer.Write(maxX);
			a_writer.Write(minY);
			a_writer.Write(maxY);
		}

		private static int EnvelopeSize(int a_indicator)
		{
			switch (a_indicator)
			{
				case 0: return 0;
				case 1: return 32;
				case 2:
				case 3: return 48;
				case 4: return 64;
				default: throw new InvalidDataException($"Invalid envelope indicator {a_indicator}.");
			}
		}

		private static void WritePosition(BinaryWriter a_writer, float[] a_position)
		{
			a_writer.Write((double)a_position[0]);
			a_writer.Write((double)a_position[1]);
		}

		private static void WritePositions(BinaryWriter a_writer, float[][] a_positions, bool a_closeRing)
		{
			a_writer.Write((uint)(a_positions.Length + (a_closeRing ? 1 : 0)));
			foreach (float[] position in a_positions)
				WritePosition(a_writer, position);
			if (a_closeRing)
				WritePosition(a_writer, a_positions[0]);
		}

		private static void WriteRing(BinaryWriter a_writer, float[][] a_ring)
		{
			WritePositions(a_writer, a_ring, true);
		}

		private static float[] ReadPosition(BinaryReader a_reader)
		{
			return new[] { (float)a_reader.ReadDouble(), (float)a_reader.ReadDouble() };
		}

		private static float[][] ReadPositions(BinaryReader a_reader, int a_count, bool a_dropClosingPosition)
		{
			float[][] positions = new float[a_count][];
			for (int i = 0; i < a_count; i++)
				positions[i] = ReadPosition(a_reader);

			if (a_dropClosingPosition && a_count > 1 && positions[0][0] == positions[a_count - 1][0] && positions[0][1] == positions[a_count - 1][1])
				Array.Resize(ref positions, a_count - 1);
			return positions;
		}

		private static float[][] ReadRing(BinaryReader a_reader)
		{
			return ReadPositions(a_reader, (int)a_reader.ReadUInt32(), true);
		}
	}
}
