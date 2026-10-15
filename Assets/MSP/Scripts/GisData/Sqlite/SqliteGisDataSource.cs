using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using POV_Unity;
using SQLite;
using UnityEngine;

namespace MSP.Scripts.GisData
{
	/// <summary>
	/// IGisDataSource implementation reading back a dataset previously written by
	/// SqliteGisDatasetWriter, reconstructing the same POV_Unity.DataConfig/ALayer
	/// types used by the rest of the renderer. GisDataset.Source is passed through
	/// as originally recorded (e.g. "msp_challenge_zip") - this reflects where the
	/// data originally came from, not that it was read via SQLite.
	/// </summary>
	public class SqliteGisDataSource : IGisDataSource
	{
		private readonly GisDatabaseService m_database;

		public SqliteGisDataSource(GisDatabaseService a_database)
		{
			m_database = a_database;
		}

		public IEnumerator LoadDataset(Action<GisDataset> a_onComplete, Action a_onFailure)
		{
			if (!m_database.IsOpen)
				throw new InvalidOperationException("GisDatabaseService must be open before loading a dataset.");

			SQLiteConnection connection = m_database.Connection;

			DatasetInfoRow infoRow = connection.Find<DatasetInfoRow>(DatasetInfoRow.SingletonId);
			if (infoRow == null)
			{
				Debug.LogError("[GisData] No dataset_info row found - the database has not been imported yet.");
				a_onFailure?.Invoke();
				yield break;
			}

			DataConfig config = new DataConfig
			{
				metadata = new ConfigMetaData
				{
					data_model_hash = infoRow.DataModelHash,
					date_modified = infoRow.DateModified,
					editor_version = infoRow.EditorVersion,
					errors = infoRow.Errors
				},
				datamodel = new DataConfigContent
				{
					projection = infoRow.Projection,
					coordinate0 = new[] { infoRow.Coordinate0X, infoRow.Coordinate0Y },
					coordinate1 = new[] { infoRow.Coordinate1X, infoRow.Coordinate1Y }
				}
			};

			List<LayerRow> layerRows = connection.Table<LayerRow>().OrderBy(a_row => a_row.SortOrder).ToList();

			List<RasterLayer> rasterLayers = new List<RasterLayer>();
			List<VectorLayer> vectorLayers = new List<VectorLayer>();
			Dictionary<string, byte[]> rasterImages = new Dictionary<string, byte[]>();

			foreach (LayerRow layerRow in layerRows)
			{
				if (layerRow.Kind == LayerKind.Raster)
				{
					RasterLayer rasterLayer = ReadRasterLayer(connection, layerRow, rasterImages);
					if (rasterLayer == null)
					{
						a_onFailure?.Invoke();
						yield break;
					}
					rasterLayers.Add(rasterLayer);
				}
				else
				{
					vectorLayers.Add(ReadVectorLayer(connection, layerRow));
				}
			}

			config.datamodel.raster_layers = rasterLayers.ToArray();
			config.datamodel.vector_layers = vectorLayers.ToArray();

			a_onComplete?.Invoke(new GisDataset
			{
				Config = config,
				RasterImages = rasterImages,
				Source = infoRow.Source
			});
		}

		private static RasterLayer ReadRasterLayer(SQLiteConnection a_connection, LayerRow a_layerRow, Dictionary<string, byte[]> a_rasterImages)
		{
			RasterLayerRow rasterRow = a_connection.Find<RasterLayerRow>(a_layerRow.Id);
			if (rasterRow == null)
			{
				Debug.LogError($"[GisData] Layer \"{a_layerRow.Name}\" is marked as Raster but has no matching raster_layers row.");
				return null;
			}

			if (rasterRow.RasterPng != null)
				a_rasterImages[RasterImageKey.Resolve(rasterRow.DataPath)] = rasterRow.RasterPng;

			return new RasterLayer
			{
				name = a_layerRow.Name,
				@short = a_layerRow.Short,
				tags = DeserializeOrNull<string[]>(a_layerRow.TagsJson),
				types = DeserializeOrNull<JObject[]>(a_layerRow.TypesJson),
				coordinate0 = new[] { rasterRow.Coordinate0X, rasterRow.Coordinate0Y },
				coordinate1 = new[] { rasterRow.Coordinate1X, rasterRow.Coordinate1Y },
				data = rasterRow.DataPath,
				mapping = DeserializeOrNull<TypeMapping[]>(rasterRow.MappingJson),
				scale = DeserializeScale(rasterRow)
			};
		}

		private static VectorLayer ReadVectorLayer(SQLiteConnection a_connection, LayerRow a_layerRow)
		{
			List<VectorObjectRow> objectRows = a_connection.Table<VectorObjectRow>()
				.Where(a_row => a_row.LayerId == a_layerRow.Id)
				.OrderBy(a_row => a_row.ObjectIndex)
				.ToList();

			VectorObject[] vectorObjects = new VectorObject[objectRows.Count];
			for (int i = 0; i < objectRows.Count; i++)
			{
				VectorObjectRow objectRow = objectRows[i];
				vectorObjects[i] = new VectorObject
				{
					points = DeserializeOrNull<float[][]>(objectRow.PointsJson),
					gaps = DeserializeOrNull<float[][][]>(objectRow.GapsJson),
					types = DeserializeOrNull<int[]>(objectRow.TypesJson),
					metadata = objectRow.MetadataJson != null ? JObject.Parse(objectRow.MetadataJson) : null
				};
			}

			return new VectorLayer
			{
				name = a_layerRow.Name,
				@short = a_layerRow.Short,
				tags = DeserializeOrNull<string[]>(a_layerRow.TagsJson),
				types = DeserializeOrNull<JObject[]>(a_layerRow.TypesJson),
				data = vectorObjects
			};
		}

		private static ScaleConfig DeserializeScale(RasterLayerRow a_row)
		{
			if (a_row.ScaleInterpolation == null)
				return null;

			return new ScaleConfig
			{
				interpolation = (ScaleConfig.DensitymapInterpolation)Enum.Parse(typeof(ScaleConfig.DensitymapInterpolation), a_row.ScaleInterpolation),
				min_value = a_row.ScaleMinValue,
				max_value = a_row.ScaleMaxValue,
				groups = DeserializeOrNull<ScaleConfigGrouping[]>(a_row.ScaleGroupsJson)
			};
		}

		private static T DeserializeOrNull<T>(string a_json) where T : class
		{
			return a_json != null ? JsonConvert.DeserializeObject<T>(a_json) : null;
		}
	}
}
