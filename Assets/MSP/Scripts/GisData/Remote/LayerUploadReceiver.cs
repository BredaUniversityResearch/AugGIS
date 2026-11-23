using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using POV_Unity;
using UnityEngine;
using WatsonWebserver.Core;

namespace MSP.Scripts.GisData
{
	/// <summary>
	/// Thrown for an upload that must be answered with a specific HTTP status.
	/// </summary>
	public class UploadRejectedException : Exception
	{
		public readonly int StatusCode;

		public UploadRejectedException(int a_statusCode, string a_message) : base(a_message)
		{
			StatusCode = a_statusCode;
		}
	}

	/// <summary>
	/// Receives a layer upload (POST /layers). S2/S3b: the upload is received and
	/// converted (vector) or checked (raster), then DISCARDED. Nothing is stored and no
	/// layer is added to the session yet; storage and the session handoff are S3c.
	///
	/// Request shape: the body is the raw payload, the metadata is in the query string.
	///  - Vector: Content-Type application/json or application/geo+json, body = GeoJSON
	///    (one geometry type per file). Converted to VectorObjects by GeoJsonLayerConverter.
	///    Query: crs=wgs84 (default, reprojected into the dataset projection) or
	///    crs=dataset (already in the dataset projection, passed through, not verified;
	///    the projection is available from GET /dataset), typeProperty=<feature property
	///    whose distinct values become the layer's types>, colour=#rrggbb
	///  - Raster: Content-Type image/png, body = PNG, query also needs
	///    bounds=minX,minY,maxX,maxY (in the dataset's own projection)
	///  - Common query: name (required), short, tags (comma separated; they decide the
	///    layer's category and display methods through the display method config, the
	///    geometry tag Polygon/Line/Point is added automatically for vector layers)
	/// A Content-Length is required (chunked uploads are answered with 411 for now).
	///
	/// Everything runs on the handler's thread-pool thread and the body is read as a
	/// stream (GeoJSON is walked with a forward-only reader), so memory use does not
	/// grow with the file size. Nothing here may touch Unity objects.
	/// </summary>
	public static class LayerUploadReceiver
	{
		public const long DefaultMaxBytes = 512L * 1024 * 1024;
		private const string MaxMegabytesEnvironmentVariable = "MSPXRMaxUploadMB";
		private const int BufferSize = 81920;
		private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

		public static long ResolveMaxBytes()
		{
			string env = Utils.GetEnvironmentVariable(MaxMegabytesEnvironmentVariable);
			if (!string.IsNullOrEmpty(env) && long.TryParse(env, out long megabytes) && megabytes > 0)
				return megabytes * 1024 * 1024;
			return DefaultMaxBytes;
		}

		/// <summary>
		/// Reads and validates the upload, returns a JSON summary for the response.
		/// Throws UploadRejectedException for anything the caller did wrong.
		/// </summary>
		public static async Task<JObject> ReceiveAsync(HttpContextBase a_context, long a_maxBytes, ILayerPayloadProvider a_datasetProvider)
		{
			System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
			HttpRequestBase request = a_context.Request;

			if (request.ChunkedTransfer || request.ContentLength <= 0)
				throw new UploadRejectedException(411, "Content-Length is required (chunked uploads are not supported)");
			if (request.ContentLength > a_maxBytes)
				throw new UploadRejectedException(413, $"Upload of {request.ContentLength} bytes exceeds the limit of {a_maxBytes} bytes");

			string name = request.RetrieveQueryValue("name");
			if (string.IsNullOrWhiteSpace(name))
				throw new UploadRejectedException(400, "Query parameter 'name' is required");

			string contentType = (request.ContentType ?? string.Empty).ToLowerInvariant();
			bool isRaster = contentType.StartsWith("image/png");
			bool isVector = contentType.StartsWith("application/json") || contentType.StartsWith("application/geo+json");
			if (!isRaster && !isVector)
				throw new UploadRejectedException(415, "Content-Type must be application/json, application/geo+json or image/png");

			JObject summary = new JObject
			{
				["received"] = true,
				["stored"] = false,
				["name"] = name,
				["short"] = request.RetrieveQueryValue("short"),
			};

			//Resolved before the body is read, so a bad request is refused without receiving it
			ICoordinateConverter converter = null;
			if (isVector)
			{
				string crs = (request.RetrieveQueryValue("crs") ?? "wgs84").Trim().ToLowerInvariant();
				summary["crs"] = crs;
				converter = ResolveConverter(crs, a_datasetProvider);
			}

			Debug.Log($"[LayerUploadReceiver] {name}, trying to ingest {request.ContentLength} bytes");


			using (CountingStream body = new CountingStream(request.Data, a_maxBytes, name, request.ContentLength))
			{
				if (isRaster)
				{
					summary["kind"] = "raster";
					summary["tags"] = new JArray(ParseTags(request.RetrieveQueryValue("tags")));
					summary["bounds"] = new JArray(ParseBounds(request.RetrieveQueryValue("bounds")));
					await ScanPngAsync(body);
				}
				else
				{
					summary["kind"] = "vector";
					VectorObject firstObject = null;
					GeoJsonConversionOptions options = new GeoJsonConversionOptions
					{
						LayerName = name,
						TypeProperty = request.RetrieveQueryValue("typeProperty"),
						Colour = request.RetrieveQueryValue("colour"),
						Converter = converter,
						OnFeature = a_count => body.ItemsSeen = a_count
					};

					GeoJsonConversionResult result;
					try
					{
						//S3b: the sink only keeps the first object for the log, S3c will store them
						result = await GeoJsonLayerConverter.ConvertAsync(body, options, a_object => { if (firstObject == null) firstObject = a_object; });
					}
					catch (GeoJsonConversionException exception)
					{
						throw new UploadRejectedException(400, exception.Message);
					}

					Debug.Log($"[LayerUploadReceiver] '{name}' conversion finished, {body.BytesRead} bytes read, {stopwatch.ElapsedMilliseconds} ms");
					summary["geometry"] = result.GeometryTag;
					summary["tags"] = new JArray(BuildVectorTags(request.RetrieveQueryValue("tags"), result.GeometryTag));
					summary["features"] = result.Features;
					summary["skippedFeatures"] = result.SkippedFeatures;
					summary["objects"] = result.Objects;
					summary["positions"] = result.Positions;
					summary["types"] = new JArray(result.Types);
					if (result.Positions > 0)
						summary["bounds"] = new JArray(result.MinX, result.MinY, result.MaxX, result.MaxY);

					if (firstObject != null)
					{
						Debug.Log($"[LayerUploadReceiver] '{name}' first object: {firstObject.points.Length} points, first point ({firstObject.points[0][0]}, {firstObject.points[0][1]}), " +
							$"{(firstObject.gaps != null ? firstObject.gaps.Length + " gaps" : "no gaps")}, metadata {firstObject.metadata?.ToString(Formatting.None) ?? "none"}");
					}
				}

				//drain whatever the scan did not consume (e.g. trailing whitespace) so the
				//byte count is the real body size and the connection is left clean
				byte[] drain = new byte[BufferSize];
				while (await body.ReadAsync(drain, 0, drain.Length) > 0)
				{
					// Debug.Log($"[LayerUploadReceiver] {name} draining, read {body.BytesRead}");
				}
				
				summary["bytes"] = body.BytesRead;
			}

			summary["elapsedMs"] = stopwatch.ElapsedMilliseconds;
			Debug.Log($"[LayerUploadReceiver] Received (and discarded) {summary["kind"]} layer '{name}': {summary["bytes"]} bytes in {stopwatch.ElapsedMilliseconds} ms on thread {Environment.CurrentManagedThreadId}");
			return summary;
		}

		private static double[] ParseBounds(string a_bounds)
		{
			string[] parts = (a_bounds ?? string.Empty).Split(',');
			double[] values = new double[4];
			if (parts.Length != 4)
				throw new UploadRejectedException(400, "Raster uploads need query parameter bounds=minX,minY,maxX,maxY");

			for (int i = 0; i < 4; i++)
			{
				if (!double.TryParse(parts[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out values[i]))
					throw new UploadRejectedException(400, "Query parameter 'bounds' must be four numbers");
			}
			return values;
		}

		private static async Task ScanPngAsync(Stream a_body)
		{
			byte[] buffer = new byte[BufferSize];
			bool signatureChecked = false;
			int read;
			while ((read = await a_body.ReadAsync(buffer, 0, buffer.Length)) > 0)
			{
				if (signatureChecked)
					continue;

				//the first read is far larger than the 8 byte signature in practice
				if (read < PngSignature.Length)
					throw new UploadRejectedException(400, "Body is not a PNG");
				for (int i = 0; i < PngSignature.Length; i++)
				{
					if (buffer[i] != PngSignature[i])
						throw new UploadRejectedException(400, "Body is not a PNG");
				}
				signatureChecked = true;
			}

			if (!signatureChecked)
				throw new UploadRejectedException(400, "Body is empty");
		}

		private static ICoordinateConverter ResolveConverter(string a_crs, ILayerPayloadProvider a_datasetProvider)
		{
			if (a_crs == "dataset")
				return null;
			if (a_crs != "wgs84")
				throw new UploadRejectedException(400, "Query parameter 'crs' must be 'wgs84' (default) or 'dataset'");

			if (!a_datasetProvider.TryGetDatasetInfo(out JObject datasetInfo))
				throw new UploadRejectedException(503, "No dataset is loaded yet, so the dataset projection is unknown");

			try
			{
				return CoordinateConverterFactory.Create((string)datasetInfo["projection"]);
			}
			catch (NotSupportedException exception)
			{
				throw new UploadRejectedException(501, $"{exception.Message} Send crs=dataset with data already in the dataset projection instead.");
			}
		}

		private static List<string> ParseTags(string a_tags)
		{
			List<string> tags = new List<string>();
			foreach (string part in (a_tags ?? string.Empty).Split(','))
			{
				string tag = part.Trim();
				if (tag.Length > 0 && !tags.Exists(a_existing => string.Equals(a_existing, tag, StringComparison.OrdinalIgnoreCase)))
					tags.Add(tag);
			}
			return tags;
		}

		//The geometry tag drives how VectorLayer processes the data, so it always comes
		//from the data itself; a caller-supplied Polygon/Line/Point tag is dropped.
		private static List<string> BuildVectorTags(string a_tags, string a_geometryTag)
		{
			List<string> tags = ParseTags(a_tags);
			tags.RemoveAll(a_tag => a_tag.Equals("Polygon", StringComparison.OrdinalIgnoreCase)
				|| a_tag.Equals("Line", StringComparison.OrdinalIgnoreCase)
				|| a_tag.Equals("Point", StringComparison.OrdinalIgnoreCase));
			tags.Insert(0, a_geometryTag);
			return tags;
		}

		/// <summary>
		/// Read-only pass-through that counts bytes and refuses to read more than the
		/// limit (covers a client that sends more than its Content-Length claims).
		/// </summary>
		private class CountingStream : Stream
		{
			private const long ProgressIntervalMs = 1000;

			private readonly Stream m_inner;
			private readonly long m_maxBytes;
			private readonly string m_label;
			private readonly long m_totalBytes;
			private readonly System.Diagnostics.Stopwatch m_progressWatch = System.Diagnostics.Stopwatch.StartNew();
			private long m_lastProgressMs;

			public long BytesRead { get; private set; }

			/// <summary>
			/// Set by the scanner (e.g. features seen so far), shown in the progress log.
			/// </summary>
			public int ItemsSeen;

			public CountingStream(Stream a_inner, long a_maxBytes, string a_label, long a_totalBytes)
			{
				m_inner = a_inner;
				m_maxBytes = a_maxBytes;
				m_label = a_label;
				m_totalBytes = a_totalBytes;
			}

			public override async Task<int> ReadAsync(byte[] a_buffer, int a_offset, int a_count, CancellationToken a_token)
			{
				//Report end-of-stream after Content-Length bytes. The request stream is not
				//bounded by Watson: on a kept-alive connection a read past the body just
				//waits for more data from the socket and never returns, which hung the
				//JSON reader when it tried to read past the end of the document.
				int allowed = (int)Math.Min(a_count, m_totalBytes - BytesRead);
				if (allowed <= 0)
					return 0;

				int read = await m_inner.ReadAsync(a_buffer, a_offset, allowed, a_token);
				return Count(read);
			}

			public override int Read(byte[] a_buffer, int a_offset, int a_count)
			{
				int allowed = (int)Math.Min(a_count, m_totalBytes - BytesRead);
				if (allowed <= 0)
					return 0;

				return Count(m_inner.Read(a_buffer, a_offset, allowed));
			}

			private int Count(int a_read)
			{
				BytesRead += a_read;
				if (BytesRead > m_maxBytes)
					throw new UploadRejectedException(413, $"Upload exceeds the limit of {m_maxBytes} bytes");

				//progress line about once a second, driven by the reads themselves so it
				//keeps ticking even while the parser is inside one huge geometry
				long elapsedMs = m_progressWatch.ElapsedMilliseconds;
				if (elapsedMs - m_lastProgressMs >= ProgressIntervalMs)
				{
					m_lastProgressMs = elapsedMs;
					double percent = m_totalBytes > 0 ? 100.0 * BytesRead / m_totalBytes : 0;
					Debug.Log($"[LayerUploadReceiver] '{m_label}': {BytesRead / 1024} / {m_totalBytes / 1024} KB ({percent:0.#}%), {ItemsSeen} features so far, {elapsedMs} ms");
				}
				return a_read;
			}

			public override bool CanRead => true;
			public override bool CanSeek => false;
			public override bool CanWrite => false;
			public override long Length => throw new NotSupportedException();
			public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
			public override void Flush() { }
			public override long Seek(long a_offset, SeekOrigin a_origin) => throw new NotSupportedException();
			public override void SetLength(long a_value) => throw new NotSupportedException();
			public override void Write(byte[] a_buffer, int a_offset, int a_count) => throw new NotSupportedException();
		}
	}
}
