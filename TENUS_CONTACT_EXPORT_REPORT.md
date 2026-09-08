# TENUS temporal contact export: implementation and verification

Working tree based on branch `Beta`, HEAD
`9adf3e58ab5e8635a2bdfaba86feaed47bc7b8bc`. Existing work was preserved; no reset,
revert, clean, stash or commit was performed. This report supersedes status statements
in the historical `TENUS_CONTACT_EXPORT_IMPLEMENTATION.md` journal.

## Root cause and baseline

The old model observed contacts at every configured epidemiological step, keyed
bounded partner sampling by that step's ticks, wrote one physical row per observation,
duplicated traceable observations into a second full schema, and flushed recorder
streams every 256 rows. The problem combined rapid mixing with repetitive storage.

Before behavior changes, 223 existing C# tests passed. Three synthetic populations
(20, 100, 1,000) were measured at 288 five-minute steps/day, workplace cap 10, seed42.
The 1,000-person case generated 5,000 physical rows/step, 1,440,000/day,
2,880 participations/person/day, 941.97 unique partners/person/day and repeat ratio
0.672927. Physical CSV was 145,460,927 bytes/day (101.0145 bytes/row including header);
traceable CSV was 108,019,091 bytes/day, duplicating 79.8017% of physical observations.
Thirty identical independent groups for 30 days extrapolate to 228.13 GB decimal.
This explains how output exceeds 100 GB; it is not a measurement of a particular
historical city. Full baseline per-size timings are in the implementation journal.

## Model and configuration

`ContactEpisodeTracker` retains active episodes only. Identity includes canonical
citizens, context, building, vehicle and district. Contiguous observations extend;
absence, gap, location/context change or traceability change closes the episode.
Completed episodes stream with deterministic IDs/order for deterministic input.
Finalization closes active episodes; reset/unload closes or leaves invalid temporary
data and clears state. Outdoor membership still uses actual 3D proximity each step;
households still require co-located family membership and being at home.

Transmission is evaluated at every original timestep. Episodes are never supplied
to transmission calculations. Neither timestep, contact caps, hourly probabilities,
disease progression nor tracing was reduced to save storage.

`ContextWindows` stabilizes deterministic bounded partners within a configurable
clock window for unchanged occupancy. Experimental defaults: school/university/work
60 minutes, healthcare30, commercial/leisure/transit/residential shared15. Zero means
legacy resampling. Roster changes may reconstruct other partners; this is not a
permanent classroom cohort. Legacy configurations migrate to `LegacyPerStep` and
retain the exact old sampling key. Old caps keep their original operational meaning;
future daily-target semantics require a separate calibrated model/version.

New configuration version14, contact export schema2, and configuration hash algorithm
v2 record all model and export choices. Legacy hash v1 remains verifiable for old
contact schemas. Dictionary metadata now correctly round-trips through JSON.

## Storage and performance

Measured 1,000-person continuously occupied workplace, one simulated day:

| Variant | Stored rows | Uncompressed bytes | Stored bytes | Recorder ms | Total harness ms | Allocated bytes |
|---|---:|---:|---:|---:|---:|---:|
| Legacy | 1,440,000 + traceable duplicates | 253,480,018 | 253,480,018 | 5,183.579 | 9,157.721 | 1,024,465,600 |
| FullRaw, legacy mixing | 1,440,000 | 145,460,927 | 12,799,390 | 4,087.091 | 8,002.702 | 1,129,310,968 |
| Standard, persistent mixing | 117,453 | 11,376,809 | 961,543 | 647.688 | 1,511.998 | 647,781,856 |
| FullRaw, persistent mixing | 1,440,000 | 145,460,927 | 12,817,563 | 2,777.591 | 3,613.302 | 995,256,768 |
| SummaryOnly, persistent mixing | 0 | 0 | 0 | 466.445 | 1,394.181 | 522,445,824 |

Standard persistent contact bytes are 263.6x smaller; that combines an explicit model
change with storage changes. FullRaw legacy mixing isolates unchanged-contact storage
at 19.8x smaller. Standard with legacy mixing stores 1,424,952 episodes and allocates
2,164,928,392 bytes: aggregation is not a universal allocation improvement.
ContactEngine baseline/Standard-persistent time was 153.138/160.269 ms; no engine
speedup is claimed. Sampling57.854/57.998, tracing336.853/349.089 ms. Recorder and total
times are inclusive synthetic timings, not in-game frame times or disease benchmarks.
Separate formatting/OS-I/O and flush-threshold measurements are in the benchmark report.
All observations are single runs without confidence intervals; files exclude small
noncontact outputs. Allocations measure allocated bytes, not peak live heap.

The additional 30-day/100-person/eight-hours-per-day workload preserved 1,440,000
observed steps in every mode: Standard105,710 rows/654,825 compressed bytes/521.753ms;
FullRaw1,440,000 rows/9,639,590 bytes/2,181.817ms in30 partitions; SummaryOnly0 individual
bytes/327.313ms. Those times include sampling, recorder and finalization. Streaming
decompression subsequently verified every represented step and per-file row count.

Gzip uses bounded 64KiB character/file buffers, flush threshold8192 and a 30-second
check on writes. All streams close before hashes/publication. Failure/size-limit/disk
reserve checks leave incomplete data; no silent truncation or success is possible.
Daily raw partitions carry path/size/SHA256/row count/time ranges. Manifest checks
reconcile rows with totals and mode/timestep/windows with the scenario snapshot.
Standard is the default; FullRaw is explicit; SummaryOnly retains scientific aggregates
and transmission/testing/intervention data. No duplicate traceable contact dataset is
written. Flags remain authoritative, and episodes split when flags change.

## Scientific equivalence

`ContactExportEquivalenceTests` runs all modes for2,880 five-minute steps,20 citizens,
seeds42/73 using production disease/transmission/testing kernels. It compares every
citizen's step states plus serialized state time series, transmission events, tests,
interventions and contact metrics. Fixtures assert nonzero transmissions/recoveries,
negative tests/isolation and one scheduled death. Matching scientific ledger SHA256:

- 42: `988A501B13B22299C7E2ED777393DCBF64DA7D16D3A54FFAF8B1A5159E11F2C6`
- 73: `21AF103CE8D3AE363EC0F83DE189202BF7685CC4B224ACEFD4D22FF71C930FC3`

This proves storage-mode equivalence on the specified synthetic inputs. It does not
claim identical epidemics between legacy mixing and the explicit persistence model.
Live game replay remains a deployment validation item, not something these tests ran.

## Requirement evidence

| Definition-of-done items | Inspected implementation and verification |
|---|---|
| 1: quantify | Initial223-test baseline, `ContactExportBaselineTests`, three-size measured tables/CSV |
| 2–3: episodes/Standard rows | `ContactEpisode`, `ContactEpisodeTracker`; one/four-step, discontinuity, deterministic order, reset/finalization tests; actual gzip roundtrip |
| 4–5: step transmission/equivalence | `PandemicManager.spread` still queues/resolves each step; two-seed three-mode scientific ledger tests |
| 6: persistence | Three production sampling callsites use `ContactPersistencePolicy`; window/seed/expiry/vehicle-roster tests; actual outdoor predicate departure/return test |
| 7–10: three modes and long runs | Config/UI/recorder/storage integration; actual30-day three-mode export test; same exposure counts,30 raw partitions |
| 11: traceable duplication | Recorder no longer creates duplicate file; authoritative flag filter tests and Standard traceability splitting |
| 12–13: compression/partitions | `ScientificContactStorageTests`: gzip bytes/roundtrip, partitions, limits, interrupted/failed writes, hash/footer/temp tests |
| 14: dashboard | Legacy and schema2 inventory/readers, bounded chunks, explicit100-row preview;24 Python tests including HTTP layout and no eager read |
| 15: metrics | Legacy-vs-current full daily/degree/duration/age/hour equality test; separate episode rates/histogram tests; explicit step aliases |
| 16–17: version/metadata | Contact schema2, config14, hashv2; all modes actual recorder→commit→readback; mismatch/corruption/inventory/count/hash tests |
| 18: UI/safety | `BuildPreflight`, exact FullRaw warning, category/range/free-space estimate; optional compressed-byte cap and100MB reserve failure tests |
| 19–20: tests/build | Final266 C# tests,24 Python tests; Release build0 errors; explicit benchmarks separately selected and passed |
| 21: benchmarks | Three-size six-variant CSV, flush and filesystem breakdowns,30-day results |
| 22: simulation invariant | Diff audit: no timestep/cap/probability reduction; mode used only in recorder/storage/config metadata; seeds do not depend on config hash |
| 23: calibration | Named limitations below and exact required statement in `DOCUMENTATION.md` |

Lifecycle audit inspected normal `FreezeRun`, per-step error handling via
`HandleSimulationFailure`, unload and reset. Writer errors mark scientific integrity
failed and stop simulation; unload logs close failures and still releases singleton
state. Temporary data cannot pass successful commit. Testing does not simulate Unity's
native loading callbacks; that limitation is explicit.

## Commands and results

From the repository root:

```powershell
dotnet test src/RealTimeTests/RealTimeTests.csproj -c Release --no-restore --logger 'console;verbosity=normal'
dotnet build src/RealTime/RealTime.csproj -c Release --no-restore
& 'C:/Users/mail/.codex/tmp/tenus-dashboard-20260906/Scripts/python.exe' -m unittest discover -s dashboard -p 'test_*.py'
git diff --check
```

Final regular suite266 passed/0 failed (4.6809s), dashboard24 passed/0 failed
(0.408s). Release succeeded with0 errors and1,903 analyzer warnings. Warnings remain;
this is not a warning-clean build. Logs: `src/contact-final-audit-tests.log` and
`src/contact-final-audit-release.log`. Benchmark fixtures are `[Explicit]` and excluded
from the default suite, not silently treated as passing. Selected commands below
passed3 baseline cases,3 comparison cases,1 flush case,1 I/O case and3 long-run cases:

```powershell
dotnet test src/RealTimeTests/RealTimeTests.csproj -c Release --no-restore --filter FullyQualifiedName~ContactExportBaselineTests --logger 'console;verbosity=detailed'
dotnet test src/RealTimeTests/RealTimeTests.csproj -c Release --no-build --filter FullyQualifiedName~CompareLegacyAndContactModes --logger 'console;verbosity=detailed'
dotnet test src/RealTimeTests/RealTimeTests.csproj -c Release --no-restore --filter FullyQualifiedName~CompareSafeFlushThresholds --logger 'console;verbosity=detailed'
dotnet test src/RealTimeTests/RealTimeTests.csproj -c Release --no-restore --filter FullyQualifiedName~MeasureSerializationAndFilesystemCalls --logger 'console;verbosity=detailed'
dotnet test src/RealTimeTests/RealTimeTests.csproj -c Release --no-restore --filter FullyQualifiedName~ContactThirtyDayExportTests --logger 'console;verbosity=detailed'
```

Intermediate failures were fixed and rerun: old raw-file expectations, net35 tuple
availability, fixture accessibility/reference issues, failure-injection fixture behavior,
and persistence dictionary readback. No failing automated tests remain in final runs.

## Scientific and deployment limitations

The eight-hour100-person school/workplace fixture produces54.86 unique partners,
70.34 episodes and4,800 summed partner-exposure minutes/person/day. Those high values
are flagged; parameters were not automatically retuned. Simultaneous partner minutes
can exceed clock minutes. Household480min/transit30min/outdoor10min durations in the
scripted fixtures are inputs, not empirical distributions. The outdoor geometry test
also checks exact range inclusion, vertical separation, departure and return.

Required calibration: every context's operational cap and turnover window; cohort
size/roster churn; household co-location/all-pairs assumption; shared-area multiplier;
transit occupancy/turnover; outdoor range and observation duration; tracing adoption
and manual traceability. No literature calibration claim is made.

Contact persistence and contact-rate parameters are model assumptions until calibrated or validated against external empirical contact data.

In-game validation remains necessary before thesis production: real-city occupancy
and unique-contact distributions, native reload/failure UI, visual preflight usability,
and gzip behavior on the deployed Unity/Mono runtime. The production net35 build and
Windows net471 synthetic tests pass; they are not a live Unity run. Standard size
depends on churn; legacy mixing can erase most episode row savings. The disk estimate
is a broad heuristic, and a reserve cannot protect against concurrent unrelated writers.
Dashboard discovery checks contact sizes but does not scan full raw hashes; explicit
full verification is available. Raw detailed analysis is streamed and opt-in.
