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
- **`network-transport-swap`**: `WorldManager` now fully replaces the old
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

## Phase B — in progress (just starting)

Two sub-phases, in order, per @sainsay:

1. **Layer discovery/fetch API** (current focus) — stop sending the whole DB file
   over the network; instead, clients discover what layers exist and fetch only
   what they need. Must work the same regardless of layer origin/type (today's
   MSPC-shaped layers in SQLite, and future real-GIS layers) — i.e. the protocol
   operates on the common `LayerRow` concept (`Id`/`Kind`/`Name`/`Short`/`Tags`/
   `SortOrder`), not on MSPC-specific knowledge. Must also handle a client joining
   an already-running session and needing whatever layers are "currently relevant"
   at that point, through the same mechanism as an initial load (no separate
   late-join code path).
2. **Real GIS support in the DB** — after (1), add actual GIS-compliant layers
   (geometry types, SRID, spatial indexing) and support the server loading/
   receiving *new* layers into the dataset (not just the fixed MSPC import). This
   is the original "Phase B" from the initial plan, not yet scoped in detail.

### Groundwork/findings relevant to designing (1), already checked in the codebase

- `ImportedConfigRoot.Initialise(DataConfig, DisplayMethodConfig)` is an all-at-once
  operation today: it expects the *complete* `raster_layers`/`vector_layers` arrays
  up front and sequentially loads every layer in lockstep
  (`LoadNextLayer`/`OnLayerLoaded`) before `ImportComplete` ever becomes `true` —
  there's no existing support for incrementally feeding it layers one at a time or
  rendering a partial set. Whether this needs to change, or whether the client
  should keep assembling a complete in-memory `DataConfig`/`GisDataset` from
  individually-fetched layers before calling `Initialise` once (same external
  contract, different internal assembly), is an open design question — see below.
- There's no existing late-join special-casing anywhere: `WorldManager.
  OnClientConnected` → `SendConfigDataToClientRoutine` already runs for *any*
  connecting client regardless of session state, and `SessionManager` has no
  separate "session already in progress" branch. This is good news — "client joins
  an ongoing session" doesn't need new session-state plumbing, only needs the
  fetch protocol itself to answer "what's the current layer set" correctly
  whenever it's asked.
- Existing Netcode RPC usage project-wide (`WorldManager`, `LayerMenuNetworked`,
  `UILayerProbeNetworked`, etc.) is **fire-and-forget void ServerRPCs** paired with
  `NetworkVariable`s for state sync — there is no existing request/response
  pattern with a returned payload. `NetworkDataMessenger`'s custom named-message /
  `CustomMessagingManager`-based chunked transport (already proven for arbitrary-
  size byte payloads) is the closest existing building block for a real
  request/response protocol (e.g. a second named-message channel for client→server
  requests), rather than fighting Netcode's RPC attribute system.
- `LayerRow.Id` (SQLite auto-increment PK) is already a natural, stable per-layer
  identifier and is already the FK used by `RasterLayerRow`/`VectorObjectRow` — the
  schema was already built with exactly this per-layer addressing in mind (called
  out in the original Phase A design as protecting future streaming use cases).

### Open design questions for (1) — not yet discussed with @sainsay

- Does the client still persist a local `gisdata.db` of fetched layers at all, or
  does it now just assemble an in-memory `GisDataset`/`DataConfig` from fetched
  layer payloads with no local SQLite involvement? (The latter would also remove
  the close/overwrite/reopen dance the client currently does with its DB file.)
- Request/response transport shape: new named-message pair on top of
  `CustomMessagingManager` (list-layers request/response, get-layer request/
  response), versus some other mechanism?
- Whether `ImportedConfigRoot.Initialise`'s all-at-once contract changes, or stays
  as-is with the client gathering a complete layer set first (per the "late join
  fetches whatever's currently relevant" framing, this seems to favor keeping
  `Initialise` unchanged, but not yet confirmed).

## How to pick this back up

1. Re-read this file top to bottom.
2. Check `git log` on `data-backend-rework` for anything committed after the
   `network-transport-swap` work that isn't reflected here yet.
3. Phase B sub-phase (1) — the layer discovery/fetch API — is the current focus.
   Design it collaboratively with @sainsay before writing code, per their
   preferred workflow (discuss/agree, then implement only once given explicit
   permission).

## previous chat history
### llm
Good, I found a clean answer for the package question — and it actually clarifies the REST surface itself too.

HTTP/REST package recommendation
Primary recommendation: WatsonWebserver.Lite (NuGet package, install the same way you already installed SQLite for Phase A — via NuGetForUnity, which is already in Packages/manifest.json, so this is a proven, already-working install path for this project, not something new to figure out).

Why this one specifically:

It's built on raw TCP sockets (via CavemanTcpServer), not System.Net.HttpListener/http.sys — and its docs explicitly list Android and iOS as supported platforms, alongside Windows/Linux/macOS. That directly matters here: you've already proven an XR device can act as host, so whatever serves the REST API has to run correctly under IL2CPP/Android, not just on your laptop. System.Net.HttpListener itself (and ASP.NET Core/Kestrel) has a much shakier, often-unsupported story on Android/IL2CPP — I'd actively avoid both for that reason.
Targets .NET Standard 2.0/2.1 — compatible with Unity's IL2CPP toolchain.
No admin/root privileges required, no OS cert-store/netsh binding requirements (those only apply to its HttpListener-based sibling package, plain WatsonWebserver) — simpler, fewer platform-specific gotchas for something that needs to "just work" identically whether host is your laptop or a headset.
Actively maintained (dotnet/WatsonWebserver org on GitHub, recent activity).
One consistent implementation across every host platform — no conditional "use X on Windows, Y on Android" branching, which fits the "keep networking layers clean" agreement from your team discussion.
(Secondary/fallback candidate, if Watson doesn't pan out for some reason: EmbedIO-Neo — a community continuation of EmbedIO, which also supports a managed, non-HttpListener listener mode. I'd treat this as backup only: the original EmbedIO is archived upstream, and Neo is a newer, less-established fork.)

For JSON — no new dependency needed; keep using the Newtonsoft.Json already in the project (com.unity.nuget.newtonsoft-json) for request/response bodies, same as the rest of the GIS code. Watson doesn't force its own serializer on you for routes you write yourself.

What this does to the design (a nice simplification)
Since the layer catalogue (what layers exist, their id/kind/name/tags/sort-order, and visibility state) now lives in Netcode NetworkLists, REST's job shrinks to just one thing: serving the actual per-layer payload bytes, GET /layers/{id}. Clients never need to GET a list over HTTP at all — they already know what ids exist and are relevant from the synced NetworkList, and only reach for REST once per id they don't have yet.

Proposed implementation plan
M1 — Replace positional LayerIndex with a stable id.
Touches LayerManager (LayerInstanceData, DMInstanceData, LayerTagData, m_layerOrderNetworkData, m_handlesPerStep), ALayer/LayerIndex, and consumers like LayerMenuLayer, LayerProbe/UILayerProbeNetworked. This is foundational — everything else below depends on a layer identity that survives individual/async fetch, not just "same array, same order, everywhere." Clean hard-cut rename (no parallel old+new field), consistent with how we've approached everything else.

M2 — New NetworkList-based layer catalogue in LayerManager.
Metadata-only entries (id/kind/name/short/tags/sort-order), server-authoritative, populated by host once the dataset is loaded (from the same GisDataset/SqliteGisDatasetWriter output Phase A already produces). This fully replaces sending the whole .db file over NetworkDataMessenger.

M3 — Host-side REST server (WatsonWebserver.Lite-based).
New component (e.g. GisHttpServer in MSP.Scripts.GisData), exposing GET /layers/{id} only for now — reusing/refactoring the single-layer read logic already sitting inside SqliteGisDataSource.ReadRasterLayer/ReadVectorLayer (today they loop all layers; split into a per-id version). Started/stopped in WorldManager alongside GisDatabaseService's existing lifecycle.

M4 — Client-side fetch.
On catalogue NetworkList change, fetch any not-yet-known layer ids via plain UnityWebRequest.Get(...) (already used elsewhere in this project — no new HTTP client needed), parse into RasterLayer/VectorLayer, assemble a GisDataset/DataConfig, call ImportedConfigRoot.Initialise(...) once — matching the "fetch everything, then continue" scope we already agreed as the right first step.

M5 — Remove the now-obsolete whole-DB-file transport.
Delete the NetworkDataMessenger-based send/receive path from WorldManager entirely (hard cut, no dual path).

Explicitly deferred/out of scope for this round: external-tool POST ingestion (your stated sub-phase 2), incremental/streamed Initialise (still an open question, not decided), and failure-mode/recovery hardening (your separately-assigned task).

Let me know which of M1–M5 you'd like to take yourself versus hand to me, and whether the plan shape itself looks right before we lock anything in.

----
### user
I think we should just go down the list. you can start with M1 i don't think it requires more planning. but if you have questions let me know. comeback ot me after you are done with M1.
put you in agent mode again
----
### llm

Everything is consistent and fully migrated. M1 is complete. Let me give you a summary of what I did and the design decisions involved.

ALayer.cs
M1 done — summary
What changed:

LayerRow.cs: Id is no longer DB [AutoIncrement] — it's now explicitly assigned by the writer (equal to SortOrder), so the host's in-memory objects know their own id immediately, without a DB read-back. Verified this is safe against sqlite-net's actual insert logic (default CreateFlags.None doesn't force auto-increment on its own).
ALayer.cs: added a new public int LayerId field — the stable, data-sourced identity. Deliberately did not touch the existing LayerIndex/m_layerIndex/Initialise(...) mechanics at all — those stay exactly as they were, since they're a genuinely different concern (render-time vertical offset / material render-queue ordering in VectorLayer.cs), not identity.
SqliteGisDatasetWriter.cs / SqliteGisDataSource.cs: writer assigns LayerId on the in-memory layer object as a side effect of writing (same value as the persisted row); reader populates it on reconstruction.
LayerManager.cs: m_layers converted from List<ALayer> to Dictionary<int, ALayer> keyed by LayerId (mirroring the pattern already used by LayerTagsCanvas.cs's m_layerTags). All identity/lookup usages across LayerManager, LayerTag.cs, LayerTagsCanvas.cs, LayerMenuLayer.cs, UIInGameMenuMapSettings.cs, and the LayerInstanceData/DMInstanceData/LayerTagData network structs (field renamed m_layerIndex→m_layerId) now flow from LayerId, not positional LayerIndex.
One thing worth flagging explicitly: NetworkList storage itself (m_layerNetworkData, etc.) is still positionally indexed internally — I didn't rewrite that into a keyed structure, since NetworkList<T> doesn't natively support that, and doing so would've been a much larger, riskier rewrite than this milestone warranted. What I did instead: every value stored in those positions now comes from LayerId (assigned once, externally, at write time) rather than each process independently deriving its own numbering — list position and id still coincide numerically today (by deliberate construction), but nothing anywhere re-derives that number locally anymore. I also tightened a couple of spots (OnConfigImportComplete, SetLayerVisualisationSetting) to read m_layerId directly off the data instead of relying on a loop counter, removing a couple of implicit position-equals-identity assumptions along the way.

I verified there are no leftover references anywhere in Assets/MSP (comprehensive repo-wide greps, case-insensitive) — only the intentionally-kept render-positional LayerIndex usages remain.

Given I don't have a way to run Unity's compiler directly in this session, could you open the project and check the Console for any compile errors? Once that's confirmed clean, let me know and I'll move on to M2 (the NetworkList-based layer catalogue).





