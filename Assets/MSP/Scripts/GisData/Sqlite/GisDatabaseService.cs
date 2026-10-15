using System;
using System.IO;
using SQLite;
using UnityEngine;

namespace MSP.Scripts.GisData
{
	/// <summary>
	/// Owns the lifecycle of the local GIS SQLite database: resolving its file path,
	/// opening/closing the connection, and creating the Phase A schema (dataset_info,
	/// layers, raster_layers, vector_objects). This service does not read or write any
	/// actual GIS data itself - that is the responsibility of data source
	/// implementations built on top of it, and is not wired up yet.
	///
	/// Requires SqliteRuntimeBootstrap to have registered the SQLitePCL provider
	/// before Open() is called (handled automatically via
	/// RuntimeInitializeOnLoadMethod).
	/// </summary>
	public class GisDatabaseService : IDisposable
	{
		public const int SchemaVersion = 1;

		private const string DefaultDatabaseFileName = "gisdata.db";

		private SQLiteConnection m_connection;

		public string DatabasePath { get; }
		public SQLiteConnection Connection => m_connection;
		public bool IsOpen => m_connection != null;

		public GisDatabaseService(string a_databasePath = null)
		{
			DatabasePath = a_databasePath ?? Path.Combine(Application.persistentDataPath, DefaultDatabaseFileName);
		}

		/// <summary>
		/// Opens (creating if necessary) the database file at DatabasePath and ensures
		/// the Phase A schema tables exist. Safe to call multiple times; subsequent
		/// calls are a no-op while already open.
		/// </summary>
		public void Open()
		{
			if (IsOpen)
				return;

			m_connection = new SQLiteConnection(DatabasePath);
			CreateSchema();
		}

		private void CreateSchema()
		{
			m_connection.CreateTable<DatasetInfoRow>();
			m_connection.CreateTable<LayerRow>();
			m_connection.CreateTable<RasterLayerRow>();
			m_connection.CreateTable<VectorObjectRow>();
		}

		public void Close()
		{
			if (!IsOpen)
				return;

			m_connection.Close();
			m_connection.Dispose();
			m_connection = null;
		}

		public void Dispose()
		{
			Close();
		}
	}
}
