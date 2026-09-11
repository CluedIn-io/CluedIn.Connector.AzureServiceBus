# Migrating a Connector to Multi-Version Targeting

This document tracks the migration of `CluedIn.Connector.AzureServiceBus` from a single-version
build to the multi-version targeting pattern. Modeled on the prior migrations of
`CluedIn.Connector.Dataverse.V2` and 18 other repos in the same effort.

Branch: `feature/multi-version-targeting` (off `develop`).

---

## Overview

| CluedIn version | .NET TFM | Package suffix |
|---|---|---|
| 4.7.0 | net6.0 | `.470` |
| 4.8.0 | net6.0 | `.480` |
| 5.0.0-beta.* | net10.0 | `.500` |

Verified independently against this repo's own `NuGet.config` feeds (no `public` feed needed —
`CluedIn.Core`/`CluedIn.Core.Agent`/`CluedIn.Crawling`/`CluedIn.DataStore` all restore cleanly at
4.7.0/4.8.0): `5.0.0-*` resolves to `5.0.0-beta.576`.

4.6.0 excluded — this repo's source already calls the async, `executionContext`-taking stream APIs
(`IStreamRepository.GetAllStreams`/`SetupConnector`/`GetStreamMappings` in
`AzureServiceBusConnectorComponent.cs`) that only exist from CluedIn 4.7.0 onward.

**Naming note:** `src/`'s only project folder is `src/Connector.SqlServer/`, and
`test/unit/Connector.SqlServer.Test/` — despite this being the AzureServiceBus repo (confirmed via
`git remote -v`). This is pre-existing cruft (this repo appears to have started life as a copy of
the SqlServer connector), not something touched by this migration.

---

## Step 1 — Pipeline template (`azure-pipelines.yml`)

Status: **Done**

Replaced `crawler.build.yml` with `crawler.build.jobs.yml`, targeting 4.7.0/4.8.0/5.0.0-beta.*.
Dropped the top-level `NuGetAuthenticate@0` step (each job in the jobs-template authenticates its
own) and switched pool from `windows-latest` to `ubuntu-22.04`.

**Judgment call, flagged for a human decision:** the original pipeline had a step remapping pipeline
secret variables (`$(ROOTMANAGESHAREDACCESSKEY_CONNECTIONSTRING)` / `$(TESTQUEUE_CONNECTIONSTRING)`)
to the `SERVICEBUS_`-prefixed env var names the integration tests read
(`AzureServiceBusConnectorTests.cs`'s `RootConnectionString`/`TestQueueConnectionString`), because
this repo's integration tests connect to a **real** Azure Service Bus queue, not a mock. The shared
`crawler.build.jobs.yml` template has no hook to inject a custom step like that into its generated
per-leg integration-test jobs. Even if it did, the template runs one job per `multiVersionCluedInTargets`
entry **concurrently**, so three legs would all hit the same single real queue at once — a genuine
collision risk the template's own comments warn is the calling repo's responsibility to rule out, not
something it can verify on a migrating repo's behalf. Rather than guess, `runIngegrationTests`
defaults to `false` now (was `true`). Needs a human decision: either rename the pipeline secrets
directly to the `SERVICEBUS_`-prefixed names and provision a queue per leg (or otherwise isolate
them), or keep integration testing for this repo outside this pipeline.

---

## Step 2 — `Directory.Build.props`

Status: **Done**

Same pattern as every other repo: honours `CluedInMultiVersionTargetFramework` (net10.0 local
fallback), derives `CLUEDIN_V47`/`V48`/`V50` `DefineConstants`, pins `LangVersion` to 13.0.

---

## Step 3 — `Packages.props`

Status: **Done**

- `_CluedIn` guarded.
- `Microsoft.EntityFrameworkCore`/`.InMemory` split 6.0.16 (net6.0) vs 10.0.7 (net10.0+) —
  previously hardcoded at 10.0.7 only, which would not have resolved on net6.0.
- `Microsoft.NET.Test.Sdk`, xunit, and `AutoFixture.Xunit2`/`Xunit3` split the same way every other
  migrated repo's test tooling was split (17.12.0/xunit 2.9.3/AutoFixture.Xunit2 4.18.0 for net6.0;
  18.3.0/xunit.v3 3.2.2/AutoFixture.Xunit3 for net10.0+).

---

## Step 4 — Test projects

Status: **Done**

Two real test projects (`test/unit/Connector.SqlServer.Test`, `test/integration/Connector.AzureServiceBus.Integration.Tests`)
— both needed the conditional xunit v2/v3 `ItemGroup` split and a `GlobalUsings.cs` for
`Xunit.Abstractions` (`ITestOutputHelper` is used constructor-injected throughout both, under a bare
`using Xunit;`, which only resolves it directly under xunit v3).

`NuGet.config` was cased `Nuget.config` — renamed via the two-step `git mv` (Windows
case-insensitivity workaround).

---

## Step 5 — API compatibility audit across 4.7.0 / 4.8.0 / 5.0.0-beta.*

Status: **Done**

Built and ran tests for real against all three legs. Found one genuine break, **not the usual
RestSharp one** (this repo doesn't use RestSharp at all — no HTTP calls, it's a message-bus
connector):

### EasyNetQ 7.x → 8.x transitive break

`CluedIn.DataStore` brings in `EasyNetQ` transitively, and the resolved version differs by CluedIn
generation: **7.8.0-cluedin.7** for 4.7.0/4.8.0, **8.1.7** for 5.0.0-beta.*. EasyNetQ 8.x removed
`IAdvancedBus.IsConnected` (a `bool` property) entirely, replacing it with
`GetConnectionStatus(PersistentConnectionType)` returning a `PersistentConnectionStatus` record
(`Type`/`State`/`ConnectedAt`/`FailureReason`, where `State` is a `PersistentConnectionState` enum:
`NotInitialised`/`Connected`/`Disconnected`).

This only broke `test/unit/Connector.SqlServer.Test/TestContext.cs`'s Moq setup
(`Bus.Setup(s => s.Advanced.IsConnected).Returns(false)`), not any production `src/` code — verified
no other `.IsConnected` usage in `src/`. Confirmed the exact API surface via .NET reflection against
both restored `EasyNetQ.dll` copies (not guessed) before writing the fix:

```csharp
#if CLUEDIN_V50
Bus.Setup(s => s.Advanced.GetConnectionStatus(It.IsAny<EasyNetQ.Persistent.PersistentConnectionType>()))
    .Returns(new EasyNetQ.Persistent.PersistentConnectionStatus(
        EasyNetQ.Persistent.PersistentConnectionType.Producer,
        EasyNetQ.Persistent.PersistentConnectionState.Disconnected,
        null,
        null));
#else
Bus.Setup(s => s.Advanced.IsConnected).Returns(false);
#endif
```

Verified with real `dotnet test` runs (not just `dotnet build`) on both the 5.0.0-beta.*/net10.0 leg
and the 4.7.0/net6.0 leg — all 5 unit tests pass on both.

---

## Step 6 — Reset the semantic version (`GitVersion.yml`)

Status: **Done**

```yaml
next-version: 1.0
ignore:
  commits-before: 2025-05-24T00:00:00
```

Highest pre-existing tag is `v4.5.0`/`4.5.0` at `2025-05-21T15:36:49+01:00` (checked every tag
candidate's real commit date via `git log -1 --format=%aI <tag>`, not tag-name sort order — this
repo has several `vX.Y.Z`-and-`X.Y.Z` tag pairs pointing at the same commit). Padded 2+ days per the
lesson from earlier repos in this effort (GitVersion.Tool 5.9.0 appears to evaluate `commits-before`
against local machine time, not UTC, and fails silently on a too-tight margin). Verified with the
pipeline's actual pinned `GitVersion.Tool 5.9.0`: resolves to `1.0.0-multi-version-targeting.86`.

---

## Step 7 — Push and confirm CI

Status: **Done**

PR #49, build 152001 — fully green on the first push: all three `Multi-version build+test` legs
(4.7.0, 4.8.0, 5.0.0-beta.*) and `Multi-version: publish` passed. No integration-test legs ran
(expected — `runIngegrationTests` defaulted to `false`, see Step 1).

---

## Step 8 — Integration tests re-enabled; concurrency risk tested empirically, didn't materialize

Status: **Done**

Step 1's collision concern was never actually tested — just reasoned about (three legs, one shared
queue, no isolation, the shared template runs them concurrently by design). Tested it for real:

1. Renamed the pipeline's `ROOTMANAGESHAREDACCESSKEY_CONNECTIONSTRING`/`TESTQUEUE_CONNECTIONSTRING`
   secrets to the `SERVICEBUS_`-prefixed plain (non-secret) variables the test code reads directly
   via `Environment.GetEnvironmentVariable` — `crawler.build.jobs.yml`'s auto-generated per-leg jobs
   have no hook to inject the old pipeline's env-var-remapping step, but non-secret pipeline
   variables are auto-exposed to every job's environment without one.
2. First attempt (build 152034) failed all three legs identically with
   `ServiceBusException: Queue was not found (MessagingEntityNotFound)` /
   `"No service is hosted at the specified address"`. Root-caused via a standalone PowerShell script
   hitting the Service Bus management REST API directly (SAS-signed, outside CI) against the
   configured namespace: it had **zero real queues**, and the entity the `TESTQUEUE_CONNECTIONSTRING`
   pointed at came back as an `EventHubDescription`, not a `QueueDescription` - the namespace was an
   Event Hubs namespace (shares the same `.servicebus.windows.net` DNS suffix as Service Bus, a
   well-known gotcha), not a real Service Bus queue namespace. Not a concurrency problem at all -
   these tests could never have passed against that resource regardless of how many legs ran.
3. Re-verified a corrected connection string (a genuine namespace with a real queue) the same way
   before touching the pipeline again: list/create/delete against the root key all succeeded, and
   the target queue resolved as a real `QueueDescription`.
4. Re-ran with the corrected pipeline variables (build 152035, no code change, same commit) - **all
   three integration-test legs passed concurrently**, plus `Multi-version: publish`. Each test run
   creates its own uniquely-named queue (`TestQueueName`, GUID-suffixed) rather than reusing a fixed
   name, so three legs hitting the same namespace simultaneously never actually contend on shared
   state. The originally-assumed collision risk does not materialize in practice with a real,
   correctly-provisioned queue namespace.

`runIngegrationTests` stays defaulted to `true`. No lock scripts, no per-leg resource provisioning,
and no shared-template changes were needed after all.

---

## Checklist

- [x] `azure-pipelines.yml` — switched to `crawler.build.jobs.yml` with `multiVersionCluedInTargets` (4.7.0, 4.8.0, 5.0.0-beta.*)
- [x] `Directory.Build.props` — honours `CluedInMultiVersionTargetFramework`; `DefineConstants` derived; `LangVersion` pinned to 13.0
- [x] `Packages.props` — `_CluedIn` guarded; EF Core and xunit/AutoFixture split by TFM
- [x] `NuGet.config` — renamed from `Nuget.config`
- [x] Test projects — xunit v2/v3 split; `GlobalUsings.cs` for `Xunit.Abstractions` in both
- [x] Source — audited across all three legs; one real break found and fixed (EasyNetQ 7.x→8.x `IsConnected`→`GetConnectionStatus`, test-only); verified with real `dotnet test` on two legs
- [x] `GitVersion.yml` — `next-version: 1.0`; `ignore.commits-before: 2025-05-24T00:00:00`; verified `1.0.0` with the pinned GitVersion.Tool 5.9.0
- [x] Integration tests — re-enabled (`runIngegrationTests` defaulted back to `true`); real live-queue collision risk tested empirically across three concurrent legs and confirmed it does not materialize, after fixing an unrelated wrong-resource-type problem with the pipeline's connection strings
- [x] Pushed branch and confirmed the Azure DevOps pipeline is green end-to-end — PR #49, build 152035: all three legs + `Integration tests` (all three) + `Multi-version: publish` passed
