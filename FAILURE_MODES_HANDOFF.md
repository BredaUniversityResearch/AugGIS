# Failure modes / recovery — handoff notes

Local note (not for commit) collecting every place the data-backend rework
deliberately did NOT handle failures, for whoever picks up the project-wide
failure-mode/recovery task. Update as milestones land.

## Pre-existing, seen during Phase A testing
- `NetworkDataMessenger.OnNetworkDespawn` threw a `NullReferenceException` once,
  mid-disconnect-cascade. Not investigated. (The messenger is slated for removal
  in M5, so check whether it still exists before looking at this.)

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

## Not in scope of the data-backend rework at all
- Behaviour when the host leaves/crashes mid-session, reconnect handling,
  and cleanup of partially-initialised layers.
