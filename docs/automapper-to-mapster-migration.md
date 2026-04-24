# From AutoMapper to Mapster in One Branch: A Production-Safe Migration Guide

*A mechanical, scripted path to swap AutoMapper for Mapster in any .NET 6/7/8 codebase — without a dual-library window, without bespoke abstractions, and without the "we'll finish it next sprint" tax.*

---

## Why this guide exists

In late 2025 AutoMapper moved to a commercial license. Teams that were happy freeloading on a maintained OSS library suddenly had to choose: pay, stay on an unmaintained snapshot, or migrate. **Mapster** is the least disruptive of the free replacements — it ships an `IMapper` interface with a `Map<T>(src)` method that is *source-compatible* with AutoMapper's API, which means most call sites don't need to change at all. Only the `using` directive and the mapping configuration do.

That source compatibility is the whole trick. This guide leans on it to do the migration in **one branch, one PR**, with a PowerShell codemod handling the mechanical part and a short cheatsheet handling the semantic part.

It is deliberately opinionated. It targets the common case: a single `Profile` (or a handful), heavy `ForMember`/`ReverseMap` use, and call sites that uniformly do `_mapper.Map<TDest>(src)` via constructor-injected `IMapper`. If you have `ProjectTo<T>` IQueryable projections, `IValueResolver`s, or inheritance mappings (`Include<>`), you need extra work — see the *Advanced scenarios* section at the end.

---

## TL;DR — the five moves

1. **Audit** — count your `CreateMap`, `ForMember`, `ReverseMap`, and `IMapper` call sites. Know the blast radius before you start.
2. **Swap packages** — remove `AutoMapper`, add `Mapster` + `Mapster.DependencyInjection`.
3. **Run the codemod** — one PowerShell script rewrites `using AutoMapper;` to `using MapsterMapper;` across the solution and flags anything it can't handle.
4. **Rewrite DI registration and one `Profile` at a time** — `CreateMap` → `NewConfig`, `ForMember` → `Map`, `ReverseMap` → `TwoWays`.
5. **Verify** — compile, run tests, round-trip the mappings that had `ReverseMap`, and spot-check nullable nested paths.

If your codebase matches the common case above, elapsed time is typically **half a day to two days**, dominated by the profile rewrite — not by call-site fixes.

---

## Step 0 — Pre-flight audit (do not skip)

Before you touch a single `.csproj`, get the numbers. They tell you whether this guide fits your codebase or whether you need to reach for one of the advanced variants.

Run these from your solution root. They are grep queries against `*.cs`, and on a large solution the whole audit takes under a minute.

```powershell
# How many Profile subclasses?
Select-String -Path *.cs -Pattern ': Profile' -Recurse |
    Select-Object -ExpandProperty Path -Unique

# How many CreateMap calls? (your migration unit of work)
(Select-String -Path *.cs -Pattern 'CreateMap<' -Recurse).Count

# Advanced features that need manual handling
(Select-String -Path *.cs -Pattern 'ForMember\('     -Recurse).Count
(Select-String -Path *.cs -Pattern 'ReverseMap\('    -Recurse).Count
(Select-String -Path *.cs -Pattern 'ProjectTo<'      -Recurse).Count  # IQueryable — SEE ADVANCED
(Select-String -Path *.cs -Pattern 'ITypeConverter'  -Recurse).Count  # SEE ADVANCED
(Select-String -Path *.cs -Pattern 'IValueResolver'  -Recurse).Count  # SEE ADVANCED
(Select-String -Path *.cs -Pattern 'Include<'        -Recurse).Count  # inheritance — SEE ADVANCED

# Call-site count — mostly informational, this codemod handles them
(Select-String -Path *.cs -Pattern '_mapper\.Map<'   -Recurse).Count
```

**Green-light rule:** if `ProjectTo<`, `ITypeConverter`, `IValueResolver`, and `Include<` all return `0`, you are in the easy lane — keep reading. If any of them are non-zero, jump to *Advanced scenarios* first and decide whether the one-branch approach is still appropriate.

---

## Step 1 — Swap the NuGet packages

Do this on every project that currently references AutoMapper — usually your `Application` and `Infrastructure` layers in a Clean Architecture solution.

```powershell
$projects = @('YourApp.Application', 'YourApp.Infrastructure')

foreach ($proj in $projects) {
    dotnet remove $proj package AutoMapper
    dotnet add    $proj package Mapster
    dotnet add    $proj package Mapster.DependencyInjection
}
```

`Mapster` is the core library. `Mapster.DependencyInjection` provides the `ServiceMapper` class that implements `IMapper` as a scoped DI service. That is the one piece that makes `_mapper.Map<T>(src)` keep working unchanged.

**Do not** add `Mapster.EFCore` yet. You only need it if you use `ProjectToType<T>` against an `IQueryable` — if you were not using AutoMapper's `ProjectTo<T>`, you do not need it now either.

---

## Step 2 — The `using` codemod

This is the part that makes the migration tractable. Mapster's `IMapper` lives in the `MapsterMapper` namespace (the `Mapster` namespace holds `TypeAdapterConfig` and config extensions). So the mechanical move is: every file that said `using AutoMapper;` now says `using MapsterMapper;`, and profile files additionally need `using Mapster;`.

The script lives at `scripts/migrate-automapper-to-mapster.ps1`. Run it from the solution root.

**How to run it safely:**

```powershell
# First: dry run. Review the list before you let it touch anything.
pwsh ./scripts/migrate-automapper-to-mapster.ps1 -DryRun

# Once the report looks right:
pwsh ./scripts/migrate-automapper-to-mapster.ps1
```

On a 100-file solution this takes under a second. The report tells you exactly which files need the manual rewrite in Step 4.

**Why regex and not Roslyn?** For a migration this mechanical, regex is sufficient and has zero setup cost. Roslyn-based codemods are the right tool if you also want to *translate* `ForMember` bodies — but profile bodies are few, varied, and more safely rewritten by hand against a cheatsheet.

---

## Step 3 — Rewrite DI registration

Find your AutoMapper registration. In most solutions it is in `Program.cs` or a `ServiceCollectionExtensions` file and looks like one of:

```csharp
builder.Services.AddAutoMapper(typeof(MappingProfile));
// or
builder.Services.AddAutoMapper(typeof(MappingProfile).Assembly);
```

Replace it with the canonical Mapster DI block:

```csharp
// --- Mapster ---
var mapsterConfig = new TypeAdapterConfig();

// Scan the assembly for IRegister classes (the equivalent of Profile scanning)
mapsterConfig.Scan(typeof(MappingRegister).Assembly);

builder.Services.AddSingleton(mapsterConfig);
builder.Services.AddScoped<IMapper, ServiceMapper>();
```

Two things to know:

- **`TypeAdapterConfig` is a singleton.** Compile your mappings once at startup, reuse forever. This is exactly how AutoMapper's `MapperConfiguration` worked.
- **`ServiceMapper` lifetime is your call.** Scoped is the safe default and matches AutoMapper's usual registration. Use transient only if you are injecting transient services into your mapping callbacks. Singleton is only safe if every dependency pulled via `MapContext.Current.GetService<T>()` is also singleton.

Add the usings at the top of the file:

```csharp
using Mapster;
using MapsterMapper;
```

That is the entire DI change. No `AddAutoMapper` extension method to find an equivalent for — the three lines above are the equivalent.

---

## Step 4 — Convert `Profile` to `IRegister`

This is the part the script cannot do for you. Good news: the syntax is nearly one-for-one, and if you line up a cheatsheet next to your editor you will cruise through it.

### Before (AutoMapper)

```csharp
public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<User, UserDto>()
            .ForMember(d => d.FullName, o => o.MapFrom(s => $"{s.FirstName} {s.LastName}"))
            .ForMember(d => d.Email,    o => o.MapFrom(s => s.EmailAddress))
            .ForMember(d => d.Audit,    o => o.Ignore())
            .ReverseMap()
            .ForMember(d => d.FirstName,    o => o.Ignore())
            .ForMember(d => d.LastName,     o => o.Ignore())
            .ForMember(d => d.EmailAddress, o => o.MapFrom(s => s.Email));

        CreateMap<Order, OrderDto>()
            .ForMember(d => d.Total, o => o.MapFrom(s => s.Items.Sum(i => i.Price)))
            .AfterMap((src, dst) => dst.GeneratedAt = DateTime.UtcNow);
    }
}
```

### After (Mapster)

```csharp
using Mapster;

public sealed class MappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // User -> UserDto
        config.NewConfig<User, UserDto>()
            .Map(d => d.FullName, s => s.FirstName + " " + s.LastName)
            .Map(d => d.Email,    s => s.EmailAddress)
            .Ignore(d => d.Audit);

        // UserDto -> User (explicit reverse; TwoWays is NOT safe when the reverse
        // side needs its own overrides — write it out instead)
        config.NewConfig<UserDto, User>()
            .Ignore(d => d.FirstName)
            .Ignore(d => d.LastName)
            .Map(d => d.EmailAddress, s => s.Email);

        // Order -> OrderDto
        config.NewConfig<Order, OrderDto>()
            .Map(d => d.Total, s => s.Items.Sum(i => i.Price))
            .AfterMapping((src, dst) => dst.GeneratedAt = DateTime.UtcNow);
    }
}
```

### The cheatsheet — pin this to your second monitor

| AutoMapper                                         | Mapster                                              | Notes |
|---|---|---|
| `Profile` subclass                                  | `IRegister` implementation                           | Scanned the same way |
| `CreateMap<S,D>()`                                  | `config.NewConfig<S,D>()`                            | Called on the `TypeAdapterConfig` argument |
| `.ForMember(d => d.X, o => o.MapFrom(s => s.Y))`   | `.Map(d => d.X, s => s.Y)`                           | Shorter and less ceremonious |
| `.ForMember(d => d.X, o => o.Ignore())`            | `.Ignore(d => d.X)`                                  | |
| `.ForMember(d => d.X, o => o.NullSubstitute("-"))`  | `.Map(d => d.X, s => s.X ?? "-")`                    | No dedicated method — use a lambda |
| `.AfterMap((s, d) => ...)`                          | `.AfterMapping((s, d) => ...)`                       | Note the `-ping` suffix |
| `.BeforeMap((s, d) => ...)`                         | `.BeforeMapping((s, d) => ...)`                      | Same |
| `.ReverseMap()` (no overrides either side)          | `.TwoWays()`                                         | **Only** when neither direction needs custom overrides |
| `.ReverseMap()` + reverse overrides                 | A second explicit `config.NewConfig<D,S>()` block   | Safer and more legible |
| `CreateMap<Base,BaseDto>().Include<Derived,DerivedDto>()` | `config.NewConfig<Base,BaseDto>().Include<Derived,DerivedDto>()` | Identical shape |
| `services.AddAutoMapper(typeof(Profile).Assembly)` | `config.Scan(typeof(Register).Assembly)` + register `TypeAdapterConfig` as singleton | See Step 3 |

### The four gotchas that cause regressions

1. **`ReverseMap()` is not always `TwoWays()`.** `TwoWays()` literally means "apply the same mappings in reverse." If the reverse direction had its own `ForMember` overrides in AutoMapper, `TwoWays()` will silently lose them. **Rule of thumb:** if the AutoMapper `ReverseMap()` was followed by even one `ForMember`, write a second explicit `NewConfig<D,S>()` block instead.

2. **Deep-path null handling differs.** AutoMapper silently returns `null`/default if `src.Address.City` is hit with `src.Address == null`. Mapster's default throws `NullReferenceException` on deep lambdas. Either wrap the lambda with a null check — `s => s.Address == null ? null : s.Address.City` — or configure `config.Default.NameMatchingStrategy(NameMatchingStrategy.Flexible)` and use nested source path syntax.

3. **Flattening is not automatic.** AutoMapper maps `src.Address.City` to `dest.AddressCity` by convention. Mapster does not. If your DTO has flattened names, you need explicit `.Map(d => d.AddressCity, s => s.Address.City)`. `grep` your DTOs for flattened fields before you assume they still work.

4. **Collections of reference types.** Mapster creates new instances of element types when mapping collections. If your AutoMapper config was relying on `MaxDepth` or `PreserveReferences` to avoid cycles, those features exist in Mapster under `config.Default.PreserveReference(true)` — but they are off by default. Check any recursive graph (parent/child, tree entities) before you ship.

5. **DateTimeOffset ↔ DateTime is not a free conversion.** AutoMapper silently converts between `DateTimeOffset` and `DateTime` via built-in type converters. Mapster treats both as immutable types and throws `InvalidOperationException: Cannot convert immutable type`. You need explicit global converters registered at the top of your `IRegister`:

    ```csharp
    config.NewConfig<DateTimeOffset, DateTime>()
        .MapWith(src => src.DateTime);
    config.NewConfig<DateTime, DateTimeOffset>()
        .MapWith(src => new DateTimeOffset(src, TimeSpan.Zero));
    config.NewConfig<DateTimeOffset?, DateTime?>()
        .MapWith(src => src.HasValue ? (DateTime?)src.Value.DateTime : null);
    config.NewConfig<DateTime?, DateTimeOffset?>()
        .MapWith(src => src.HasValue ? (DateTimeOffset?)new DateTimeOffset(src.Value, TimeSpan.Zero) : null);
    ```

    Without these, any mapping where the source has `DateTimeOffset` and the destination has `DateTime` (or vice versa) — including through navigation properties or nested DTOs — will fail at `Compile()` time.

6. **Hidden base-class members cause `AmbiguousMatchException`.** If a DTO or entity declares `public DateTimeOffset CreatedAt` that hides an inherited `public DateTime CreatedAt` from a base class — even if the `new` keyword is present — .NET reflection's `Type.GetProperty("CreatedAt")` throws `AmbiguousMatchException` because both properties exist in the type hierarchy. AutoMapper handled this internally; Mapster does not.

    **Detection:** grep for any `DateTimeOffset` property whose name matches a `DateTime` property on the base class (`CreatedAt`, `UpdatedAt` are the usual suspects).

    **Fix:** use `config.Default.Settings.ShouldMapMember` to globally skip auto-discovery of the ambiguous member names, then copy them manually via `AfterMapping` where needed:

    ```csharp
    // In your IRegister.Register() — before any NewConfig calls
    config.Default.Settings.ShouldMapMember.Add((member, side) =>
        member.Name == "CreatedAt" || member.Name == "UpdatedAt"
            ? (bool?)false
            : null);

    // Then in specific configs that need CreatedAt/UpdatedAt copied:
    config.NewConfig<Bordereau, BordereauDto>()
        .AfterMapping((src, dest) =>
        {
            dest.CreatedAt = src.CreatedAt;
            dest.UpdatedAt = src.UpdatedAt;
        });
    ```

    This bypasses Mapster's reflection entirely for those members — the C# compiler resolves the property at compile time via the expression tree, so the ambiguity never surfaces.

### How to attack the rewrite

On a codebase with a single `MappingProfile.cs` containing 81 `CreateMap` calls, ~210 `ForMember` invocations, and 63 `ReverseMap()` calls, the rewrite takes about 3–5 hours of focused work. The method that scales best:

1. Copy the whole `MappingProfile.cs` next to the new `MappingRegister.cs`.
2. Work top to bottom. For each `CreateMap` block, paste the Mapster equivalent below it, then delete the AutoMapper original when the new one compiles.
3. Run `dotnet build` frequently — Mapster surfaces type mismatches at compile time, not at startup, which is a genuine ergonomic win over AutoMapper.
4. When you hit a `ReverseMap` with overrides, split it immediately rather than trying to make `TwoWays()` work. It takes the same amount of time and it is obviously correct.

---

## Step 5 — Fix tests

Two tiny things.

**`Mock<IMapper>`.** The codemod already rewrote `AutoMapper.IMapper` to `MapsterMapper.IMapper`. Any tests that mocked `IMapper` compile unchanged — the `Map<T>(src)` signature is identical.

**Configuration validation tests.** If you had a test like:

```csharp
[Fact]
public void MappingConfiguration_is_valid()
{
    var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
    config.AssertConfigurationIsValid();
}
```

Replace with the Mapster equivalent:

```csharp
[Fact]
public void MappingConfiguration_is_valid()
{
    var config = new TypeAdapterConfig();
    config.Scan(typeof(MappingRegister).Assembly);
    config.Compile(); // throws at compile time if any mapping is invalid
}
```

`Compile()` is Mapster's equivalent of `AssertConfigurationIsValid` — it forces every registered mapping to actually build its delegate, so any missing target member or bad lambda surfaces as an exception at test time rather than in production.

---

## Step 6 — Verify before you merge

This is a migration where the worst regressions are silent — a `TwoWays()` that dropped a reverse override, a null-path that now throws, a flattened field that is suddenly `null`. Do not skip verification.

**Gate 1 — compile:** `dotnet build YourApp.sln`. Zero warnings about AutoMapper types.

**Gate 2 — tests:** `dotnet test`. Everything green. Pay attention to any functional mapping test that had a hand-written `Should().Be(...)` — those are exactly the ones that catch silent semantic changes.

**Gate 3 — round-trip every former `ReverseMap`:** For each pair that was `ReverseMap()`, write (or confirm you already have) a round-trip test:

```csharp
var dto = original.Adapt<UserDto>();
var back = dto.Adapt<User>();
back.Should().BeEquivalentTo(original, opts => opts.Excluding(x => x.Audit));
```

**Gate 4 — nullable nested paths:** For every mapping that reaches into a nullable navigation (`s.Adresse.Ville`, `s.Compte.Numero`, etc.), run at least one test with the navigation set to `null`. This is the single highest source of post-merge regressions in my experience.

**Gate 5 — smoke:** start the API, hit the endpoints that exercise the biggest mappings, diff the JSON against a baseline captured from `master` before the migration. Five minutes of manual clicking is cheaper than a production rollback.

Only when all five gates are green do you merge.

---

## Advanced scenarios (read only if your audit flagged them)

### You were using `ProjectTo<T>` for IQueryable projection

AutoMapper's `ProjectTo<T>` compiles mappings into `IQueryable` expression trees so the database does the projection. Mapster's equivalent is `ProjectToType<T>` from the `Mapster` namespace:

```csharp
// Before
var dtos = await db.Users.ProjectTo<UserDto>(mapperConfig).ToListAsync();

// After
var dtos = await db.Users.ProjectToType<UserDto>().ToListAsync();
```

You do **not** need `Mapster.EFCore` for this unless you hit edge cases with `IQueryable` providers that need EF-specific expression rewrites. Start without it and add it only if a query fails to translate.

### You were using `ITypeConverter` / `IValueResolver`

Mapster's equivalent is a converter lambda registered on the config, or `MapContext.Current.GetService<T>()` to pull a service inside a `.Map()` lambda:

```csharp
config.NewConfig<Source, Destination>()
    .Map(d => d.Formatted,
         s => MapContext.Current.GetService<IFormatter>().Format(s.Raw));
```

The lifetime of `ServiceMapper` in DI must allow those service dependencies (scoped is the safe default).

### You have inheritance mappings with `Include<>`

Mapster supports this with the same shape:

```csharp
config.NewConfig<Animal, AnimalDto>()
    .Include<Dog, DogDto>()
    .Include<Cat, CatDto>();
```

Verify each subtype round-trips — Mapster's behavior with polymorphic collections occasionally differs from AutoMapper's and needs explicit testing.

---

## When to reach for `Mapster.Tool` (source generation)

Everything in this guide uses Mapster's runtime, reflection-based path. That is the right default: it is what makes the one-branch migration tractable. But Mapster also ships a source generator (`Mapster.Tool`) that produces hand-written-equivalent mapping code at build time. Consider it as a **second, separate PR** after the migration has stabilized if any of the following apply:

- You are building for **Native AOT** or aggressive trimming — the reflection path will not survive trimming.
- You have a hot mapping path that shows up in profiler traces (high-throughput API, streaming pipeline).
- You want mapping errors to surface as **compile errors** instead of startup errors.
- You want to step through the generated mapping code in the debugger.

Adding source generation on top of an already-migrated Mapster codebase is incremental and low-risk. Doing it *as part of* the initial migration is not — it doubles the number of moving parts. One change at a time.

---

## Closing notes

The single mental shift that makes this migration cheap is: **treat `IMapper` as a stable contract and migrate everything behind it in one branch.** Every guide that recommends building an abstraction layer, running both libraries in parallel for weeks, or doing "bounded-context-by-bounded-context" cutovers is solving a harder problem than you have. If your audit in Step 0 was clean, you do not need any of that. A PowerShell script, a cheatsheet, and an afternoon of focused profile rewriting will get you there.

If your audit was not clean — if you have `ProjectTo`, custom resolvers, or large inheritance graphs — fall back on the phased approach. But do not reach for it preemptively. The fewer moving parts in a migration, the fewer regressions.

---

### Sources

- [Mapster on GitHub](https://github.com/MapsterMapper/Mapster)
- [Mapster Wiki — Basic usage](https://github.com/MapsterMapper/Mapster/wiki/Basic-usage)
- [Mapster Wiki — Dependency Injection](https://github.com/MapsterMapper/Mapster/wiki/Dependency-Injection)
- [AutoMapper vs Mapster — Code Maze](https://code-maze.com/automapper-vs-mapster-dotnet/)
- [AutoMapper goes commercial: should you switch to Mapster? — Dino Cosic](https://medium.com/@dino.cosic/automapper-is-now-commercial-should-net-developers-switch-to-mapster-25445581d38c)
