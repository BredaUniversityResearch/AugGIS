# AugGIS GIS Data Layer — Implementation Info

Reference doc for the GIS data work, a shared, fixed point of continuity between
us across sessions/chats. Semi-static: updated at milestones, as its own step.

Current branch: `rest-gis-layer` (S2/S3). Previous branch `data-backend-rework`
(Phase A + sub-phase 1 + S1) is finished and in review.

## Done (summary)

### Phase A — SQLite storage
GIS datasets are stored in a local SQLite database instead of being re-parsed from
a zip every time. Code under `Assets/MSP/Scripts/GisData/` (namespace
`MSP.Scripts.GisData`).
- Schema: `DatasetInfoRow` / `LayerRow` / `RasterLayerRow` / `VectorObjectRow`
  (JSON-text columns, per-layer raster BLOBs, single-dataset model).
- `IGisDataSource` + `GisDataset` envelope; `MspChallengeZipDataSource` (legacy MSPC
  zip); `SqliteGisDatasetWriter` / `SqliteGisDataSource` (the DB stays pristine:
  throws if handed already-rendered data); `GisDatabaseService` (provider init and
  connection lifecycle). It must be constructed in `Awake()`/`Start()` because it
  touches `Application.persistentDataPath`.
- `WorldManager` loads via the zip source, writes `gisdata.db`, decodes rasters with
  `GisRasterTextureLoader`, and calls `ImportedConfigRoot.Instance.Initialise(...)`.
  `LoadZipData`/`ZipUtil` remain only for `ConfigImporterWindow`'s offline tooling.
- Deferred: `sqlite-caching-versioning` (reuse the DB across host restarts, low
  priority; `SchemaVersion` already exists).

### Phase B, sub-phase 1 — layer discovery/fetch API (M1-M5)
Clients no longer receive the DB file; they discover layers and fetch them over a
layer-kind-agnostic protocol. Live-tested (editor, Linux server host, Quest
host/client, early/late join).
- **Stable identity:** `ALayer.LayerId` (= `LayerRow.Id`/`SortOrder`) replaces
  positional identity wherever identity crosses a network/lookup boundary.
  `LayerIndex` is render-time only.
- **Catalogue:** `LayerCatalogue` holds a server-written
  `NetworkList<LayerCatalogueEntry>` (metadata only, fixed-size strings, truncation
  logged). Entries present at spawn raise no events, entries added later raise
  `OnEntryAdded`. `WhenReady` fires only once the host finished its own import.
- **Host REST server:** `GisHttpServer` (Watson.Lite 6.6.0, TCP, works on
  Android/IL2CPP, `0.0.0.0`, default port 50124, env `MSPXRHttpPort`, no auth, LAN
  only). Handlers run on thread-pool threads. All content goes through
  `ILayerPayloadProvider` (`SqliteLayerPayloadProvider` today, short-lived read-only
  SQLite connection per request):
  - `GET /dataset` -> projection, coordinates, metadata, source, schemaVersion
  - `GET /layers/{id}` -> manifest: common fields (id, kind, sortOrder, name, short,
    tags, types), an opaque kind-specific `data` object, and `assets`
    (`{key, contentType, length, meta?}`)
  - `GET /layers/{id}/assets/{key}` -> raw bytes with the asset's content type
  - Raster: `data` = coordinates/dataPath/scale/mapping + asset `raster` (image/png).
    Vector: `data.objects` inline, no assets.
- **Client fetch:** `GisHttpDataSource` (an `IGisDataSource`) fetches `/dataset`,
  then each catalogue entry's manifest and assets sequentially (skipping
  `Invisible`). `LayerManifestReader` builds `RasterLayer`/`VectorLayer`. The host
  URL = `UnityTransport.ConnectionData.Address` + `GisHttpServer.ResolvePort()`.
  Clients keep no local database. The old whole-DB transport and
  `NetworkDataMessenger` were removed; `SqliteGisDataSource` is kept for the
  deferred caching work.
- Known, accepted: `Initialise` is all-at-once; clients fetch layers `Initialise`
  later drops as invalid; fetching is sequential; vector JSON is parsed on the main
  thread (not measured on Quest).

### Sub-phase 1b (S1) — runtime layer add
Layers can be added to a running session, on the host and every client. Live-tested
(editor host + 2 Quest clients, early and late joiner).
- **Host:** `WorldManager.AddLayerToSession(layer, kind, textures)` (server, main
  thread; **the layer must already be in the DB**) calls
  `ImportedConfigRoot.AddLayer` (queued, one at a time, needs completed initial
  import, same Invisible/Validate filtering, raises `m_onLayerAdded`). Only on
  success it appends to `LayerCatalogue`, which notifies clients.
  `LayerManager.OnLayerAddedAfterImport` handles it; runtime layers start hidden.
- **Layer menu:** the host owns an append-only `NetworkList<FixedString128Bytes>` of
  category names (`LayerMenuNetworked`); the list index is the category id. A new
  layer in an existing category is appended in place; a new category is appended at
  the end. A shared selection of a category not built yet is applied once it appears.
- **Client:** `ClientLayerLoader` keeps a set of handled ids (seeded from the
  initial catalogue snapshot), reconciles against the catalogue after the initial
  import and on every `OnEntryAdded`, fetches unknown non-Invisible layers one at a
  time via `GisHttpDataSource.LoadLayer`, then `ImportedConfigRoot.AddLayer`.
- **Failures:** a failed runtime layer on a client is logged, skipped, never
  retried, no disconnect; the client reports it (`ReportLayerLoadFailedRpc`) and the
  host logs a warning.
- **Editor test aid:** `WorldManager.m_debugHoldBackLayerCount` holds back the last N
  layers at load; the Odin button "Add next held-back layer" adds them one by one.
- Known, accepted: runtime category entries call `AssetManager.GetSprite`, which may
  recreate an `AssetManager` destroyed by the import; vertical step is not
  re-applied to runtime layers; a new category can briefly show an empty page on a
  client that has not loaded its layers yet.

## Decisions (don't re-litigate)

- **Target format: GeoPackage** (OGC GPKG, SQLite based); GeoJSON is the input
  format for vector data.
- **No authentication** on the layer API for now (LAN, trusted); at most a fixed
  shared token later.
- **Caller of the write API:** not the AR devices but the team's "Extended
  Functionality" service (LLM-orchestrated; a Quest voice command makes it look up,
  process and present layers from an external DB, and it POSTs them to the host).
- **Reprojection stays minimal:** the project has no projection library, the MSP
  dataset projection is a PROJ.4 laea string and GeoJSON is WGS84. Implement only a
  small WGS84 -> dataset-laea converter behind an interface; reject anything else
  with a clear error. Generic PROJ support is explicitly out of scope.
- **Geometry types:** points, lines, polygons (plus Multi variants) from GeoJSON.
  One geometry type per layer; mixed files are rejected, the caller sends clean
  single-purpose layers.
- **GeoPackage scope:** standard GPKG tables in the existing `gisdata.db`, new
  layers only, data reprojected into the dataset CRS BEFORE it lands in the DB so
  the rest of the path stays simple. Zip-imported layers stay in the old tables.
- **Raster ingestion (first version):** PNG plus bounds in the dataset's own
  projection, matching what `RasterLayer` already stores. GeoTIFF and raster
  reprojection are later work.
- **New layers** are hidden by default and should appear in the layer list and be
  loaded by clients as soon as possible, even if not shown.
- **Failure handling** belongs to another teammate's task; gaps are noted in
  `FAILURE_MODES_HANDOFF.md`, not fixed piecemeal.

## Plan — branch `rest-gis-layer`

Each step is handed over individually; nothing is started before it is asked for.

### S2 — POST ingestion (receive only) — done
`POST /layers` (`LayerUploadReceiver`, called from `GisHttpServer`) receives an
upload and discards it. Live-tested with a large GeoJSON (600+ MultiPolygon
features), PNG, and invalid input.
- **Request shape:** body = raw payload, metadata in the query string. Vector:
  `Content-Type: application/json` or `application/geo+json`, GeoJSON body. Raster:
  `image/png`, PNG body plus `bounds=minX,minY,maxX,maxY`. Query: `name` (required),
  `short`, `category`, `tags` (comma separated).
- **Large files, non-blocking:** body read as a stream on the handler's thread-pool
  thread; GeoJSON walked with a forward-only `JsonTextReader` (no full `JObject`).
  `Content-Length` required (chunked -> 411); limit 512 MB, env `MSPXRMaxUploadMB`
  (413); 400 for bad JSON/PNG/missing params; 415 for other content types.
- **Gotcha:** Watson's `Request.Data` is not bounded by `Content-Length`; a read
  past the body blocks on the kept-alive socket forever. `CountingStream` reports
  end-of-stream after `Content-Length` bytes (also counts bytes, logs progress
  about once a second).

### S3 — reprojection, conversion, storage, wiring
Findings that shape it: a runtime `VectorObject` is ONE polygon (`points` = outer
ring, `gaps` = holes), line or point in dataset-projection `float` coordinates, so a
MultiPolygon feature becomes several objects. A layer's geometry type comes from its
tag (`Polygon`/`Line`/`Point`), so layers are homogeneous. Polygons/lines are
clipped to the play area at render time, so no clipping at ingest. The `types`
catalogue is per layer (`name`, `colour`, ...), objects index into it.

- **S3a — reprojection.** `ICoordinateConverter` with an ellipsoidal Lambert
  azimuthal equal-area implementation (Snyder), parameters read from the dataset's
  PROJ string; any other projection is rejected with a clear error. Check: lon 10,
  lat 52 must give exactly (x_0, y_0); plus a forward/inverse round trip (no pyproj
  available for reference values). No DB involved.
- **S3b — GeoJSON -> layer model.** Streaming conversion to `VectorObject[]`: Multi*
  split into several objects, rings reprojected, feature `properties` -> object
  metadata, layer tags and types catalogue built. A file mixing geometry types is
  rejected (400); the caller must send clean single-type layers. Types: optional
  query param `typeProperty` (distinct values of that feature property become the
  catalogue, default colour or a `colour` param); without it one type named after
  the layer. Still no DB (log/inspect the result).
  CRS: optional query param `crs` on vector uploads: `wgs84` (default, reprojected
  through `CoordinateConverterFactory`) or `dataset` (already in the dataset
  projection, passed through untouched). Anything else is a 400. We do NOT verify
  what the caller claims. A caller that cannot send WGS84 finds the dataset
  projection string via the existing `GET /dataset` (`projection`), projects the
  data itself and sends `crs=dataset`. The legacy GeoJSON `crs` member stays ignored.
- **S3c — storage + wiring.** Standard GPKG tables added to the existing
  `gisdata.db` (`gpkg_spatial_ref_sys`, `gpkg_contents`, `gpkg_geometry_columns`),
  one feature table per uploaded layer (WKB geometry + attributes), stored already
  reprojected into the dataset CRS, so serving stays cheap. Plus the generic asset
  table (layer id, key, content type, bytes). Served through `ILayerPayloadProvider`
  (manifest format unchanged for clients). Zip-imported layers stay in the old
  tables, no compatibility work. The DB write must complete BEFORE the main-thread
  handoff to `AddLayerToSession` (otherwise clients get a 404). The POST response
  returns the new layer id or an error.
- **S3d — raster ingest.** PNG + bounds into `RasterLayerRow` through the same add
  flow. `DataPath` is only the key under which `AssetManager` stores the texture
  (the zip path is legacy), so uploads get a generated unique key. Open: what kind
  of raster the service sends (value map with a scale vs. type map vs. a
  display-as-is image); the current `RasterLayer` has no plain-image mode, a layer
  without scale falls back to Lin(0,1000) with a warning.
- **S3e — upload API self-description and usage docs** (after S3d, so it describes the
  final request shape).
  - `GET /layers/schema`: small, hand-written, static JSON describing the upload API:
    accepted content types, query parameters (required/optional, type, example), the
    live size limit (`MSPXRMaxUploadMB`), layer kinds and their required fields, the
    `crs` options and where to find the dataset projection. Lets
    the Extended Functionality service read the live contract instead of a hardcoded
    copy. No OpenAPI generator or UI.
  - Usage documentation for the service developers (curl examples, parameters,
    status codes, response format). Written as its own step when asked.

### Later
- **S4:** spatial indexing (R-tree / bbox) for large layers.
- Incremental `Initialise` for the initial load; clients fetching only relevant
  layers.
- GeoTIFF / raster reprojection; `sqlite-caching-versioning`.
- Failure-mode/recovery pass (separate task, `FAILURE_MODES_HANDOFF.md`).

## How to pick this back up

1. Re-read this file top to bottom.
2. Check `git log` on `rest-gis-layer` for anything newer than described here.
3. Plan the next step collaboratively before writing code; implement only the
   part explicitly handed over; never commit (the user manages git); the user
   tests in Unity.
