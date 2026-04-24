// Before — AutoMapper Profile
//
// This is the canonical AutoMapper style: one Profile subclass,
// CreateMap calls with ForMember overrides, ReverseMap for bidirectional pairs,
// AfterMap for post-mapping mutation.
//
// Everything in this file needs to be rewritten. The rest of your codebase
// (the _mapper.Map<T>(src) call sites) does NOT need changes.

using System;
using AutoMapper;

namespace YourApp.Application.Mappings;

public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        // ----------------------------------------------------------------
        // User <-> UserDto  (bidirectional with reverse-side overrides)
        // ----------------------------------------------------------------
        CreateMap<User, UserDto>()
            .ForMember(d => d.FullName, o => o.MapFrom(s => $"{s.FirstName} {s.LastName}"))
            .ForMember(d => d.Email,    o => o.MapFrom(s => s.EmailAddress))
            .ForMember(d => d.Audit,    o => o.Ignore())

            // ReverseMap() + overrides: the most common silent-regression trap.
            // Mapster's TwoWays() cannot express the overrides below — it drops them.
            .ReverseMap()
                .ForMember(d => d.FirstName,    o => o.Ignore())
                .ForMember(d => d.LastName,     o => o.Ignore())
                .ForMember(d => d.EmailAddress, o => o.MapFrom(s => s.Email));

        // ----------------------------------------------------------------
        // Order -> OrderDto  (AfterMap + nested child)
        // ----------------------------------------------------------------
        CreateMap<Order, OrderDto>()
            .ForMember(d => d.Total, o => o.MapFrom(s => s.Items.Sum(i => i.Price)))
            .AfterMap((src, dst) => dst.GeneratedAt = DateTime.UtcNow);

        CreateMap<OrderItem, OrderItemDto>();  // no overrides — pure name-match

        // ----------------------------------------------------------------
        // Audit value object — DateTimeOffset <-> DateTime silent conversion
        // (AutoMapper did this for free; Mapster will NOT)
        // ----------------------------------------------------------------
        CreateMap<Audit, AuditDto>()
            .ForMember(d => d.CreatedAt, o => o.MapFrom(s => s.CreatedAt))
            .ForMember(d => d.UpdatedAt, o => o.MapFrom(s => s.UpdatedAt));
    }
}

// ==== domain ====

public sealed record User(
    string FirstName,
    string LastName,
    string EmailAddress,
    Audit Audit);

public sealed record Audit(DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

public sealed record Order(Guid Id, IReadOnlyList<OrderItem> Items);
public sealed record OrderItem(string Sku, decimal Price);

// ==== DTOs (what the API returns) ====

public sealed class UserDto
{
    public string? FullName { get; set; }
    public string? Email    { get; set; }
    public object? Audit    { get; set; }   // ignored on the way down
}

public sealed class AuditDto
{
    public DateTime  CreatedAt { get; set; }  // note: DateTime, not DateTimeOffset
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
