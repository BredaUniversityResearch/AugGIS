using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using WatsonWebserver.Core;
using WatsonWebserver.Lite;

namespace MSP.Scripts.GisData
{
	/// <summary>
	/// Host-side, read-only HTTP API for fetching individual layers:
	///   GET /dataset                     -> JSON dataset info (projection, coordinates, metadata)
	///   GET /layers/{id}                 -> JSON manifest (see ILayerPayloadProvider)
	///   GET /layers/{id}/assets/{key}    -> raw asset bytes with its Content-Type
	/// The API is identical for every layer kind; what a layer contains is the
	/// provider's and the client's business. Handlers run on thread-pool threads.
	/// No authentication: intended for the local network session only.
	/// </summary>
	public class GisHttpServer : IDisposable
	{
		public const ushort DefaultPort = 50124;
		private const string PortEnvironmentVariable = "MSPXRHttpPort";

		private readonly ILayerPayloadProvider m_provider;
		private readonly ushort m_port;
		private WebserverLite m_server;

		public bool IsRunning => m_server != null && m_server.IsListening;

		public GisHttpServer(ILayerPayloadProvider a_provider, ushort a_port)
		{
			m_provider = a_provider;
			m_port = a_port;
		}

		public static ushort ResolvePort()
		{
			string portEnv = Utils.GetEnvironmentVariable(PortEnvironmentVariable);
			return string.IsNullOrEmpty(portEnv) ? DefaultPort : ushort.Parse(portEnv);
		}

		public void Start()
		{
			if (m_server != null)
				return;

			try
			{
				//Watson.Lite uses a TCP listener, 0.0.0.0 = listen on all interfaces
				WebserverSettings settings = new WebserverSettings("0.0.0.0", m_port);
				m_server = new WebserverLite(settings, HandleNotFound);
				m_server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/dataset", HandleDatasetInfo);
				m_server.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, "/layers/{id}", HandleManifest);
				m_server.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, "/layers/{id}/assets/{key}", HandleAsset);
				m_server.Start();

				Debug.Log($"[GisHttpServer] Listening on port {m_port}");
			}
			catch (Exception exception)
			{
				Debug.LogError($"[GisHttpServer] Failed to start on port {m_port}: {exception}");
				m_server?.Dispose();
				m_server = null;
			}
		}

		public void Stop()
		{
			if (m_server == null)
				return;

			try
			{
				m_server.Stop();
				m_server.Dispose();
			}
			catch (Exception exception)
			{
				Debug.LogError($"[GisHttpServer] Error while stopping: {exception}");
			}
			m_server = null;
		}

		public void Dispose()
		{
			Stop();
		}

		private async Task HandleDatasetInfo(HttpContextBase a_context)
		{
			try
			{
				if (!m_provider.TryGetDatasetInfo(out JObject datasetInfo))
				{
					await SendText(a_context, 404, "Dataset not found");
					return;
				}

				a_context.Response.StatusCode = 200;
				a_context.Response.ContentType = "application/json";
				await a_context.Response.Send(datasetInfo.ToString(Formatting.None));
			}
			catch (Exception exception)
			{
				await SendError(a_context, exception);
			}
		}

		private async Task HandleManifest(HttpContextBase a_context)
		{
			try
			{
				if (!TryParseLayerId(a_context, out int layerId))
				{
					await SendText(a_context, 400, "Invalid layer id");
					return;
				}

				if (!m_provider.TryGetManifest(layerId, out JObject manifest))
				{
					await SendText(a_context, 404, "Layer not found");
					return;
				}

				a_context.Response.StatusCode = 200;
				a_context.Response.ContentType = "application/json";
				await a_context.Response.Send(manifest.ToString(Formatting.None));
			}
			catch (Exception exception)
			{
				await SendError(a_context, exception);
			}
		}

		private async Task HandleAsset(HttpContextBase a_context)
		{
			try
			{
				if (!TryParseLayerId(a_context, out int layerId))
				{
					await SendText(a_context, 400, "Invalid layer id");
					return;
				}

				string assetKey = a_context.Request.Url.Parameters["key"];
				if (string.IsNullOrEmpty(assetKey) || !m_provider.TryGetAsset(layerId, assetKey, out LayerAsset asset))
				{
					await SendText(a_context, 404, "Asset not found");
					return;
				}

				a_context.Response.StatusCode = 200;
				a_context.Response.ContentType = asset.ContentType;
				await a_context.Response.Send(asset.Bytes);
			}
			catch (Exception exception)
			{
				await SendError(a_context, exception);
			}
		}

		private static Task HandleNotFound(HttpContextBase a_context)
		{
			return SendText(a_context, 404, "Not found");
		}

		private static bool TryParseLayerId(HttpContextBase a_context, out int a_layerId)
		{
			return int.TryParse(a_context.Request.Url.Parameters["id"], out a_layerId);
		}

		private static Task SendText(HttpContextBase a_context, int a_statusCode, string a_text)
		{
			a_context.Response.StatusCode = a_statusCode;
			a_context.Response.ContentType = "text/plain";
			return a_context.Response.Send(a_text);
		}

		private static Task SendError(HttpContextBase a_context, Exception a_exception)
		{
			Debug.LogError($"[GisHttpServer] Request {a_context.Request.Url.RawWithQuery} failed: {a_exception}");
			return SendText(a_context, 500, "Internal server error");
		}
	}
}
