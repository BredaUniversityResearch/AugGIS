# Failure modes / recovery — handoff notes

Local note (not for commit) collecting every place the data-backend rework
deliberately did NOT handle failures, for whoever picks up the project-wide
failure-mode/recovery task. Update as milestones land.

## Pre-existing, seen during Phase A testing
- `NetworkDataMessenger.OnNetworkDespawn` threw a `NullReferenceException` once,
  mid-disconnect-cascade. Resolved by removal: the class was deleted in M5. Worth
  checking whether the same disconnect cascade still produces errors elsewhere.

## M3 — host REST server (`GisHttpServer`)
- If the port (default 50124, env `MSPXRHttpPort`) cannot be bound, the error is
  only logged; the session continues with no layer API, so clients would fail
  to load. No fallback port, no surfacing to the session/UI.
- No restart if the server stops unexpectedly while the session runs.
- Request handler exceptions become a logged HTTP 500; nothing else reacts.
- No authentication (read-only GETs, LAN session assumed).

## M4 — client layer fetch (`GisHttpDataSource`, implemented)
- Any failed request (network error, non-200, timeout of 60 s, unparsable
  manifest, layer that cannot be built) logs the error and calls
  `WorldManager.OnDatasetLoadFailed`, i.e. disconnects from the session.
- No retries, no partial recovery: one failed layer fails the whole load.
- The client builds the URL from the transport's connection address plus
  `MSPXRHttpPort`/50124. If the env var is set on only one side, the client
  cannot reach the API and the load fails.
- The catalogue "ready" signal (`LayerCatalogue.WhenReady`) is only raised after
  the HOST's own `ImportedConfigRoot` import completes. If the host's dataset
  load or import fails, clients wait indefinitely (no timeout, no error).
- Clients fetch every layer not tagged `Invisible`, including ones that
  `Initialise` later drops as invalid (known, accepted for now).
- Vector layer JSON is parsed on the main thread, which may hitch on large
  layers (not measured).

## S1 — runtime layer add
- Policy for a layer that fails on a client after the initial load (fetch error,
  unparsable manifest, rejected by `ImportedConfigRoot.AddLayer`): log an error,
  skip it, no retry, no disconnect. The client reports it to the host, which only
  logs a warning (`Client <id> could not load layer <layerId>`). Nothing else reacts,
  so a client can silently lack a layer the host has.
- Host side: if `ImportedConfigRoot.AddLayer` rejects a layer, it is not added to
  the catalogue (error logged, nothing announced).
- `ClientLayerLoader` marks an id as handled before fetching, so a failed layer is
  never retried until the client reconnects.
- Untidy, not failures: `AssetManager` is recreated by sprite lookups after the
  import destroyed it; vertical step is not re-applied to runtime layers.

## S2 — POST /layers (`LayerUploadReceiver`, receive-and-discard)
- Chunked uploads (no Content-Length) are rejected with 411; no support yet.
- No limit on the number of concurrent uploads beyond Watson's own; each runs on a
  thread-pool thread and is read at network speed. A stalled client holds a handler
  until Watson's read timeout (not verified what its default is).
- Oversized or malformed uploads are answered (413/400/415) and logged, but the
  caller (Extended Functionality service) gets no retry guidance.
- A client that aborts mid-upload surfaces as a logged HTTP 500. A client that
  sends FEWER bytes than its Content-Length leaves the read waiting on the socket
  (the request stream is not bounded by Watson), until a socket/read timeout.
- Whether Watson keeps the connection clean after an early 413 (body not read) is
  untested.

## S3b — GeoJSON conversion (`GeoJsonLayerConverter`)
- `crs=dataset` is trusted: data that is not actually in the dataset projection
  lands in the wrong place without any error. (`crs=wgs84` does reject positions
  outside the lon/lat range.)
- Layers whose tags match no category/display method in the display method config
  are accepted but will not appear in the layer menu (known, shown/handled later).
- Memory is bounded per feature while parsing, but one enormous single feature is
  loaded whole. Coordinates are stored as float (about 0.25 m at MSP coordinates).
- Malformed features abort the whole upload with a 400 naming the feature index;
  there is no partial acceptance.

## S3c step 1 — storing uploaded vector layers (`GpkgVectorLayerWriter`)
- Uploads are serialised by one lock held for the whole upload, so a slow or
  stalled upload makes every later upload wait (no timeout on the wait).
- An upload that arrives before the host's dataset is in the DB gets a 503. One that
  arrives while the host writes the dataset can interleave with it, and replacing the
  dataset (every host start) drops all uploaded layers and restarts layer ids at 0.
- A stored layer is not yet announced to the session (step 2): until then it is in
  the DB but invisible to clients; if the announce step fails later, the stored layer
  stays behind as an orphan.
- Needs WAL mode (set when the host opens the DB) so uploads do not block layer
  fetches. Not tested on Quest/Android; if WAL cannot be enabled only a warning is
  logged.
- If the dataset projection cannot be described as WKT the layer is stored with the
  undefined-Cartesian srs_id -1 instead (the PROJ string stays in dataset_info).
- Coordinates are stored as double but originate from float (about 0.25 m at MSP
  coordinates); a Multi* feature becomes one row per part, with its metadata repeated.

## Not in scope of the data-backend rework at all
- Behaviour when the host leaves/crashes mid-session, reconnect handling,
  and cleanup of partially-initialised layers.
