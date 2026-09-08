# Temporal contact export implementation journal

Current implementation, verification results and limitations are consolidated in
`TENUS_CONTACT_EXPORT_REPORT.md`. The entries below are historical work notes;
their intermediate "not yet" statements do not describe the final working tree.

Starting HEAD: `9adf3e58ab5e8635a2bdfaba86feaed47bc7b8bc`, branch `Beta`.
Initial worktree was clean. No reset, cleanup, stash, or discard performed.

## Verified baseline (2026-09-08)

`dotnet test src/RealTimeTests/RealTimeTests.csproj -c Release --no-restore --logger 'trx;LogFileName=contact-baseline.trx'`

223 passed, zero failed/skipped. Existing analyzer warnings remain.

`dotnet test src/RealTimeTests/RealTimeTests.csproj -c Release --no-restore --filter FullyQualifiedName~ContactExportBaselineTests --logger 'console;verbosity=detailed'`

Three explicit benchmark cases passed. Log: `src/contact-export-baseline.log` (ignored build artifact).
Each case runs 288 five-minute steps, one continuously occupied workplace, cap 10,
sampling seed 42, building 7, app adoption 60%, manual traceability 80%.
This deliberately synthetic constant-occupancy workload is not a calibrated city.

| Population | Rows/step | Rows/day | Unique partners/person/day | Repeated ratio | Traceable % | Physical bytes | Traceable bytes |
|---:|---:|---:|---:|---:|---:|---:|---:|
|20|100|28,800|19|0.993403|90.4965|2,785,565|2,337,771|
|100|500|144,000|99|0.965625|91.3389|14,122,046|11,977,882|
|1,000|5,000|1,440,000|941.97|0.672927|79.8017|145,460,927|108,019,091|

All three cases have 2,880 contact participations/person/day. Physical bytes/row
including header: 96.7210, 98.0698, 101.0145 respectively.

| Population | Sampling ms | Engine ms | Traceability ms | Separate metrics ms | Recorder including its metrics ms | Finalization ms | Total harness ms |
|---:|---:|---:|---:|---:|---:|---:|---:|
|20|8.9348|5.1586|7.0717|9.8709|34.3582|see log|107.696|
|100|5.0295|12.4380|30.6865|27.9001|163.4686|1.6882|285.572|
|1,000|53.5639|133.3595|298.4225|3547.7237|4931.5584|2.3904|12632.5603|

Single measurements, no warmup/replicate confidence claims. Total includes assertions,
pair-set accounting and a second metrics instance. Recorder includes metrics and
serialization/write; separate serialization and OS-write timings and allocation
measurements remain to be added. These timings are not in-game frame timings.

The code uses the simulation-step key in partner ordering, writes physical steps
and a second traceable schema, and flushes all recorder streams every 256 rows.
For illustration only, scaling this particular 1,000-person workload to 30,000
people in independent identical groups for 30 days gives 228.13 GB decimal before
other outputs (extrapolation; not a measured run).

## Implementation status

First-class episode tracker added with active-only storage, explicit empty-step
closure, context/building/vehicle/district identity, traceability-change splitting,
deterministic closure order for deterministic input, finalization and reset.
It is not yet wired into the recorder or simulation. No simulation behavior changed.
The production framework does not provide System.Tuple; identity uses an explicit
value-type key compatible with the actual project target.

Next: integrate step lifecycle and streaming modes, persistence configuration,
compressed partitions and metadata, metrics/UI/dashboard migration, failure tests,
scientific equivalence harness, before/after benchmarks and DOCUMENTATION.md.
The full requested objective is not complete.

## Continuation: persistence and storage components

Previous turn classified as progress: baseline measurements and episode tracker
changed authoritative state. Rechecked HEAD/status; preserved every existing edit.

ContextWindows persistence is now integrated at all three bounded-sampling call
sites in PandemicManager. Household all-pairs and outdoor geometry remain unchanged.
Eight independent clock-window settings round-trip through scenario snapshots and
hashing. New configurations use experimental 60/30/15-minute windows; configuration
v14 migration preserves old configurations with LegacyPerStep. Legacy scenario
fields default to LegacyPerStep. Occupancy churn can still reconstruct the graph;
this is explicitly documented, not presented as empirical social-cohort calibration.

Standalone ScientificContactStorage now supports Standard episodes, daily FullRaw
partitions, SummaryOnly without individual files, streaming gzip, 64 KiB character
and file buffers, configurable row flush threshold (8192 initially), and a 30-second
flush check on writes. Completed streams are closed before hashing and publication;
interrupted streams remain temporary. A compressed-byte limit fails closed and is
latched. Per-file row counts, time ranges, lengths and SHA-256 are captured.
This writer is not yet connected to ExperimentRecorder/manifest/UI. Recorder still
exports the old raw/traceable files until that integration is complete. The enum is
defined but not yet selectable in batch UI.

`dotnet test src/RealTimeTests/RealTimeTests.csproj -c Release --no-restore --logger 'console;verbosity=normal'`
passed after persistence integration (236 tests), and again after storage additions
(see `src/contact-storage-full-tests.log`). Dedicated storage suite: 5 passed,
covering gzip roundtrip, daily rotation, metadata hashes/counts, temporary files,
interruption, limit failure and existing-file protection. Dedicated persistence
suite: 4 tests (stable window, expiry/seed differences, exact legacy key, context
snapshot roundtrip, and removal from vehicle roster covered across those tests).
An initial test failure expecting configuration version 13 was updated to verify
version 14 and preservation of LegacyPerStep; rerun passed. All builds target the
actual net35 production project through net471 tests; no new dependency was added.

Next required integration: preserve a baseline recorder reference for repeatable
before/after benchmarks; wire episode Begin/EndStep around spread including empty
steps; update recorder modes/finalization and invalid-run semantics; extend metrics,
schema/manifest fingerprinting (currently top-level legacy mandatory files), UI,
dashboard readers and equivalence tests. Add injected filesystem failure tests,
disk reserve checks, deterministic gzip-content tests, and allocation/flush benchmarks.

## Continuation: recorder, manifest, dashboard, equivalence

Preserved HEAD and all prior edits. Previous turn was progress. Recorder now defaults
to Standard, uses the active episode tracker in all modes, and stores raw steps only
in FullRaw. `PandemicManager.spread` explicitly begins/ends contact measurement steps
including an empty citizen-array path. Transmission still resolves at every step.
Recorder disposal closes active episodes into temporary data without publishing.
The old recorder is frozen in `src/RealTimeTests/Reference/LegacyExperimentRecorder.cs`
for rerunning baseline benchmarks. Old baseline tests now explicitly use that class.

Episode summaries/duration histograms are separate from unchanged legacy per-step
network metrics. Scientific contact schema 2 (independent of overall schema 4) is
populated at batch commit, with contact configuration and per-file metadata. The
commit service validates compressed partition identities and rejects temporary
output recursively. Three new integration cases actually run the recorder and
commit Standard/FullRaw/SummaryOnly packages; contact corruption is rejected.

Dashboard aggregation recognizes schema-2 inventories; raw data are lazy through
`contact_reader.py`. Routine discovery does not hash every byte of raw contacts;
explicit full contact verification remains available. Legacy datasets are supported.
Python was located at
`C:/Users/mail/.codex/tmp/tenus-dashboard-20260906/Scripts/python.exe`.
The initial 15 dashboard tests passed before edits; 18 pass after lazy-reader tests.

Verification commands:

- `dotnet test src/RealTimeTests/RealTimeTests.csproj -c Release --no-restore --logger 'console;verbosity=normal'`
  — 246 passed, zero failed, 4.4214 seconds; log `src/contact-integration-tests.log`.
- `dotnet test src/RealTimeTests/RealTimeTests.csproj -c Release --no-restore --filter FullyQualifiedName~ContactExportEquivalenceTests --logger 'console;verbosity=detailed'`
  — 2 passed. Each fixture runs all three export modes for 2,880 five-minute steps
  (10 days), 20 citizens, deterministic sampling, production transmission/disease
  kernel, test state machine, traceability, positive-test isolation, and a scheduled
  death. It asserts nonzero transmissions, recoveries, negative tests, isolation
  events, and one death. All modes have identical per-citizen step states, state
  time series, transmissions, testing records, interventions and network summaries.
  Seed 42 ledger SHA256: `988A501B13B22299C7E2ED777393DCBF64DA7D16D3A54FFAF8B1A5159E11F2C6`.
  Seed 73 ledger SHA256: `21AF103CE8D3AE363EC0F83DE189202BF7685CC4B224ACEFD4D22FF71C930FC3`.
  This is a synthetic orchestration fixture, not an in-game replay or mortality
  calibration test. Its death is scheduled explicitly.
- `& 'C:/Users/mail/.codex/tmp/tenus-dashboard-20260906/Scripts/python.exe' -m unittest discover -s dashboard -p 'test_*.py'`
  — 18 passed, zero failed, 0.109 seconds.
- `dotnet build src/RealTime/RealTime.csproj -c Release --no-restore`
  — succeeded, zero errors, 1,825 analyzer warnings. Log `src/contact-integration-release.log`.
- `git diff --check` — passed.

Intermediate failures were three recorder tests expecting obsolete uncompressed
files, plus one malformed path-separator character literal. Fixed, rerun green.
The old timestamp test now selects FullRaw and verifies decompressed rows exactly;
Standard lifecycle tests assert episode storage and absence of duplicate traceable rows.

Still outstanding for the actual objective:

1. Storage estimation/warnings in batch preflight and clear export-mode UI help;
   mode/limit are already in configuration and generic scenario parameter editor.
2. Disk reserve checks, injectable write-failure testing, failed writer reset/cleanup
   audit, invalid-run archive handling for compressed partitions, strict inventory
   completeness and dashboard metadata/corruption tests.
3. Before/after benchmark across group sizes and modes, allocation/serialization/
   filesystem-flush measurements, safe flush-threshold comparison, scientific
   temporal-statistics inspection. No post-change performance claims yet.
4. Detailed contact analysis UI wiring, legacy configuration-hash compatibility audit,
   aliases for ambiguous raw metric names, episode metrics correctness tests.
5. Full completion audit and final report; in-game behavior remains unverified.

## Continuation: preflight, disk reserve, measured optimization

Re-read objective and revalidated branch/HEAD/status; preserved prior edits. Previous
turn was progress. Added per-scenario storage estimate/category to batch preflight,
the exact required FullRaw warning, disk-space comparison, and mode/limit help in
the generic scenario editor and English localization. Estimate uses current loaded
population as explicitly labelled proxy, configured cap/time/duration/repetitions,
and a broad engineering range. It never changes simulation or blocks on heuristic.

Contact writer now checks measured available space with a 100 MB reserve, first
before creating streams and then at least every MiB (earlier near reserve). Its
free-space callback is injectable: tests verify failure before opening and during
finalization, with no published contact output. A free-space query unavailable on
the platform remains unknown; I/O failures still fail the run. This cannot prevent
unrelated processes from concurrently filling disk.

Added `contact_step_summary.csv` with explicit aliases for legacy metrics. Numeric
rows are unchanged, verified in a regression test. Added episode count/minute/rate
and duration-histogram checks. Found a metrics hotspot from default ulong pair
hashing (high/low XOR collisions); replaced only metrics set hashing with an exact
equality comparer and distributed hash. A multi-day comparison proves all legacy
network output strings are identical. Frozen reference metrics are now test-only.
FullRaw timestamp strings are cached by exact timestamp/kind, retaining exact rows.

Benchmark result artifacts are tracked under `benchmarks/`:

- `contact_export_20260908.csv`: six variants at each of 20/100/1,000 citizens.
- `contact_flush_20260908.csv`: thresholds 256/8192/65536 at 100,000 rows.
- `CONTACT_EXPORT_20260908.md`: measured before/after report and limitations.

At 1,000 citizens, 1,440,000 five-minute steps are preserved. Standard with explicit
persistence writes 117,453 episodes / 961,543 compressed bytes versus legacy
253,480,018 bytes including duplicate traceable rows. FullRaw with legacy mixing
writes every identical raw step / 12,799,390 compressed bytes. Recorder timings
are 5,183.579 ms legacy, 4,087.091 ms FullRaw legacy, 647.688 ms Standard persistent.
See the CSV for allocations/engine/tracing/sampling/total timings. These are single
observations, not statistical speedup guarantees. Do not attribute mixing-driven
improvement to storage alone. Standard legacy mixing allocates more memory than
legacy storage. Workplace unique contacts remain suspicious (203.506/person/day
even with persistence) in this continuous 24-hour fixture; defaults were not retuned.

Current verification: 253 regular C# tests pass; benchmark cases (3) and flush case
(1) explicitly selected and pass; 18 Python tests pass. Release build passes with
existing analyzer warnings. Logs `src/contact-preflight-tests.log`,
`src/contact-preflight-release.log`, `src/contact-export-comparison-final.log`,
`src/contact-flush-benchmark.log`. Exact commands are in the benchmark report.

Remaining work still required: separate serialization/underlying write timings;
multi-context temporal-statistics workloads; deeper arbitrary filesystem failure
injection and failed-recorder reset/invalid archive audit; strict manifest inventory
completeness/metadata-corruption tests; dashboard detailed-analysis UI and schema-2
aggregate reader tests; legacy configuration-hash compatibility; deterministic gzip
verification; final requirement-by-requirement completion audit and final report.

## Continuation: failure integrity and legacy hashes

Read objective, checked current worktree/HEAD and preserved prior work. Previous
turn was progress. Fixed a concrete handle-cleanup risk when gzip disposal throws:
storage now independently owns and finally disposes the underlying output stream.
Added an injectable output stream factory to exercise real underlying Write and
flush-on-close errors. Known failed storage does not retry serialization on disposal;
recorder Reset can start a new run while preserving the old `.tmp` data. Tests verify
old failed data never publishes and the next run has clean episode/contact counts.

Invalid archives now fingerprint nested partitions with relative paths, deterministic
ordering, and recursive temporary-file rejection. Successful commit/revalidation
compares actual contact files to the manifest inventory and rejects extra raw
partitions in all modes. Dashboard validation mirrors the inventory check and checks
representation/compression/partitioning. Added schema-2 tests for all modes, unlisted
files, row-count metadata disagreement, and explicit full-hash corruption detection.
Routine dashboard discovery intentionally remains size-only for individual contacts.

New configuration hashes now use `TENUS-CONFIG-v2/SHA-256`. Old contact-schema packages
with v1 are validated against exactly the old property set, including recursive
scheduled phase hashes. New contact-schema packages require v2. Regression tests
check legacy identities ignore only the newly introduced contact fields, still
change for old scientific inputs, and remain valid through published-run verification.

Verification: 260 regular C# tests passed via
`dotnet test src/RealTimeTests/RealTimeTests.csproj -c Release --no-restore --logger 'console;verbosity=normal'`
(`src/contact-failure-full-tests.log`, 4.6764 s). Ten dedicated storage tests pass,
including repeated gzip byte identity and injected write/flush failures. Twenty
Python dashboard tests pass using the previously located interpreter. An initial
flush-injection fixture did not fail because its Dispose did not emulate flushing;
corrected the fixture to flush on close and confirmed resource cleanup/failure.

Remaining objective work: detailed-contact dashboard UI; serialization/filesystem
timing breakdown; multi-context temporal-statistics/scientific inspection; additional
metadata consistency and runtime integration audit; final requirement-by-requirement
verification/report. In-game replay and Unity-runtime gzip behavior remain unverified.
# Continued verification: dashboard, temporal statistics and metadata

Dashboard now exposes an explicit bounded 100-row contact preview. Tests cover no
read on run selection, explicit callback rendering, bounded iteration/stream closure,
and Dash HTTP layout endpoints. All 24 Python tests pass (0.438 s).

Five multi-context synthetic tests conserve all step exposures and verify closure on
absence; statistics and scientific caveats are in `benchmarks/CONTACT_EXPORT_20260908.md`.
The separate CSV/OS-write timing benchmark passed and is recorded there as well.

Stricter manifest validation now compares export mode, timestep and all eight
persistence windows against the hashed scenario snapshot. This caught a pre-existing
generic dictionary deserialization gap exposed by the new metadata: windows wrote
correctly but read as an empty dictionary. Added concrete string-key dictionary
conversion; all three export-mode commit/readback tests now verify the round trip
and reject deliberate timestep/window mismatches before publishing.

Full C# suite and Release build rerun successfully after that correction; latest
logs are `src/contact-dashboard-temporal-full-tests.log` and
`src/contact-dashboard-temporal-release.log`. Initial new test compilation failed
because an internal enum appeared in a public fixture signature; fixed fixture
accessibility. The stricter check initially failed three roundtrip tests and led to
the dictionary fix; no remaining failures in this run.

Remaining: final runtime integration/requirement audit and report, and any issues
found by that audit. Live Unity replay, UI visual inspection and real-city contact
statistics are not covered by the synthetic/HTTP tests.
