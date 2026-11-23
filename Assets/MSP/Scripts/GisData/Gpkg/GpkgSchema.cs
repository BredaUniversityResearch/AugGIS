using System;
using System.Collections.Generic;
using SQLite;

namespace MSP.Scripts.GisData
{
	/// <summary>
	/// The GeoPackage (spec 1.4) metadata tables used for uploaded vector layers, living
	/// inside the same gisdata.db as the MSP tables. Table definitions are copied from
	/// the spec's Annex C (Table Definition SQL); the file is NOT turned into a
	/// standalone GeoPackage: application_id/user_version are not set, since it also
	/// holds the MSP tables, which makes it an "Extended GeoPackage" at best.
	///
	/// Zip-imported layers stay in the old tables; only uploaded layers get a feature
	/// table (see GpkgVectorLayerWriter).
	/// </summary>
	public static class GpkgSchema
	{
		/// <summary>srs_id used for the dataset's own projection.</summary>
		public const int DatasetSrsId = 100000;

		//srs_id -1 = "undefined Cartesian", used when the dataset projection cannot be described
		private const int UndefinedCartesianSrsId = -1;

		//Definition text for the required srs_id 4326 row (EPSG WKT for WGS 84). The
		//spec requires a valid CRS here but does not spell the text out.
		private const string Wgs84Wkt = "GEOGCS[\"WGS 84\",DATUM[\"WGS_1984\",SPHEROID[\"WGS 84\",6378137,298.257223563,AUTHORITY[\"EPSG\",\"7030\"]],AUTHORITY[\"EPSG\",\"6326\"]],PRIMEM[\"Greenwich\",0,AUTHORITY[\"EPSG\",\"8901\"]],UNIT[\"degree\",0.0174532925199433,AUTHORITY[\"EPSG\",\"9122\"]],AUTHORITY[\"EPSG\",\"4326\"]]";

		public static string TableName(int a_layerId)
		{
			return $"layer_{a_layerId}";
		}

		public static bool TableExists(SQLiteConnection a_connection, string a_tableName)
		{
			return a_connection.ExecuteScalar<int>("SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = ?", a_tableName) > 0;
		}

		/// <summary>
		/// Creates the metadata tables when missing and makes sure the three SRS rows the
		/// spec requires (Requirement 11) exist.
		/// </summary>
		public static void EnsureMetadataTables(SQLiteConnection a_connection)
		{
			a_connection.Execute(
				"CREATE TABLE IF NOT EXISTS gpkg_spatial_ref_sys (" +
				" srs_name TEXT NOT NULL," +
				" srs_id INTEGER PRIMARY KEY," +
				" organization TEXT NOT NULL," +
				" organization_coordsys_id INTEGER NOT NULL," +
				" definition TEXT NOT NULL," +
				" description TEXT)");

			a_connection.Execute(
				"CREATE TABLE IF NOT EXISTS gpkg_contents (" +
				" table_name TEXT NOT NULL PRIMARY KEY," +
				" data_type TEXT NOT NULL," +
				" identifier TEXT UNIQUE," +
				" description TEXT DEFAULT ''," +
				" last_change DATETIME NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now'))," +
				" min_x DOUBLE," +
				" min_y DOUBLE," +
				" max_x DOUBLE," +
				" max_y DOUBLE," +
				" srs_id INTEGER," +
				" CONSTRAINT fk_gc_r_srs_id FOREIGN KEY (srs_id) REFERENCES gpkg_spatial_ref_sys(srs_id))");

			a_connection.Execute(
				"CREATE TABLE IF NOT EXISTS gpkg_geometry_columns (" +
				" table_name TEXT NOT NULL," +
				" column_name TEXT NOT NULL," +
				" geometry_type_name TEXT NOT NULL," +
				" srs_id INTEGER NOT NULL," +
				" z TINYINT NOT NULL," +
				" m TINYINT NOT NULL," +
				" CONSTRAINT pk_geom_cols PRIMARY KEY (table_name, column_name)," +
				" CONSTRAINT uk_gc_table_name UNIQUE (table_name)," +
				" CONSTRAINT fk_gc_tn FOREIGN KEY (table_name) REFERENCES gpkg_contents(table_name)," +
				" CONSTRAINT fk_gc_srs FOREIGN KEY (srs_id) REFERENCES gpkg_spatial_ref_sys (srs_id))");

			InsertSrsIfMissing(a_connection, "Undefined Cartesian SRS", UndefinedCartesianSrsId, "NONE", -1, "undefined", "undefined Cartesian coordinate reference system");
			InsertSrsIfMissing(a_connection, "Undefined geographic SRS", 0, "NONE", 0, "undefined", "undefined geographic coordinate reference system");
			InsertSrsIfMissing(a_connection, "WGS 84", 4326, "EPSG", 4326, Wgs84Wkt, "longitude/latitude coordinates in decimal degrees on the WGS 84 spheroid");
		}

		/// <summary>
		/// Registers the dataset's own projection as a user-defined SRS (WKT built from the
		/// PROJ string) and returns its srs_id. A projection the converter cannot describe
		/// falls back to srs_id -1 (undefined Cartesian); the PROJ string itself stays in
		/// dataset_info either way. The row is replaced on every call, so it always
		/// follows the current dataset.
		/// </summary>
		public static int EnsureDatasetSrs(SQLiteConnection a_connection, string a_projString)
		{
			string wkt;
			try
			{
				wkt = CoordinateConverterFactory.Create(a_projString).ToWkt();
			}
			catch (NotSupportedException)
			{
				return UndefinedCartesianSrsId;
			}

			a_connection.Execute(
				"INSERT OR REPLACE INTO gpkg_spatial_ref_sys (srs_name, srs_id, organization, organization_coordsys_id, definition, description) VALUES (?, ?, ?, ?, ?, ?)",
				"MSP dataset projection", DatasetSrsId, "NONE", DatasetSrsId, wkt, a_projString);
			return DatasetSrsId;
		}

		/// <summary>
		/// Drops every uploaded layer's feature table and its metadata rows. Called when
		/// the dataset is replaced: layer ids restart at 0 then, so leftovers would clash
		/// with (and be mistaken for) the new layers' tables.
		/// </summary>
		public static void DropAllLayerTables(SQLiteConnection a_connection)
		{
			if (!TableExists(a_connection, "gpkg_contents"))
				return;

			List<GpkgTableNameRow> featureTables = a_connection.Query<GpkgTableNameRow>("SELECT table_name AS Name FROM gpkg_contents WHERE data_type = 'features'");
			foreach (GpkgTableNameRow row in featureTables)
				a_connection.Execute($"DROP TABLE IF EXISTS \"{row.Name}\"");

			if (TableExists(a_connection, "gpkg_geometry_columns"))
				a_connection.Execute("DELETE FROM gpkg_geometry_columns");
			a_connection.Execute("DELETE FROM gpkg_contents");
		}

		private static void InsertSrsIfMissing(SQLiteConnection a_connection, string a_name, int a_id, string a_organization, int a_organizationId, string a_definition, string a_description)
		{
			a_connection.Execute(
				"INSERT OR IGNORE INTO gpkg_spatial_ref_sys (srs_name, srs_id, organization, organization_coordsys_id, definition, description) VALUES (?, ?, ?, ?, ?, ?)",
				a_name, a_id, a_organization, a_organizationId, a_definition, a_description);
		}
	}

	/// <summary>Result row of a single table name query (sqlite-net maps by property name).</summary>
	public class GpkgTableNameRow
	{
		public string Name { get; set; }
	}
}
