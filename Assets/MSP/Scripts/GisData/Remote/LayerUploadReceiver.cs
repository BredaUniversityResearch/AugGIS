using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
	/// S2: receives a layer upload (POST /layers) and then DISCARDS it. Nothing is
	/// stored and no layer is added to the session yet; the point is to prove the host
	/// can take large bodies without being blocked.
	///
	/// Request shape: the body is the raw payload, the metadata is in the query string.
	///  - Vector: Content-Type application/json or application/geo+json, body = GeoJSON
	///  - Raster: Content-Type image/png, body = PNG, query also needs
	///    bounds=minX,minY,maxX,maxY (in the dataset's own projection)
	///  - Query: name (required), short, category, tags (comma separated)
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
		public static async Task<JObject> ReceiveAsync(HttpContextBase a_context, long a_maxBytes)
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
				["category"] = request.RetrieveQueryValue("category"),
				["tags"] = request.RetrieveQueryValue("tags"),
			};

			Debug.Log($"[LayerUploadReceiver] {name}, trying to ingest {request.ContentLength} bytes");


			using (CountingStream body = new CountingStream(request.Data, a_maxBytes, name, request.ContentLength))
			{
				if (isRaster)
				{
					summary["kind"] = "raster";
					summary["bounds"] = new JArray(ParseBounds(request.RetrieveQueryValue("bounds")));
					await ScanPngAsync(body);
				}
				else
				{
					summary["kind"] = "vector";
					Dictionary<string, int> counts = await ScanGeoJsonAsync(body);
					Debug.Log($"[LayerUploadReceiver] '{name}' scan finished, {body.BytesRead} bytes read, {stopwatch.ElapsedMilliseconds} ms");
					JObject geometryCounts = new JObject();
					foreach (KeyValuePair<string, int> pair in counts)
						geometryCounts[pair.Key] = pair.Value;
					summary["typeCounts"] = geometryCounts;
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

		//Forward-only walk, no JObject of the whole file. "properties", "coordinates",
		//"crs" and "bbox" subtrees are skipped (still parsed, but never materialised)
		//so only geometry/feature "type" values are tallied.
		private static async Task<Dictionary<string, int>> ScanGeoJsonAsync(CountingStream a_body)
		{
			Dictionary<string, int> counts = new Dictionary<string, int>();

			try
			{
				using (StreamReader streamReader = new StreamReader(a_body, new UTF8Encoding(false), true, BufferSize, true))
				using (JsonTextReader reader = new JsonTextReader(streamReader))
				{
					if (!await reader.ReadAsync() || reader.TokenType != JsonToken.StartObject)
						throw new UploadRejectedException(400, "GeoJSON root must be an object");

					while (await reader.ReadAsync())
					{
						if (reader.TokenType != JsonToken.PropertyName)
							continue;

						string propertyName = (string)reader.Value;
						if (propertyName == "properties" || propertyName == "coordinates" || propertyName == "crs" || propertyName == "bbox")
						{
							await reader.SkipAsync();
						}
						else if (propertyName == "type")
						{
							await reader.ReadAsync();
							if (reader.TokenType == JsonToken.String)
							{
								string type = (string)reader.Value;
								counts.TryGetValue(type, out int count);
								counts[type] = count + 1;
								// Debug.Log($"{type} seen at {reader.LineNumber}:{reader.LinePosition}");
								if (type == "Feature")
								{
									a_body.ItemsSeen = count + 1;
								}
							}
						}
					}
				}
			}
			catch (JsonReaderException exception)
			{
				throw new UploadRejectedException(400, $"Invalid JSON: {exception.Message}");
			}

			if (counts.Count == 0)
				throw new UploadRejectedException(400, "Body is not GeoJSON (no 'type' found)");

			return counts;
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
