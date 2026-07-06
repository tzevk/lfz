using LFZ.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LFZ.Application.Abstractions;

/// <summary>
/// Persistence abstraction consumed by Application services.
/// Implemented by LFZ.Infrastructure.ApplicationDbContext.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Plot> Plots { get; }
    DbSet<Tenant> Tenants { get; }
    DbSet<PlotRequest> PlotRequests { get; }
    DbSet<PlotTenantBlock> PlotTenantBlocks { get; }
    DbSet<PlotStatusHistory> PlotStatusHistory { get; }
    DbSet<AppSetting> AppSettings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
