using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using POV_Unity;
using SQLite;

namespace MSP.Scripts.GisData
{
	/// <summary>
	/// Entry point for storing uploaded layers in gisdata.db. Uploads are serialised:
	/// the first holds the write lock until it is committed or disposed, later ones wait
	/// (needed so two uploads cannot pick the same layer id, and because SQLite allows a
	/// single writer anyway). Thread-safe, used from handler threads.
	/// </summary>
	public class GpkgLayerStore
	{
		private readonly string m_databasePath;
		private readonly SemaphoreSlim m_writeLock = new SemaphoreSlim(1, 1);

		public GpkgLayerStore(string a_databasePath)
		{
			m_databasePath = a_databasePath;
		}

		/// <summary>
		/// Opens a write transaction for a new vector layer and reserves its layer id.
		/// Dispose the writer (or Commit it) to release the lock; disposing without a
		/// commit rolls everything back, so a failed upload leaves nothing behind.
		/// </summary>
		public async Task<GpkgVectorLayerWriter> BeginVectorLayerAsync()
		{
			await m_writeLock.WaitAsync();
			try
			{
				return new GpkgVectorLayerWriter(m_databasePath, m_writeLock);
			}
			catch
			{
				m_writeLock.Release();
				throw;
			}
		}
	}

	/// <summary>
	/// Writes one uploaded vector layer inside a single transaction on its own
	/// connection (the host's main connection belongs to the main thread, and sqlite-net
	/// connections must not be shared across threads). Each VectorObject becomes one row
	/// of a GeoPackage feature table "layer_{id}"; the layer's name, tags and types go
	/// into the existing layers table like every other layer. Nothing is visible to
	/// readers until Commit.
	/// </summary>
	public class GpkgVectorLayerWriter : IDisposable
	{
		private readonly SQLiteConnection m_connection;
		private readonly SemaphoreSlim m_writeLock;
		private readonly int m_srsId;
		private string m_tableName;
		private string m_geometryType;
		private JObject m_lastMetadata;
		private string m_lastMetadataJson;
		private bool m_committed;
		private bool m_disposed;

		public int LayerId { get; }
		public int ObjectCount { get; private set; }

		internal GpkgVectorLayerWriter(string a_databasePath, SemaphoreSlim a_writeLock)
		{
			m_writeLock = a_writeLock;
			m_connection = new SQLiteConnection(a_databasePath, SQLiteOpenFlags.ReadWrite);

			try
			{
				m_connection.BusyTimeout = TimeSpan.FromSeconds(30);
				m_connection.BeginTransaction();

				GpkgSchema.EnsureMetadataTables(m_connection);

				DatasetInfoRow datasetInfo = m_connection.Find<DatasetInfoRow>(DatasetInfoRow.SingletonId);
				if (datasetInfo == null)
					throw new InvalidOperationException("No dataset is loaded, so there is nothing to add the layer to.");
				m_srsId = GpkgSchema.EnsureDatasetSrs(m_connection, datasetInfo.Projection);

				//Read after the first write above: this transaction holds the write lock now,
				//so no other writer can take the same id. Id equals SortOrder, like for the
				//zip-imported layers.
				int highestId = m_connection.ExecuteScalar<int>("SELECT COALESCE(MAX(Id), -1) FROM layers");
				int highestSortOrder = m_connection.ExecuteScalar<int>("SELECT COALESCE(MAX(SortOrder), -1) FROM layers");
				LayerId = Math.Max(highestId, highestSortOrder) + 1;
			}
			catch
			{
				m_connection.Dispose();
				throw;
			}
		}

		/// <summary>
		/// Creates the feature table. Called once the geometry type is known ("Polygon",
		/// "Line" or "Point", the tags GeoJsonLayerConverter reports), because the
		/// geometry column's declared type must be the geometry type name.
		/// </summary>
		public void BeginTable(string a_geometryTag)
		{
			if (m_tableName != null)
				return;

			switch (a_geometryTag)
			{
				case "Polygon": m_geometryType = GpkgGeometryCodec.TypePolygon; break;
				case "Line": m_geometryType = GpkgGeometryCodec.TypeLineString; break;
				case "Point": m_geometryType = GpkgGeometryCodec.TypePoint; break;
				default: throw new ArgumentException($"Unknown geometry tag '{a_geometryTag}'.", nameof(a_geometryTag));
			}

			m_tableName = GpkgSchema.TableName(LayerId);
			m_connection.Execute($"CREATE TABLE {m_tableName} (id INTEGER PRIMARY KEY AUTOINCREMENT, geom {m_geometryType}, type_index INTEGER NOT NULL, metadata TEXT)");
		}

		public void Add(VectorObject a_object)
		{
			if (m_tableName == null)
				throw new InvalidOperationException("BeginTable must be called before adding objects.");

			//The parts of a Multi* feature share one metadata object, serialise it once
			if (!ReferenceEquals(a_object.metadata, m_lastMetadata))
			{
				m_lastMetadata = a_object.metadata;
				m_lastMetadataJson = a_object.metadata?.ToString(Formatting.None);
			}

			byte[] geometry = GpkgGeometryCodec.Encode(a_object, m_geometryType, m_srsId);
			m_connection.Execute($"INSERT INTO {m_tableName} (geom, type_index, metadata) VALUES (?, ?, ?)", geometry, a_object.types[0], m_lastMetadataJson);
			ObjectCount++;
		}

		/// <summary>
		/// Registers the layer and commits the whole transaction. Returns the layer id.
		/// </summary>
		public int Commit(GeoJsonConversionResult a_result, string a_name, string a_short, IList<string> a_tags)
		{
			if (m_tableName == null || ObjectCount == 0)
				throw new InvalidOperationException("The layer has no geometry to store.");

			m_connection.Insert(new LayerRow
			{
				Id = LayerId,
				Kind = LayerKind.Vector,
				SortOrder = LayerId,
				Name = a_name,
				Short = a_short,
				TagsJson = JsonConvert.SerializeObject(a_tags),
				TypesJson = JsonConvert.SerializeObject(a_result.Types)
			});

			bool hasBounds = a_result.Positions > 0;
			m_connection.Execute(
				"INSERT INTO gpkg_contents (table_name, data_type, identifier, description, min_x, min_y, max_x, max_y, srs_id) VALUES (?, 'features', ?, ?, ?, ?, ?, ?, ?)",
				m_tableName, $"{a_name} ({LayerId})", a_name,
				hasBounds ? (object)a_result.MinX : null, hasBounds ? (object)a_result.MinY : null,
				hasBounds ? (object)a_result.MaxX : null, hasBounds ? (object)a_result.MaxY : null,
				m_srsId);

			m_connection.Execute(
				"INSERT INTO gpkg_geometry_columns (table_name, column_name, geometry_type_name, srs_id, z, m) VALUES (?, 'geom', ?, ?, 0, 0)",
				m_tableName, m_geometryType, m_srsId);

			m_connection.Commit();
			m_committed = true;
			return LayerId;
		}

		public void Dispose()
		{
			if (m_disposed)
				return;
			m_disposed = true;

			try
			{
				if (!m_committed && m_connection.IsInTransaction)
					m_connection.Rollback();
			}
			finally
			{
				m_connection.Dispose();
				m_writeLock.Release();
			}
		}
	}
}
