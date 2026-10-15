using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using POV_Unity;
using SQLite;

namespace MSP.Scripts.GisData
{
	/// <summary>
	/// Persists a GisDataset into the Phase A schema via GisDatabaseService. Replaces
	/// any previously stored dataset entirely - this is not an incremental/diffing
	/// importer, matching the "single current dataset, overwritten on each import"
	/// approach agreed for Phase A.
	///
	/// The dataset handed in must be freshly parsed (e.g. straight from
	/// MspChallengeZipDataSource), not one that has already been passed to
	/// ImportedConfigRoot.Initialise(): VectorObject.ProcessDataPolygon/Line/Point()
	/// null out points/gaps once a layer has been rendered, replacing them with
	/// runtime-only Shapes. Writing that mutated state would silently persist empty
	/// geometry, so this is treated as a hard error instead (see WriteVectorLayer).
	/// </summary>
	public class SqliteGisDatasetWriter
	{
		private readonly GisDatabaseService m_database;

		public SqliteGisDatasetWriter(GisDatabaseService a_database)
		{
			m_database = a_database;
		}

		public void Write(GisDataset a_dataset)
		{
			if (!m_database.IsOpen)
				throw new InvalidOperationException("GisDatabaseService must be open before writing a dataset.");

			SQLiteConnection connection = m_database.Connection;

			connection.RunInTransaction(() =>
			{
				ClearExistingDataset(connection);
				WriteDatasetInfo(connection, a_dataset);

				int sortOrder = 0;

				foreach (RasterLayer rasterLayer in a_dataset.Config.datamodel.raster_layers)
					WriteRasterLayer(connection, rasterLayer, a_dataset.RasterImages, sortOrder++);

				foreach (VectorLayer vectorLayer in a_dataset.Config.datamodel.vector_layers)
					WriteVectorLayer(connection, vectorLayer, sortOrder++);
			});
		}

		private static void ClearExistingDataset(SQLiteConnection a_connection)
		{
			a_connection.DeleteAll<VectorObjectRow>();
			a_connection.DeleteAll<RasterLayerRow>();
			a_connection.DeleteAll<LayerRow>();
			a_connection.DeleteAll<DatasetInfoRow>();
		}

		private static void WriteDatasetInfo(SQLiteConnection a_connection, GisDataset a_dataset)
		{
			ConfigMetaData metadata = a_dataset.Config.metadata;
			DataConfigContent datamodel = a_dataset.Config.datamodel;

			DatasetInfoRow row = new DatasetInfoRow
			{
				DataModelHash = metadata?.data_model_hash,
				DateModified = metadata?.date_modified,
				EditorVersion = metadata?.editor_version,
				Errors = metadata?.errors ?? 0,
				Projection = datamodel.projection,
				Coordinate0X = datamodel.coordinate0[0],
				Coordinate0Y = datamodel.coordinate0[1],
				Coordinate1X = datamodel.coordinate1[0],
				Coordinate1Y = datamodel.coordinate1[1],
				Source = a_dataset.Source,
				SchemaVersion = GisDatabaseService.SchemaVersion,
				ImportedAt = DateTime.UtcNow.ToString("o")
			};

			a_connection.InsertOrReplace(row);
		}

		private static void WriteRasterLayer(SQLiteConnection a_connection, RasterLayer a_layer, IReadOnlyDictionary<string, byte[]> a_rasterImages, int a_sortOrder)
		{
			LayerRow layerRow = BuildLayerRow(a_layer.name, a_layer.@short, a_layer.tags, a_layer.types, LayerKind.Raster, a_sortOrder);
			a_connection.Insert(layerRow);

			string imageKey = RasterImageKey.Resolve(a_layer.data);
			if (a_rasterImages == null || !a_rasterImages.TryGetValue(imageKey, out byte[] rasterPng))
			{
				throw new InvalidOperationException(
					$"No raster image bytes found for layer \"{a_layer.name}\" (data = \"{a_layer.data}\", " +
					$"resolved key = \"{imageKey}\"). GisDataset.RasterImages did not contain a matching entry.");
			}

			RasterLayerRow rasterRow = new RasterLayerRow
			{
				LayerId = layerRow.Id,
				Coordinate0X = a_layer.coordinate0[0],
				Coordinate0Y = a_layer.coordinate0[1],
				Coordinate1X = a_layer.coordinate1[0],
				Coordinate1Y = a_layer.coordinate1[1],
				DataPath = a_layer.data,
				RasterPng = rasterPng,
				ScaleInterpolation = a_layer.scale?.interpolation.ToString(),
				ScaleMinValue = a_layer.scale?.min_value ?? 0f,
				ScaleMaxValue = a_layer.scale?.max_value ?? 0f,
				ScaleGroupsJson = a_layer.scale?.groups != null ? JsonConvert.SerializeObject(a_layer.scale.groups) : null,
				MappingJson = a_layer.mapping != null ? JsonConvert.SerializeObject(a_layer.mapping) : null
			};
			a_connection.Insert(rasterRow);
		}

		private static void WriteVectorLayer(SQLiteConnection a_connection, VectorLayer a_layer, int a_sortOrder)
		{
			LayerRow layerRow = BuildLayerRow(a_layer.name, a_layer.@short, a_layer.tags, a_layer.types, LayerKind.Vector, a_sortOrder);
			a_connection.Insert(layerRow);

			if (a_layer.data == null)
				return;

			for (int i = 0; i < a_layer.data.Length; i++)
			{
				VectorObject vectorObject = a_layer.data[i];

				if (vectorObject.points == null)
				{
					throw new InvalidOperationException(
						$"VectorObject #{i} on layer \"{a_layer.name}\" has no points. This happens once " +
						"VectorObject.ProcessDataPolygon/Line/Point() has run (typically as part of " +
						"ImportedConfigRoot.Initialise()), which nulls out points/gaps in favour of runtime-only " +
						"Shapes. SqliteGisDatasetWriter must only ever be given a freshly-parsed DataConfig, before " +
						"it is handed to ImportedConfigRoot - the database must hold pristine source data, never a " +
						"client/host's locally-rendered state.");
				}

				VectorObjectRow objectRow = new VectorObjectRow
				{
					LayerId = layerRow.Id,
					ObjectIndex = i,
					PointsJson = JsonConvert.SerializeObject(vectorObject.points),
					GapsJson = vectorObject.gaps != null ? JsonConvert.SerializeObject(vectorObject.gaps) : null,
					TypesJson = JsonConvert.SerializeObject(vectorObject.types),
					MetadataJson = vectorObject.metadata?.ToString(Formatting.None)
				};
				a_connection.Insert(objectRow);
			}
		}

		private static LayerRow BuildLayerRow(string a_name, string a_short, string[] a_tags, JObject[] a_types, LayerKind a_kind, int a_sortOrder)
		{
			return new LayerRow
			{
				Kind = a_kind,
				SortOrder = a_sortOrder,
				Name = a_name,
				Short = a_short,
				TagsJson = JsonConvert.SerializeObject(a_tags),
				TypesJson = JsonConvert.SerializeObject(a_types)
			};
		}
	}
}
