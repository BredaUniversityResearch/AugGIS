using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using SQLite;

namespace MSP.Scripts.GisData
{
	/// <summary>
	/// ILayerPayloadProvider backed by the local SQLite database. Every call opens
	/// its own short-lived read-only connection, since requests arrive on arbitrary
	/// thread-pool threads and sqlite-net connections must not be shared across
	/// threads.
	///
	/// Today's layers map as follows:
	///  - raster: data = coordinates/dataPath/scale/mapping, one asset "raster" (image/png)
	///  - vector: data = { objects: [...] } inline, no assets
	/// </summary>
	public class SqliteLayerPayloadProvider : ILayerPayloadProvider
	{
		public const string RasterAssetKey = "raster";

		private readonly string m_databasePath;

		public SqliteLayerPayloadProvider(string a_databasePath)
		{
			m_databasePath = a_databasePath;
		}

		public bool TryGetDatasetInfo(out JObject a_datasetInfo)
		{
			a_datasetInfo = null;

			using (SQLiteConnection connection = OpenReadOnly())
			{
				DatasetInfoRow infoRow = connection.Find<DatasetInfoRow>(DatasetInfoRow.SingletonId);
				if (infoRow == null)
					return false;

				a_datasetInfo = new JObject
				{
					["projection"] = infoRow.Projection,
					["coordinate0"] = new JArray(infoRow.Coordinate0X, infoRow.Coordinate0Y),
					["coordinate1"] = new JArray(infoRow.Coordinate1X, infoRow.Coordinate1Y),
					["metadata"] = new JObject
					{
						["dataModelHash"] = infoRow.DataModelHash,
						["dateModified"] = infoRow.DateModified,
						["editorVersion"] = infoRow.EditorVersion,
						["errors"] = infoRow.Errors
					},
					["source"] = infoRow.Source,
					["schemaVersion"] = infoRow.SchemaVersion
				};
				return true;
			}
		}

		public bool TryGetManifest(int a_layerId, out JObject a_manifest)
		{
			a_manifest = null;

			using (SQLiteConnection connection = OpenReadOnly())
			{
				LayerRow layerRow = connection.Find<LayerRow>(a_layerId);
				if (layerRow == null)
					return false;

				JObject data;
				JArray assets = new JArray();

				if (layerRow.Kind == LayerKind.Raster)
				{
					RasterLayerRow rasterRow = connection.Find<RasterLayerRow>(a_layerId);
					if (rasterRow == null)
						return false;

					data = BuildRasterData(rasterRow);

					int pngLength = connection.ExecuteScalar<int>("SELECT length(RasterPng) FROM raster_layers WHERE LayerId = ?", a_layerId);
					assets.Add(new JObject
					{
						["key"] = RasterAssetKey,
						["contentType"] = "image/png",
						["length"] = pngLength
					});
				}
				else
				{
					data = BuildVectorData(connection, a_layerId);
				}

				a_manifest = new JObject
				{
					["id"] = layerRow.Id,
					["kind"] = layerRow.Kind.ToString(),
					["sortOrder"] = layerRow.SortOrder,
					["name"] = layerRow.Name,
					["short"] = layerRow.Short,
					["tags"] = RawOrNull(layerRow.TagsJson),
					["types"] = RawOrNull(layerRow.TypesJson),
					["data"] = data,
					["assets"] = assets
				};
				return true;
			}
		}

		public bool TryGetAsset(int a_layerId, string a_assetKey, out LayerAsset a_asset)
		{
			a_asset = null;

			if (a_assetKey != RasterAssetKey)
				return false;

			using (SQLiteConnection connection = OpenReadOnly())
			{
				RasterLayerRow rasterRow = connection.Find<RasterLayerRow>(a_layerId);
				if (rasterRow?.RasterPng == null)
					return false;

				a_asset = new LayerAsset { ContentType = "image/png", Bytes = rasterRow.RasterPng };
				return true;
			}
		}

		private SQLiteConnection OpenReadOnly()
		{
			return new SQLiteConnection(m_databasePath, SQLiteOpenFlags.ReadOnly);
		}

		private static JObject BuildRasterData(RasterLayerRow a_row)
		{
			JToken scale = JValue.CreateNull();
			if (a_row.ScaleInterpolation != null)
			{
				scale = new JObject
				{
					["interpolation"] = a_row.ScaleInterpolation,
					["minValue"] = a_row.ScaleMinValue,
					["maxValue"] = a_row.ScaleMaxValue,
					["groups"] = RawOrNull(a_row.ScaleGroupsJson)
				};
			}

			return new JObject
			{
				["coordinate0"] = new JArray(a_row.Coordinate0X, a_row.Coordinate0Y),
				["coordinate1"] = new JArray(a_row.Coordinate1X, a_row.Coordinate1Y),
				["dataPath"] = a_row.DataPath,
				["scale"] = scale,
				["mapping"] = RawOrNull(a_row.MappingJson)
			};
		}

		private static JObject BuildVectorData(SQLiteConnection a_connection, int a_layerId)
		{
			List<VectorObjectRow> objectRows = a_connection.Table<VectorObjectRow>()
				.Where(a_row => a_row.LayerId == a_layerId)
				.OrderBy(a_row => a_row.ObjectIndex)
				.ToList();

			JArray objects = new JArray();
			foreach (VectorObjectRow objectRow in objectRows)
			{
				objects.Add(new JObject
				{
					["points"] = RawOrNull(objectRow.PointsJson),
					["gaps"] = RawOrNull(objectRow.GapsJson),
					["types"] = RawOrNull(objectRow.TypesJson),
					["metadata"] = RawOrNull(objectRow.MetadataJson)
				});
			}

			return new JObject { ["objects"] = objects };
		}

		//The stored columns already hold JSON text; embed it verbatim rather than
		//parsing and re-serialising it.
		private static JToken RawOrNull(string a_json)
		{
			return a_json != null ? new JRaw(a_json) : JValue.CreateNull();
		}
	}
}
