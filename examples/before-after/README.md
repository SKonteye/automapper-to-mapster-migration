# Before / After — a minimal worked example

This example shows the same set of mappings written twice: once as an AutoMapper `Profile`, once as a Mapster `IRegister`. Read them side by side.

## Files

| File | What it is |
|---|---|
| [`before.cs`](before.cs) | AutoMapper `Profile` — what you start with. |
| [`after.cs`](after.cs) | Mapster `IRegister` — what you end up with. |

## What these mappings cover

Between them they exercise every non-trivial transformation you're likely to hit:

- `.ForMember` with `MapFrom` (projection)
- `.ForMember` with `Ignore`
- `.ReverseMap()` with reverse-side overrides — the gotcha in the wild
- `.AfterMap` (post-mapping mutation)
- Nested child mappings
- `DateTimeOffset` ↔ `DateTime` conversion (which AutoMapper did silently and Mapster will not)

## The pattern to internalize

When `ReverseMap()` has its own `ForMember` overrides, **do not** use `TwoWays()` — it silently drops them. Always write a second explicit `NewConfig<D, S>()` block. You'll see this in `after.cs`: for the `User ↔ UserDto` pair, the forward and reverse directions are declared separately, even though it's a bit more typing.

## Not in this example

Things that require extra work beyond the mechanical rewrite (see [`docs/automapper-to-mapster-migration.md`](../../docs/automapper-to-mapster-migration.md#advanced-scenarios-read-only-if-your-audit-flagged-them) for these):

- `ProjectTo<T>` against `IQueryable` → `ProjectToType<T>` (needs `Mapster.EFCore`)
- `ITypeConverter<S, D>` → `.MapWith(...)` with a named converter
- `IValueResolver<S, D, TMember>` → inline lambdas or method groups
- `Include<TOther>()` inheritance mappings → `.Include<S, D>()` with caveats
