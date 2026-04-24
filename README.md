# AutoMapper → Mapster Migration

> One-branch migration from AutoMapper to Mapster. No adapter layer. No dual-library window. A PowerShell codemod for the mechanical part, a cheatsheet for the manual part, and five verify gates before you merge.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-6%20%7C%207%20%7C%208-512BD4)](#compatibility)
[![PowerShell](https://img.shields.io/badge/PowerShell-5.1%2B%20%7C%207%2B-5391FE)](#requirements)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](CONTRIBUTING.md)

---

## Why this repo exists

In late 2025, AutoMapper went commercial. Teams that were happy freeloading on a maintained OSS library suddenly had to choose: **pay, freeze, or migrate**.

> **Security update (March 2026):** [CVE-2026-32933](https://nvd.nist.gov/vuln/detail/CVE-2026-32933) — uncontrolled recursion leading to `StackOverflowException` and process termination (CVSS 7.5, High) — is patched **only** in the commercial `15.1.1` and `16.1.1` releases. The last OSS version is unpatched. "Stay on the last OSS version" is materially worse advice than it was in late 2025.

If you're migrating, the most common advice online — _build an adapter interface, run both libraries in parallel, migrate bounded-context by bounded-context over several sprints_ — is wrong for the common case. It introduces the tech debt you were trying to avoid.

This repo contains the opposite playbook: **one branch, one PR, five moves**. It's built around a single observation:

> Mapster ships an `IMapper` interface in the `MapsterMapper` namespace with a `Map<T>(src)` method that is **source-compatible** with `AutoMapper.IMapper.Map<T>(src)`. Every call site in your codebase compiles unchanged once you swap the `using` directive.

That source compatibility is the whole trick. This repo leans on it.

## What's in here

| Path | What it is |
|---|---|
| [`docs/automapper-to-mapster-migration.md`](docs/automapper-to-mapster-migration.md) | The full step-by-step migration guide with the `ForMember` → `Map` cheatsheet and six gotchas that cause silent regressions. |
| [`docs/cheatsheet.md`](docs/cheatsheet.md) | Just the API mapping table. Pin it to your second monitor. |
| [`scripts/migrate-automapper-to-mapster.ps1`](scripts/migrate-automapper-to-mapster.ps1) | PowerShell codemod that rewrites every `using AutoMapper;` to `using MapsterMapper;` across your solution and flags files that need manual attention. |
| [`examples/before-after/`](examples/before-after/) | A minimal `Profile` → `IRegister` rewrite showing the mechanical parts side by side. |

## TL;DR — the five moves

1. **Audit** — count `CreateMap`, `ForMember`, `ReverseMap` call sites. Know the blast radius.
2. **Swap packages** — remove `AutoMapper`, add `Mapster` + `Mapster.DependencyInjection`.
3. **Run the codemod** — one PowerShell script rewrites every `using` directive and flags the files you still need to touch by hand.
4. **Rewrite profiles** — `CreateMap` → `NewConfig`, `ForMember` → `Map`, `ReverseMap` → explicit second `NewConfig` block. This is the only part that needs your brain.
5. **Verify** — compile, tests, round-trip every former `ReverseMap`, nullable-path sanity checks, smoke test.

Elapsed time on a real codebase with ~80 `CreateMap` calls and ~60 `ReverseMap` pairs: **half a day to two days**, dominated by the profile rewrite, not by call-site fixes.

## Quick start

### 1. Audit first (do not skip)

From your solution root:

```powershell
# How many CreateMap calls? (your unit of work)
(Select-String -Path *.cs -Pattern 'CreateMap<' -Recurse).Count

# Advanced features — non-zero means you need the escape hatches in the full guide
(Select-String -Path *.cs -Pattern 'ProjectTo<'     -Recurse).Count
(Select-String -Path *.cs -Pattern 'ITypeConverter' -Recurse).Count
(Select-String -Path *.cs -Pattern 'IValueResolver' -Recurse).Count
(Select-String -Path *.cs -Pattern 'Include<'       -Recurse).Count
```

**Green-light rule:** if `ProjectTo`, `ITypeConverter`, `IValueResolver`, and `Include` all return `0`, this playbook fits. Anything non-zero → read the [Advanced scenarios](docs/automapper-to-mapster-migration.md#advanced-scenarios-read-only-if-your-audit-flagged-them) section first.

### 2. Swap the NuGet packages

```powershell
$projects = @('YourApp.Application', 'YourApp.Infrastructure')

foreach ($proj in $projects) {
    dotnet remove $proj package AutoMapper
    dotnet add    $proj package Mapster
    dotnet add    $proj package Mapster.DependencyInjection
}
```

### 3. Run the codemod

Always dry-run first:

```powershell
# Dry run — prints the report, touches nothing
pwsh ./scripts/migrate-automapper-to-mapster.ps1 -DryRun

# Apply
pwsh ./scripts/migrate-automapper-to-mapster.ps1
```

On a 100-file solution this takes under a second. The report tells you exactly which files still need a manual rewrite (usually only your `Profile` classes).

### 4. Rewrite DI registration

Replace:

```csharp
builder.Services.AddAutoMapper(typeof(MappingProfile).Assembly);
```

With:

```csharp
var config = new TypeAdapterConfig();
config.Scan(typeof(MappingRegister).Assembly);
builder.Services.AddSingleton(config);
builder.Services.AddScoped<IMapper, ServiceMapper>();
```

### 5. Rewrite your `Profile` classes

Use the [cheatsheet](docs/cheatsheet.md) — the full guide has a worked example in [`examples/before-after/`](examples/before-after/).

### 6. Verify

Five gates before you merge: compile, tests, round-trip every former `ReverseMap`, nullable paths, smoke test. Full details in the [verification section](docs/automapper-to-mapster-migration.md#step-6--verify-before-you-merge) of the guide.

## Requirements

- **PowerShell 5.1+** (Windows PowerShell) or **PowerShell 7+** (cross-platform, recommended)
- **.NET 6, 7, or 8** — the migration pattern is runtime-agnostic; the script touches text files
- A codebase that currently uses AutoMapper via its standard `IMapper` injection pattern

## Compatibility

| You're using… | This repo fits? |
|---|---|
| Constructor-injected `IMapper` with `Map<T>(src)` | ✅ Direct fit |
| `ProjectTo<T>` against `IQueryable` | ⚠️ Read [Advanced scenarios](docs/automapper-to-mapster-migration.md#advanced-scenarios-read-only-if-your-audit-flagged-them) first |
| Custom `ITypeConverter` / `IValueResolver` | ⚠️ Read [Advanced scenarios](docs/automapper-to-mapster-migration.md#advanced-scenarios-read-only-if-your-audit-flagged-them) first |
| AutoMapper inheritance mappings (`Include<T>()`) | ⚠️ Manual work required |
| Static `Mapper.Map(...)` calls (pre-IMapper style) | ⚠️ Audit and refactor to injected `IMapper` first |

## The six gotchas

These are the ones that cause **silent** regressions — the dangerous kind. Full details in the [guide](docs/automapper-to-mapster-migration.md#the-six-gotchas).

1. **`TwoWays()` ≠ `ReverseMap()`** — reverse-side `ForMember` overrides get silently dropped.
2. **Null paths throw** — `s.Address.City` with `Address == null` throws `NullReferenceException` instead of returning `null`.
3. **Flattening isn't automatic** — `src.Address.City → dest.AddressCity` was AutoMapper magic; in Mapster you write the `.Map()` explicitly.
4. **`DateTimeOffset ↔ DateTime`** — AutoMapper converted silently. Mapster throws. Register global `.MapWith` converters.
5. **Hidden base members** — DTO hiding a base `DateTime` with `DateTimeOffset` triggers `AmbiguousMatchException`.
6. **`Compile()` is your friend** — call `config.Compile()` in a unit test. It's Mapster's equivalent of `AssertConfigurationIsValid`.

## Why not an abstraction layer?

Because **the abstraction layer is the tech debt you were trying to avoid**. The whole reason `IMapper` is source-compatible between the two libraries is so you don't have to build one. Every extra interface you add during the migration is a layer you'll have to delete later — and in practice, nobody deletes it.

If you need an adapter-layer-first migration path, this is not the repo for you — but please read the [full guide](docs/automapper-to-mapster-migration.md#why-not-just-build-an-abstraction-layer) first, the argument is worth hearing out.

## Contributing

Gotchas you hit on your own migration are gold — please open an issue using the [Gotcha report template](.github/ISSUE_TEMPLATE/gotcha_report.md). See [`CONTRIBUTING.md`](CONTRIBUTING.md) for details.

## License

[MIT](LICENSE) — use it, fork it, ship it. Attribution is appreciated but not required.

## Author

**Sidy Konteye** — Full-Stack .NET Engineer, Dakar.
[github.com/SKonteye](https://github.com/SKonteye)
