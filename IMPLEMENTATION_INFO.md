# AugGIS Data Backend Rework — Implementation Info

Local reference doc for this work, kept out of version control on purpose (per
@sainsay — most documentation lives outside the repo; this file is just a shared,
fixed point of reference between us for continuity across sessions/chats).

Branch: `data-backend-rework`.

## Phase A — done

Goal: move GIS dataset storage from "re-parsed from a zip every time" to a local
SQLite database, as a stepping stone to real GIS-compliant layers later (Phase B).
Kept the existing MSPC zip-shaped data model unchanged in meaning for Phase A — no
GIS compliance yet, just proving the persistence + transport mechanism works.

**Status: fully done and live-tested.**

- SQLite schema (JSON-text columns, per-layer raster BLOBs, single-dataset model),
  under `Assets/MSP/Scripts/GisData/` (namespace `MSP.Scripts.GisData`):
  `DatasetInfoRow` / `LayerRow` / `RasterLayerRow` / `VectorObjectRow`.
- `IGisDataSource` + `GisDataset` envelope abstraction; `MspChallengeZipDataSource`
  (wraps the legacy MSPC zip pipeline unchanged); `SqliteGisDatasetWriter` /
  `SqliteGisDataSource` (write/read, enforcing "DB stays pristine" by throwing if
  handed already-rendered data); `SqliteRuntimeBootstrap` / `GisDatabaseService`
  (provider init + connection lifecycle).
- **`network-transport-swap`** (the whole-DB-file transport described here was
  later replaced by the layer fetch API, see Phase B sub-phase 1): `WorldManager` now fully replaces the old
  raw-zip-over-the-network flow (hard cut, no legacy toggle/dual-path — explicitly
  agreed, since backward compatibility isn't being kept beyond "load the zip into
  the DB"):
  - Host loads via `MspChallengeZipDataSource` → `SqliteGisDatasetWriter.Write`
    (persists to `gisdata.db`) → new `GisRasterTextureLoader` (decodes raw PNG
    bytes → `Texture2D`, replacing `ZipUtil`'s decode-as-side-effect) →
    `ImportedConfigRoot.Instance.Initialise(...)`, using the **in-memory** dataset
    directly (no DB re-read on the host — nothing mutates it before `Initialise`).
  - Host ships the written `gisdata.db` file's bytes (`File.ReadAllBytes`) to each
    connecting client over the existing `NetworkDataMessenger` chunked transport,
    instead of raw zip bytes.
  - Client writes received bytes to its own local `gisdata.db`, reads it back via
    `SqliteGisDataSource`, same `GisRasterTextureLoader` → `Initialise(...)` path.
  - `LoadZipData`/`ZipUtil` remain only for `ConfigImporterWindow`'s manual/offline
    tooling — no longer part of the live session path. Two small additive getters
    added to `LoadZipData` (`DisplayMethodConfigFile`, `FileName`) so `WorldManager`
    can reuse its existing configured Inspector references instead of duplicating
    them.
  - `GisDatabaseService` must be constructed in `Awake()`/`Start()`, not a field
    initializer — its constructor touches `Application.persistentDataPath`, which
    Unity disallows outside those lifecycle methods (hit and fixed during testing).
  - **Live-tested and confirmed working**: laptop-as-host + XR-device-as-client,
    and XR-device-as-host + XR-device-as-client. Both topologies render identically
    to the old zip flow (confirmed via `config.zip`; the `Test_Small/Medium/Large`
    zips show unrelated pre-existing layer-validation warnings from stale fixture
    data, not something this rework introduced).
- **Deferred, not dropped**: `sqlite-caching-versioning` (persist/reuse the DB
  across host restarts) — confirmed low priority for now after checking with the
  game designer. `DatasetInfoRow.SchemaVersion`/`GisDatabaseService.SchemaVersion`
  already exist in the schema if/when this becomes relevant again.
- **Explicitly out of scope here, by design**: a dedicated failure-mode/recovery
  pass (e.g. the `NetworkDataMessenger.OnNetworkDespawn` `NullReferenceException`
  seen once during testing, mid-disconnect-cascade) — @sainsay has a separate,
  already-assigned task specifically for failure modes/recovery across the project,
  so this is intentionally left alone here rather than fixed piecemeal.

## Phase B, sub-phase 1 — layer discovery/fetch API — done

Goal: stop sending the whole DB file over the network; clients discover which
layers exist and fetch only through a layer-kind-agnostic protocol. Milestones
M1-M5, all live-tested (editor, Linux server host, Quest host + Quest client,
early join, late join, failure case).

- **M1 — stable layer identity.** `ALayer.LayerId` (data-sourced, assigned by
  `SqliteGisDatasetWriter`, equals `LayerRow.Id`/`SortOrder`) replaces positional
  identity everywhere identity crosses a network/lookup boundary
  (`LayerManager`, `LayerTag*`, `LayerMenuLayer`, NetworkList structs
  `m_layerId`). `LayerIndex` stays a purely render-time positional value
  (vertical offset / render-queue order), unrelated to identity.
- **M2 — layer catalogue.** `LayerCatalogue` (on the `LayerManager` GameObject)
  holds a server-written `NetworkList<LayerCatalogueEntry>` (metadata only:
  id/kind/sortOrder/name/short/tags, fixed-size strings: 128/128/512 bytes,
  truncation is logged). Late joiners receive it automatically; entries added
  later raise `OnEntryAdded`. Entries already present at spawn raise no events,
  so consumers read `Entries` once when ready. `LayerManager` looks up its
  positional NetworkLists by id (`IndexOfLayerData`/`IndexOfTagData`).
- **M3 — host REST server.** `GisHttpServer` (Watson.Lite 6.6.0, TCP-based, works
  on Android/IL2CPP; bound to `0.0.0.0`, default port 50124, override with env
  `MSPXRHttpPort`; no auth, LAN only). Protocol is identical for every layer
  kind and goes through the `ILayerPayloadProvider` seam
  (`SqliteLayerPayloadProvider` today; opens a short-lived read-only SQLite
  connection per request because handlers run on thread-pool threads):
  - `GET /dataset` -> projection, coordinates, metadata, source, schemaVersion
  - `GET /layers/{id}` -> JSON manifest: common fields (id, kind, sortOrder,
    name, short, tags, types), an opaque kind-specific `data` object, and an
    `assets` array of `{key, contentType, length, meta?}`
  - `GET /layers/{id}/assets/{key}` -> raw bytes with the asset's content type
  - Raster today: `data` = coordinates/dataPath/scale/mapping + one asset
    `raster` (image/png). Vector today: `data.objects` inline, no assets.
    Future timeline PNGs or dense binary blobs fit as extra assets; how an
    asset is interpreted is up to each layer kind, not the protocol.
- **M4 — client fetch.** `LayerCatalogue.WhenReady` fires once the host has
  published the catalogue AND finished its own `ImportedConfigRoot` import
  (the `NetworkVariable<int>` published-count is only set on host
  `ImportComplete`; this deliberately preserves the old "clients load only after
  host import is complete" guarantee, because `LayerManager` ignores `Add` events
  on its per-layer lists). The client then runs `GisHttpDataSource` (an
  `IGisDataSource`): fetch `/dataset`, then each catalogue entry's manifest and
  assets sequentially in sort order (skipping layers tagged `Invisible`),
  `LayerManifestReader` builds `RasterLayer`/`VectorLayer`, and the assembled
  `GisDataset` goes through the existing `GisRasterTextureLoader` ->
  `Initialise(...)` path. The host URL comes from
  `UnityTransport.ConnectionData.Address` + `GisHttpServer.ResolvePort()`.
  Clients keep no local database.
- **M5 — cleanup.** Removed the whole-DB-file transport:
  `NetworkDataMessenger` (script and scene component), `WorldManager`'s
  send/receive code. Only the host opens `GisDatabaseService`.
  `SqliteGisDataSource` is intentionally kept for the deferred caching work.
  `LoadZipData`/`ZipUtil` remain only for `ConfigImporterWindow`'s offline tooling.

Notes:
- `GisDatabaseService` must be constructed in `Awake()`/`Start()` (touches
  `Application.persistentDataPath`).
- Known, accepted limitations: `Initialise` is still all-at-once and clients
  fetch every non-`Invisible` layer including ones `Initialise` later drops as
  invalid; fetching is sequential and vector JSON is parsed on the main thread
  (not measured on Quest). Failure handling is out of scope here, see
  `FAILURE_MODES_HANDOFF.md` (untracked) for what to pass to the failure-modes
  task.

## Phase B, sub-phase 1b (S1) — runtime layer add — done

Goal: layers can be added to a running session, on the host and on every client,
without a reload. Live-tested (editor host + 2 Quest clients, one early, one late
joiner). This is the last step of branch `data-backend-rework`.

- **Host add.** `WorldManager.AddLayerToSession(layer, kind, textures)` (server,
  main thread; layer data must already be in the DB) calls
  `ImportedConfigRoot.AddLayer` (queued, one at a time, requires the initial import
  to be complete, same Invisible/Validate filtering, applies all display methods,
  raises `m_onLayerAdded`), and only on success appends the layer to
  `LayerCatalogue`, which tells the clients. `LayerManager` handles
  `m_onLayerAdded` (`OnLayerAddedAfterImport`); runtime layers start hidden
  (static layers still follow config).
- **Layer menu.** The host owns an append-only `NetworkList<FixedString128Bytes>`
  of category names (`LayerMenuNetworked`); the list index is the category id, so
  indices agree on every machine and late joiners get it for free. Every machine
  builds category entries from that list (`LayerMenu.AddCategory`). A new layer in
  an existing category is appended to its page in place; a layer in a new category
  appends that category at the end. Empty categories are ignored. A shared
  selection naming a category not built yet on this machine is remembered and
  applied when it appears.
- **Client add.** `ClientLayerLoader` (created by `WorldManager` on the client):
  keeps a set of handled layer ids (seeded from the catalogue snapshot used by the
  initial load), reconciles it against the catalogue when the client's initial
  import completes and on every `OnEntryAdded`, and fetches unknown non-Invisible
  layers one at a time via `GisHttpDataSource.LoadLayer` (the same per-layer fetch
  code the initial load uses), then `ImportedConfigRoot.AddLayer`. Late joiners
  get everything through the normal initial path.
- **Failures.** A runtime layer that fails on a client is logged, skipped, never
  retried and does not disconnect; the client reports it to the host
  (`WorldManager.ReportLayerLoadFailedRpc`) which logs a warning with the client id
  and layer id.
- **Testing aid (editor only).** `WorldManager.m_debugHoldBackLayerCount` holds back
  the last N layers at dataset load; the Odin button "Add next held-back layer"
  (play mode, host) adds them one by one through `AddLayerToSession`.
- Small additions: `AssetManager.AddRasterTextures`,
  `GisRasterTextureLoader.Decode`, `LayerManager.TryGetLayerData`.

Known, accepted: a category entry created at runtime calls `AssetManager.GetSprite`,
which may recreate an `AssetManager` after the import destroyed it; the vertical
step is not re-applied to runtime layers (they start at step 0, like initial
import); a new category can briefly show an empty page on a client that has not
loaded its layers yet.

## Decisions for what comes next

- **Target format: GeoPackage** (OGC GPKG, SQLite based), with GeoJSON as input
  format. New layers follow standard GIS formats; the host only needs to receive
  the data, not download it from sources.
- **Write endpoint: no authentication** for now (LAN, trusted); at most a fixed
  shared token later.
- **Projection gap:** the MSP dataset projection is a PROJ.4 laea string, GeoJSON is
  WGS84, and the project has no projection library, so reprojection must be added.
- **New layers** are hidden by default and should show up in the layer list and be
  loaded by clients as soon as possible, even if not shown.

## Deferred / next (separate branches)

- **S2 — POST ingestion on the host:** HTTP endpoint on `GisHttpServer`; handlers
  run on thread-pool threads, so hand the layer to the main thread and call
  `AddLayerToSession`. The layer must be written to the DB BEFORE
  `AddLayerToSession`, otherwise clients get a 404 when they fetch it.
- **S3 — GeoPackage tables, GeoJSON parsing, reprojection** (likely behind
  `ILayerPayloadProvider`, with a generic asset table: layer id, key, content type,
  bytes).
- **S4 — spatial indexing** (R-tree / bbox) for large layers.
- **Later:** incremental `Initialise` for the initial load; clients fetching only
  relevant layers.
- **`sqlite-caching-versioning`:** persist/reuse the DB across host restarts (low
  priority; `SchemaVersion` exists).
- **Out of scope by design:** failure-mode/recovery pass (separate task, see
  `FAILURE_MODES_HANDOFF.md`).

## How to pick this back up

1. Re-read this file top to bottom.
2. Check `git log` on `data-backend-rework` for anything newer than described here.
3. Plan the next step collaboratively before writing code; implement only the
   part explicitly handed over; never commit (the user manages git); the user
   tests in Unity.
