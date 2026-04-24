# AutoMapper → Mapster cheatsheet

**Pin this to your second monitor while you rewrite `Profile` classes.**

For the full migration guide (audit, package swap, DI rewrite, verify gates, gotchas), see [`automapper-to-mapster-migration.md`](automapper-to-mapster-migration.md).

## The mapping table

| AutoMapper | Mapster |
|---|---|
| `CreateMap<S, D>()` | `config.NewConfig<S, D>()` |
| `.ForMember(d => d.X, o => o.MapFrom(s => s.Y))` | `.Map(d => d.X, s => s.Y)` |
| `.ForMember(d => d.X, o => o.Ignore())` | `.Ignore(d => d.X)` |
| `.ForMember(d => d.X, o => o.MapFrom(s => s.Y ?? "default"))` | `.Map(d => d.X, s => s.Y ?? "default")` |
| `.ForMember(d => d.X, o => o.Condition(s => s.Y != null))` | `.Map(d => d.X, s => s.Y, srcCond: s => s.Y != null)` |
| `.AfterMap((s, d) => ...)` | `.AfterMapping((s, d) => ...)` (note the `-ping`) |
| `.BeforeMap((s, d) => ...)` | `.BeforeMapping((s, d) => ...)` |
| `.ReverseMap()` (no overrides) | `.TwoWays()` |
| `.ReverseMap()` + overrides on reverse side | **second explicit `NewConfig<D, S>()` block** (safer) |
| `Profile` subclass | `IRegister` implementation (scanned the same) |
| `_mapper.Map<D>(source)` | `_mapper.Map<D>(source)` — **unchanged** |
| `_mapper.Map(source, destination)` | `_mapper.Map(source, destination)` — **unchanged** |
| `ProjectTo<D>(config)` | `ProjectToType<D>()` (requires `Mapster.EFCore`) |
| `ITypeConverter<S, D>` | `.MapWith(src => customConverter.Convert(src))` |
| `IValueResolver<S, D, TMember>` | inline lambda in `.Map(...)` or named method group |
| `Include<TOther>()` (inheritance) | `.Include<S, D>()` — see guide for caveats |
| `.ConstructUsing(s => new D(s.X))` | `.ConstructUsing(s => new D(s.X))` (same syntax) |
| `AddAutoMapper(typeof(T).Assembly)` | `config.Scan(typeof(T).Assembly)` + `AddSingleton(config)` + `AddScoped<IMapper, ServiceMapper>()` |
| `AssertConfigurationIsValid()` | `config.Compile()` (run in a unit test) |

## The four global converters you almost certainly need

Put these at the top of your `IRegister.Register(...)` body before any `NewConfig` calls. AutoMapper converted these silently; Mapster throws without them.

```csharp
public void Register(TypeAdapterConfig config)
{
    // DateTime <-> DateTimeOffset
    config.NewConfig<DateTime, DateTimeOffset>()
        .MapWith(src => new DateTimeOffset(DateTime.SpecifyKind(src, DateTimeKind.Utc)));
    config.NewConfig<DateTimeOffset, DateTime>()
        .MapWith(src => src.UtcDateTime);

    // Nullable variants
    config.NewConfig<DateTime?, DateTimeOffset?>()
        .MapWith(src => src.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(src.Value, DateTimeKind.Utc))
            : (DateTimeOffset?)null);
    config.NewConfig<DateTimeOffset?, DateTime?>()
        .MapWith(src => src.HasValue ? src.Value.UtcDateTime : (DateTime?)null);

    // ...your NewConfig<S,D>() blocks below
}
```

## Canonical `IRegister` skeleton

```csharp
using Mapster;

public sealed class MappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // 1. Global converters (above) go first.

        // 2. Then your explicit mappings, one per direction.
        config.NewConfig<User, UserDto>()
            .Map(d => d.FullName, s => s.FirstName + " " + s.LastName)
            .Map(d => d.Email,    s => s.EmailAddress)
            .Ignore(d => d.Audit);

        // 3. Reverse direction with its OWN overrides — never TwoWays when there
        //    are reverse-side customizations. TwoWays silently drops them.
        config.NewConfig<UserDto, User>()
            .Ignore(d => d.FirstName)
            .Ignore(d => d.LastName)
            .Map(d => d.EmailAddress, s => s.Email);
    }
}
```

## The one test you must write

```csharp
[Fact]
public void Mapping_configuration_is_valid()
{
    var config = new TypeAdapterConfig();
    config.Scan(typeof(MappingRegister).Assembly);
    config.Compile(); // throws at test time if any mapping can't build its delegate
}
```

This is Mapster's equivalent of `AssertConfigurationIsValid()`. Run it in CI. It will catch roughly 60% of post-migration regressions before they reach a branch.

## See also

- [Full migration guide](automapper-to-mapster-migration.md)
- [Before/after example](../examples/before-after/README.md)
- [Mapster docs](https://github.com/MapsterMapper/Mapster/wiki)
