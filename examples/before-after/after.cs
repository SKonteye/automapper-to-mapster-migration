// After — Mapster IRegister
//
// Same mappings as before.cs, rewritten for Mapster. Note what changed
// (and what did NOT):
//
//   * Profile subclass -> IRegister implementation
//   * CreateMap<S,D>() -> config.NewConfig<S,D>()
//   * .ForMember(d => d.X, o => o.MapFrom(s => s.Y)) -> .Map(d => d.X, s => s.Y)
//   * .ForMember(d => d.X, o => o.Ignore()) -> .Ignore(d => d.X)
//   * .ReverseMap() with overrides -> SECOND explicit NewConfig<D,S>() block
//       (NOT TwoWays() — it would silently drop the reverse-side overrides)
//   * .AfterMap -> .AfterMapping (note the "-ping")
//   * DateTimeOffset <-> DateTime now needs EXPLICIT converters at the top
//       (AutoMapper did this silently; Mapster throws without them)
//
// What did NOT change: the _mapper.Map<T>(src) call sites in your controllers,
// handlers, services. That's the whole reason this migration is cheap.

using System;
using Mapster;

namespace YourApp.Application.Mappings;

public sealed class MappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // ----------------------------------------------------------------
        // Global converters — always FIRST, before any NewConfig<> blocks
        // ----------------------------------------------------------------
        config.NewConfig<DateTimeOffset, DateTime>()
            .MapWith(src => src.UtcDateTime);

        config.NewConfig<DateTimeOffset?, DateTime?>()
            .MapWith(src => src.HasValue ? src.Value.UtcDateTime : (DateTime?)null);

        config.NewConfig<DateTime, DateTimeOffset>()
            .MapWith(src => new DateTimeOffset(DateTime.SpecifyKind(src, DateTimeKind.Utc)));

        config.NewConfig<DateTime?, DateTimeOffset?>()
            .MapWith(src => src.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(src.Value, DateTimeKind.Utc))
                : (DateTimeOffset?)null);

        // ----------------------------------------------------------------
        // User -> UserDto  (forward direction)
        // ----------------------------------------------------------------
        config.NewConfig<User, UserDto>()
            .Map(d => d.FullName, s => s.FirstName + " " + s.LastName)
            .Map(d => d.Email,    s => s.EmailAddress)
            .Ignore(d => d.Audit);

        // ----------------------------------------------------------------
        // UserDto -> User  (reverse direction — explicit, NOT TwoWays)
        //
        // Reason: the reverse side had ForMember overrides in before.cs.
        // TwoWays() would silently drop them. Write it out.
        // ----------------------------------------------------------------
        config.NewConfig<UserDto, User>()
            .Ignore(d => d.FirstName)
            .Ignore(d => d.LastName)
            .Map(d => d.EmailAddress, s => s.Email);

        // ----------------------------------------------------------------
        // Order -> OrderDto  (AfterMapping + nested child)
        // ----------------------------------------------------------------
        config.NewConfig<Order, OrderDto>()
            .Map(d => d.Total, s => s.Items.Sum(i => i.Price))
            .AfterMapping((src, dst) => dst.GeneratedAt = DateTime.UtcNow);

        // OrderItem -> OrderItemDto — pure name-match, no overrides needed.
        // Mapster will discover it via config.Scan, but declaring it
        // explicitly makes the intent visible and makes Compile() catch
        // any future field drift.
        config.NewConfig<OrderItem, OrderItemDto>();

        // ----------------------------------------------------------------
        // Audit -> AuditDto
        //
        // Name-match would almost work, BUT the destination's CreatedAt is
        // DateTime (not DateTimeOffset). The global converters above handle
        // the type mismatch; no per-property Map(...) needed here.
        // ----------------------------------------------------------------
        config.NewConfig<Audit, AuditDto>();
    }
}

// ==== domain (unchanged from before.cs) ====

public sealed record User(
    string FirstName,
    string LastName,
    string EmailAddress,
    Audit Audit);

public sealed record Audit(DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

public sealed record Order(Guid Id, IReadOnlyList<OrderItem> Items);
public sealed record OrderItem(string Sku, decimal Price);

// ==== DTOs (unchanged from before.cs) ====

public sealed class UserDto
{
    public string? FullName { get; set; }
    public string? Email    { get; set; }
    public object? Audit    { get; set; }
}

public sealed class AuditDto
{
    public DateTime  CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class OrderDto
{
    public Guid                         Id          { get; set; }
    public IReadOnlyList<OrderItemDto>? Items       { get; set; }
    public decimal                      Total       { get; set; }
    public DateTime                     GeneratedAt { get; set; }
}

public sealed class OrderItemDto
{
    public string  Sku   { get; set; } = "";
    public decimal Price { get; set; }
}
