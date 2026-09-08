# Measured contact export comparison, 2026-09-08

Source data: `contact_export_20260908.csv`, `contact_flush_20260908.csv`.
Additional source data: `contact_io_20260908.csv`, `contact_thirty_day_20260908.csv`.
Current working tree based on `9adf3e58ab5e8635a2bdfaba86feaed47bc7b8bc`.
These are synthetic engineering measurements, not observed city contacts.

Command after Release test build:

`dotnet test src/RealTimeTests/RealTimeTests.csproj -c Release --no-build --filter FullyQualifiedName~CompareLegacyAndContactModes --logger 'console;verbosity=detailed'`

Three population cases passed. Each runs six variants over 288 five-minute steps
in one continuously occupied workplace with cap 10, sampler seed 42, building 7,
60% app adoption and 80% manual traceability. Persistent variants use the explicit
60-minute clock window. Legacy variants use exactly the old timestep key.
The legacy recorder and pre-optimization metrics are frozen test-only references.

## 1,000-person example

| Variant | Physical steps | Stored rows | Contact bytes stored | Uncompressed contact bytes | Recorder ms | Total harness ms | Allocated bytes |
|---|---:|---:|---:|---:|---:|---:|---:|
| Legacy, legacy mixing | 1,440,000 | 1,440,000 physical plus traceable duplicates | 253,480,018 | 253,480,018 | 5,183.579 | 9,157.721 | 1,024,465,600 |
| FullRaw, legacy mixing | 1,440,000 | 1,440,000 | 12,799,390 | 145,460,927 | 4,087.091 | 8,002.702 | 1,129,310,968 |
| Standard, legacy mixing | 1,440,000 | 1,424,952 episodes | 11,647,033 | 136,819,296 | 4,473.744 | 8,278.698 | 2,164,928,392 |
| FullRaw, persistent mixing | 1,440,000 | 1,440,000 | 12,817,563 | 145,460,927 | 2,777.591 | 3,613.302 | 995,256,768 |
| Standard, persistent mixing | 1,440,000 | 117,453 episodes | 961,543 | 11,376,809 | 647.688 | 1,511.998 | 647,781,856 |
| SummaryOnly, persistent mixing | 1,440,000 | 0 individual rows | 0 | 0 | 466.445 | 1,394.181 | 522,445,824 |

Standard persistent contact storage is approximately 263.6 times smaller than the
legacy physical-plus-traceable files on this workload. This combines two different
changes: storage optimization and explicit partner persistence. The FullRaw legacy
mixing comparison isolates storage/measurement implementation changes: raw rows and
contact partners remain unchanged, with approximately 19.8 times smaller contact
storage. Standard with legacy mixing barely reduces row count and allocates more
than the legacy recorder. Do not generalize the persistent result to arbitrary
occupancy patterns or claim that compression is free.

For the 1,000-person baseline versus Standard persistent case, sampling was
57.854/57.998 ms, ContactEngine 153.138/160.269 ms, and tracing 336.853/349.089 ms.
There is no measured ContactEngine speedup. Recorder time includes metrics,
episode tracking, serialization, buffering and writes; finalization is separate
(1.692/23.967 ms). Full stored files are closed and hashed before finalization ends.

The total harness also includes unique-pair accounting in a separate HashSet and
allocation of requests. It is not simulation frame time and does not include a
disease calculation. Allocations are measured with the runtime's
`GC.GetAllocatedBytesForCurrentThread` API via reflection; -1 would mean unavailable.
Compression size excludes the small transmission header and all aggregate outputs.
Uncompressed bytes are counted by streaming decompression after the timed interval.

All timings are single sequential observations without warmup or confidence
intervals. Small differences can be noise. The first 20-person legacy case includes
JIT/setup effects. See the CSV for every 20/100/1,000-person result.

## Flush threshold measurement

`dotnet test src/RealTimeTests/RealTimeTests.csproj -c Release --no-restore --filter FullyQualifiedName~CompareSafeFlushThresholds --logger 'console;verbosity=detailed'`

100,000 FullRaw rows, same data and gzip buffers:

| Flush threshold | Compressed bytes | Write plus close/hash ms |
|---:|---:|---:|
| 256 | 735,252 | 142.685 |
| 8,192 | 735,252 | 120.011 |
| 65,536 | 735,252 | 120.077 |

Keep 8,192: this single measurement does not justify the larger row threshold.
The memory buffers remain bounded independently of row count. This is managed
stream throughput, not durable-device latency or a filesystem fsync benchmark.

## Scientific interpretation

At 1,000 citizens, legacy mixing reaches 941.97 unique partners/person/day versus
203.506 with the experimental 60-minute workplace window. Both are suspiciously
large for use as an unexamined workplace assumption. The cap remains 10 and both
have 2,880 contact participations/person/day. Standard persistent has 234.906
episode participations/person/day and 14,400 episode participation minutes/person/day
(10 concurrent partners over 24 hours). Pair-minutes count simultaneous contacts;
they are not wall-clock minutes. Repeated-step ratio changes from 0.672927 to
0.929338. A high repeated-step ratio alone does not establish realistic mixing.

No parameter was changed because these values looked suspicious. Continuous
24-hour workplace occupancy is deliberately synthetic. Context caps, turnover
windows, occupancy schedules/churn, household all-pairs assumptions, outdoor range,
transit turnover and traceability all still require empirical validation. School,
household, transit and outdoor statistics need separate workloads/in-game review.

Remaining benchmark gaps: separate serialization and filesystem API timings,
full stepwise metrics timing after optimization, and multi-context temporal-statistics
inspection. These results do not complete the overall task by themselves.
# Additional temporal and I/O checks

`TemporalContactStatisticsTests` uses 100 citizens, cap 10, seed 42, eight hours
of unchanged occupancy, five-minute steps and 60-minute windows. Both school and
workplace produce 48,000 contact steps, 3,517 episodes and 2,743 unique pairs:
54.86 unique partners/person/day, 70.34 episodes/person/day, 4,800 pair-exposure
minutes/person/day and repeated-step ratio 0.9428541667. All step counts and exposure
minutes are conserved. This fixture has identical occupancy and window settings in
both contexts, hence identical results; it is not empirical validation. The high
unique-partner count and ten concurrent contacts warrant calibration. Exposure
minutes sum across simultaneous partners and are not elapsed clock minutes.

Scripted co-location fixtures produce one household episode of 480 minutes, one
transit episode of 30 minutes and one outdoor episode of 10 minutes; the first absent
step closes each. Those durations are fixture inputs, not measured city behavior.
Unity occupancy acquisition and pedestrian proximity still require in-game validation.

Command: `dotnet test src/RealTimeTests/RealTimeTests.csproj -c Release --no-restore --filter FullyQualifiedName~TemporalContactStatisticsTests --logger 'console;verbosity=detailed'`.
Five tests passed; log `src/contact-temporal-statistics.log`.

`MeasureSerializationAndFilesystemCalls` separately measures 100,000 simple raw rows
using the production CSV formatter, bounded streams and the same filesystem:

| Operation | Bytes | Total ms | FileStream.Write ms | Flush/close ms |
|---|---:|---:|---:|---:|
| Format + UTF-8 encode to null stream | — | 62.169 | — | — |
| Plain CSV, flush 256 rows | 9,588,895 | 57.986 | 0.079 | 3.390 |
| Gzip CSV, flush 8,192 rows | 282,733 | 103.678 | 0.250 | 0.132 |

These are sequential microbenchmarks without warmup/statistical replication. The
null sink ran first; its longer time than plain CSV is not negative I/O overhead.
FileStream measurements include buffering/OS cache, not physical durable-media
latency. Gzip costs CPU on this unchanged row workload; the large overall recorder
speedup reported elsewhere depends on fewer stored episodes and metric hashing.
No subtraction of these independent timings is used to claim compression-only cost.
Command: `dotnet test src/RealTimeTests/RealTimeTests.csproj -c Release --no-restore --filter FullyQualifiedName~MeasureSerializationAndFilesystemCalls --logger 'console;verbosity=detailed'`.
One explicit test passed; log `src/contact-io-breakdown.log`.

## Thirty-day export validation

`ContactThirtyDayExportTests` runs all 8,640 configured steps with 100 citizens,
cap 10, seed 42, unchanged workplace occupancy from 08:00–16:00 daily and explicit
empty steps otherwise. All modes observe 1,440,000 contacts and measure 105,710
episodes. Standard stores 654,825 bytes in one episode file; FullRaw stores 9,639,590
bytes across 30 daily partitions; SummaryOnly stores no individual contact bytes.
Write/finalize timings are 521.753/2,181.817/327.313 ms respectively (include sampling
and recorder). Streaming decompression after timing verifies all represented steps,
per-partition row counts and absence of temporary files. This isolates export
longevity, not disease behavior, population realism or peak live memory.

Command: `dotnet test src/RealTimeTests/RealTimeTests.csproj -c Release --no-restore --filter FullyQualifiedName~ContactThirtyDayExportTests --logger 'console;verbosity=detailed'`.
Three explicit cases passed; log `src/contact-thirty-day.log`.
